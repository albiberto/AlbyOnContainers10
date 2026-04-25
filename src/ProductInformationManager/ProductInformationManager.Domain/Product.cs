using ProductInformationManager.Domain.ValueObjects;


namespace ProductInformationManager.Domain;

public class Product : AuditableEntity
{
    private readonly List<DescriptionValue> _descriptions = [];
    private readonly List<Attribute> _attributes = []; 

    private Product() { } // Requisito di EF Core

    public Product(string name, string sku, CategoryId categoryId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("The product name is mandatory.");
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("The SKU is mandatory.");

        Id = ProductId.New;
        Name = name;
        Sku = sku;
        CategoryId = categoryId;
        IsActive = true;
    }

    public ProductId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public CategoryId CategoryId { get; private set; } = null!;

    public Category Category { get; private set; } = null!;
    
    // Proprietà di navigazione esposte al mondo esterno
    public IReadOnlyCollection<DescriptionValue> Descriptions => _descriptions;
    public IReadOnlyCollection<Attribute> Attributes => _attributes;

    // --- Comportamenti di Base ---
    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Il nome non può essere vuoto.");
        Name = name;
        Description = description;
    }

    public void ChangeCategory(CategoryId newCategoryId) => CategoryId = newCategoryId;
    
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    // --- Gestione DDL (Descrizioni Vincolate dalla Categoria) ---
    public void SetDescription(DescriptionType type, DescriptionValue value)
    {
        // 1. Verifica che il valore appartenga effettivamente al tipo passato
        if (value.DescriptionTypeId != type.Id)
            throw new DomainException($"Il valore '{value.Value}' non appartiene al tipo '{type.Name}'.");

        // 2. Regola di Business (Sostituisce il vincolo del DB): 
        // Rimuoviamo qualsiasi valore precedentemente scelto per questo specifico Tipo.
        var existing = _descriptions.FirstOrDefault(x => x.DescriptionTypeId == type.Id);
        if (existing != null) 
        {
            _descriptions.Remove(existing);
        }

        // 3. Aggiungiamo il nuovo valore
        _descriptions.Add(value);
    }

    // --- Gestione Attributi Globali ---
    public void AddAttribute(Attribute attribute)
    {
        if (_attributes.Any(a => a.Id == attribute.Id)) return; // Idempotenza
        
        _attributes.Add(attribute);
    }

    public void RemoveAttribute(AttributeId attributeId)
    {
        var existing = _attributes.FirstOrDefault(a => a.Id == attributeId);
        if (existing != null) _attributes.Remove(existing);
    }
}