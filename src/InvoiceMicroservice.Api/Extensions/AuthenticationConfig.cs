using InvoiceMicroservice.Api.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace InvoiceMicroservice.Api.Extensions;

public static class AuthenticationConfig
{
    public static void AddAuthenticationConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ApiKeyAuthenticationOptions>()
            .Bind(configuration.GetSection(ApiKeyAuthenticationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "API key header name must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Key), "API key must be configured.")
            .ValidateOnStart();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.SchemeName;
                options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.SchemeName;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName,
                _ => { });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName)
                .RequireAuthenticatedUser()
                .Build());
    }
}
