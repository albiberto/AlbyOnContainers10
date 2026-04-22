using System.Text.RegularExpressions;
using AlbyOnContainers.Shared.Contracts;
using AlbyOnContainers.Shared.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductInformationManager.Application.Cache;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public partial class CreateCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    IBus bus,
    ILogger<CreateCategoryConsumer> logger) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;

        var parentId = command.ParentId.HasValue ? new CategoryId(command.ParentId.Value) : null;
        var path = NormalizePath(command.Name);

        if (parentId is not null)
        {
            var parentPath = await db.Categories
                                 .AsNoTracking()
                                 .Where(c => c.Id == parentId)
                                 .Select(c => c.Path)
                                 .SingleOrDefaultAsync(context.CancellationToken)
                             ?? throw new DomainException($"Parent category with ID {parentId.Value} not found.");

            path = $"{parentPath}.{path}";
        }

        var category = new Category(command.Name, path, command.Description ?? string.Empty, parentId);

        await db.Categories.AddAsync(category, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);

        await cache.InvalidateAsync(context.CancellationToken);

        await bus.Publish(
            new CategoryCreatedEvent(category.Id.Value, category.Name, category.Description, category.Path, category.ParentId?.Value),
            context.CancellationToken);

        logger.LogInformation(
            "PIM category created {CategoryId} ({CategoryName}) with parent {ParentId} and path {Path}",
            category.Id.Value,
            category.Name,
            category.ParentId?.Value,
            category.Path);
    }

    private static string NormalizePath(string name) => PathNormalizer().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex PathNormalizer();
}

public class UpdateCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    IBus bus,
    ILogger<UpdateCategoryConsumer> logger) : IConsumer<UpdateCategory>
{
    public async Task Consume(ConsumeContext<UpdateCategory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.Id);

        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken)
                       ?? throw new DomainException($"Category with ID {command.Id} not found.");

        category.Rename(command.Name, command.Description);
        await db.SaveChangesAsync(context.CancellationToken);

        await cache.InvalidateAsync(context.CancellationToken);

        await bus.Publish(
            new CategoryUpdatedEvent(category.Id.Value, category.Name, category.Description, category.Path, category.ParentId?.Value),
            context.CancellationToken);

        logger.LogInformation(
            "PIM category updated {CategoryId} ({CategoryName}) with path {Path}",
            category.Id.Value,
            category.Name,
            category.Path);
    }
}

public class DeleteCategoryConsumer(
    ProductContext db,
    CategoryCache cache,
    IBus bus,
    ILogger<DeleteCategoryConsumer> logger) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.Id);

        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken)
                       ?? throw new DomainException($"Category with ID {command.Id} not found.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await cache.InvalidateAsync(context.CancellationToken);

        await bus.Publish(new CategoryDeletedEvent(category.Id.Value), context.CancellationToken);

        logger.LogInformation("PIM category deleted {CategoryId}", category.Id.Value);
    }
}
