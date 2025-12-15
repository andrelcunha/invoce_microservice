
using InvoiceMicroservice.Api.Extensions;

namespace InvoiceMicroservice.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        builder.Services.AddOpenApiConfiguration();

        var app = builder.Build();


        // Configure the HTTP request pipeline.
        app.UseOpenApiConfiguration();
        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapGet("/", () => Results.Ok("Invoice Microservice - Alive"));

        app.Run();
    }
}
