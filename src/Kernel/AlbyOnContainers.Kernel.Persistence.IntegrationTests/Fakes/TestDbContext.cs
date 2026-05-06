using Microsoft.EntityFrameworkCore;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<FakeAuditableEntity> FakeEntities => Set<FakeAuditableEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<FakeAuditableEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
        });
    }
}
