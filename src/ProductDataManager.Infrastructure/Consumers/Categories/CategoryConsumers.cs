using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductDataManager.Infrastructure.Data;
using ProductDataManager.Infrastructure.Messages.Categories;

namespace ProductDataManager.Infrastructure.Consumers.Categories;

public partial class GetRootCategoriesConsumer(ProductContext db) : IConsumer<GetRootCategories>
{
    public async Task Consume(ConsumeContext<GetRootCategories> context)
    {
        var categories = await db.Categories
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Name)
            .ToListAsync(context.CancellationToken);

        // Check which categories have children
        var categoryIds = categories.Select(c => c.Id).ToList();
        var childCounts = await db.Categories
            .Where(c => c.ParentId != null && categoryIds.Contains(c.ParentId.Value))
            .GroupBy(c => c.ParentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId!.Value, x => x.Count, context.CancellationToken);

        var dtos = categories
            .Select(c => CategoryDto.FromEntity(c, childCounts.ContainsKey(c.Id)))
            .ToList();

        await context.RespondAsync(new GetRootCategoriesResult(dtos));
    }
}

public class GetChildCategoriesConsumer(ProductContext db) : IConsumer<GetChildCategories>
{
    public async Task Consume(ConsumeContext<GetChildCategories> context)
    {
        var parentId = context.Message.ParentId;

        var children = await db.Categories
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.Name)
            .ToListAsync(context.CancellationToken);

        var childIds = children.Select(c => c.Id).ToList();
        var grandchildCounts = await db.Categories
            .Where(c => c.ParentId != null && childIds.Contains(c.ParentId.Value))
            .GroupBy(c => c.ParentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId!.Value, x => x.Count, context.CancellationToken);

        var dtos = children
            .Select(c => CategoryDto.FromEntity(c, grandchildCounts.ContainsKey(c.Id)))
            .ToList();

        await context.RespondAsync(new GetChildCategoriesResult(dtos));
    }
}

public class GetCategoryByIdConsumer(ProductContext db) : IConsumer<GetCategoryById>
{
    public async Task Consume(ConsumeContext<GetCategoryById> context)
    {
        var category = await db.Categories.FindAsync([context.Message.Id], context.CancellationToken);
        var hasChildren = category is not null && await db.Categories.AnyAsync(c => c.ParentId == category.Id, context.CancellationToken);

        await context.RespondAsync(new GetCategoryByIdResult(
            category is not null ? CategoryDto.FromEntity(category, hasChildren) : null));
    }
}

public partial class CreateCategoryConsumer(ProductContext db) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;

        string path;
        if (command.ParentId is not null)
        {
            var parent = await db.Categories.FindAsync([command.ParentId.Value], context.CancellationToken);
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

        var category = new ProductDataManager.Models.Category(command.Name, path, command.Description, command.ParentId);

        db.Categories.Add(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateCategoryResult(category.Id, category.Path));
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
        var category = await db.Categories.FindAsync([command.Id], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new UpdateCategoryResult(false));
            return;
        }

        category.Update(command.Name, command.Description);

        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateCategoryResult(true));
    }
}

public class DeleteCategoryConsumer(ProductContext db) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var category = await db.Categories.FindAsync([context.Message.Id], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new DeleteCategoryResult(false));
            return;
        }

        // Check if category has children
        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == category.Id, context.CancellationToken);
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
