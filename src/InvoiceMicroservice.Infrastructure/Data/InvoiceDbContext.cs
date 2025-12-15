using Microsoft.EntityFrameworkCore;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Infrastructure.Data;

public class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : base(options)
    {
    }

    public DbSet<Invoice> Invoices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var invoice = modelBuilder.Entity<Invoice>();
            invoice.HasKey(i => i.Id);
            
            invoice.Property(i => i.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            invoice.Property(i => i.ClientId)
                .HasMaxLength(100)
                .IsRequired();

            invoice.Property(i => i.IssuerCnpj)
                .HasConversion(c => c.Value, v => new Cnpj(v))
                .HasMaxLength(14)
                .IsRequired();

            invoice.Property(i => i.IssuerData)
                .HasColumnType("jsonb")
                .IsRequired();

            invoice.Property(i => i.ConsumerData)
                .HasColumnType("jsonb")
                .IsRequired();

            invoice.Property(i => i.Amount)
                .HasColumnType("decimal(10,2)");
                
            invoice.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
            
            invoice.Property(i => i.ErrorDetails)
                .HasColumnType("jsonb");

            invoice.Property(i => i.ExternalResponse)
                .HasColumnType("jsonb"); 
            
            invoice.Property(i => i.CreatedAt)
                .HasDefaultValueSql("now() at time zone 'utc'");
        
            // Indexes
            invoice.HasIndex(i => i.ClientId);
            invoice.HasIndex(i => i.IssuerCnpj);
            invoice.HasIndex(i => i.ExternalInvoiceId);
            invoice.HasIndex(i => new { i.Status, i.CreatedAt });
    }
}
