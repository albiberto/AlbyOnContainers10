namespace AlbyOnContainers.Kernel.Persistence.UnitTests.Interceptors;

using AlbyOnContainers.Kernel.Domain.SeedWork;
using AlbyOnContainers.Kernel.Persistence.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class DomainEventDispatcherInterceptorBaseTests
{
    [Fact]
    public void SavingChanges_throws_for_synchronous_save_changes()
    {
        var interceptor = new DomainEventDispatcherInterceptorBase(NullLogger<DomainEventDispatcherInterceptorBase>.Instance);

        var exception = Assert.Throws<InvalidOperationException>(() => interceptor.SavingChanges(default!, default));

        Assert.Contains("SaveChangesAsync", exception.Message);
    }

    [Fact]
    public async Task SaveChangesAsync_maps_publishes_and_clears_domain_events()
    {
        var publishEndpoint = new RecordingPublishEndpoint();
        var integrationMessage = new TestIntegrationMessage(Guid.NewGuid());
        var mapper = new StubDomainEventMapper(_ => [integrationMessage]);
        await using var dbContext = CreateDbContext(publishEndpoint, mapper);
        var aggregate = new TestAggregate();
        aggregate.Raise(new TestDomainEvent(Guid.NewGuid()));

        dbContext.Aggregates.Add(aggregate);
        await dbContext.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
        var published = Assert.Single(publishEndpoint.Published);
        Assert.Same(integrationMessage, published.Message);
        Assert.Equal(typeof(TestIntegrationMessage), published.MessageType);
    }

    [Fact]
    public async Task SaveChangesAsync_clears_domain_events_when_publish_endpoint_is_missing()
    {
        var mapper = new StubDomainEventMapper(_ => [new TestIntegrationMessage(Guid.NewGuid())]);
        await using var dbContext = CreateDbContext(publishEndpoint: null, mapper);
        var aggregate = new TestAggregate();
        aggregate.Raise(new TestDomainEvent(Guid.NewGuid()));

        dbContext.Aggregates.Add(aggregate);
        await dbContext.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
        Assert.Equal(0, mapper.MapCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_does_not_publish_when_mapper_returns_no_messages()
    {
        var publishEndpoint = new RecordingPublishEndpoint();
        var mapper = new StubDomainEventMapper(_ => []);
        await using var dbContext = CreateDbContext(publishEndpoint, mapper);
        var aggregate = new TestAggregate();
        aggregate.Raise(new TestDomainEvent(Guid.NewGuid()));

        dbContext.Aggregates.Add(aggregate);
        await dbContext.SaveChangesAsync();

        Assert.Empty(aggregate.DomainEvents);
        Assert.Empty(publishEndpoint.Published);
        Assert.Equal(1, mapper.MapCallCount);
    }

    private static DomainEventTestDbContext CreateDbContext(
        IPublishEndpoint? publishEndpoint,
        IDomainEventMapper? mapper)
    {
        var interceptor = new DomainEventDispatcherInterceptorBase(
            NullLogger<DomainEventDispatcherInterceptorBase>.Instance,
            publishEndpoint,
            mapper);

        var options = new DbContextOptionsBuilder<DomainEventTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new(options);
    }

    private sealed class DomainEventTestDbContext(DbContextOptions<DomainEventTestDbContext> options) : DbContext(options)
    {
        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public int Id { get; init; }

        public void Raise(IDomainEvent domainEvent) => AppendEvent(domainEvent);
    }

    private sealed record TestDomainEvent(Guid Id) : IDomainEvent;

    private sealed record TestIntegrationMessage(Guid Id);

    private sealed class StubDomainEventMapper(Func<IDomainEvent, IEnumerable<object>> map) : IDomainEventMapper
    {
        public int MapCallCount { get; private set; }

        public IEnumerable<object> Map(IDomainEvent domainEvent)
        {
            MapCallCount++;
            return map(domainEvent);
        }
    }

    private sealed class RecordingPublishEndpoint : IPublishEndpoint
    {
        public List<(object Message, Type MessageType)> Published { get; } = [];

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new NoopConnectHandle();

        public Task Publish<T>(T message, CancellationToken cancellationToken = default)
            where T : class
        {
            Published.Add((message, typeof(T)));
            return Task.CompletedTask;
        }

        public Task Publish<T>(
            T message,
            IPipe<PublishContext<T>> publishPipe,
            CancellationToken cancellationToken = default)
            where T : class =>
            Publish(message, cancellationToken);

        public Task Publish<T>(
            T message,
            IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default)
            where T : class =>
            Publish(message, cancellationToken);

        public Task Publish(object message, CancellationToken cancellationToken = default)
        {
            Published.Add((message, message.GetType()));
            return Task.CompletedTask;
        }

        public Task Publish(
            object message,
            IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default) =>
            Publish(message, cancellationToken);

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        {
            Published.Add((message, messageType));
            return Task.CompletedTask;
        }

        public Task Publish(
            object message,
            Type messageType,
            IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default) =>
            Publish(message, messageType, cancellationToken);

        public Task Publish<T>(object values, CancellationToken cancellationToken = default)
            where T : class =>
            Publish(values, typeof(T), cancellationToken);

        public Task Publish<T>(
            object values,
            IPipe<PublishContext<T>> publishPipe,
            CancellationToken cancellationToken = default)
            where T : class =>
            Publish<T>(values, cancellationToken);

        public Task Publish<T>(
            object values,
            IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default)
            where T : class =>
            Publish<T>(values, cancellationToken);

        private sealed class NoopConnectHandle : ConnectHandle
        {
            public void Disconnect()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
