using FluentValidation;
using InvoiceMicroservice.Application.Validators;

namespace InvoiceMicroservice.Application.Commands.Issuer;

public class RegisterIssuerCommandValidator : AbstractValidator<RegisterIssuerCommand>
{
    public RegisterIssuerCommandValidator()
    {
        RuleFor(x => x.Cnpj)
            .NotEmpty()
            .WithMessage("CNPJ é obrigatório.")
            .Must(ValidationHelpers.BeValidCnpj)
            .WithMessage("CNPJ inválido.");

        RuleFor(x => x.MunicipalInscription)
            // .NotEmpty().WithMessage("Inscrição Municipal é obrigatória.")
            .MaximumLength(50).WithMessage("Inscrição Municipal deve ter no máximo 50 caracteres.");

        RuleFor(x => x.TradeName)
            .NotEmpty().WithMessage("Nome Fantasia é obrigatório.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Razão Social é obrigatória.");

        RuleFor(x => x.Cnae) // must have seven digits
            .NotEmpty().WithMessage("CNAE é obrigatório.")
            .Matches(@"^\d{7}$").WithMessage("CNAE deve ter 7 dígitos.");

        RuleFor(x => x.Address)
            .NotNull().WithMessage("Endereço é obrigatório.")
            .SetValidator(new AddressValidator());
    }
}
