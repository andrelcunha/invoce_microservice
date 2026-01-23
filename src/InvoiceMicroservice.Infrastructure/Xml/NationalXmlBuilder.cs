using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Xml;

public class NationalXmlBuilder : IInvoiceXmlBuilder
{
    private readonly IServiceTypeTaxMappingRepository _serviceTaxRepo;
    private readonly IMunicipalityRepository _municipalityRepo;
    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly ILogger<NationalXmlBuilder> _logger;


    public NationalXmlBuilder(
        IServiceTypeTaxMappingRepository serviceTaxRepo,
        IMunicipalityRepository municipalityRepo,
        IPortalCredentialsRepository credentialsRepo,
        ILogger<NationalXmlBuilder> logger)
    {
        _serviceTaxRepo = serviceTaxRepo;
        _municipalityRepo = municipalityRepo;
        _credentialsRepo = credentialsRepo;
        _logger = logger;
    }

    public PortalType GetPortalType() => PortalType.Nacional;

    public async Task<string> BuildInvoiceXmlAsync(Invoice invoice, bool isTestMode = true, CancellationToken cancellationToken = default)
    {
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;
        _logger.LogInformation("Building XML for invoice {InvoiceId} issued by {IssuerCnpj}", invoice.Id, issuer.Cnpj);
        var consumer = JsonSerializer.Deserialize<Consumer>(invoice.ConsumerData)!;
        var issuer_cnpj = Helpers.StripDots(issuer.Cnpj);
        var credentials = await _credentialsRepo.GetByIssuerCnpjAsync(issuer_cnpj, cancellationToken);
        if (credentials == null)
            throw new InvalidOperationException($"No portal credentials found for issuer CNPJ {issuer_cnpj}");

        var root = new XElement("DPS", new XAttribute("versao", "2.0"));
        int serie = 1; // Hardcoded for MVP
        int numero = 1; // Hardcoded for MVP TODO: Find a way to get real series/number
        var serviceCodes = await GetServiceCodesAsync(invoice.ServiceTypeKey, issuer.Cnae, cancellationToken);

        var infDps = await BuildInfDpsAsync(invoice, issuer, consumer, serviceCodes,  serie, numero, isTestMode, cancellationToken);
        root.Add(infDps);
        XmlDocument xmlDoc = new()
        {
            PreserveWhitespace = false,
        };
        xmlDoc.LoadXml(root.ToString(SaveOptions.DisableFormatting));
        // sign the XML
        string signedXml = SignXml(xmlDoc, credentials!.CertificateData!, credentials.CertificatePasswordHash!);
        // root.Add(XElement.Parse(signedXml));
        var doc = new XDocument(new XDeclaration("1.0", "utf-8",null), signedXml);
        var xmlString = doc.Declaration!.ToString() + Environment.NewLine + doc.ToString(SaveOptions.None);
        return xmlString;
    }

    private string BuildDpsId(string cnpj,  string codMun, int serie, int numero)
    {
        // A formação do identificador da DPS
        var builder = new System.Text.StringBuilder();
        builder.Append("DPS");
        // cod. municipal (7)
        builder.Append(codMun.PadLeft(7, '0'));
        // Tipo de inscrição federal (1) // 1=CPF, 2=CNPJ
        builder.Append(cnpj.Length == 11 ? "1" : "2");
        // inscrição federal (14)  Se for CPF completar com 000 à esquerda
        builder.Append(Helpers.OnlyDigits(cnpj).PadLeft(14, '0'));
        // Serie (5)
        builder.Append(serie.ToString().PadRight(5, '0'));
        // Número da DPS (15)
        builder.Append(numero.ToString().PadLeft(15, '0'));
        return builder.ToString();
    }

