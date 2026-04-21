using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using Attribute = ProductInformationManager.Domain.Attribute;

namespace ProductInformationManager.Infrastructure;

public class ProductContext(
    DbContextOptions<ProductContext> options, 
    IEnumerable<IInterceptor> interceptors) : DbContext(options), IUnitOfWork
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DescriptionType> DescriptionTypes => Set<DescriptionType>();
    public DbSet<DescriptionValue> DescriptionValues => Set<DescriptionValue>();
    public DbSet<AttributeType> AttributeTypes => Set<AttributeType>();
    public DbSet<Attribute> Attributes => Set<Attribute>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Aggiungiamo l'interceptor per l'AuditableEntity che mi hai passato
        optionsBuilder.AddInterceptors(interceptors);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Bulk configuration per gli Strongly-Typed IDs
        // Istruisce EF Core a tradurre tutti i record ID in Guid su Postgres
        configurationBuilder.Properties<ProductId>().HaveConversion<ProductIdConverter>();
        configurationBuilder.Properties<CategoryId>().HaveConversion<CategoryIdConverter>();
        configurationBuilder.Properties<AttributeId>().HaveConversion<AttributeIdConverter>();
        configurationBuilder.Properties<AttributeTypeId>().HaveConversion<AttributeTypeIdConverter>();
        configurationBuilder.Properties<DescriptionTypeId>().HaveConversion<DescriptionTypeIdConverter>();
        configurationBuilder.Properties<DescriptionValueId>().HaveConversion<DescriptionValueIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Abilitazione estensione ltree e sequence per SKU
        modelBuilder.HasPostgresExtension("ltree");
        modelBuilder.HasSequence<int>("product_sku_seq").StartsAt(1);

        // === Category ===
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Path).HasColumnType("ltree");
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);

            // Relazione self-referencing (Parent -> Children)
            entity.HasOne(c => c.Parent)
                  .WithMany(c => c.Children)
                  .HasForeignKey(c => c.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // === DescriptionType ===
        modelBuilder.Entity<DescriptionType>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.Name).IsUnique();
            entity.Property(d => d.Name).HasMaxLength(100);
            entity.Property(d => d.Description).HasMaxLength(500);
            entity.Ignore(d => d.IsGlobal);
        });

        // === DescriptionValue ===
        modelBuilder.Entity<DescriptionValue>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Value).HasMaxLength(500);

            entity.HasOne(v => v.DescriptionType)
                  .WithMany(d => d.Values)
                  .HasForeignKey(v => v.DescriptionTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // === AttributeType ===
        modelBuilder.Entity<AttributeType>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(100);
            entity.Property(a => a.Description).HasMaxLength(500);
            entity.HasIndex(a => a.Name).IsUnique();
        });

        // === Attribute ===
        modelBuilder.Entity<Attribute>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(100);
            entity.Property(a => a.Value).HasMaxLength(500);

            entity.HasOne(a => a.AttributeType)
                  .WithMany(at => at.Attributes)
                  .HasForeignKey(a => a.AttributeTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // === Product ===
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Sku).HasMaxLength(10);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.HasIndex(p => p.Sku).IsUnique();

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // 1. Mapping Molti-a-Molti Esplicito per Attributes
            entity.HasMany(p => p.Attributes)
                  .WithMany() // Non c'è navigazione inversa da Attribute a Product nel dominio
                  .UsingEntity<ProductAttribute>( // <--- USIAMO LA CLASSE DI JOIN AUDITABILE
                      j => j.HasOne(pa => pa.Attribute)
                            .WithMany()
                            .HasForeignKey(pa => pa.AttributeId)
                            .OnDelete(DeleteBehavior.Cascade),
                      j => j.HasOne(pa => pa.Product)
                            .WithMany()
                            .HasForeignKey(pa => pa.ProductId)
                            .OnDelete(DeleteBehavior.Cascade),
                      j =>
                      {
                          // Chiave primaria composta per la tabella di join
                          j.HasKey(pa => new { pa.ProductId, pa.AttributeId });
                          j.ToTable("ProductAttributes"); // Nome tabella
                      }
                  );

            // 2. Mapping Molti-a-Molti Esplicito per Descriptions
            entity.HasMany(p => p.Descriptions)
                  .WithMany()
                  .UsingEntity<ProductDescription>( // <--- USIAMO LA CLASSE DI JOIN AUDITABILE
                      j => j.HasOne(pd => pd.DescriptionValue)
                            .WithMany()
                            .HasForeignKey(pd => pd.DescriptionValueId)
                            .OnDelete(DeleteBehavior.Cascade),
                      j => j.HasOne(pd => pd.Product)
                            .WithMany()
                            .HasForeignKey(pd => pd.ProductId)
                            .OnDelete(DeleteBehavior.Cascade),
                      j =>
                      {
                          j.HasKey(pd => new { pd.ProductId, pd.DescriptionValueId });
                          j.ToTable("ProductDescriptions");
                      }
                  );
        });
    }
}

// ====================================================================
// VALUE CONVERTERS (Traducono gli Strongly-Typed IDs per Postgres)
// ====================================================================
public class ProductIdConverter() : ValueConverter<ProductId, Guid>(id => id.Value, value => new ProductId(value));
public class CategoryIdConverter() : ValueConverter<CategoryId, Guid>(id => id.Value, value => new CategoryId(value));
public class AttributeIdConverter() : ValueConverter<AttributeId, Guid>(id => id.Value, value => new AttributeId(value));
public class AttributeTypeIdConverter() : ValueConverter<AttributeTypeId, Guid>(id => id.Value, value => new AttributeTypeId(value));
public class DescriptionTypeIdConverter() : ValueConverter<DescriptionTypeId, Guid>(id => id.Value, value => new DescriptionTypeId(value));
public class DescriptionValueIdConverter() : ValueConverter<DescriptionValueId, Guid>(id => id.Value, value => new DescriptionValueId(value));