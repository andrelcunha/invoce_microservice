using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
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
        root.Add(await BuildNfSectionAsync(invoice, cancellationToken));

        // <prestador> - service provider (issuer)
        var issuerTomCode = await GetTomCodeAsync(issuer.Address.City, issuer.Address.Uf, cancellationToken);
        root.Add(new XElement("prestador",
            new XElement("cpfcnpj", Helpers.OnlyDigits(issuer.Cnpj)),
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

    private async Task<XElement> BuildNfSectionAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var nf = new XElement("nf");
        
        // Basic invoice values - use COMMA as decimal separator per IPM XSD
        nf.Add(new XElement("data_fato_gerador", DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        nf.Add(new XElement("valor_total", Helpers.FormatMonetary(invoice.Amount)));
        nf.Add(new XElement("valor_desconto", Helpers.FormatMonetary(0)));
        nf.Add(new XElement("valor_ir", Helpers.FormatMonetary(0)));
        nf.Add(new XElement("valor_inss", Helpers.FormatMonetary(0)));
        nf.Add(new XElement("valor_contribuicao_social", Helpers.FormatMonetary(0)));
        nf.Add(new XElement("valor_rps", Helpers.FormatMonetary(0)));
        
        // PIS/COFINS section (required when applicable)
        nf.Add(BuildPisCofinsSection(invoice.Amount));
        
        // Calculate PIS/COFINS values for display
        var pisValue = invoice.Amount * _taxConfig.PAliquotaPis;
        var cofinsValue = invoice.Amount * _taxConfig.PAliquotaCofins;
        nf.Add(new XElement("valor_pis", Helpers.FormatMonetary(pisValue)));
        nf.Add(new XElement("valor_cofins", Helpers.FormatMonetary(cofinsValue)));
        
        nf.Add(new XElement("observacao", Helpers.EscapeXmlContent(invoice.ServiceDescription ?? "")));

        // IBS/CBS section inside <nf> - must include pRedutor elements BEFORE valor_* elements per XSD
        nf.Add(await BuildIbsCbsNfSectionAsync(invoice.Amount, cancellationToken));

        return nf;
    }

    private XElement BuildPisCofinsSection(decimal baseValue)
    {
        var pisCofins = new XElement("pis_cofins");
        
        // CST code - should be configurable per issuer
        pisCofins.Add(new XElement("cst", "01"));
        
        // Retention type: 1=Retained, 2=Not Retained, 3=PIS Retained/COFINS Not, 4=PIS Not/COFINS Retained
        pisCofins.Add(new XElement("tipo_retencao", "2")); // Default: not retained
        
        pisCofins.Add(new XElement("base_calculo", Helpers.FormatMonetary(baseValue)));
        
        // Rates use comma with max 2 decimals per XSD pattern
        pisCofins.Add(new XElement("aliquota_pis", Helpers.FormatRate(_taxConfig.PAliquotaPis)));
        pisCofins.Add(new XElement("aliquota_cofins", Helpers.FormatRate(_taxConfig.PAliquotaCofins)));

        return pisCofins;
    }

    private async Task<XElement> BuildIbsCbsNfSectionAsync(decimal amount, CancellationToken cancellationToken)
    {
        // IBSCBS section inside <nf> per NTE-122/2025
        // This section contains calculation base, rates, and totals for IBS/CBS
        var section = new XElement("IBSCBS");
        
        // pRedutor: Global reduction percentage (for government purchases - generally 0 for private sector)
        section.Add(new XElement("pRedutor", Helpers.FormatRate(0)));
        
        // Valores group - contains calculation base and breakdown by jurisdiction
        var valores = new XElement("valores");
        
        // vBC: Calculation base BEFORE reductions
        // Formula: vBC = vServ - descIncond - vCalcReeRepRes - vISSQN - vPIS - vCOFINS (until 2026)
        // For MVP: vBC = invoice amount (no discounts, no other taxes deducted yet)
        var baseCalculo = amount;
        valores.Add(new XElement("vBC", Helpers.FormatMonetary(baseCalculo)));
        
        // UF group - State-level IBS information
        var uf = new XElement("uf");
        uf.Add(new XElement("pIBSUF", Helpers.FormatRate(_taxConfig.PIbsUf))); // State IBS rate (from system config)
        uf.Add(new XElement("pRedAliqUF", Helpers.FormatRate(_taxConfig.PRedAliqUf))); // State rate reduction %
        
        // pAliqEfetUF: Effective state rate after reductions
        // Formula: pAliqEfetUF = pIBSUF × (1 - pRedAliqUF) × (1 - pRedutor)
        var aliqEfetUF = _taxConfig.PIbsUf * (1 - _taxConfig.PRedAliqUf) * (1 - 0); // pRedutor = 0 for now
        uf.Add(new XElement("pAliqEfetUF", Helpers.FormatRate(aliqEfetUF)));
        valores.Add(uf);
        
        // MUN group - Municipal-level IBS information
        var mun = new XElement("mun");
        mun.Add(new XElement("pIBSMun", Helpers.FormatRate(_taxConfig.PIbsMun))); // Municipal IBS rate
        mun.Add(new XElement("pRedAliqMun", Helpers.FormatRate(_taxConfig.PRedAliqMun))); // Municipal rate reduction %
        
        // pAliqEfetMun: Effective municipal rate after reductions
        // Formula: pAliqEfetMun = pIBSMun × (1 - pRedAliqMun) × (1 - pRedutor)
        var aliqEfetMun = _taxConfig.PIbsMun * (1 - _taxConfig.PRedAliqMun) * (1 - 0);
        mun.Add(new XElement("pAliqEfetMun", Helpers.FormatRate(aliqEfetMun)));
        valores.Add(mun);
        
        // FED group - Federal CBS information
        var fed = new XElement("fed");
        fed.Add(new XElement("pCBS", Helpers.FormatRate(_taxConfig.PCbs))); // CBS rate
        fed.Add(new XElement("pRedAliqCBS", Helpers.FormatRate(_taxConfig.PRedAliqCbs))); // CBS rate reduction %
        
        // pAliqEfetCBS: Effective CBS rate after reductions
        // Formula: pAliqEfetCBS = pCBS × (1 - pRedAliqCBS) × (1 - pRedutor)
        var aliqEfetCBS = _taxConfig.PCbs * (1 - _taxConfig.PRedAliqCbs) * (1 - 0);
        fed.Add(new XElement("pAliqEfetCBS", Helpers.FormatRate(aliqEfetCBS)));
        valores.Add(fed);
        
        section.Add(valores);
        
        // totCIBS group - Totals and breakdown
        var totCIBS = new XElement("totCIBS");
        
        // vTotNF: Total invoice value including taxes
        // Formula (2026): vTotNF = vLiq (taxes not added yet, will be mandatory from 2027)
        // For MVP in 2026: vTotNF = invoice amount (taxes are "por fora" = not included)
        totCIBS.Add(new XElement("vTotNF", Helpers.FormatMonetary(amount)));
        
        // gTribRegular group - Regular taxation values (non-government)
        var gTribRegular = new XElement("gTribRegular");
        
        // State IBS regular taxation
        gTribRegular.Add(new XElement("pAliqEfeRegIBSUF", Helpers.FormatRate(aliqEfetUF))); // Effective state rate
        var vTribRegIBSUF = baseCalculo * aliqEfetUF; // vTribRegIBSUF = vBC × pAliqEfeRegIBSUF
        gTribRegular.Add(new XElement("vTribRegIBSUF", Helpers.FormatMonetary(vTribRegIBSUF)));
        
        // Municipal IBS regular taxation
        gTribRegular.Add(new XElement("pAliqEfeRegIBSMun", Helpers.FormatRate(aliqEfetMun))); // Effective municipal rate
        var vTribRegIBSMun = baseCalculo * aliqEfetMun; // vTribRegIBSMun = vBC × pAliqEfeRegIBSMun
        gTribRegular.Add(new XElement("vTribRegIBSMun", Helpers.FormatMonetary(vTribRegIBSMun)));
        
        // CBS regular taxation
        gTribRegular.Add(new XElement("pAliqEfeRegCBS", Helpers.FormatRate(aliqEfetCBS))); // Effective CBS rate
        var vTribRegCBS = baseCalculo * aliqEfetCBS; // vTribRegCBS = vBC × pAliqEfeRegCBS
        gTribRegular.Add(new XElement("vTribRegCBS", Helpers.FormatMonetary(vTribRegCBS)));
        
        totCIBS.Add(gTribRegular);
        
        // gTribCompraGov group - Government purchase taxation (NOT APPLICABLE for private sector)
        // Omit this group for MVP since we're not handling government contracts
        // If needed later, this would contain different IBS/CBS calculations for public sector purchases
        
        // gIBS group - IBS totals (state + municipal)
        var gIBS = new XElement("gIBS");
        
        // vIBSTot: Total IBS value (state + municipal)
        // Formula: vIBSTot = vIBSUF + vIBSMun
        var vIBSUF = baseCalculo * aliqEfetUF;
        var vIBSMun = baseCalculo * aliqEfetMun;
        var vIBSTot = vIBSUF + vIBSMun;
        gIBS.Add(new XElement("vIBSTot", Helpers.FormatMonetary(vIBSTot)));
        
        // gIBSCredPres group - Presumed credit for IBS (tax incentive mechanism)
        // For MVP: no presumed credits, omit this group
        // Future: if issuer has tax incentives, add:
        // <gIBSCredPres>
        //   <pCredPresIBS>rate</pCredPresIBS>
        //   <vCredPresIBS>value</vCredPresIBS>
        // </gIBSCredPres>
        
        // gIBSUFTot group - State IBS breakdown
        var gIBSUFTot = new XElement("gIBSUFTot");
        
        // vDifUF: State IBS deferral (postponed payment - usually 0 for immediate taxation)
        // Formula: vDifUF = vIBSUF × pDifUF (where pDifUF = deferral percentage, typically 0)
        gIBSUFTot.Add(new XElement("vDifUF", Helpers.FormatMonetary(0)));
        
        // vIBSUF: Total state IBS value
        // Formula: vIBSUF = vBC × (pIBSUF or pAliqEfetUF)
        gIBSUFTot.Add(new XElement("vIBSUF", Helpers.FormatMonetary(vIBSUF)));
        gIBS.Add(gIBSUFTot);
        
        // gIBSMunTot group - Municipal IBS breakdown
        var gIBSMunTot = new XElement("gIBSMunTot");
        
        // vDifMun: Municipal IBS deferral (typically 0)
        // Formula: vDifMun = vIBSMun × pDifMun
        gIBSMunTot.Add(new XElement("vDifMun", Helpers.FormatMonetary(0)));
        
        // vIBSMun: Total municipal IBS value
        // Formula: vIBSMun = vBC × (pIBSMun or pAliqEfetMun)
        gIBSMunTot.Add(new XElement("vIBSMun", Helpers.FormatMonetary(vIBSMun)));
        gIBS.Add(gIBSMunTot);
        
        totCIBS.Add(gIBS);
        
        // gCBS group - CBS (federal contribution) breakdown
        var gCBS = new XElement("gCBS");
        
        // gCBSCredPres group - Presumed credit for CBS (omit for MVP - no incentives)
        // Future: if issuer has tax incentives:
        // <gCBSCredPres>
        //   <pCredPresCBS>rate</pCredPresCBS>
        //   <vCredPresCBS>value</vCredPresCBS>
        // </gCBSCredPres>
        
        // vDifCBS: CBS deferral (typically 0)
        // Formula: vDifCBS = vCBS × pDifCBS
        gCBS.Add(new XElement("vDifCBS", Helpers.FormatMonetary(0)));
        
        // vCBS: Total CBS value
        // Formula: vCBS = vBC × (pCBS or pAliqEfetCBS)
        var vCBS = baseCalculo * aliqEfetCBS;
        gCBS.Add(new XElement("vCBS", Helpers.FormatMonetary(vCBS)));
        
        totCIBS.Add(gCBS);
        section.Add(totCIBS);
        
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
        lista.Add(new XElement("unidade_valor_unitario", Helpers.FormatMonetary(invoice.Amount)));
        
        // Service codes - strip dots/formatting, must be integers per XSD
        lista.Add(new XElement("codigo_item_lista_servico", Helpers.StripDots(codes.ServiceListCode)));
        lista.Add(new XElement("codigo_nbs", Helpers.StripDots(codes.NbsCode)));
        
        lista.Add(new XElement("descritivo", Helpers.EscapeXmlContent(invoice.ServiceDescription)));
        
        // Tax rates - use 4 decimal places for rates
        lista.Add(new XElement("aliquota_item_lista_servico", Helpers.FormatRate(invoice.IssRate)));
        
        // situacao_tributaria: 4-digit code
        lista.Add(new XElement("situacao_tributaria", "0000"));
        
        lista.Add(new XElement("valor_tributavel", Helpers.FormatMonetary(invoice.Amount)));
        lista.Add(new XElement("valor_deducao", Helpers.FormatMonetary(0)));
        lista.Add(new XElement("valor_issrf", Helpers.FormatMonetary(0)));

        itens.Add(lista);
        return itens;
    }

    private async Task<XElement> BuildTomadorAsync(Consumer consumer, CancellationToken cancellationToken)
    {
        var tomador = new XElement("tomador");
        
        tomador.Add(new XElement("endereco_informado", "1")); // "0" or "N"=not informed, "1" or "S"=informed -- TODO: make it dynamic, based on data presence
        
        // Determine type: J=Legal entity (CNPJ), F=Natural person (CPF), E=Foreign
        tomador.Add(new XElement("tipo", Helpers.DetermineTomadorType(consumer.CpfCnpj)));
        tomador.Add(new XElement("cpfcnpj", Helpers.OnlyDigits(consumer.CpfCnpj)));
        tomador.Add(new XElement("ie", "")); // State registration - TODO:  make it dynamic if available
        tomador.Add(new XElement("nome_razao_social", Helpers.EscapeXmlContent(consumer.Name)));
        tomador.Add(new XElement("sobrenome_nome_fantasia", "")); // Trade name - usually empty
        
        // Address information
        tomador.Add(new XElement("logradouro", Helpers.EscapeXmlContent(consumer.Address.Street)));
        tomador.Add(new XElement("numero_residencia", consumer.Address.Number));
        tomador.Add(new XElement("complemento", Helpers.EscapeXmlContent(consumer.Address.Complement ?? "")));
        tomador.Add(new XElement("ponto_referencia", "")); // Reference point - optional
        tomador.Add(new XElement("bairro", Helpers.EscapeXmlContent(consumer.Address.Neighborhood)));
        
        var consumerTomCode = await GetTomCodeAsync(consumer.Address.City, consumer.Address.Uf, cancellationToken);
        tomador.Add(new XElement("cidade", consumerTomCode));
        tomador.Add(new XElement("cep", Helpers.OnlyDigits(consumer.Address.ZipCode)));
        
        tomador.Add(new XElement("email", Helpers.EscapeXmlContent(consumer.Email ?? "")));
        
        // Phone numbers - split DDD (area code) from number
        if (!string.IsNullOrEmpty(consumer.Phone))
        {
            string ddd, number;
            Helpers.ParsePhoneDetails(consumer, out ddd, out number);

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
        return municipality?.TomCode ?? "8083";
    }
}
