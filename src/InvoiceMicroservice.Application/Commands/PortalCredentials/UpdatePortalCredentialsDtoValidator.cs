using FluentValidation;

namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

public class UpdatePortalCredentialsDtoValidator : AbstractValidator<UpdatePortalCredentialsDto>
{
    public UpdatePortalCredentialsDtoValidator()
    {
        RuleFor(x => x.ApiBaseUrl)
            .NotEmpty().WithMessage("ApiBaseUrl is required.")
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage("ApiBaseUrl must be a valid URL.");
        
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(200).WithMessage("Username cannot exceed 200 characters.");
        
        // Password validation when provided
        When(x => !string.IsNullOrWhiteSpace(x.Password), () =>
        {
            RuleFor(x => x.Password)
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.");
        });
        
        // Certificate validation when signature is required
        When(x => x.RequiresSignature, () =>
        {
            When(x => !string.IsNullOrWhiteSpace(x.CertificateBase64), () =>
            {
                RuleFor(x => x.CertificateBase64)
                    .Must(BeValidBase64).WithMessage("CertificateBase64 must be valid Base64 string.");
            });
        });
    }

    private bool BeValidBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return false;

        try
        {
            Convert.FromBase64String(base64);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
