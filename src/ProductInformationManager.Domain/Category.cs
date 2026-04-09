using AlbyOnContainers.Shared.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class Category : AuditableEntity
{
    private readonly List<Category> _children = [];
    private readonly List<Product> _products = [];
    private readonly List<CategoryDescriptionRule> _descriptionRules = [];

    private Category() { } // Requisito di EF Core

    public Category(string name, string path, string? description = null, CategoryId? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Il nome della categoria è obbligatorio.");
        if (string.IsNullOrWhiteSpace(path)) throw new DomainException("Il path della categoria è obbligatorio.");

        Id = CategoryId.New;
        Name = name;
        Path = path;
        Description = description;
        ParentId = parentId;
    }

    public CategoryId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Path { get; private set; } = null!;
    public CategoryId? ParentId { get; init; }

    // Navigational properties
    public Category? Parent { get; private set; }
    public IReadOnlyCollection<Category> Children => _children;
    public IReadOnlyCollection<Product> Products => _products;
    public IReadOnlyCollection<CategoryDescriptionRule> DescriptionRules => _descriptionRules;

    // --- Comportamenti ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) throw new DomainException("Il nome non può essere vuoto.");
        Name = newName;
        Description = newDescription;
    }

    public void AddDescriptionRule(DescriptionType descriptionType, bool isMandatory)
    {
        // Se la regola esiste già per questo tipo di descrizione, ignoriamo (idempotenza)
        if (_descriptionRules.Any(r => r.DescriptionTypeId == descriptionType.Id)) 
            return;

        _descriptionRules.Add(new CategoryDescriptionRule(descriptionType.Id, this.Id, isMandatory));
    }
}