namespace ProductInformationManager.Messages.Products;

// === Queries ===
public record GetProducts(int Page = 1, int PageSize = 20, Guid? CategoryId = null, bool? IsActive = null);
public record GetProductsResult(List<ProductDto> Products, int TotalCount);

public record GetProductById(Guid Id);
public record GetProductByIdResult(ProductDto? Product);

// === Commands ===
public record CreateProduct(string Name, string Sku, Guid CategoryId);
public record CreateProductResult(Guid Id);

// I comandi ora riflettono i "Task" di business, non i campi del database
public record UpdateProductDetails(Guid Id, string Name, string? Description);
public record ChangeProductCategory(Guid Id, Guid CategoryId);
public record ChangeProductStatus(Guid Id, bool IsActive);

public record AddProductAttribute(Guid ProductId, Guid AttributeId);
public record RemoveProductAttribute(Guid ProductId, Guid AttributeId);

public record SetProductDescription(Guid ProductId, Guid DescriptionTypeId, Guid DescriptionValueId);

public record DeleteProduct(Guid Id);
public record DeleteProductResult(bool Success);

// === DTOs ===
public record ProductDto(
    Guid Id, string Name, string Sku, string? Description, bool IsActive,
    Guid CategoryId, string CategoryName, 
    List<ProductAttributeDto> Attributes,
    List<ProductDescriptionDto> Descriptions); // Nuova lista per le descrizioni

public record ProductAttributeDto(Guid Id, string Name, string Value, string TypeName);
public record ProductDescriptionDto(Guid TypeId, string TypeName, Guid ValueId, string Value);