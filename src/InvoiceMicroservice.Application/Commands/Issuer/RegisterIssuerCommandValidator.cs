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
            .NotEmpty()
            .WithMessage("Inscrição Municipal é obrigatória.")
            .MaximumLength(50).WithMessage("Inscrição Municipal deve ter no máximo 50 caracteres.");

        RuleFor(x => x.TradeName)
            .NotEmpty().WithMessage("Nome Fantasia é obrigatório.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("Razão Social é obrigatória.");

        RuleFor(x => x.Cnae)
            .NotEmpty().WithMessage("CNAE é obrigatório.")
            .Matches(@"^\d{4}-\d\/\d{2}$")
            .WithMessage("CNAE deve estar no formato 'NNNN-N/NN' (ex: 4520-0/05 ).");

        RuleFor(x => x.Address)
            .NotNull().WithMessage("Endereço é obrigatório.")
            .SetValidator(new AddressValidator());

        RuleFor(x => x.PortalUsername)
            .NotEmpty().WithMessage("Usuário do portal é obrigatório.");

        RuleFor(x => x.PortalPassword)
            .NotEmpty().WithMessage("Senha do portal é obrigatória.")
            .MinimumLength(6).WithMessage("Senha do portal deve conter no mínimo 6 caracteres.");
    }
}
