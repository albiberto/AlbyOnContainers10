namespace AlbyOnContainers.Kernel.Messaging.UnitTests;

using AlbyOnContainers.Kernel.Domain.SeedWork;
using AlbyOnContainers.Kernel.Messaging.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

[TestFixture]
public sealed class DomainEventDispatcherInterceptorTests
{
    private sealed record TestDomainEvent : IDomainEvent;
    private sealed record TestIntegrationEvent(Guid Id);

    private sealed class TestAggregate : AggregateRoot
    {
        public Guid Id { get; private set; } = Guid.NewGuid();

        public static TestAggregate WithEvent()
        {
            var aggregate = new TestAggregate();
            aggregate.AppendEvent(new TestDomainEvent());
            return aggregate;
        }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<TestAggregate>().HasKey(e => e.Id);
    }

    private sealed class ThrowingSavingChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("save failed after domain events were published to the outbox");
    }

    [Test]
    public async Task SaveChangesAsync_WhenSaveSucceeds_ClearsDomainEventsAfterSave()
    {
        // Arrange
        var aggregate = TestAggregate.WithEvent();
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var mapper = Substitute.For<IDomainEventMapper>();
        mapper.Map(Arg.Any<IDomainEvent>()).Returns([new TestIntegrationEvent(Guid.NewGuid())]);

        await using var dbContext = CreateDbContext(
            new DomainEventDispatcherInterceptor(
                NullLogger<DomainEventDispatcherInterceptor>.Instance,
                publishEndpoint,
                mapper));

        dbContext.Aggregates.Add(aggregate);

        // Act
        await dbContext.SaveChangesAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(aggregate.DomainEvents, Is.Empty);
            publishEndpoint.Received(1).Publish(
                Arg.Any<object>(),
                typeof(TestIntegrationEvent),
                Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public void SaveChangesAsync_WhenSaveFailsAfterOutboxPublish_RetainsDomainEventsForRetry()
    {
        // Arrange
        var aggregate = TestAggregate.WithEvent();
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var mapper = Substitute.For<IDomainEventMapper>();
        mapper.Map(Arg.Any<IDomainEvent>()).Returns([new TestIntegrationEvent(Guid.NewGuid())]);

        using var dbContext = CreateDbContext(
            new DomainEventDispatcherInterceptor(
                NullLogger<DomainEventDispatcherInterceptor>.Instance,
                publishEndpoint,
                mapper),
            new ThrowingSavingChangesInterceptor());

        dbContext.Aggregates.Add(aggregate);

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(aggregate.DomainEvents, Has.Count.EqualTo(1));
            publishEndpoint.Received(1).Publish(
                Arg.Any<object>(),
                typeof(TestIntegrationEvent),
                Arg.Any<CancellationToken>());
        });
    }

    private static TestDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptors)
            .Options;

        return new(options);
    }
}
