namespace InvoiceMicroservice.Domain.ValueObjects;

public record class Cpf
{
    public string Value { get; }

    public Cpf(string value)
    {
        var digits = new string([.. value.Where(char.IsDigit)]);
        if (digits.Length != 11)
            throw new ArgumentException("CPF must have 11 digits.", nameof(value));

        Value = digits;
    }

    public override string ToString() => Value;
}
