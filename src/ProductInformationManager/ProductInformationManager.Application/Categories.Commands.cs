using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    ILogger<CreateCategoryConsumer> logger) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;
        var parentId = command.ParentId.HasValue ? new CategoryId(command.ParentId.Value) : null;
        var parentPath = string.Empty;

        if (parentId is not null)
        {
            parentPath = await db.Categories
                                 .AsNoTracking()
                                 .Where(c => c.Id == parentId)
                                 .Select(c => c.Path)
                                 .SingleOrDefaultAsync(context.CancellationToken)
                         ?? throw new DomainException($"Parent category with ID {parentId.Value} not found.");
        }

        // Domain event (CategoryCreatedDomainEvent) is appended internally by Category.Create()
        var category = Category.Create(command.Name, command.Description ?? string.Empty, parentId, parentPath);

        await db.Categories.AddAsync(category, context.CancellationToken);

        // DomainEventDispatcherInterceptor dispatches the domain event to the bus transactionally
        await db.SaveChangesAsync(context.CancellationToken);
        await cache.InvalidateAsync(context.CancellationToken);

        logger.LogInformation(
            "Category created {CategoryId} ({CategoryName}) with parent {ParentId} and path {Path}",
            category.Id.Value, category.Name, category.ParentId?.Value, category.Path);
    }
}

public class UpdateCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    ILogger<UpdateCategoryConsumer> logger) : IConsumer<UpdateCategory>
{
    public async Task Consume(ConsumeContext<UpdateCategory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.Id);

        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken)
                       ?? throw new DomainException($"Category with ID {command.Id} not found.");

        // Domain event (CategoryUpdatedDomainEvent) is appended internally by Rename()
        category.Rename(command.Name, command.Description);

        // DomainEventDispatcherInterceptor dispatches the domain event to the bus transactionally
        await db.SaveChangesAsync(context.CancellationToken);
        await cache.InvalidateAsync(context.CancellationToken);

        logger.LogInformation(
            "Category updated {CategoryId} ({CategoryName}) with path {Path}",
            category.Id.Value, category.Name, category.Path);
    }
}

public class DeleteCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    ILogger<DeleteCategoryConsumer> logger) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.Id);

        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken)
                       ?? throw new DomainException($"Category with ID {command.Id} not found.");

        // Domain event (CategoryDeletedDomainEvent) is appended internally by MarkDeleted()
        category.MarkDeleted();
        db.Categories.Remove(category);

        // DomainEventDispatcherInterceptor dispatches the domain event to the bus transactionally
        await db.SaveChangesAsync(context.CancellationToken);
        await cache.InvalidateAsync(context.CancellationToken);

        logger.LogInformation("Category deleted {CategoryId}", category.Id.Value);
    }
}