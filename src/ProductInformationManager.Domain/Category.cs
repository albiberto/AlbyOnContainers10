using System.Text.RegularExpressions;
using AlbyOnContainers.Kernel.Domain;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public partial class Category : AuditableEntity
{
    private readonly List<Category> _children = [];
    private readonly List<Product> _products = [];
    private readonly List<DescriptionType> _descriptionTypes = [];

    private Category() { } // EF Core requirement

    internal Category(string name, string path, string description, CategoryId? parentId)
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
    
    // --- Factory Method ---
    
    public static Category Create(string name, string description, CategoryId? parentId, string parentPath)
    {
        var normalizedName = NormalizePath(name);
        var path = string.IsNullOrWhiteSpace(parentPath) ? normalizedName : $"{parentPath}.{normalizedName}";
        
        return new(name, path, description, parentId);
    }

    // --- Proprietà di navigazione omesse per brevità... ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new DomainException("Name cannot be empty.");
        Name = newName;
        Description = newDescription ?? string.Empty;
    }

    private static string NormalizePath(string name) => PathNormalizer().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex PathNormalizer();
}