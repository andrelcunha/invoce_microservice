using System.Text.Json;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Application.Commands.Issuer;
public record RegisterIssuerCommand
{
    public required string Cnpj { get; init; }
    public required string MunicipalInscription { get; init; }
    public required string TradeName { get; init; }
    public required string LegalName { get; init; }
    public required string Cnae { get; init; }
    public required Address Address { get; init; }
    public required RegimeTributario RegimeTributario { get; init; }
    public required SubRegimeTributario SubRegimeTributario { get; init; }
    
    // public required PortalType PortalType { get; init; }
    // public required string PortalUsername { get; init; }
    // public required string PortalPassword { get; init; } // Will be hashed
    // public bool RequiresSignature { get; init; }
    // public byte[]? Certificate { get; init; }
    // public string? CertificatePassword { get; init; }
}

public class RegisterIssuerCommandHandler
{
    private readonly IIssuerRepository _repository;

    public RegisterIssuerCommandHandler(IIssuerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> HandleAsync(RegisterIssuerCommand command, CancellationToken cancellationToken)
    {
        var cnpj = new Cnpj(command.Cnpj);
        // Check if issuer with same CNPJ already exists
        if (await _repository.ExistsAsync(cnpj, cancellationToken))
            throw new InvalidOperationException($"Emissor com CNPJ {cnpj.Value} já cadastrado.");

        var addressJson = JsonSerializer.Serialize(command.Address, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var issuer = new IssuerEntity
        {
            Id = Guid.NewGuid(),
            Cnpj = cnpj,
            MunicipalInscription = command.MunicipalInscription,
            TradeName = command.TradeName,
            LegalName = command.LegalName,
            Cnae = command.Cnae,
            AddressJson = addressJson,
            RegimeTributario = command.RegimeTributario,
            SubRegimeTributario = command.SubRegimeTributario,
            IsActive = true,
        };


        // var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.PortalPassword);
        // var certificatePasswordHash = !string.IsNullOrEmpty(command.CertificatePassword)
        //     ? BCrypt.Net.BCrypt.HashPassword(command.CertificatePassword)
        //     : null;

        // var credentials = Domain.Entities.PortalCredentials.Create(
        //     issuer.Id,
        //     command.PortalType,
        //     command.PortalUsername,
        //     passwordHash,
        //     command.RequiresSignature,
        //     command.Certificate,
        //     certificatePasswordHash
        // );

        // issuer.PortalCredentials = credentials;

        await _repository.AddAsync(issuer, cancellationToken);
        return issuer.Id;
    }

    
}