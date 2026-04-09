using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class GetCategoryByIdConsumer(ProductContext db) : IConsumer<GetCategoryById>
{
    public async Task Consume(ConsumeContext<GetCategoryById> context)
    {
        var categoryId = new CategoryId(context.Message.Id);
        
        var dto = await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == categoryId)
            .Select(c => new CategoryDto(
                c.Id.Value,
                c.Name,
                c.Description,
                c.Path,
                c.ParentId != null ? c.ParentId.Value : null,
                c.Children.Any() // Magia di EF Core: diventa un "EXISTS (SELECT 1 FROM Categories WHERE ParentId = Id)"
            ))
            .FirstOrDefaultAsync(context.CancellationToken);

        await context.RespondAsync(new GetCategoryByIdResult(dto));
    }
}

public class GetChildCategoriesConsumer(ProductContext db) : IConsumer<GetChildCategories>
{
    public async Task Consume(ConsumeContext<GetChildCategories> context)
    {
        var parentId = new CategoryId(context.Message.ParentId);

        var dtos = await db.Categories
            .AsNoTracking()
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id.Value,
                c.Name,
                c.Description,
                c.Path,
                c.ParentId != null ? c.ParentId.Value : null,
                c.Children.Any()
            ))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetChildCategoriesResult(dtos));
    }
}

public class GetRootCategoriesConsumer(ProductContext db) : IConsumer<GetRootCategories>
{
    public async Task Consume(ConsumeContext<GetRootCategories> context)
    {
        var dtos = await db.Categories
            .AsNoTracking()
            .Where(c => c.ParentId == null) // Le root non hanno padre
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id.Value,
                c.Name,
                c.Description,
                c.Path,
                null,
                c.Children.Any()
            ))
            .ToListAsync(context.CancellationToken);

        await context.RespondAsync(new GetRootCategoriesResult(dtos));
    }
}