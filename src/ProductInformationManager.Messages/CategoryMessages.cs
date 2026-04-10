namespace ProductInformationManager.Messages;

// === Queries ===
public record GetRootCategories;
public record GetRootCategoriesResult(List<CategoryDto> Categories);

public record GetChildCategories(Guid ParentId);
public record GetChildCategoriesResult(List<CategoryDto> Children);

public record GetCategoryById(Guid Id);
public record GetCategoryByIdResult(CategoryDto? Category);

public record GetAllCategoriesFlat;
public record GetAllCategoriesFlatResult(List<CategoryFlatDto> Categories);

// === Commands ===
public record CreateCategory(string Name, string? Description, Guid? ParentId);
public record CreateCategoryResult(bool Success, Guid Id = default, string Path = "", string? ErrorMessage = null);

public record UpdateCategory(Guid Id, string Name, string? Description);
public record UpdateCategoryResult(bool Success, string? ErrorMessage = null);

public record DeleteCategory(Guid Id);
public record DeleteCategoryResult(bool Success, string? ErrorMessage = null);

// === CategoryDescriptionRule Commands ===
public record AddCategoryDescriptionRule(Guid CategoryId, Guid DescriptionTypeId, bool IsMandatory);
public record AddCategoryDescriptionRuleResult(bool Success, string? ErrorMessage = null);

public record RemoveCategoryDescriptionRule(Guid CategoryId, Guid DescriptionTypeId);
public record RemoveCategoryDescriptionRuleResult(bool Success, string? ErrorMessage = null);

public record UpdateCategoryDescriptionRuleMandatory(Guid CategoryId, Guid DescriptionTypeId, bool IsMandatory);
public record UpdateCategoryDescriptionRuleMandatoryResult(bool Success, string? ErrorMessage = null);

// === Query: Effective descriptions for a category (own + inherited) ===
public record GetCategoryDescriptions(Guid CategoryId);
public record GetCategoryDescriptionsResult(List<CategoryDescriptionRuleDto> Descriptions);

// === DTOs ===
public record CategoryDto(Guid Id, string Name, string? Description, string Path, Guid? ParentId, bool HasChildren, int DescriptionRulesCount = 0);

public record CategoryFlatDto(Guid Id, string Name, string Path, int Depth);

public record CategoryDescriptionRuleDto(
    Guid DescriptionTypeId,
    string DescriptionTypeName,
    bool IsMandatory,
    bool IsInherited,
    Guid SourceCategoryId,
    string SourceCategoryName,
    List<DescriptionValueDto> Values);
