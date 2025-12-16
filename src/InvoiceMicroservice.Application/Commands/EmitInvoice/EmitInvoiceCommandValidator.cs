using System;
using System.Data;
using FluentValidation;
using System.Text.RegularExpressions;   

namespace InvoiceMicroservice.Application.Commands.EmitInvoice;

public class EmitInvoiceCommandValidator : AbstractValidator<EmitInvoiceCommand>
{
    private static readonly HashSet<string> ValidUfs = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
        "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public  EmitInvoiceCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("ClientId is required.");

            RuleFor(x => x.Data).SetValidator(new InvoiceDataValidator());
    }

    internal class InvoiceDataValidator : AbstractValidator<InvoiceData>
    {
        public InvoiceDataValidator()
        {
            RuleFor(x => x.Issuer).SetValidator(new IssuerValidator());
            RuleFor(x => x.Consumer).SetValidator(new ConsumerValidator());

            RuleFor(x => x.ServiceDescription)
                .NotEmpty()
                .MaximumLength(2000)
                .WithMessage("ServiceDescription is required.");

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Amount must be greater than zero.")
                .LessThan(1_000_000).WithMessage("Amount exceeds the maximum allowed (R$ 1.000.000).");

            RuleFor(x => x.IssuedAt)
                .NotEmpty()
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("IssuedAt cannot be in the future.");
        }
    }

    internal class IssuerValidator : AbstractValidator<Issuer>
    {
        public IssuerValidator()
        {
            RuleFor(x => x.Cnpj)
                .NotEmpty()
                .Must(BeValidCnpj).WithMessage("Invalid CNPJ format or check digits.");

            RuleFor(x => x.MunicipalInscription)
                .NotEmpty().MaximumLength(20);

            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Cnae)
                .NotEmpty()
                .Matches(@"^\d{4}-\d\/\d{2}$").WithMessage("CNAE must match the format 'NNNN-N/NN'.");

            RuleFor(x => x.Address).SetValidator(new AddressValidator());
        }
    }

    internal class ConsumerValidator : AbstractValidator<Consumer>
    {
        public ConsumerValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.CpfCnpj)
                .NotEmpty()
                .Must(BeValidCpfOrCnpj).WithMessage("Invalid CPF/CNPJ format or check digits.");

            When(x => !string.IsNullOrEmpty(x.Email), () =>
            {
                RuleFor(x =>x.Email)
                    .EmailAddress()
                    .WithMessage("Invalid email format.");
            });

            When(x => !string.IsNullOrEmpty(x.Phone), () =>
            {
                RuleFor(x => x.Phone)
                    .Matches(@"^\d{10,11}$")
                    .WithMessage("Phone must have 10 or 11 digits (DDD + number).");
            });

            RuleFor(x => x.Address).SetValidator(new AddressValidator());
        }
    }

    internal class AddressValidator : AbstractValidator<Address>
    {
        public AddressValidator()
        {
            RuleFor(x => x.Street)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Number)
                .NotEmpty()
                .MaximumLength(20);

            When(x => !string.IsNullOrEmpty(x.Complement), () =>
            {
                RuleFor(x => x.Complement)
                    .MaximumLength(100);
            });

            RuleFor(x => x.Neighborhood)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.City)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Uf)
                .NotEmpty()
                .MaximumLength(2)
                .Must(uf => ValidUfs.Contains(uf))
                .WithMessage("Invalid Brazilian state (UF).");

            RuleFor(x => x.ZipCode)
                .NotEmpty()
                .Matches(@"^\d{5}-?\d{3}$")
                .WithMessage("ZipCode must be 8 digits  (NNNNN-NNN or NNNNNNNN).");
        }
    }

    private static bool BeValidCnpj(string cnpj)
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

    private static bool BeValidCpfOrCnpj(string cpfOrCnpj)
    {
        var digits = OnlyDigits(cpfOrCnpj);
        return digits.Length == 11 ? BeValidCpf(digits) :BeValidCnpj(digits);
    }

    private static bool BeValidCpf(string digits)
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

    private static string OnlyDigits(string input) =>
        new([.. input.Where(char.IsDigit)]);
}