using AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;
using NUnit.Framework;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Interceptors;

[TestFixture]
public class AuditableEntityInterceptorTests : IntegrationTestBase
{
    [Test]
    public async Task SaveChanges_WhenEntityIsAdded_ShouldSetCreatedAtAndCreatedBy()
    {
        // Arrange
        var entity = new FakeAuditableEntity
        {
            Name = "Test Entity"
        };

        // Act
        DbContext.FakeEntities.Add(entity);
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(entity.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(entity.CreatedBy, Is.EqualTo("TestUser"));
        });
    }

    [Test]
    public async Task SaveChanges_WhenEntityIsModified_ShouldSetUpdatedAtAndUpdatedBy()
    {
        // Arrange
        var entity = new FakeAuditableEntity
        {
            Name = "Original Name"
        };
        DbContext.FakeEntities.Add(entity);
        await DbContext.SaveChangesAsync();

        var originalCreatedAt = entity.CreatedAt;
        var originalCreatedBy = entity.CreatedBy;

        CurrentUserService.UserId = "ModifierUser";
        entity.Name = "Modified Name";

        // Act
        await DbContext.SaveChangesAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(entity.UpdatedAt, Is.Not.Null);
            Assert.That(entity.UpdatedBy, Is.EqualTo("ModifierUser"));
            Assert.That(entity.CreatedAt, Is.EqualTo(originalCreatedAt));
            Assert.That(entity.CreatedBy, Is.EqualTo(originalCreatedBy));
        });
    }
}
