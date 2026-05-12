using System.Globalization;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;

namespace InvoiceMicroservice.Infrastructure.Xml;

internal static class Helpers

{
    internal static string EscapeXmlContent(string content)
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

    internal static string OnlyDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        
        return new string(input.Where(char.IsDigit).ToArray());
    }

    internal static string DetermineTomadorType(string cpfCnpj)
    {
        var digits = OnlyDigits(cpfCnpj);
        
        // No document = foreign entity
        if (digits.Length == 0) return "E";
        
        // CPF (11 digits) = natural person, CNPJ (14 digits) = legal entity
        return digits.Length == 11 ? "F" : "J";
    }

    internal static void ParsePhoneDetails(Consumer consumer, out string ddd, out string number)
    {
        if (string.IsNullOrEmpty(consumer.Phone))
        {
            ddd = "";
            number = "";
            return;
        }
        var phoneDigits = OnlyDigits(consumer.Phone);
        ddd = phoneDigits.Length >= 2 ? phoneDigits[..2] : "";
        number = phoneDigits.Length > 2 ? phoneDigits[2..] : "";
    }

    /// <summary>
    /// Formats percentage rates using COMMA with max 2 decimals (e.g., 2,50 for 2.5%)
    /// Per IPM XSD pattern: rates must be 0-2 decimals, NOT 4 decimals
    /// </summary>
    internal static string FormatRate(decimal rate)
    {
        var percentage = rate * 100;
        // Use pt-BR culture for comma separator, format with 2 decimals
        return percentage.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"));
    }

    /// <summary>
    /// Strips dots/dashes from code strings. IPM XSD expects integer types for NBS/service list codes.
    /// Example: "1.1803.29.00" → "118032900", "14.01" → "1401"
    /// </summary>
    internal static string StripDots(string code)
    {
        if (string.IsNullOrEmpty(code))
            return "";
        
        return code.Replace(".", "").Replace("-", "").Replace("/", "");
    }

    /// <summary>
    /// Formats monetary values using COMMA as decimal separator with 2 decimals (e.g., 1500,00)
    /// Per IPM XSD pattern: 0|0,0|0,00|[0-9]{1}\d{0,12}([,]\d{2})?
    /// </summary>
    internal static string FormatMonetary(decimal amount)
    {
        return amount.ToString("F2", CultureInfo.GetCultureInfo("pt-BR"));
    }

    internal static string? NormalizePisCofinsCst(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!int.TryParse(value, out var parsed) || parsed <= 0)
            return null;

        return parsed.ToString("D2", CultureInfo.InvariantCulture);
    }

    internal static async Task<ServiceTypeTaxCodes> GetServiceCodesAsync(
        IServiceTypeTaxMappingRepository repo,
        string? serviceTypeKey,
        string? cnaeCode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(serviceTypeKey))
        {
            var mapping = await repo.GetByServiceTypeKeyAsync(serviceTypeKey, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        if (!string.IsNullOrEmpty(cnaeCode))
        {
            var mapping = await repo.GetByCnaeCodeAsync(cnaeCode, cancellationToken);
            if (mapping != null)
                return new ServiceTypeTaxCodes(mapping);
        }

        return ServiceTypeTaxCodes.Default();
    }
}