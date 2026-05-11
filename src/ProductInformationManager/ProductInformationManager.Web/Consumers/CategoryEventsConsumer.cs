namespace ProductInformationManager.Web.Consumers;

using MassTransit;
using ProductInformationManager.Application;
using ProductInformationManager.Contracts;
using ProductInformationManager.Web.Notifiers;
using ZiggyCreatures.Caching.Fusion;

public class CategoryEventsConsumer(
    CategoryNotifier notifier,
    IFusionCache cache,
    ILogger<CategoryEventsConsumer> logger) : IConsumer<CategoryCreatedEvent>, IConsumer<CategoryUpdatedEvent>, IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CategoryCacheKeys.All, token: context.CancellationToken);
        notifier.Notify(new CategoryCreated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryCreatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CategoryCacheKeys.All, token: context.CancellationToken);
        notifier.Notify(new CategoryUpdated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryUpdatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(CategoryCacheKeys.All, token: context.CancellationToken);
        notifier.Notify(new CategoryDeleted(message.Id));
        logger.LogInformation("PIM UI category event received CategoryDeletedEvent {CategoryId}", message.Id);
    }
}
