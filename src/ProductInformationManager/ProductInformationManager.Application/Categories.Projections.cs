namespace ProductInformationManager.Application;

using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

internal static class CategoryProjections
{
    public static Task<List<CategoryDto>> GetAllCategoriesAsync(this ProductContext db, CancellationToken ct) =>
        db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Path)
            .Select(c => new CategoryDto(
                c.Id.Value,
                c.Name,
                c.Description,
                c.Path,
                c.ParentId != null ? c.ParentId.Value : null,
                c.Children.Any()))
            .ToListAsync(ct);
}
