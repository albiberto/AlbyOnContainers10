namespace ECommerce.Messages.Products;

public record CreateProductCommand(
    string Name,
    string Sku,
    string? Description,
    Guid CategoryId);

public record CreateProductResult(Guid ProductId);
