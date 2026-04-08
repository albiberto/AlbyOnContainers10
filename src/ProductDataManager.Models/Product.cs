namespace ProductDataManager.Models;

public class Product(string name, string sku, Guid categoryId, decimal price = 0, string? description = null) : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public string Sku { get; set; } = sku;
    public string? Description { get; set; } = description;
    public decimal Price { get; set; } = price;
    public bool IsActive { get; set; } = true;
    public Guid CategoryId { get; set; } = categoryId;

    public Category Category { get; set; } = null!;
    public ICollection<ProductAttribute> ProductAttributes { get; set; } = [];
}
