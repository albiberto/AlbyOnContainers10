using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public partial class CreateCategoryConsumer(ProductContext db) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;
        
        // Traduzione del Nullable Primitive al Nullable Strongly-Typed ID
        CategoryId? parentId = command.ParentId.HasValue ? new CategoryId(command.ParentId.Value) : null;

        string path;
        if (parentId is not null)
        {
            var parent = await db.Categories.FindAsync([parentId], context.CancellationToken);
            if (parent is null)
            {
                await context.RespondAsync(new CreateCategoryResult(Guid.Empty, string.Empty));
                return;
            }
            path = $"{parent.Path}.{NormalizePath(command.Name)}";
        }
        else
        {
            path = NormalizePath(command.Name);
        }

        // Invocazione Aggregate Root
        var category = new Category(command.Name, path, command.Description, parentId);

        db.Categories.Add(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateCategoryResult(category.Id.Value, category.Path));
    }

    private static string NormalizePath(string name) =>
        PathNormalizer().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex PathNormalizer();
}

public class UpdateCategoryConsumer(ProductContext db) : IConsumer<UpdateCategory>
{
    public async Task Consume(ConsumeContext<UpdateCategory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.Id);
        
        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new UpdateCategoryResult(false));
            return;
        }

        // Chiamata al metodo di Business del Dominio (protegge gli invarianti)
        category.Rename(command.Name, command.Description);

        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new UpdateCategoryResult(true));
    }
}

public class DeleteCategoryConsumer(ProductContext db) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var categoryId = new CategoryId(context.Message.Id);
        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new DeleteCategoryResult(false));
            return;
        }

        // Verifica figlie (tradotto in EXISTS SQL da EF Core)
        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == categoryId, context.CancellationToken);
        if (hasChildren)
        {
            await context.RespondAsync(new DeleteCategoryResult(false));
            return;
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new DeleteCategoryResult(true));
    }
}