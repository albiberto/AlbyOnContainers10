using AlbyOnContainers.Shared.Domain;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class Category : AuditableEntity
{
    private readonly List<Category> _children = [];
    private readonly List<Product> _products = [];
    private readonly List<DescriptionType> _descriptionTypes = [];

    private Category() { } // EF Core requirement

    public Category(string name, string path, string description = "", CategoryId? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Category name is required.");
        if (string.IsNullOrWhiteSpace(path)) throw new DomainException("Category path is required.");

        Id = CategoryId.New;
        Name = name;
        Path = path;
        Description = description;
        ParentId = parentId;
    }

    public CategoryId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public string Path { get; private set; } = null!;
    public CategoryId? ParentId { get; init; }

    // --- Navigational properties ---
    
    public Category? Parent { get; private set; }
    
    public IReadOnlyCollection<Category> Children => _children;
    public IReadOnlyCollection<Product> Products => _products;
    public IReadOnlyCollection<DescriptionType> DescriptionTypes => _descriptionTypes;

    // --- Behaviors ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new DomainException("Name cannot be empty.");
        
        Name = newName;
        Description = newDescription;
    }
}