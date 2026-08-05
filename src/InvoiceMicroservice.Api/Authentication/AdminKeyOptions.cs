namespace InvoiceMicroservice.Api.Authentication;

public sealed class AdminKeyOptions
{
    public const string SectionName = "Authentication:AdminKey";
    public string Key { get; set; } = string.Empty;
}
