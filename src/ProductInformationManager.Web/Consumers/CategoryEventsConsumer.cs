using AlbyOnContainers.Shared.Contracts;
using MassTransit;
using ProductInformationManager.Web.Notifiers;

namespace ProductInformationManager.Web.Consumers;

public class CategoryEventsConsumer(CategoryNotifier notifier) : IConsumer<CategoryCreatedEvent>, IConsumer<CategoryUpdatedEvent>, IConsumer<CategoryDeletedEvent>
{
    public Task Consume(ConsumeContext<CategoryCreatedEvent> context)
    {
        var message = context.Message;
        notifier.Notify(new CategoryCreated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<CategoryUpdatedEvent> context)
    {
        var message = context.Message;
        notifier.Notify(new CategoryUpdated(message.Id, message.Name, message.Description, message.Path, message.ParentId));
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<CategoryDeletedEvent> context)
    {
        var message = context.Message;
        notifier.Notify(new CategoryDeleted(message.Id));
        return Task.CompletedTask;
    }
}