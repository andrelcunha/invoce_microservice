using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.Entities;
using Credentials = InvoiceMicroservice.Domain.Entities.PortalCredentialsEntity;

using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

public record CreatePortalCredentialsCommand
{
    public required string IssuerCnpj { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public bool RequiresSignature { get; init; }
    public byte[]? Certificate { get; init; }
    public string? CertificatePassword { get; init; }
    public string PortalType { get; init; } = string.Empty;
}

public class CreatePortalCredentialsCommandHandler
{
    private readonly IIssuerRepository _issuerRepository;
    private readonly IPortalCredentialsRepository _repository;

    public CreatePortalCredentialsCommandHandler(IIssuerRepository issuerRepository, IPortalCredentialsRepository repository)
    {
        _issuerRepository = issuerRepository;
        _repository = repository;
    }
    public async Task<Credentials?> HandleAsync(CreatePortalCredentialsCommand command, byte[]? certificateData, CancellationToken ct)
    {
        var cnpj = new Cnpj(command.IssuerCnpj);

        var issuer = await _issuerRepository.GetByCnpjAsync(cnpj, ct);
        if (issuer == null)
        {
            throw new InvalidOperationException($"Emissor com CNPJ {cnpj.Value} não encontrado.");
        }
        var existingCredentials = await _repository.GetByIssuerCnpjAsync(command.IssuerCnpj, ct);
        if (existingCredentials != null)
        {
            throw new InvalidOperationException($"Credenciais para emissor com CNPJ {cnpj.Value} já existem.");
        }

        if (command.RequiresSignature && (certificateData == null || certificateData.Length == 0))
        {
            throw new InvalidOperationException("Certificado digital é obrigatório para emissores que requerem assinatura.");
        }
        string? certificatePasswordHash = null;
        if (!string.IsNullOrWhiteSpace(command.CertificatePassword))
                certificatePasswordHash = HashPassword(command.CertificatePassword);

        var credentials = Credentials.Create(
            issuer.Id,
            GetPortalFromString(command.PortalType),
            command.Username,
            HashPassword(command.Password),
            command.RequiresSignature,
            certificateData,
            certificatePasswordHash
        );

        await _repository.AddAsync(credentials, ct);
        return await _repository.GetByIssuerCnpjAsync(command.IssuerCnpj, ct);
    }

        private static string HashPassword(string password)
    {
        // // Using SHA256 for simplicity - in production, use BCrypt or Argon2
        // using var sha256 = SHA256.Create();
        // var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        // return Convert.ToBase64String(bytes);
        return password;
    }

    private static PortalType GetPortalFromString(string portalTypeStr)
    {
        if (!Enum.TryParse<PortalType>(portalTypeStr, true, out var portalType))
            throw new ArgumentException($"Invalid portal type: {portalTypeStr}");
        return portalType;
    }
}

