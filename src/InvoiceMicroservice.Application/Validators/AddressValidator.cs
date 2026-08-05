
using FluentValidation;
using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Application.Validators;

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty()
            .WithMessage("Logradouro é obrigatório")
            .MaximumLength(200);

        RuleFor(x => x.Number)
            .NotEmpty()
            .WithMessage("Número é obrigatório")
            .MaximumLength(20);

        RuleFor(x => x.Neighborhood)
            .NotEmpty()
            .WithMessage("Bairro é obrigatório")
            .MaximumLength(100);

        RuleFor(x => x.City)
            .NotEmpty()
            .WithMessage("Cidade é obrigatória")
            .MaximumLength(100);

        RuleFor(x => x.Uf)
            .NotEmpty()
            .WithMessage("UF é obrigatória")
            .Must(ValidationHelpers.IsValidUf)
            .WithMessage("UF deve ser uma sigla válida (ex: SC, SP, RJ)");

        RuleFor(x => x.ZipCode)
            .NotEmpty()
            .WithMessage("CEP é obrigatório")
            .Matches(@"^\d{5}-\d{3}$")
            .WithMessage("CEP deve estar no formato NNNNN-NNN");

        RuleFor(x => x.IbgeCode)
            .NotEmpty()
            .WithMessage("Código IBGE é obrigatório")
            .Matches(@"^\d{7}$")
            .WithMessage("Código IBGE deve ter 7 dígitos");
    }
}