using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Infrastructure.Xml;

public interface IIpmXmlBuilder
{
    string BuildInvoiceXml(Invoice invoice, bool isTestMode = true);
}

public class IpmXmlBuilder : IIpmXmlBuilder
{
    private readonly TaxConfig _taxConfig;

    private static readonly Dictionary<string, string> TomCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["concordia-sc"] = "8083",
        ["florianopolis-sc"] = "8105",
        ["chapeco-sc"] = "8081",
        ["joacaba-sc"] = "8177"
    };

    public IpmXmlBuilder(TaxConfig taxConfig)
    {
        _taxConfig = taxConfig;
    }

    public string BuildInvoiceXml(Invoice invoice, bool isTestMode = true)
    {
        var issuer = JsonSerializer.Deserialize<Issuer>(invoice.IssuerData)!;
        var consumer = JsonSerializer.Deserialize<Consumer>(invoice.ConsumerData)!;

        var root = new XElement("nfse");
        if (isTestMode)
            root.Add(new XElement("nfse_teste", "1"));

        root.Add(new XElement("identificador", invoice.Id.ToString("N")));
        
        // <nf> section
        var nf = new XElement("nf");
        nf.Add(new XElement("data_fato_gerador", DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        nf.Add(new XElement("valor_total", FormatDecimal(invoice.Amount)));
        nf.Add(new XElement("valor_desconto", "0,00"));
        nf.Add(new XElement("valor_ir", "0,00"));
        nf.Add(new XElement("valor_inss", "0,00"));
        nf.Add(new XElement("valor_contribuicao_social", "0,00"));
        nf.Add(new XElement("valor_rps", "0,00"));
        nf.Add(new XElement("valor_pis", "0,00"));
        nf.Add(new XElement("valor_cofins", "0,00"));
        nf.Add(new XElement("observacao", ""));

        nf.Add(BuildIbsCbsNfSection(invoice.Amount));

        root.Add(nf);

        // <prestador> 
        root.Add(new XElement("prestador",
            new XElement("cpfcnpj", issuer.Cnpj),
            new XElement("cidade", GetTomCode(issuer.Address.City, issuer.Address.Uf))
        ));

        // <tomador>
        root.Add(BuildTomador(consumer));

        // <itens>
        root.Add(BuildItems(invoice, issuer));

        // <forma_pagamento>
        root.Add(new XElement("forma_pagamento",
            new XElement("tipo_pagamento", "1")
        ));

        var doc = new XDocument(root);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private XElement BuildIbsCbsNfSection(decimal amount)
    {
        var section = new XElement("ibs_cbs_nf");
        
        section.Add(new XElement("valor_ibs_uf", FormatDecimal(amount * _taxConfig.PIbsUf)));
        section.Add(new XElement("aliquota_ibs_uf", FormatDecimal(_taxConfig.PIbsUf, 4)));
        section.Add(new XElement("valor_reducao_ibs_uf", FormatDecimal(amount * _taxConfig.PRedAliqUf)));
        
        section.Add(new XElement("valor_ibs_mun", FormatDecimal(amount * _taxConfig.PIbsMun)));
        section.Add(new XElement("aliquota_ibs_mun", FormatDecimal(_taxConfig.PIbsMun, 4)));
        section.Add(new XElement("valor_reducao_ibs_mun", FormatDecimal(amount * _taxConfig.PRedAliqMun)));
        
        section.Add(new XElement("valor_cbs", FormatDecimal(amount * _taxConfig.PCbs)));
        section.Add(new XElement("aliquota_cbs", FormatDecimal(_taxConfig.PCbs, 4)));
        section.Add(new XElement("valor_reducao_cbs", FormatDecimal(amount * _taxConfig.PRedAliqCbs)));

        return section;
    }

    private XElement BuildItems(Invoice invoice, Issuer issuer)
    {
        var itens = new XElement("itens");
        var lista = new XElement("lista");

        lista.Add(new XElement("tributa_municipio_prestador", "S"));
        lista.Add(new XElement("codigo_local_prestacao_servico", GetTomCode(issuer.Address.City, issuer.Address.Uf)));
        lista.Add(new XElement("unidade_codigo", "1"));
        lista.Add(new XElement("unidade_quantidade", "1"));
        lista.Add(new XElement("unidade_valor_unitario", FormatDecimal(invoice.Amount)));
        lista.Add(new XElement("codigo_item_lista_servico", "0024")); // Default service code
        lista.Add(new XElement("descritivo", EscapeXmlContent(invoice.ServiceDescription)));
        lista.Add(new XElement("aliquota_item_lista_servico", FormatDecimal(_taxConfig.PIbsUf, 2)));
        lista.Add(new XElement("situacao_tributaria", "0"));
        lista.Add(new XElement("valor_tributavel", FormatDecimal(invoice.Amount)));
        lista.Add(new XElement("valor_deducao", "0,00"));
        lista.Add(new XElement("valor_issrf", "0,00"));

        itens.Add(lista);
        return itens;
    }

    private XElement BuildTomador(Consumer consumer)
    {
        var tomador = new XElement("tomador");
        
        tomador.Add(new XElement("endereco_informado", "1"));
        tomador.Add(new XElement("tipo", DetermineTomadorType(consumer.CpfCnpj)));
        
        tomador.Add(new XElement("cpfcnpj", OnlyDigits(consumer.CpfCnpj)));
        tomador.Add(new XElement("ie", ""));
        tomador.Add(new XElement("nome_razao_social", EscapeXmlContent(consumer.Name)));
        tomador.Add(new XElement("sobrenome_nome_fantasia", ""));
        
        tomador.Add(new XElement("logradouro", EscapeXmlContent(consumer.Address.Street)));
        tomador.Add(new XElement("numero_residencia", consumer.Address.Number));
        tomador.Add(new XElement("complemento", EscapeXmlContent(consumer.Address.Complement ?? "")));
        tomador.Add(new XElement("ponto_referencia", ""));
        tomador.Add(new XElement("bairro", EscapeXmlContent(consumer.Address.Neighborhood)));
        tomador.Add(new XElement("cidade", GetTomCode(consumer.Address.City, consumer.Address.Uf)));
        tomador.Add(new XElement("cep", OnlyDigits(consumer.Address.ZipCode)));
        
        tomador.Add(new XElement("email", EscapeXmlContent(consumer.Email ?? "")));
        
        if (!string.IsNullOrEmpty(consumer.Phone))
        {
            var phoneDigits = OnlyDigits(consumer.Phone);
            var ddd = phoneDigits.Length >= 2 ? phoneDigits.Substring(0, 2) : "";
            var number = phoneDigits.Length > 2 ? phoneDigits.Substring(2) : "";
            
            tomador.Add(new XElement("ddd_fone_comercial", ddd));
            tomador.Add(new XElement("fone_comercial", number));
        }

        return tomador;
    }

    private string GetTomCode(string city, string uf)
    {
        var key = $"{city.ToLowerInvariant().Replace(" ", "-")}-{uf.ToLowerInvariant()}";
        
        if (TomCodes.TryGetValue(key, out var code))
        {
            return code;
        }

        // Default fallback - in production, this should be looked up from a database
        return "8083";
    }

    private static string DetermineTomadorType(string cpfCnpj)
    {
        var digits = OnlyDigits(cpfCnpj);
        return digits.Length == 11 ? "F" : "J"; // F = Físico (CPF), J = Jurídico (CNPJ)
    }

    private static string EscapeXmlContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        return content
            .Replace("&", "&amp;")   // Must be first
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;")
            .Replace("\"", "&quot;")
            .Replace("/", "");       // Remove forward slashes
    }

    private static string OnlyDigits(string input)
    {
        return new(input.Where(char.IsDigit).ToArray());
    }

    private static string FormatDecimal(decimal amount, int? fixedDigits = null)
    {
        var format = fixedDigits.HasValue ? $"F{fixedDigits.Value}" : "F2";
        return amount.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
