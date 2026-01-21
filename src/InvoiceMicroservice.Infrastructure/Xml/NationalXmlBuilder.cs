using System.Text.Json;
using System.Xml.Linq;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;

namespace InvoiceMicroservice.Infrastructure.Xml;

public class NationalXmlBuilder : IInvoiceXmlBuilder
{
    private readonly TaxConfig _taxConfig;
    private readonly IServiceTypeTaxMappingRepository _serviceTaxRepo;
    private readonly IMunicipalityRepository _municipalityRepo;

    public NationalXmlBuilder(
        TaxConfig taxConfig, 
        IServiceTypeTaxMappingRepository serviceTaxRepo,
        IMunicipalityRepository municipalityRepo)
    {
        _taxConfig = taxConfig;
        _serviceTaxRepo = serviceTaxRepo;
        _municipalityRepo = municipalityRepo;
    }

    public async Task<string> BuildInvoiceXmlAsync(Invoice invoice, bool isTestMode = true, CancellationToken cancellationToken = default)
    {
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;
        var consumer = JsonSerializer.Deserialize<Consumer>(invoice.ConsumerData)!;

        // Lookup service type codes - fallback to defaults if not found
        var serviceCodes = await GetServiceCodesAsync(invoice.ServiceTypeKey, issuer.Cnae, cancellationToken);

        // var root = new XElement("EnviarLoteRpsEnvio",
        //     new XAttribute("xmlns", "http://www.abrasf.org.br/nfse.xsd"));

        // var loteRps = new XElement("LoteRps",
        //     new XAttribute("Id", $"lote_{Guid.NewGuid():N}")); // Unique batch ID

        // // Batch details
        // loteRps.Add(new XElement("NumeroLote", invoice.Id)); // Use invoice ID as batch number for simplicity
        // loteRps.Add(new XElement("Cnpj", Helpers.OnlyDigits(issuer.Cnpj)));
        // loteRps.Add(new XElement("InscricaoMunicipal", issuer.MunicipalInscription));
        // loteRps.Add(new XElement("QuantidadeRps", 1)); // Single RPS for MVP

        // var listaRps = new XElement("ListaRps");
        // var rps = new XElement("Rps");
        // var infDps = await BuildInfDpsAsync(invoice, issuer, consumer, serviceCodes, isTestMode, cancellationToken);
        // rps.Add(infDps);
        // listaRps.Add(rps);
        // loteRps.Add(listaRps);

        // root.Add(loteRps);
        var root = new XElement("Nfse",
            new XAttribute("xmlns", "http://www.abrasf.org.br/nfse.xsd"));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<XElement> BuildDpsAsync(Invoice invoice,  bool isTestMode, CancellationToken cancellationToken = default)
    {
        int serie = 1; // Hardcoded for MVP
        int numero = 1; // Hardcoded for MVP TODO: Find a way to get real series/number
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;
        string codMun = await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken);
        var consumer = JsonSerializer.Deserialize<Consumer>(invoice.ConsumerData)!;

        // Lookup service type codes - fallback to defaults if not found
        var serviceCodes = await GetServiceCodesAsync(invoice.ServiceTypeKey, issuer.Cnae, cancellationToken);

        var dps = new XElement("DPS");
        dps.Add(new XElement("versao", "1.00")); // TODO: confiirmar versão correta
        dps.Add(await BuildInfDpsAsync(invoice, issuer, consumer, serviceCodes, codMun, serie, numero, isTestMode, cancellationToken));
        return dps;
    }


    private string BuildDpsId(Invoice invoice, Issuer issuer,  string codMun, int serie, int numero)
    {
        // A formação do identificador da DPS
        var builder = new System.Text.StringBuilder();
        builder.Append("DPS");
        // cod. municipal (7)
        builder.Append(codMun.PadLeft(7, '0'));
        // Tipo de inscrição federal (1) // 1=CPF, 2=CNPJ
        builder.Append(issuer.Cnpj.Length == 11 ? "1" : "2");
        // inscrição federal (14)  Se for CPF completar com 000 à esquerda
        builder.Append(Helpers.OnlyDigits(issuer.Cnpj).PadLeft(14, '0'));
        // Serie (5)
        builder.Append(serie.ToString().PadRight(5, '0'));
        // Número da DPS (15)
        builder.Append(numero.ToString().PadLeft(15, '0'));
        return builder.ToString();
    }

