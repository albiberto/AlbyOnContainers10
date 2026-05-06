using AlbyOnContainers.Kernel.Persistence.IntegrationTests.Fakes;
using NUnit.Framework;

namespace AlbyOnContainers.Kernel.Persistence.IntegrationTests.Interceptors;

[TestFixture]
public class AuditableEntityInterceptorTests : IntegrationTestBase
{
    [Test]
    public async Task Should_Set_CreatedAt_And_CreatedBy_On_Add()
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
}
