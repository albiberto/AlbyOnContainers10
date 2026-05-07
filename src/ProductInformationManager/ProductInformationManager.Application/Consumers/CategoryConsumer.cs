using AlbyOnContainers.Kernel.Caching.Cache;
using AlbyOnContainers.Kernel.Caching.Keys;
using AlbyOnContainers.Kernel.Messaging.Attributes;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Contracts;
using ProductInformationManager.Domain;

namespace ProductInformationManager.Application.Consumers;

[EventConsumer]
public class CategoryConsumer(IAlbyCache cache, ILogger<CategoryConsumer> logger) :
    IConsumer<CategoryCreatedEvent>,
    IConsumer<CategoryUpdatedEvent>,
    IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        await cache.RemoveAsync(CacheKey.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryCreatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        await cache.RemoveAsync(CacheKey.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryUpdatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        await cache.RemoveAsync(CacheKey.Type<Category>("All"), context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryDeletedEvent {CategoryId}", context.Message.Id);
    }
}
