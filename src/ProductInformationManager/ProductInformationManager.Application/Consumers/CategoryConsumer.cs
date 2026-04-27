using MassTransit;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Contracts;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Consumers;

public class CategoryConsumer(CategoryCache cache, ILogger<CategoryConsumer> logger) :
    IConsumer<CategoryCreatedEvent>,
    IConsumer<CategoryUpdatedEvent>,
    IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        await cache.InvalidateAllAsync(context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryCreatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        await cache.InvalidateAllAsync(context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryUpdatedEvent {CategoryId}", context.Message.Id);
    }

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        await cache.InvalidateAllAsync(context.CancellationToken);
        logger.LogInformation("PIM category cache invalidated after CategoryDeletedEvent {CategoryId}", context.Message.Id);
    }
}
