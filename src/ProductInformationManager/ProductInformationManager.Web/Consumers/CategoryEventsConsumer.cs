namespace ProductInformationManager.Web.Consumers;

using AlbyOnContainers.Kernel.Caching.Abstractions;
using AlbyOnContainers.Kernel.Caching.Cache;
using MassTransit;
using ProductInformationManager.Contracts;
using ProductInformationManager.Domain;
using ProductInformationManager.Web.Notifiers;

public class CategoryEventsConsumer(
    CategoryNotifier notifier,
    ICache cache,
    ILogger<CategoryEventsConsumer> logger) : IConsumer<CategoryCreatedEvent>, IConsumer<CategoryUpdatedEvent>, IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryCreated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryCreatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryUpdated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        logger.LogInformation("PIM UI category event received CategoryUpdatedEvent {CategoryId}", message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        var message = context.Message;
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        notifier.Notify(new CategoryDeleted(message.Id));
        logger.LogInformation("PIM UI category event received CategoryDeletedEvent {CategoryId}", message.Id);
    }
}
