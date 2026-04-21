namespace ProductInformationManager.Messages;

// --- Shared Interface for Formal Validation ---
public interface ICategorySaveCommand
{
    string Name { get; }
    string? Description { get; }
}

// === Queries ===
public record GetRootCategories;
public record GetChildCategories(Guid ParentId);
public record GetCategoryById(Guid Id);
public record SearchCategories(string? SearchPattern = null);

// === Queries Results ===
public record GetCategoriesResult(IReadOnlyList<CategoryDto> Categories);
public record SearchCategoriesResult(IReadOnlyList<CategoryFlatDto> Categories);
public record GetCategoryResult(CategoryDto Category);

// === Commands ===
public record CreateCategory(string Name, string? Description, Guid? ParentId) : ICategorySaveCommand;
public record UpdateCategory(Guid Id, string Name, string? Description) : ICategorySaveCommand;
public record DeleteCategory(Guid Id);

// === Commands Results ===
public record CreateCategoryCommandResult(Guid Id);
public record UpdateCategoryCommandResult(Guid Id);
public record DeleteCategoryCommandResult(Guid Id);

// === Queries ===
public record CategoryDto(Guid Id, string Name, string? Description, string Path, Guid? ParentId, bool HasChildren);
public record CategoryFlatDto(Guid Id, string Name, string Path, int Depth);
