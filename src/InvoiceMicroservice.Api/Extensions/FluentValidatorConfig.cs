using FluentValidation;
using FluentValidation.AspNetCore;
using  InvoiceMicroservice.Application.Commands.EmitInvoice;

namespace InvoiceMicroservice.Api.Extensions;

public static class FluentValidatorConfig
{
    public static IServiceCollection AddFluentValidationConfiguration(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<EmitInvoiceCommandValidator>();
        services.AddFluentValidationAutoValidation();
        return services;
    }
}
