using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;

namespace InvoiceMicroservice.Infrastructure.Xml;

public interface IIpmXmlBuilder
{
    Task<string> BuildInvoiceXmlAsync(Invoice invoice, bool isTestMode = true, CancellationToken cancellationToken = default);
}

public class IpmXmlBuilder : IIpmXmlBuilder
{
    private readonly TaxConfig _taxConfig;
    private readonly IServiceTypeTaxMappingRepository _serviceTaxRepo;
    private readonly IMunicipalityRepository _municipalityRepo;

    public IpmXmlBuilder(
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

        var root = new XElement("nfse");
        if (isTestMode)
            root.Add(new XElement("nfse_teste", "1"));

        // Unique identifier to prevent duplicate processing
        root.Add(new XElement("identificador", invoice.Id.ToString("N")));
        
        // <nf> section - invoice values and tax info
        root.Add(BuildNfSection(invoice));

        // <prestador> - service provider (issuer)
        var issuerTomCode = await GetTomCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken);
        root.Add(new XElement("prestador",
            new XElement("cpfcnpj", OnlyDigits(issuer.Cnpj)),
            new XElement("cidade", issuerTomCode)
        ));

        // <tomador> - service taker (consumer)
        root.Add(await BuildTomadorAsync(consumer, cancellationToken));

        // <itens> - service items list
        root.Add(await BuildItemsAsync(invoice, issuer, serviceCodes, cancellationToken));

        // <IBSCBS> - Top-level tax reform section (REQUIRED for compliance)
        root.Add(BuildIbsCbsSection(serviceCodes));

