namespace AlbyOnContainers.Kernel.Persistence.UnitTests.Interceptors;

using AlbyOnContainers.Kernel.Domain.SeedWork;
using AlbyOnContainers.Kernel.Persistence.Interceptors;
using AlbyOnContainers.Kernel.Security.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class AuditableInterceptorTests
{
    [Fact]
    public void SavingChanges_throws_for_synchronous_save_changes()
    {
        var interceptor = new AuditableInterceptor(NullLogger<AuditableInterceptor>.Instance);

        var exception = Assert.Throws<InvalidOperationException>(() => interceptor.SavingChanges(default!, default));

        Assert.Contains("SaveChangesAsync", exception.Message);
    }

    [Fact]
    public async Task SaveChangesAsync_sets_creation_metadata_from_current_user()
    {
        var currentUser = new StubCurrentUserService(userId: "user-123");
        await using var dbContext = CreateDbContext(currentUser);
        var entity = new TestAuditableEntity { Name = "Created" };
        var startedAt = DateTimeOffset.UtcNow;

        dbContext.Entities.Add(entity);
        await dbContext.SaveChangesAsync();

        Assert.Equal("user-123", entity.CreatedBy);
        Assert.True(entity.CreatedAt >= startedAt);
        Assert.Null(entity.UpdatedBy);
        Assert.Null(entity.UpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_sets_update_metadata_from_current_user()
    {
        var currentUser = new StubCurrentUserService(userId: "editor-456");
        await using var dbContext = CreateDbContext(currentUser);
        var entity = new TestAuditableEntity { Name = "Original" };

        dbContext.Entities.Add(entity);
        await dbContext.SaveChangesAsync();
        var startedAt = DateTimeOffset.UtcNow;

        entity.Name = "Updated";
        await dbContext.SaveChangesAsync();

        Assert.Equal("editor-456", entity.UpdatedBy);
        Assert.True(entity.UpdatedAt >= startedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_uses_system_when_current_user_is_missing()
    {
        await using var dbContext = CreateDbContext(currentUserService: null);
        var entity = new TestAuditableEntity { Name = "System-created" };

        dbContext.Entities.Add(entity);
        await dbContext.SaveChangesAsync();

        Assert.Equal("System", entity.CreatedBy);
    }

    private static AuditableTestDbContext CreateDbContext(ICurrentUserService? currentUserService)
    {
        var interceptor = new AuditableInterceptor(
            NullLogger<AuditableInterceptor>.Instance,
            currentUserService);

        var options = new DbContextOptionsBuilder<AuditableTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new(options);
    }

    private sealed class AuditableTestDbContext(DbContextOptions<AuditableTestDbContext> options) : DbContext(options)
    {
        public DbSet<TestAuditableEntity> Entities => Set<TestAuditableEntity>();
    }

    private sealed class TestAuditableEntity : AuditableEntity
    {
        public int Id { get; init; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class StubCurrentUserService(string? userId) : ICurrentUserService
    {
        public string? UserId { get; } = userId;
        public string? UserName => null;
        public string? Email => null;
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyCollection<string> Roles => [];
        public string? GetClaim(string claimType) => null;
        public bool IsInRole(string role) => false;
    }
}
