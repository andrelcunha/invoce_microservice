
using FluentValidation;

namespace InvoiceMicroservice.Application.Validators;

public static class ValidationHelpers
{
    public static bool BeValidCnpj(string cnpj)
    {
        var digits = OnlyDigits(cnpj);
    if (digits.Length != 14) return false;

    // CNPJ check digit calculation
    int[] multipliers1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    int[] multipliers2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    var sum = 0;
    for (int i = 0; i < 12; i++)
        sum += (digits[i] - '0') * multipliers1[i];

    var mod = sum % 11;
    var digit1 = mod < 2 ? 0 : 11 - mod;

    if (digits[12] - '0' != digit1) return false;

    sum = 0;
    for (int i = 0; i < 13; i++)
        sum += (digits[i] - '0') * multipliers2[i];

    mod = sum % 11;
    var digit2 = mod < 2 ? 0 : 11 - mod;

    return digits[13] - '0' == digit2;
    }

    public static bool BeValidCpfOrCnpj(string cpfOrCnpj)
    {
        var digits = OnlyDigits(cpfOrCnpj);
        return digits.Length == 11 ? BeValidCpf(digits) :BeValidCnpj(digits);
    }

    public static bool BeValidCpf(string digits)
    {
        if (digits.Length != 11) return false;

        // Reject known invalid patterns like 00000000000
        if (new string(digits[0], 11) == digits) return false;

        int[] multipliers1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] multipliers2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

        var sum = 0;
        for (int i = 0; i < 9; i++)
            sum += (digits[i] - '0') * multipliers1[i];

        var mod = sum % 11;
        var digit1 = mod < 2 ? 0 : 11 - mod;
        if (digits[9] - '0' != digit1) return false;

        sum = 0;
        for (int i = 0; i < 10; i++)
            sum += (digits[i] - '0') * multipliers2[i];

        mod = sum % 11;
        var digit2 = mod < 2 ? 0 : 11 - mod;

        return digits[10] - '0' == digit2;
    }

    public static string OnlyDigits(string input) =>
        new([.. input.Where(char.IsDigit)]);

    public static bool IsValidUf(string uf)
    {
        var validUfs = new HashSet<string>
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO",
            "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE",
            "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP",
            "SE", "TO"
        };
        return validUfs.Contains(uf.ToUpper());
    }
}
