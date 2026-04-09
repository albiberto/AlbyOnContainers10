using Attribute = ProductInformationManager.Domain.Attribute;

namespace ProductInformationManager.Messages.Products;

// === Queries ===

public record GetProducts(int Page = 1, int PageSize = 20, Guid? CategoryId = null, bool? IsActive = null);

public record GetProductsResult(List<ProductDto> Products, int TotalCount);

public record GetProductById(Guid Id);

public record GetProductByIdResult(ProductDto? Product);

// === Commands ===

public record CreateProduct(string Name, string Sku, string? Description, decimal Price, Guid CategoryId);

public record CreateProductResult(Guid Id);

public record UpdateProduct(Guid Id, string Name, string Sku, string? Description, decimal Price, Guid CategoryId, bool IsActive);

public record UpdateProductResult(bool Success);

public record DeleteProduct(Guid Id);

public record DeleteProductResult(bool Success);

// === DTOs ===

public record ProductDto(
    Guid Id, string Name, string Sku, string? Description, decimal Price, bool IsActive,
    Guid CategoryId, string CategoryName, List<ProductAttributeDto> Attributes)
{
    public static ProductDto FromEntity(Product product) =>
        new(product.Id, product.Name, product.Sku, product.Description, product.Price, product.IsActive,
            product.CategoryId, product.Category?.Name ?? string.Empty,
            product.ProductAttributes?.Select(pa => ProductAttributeDto.FromEntity(pa.Attribute)).ToList() ?? []);
}

public record ProductAttributeDto(Guid Id, string Name, string Value, string TypeName)
{
    public static ProductAttributeDto FromEntity(Attribute attribute) =>
        new(attribute.Id, attribute.Name, attribute.Value, attribute.AttributeType?.Name ?? string.Empty);
}
