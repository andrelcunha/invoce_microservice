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

        var root = new XElement("EnviarLoteRpsEnvio",
            new XAttribute("xmlns", "http://www.abrasf.org.br/nfse.xsd"));

        var loteRps = new XElement("LoteRps",
            new XAttribute("Id", $"lote_{Guid.NewGuid():N}")); // Unique batch ID

        // Batch details
        loteRps.Add(new XElement("NumeroLote", invoice.Id)); // Use invoice ID as batch number for simplicity
        loteRps.Add(new XElement("Cnpj", Helpers.OnlyDigits(issuer.Cnpj)));
        loteRps.Add(new XElement("InscricaoMunicipal", issuer.MunicipalInscription));
        loteRps.Add(new XElement("QuantidadeRps", 1)); // Single RPS for MVP

        var listaRps = new XElement("ListaRps");
        var rps = new XElement("Rps");
        var infDps = await BuildInfDpsAsync(invoice, issuer, consumer, serviceCodes, isTestMode, cancellationToken);
        rps.Add(infDps);
        listaRps.Add(rps);
        loteRps.Add(listaRps);

        root.Add(loteRps);

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<XElement> BuildInfDpsAsync(Invoice invoice, Issuer issuer, Consumer consumer, ServiceTypeTaxCodes codes, bool isTestMode, CancellationToken cancellationToken)
    {
        var infDps = new XElement("InfDeclaracaoPrestacaoServico",
            new XAttribute("Id", $"dps_{Guid.NewGuid():N}"));

        // RPS Identification
        var identRps = new XElement("Rps");
        identRps.Add(new XElement("Numero", invoice.Id)); // Use invoice ID as RPS number
        identRps.Add(new XElement("Serie", "RPS")); // Default series
        identRps.Add(new XElement("Tipo", 1)); // 1 = RPS
        infDps.Add(identRps);

        // Dates
        infDps.Add(new XElement("Competencia", DateTime.Today.ToString("yyyy-MM-dd")));
        infDps.Add(new XElement("DataEmissao", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")));

        // Service details
        infDps.Add(await BuildServicoAsync(invoice, codes, cancellationToken));

        // Provider (Prestador)
        infDps.Add(new XElement("Prestador",
            new XElement("Cnpj", Helpers.OnlyDigits(issuer.Cnpj)),
            new XElement("InscricaoMunicipal", issuer.MunicipalInscription)
        ));

        // Recipient (Tomador/Destinatario)
        infDps.Add(await BuildTomadorAsync(consumer, cancellationToken));

        // IBS/CBS group - Required for 2026 reform
        infDps.Add(await BuildIbsCbsDpsSectionAsync(invoice, codes, cancellationToken));

        // Test mode flag (if applicable - national portal may use different test mechanism)
        if (isTestMode)
        {
            // National test mode typically uses separate environment, but add custom flag if needed
            infDps.Add(new XElement("nfse_teste", "1"));
        }

        return infDps;
    }

    private async Task<XElement> BuildServicoAsync(Invoice invoice, ServiceTypeTaxCodes codes, CancellationToken cancellationToken)
    {
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;

        var servico = new XElement("Servico");

        // Values subgroup
        var valores = new XElement("Valores");
        valores.Add(new XElement("ValorServicos", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(new XElement("ValorDeducoes", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("ValorPis", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("ValorCofins", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("ValorInss", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("ValorIr", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("ValorCsll", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("IssRetido", 2)); // 2 = Not retained
        valores.Add(new XElement("ValorIss", Helpers.FormatMonetary(invoice.Amount * invoice.IssRate)));
        valores.Add(new XElement("OutrasRetencoes", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("BaseCalculo", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(new XElement("Aliquota", Helpers.FormatRate(invoice.IssRate)));
        valores.Add(new XElement("ValorLiquidoNfse", Helpers.FormatMonetary(invoice.Amount)));
        valores.Add(new XElement("DescontoIncondicionado", Helpers.FormatMonetary(0)));
        valores.Add(new XElement("DescontoCondicionado", Helpers.FormatMonetary(0)));
        servico.Add(valores);

        // Service codes
        servico.Add(new XElement("ItemListaServico", codes.ServiceListCode)); // e.g., "0101"
        servico.Add(new XElement("CodigoCnae", Helpers.OnlyDigits(issuer.Cnae ?? "")));
        servico.Add(new XElement("CodigoTributacaoMunicipio", codes.TaxClassificationCode)); // Municipal tax code
        servico.Add(new XElement("Discriminacao", Helpers.EscapeXmlContent(invoice.ServiceDescription ?? "Serviços prestados")));
        servico.Add(new XElement("CodigoMunicipio", await GetIbgeCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken)));

        return servico;
    }

    private async Task<XElement> BuildTomadorAsync(Consumer consumer, CancellationToken cancellationToken)
    {
        var tomador = new XElement("TomadorServico");

        var identTomador = new XElement("IdentificacaoTomador");
        var cpfCnpj = new XElement("CpfCnpj");
        if (consumer.CpfCnpj.Length == 11)
            cpfCnpj.Add(new XElement("Cpf", Helpers.OnlyDigits(consumer.CpfCnpj)));
        else
            cpfCnpj.Add(new XElement("Cnpj", Helpers.OnlyDigits(consumer.CpfCnpj)));
        identTomador.Add(cpfCnpj);
        tomador.Add(identTomador);

        tomador.Add(new XElement("RazaoSocial", Helpers.EscapeXmlContent(consumer.Name)));

        var endereco = new XElement("Endereco");
        endereco.Add(new XElement("Endereco", Helpers.EscapeXmlContent(consumer.Address.Street)));
        endereco.Add(new XElement("Numero", consumer.Address.Number));
        endereco.Add(new XElement("Complemento", Helpers.EscapeXmlContent(consumer.Address.Complement ?? "")));
        endereco.Add(new XElement("Bairro", Helpers.EscapeXmlContent(consumer.Address.Neighborhood)));
        endereco.Add(new XElement("CodigoMunicipio", await GetIbgeCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken)));
        endereco.Add(new XElement("Uf", consumer.Address.Uf.ToUpperInvariant()));
        endereco.Add(new XElement("Cep", Helpers.OnlyDigits(consumer.Address.ZipCode)));
        tomador.Add(endereco);

        var contato = new XElement("Contato");
        contato.Add(new XElement("Telefone", Helpers.OnlyDigits(consumer.Phone ?? "")));
        contato.Add(new XElement("Email", Helpers.EscapeXmlContent(consumer.Email ?? "")));
        tomador.Add(contato);

        return tomador;
    }

    private async Task<XElement> BuildIbsCbsDpsSectionAsync(Invoice invoice, ServiceTypeTaxCodes codes, CancellationToken cancellationToken)
    {
        var ibscbs = new XElement("IBSCBS");

        // Basic indicators
        ibscbs.Add(new XElement("finNFSe", "0")); // 0 = Regular
        ibscbs.Add(new XElement("indFinal", "1")); // 1 = Final consumer
        ibscbs.Add(new XElement("cIndOp", codes.OperationIndicator)); // From service codes

        // Reference NFS-e if applicable (omit for new)
        // ibscbs.Add(new XElement("gRefNFSe", new XElement("refNFSe", "...")));

        // Government entity type (omit if not applicable)
        // ibscbs.Add(new XElement("tpEnteGov", "1"));

        // Relationship indicator
        ibscbs.Add(new XElement("indPessoasDest", "1")); // Default

        // Destinatario (Recipient) - Required for IBS/CBS
        ibscbs.Add(await BuildDestinatarioAsync(invoice.ConsumerData, cancellationToken));

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
        var end = new XElement("end");
        var endNac = new XElement("endNac");
        endNac.Add(new XElement("cMun", await GetIbgeCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken)));
        endNac.Add(new XElement("CEP", Helpers.OnlyDigits(consumer.Address.ZipCode)));
        end.Add(endNac);
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
        gIBSCBS.Add(new XElement("CST", codes.TaxSituationCode));
        gIBSCBS.Add(new XElement("cClassTrib", codes.TaxClassificationCode));
        // gIBSCBS.Add(new XElement("cCredPres", "...")); // Presumed credit code if applicable

        // gTribRegular: Regular taxation details
        var gTribRegular = new XElement("gTribRegular");
        gTribRegular.Add(new XElement("CSTReg", "01")); // Example
        gTribRegular.Add(new XElement("cClassTribReg", codes.TaxClassificationCode));
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
