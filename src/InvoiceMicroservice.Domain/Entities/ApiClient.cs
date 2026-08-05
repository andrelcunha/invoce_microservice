namespace InvoiceMicroservice.Domain.Entities;

public class ApiClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = null!;
    public string ApiKeyHash { get; set; } = null!;
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
