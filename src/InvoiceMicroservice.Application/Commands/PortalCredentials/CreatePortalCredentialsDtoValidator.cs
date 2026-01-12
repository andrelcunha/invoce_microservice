using FluentValidation;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

public class CreatePortalCredentialsDtoValidator : AbstractValidator<CreatePortalCredentialsDto>
{
    public CreatePortalCredentialsDtoValidator()
    {
        RuleFor(x => x.IssuerCnpj)
            .NotEmpty().WithMessage("IssuerCnpj is required.")
            .Must(BeValidCnpj).WithMessage("IssuerCnpj is not valid.");
        
        RuleFor(x => x.ApiBaseUrl)
            .NotEmpty().WithMessage("ApiBaseUrl is required.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("ApiBaseUrl must be a valid URL.");
        
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(200).WithMessage("Username cannot exceed 200 characters.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        
        // Certificate validation when signature is required
        When(x => x.RequiresSignature, () =>
        {
            RuleFor(x => x.CertificatePassword)
                .NotEmpty().WithMessage("CertificatePassword is required when RequiresSignature is true.");
        });
    }

    private bool BeValidCnpj(string cnpj)
    {
        try
        {
            _ = new Cnpj(cnpj);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
