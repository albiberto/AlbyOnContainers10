namespace ECommerce.Messages.Products;

public record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    bool? IsActive = null);

public record GetProductsResult(
    IReadOnlyCollection<ProductDto> Products,
    int TotalCount);

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Description,
    bool IsActive,
    Guid CategoryId,
    string CategoryName,
    IReadOnlyCollection<ProductAttributeDto> Attributes);

public record ProductAttributeDto(
    Guid Id,
    string Name,
    string Value,
    string AttributeTypeName);
