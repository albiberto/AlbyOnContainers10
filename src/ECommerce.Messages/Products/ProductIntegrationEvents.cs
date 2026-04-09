namespace ECommerce.Messages.Products;

public record ProductCreatedIntegrationEvent(
    Guid ProductId,
    string Name,
    string Sku,
    Guid CategoryId);
