using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class DescriptionType : AuditableEntity
{
    private readonly List<DescriptionValue> _values = [];

    private DescriptionType() { } // EF Core requirement

    // If categoryId is null, the description is implicitly Global
    public DescriptionType(string name, string? description = null, CategoryId? categoryId = null, bool isMandatory = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Description type name is required.");

        Id = DescriptionTypeId.New;
        Name = name;
        Description = description;
        
        CategoryId = categoryId;
        IsMandatory = isMandatory;
    }

    public DescriptionTypeId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    
    public bool IsGlobal => CategoryId is null;
    
    public bool IsMandatory { get; private set; }

    // 1:N relationship with Category
    public CategoryId? CategoryId { get; private set; }
    public Category? Category { get; private set; }

    // Protected navigational properties
    public IReadOnlyCollection<DescriptionValue> Values => _values;

    // --- Behaviors ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) 
            throw new DomainException("Name cannot be empty.");
        
        Name = newName;
        Description = newDescription;
    }

    // Single method to move the description in the tree or change its mandatory status
    public void ChangeSettings(CategoryId? newCategoryId, bool isMandatory)
    {
        CategoryId = newCategoryId;
        IsMandatory = isMandatory;
    }

    public void AddValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) 
            throw new DomainException("Value cannot be empty.");
        
        // Prevent case-insensitive duplicates (e.g., "Red" and "red")
        if (_values.Any(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase))) 
            return;

        _values.Add(new DescriptionValue(value, this.Id));
    }

    public void RemoveValue(DescriptionValueId valueId)
    {
        var valueToRemove = _values.FirstOrDefault(v => v.Id == valueId);
        if (valueToRemove != null)
        {
            _values.Remove(valueToRemove);
        }
    }
}