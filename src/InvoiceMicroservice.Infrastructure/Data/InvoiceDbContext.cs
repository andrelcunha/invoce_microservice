using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Data;

public class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : base(options) { }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ServiceTypeTaxMapping> ServiceTypeTaxMappings => Set<ServiceTypeTaxMapping>();
    public DbSet<IssuerEntity> Issuers => Set<IssuerEntity>();
    public DbSet<PortalCredentialsEntity> PortalCredentials => Set<PortalCredentialsEntity>();
    public DbSet<InvoiceEmissionJob> InvoiceEmissionJobs => Set<InvoiceEmissionJob>();
    public DbSet<InvoiceEmissionResult> InvoiceEmissionResults => Set<InvoiceEmissionResult>();

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

            // Indexes for common lookups
            entity.HasIndex(e => e.IbgeCode).IsUnique();
            entity.HasIndex(e => e.TomCode);
            entity.HasIndex(e => new { e.Name, e.Uf });
            entity.HasIndex(e => e.Uf);
        });

        // IssuerEntity configuration
        modelBuilder.Entity<IssuerEntity>(entity =>
        {
            entity.ToTable("issuers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Cnpj)
                .HasColumnName("cnpj")
                .HasMaxLength(14)
                .IsRequired()
                .HasConversion(
                    v => v.Value,
                    v => new Cnpj(v));

            entity.HasIndex(e => e.Cnpj).IsUnique();

            entity.Property(e => e.MunicipalInscription)
                .HasColumnName("municipal_inscription")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.TradeName)
                .HasColumnName("trade_name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.LegalName)
                .HasColumnName("legal_name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Cnae)
                .HasColumnName("cnae")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.AddressJson)
                .HasColumnName("address")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(e => e.RegimeTributario)
                .HasColumnName("regime_tributario")
                .HasConversion<int>();

            entity.Property(e => e.SubRegimeTributario)
                .HasColumnName("sub_regime_tributario")
                .HasConversion<int>();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now() at time zone 'utc'");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("now() at time zone 'utc'");

            entity.HasOne(e => e.PortalCredentials)
                .WithOne(pc => pc.Issuer)
                .HasForeignKey<PortalCredentialsEntity>(pc => pc.IssuerId);
        });

        // PortalCredentials configuration
        modelBuilder.Entity<PortalCredentialsEntity>(entity =>
        {
            entity.ToTable("portal_credentials");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.IssuerId)
                .HasColumnName("issuer_id")
                .IsRequired();

            entity.Property(e => e.PortalType)
                .HasColumnName("portal_type")
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.Username)
                .HasColumnName("username")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.RequiresSignature)
                .HasColumnName("requires_signature")
                .HasDefaultValue(false);

            entity.Property(e => e.CertificateData)
                .HasColumnName("certificate_data")
                .HasColumnType("bytea");

            entity.Property(e => e.CertificatePasswordHash)
                .HasColumnName("certificate_password_hash")
                .HasMaxLength(500);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now() at time zone 'utc'");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("now() at time zone 'utc'");

            // Composite index for efficient lookup
            entity.HasIndex(e => new { e.IssuerId, e.PortalType, e.IsActive });
        });

        modelBuilder.Entity<InvoiceEmissionJob>(b =>
        {
            b.ToTable("invoice_emission_jobs");
            b.HasKey(x => x.Id);

            b.Property(x => x.IssuerCnpj).HasMaxLength(14).IsRequired();
            b.Property(x => x.PayloadJson).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

            b.Property(x => x.LockedBy).HasMaxLength(128);
            b.Property(x => x.LastError).HasMaxLength(4000);

            b.HasIndex(x => new { x.Status, x.NextRetryAt, x.CreatedAt });
            b.HasIndex(x => x.IssuerCnpj);
        });

        modelBuilder.Entity<InvoiceEmissionResult>(b =>
        {
            b.ToTable("invoice_emission_results");

            b.HasKey(x => x.Id);

            b.Property(x => x.IssuerCnpj)
                .HasMaxLength(14)
                .IsRequired();

            b.Property(x => x.PortalType)
                .HasMaxLength(32)
                .IsRequired();

            b.Property(x => x.NumeroDfe).HasMaxLength(64);
            b.Property(x => x.SerieDfe).HasMaxLength(32);
            b.Property(x => x.CodStatus).HasMaxLength(32);
            b.Property(x => x.StatusDescription).HasMaxLength(512);
            b.Property(x => x.Protocolo).HasMaxLength(128);
            b.Property(x => x.ChaveAcesso).HasMaxLength(128);
            b.Property(x => x.VerificationCode).HasMaxLength(128);
            b.Property(x => x.DocumentUrl).HasMaxLength(2048);

            b.Property(x => x.RequestXml).HasColumnType("text");
            b.Property(x => x.ResponseRaw).HasColumnType("text");
            b.Property(x => x.AlertsJson).HasColumnType("text");
            b.Property(x => x.ErrorMessage).HasColumnType("text");

            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.JobId).IsUnique();
            b.HasIndex(x => x.IssuerCnpj);
            b.HasIndex(x => x.ChaveAcesso);

            b.HasOne(x => x.Job)
                .WithOne()
                .HasForeignKey<InvoiceEmissionResult>(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
