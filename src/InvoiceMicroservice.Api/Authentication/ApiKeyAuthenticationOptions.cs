using Microsoft.AspNetCore.Authentication;

namespace InvoiceMicroservice.Api.Authentication;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string SectionName = "Authentication:ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";
}