        // <forma_pagamento> - payment method (optional but recommended)
        root.Add(new XElement("forma_pagamento",
            new XElement("tipo_pagamento", "1") // 1 = À vista (immediate payment)
        ));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<ServiceTypeTaxCodes> GetServiceCodesAsync(string? serviceTypeKey, string? cnaeCode, CancellationToken cancellationToken)
    {
        // Try lookup by service type key
        if (!string.IsNullOrEmpty(serviceTypeKey))
        {
            var mapping = await _serviceTaxRepo.GetByServiceTypeKeyAsync(serviceTypeKey, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        // Fallback: try lookup by CNAE
        if (!string.IsNullOrEmpty(cnaeCode))
        {
            var mapping = await _serviceTaxRepo.GetByCnaeCodeAsync(cnaeCode, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        // Ultimate fallback: default codes
        return ServiceTypeTaxCodes.Default();
    }

    private XElement BuildNfSection(Invoice invoice)
    {
        var nf = new XElement("nf");
        
        // Basic invoice values - use period as decimal separator per spec examples
        nf.Add(new XElement("data_fato_gerador", DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        nf.Add(new XElement("valor_total", FormatMonetary(invoice.Amount)));
        nf.Add(new XElement("valor_desconto", FormatMonetary(0)));
        nf.Add(new XElement("valor_ir", FormatMonetary(0)));
        nf.Add(new XElement("valor_inss", FormatMonetary(0)));
        nf.Add(new XElement("valor_contribuicao_social", FormatMonetary(0)));
        nf.Add(new XElement("valor_rps", FormatMonetary(0)));
        
        // PIS/COFINS section (required when applicable)
        nf.Add(BuildPisCofinsSection(invoice.Amount));
        
        // Calculate PIS/COFINS values for display
        var pisValue = invoice.Amount * _taxConfig.PAliquotaPis;
        var cofinsValue = invoice.Amount * _taxConfig.PAliquotaCofins;
        nf.Add(new XElement("valor_pis", FormatMonetary(pisValue)));
        nf.Add(new XElement("valor_cofins", FormatMonetary(cofinsValue)));
        
        nf.Add(new XElement("observacao", EscapeXmlContent(invoice.ServiceDescription ?? "")));

        // IBS/CBS rates inside <nf> (auto-calculated by IPM, but we send for validation)
        nf.Add(BuildIbsCbsNfSection(invoice.Amount));

        return nf;
    }

    private XElement BuildPisCofinsSection(decimal baseValue)
    {
        var pisCofins = new XElement("pis_cofins");
        
        // CST code - should be configurable per issuer
        pisCofins.Add(new XElement("cst", "01"));
        
        // Retention type: 1=Retained, 2=Not Retained, 3=PIS Retained/COFINS Not, 4=PIS Not/COFINS Retained
        pisCofins.Add(new XElement("tipo_retencao", "2")); // Default: not retained
        
        pisCofins.Add(new XElement("base_calculo", FormatMonetary(baseValue)));
        
        // Rates use comma as decimal separator per spec
        pisCofins.Add(new XElement("aliquota_pis", FormatRate(_taxConfig.PAliquotaPis)));
        pisCofins.Add(new XElement("aliquota_cofins", FormatRate(_taxConfig.PAliquotaCofins)));

        return pisCofins;
    }

    private XElement BuildIbsCbsNfSection(decimal amount)
    {
        // This section inside <nf> contains IBS/CBS rates - IPM auto-calculates but validates our input
        var section = new XElement("IBSCBS");
        
        // IBS UF (state-level)
        section.Add(new XElement("valor_ibs_uf", FormatMonetary(amount * _taxConfig.PIbsUf)));
        section.Add(new XElement("aliquota_ibs_uf", FormatRate(_taxConfig.PIbsUf)));
        section.Add(new XElement("valor_reducao_ibs_uf", FormatMonetary(amount * _taxConfig.PRedAliqUf)));
        
        // IBS MUN (municipal-level)
        section.Add(new XElement("valor_ibs_mun", FormatMonetary(amount * _taxConfig.PIbsMun)));
        section.Add(new XElement("aliquota_ibs_mun", FormatRate(_taxConfig.PIbsMun)));
        section.Add(new XElement("valor_reducao_ibs_mun", FormatMonetary(amount * _taxConfig.PRedAliqMun)));
        
        // CBS (federal contribution)
        section.Add(new XElement("valor_cbs", FormatMonetary(amount * _taxConfig.PCbs)));
        section.Add(new XElement("aliquota_cbs", FormatRate(_taxConfig.PCbs)));
        section.Add(new XElement("valor_reducao_cbs", FormatMonetary(amount * _taxConfig.PRedAliqCbs)));

        return section;
    }

    private XElement BuildIbsCbsSection(ServiceTypeTaxCodes codes)
    {
        // Top-level IBSCBS section - REQUIRED for tax reform compliance
        // This section is OUTSIDE <nf> and contains operational tax data
        var ibscbs = new XElement("IBSCBS");
        
        // finNFSe: 0=regular operation, 1=adjustment, 2=return
        ibscbs.Add(new XElement("finNFSe", "0"));
        
        // indFinal: 0=not final consumer, 1=final consumer
        ibscbs.Add(new XElement("indFinal", "1"));
        
        // cIndOp: Operation indicator code (6 digits) - links to service list
        ibscbs.Add(new XElement("cIndOp", codes.OperationIndicator));
        
        var valores = new XElement("valores");
        var trib = new XElement("trib");
        var gIBSCBS = new XElement("gIBSCBS");
        
        // CST: Tax Situation Code (3 digits)
        gIBSCBS.Add(new XElement("CST", codes.TaxSituationCode));
        
        // cClassTrib: Tax Classification Code (6 digits)
        gIBSCBS.Add(new XElement("cClassTrib", codes.TaxClassificationCode));
        
        trib.Add(gIBSCBS);
        valores.Add(trib);
        ibscbs.Add(valores);
        
        return ibscbs;
    }

    private async Task<XElement> BuildItemsAsync(Invoice invoice, Issuer issuer, ServiceTypeTaxCodes codes, CancellationToken cancellationToken)
    {
        var itens = new XElement("itens");
        var lista = new XElement("lista");

        // Service location and taxation
        lista.Add(new XElement("tributa_municipio_prestador", "S"));
        
        var issuerTomCode = await GetTomCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken);
        lista.Add(new XElement("codigo_local_prestacao_servico", issuerTomCode));
        
        // Unit information
        lista.Add(new XElement("unidade_codigo", "1")); // 1 = unit
        lista.Add(new XElement("unidade_quantidade", "1"));
        lista.Add(new XElement("unidade_valor_unitario", FormatMonetary(invoice.Amount)));
        
        // Service classification codes
        lista.Add(new XElement("codigo_item_lista_servico", codes.ServiceListCode));
        
        // NBS code - MANDATORY for tax reform (error 00366)
        lista.Add(new XElement("codigo_nbs", codes.NbsCode));
        
        lista.Add(new XElement("descritivo", EscapeXmlContent(invoice.ServiceDescription)));
        
        // Tax rates - use 4 decimal places for rates
        lista.Add(new XElement("aliquota_item_lista_servico", FormatRate(_taxConfig.PIbsUf)));
        
        // situacao_tributaria: 4-digit code
        lista.Add(new XElement("situacao_tributaria", "0000"));
        
        lista.Add(new XElement("valor_tributavel", FormatMonetary(invoice.Amount)));
        lista.Add(new XElement("valor_deducao", FormatMonetary(0)));
        lista.Add(new XElement("valor_issrf", FormatMonetary(0)));

        itens.Add(lista);
        return itens;
    }

    private async Task<XElement> BuildTomadorAsync(Consumer consumer, CancellationToken cancellationToken)
    {
        var tomador = new XElement("tomador");
        
        tomador.Add(new XElement("endereco_informado", "1"));
        
        // Determine type: J=Legal entity (CNPJ), F=Natural person (CPF), E=Foreign
        tomador.Add(new XElement("tipo", DetermineTomadorType(consumer.CpfCnpj)));
        
        tomador.Add(new XElement("cpfcnpj", OnlyDigits(consumer.CpfCnpj)));
        tomador.Add(new XElement("ie", "")); // State registration - empty for most services
        tomador.Add(new XElement("nome_razao_social", EscapeXmlContent(consumer.Name)));
        tomador.Add(new XElement("sobrenome_nome_fantasia", "")); // Trade name - usually empty
        
        // Address information
        tomador.Add(new XElement("logradouro", EscapeXmlContent(consumer.Address.Street)));
        tomador.Add(new XElement("numero_residencia", consumer.Address.Number));
        tomador.Add(new XElement("complemento", EscapeXmlContent(consumer.Address.Complement ?? "")));
        tomador.Add(new XElement("ponto_referencia", "")); // Reference point - optional
        tomador.Add(new XElement("bairro", EscapeXmlContent(consumer.Address.Neighborhood)));
        
        var consumerTomCode = await GetTomCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken);
        tomador.Add(new XElement("cidade", consumerTomCode));
        tomador.Add(new XElement("cep", OnlyDigits(consumer.Address.ZipCode)));
        
        tomador.Add(new XElement("email", EscapeXmlContent(consumer.Email ?? "")));
        
        // Phone numbers - split DDD (area code) from number
        if (!string.IsNullOrEmpty(consumer.Phone))
        {
            var phoneDigits = OnlyDigits(consumer.Phone);
            var ddd = phoneDigits.Length >= 2 ? phoneDigits[..2] : "";
            var number = phoneDigits.Length > 2 ? phoneDigits[2..] : "";
            
            tomador.Add(new XElement("ddd_fone_comercial", ddd));
            tomador.Add(new XElement("fone_comercial", number));
        }
        else
        {
            tomador.Add(new XElement("ddd_fone_comercial", ""));
            tomador.Add(new XElement("fone_comercial", ""));
        }

        return tomador;
    }

    private async Task<string> GetTomCodeAsync(string city, string uf, CancellationToken cancellationToken)
    {
        var municipality = await _municipalityRepo.GetByCityAndUfAsync(city, uf, cancellationToken);
        
        if (municipality != null)
        {
            return municipality.TomCode;
        }

        // Fallback to Concordia-SC (your HQ city) if municipality not found
        // In production, you might want to throw an exception or log a warning
        return "8083";
    }

    private static string DetermineTomadorType(string cpfCnpj)
    {
        var digits = OnlyDigits(cpfCnpj);
        
        // No document = foreign entity
        if (digits.Length == 0) return "E";
        
        // CPF (11 digits) = natural person, CNPJ (14 digits) = legal entity
        return digits.Length == 11 ? "F" : "J";
    }

    /// <summary>
    /// Escapes XML special characters per IPM specification:
    /// &lt; &gt; &apos; &quot; &amp; and removes forward slashes
    /// </summary>
    private static string EscapeXmlContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        return content
            .Replace("&", "&amp;")   // Must be first to avoid double escaping
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("/", "");       // Forward slashes not allowed per spec
    }

    private static string OnlyDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        
        return new string(input.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Formats monetary values using period as decimal separator (e.g., 1500.00)
    /// Per IPM spec examples: valor_total, base_calculo use period
    /// </summary>
    private static string FormatMonetary(decimal amount)
    {
        return amount.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats percentage rates using comma as decimal separator with 4 decimals (e.g., 5,0000)
    /// Per IPM spec examples: aliquota_* fields use comma
    /// </summary>
    private static string FormatRate(decimal rate)
    {
        return (rate * 100).ToString("F4", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}

// Helper class to encapsulate service tax codes
internal record ServiceTypeTaxCodes
{
    public string NbsCode { get; init; }
    public string ServiceListCode { get; init; }
    public string OperationIndicator { get; init; }
    public string TaxSituationCode { get; init; }
    public string TaxClassificationCode { get; init; }

    public ServiceTypeTaxCodes(ServiceTypeTaxMapping mapping)
    {
        NbsCode = mapping.NbsCode;
        ServiceListCode = mapping.ServiceListCode;
        OperationIndicator = mapping.OperationIndicator;
        TaxSituationCode = mapping.TaxSituationCode;
        TaxClassificationCode = mapping.TaxClassificationCode;
    }

    private ServiceTypeTaxCodes(
        string nbsCode,
        string serviceListCode,
        string operationIndicator,
        string taxSituationCode,
        string taxClassificationCode)
    {
        NbsCode = nbsCode;
        ServiceListCode = serviceListCode;
        OperationIndicator = operationIndicator;
        TaxSituationCode = taxSituationCode;
        TaxClassificationCode = taxClassificationCode;
    }

    public static ServiceTypeTaxCodes Default() => new(
        "123456789",
        "0024",
        "030102",
        "200",
        "200028"
    );
}
