namespace ProductInformationManager.Messages;

// === Queries ===
public record GetProducts(int Page = 1, int PageSize = 20, Guid? CategoryId = null, bool? IsActive = null);
public record GetProductsResult(List<ProductDto> Products, int TotalCount);

public record GetProductById(Guid Id);
public record GetProductByIdResult(ProductDto? Product);

// === Commands ===
public record CreateProduct(string Name, string Sku, Guid CategoryId);
public record CreateProductResult(bool Success, Guid Id = default, string? ErrorMessage = null);

public record UpdateProductDetails(Guid Id, string Name, string? Description);
public record UpdateProductDetailsResult(bool Success, string? ErrorMessage = null);

public record ChangeProductCategory(Guid Id, Guid CategoryId);
public record ChangeProductCategoryResult(bool Success, string? ErrorMessage = null);

public record ChangeProductStatus(Guid Id, bool IsActive);
public record ChangeProductStatusResult(bool Success, string? ErrorMessage = null);

public record AddProductAttribute(Guid ProductId, Guid AttributeId);
public record RemoveProductAttribute(Guid ProductId, Guid AttributeId);

public record SetProductDescription(Guid ProductId, Guid DescriptionTypeId, Guid DescriptionValueId);
public record SetProductDescriptionResult(bool Success, string? ErrorMessage = null);

public record DeleteProduct(Guid Id);
public record DeleteProductResult(bool Success, string? ErrorMessage = null);

// === DTOs ===
public record ProductDto(
    Guid Id, string Name, string Sku, string? Description, bool IsActive,
    Guid CategoryId, string CategoryName, 
    List<ProductAttributeDto> Attributes,
    List<ProductDescriptionDto> Descriptions);

public record ProductAttributeDto(Guid Id, string Name, string Value, string TypeName);
public record ProductDescriptionDto(Guid TypeId, string TypeName, Guid ValueId, string Value);
