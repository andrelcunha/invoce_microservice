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
            root.Add(new XElement("testMode", "true"));

        root.Add(new XElement("identificador", invoice.Id.ToString("N")));
        
        // <nf> section
        var nf = new XElement("nf");
        // nf.Add(new XElement("serie_nfse", 1)); // TODO: Check if needed
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

        // <items>
        root.Add(BuildItems(invoice, issuer));


        // skipping optional blocks: genericos, produtos, forma_pagamento, pedagio (unsupported)
        
        var doc = new XDocument(root);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private object? BuildIbsCbsNfSection(decimal amount)
    {
        throw new NotImplementedException();
    }

    private object? BuildItems(Invoice invoice, Issuer issuer)
    {
        throw new NotImplementedException();
    }

    private object? BuildTomador(Consumer consumer)
    {
        throw new NotImplementedException();
    }

    private object? GetTomCode(string city, string uf)
    {
        throw new NotImplementedException();
    }



    private static string FormatDecimal(decimal amount, int? fixedDigits = null)
    {
        var format = fixedDigits.HasValue ? $"F{fixedDigits.Value}" : "F2";
        return amount.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
