using Microsoft.EntityFrameworkCore;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Infrastructure.Data;

public class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : base(options) { }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ServiceTypeTaxMapping> ServiceTypeTaxMappings => Set<ServiceTypeTaxMapping>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();

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

        // ServiceTypeTaxMapping configuration
        modelBuilder.Entity<ServiceTypeTaxMapping>(entity =>
        {
            entity.ToTable("service_type_tax_mappings");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            
            entity.Property(e => e.ServiceTypeKey)
                .HasColumnName("service_type_key")
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.CnaeCode)
                .HasColumnName("cnae_code")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(500)
                .IsRequired();
            
            entity.Property(e => e.NbsCode)
                .HasColumnName("nbs_code")
                .HasMaxLength(20)
                .IsRequired();
            
            entity.Property(e => e.ServiceListCode)
                .HasColumnName("service_list_code")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.OperationIndicator)
                .HasColumnName("operation_indicator")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.TaxSituationCode)
                .HasColumnName("tax_situation_code")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.TaxClassificationCode)
                .HasColumnName("tax_classification_code")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now() at time zone 'utc'");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            // Indexes
            entity.HasIndex(e => e.ServiceTypeKey).IsUnique();
            entity.HasIndex(e => e.CnaeCode);
            entity.HasIndex(e => new { e.IsActive, e.ServiceTypeKey });
        });

        // Municipality configuration
        modelBuilder.Entity<Municipality>(entity =>
        {
            entity.ToTable("municipalities");
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();
            
            entity.Property(e => e.IbgeCode)
                .HasColumnName("ibge_code")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();
            
            entity.Property(e => e.Uf)
                .HasColumnName("uf")
                .HasMaxLength(2)
                .IsRequired();
            
            entity.Property(e => e.TomCode)
                .HasColumnName("tom_code")
                .HasMaxLength(10)
                .IsRequired();
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasMaxLength(20);
            
            entity.Property(e => e.ExtinguishedAt)
                .HasColumnName("extinguished_at")
                .HasMaxLength(20);
            
            // Indexes for common lookups
            entity.HasIndex(e => e.IbgeCode).IsUnique();
            entity.HasIndex(e => e.TomCode);
            entity.HasIndex(e => new { e.Name, e.Uf });
            entity.HasIndex(e => e.Uf);
        });
    }
}
