using ProductDataManager.Models;

namespace ProductDataManager.Infrastructure.Messages.Categories;

// === Queries ===

public record GetRootCategories;

public record GetRootCategoriesResult(List<CategoryDto> Categories);

public record GetChildCategories(Guid ParentId);

public record GetChildCategoriesResult(List<CategoryDto> Children);

public record GetCategoryById(Guid Id);

public record GetCategoryByIdResult(CategoryDto? Category);

// === Commands ===

public record CreateCategory(string Name, string? Description, Guid? ParentId);

public record CreateCategoryResult(Guid Id, string Path);

public record UpdateCategory(Guid Id, string Name, string? Description);

public record UpdateCategoryResult(bool Success);

public record DeleteCategory(Guid Id);

public record DeleteCategoryResult(bool Success);

// === DTOs ===

public record CategoryDto(Guid Id, string Name, string? Description, string Path, Guid? ParentId, bool HasChildren)
{
    public static CategoryDto FromEntity(Category category, bool hasChildren = false) =>
        new(category.Id, category.Name, category.Description, category.Path, category.ParentId, hasChildren);
}