    private async Task<XElement> BuildInfDpsAsync(Invoice invoice, Issuer issuer,  Consumer consumer, ServiceTypeTaxCodes serviceCodes, string codMun, int serie, int numero, bool isTestMode, CancellationToken ct)
    {
        var infDps = new XElement("infDps");
        infDps.Add(new XElement("Id", BuildDpsId(invoice, issuer, codMun, serie, numero))); // Serie and Numero hardcoded for MVP
        // tpAmp - Tipo de Ambiente (1=Produção, 2=Homologação)
        infDps.Add(new XElement("tpAmb", isTestMode ? "2" : "1"));
        // dhEmi - Data e Hora de Emissão (ISO 8601)
        infDps.Add(new XElement("dhEmi", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        // verAplic - Versão do Aplicativo
        infDps.Add(new XElement("verAplic", "1.0.0")); // Hardcoded for MVP
        // serie
        infDps.Add(new XElement("serie", serie));
        // numero
        infDps.Add(new XElement("nDPS", numero));
        // dCompet - Data de Competência (ISO 8601) YYYY-MM-DD
        infDps.Add(new XElement("dCompet", invoice.IssuedAt?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")));
        //  tpEmit - Emitente da DPS (1=Prestador de Serviço, 2=Tomador de Serviço, 3=Intermediário)
        infDps.Add(new XElement("tpEmit", "1")); // Always Prestador 
        // cMotivoEmisTI - Código do Motivo de Emissão em Sistema pelo tomador/intermediário
        // chNFSeRej - Chave da NFS-e rejeitada
        // cLocEmi - Código do Local de Emissão (7) Código IBGE
        infDps.Add(new XElement("cLocEmi", codMun));
        // grupo 'subst'
        infDps.Add(BuildDpsPrest(issuer));
        // grupo 'toma'
        infDps.Add(BuildDpsToma(consumer));
        // grupo 'interm' -- não se aplica
        // grupo 'serv'
        infDps.Add(await BuildServicoAsync(invoice, issuer, serviceCodes, ct));
        infDps.Add(BuildValoresAsync(invoice));
        infDps.Add(await BuildIbsCbsAsync(invoice, serviceCodes, ct));
        return infDps;
    }

    private XElement BuildDpsPrest(Issuer issuer)
    {
        var prest = new XElement("prest");
        if (issuer.Cnpj.Length == 11)
            prest.Add(new XElement("CPF", Helpers.OnlyDigits(issuer.Cnpj)));
        else
            prest.Add(new XElement("CNPJ", Helpers.OnlyDigits(issuer.Cnpj)));
        // NIF --  não preenchido se tpEmit = 1
        // cNaoNIF --  não preenchido se tpEmit = 1
        // CAEPF -- não preenchido se tpEmit = 1
        prest.Add(new XElement("IM", issuer.MunicipalInscription));
        prest.Add(new XElement("xNome", Helpers.EscapeXmlContent(issuer.Name)));
        XElement end = BuildEndElement(issuer.Address);
        prest.Add(end);
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

    private static XElement BuildEndElement(Address address)
    {
        var end = new XElement("end");
        var endNac = new XElement("endNac");
        endNac.Add(new XElement("cMun", "")); // deixar vazio no prestador
        endNac.Add(new XElement("CEP", Helpers.OnlyDigits(address.ZipCode)));
        end.Add(endNac);
        end.Add(new XElement("xLgr", Helpers.EscapeXmlContent(address.Street)));
        end.Add(new XElement("nro", address.Number));
        end.Add(new XElement("xCpl", Helpers.EscapeXmlContent(address.Complement ?? "")));
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
    private XElement BuildDpsToma(Consumer consumer)
    {
        var tom = new XElement("toma");
        if (consumer.CpfCnpj.Length == 11)
            tom.Add(new XElement("CPF", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else
            tom.Add(new XElement("CNPJ", Helpers.OnlyDigits(consumer.CpfCnpj)));
        // NIF -- omitido se tpEmit = 1
        // cNaoNIF -- omitido se tpEmit = 1
        tom.Add(new XElement("xNome", Helpers.EscapeXmlContent(consumer.Name)));
        var end = BuildEndElement(consumer.Address);
        tom.Add(end);
        tom.Add(new XElement("fone", Helpers.OnlyDigits(consumer.Phone ?? "")));
        tom.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));
        return tom;
    }

    private async Task<XElement> BuildEmitterXmlAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var emit = new XElement("emit");
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;
        emit.Add(new XElement("CNPJ", Helpers.OnlyDigits(issuer.Cnpj)));
        emit.Add(new XElement("IM", issuer.MunicipalInscription));
        emit.Add(new XElement("xNome", Helpers.EscapeXmlContent(issuer.Name)));
        // emit.Add(new XElement("xFant", Helpers.EscapeXmlContent(issuer.FantasyName ?? "")));
        emit.Add(new XElement("xFant",  ""));

        var enderNac = new XElement("enderNac");
        enderNac.Add(new XElement("xLgr", Helpers.EscapeXmlContent(issuer.Address.Street)));
        enderNac.Add(new XElement("nro", issuer.Address.Number));
        enderNac.Add(new XElement("xCpl", Helpers.EscapeXmlContent(issuer.Address.Complement ?? "")));
        enderNac.Add(new XElement("xBairro", Helpers.EscapeXmlContent(issuer.Address.Neighborhood)));
        enderNac.Add(new XElement("cMun",await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken)));
        enderNac.Add(new XElement("UF", issuer.Address.Uf));
        enderNac.Add(new XElement("CEP", Helpers.OnlyDigits(issuer.Address.ZipCode)));
        emit.Add(enderNac);
        emit.Add(new XElement("fone", Helpers.OnlyDigits("")));
        emit.Add(new XElement("email", Helpers.EscapeXmlContent("")));

        return emit;
    }

    private XElement BuildValoresAsync(Invoice invoice, decimal pisRetido, decimal cofinsRetido)
    {
        var valores = new XElement("valores");

        valores.Add(new XElement("vCalcDR", Helpers.FormatMonetary(0))); // Deductions
        // valores.Add(new XElement("tpBM", Helpers.FormatMonetary(invoice.Amount))); 
        // valores.Add(new XElement("vCalcBM", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(new XElement("vBC", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(new XElement("pAliqAplic", Helpers.FormatRate(invoice.IssRate)));
        var issRetido = invoice.Amount * invoice.IssRate;
        valores.Add(new XElement("vISSQN", Helpers.FormatMonetary(issRetido)));
        valores.Add(new XElement("vTotalRet", Helpers.FormatMonetary(issRetido + pisRetido + cofinsRetido))); // adicionar PIS e COFINS se houver
        valores.Add(new XElement("vLiq", Helpers.FormatMonetary(invoice.Amount - issRetido)));


        return valores;
    }



    private async Task<XElement> BuildServicoAsync(Invoice invoice, Issuer issuer, ServiceTypeTaxCodes codes, CancellationToken ct)
    {
        var servico = new XElement("serv");

        var locPrest = new XElement("locPrest");
        locPrest.Add(new XElement("cLocPrestacao", await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, ct)));
        locPrest.Add(new XElement("cPaisPrestacao", "1058")); // Brazil);
        servico.Add(locPrest);

        var cServ = new XElement("cServ");
        // cTribNac - Código de Tributação Nacional do ISSQN
        cServ.Add(new XElement("cTribNac", codes.ServiceListCode));
        // cTribMun
        cServ.Add(new XElement("cTribMun", invoice.MunicipalTaxCode));
        // xDescServ
        cServ.Add(new XElement("xDescServ", codes.Description));
        // cNBS
        // cIntContrib - Código interno do contribuinte (id no Sistema Interno do Contribuinte)
        cServ.Add(new XElement("cIntContrib", invoice.Id.ToString()));
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
        vServPrest.Add(new XElement("vReceb", Helpers.FormatMonetary(invoice.Amount)));
        vServPrest.Add(new XElement("vServ", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(vServPrest);

        var vDescCondIncond = new XElement("vDescCondIncond");
        vDescCondIncond.Add(new XElement("vDescIncond", Helpers.FormatMonetary(0))); // No unconditional discount
        vDescCondIncond.Add(new XElement("vDescCond", Helpers.FormatMonetary(0))); // No conditional discount
        valores.Add(vDescCondIncond);

        // var vDedRed = new XElement("vDedRed");
        // vDedRed.Add(new XElement("vDed", Helpers.FormatMonetary(0)));
        // vDedRed.Add(new XElement("vRed", Helpers.FormatMonetary(0)));
        // valores.Add(vDedRed);
        var trib = new XElement("trib");
        var tribMun = new XElement("tribMun");
        tribMun.Add(new XElement("tribISSQN", "1")); // 1 = Tributável, 2 = Imunidade, 3 - Exportação, 4 - Não Incidência
        tribMun.Add(new XElement("tpRetISSQN", "1")); // 1 = Não Retido, 2 = Retido pelo tomador 3 = Retido pelo intermediário
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

    private async Task<XElement> BuildTomadorAsync(Consumer consumer, CancellationToken cancellationToken)
    {
        var dest = new XElement("dest");

        if (consumer.CpfCnpj.Length == 11)
            dest.Add(new XElement("CPF", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else
            dest.Add(new XElement("CNPJ", Helpers.OnlyDigits(consumer.CpfCnpj)));

        // NIF for foreign entities
        dest.Add(new XElement("cNaoNIF", "0")); // 0 = Não informado ; 1 = Dispensado; 2 = Não exigência do NIF

        dest.Add(new XElement("xNome", Helpers.EscapeXmlContent(consumer.Name)));

        var endereco = new XElement("end");

        var endNac = new XElement("endNac");
        endNac.Add(new XElement("cMun", await GetIbgeCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken)));
        endNac.Add(new XElement("CEP", Helpers.OnlyDigits(consumer.Address.ZipCode)));

        // var endExt = new XElement("endExt");   // Omit for national
        // endExt.Add(new XElement("cPais", "1058")); // Brazil
        // endExt.Add(new XElement("cEndPost", "0")); // Not applicable
        // endExt.Add(new XElement("xCidade", ""));
        // endExt.Add(new XElement("xEstProvReg", ""));

        endereco.Add(new XElement("xLgr", Helpers.EscapeXmlContent(consumer.Address.Street)));
        endereco.Add(new XElement("nro", consumer.Address.Number));
        endereco.Add(new XElement("xCpl", Helpers.EscapeXmlContent(consumer.Address.Complement ?? "")));
        endereco.Add(new XElement("xBairro", Helpers.EscapeXmlContent(consumer.Address.Neighborhood)));

        endereco.Add(endNac);
        dest.Add(endereco);

        dest.Add(new XElement("fone", Helpers.OnlyDigits(consumer.Phone ?? "")));
        dest.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));

        return dest;
    }

    private async Task<XElement> BuildIbsCbsAsync(Invoice invoice, ServiceTypeTaxCodes codes, CancellationToken cancellationToken)
    {
        var ibscbs = new XElement("IBSCBS");

        // Basic indicators
        ibscbs.Add(new XElement("finNFSe", "0")); // 0 = Regular
        ibscbs.Add(new XElement("indFinal", "1")); // 1 = Final consumer -- this field will be deprecated
        ibscbs.Add(new XElement("cIndOp", codes.OperationIndicator)); // From service codes

        // Reference NFS-e if applicable (omit for new)
        // ibscbs.Add(new XElement("gRefNFSe", new XElement("refNFSe", "...")));

        // Government entity type (omit if not applicable)
        // ibscbs.Add(new XElement("tpEnteGov", "1"));

        // Relationship indicator
        ibscbs.Add(new XElement("indPessoasDest", "1")); // Default

        // Destinatario (Recipient) - Required for IBS/CBS
        var dest = await BuildDestinatarioAsync(invoice.ConsumerData, cancellationToken);
        ibscbs.Add(dest);

        // Imovel (Property) - Omit unless real estate
        // ibscbs.Add(BuildImovelSection(...));

        // Valores group with reembolsos and tributacao
        var valores = await BuildValoresIbsCbsAsync(invoice, codes, cancellationToken);
        ibscbs.Add(valores);

        return ibscbs;
    }

    private async Task<XElement> BuildDestinatarioAsync(string consumerDataJson, CancellationToken cancellationToken)
    {
        var consumer = JsonSerializer.Deserialize<Consumer>(consumerDataJson)!;
        var dest = new XElement("dest");

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
        XElement end = BuildEndElement(consumer.Address);
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
}
