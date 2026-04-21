using AlbyOnContainers.Shared.Contracts;
using MassTransit;
using ProductInformationManager.Application.Cache;

namespace ProductInformationManager.Application.Consumers;

public class CategoryConsumer(CategoryCache cache) :
    IConsumer<CategoryCreatedEvent>,
    IConsumer<CategoryUpdatedEvent>,
    IConsumer<CategoryDeletedEvent>
{
    public async Task Consume(ConsumeContext<CategoryCreatedEvent> context) =>
        await cache.InvalidateAsync(context.CancellationToken);

    public async Task Consume(ConsumeContext<CategoryUpdatedEvent> context) =>
        await cache.InvalidateAsync(context.CancellationToken);

    public async Task Consume(ConsumeContext<CategoryDeletedEvent> context) =>
        await cache.InvalidateAsync(context.CancellationToken);
}