    private async Task<XElement> BuildInfDpsAsync(Invoice invoice, Issuer issuer,  Consumer consumer, ServiceTypeTaxCodes serviceCodes, int serie, int numero, bool isTestMode, CancellationToken ct)
    {
        var codMun = await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, ct);
        var codMunToma = await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, ct);

        var id = BuildDpsId(issuer.Cnpj, codMun, serie, numero); 
        var infDps = new XElement("infDPS", new XAttribute("Id", id));
        // tpAmp - Tipo de Ambiente (1=Produção, 2=Homologação)
        infDps.Add(new XElement("tpAmb", isTestMode ? "2" : "1")); // 1 - Produção, 2 - Homologação
        // dhEmi - Data e Hora de Emissão (ISO 8601)
        infDps.Add(new XElement("dhEmi", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        // verAplic - Versão do Aplicativo
        infDps.Add(new XElement("verAplic", "1.0.0")); // Hardcoded for MVP
        // serie
        infDps.Add(new XElement("serie", serie));
        // numero
        infDps.Add(new XElement("nDPS", numero));
        infDps.Add(new XElement("dCompet", invoice.IssuedAt?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")));
        //  tpEmit - Emitente da DPS (1=Prestador de Serviço, 2=Tomador de Serviço, 3=Intermediário)
        infDps.Add(new XElement("tpEmit", "1")); // Always Prestador 
        // cMotivoEmisTI - Código do Motivo de Emissão em Sistema pelo tomador/intermediário
        // chNFSeRej - Chave da NFS-e rejeitada
        // cLocEmi - Código do Local de Emissão (7) Código IBGE
        infDps.Add(new XElement("cLocEmi", codMun));
        // grupo 'subst' -- não se aplica
        infDps.Add(BuildDpsPrest(issuer, codMun));
        infDps.Add(BuildDpsToma(consumer, codMunToma));
        // grupo 'interm' -- não se aplica
        infDps.Add(await BuildServAsync(invoice, issuer, serviceCodes, ct));
        infDps.Add(BuildValoresAsync(invoice));
        infDps.Add(await BuildIbsCbsAsync(invoice, issuer, consumer, serviceCodes, ct));
        return infDps;
    }

    private XElement BuildDpsPrest(Issuer issuer, string codMun)
    {
        var prest = new XElement("prest");
        if (issuer.Cnpj.Length == 11)
            prest.Add(new XElement("CPF", Helpers.OnlyDigits(issuer.Cnpj)));
        else
            prest.Add(new XElement("CNPJ", Helpers.OnlyDigits(issuer.Cnpj)));
        // NIF --  não preenchido se tpEmit = 1
        // cNaoNIF --  não preenchido se tpEmit = 1
        // CAEPF -- não preenchido se tpEmit = 1
        // prest.Add(new XElement("IM", issuer.MunicipalInscription));
        prest.Add(new XElement("xNome", Helpers.EscapeXmlContent(issuer.Name)));
        prest.Add(BuildEndElement(issuer.Address, codMun));
        prest.Add(new XElement("fone", Helpers.OnlyDigits("")));
        prest.Add(new XElement("email", Helpers.EscapeXmlContent("")));

        var regTrib = new XElement("regTrib");
        // opSimpNac - Optante do Simples Nacional (1=Nao, 2= MEI, 3= ME/EPP)
        int opSimNac = EvaluateSimplesNacionalRegime(issuer);
        regTrib.Add(new XElement("opSimpNac", opSimNac));
        // regApTribSN - Regime de Apuração do Simples Nacional
        if (opSimNac == 3)
            regTrib.Add(new XElement("regApTribSN", EvaluateRegApTribSN(issuer)));
        // regEspTrib - Regime Especial de Tributação
        regTrib.Add(new XElement("regEspTrib", EvaluateRegEspTrib(issuer)));
        // regEspTrib
        prest.Add(regTrib);
        return prest;
    }

    private static XElement BuildEndElement(Address address, string codMun)
    {
        var end = new XElement("end");
        var endNac = new XElement("endNac");
        endNac.Add(new XElement("cMun", codMun)); // To be filled with IBGE code
        endNac.Add(new XElement("CEP", Helpers.OnlyDigits(address.ZipCode)));
        end.Add(endNac);
        end.Add(new XElement("xLgr", Helpers.EscapeXmlContent(address.Street)));
        end.Add(new XElement("nro", address.Number));
        if (string.IsNullOrEmpty(address.Complement) == false)
        {
            end.Add(new XElement("xCpl", Helpers.EscapeXmlContent(address.Complement ?? "")));
        }
        end.Add(new XElement("xBairro", Helpers.EscapeXmlContent(address.Neighborhood)));
        // endExt -- omitido no prestador nacional
        return end;
    }

    private static int EvaluateSimplesNacionalRegime(Issuer issuer)
    {
        //Optante do Simples Nacional (1=Nao, 2= MEI, 3= ME/EPP)
        int opSimNac;
        if (issuer.RegimeTributario == RegimeTributario.SimplesNacional)
        {
            if (issuer.SubRegimeTributario == SubRegimeTributario.MEI)
                opSimNac = 2;
            else
                opSimNac = 3;
        }
        else
        {
            opSimNac = 1;
        }

        return opSimNac;
    }

    private static int EvaluateRegEspTrib(Issuer issuer)
    {
        // 0 - Nenhum; 
        //       - Se tribISSQN = [2,3,4] ou 
        //      -  (opSimpNac = 2) ou 
        //      -  (opSimpNac = 3 e regApTribSN = 1)

        // 1 - Ato Cooperado;
        // 2 - Estimativa;
        // 3 - Microempresa municipal;
        // 4 - Notário ou Registrador;
        // 5 - Profissional Autônomo;
        // 6 - Sociedade de Profissionais;
        return 0; // TODO: Implement logic if needed
    }
    private static int EvaluateRegApTribSN(Issuer issuer)
    {
        // TODO: 
        // Implement logic to evaluate Regime de Apuração do Simples Nacional based on issuer details
        // Placeholder implementation
        // 1- Regime de apuração dos tributos federais e municipais pelo SN;
        // 2 - Regime de apuração dos tributos federais pelo SN e o ISSQN pela NFS-e conforme legislação municipal;
        // 3 - Regime de apuração dos tributos federais e municipais pela NFS-e conforme legislação municipal.
        return 1; // Example value
    }
    private XElement BuildDpsToma(Consumer consumer, string codMun)
    {
        var tom = new XElement("toma");
        if (consumer.CpfCnpj.Length == 11)
            tom.Add(new XElement("CPF", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else
            tom.Add(new XElement("CNPJ", Helpers.OnlyDigits(consumer.CpfCnpj)));
        // NIF -- omitido se tpEmit = 1
        // cNaoNIF -- omitido se tpEmit = 1
        tom.Add(new XElement("xNome", Helpers.EscapeXmlContent(consumer.Name)));
        var end = BuildEndElement(consumer.Address, codMun);
        tom.Add(end);
        tom.Add(new XElement("fone", Helpers.OnlyDigits(consumer.Phone ?? "")));
        tom.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));
        return tom;
    }

    private async Task<XElement> BuildServAsync(Invoice invoice, Issuer issuer, ServiceTypeTaxCodes codes, CancellationToken ct)
    {
        var servico = new XElement("serv");

        var locPrest = new XElement("locPrest");
        locPrest.Add(new XElement("cLocPrestacao", await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, ct)));
        servico.Add(locPrest);

        var cServ = new XElement("cServ");
        // cTribNac - Código de Tributação Nacional do ISSQN
        cServ.Add(new XElement("cTribNac", codes.ServiceListCode));
        // cTribMun
        cServ.Add(new XElement("cTribMun", invoice.MunicipalTaxCode));
        // xDescServ
        cServ.Add(new XElement("xDescServ", codes.Description));
        // cNBS
        cServ.Add(new XElement("cNBS", codes.NbsCode));
        // cServ.Add(new XElement("cIntContrib", invoice.Id.ToString()));  // cIntContrib - Código interno do contribuinte (id no Sistema Interno do Contribuinte)
        servico.Add(cServ);
        // grupo 'comExt' (comércio exterior) -- omitido para nacional
        // grupo 'obra' (obras de construção civil)
        // grupo 'atvEvento' (atividades com eventos)
        // grupo 'infCompl' (informações complementares)

        return servico;
    }

    private XElement BuildValoresAsync(Invoice invoice)
    {
        // grupo valores
        var valores = new XElement("valores");
        var vServPrest = new XElement("vServPrest");
        var temIntermediario = false; // Hardcoded for MVP
        if (temIntermediario)
        {
            vServPrest.Add(new XElement("vReceb", Helpers.FormatMonetary(invoice.Amount))); // valor recebido pelo intermediário  
        }
        vServPrest.Add(new XElement("vServ", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(vServPrest);

        // var vDescCondIncond = new XElement("vDescCondIncond");
        // vDescCondIncond.Add(new XElement("vDescIncond", Helpers.FormatMonetary(0))); // No unconditional discount
        // vDescCondIncond.Add(new XElement("vDescCond", Helpers.FormatMonetary(0))); // No conditional discount
        // valores.Add(vDescCondIncond);

        // var vDedRed = new XElement("vDedRed");
        var trib = new XElement("trib");
        var tribMun = new XElement("tribMun");
        var tribISSQN = invoice.IssRate > 0 ? 2 : 1;
        tribMun.Add(new XElement("tribISSQN", tribISSQN)); // 1 = Tributável, 2 = Imunidade, 3 - Exportação, 4 - Não Incidência
        var tpRetISSQN = tribISSQN == 1 ? invoice.IssRate > 0 ? 2 : 1 : 1;
        tribMun.Add(new XElement("tpRetISSQN", tpRetISSQN)); // 1 = Não Retido, 2 = Retido pelo tomador 3 = Retido pelo intermediário
        if (tribISSQN == 1)
            tribMun.Add(new XElement("pAliq", Helpers.FormatRate(invoice.IssRate)));
        trib.Add(tribMun);

        var tribFed = new XElement("tribFed");
        var pisCofins = new XElement("pisCofins");
        // Código de Situação Tributária do PIS/COFINS
        pisCofins.Add(new XElement("CTS",  invoice.PisCofinsCts)); 
        pisCofins.Add(new XElement("vBCPisCofins", Helpers.FormatMonetary(invoice.Amount))); // Base de Cálculo
        pisCofins.Add(new XElement("pAliqPis", Helpers.FormatRate(invoice.AliquotaPis))); 
        pisCofins.Add(new XElement("pAliqCofins", Helpers.FormatRate(invoice.AliquotaCofins)));
        pisCofins.Add(new XElement("vPis", Helpers.FormatMonetary(invoice.Amount * invoice.AliquotaPis))); 
        pisCofins.Add(new XElement("vCofins", Helpers.FormatMonetary(invoice.Amount * invoice.AliquotaCofins)));
        pisCofins.Add(new XElement("tpRetPisCofins", invoice.TipoRetencaoPisCofins));
        tribFed.Add(pisCofins);
        trib.Add(tribFed);

        var totTrib = new XElement("totTrib");
        var vTotTrib = new XElement("vTotTrib");
        vTotTrib.Add(new XElement("vTotTribFed", Helpers.FormatMonetary(invoice.Amount * invoice.AliquotaPis)));
        // vTotTrib.Add(new XElement("vTotTribEst", Helpers.FormatMonetary(0)));
        vTotTrib.Add(new XElement("vTotTribMun", Helpers.FormatMonetary(invoice.Amount * invoice.IssRate)));
        totTrib.Add(vTotTrib);
        var pTotTrib = new XElement("pTotTrib");
        pTotTrib.Add(new XElement("pTotTribFed", Helpers.FormatRate((invoice.AliquotaPis  + invoice.AliquotaCofins) * 100)));
        // pTotTrib.Add(new XElement("pTotTribEst", Helpers.FormatRate(0)));
        pTotTrib.Add(new XElement("pTotTribMun", Helpers.FormatRate(invoice.IssRate * 100)));
        totTrib.Add(pTotTrib);
        trib.Add(totTrib);
        valores.Add(trib);
        return valores;
    }

    // private async Task<XElement> BuildTomadorAsync(Consumer consumer, CancellationToken cancellationToken)
    // {
    //     var dest = new XElement("dest");

    //     if (consumer.CpfCnpj.Length == 11)
    //         dest.Add(new XElement("CPF", Helpers.OnlyDigits(consumer.CpfCnpj)));
    //     else
    //         dest.Add(new XElement("CNPJ", Helpers.OnlyDigits(consumer.CpfCnpj)));

    //     // NIF for foreign entities
    //     dest.Add(new XElement("cNaoNIF", "0")); // 0 = Não informado ; 1 = Dispensado; 2 = Não exigência do NIF

    //     dest.Add(new XElement("xNome", Helpers.EscapeXmlContent(consumer.Name)));

    //     var endereco = new XElement("end");

    //     var endNac = new XElement("endNac");
    //     endNac.Add(new XElement("cMun", await GetIbgeCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken)));
    //     endNac.Add(new XElement("CEP", Helpers.OnlyDigits(consumer.Address.ZipCode)));

    //     // var endExt = new XElement("endExt");   // Omit for national
    //     // endExt.Add(new XElement("cPais", "1058")); // Brazil
    //     // endExt.Add(new XElement("cEndPost", "0")); // Not applicable
    //     // endExt.Add(new XElement("xCidade", ""));
    //     // endExt.Add(new XElement("xEstProvReg", ""));

    //     endereco.Add(new XElement("xLgr", Helpers.EscapeXmlContent(consumer.Address.Street)));
    //     endereco.Add(new XElement("nro", consumer.Address.Number));
    //     endereco.Add(new XElement("xCpl", Helpers.EscapeXmlContent(consumer.Address.Complement ?? "")));
    //     endereco.Add(new XElement("xBairro", Helpers.EscapeXmlContent(consumer.Address.Neighborhood)));

    //     endereco.Add(endNac);
    //     dest.Add(endereco);

    //     dest.Add(new XElement("fone", Helpers.OnlyDigits(consumer.Phone ?? "")));
    //     dest.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));

    //     return dest;
    // }

    private async Task<XElement> BuildIbsCbsAsync(Invoice invoice, Issuer issuer, Consumer consumer, ServiceTypeTaxCodes codes, CancellationToken ct)
    {
        var ibscbs = new XElement("IBSCBS");

        // Basic indicators
        ibscbs.Add(new XElement("finNFSe", "0")); // 0 = Regular
        ibscbs.Add(new XElement("indFinal", "1")); // 1 = Final consumer -- this field will be deprecated
        ibscbs.Add(new XElement("cIndOp", codes.OperationIndicator)); // From service codes
        // Government entity type (omit if not applicable)
        // ibscbs.Add(new XElement("tpEnteGov", "1"));

        // Reference NFS-e if applicable (omit for new)
        // ibscbs.Add(new XElement("gRefNFSe", new XElement("refNFSe", "...")));


        // Indicador de Destino da Operação
        var indDest = 1;  // Hardcoded for MVP: 1 = oper
        ibscbs.Add(new XElement("indDest", indDest)); // 0 = tomador=adquirente=destinatario; 1 = tomador diferente do adquirente/destinatario

        // Destinatario (Recipient) - Required for IBS/CBS
        if (indDest == 1)
        {
            var dest = await BuildDestinatarioAsync(consumer, ct);
            ibscbs.Add(dest);
        }

        // Imovel (Property) - Omit unless real estate
        // ibscbs.Add(BuildImovelSection(...));

        // Valores group with reembolsos and tributacao
        var valores = await BuildValoresIbsCbsAsync(invoice, codes, ct);
        ibscbs.Add(valores);

        return ibscbs;
    }

    private async Task<XElement> BuildDestinatarioAsync(Consumer consumer, CancellationToken cancellationToken)
    {
        var dest = new XElement("dest");
        string codMunConsumer = await GetIbgeCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken);


        // ID: CPF/CNPJ/NIF
        if (consumer.CpfCnpj.Length == 11)
            dest.Add(new XElement("CPF", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else if (consumer.CpfCnpj.Length == 14)
            dest.Add(new XElement("CNPJ", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else
            dest.Add(new XElement("NIF", consumer.CpfCnpj)); // Foreign

        dest.Add(new XElement("cNaoNIF", "0")); // Default: Not informed
        dest.Add(new XElement("xNome", Helpers.EscapeXmlContent(consumer.Name)));

        // Address (endNac for national)
        XElement end = BuildEndElement(consumer.Address, codMunConsumer);
        dest.Add(end);

        // Phone and email
        dest.Add(new XElement("fone", Helpers.OnlyDigits(consumer.Phone ?? "")));
        dest.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));

        return dest;
    }

    private async Task<XElement> BuildValoresIbsCbsAsync(Invoice invoice, ServiceTypeTaxCodes codes, CancellationToken cancellationToken)
    {
        var valores = new XElement("valores");

        // gReeRepRes: Reembolsos/Repasses - Omit if none, or add for exclusions from BC
        // For MVP: Assume no reembolsos
        // valores.Add(BuildReeRepResSection(...));

        // Tributacao group
        var trib = new XElement("trib");
        var gIBSCBS = new XElement("gIBSCBS");
        gIBSCBS.Add(new XElement("CST", invoice.IbsCbsCst)); 
        gIBSCBS.Add(new XElement("cClassTrib", invoice.IbsCbsClassTrib)); 
        // gIBSCBS.Add(new XElement("cCredPres", "...")); // Presumed credit code if applicable

        // gTribRegular: Regular taxation details
        var gTribRegular = new XElement("gTribRegular");
        gTribRegular.Add(new XElement("CSTReg", "01")); // Example
        // gTribRegular.Add(new XElement("cClassTribReg", codes.TaxClassificationCode));
        gIBSCBS.Add(gTribRegular);

        // gDif: Deferrals 
        // var gDif = new XElement("gDif");
        // gDif.Add(new XElement("pDifUF", Helpers.FormatRate(_taxConfig.PDifUf))); // State deferral %
        // gDif.Add(new XElement("pDifMun", Helpers.FormatRate(_taxConfig.PDifMun)));
        // gDif.Add(new XElement("pDifCBS", Helpers.FormatRate(_taxConfig.PDifCbs)));
        // gIBSCBS.Add(gDif);

        trib.Add(gIBSCBS);
        valores.Add(trib);

        return valores;
    }

    private async Task<ServiceTypeTaxCodes> GetServiceCodesAsync(string? serviceTypeKey, string? cnaeCode, CancellationToken cancellationToken)
    {
        // Similar to IPM, but with national codes (e.g., ItemListaServico from LC 116)
        // Add IBS/CBS specific codes
        if (!string.IsNullOrEmpty(serviceTypeKey))
        {
            var mapping = await _serviceTaxRepo.GetByServiceTypeKeyAsync(serviceTypeKey, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        if (!string.IsNullOrEmpty(cnaeCode))
        {
            var mapping = await _serviceTaxRepo.GetByCnaeCodeAsync(cnaeCode, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        return ServiceTypeTaxCodes.Default();
    }

    private async Task<string> GetIbgeCodeAsync(string city, string uf, CancellationToken cancellationToken)
    {
        var municipality = await _municipalityRepo.GetByCityAndUfAsync(city, uf, cancellationToken);
        return municipality?.IbgeCode ?? "4204301"; // Default to example (Concórdia-SC IBGE)
    }

        private string SignXml(XmlDocument xmlDoc, byte[] certificateData, string certificatePassword)
    {
        var certificate = X509CertificateLoader.LoadPkcs12(
            certificateData, 
            certificatePassword, 
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        // Create signed XML
        // var xmlDoc = new XmlDocument
        // {
        //     PreserveWhitespace = false,
        // };
        // xmlDoc.LoadXml(xml);
        var signedXml = new SignedXml(xmlDoc)
        {
            SigningKey = certificate.GetRSAPrivateKey()
        };

        // Reference the entire document
        var reference = new Reference { Uri = "" };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(reference);

        // Add key info
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        // Compute signature
        signedXml.ComputeSignature();

        // Append signature to XML
        var signatureElement = signedXml.GetXml();
        xmlDoc.DocumentElement?.AppendChild(xmlDoc.ImportNode(signatureElement, true));

        // _logger.LogDebug("XML signed successfully using certificate: {Thumbprint}", certificate.Thumbprint);

        return xmlDoc.OuterXml;
    }

}
