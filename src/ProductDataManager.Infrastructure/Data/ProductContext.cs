using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProductDataManager.Models;
using Attribute = ProductDataManager.Models.Attribute;

namespace ProductDataManager.Infrastructure.Data;

public class ProductContext(DbContextOptions<ProductContext> options, IEnumerable<IInterceptor> interceptors) : DbContext(options), IUnitOfWork
{
    public DbSet<Category> Categories { get; private set; } = null!;
    public DbSet<DescriptionType> DescriptionTypes { get; private set; } = null!;
    public DbSet<DescriptionValue> DescriptionValues { get; private set; } = null!;
    public DbSet<DescriptionTypeCategory> DescriptionTypesCategories { get; private set; } = null!;
    public DbSet<AttributeType> AttributeTypes { get; private set; } = null!;
    public DbSet<Attribute> Attributes { get; private set; } = null!;
    public DbSet<Product> Products { get; private set; } = null!;
    public DbSet<ProductAttribute> ProductsAttributes { get; private set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(interceptors);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Abilitazione estensione ltree per PostgreSQL
        modelBuilder.HasPostgresExtension("ltree");

        // === Category ===
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Path).HasColumnType("ltree");
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);

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
            entity.Property(d => d.Name).HasMaxLength(30);
            entity.Property(d => d.Description).HasMaxLength(100);
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

        // === DescriptionTypeCategory (Join Table) ===
        modelBuilder.Entity<DescriptionTypeCategory>(entity =>
        {
            entity.HasKey(dtc => new { dtc.DescriptionTypeId, dtc.CategoryId });

            entity.HasOne(dtc => dtc.DescriptionType)
                .WithMany(d => d.DescriptionTypeCategories)
                .HasForeignKey(dtc => dtc.DescriptionTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dtc => dtc.Category)
                .WithMany(c => c.DescriptionTypeCategories)
                .HasForeignKey(dtc => dtc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === AttributeType ===
        modelBuilder.Entity<AttributeType>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(50);
            entity.Property(a => a.Description).HasMaxLength(200);
            entity.HasIndex(a => a.Name).IsUnique();
        });

        // === Attribute ===
        modelBuilder.Entity<Attribute>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(100);
            entity.Property(a => a.Value).HasMaxLength(500);

            entity.HasOne(a => a.AttributeType)
                .WithMany()
                .HasForeignKey(a => a.AttributeTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === Product ===
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Sku).HasMaxLength(50);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.HasIndex(p => p.Sku).IsUnique();

            entity.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // === ProductAttribute (Join Table) ===
        modelBuilder.Entity<ProductAttribute>(entity =>
        {
            entity.HasKey(pa => new { pa.ProductId, pa.AttributeId });

            entity.HasOne(pa => pa.Product)
                .WithMany(p => p.ProductAttributes)
                .HasForeignKey(pa => pa.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pa => pa.Attribute)
                .WithMany(a => a.ProductAttributes)
                .HasForeignKey(pa => pa.AttributeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
