using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Messaging.Attributes;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Contracts;
using ProductInformationManager.Domain;

namespace ProductInformationManager.Application.Consumers;

using AlbyOnContainers.Kernel.Caching.Abstractions;

[EventConsumer]
public class CategoryConsumer(ICache cache, ILogger<CategoryConsumer> logger) :
    IConsumer<CategoryCreatedEvent>,
    IConsumer<CategoryUpdatedEvent>,
    IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryCreatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryUpdatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        await cache.RemoveAsync(Key.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryDeletedEvent {CategoryId}", context.Message.Id);
    }
}
