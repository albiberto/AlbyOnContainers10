using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Keys;
using ProductInformationManager.Contracts;
using MassTransit;
using ProductInformationManager.Domain;
using ProductInformationManager.Web.Notifiers;

namespace ProductInformationManager.Web.Consumers;

public class CategoryEventsConsumer(
    CategoryNotifier notifier,
    IAlbyCache cache,
    ILogger<CategoryEventsConsumer> logger) : IConsumer<CategoryCreatedEvent>, IConsumer<CategoryUpdatedEvent>, IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CacheKey.For<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryCreated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryCreatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CacheKey.For<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryUpdated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryUpdatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CacheKey.For<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryDeleted(message.Id));
        logger.LogInformation("PIM UI category event received CategoryDeletedEvent {CategoryId}", message.Id);
    }
}
