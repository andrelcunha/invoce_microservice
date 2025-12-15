namespace InvoiceMicroservice.Domain.ValueObjects;

public record Cnpj
{
    public string Value { get; }

    public Cnpj(string value)
    {
        var digits = new string([.. value.Where(char.IsDigit)]);
        if (digits.Length != 14)
            throw new ArgumentException("CNPJ must have 14 digits.", nameof(value));

        Value = digits;
    }

    public override string ToString() => Value;
}
