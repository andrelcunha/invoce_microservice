using System.Data;
using FluentValidation;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Application.Validators;

namespace InvoiceMicroservice.Application.Commands.EmitInvoice;

public class EmitInvoiceCommandValidator : AbstractValidator<EmitInvoiceCommand>
{

    public EmitInvoiceCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("ClientId is required.");

        RuleFor(x => x.IssuerCnpj)
            .NotEmpty().WithMessage("IssuerCnpj is required.")
            .Must(ValidationHelpers.BeValidCnpj).WithMessage("Invalid CNPJ format or check digits.");

        RuleFor(x => x.Data).SetValidator(new EmitInvoiceDataValidator());
    }

    internal class EmitInvoiceDataValidator : AbstractValidator<EmitInvoiceData>
    {
        public EmitInvoiceDataValidator()
        {
            RuleFor(x => x.NfseSeries)
                .GreaterThan(0).WithMessage("NfseSeries must be greater than zero.")
                .LessThan(10000).WithMessage("NfseSeries must be less than 10000.");

            RuleFor(x => x.NfseNumber)
                .GreaterThan(0).WithMessage("NfseNumber must be greater than zero.")
                .LessThan(1000000).WithMessage("NfseNumber must be less than 1000000.");

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
                .LessThanOrEqualTo(_ => DateTime.UtcNow).WithMessage("IssuedAt cannot be in the future.");

            RuleFor(x => x.ServiceTypeKey).MaximumLength(100).When(x => x.ServiceTypeKey != null);

            RuleFor(x => x.PisCofinsCts)
                .Must(BeValidPisCofinsCst)
                .When(x => x.PisCofinsCts.HasValue)
                .WithMessage("PisCofinsCts must be one of: 1-9, 49-56, 60-67, 70-75, 98, 99.");

            // ISS rate validation: Brazilian municipalities can charge 2% to 5%
            RuleFor(x => x.IssRate)
                .InclusiveBetween(0.02m, 0.05m)
                .WithMessage("ISS rate must be between 2% (0.02) and 5% (0.05)");
        }

        private static bool BeValidPisCofinsCst(int? value)
        {
            if (!value.HasValue)
                return true;

            return value.Value is >= 1 and <= 9
                or >= 49 and <= 56
                or >= 60 and <= 67
                or >= 70 and <= 75
                or 98
                or 99;
        }
    }

    internal class IssuerValidator : AbstractValidator<IssuerDto>
    {
        public IssuerValidator()
        {
            RuleFor(x => x.Cnpj)
                .NotEmpty()
                .Must(ValidationHelpers.BeValidCnpj).WithMessage("Invalid CNPJ format or check digits.");

            RuleFor(x => x.MunicipalInscription)
                .MaximumLength(20).When(x => x.MunicipalInscription != null);

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
                .Must(ValidationHelpers.BeValidCpfOrCnpj).WithMessage("Invalid CPF/CNPJ format or check digits.");

            When(x => !string.IsNullOrEmpty(x.Email), () =>
            {
                RuleFor(x => x.Email)
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
                .Must(uf => ValidationHelpers.IsValidUf(uf))
                .WithMessage("Invalid Brazilian state (UF).");

            RuleFor(x => x.ZipCode)
                .NotEmpty()
                .Matches(@"^\d{5}-?\d{3}$")
                .WithMessage("ZipCode must be 8 digits  (NNNNN-NNN or NNNNNNNN).");
        }
    }
}
