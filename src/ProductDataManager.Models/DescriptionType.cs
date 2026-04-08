using ProductDataManager.Models.Abstract;
using ProductDataManager.Models.Exceptions;
using ProductDataManager.Models.ValueObjects;

namespace ProductDataManager.Models;

public class DescriptionType : AuditableEntity
{
    private readonly List<DescriptionValue> _values = [];
    private readonly List<CategoryDescriptionRule> _categoryRules = [];

    private DescriptionType() { } // Requisito di EF Core

    public DescriptionType(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) 
            throw new DomainException("Il nome del tipo di descrizione è obbligatorio.");

        Id = DescriptionTypeId.New;
        Name = name;
        Description = description;
    }

    public DescriptionTypeId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    // Navigational properties protette
    public IReadOnlyCollection<DescriptionValue> Values => _values;
    public IReadOnlyCollection<CategoryDescriptionRule> CategoryRules => _categoryRules;

    // --- Comportamenti ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) 
            throw new DomainException("Il nome non può essere vuoto.");
        
        Name = newName;
        Description = newDescription;
    }

    public void AddValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) 
            throw new DomainException("Il valore non può essere vuoto.");
        
        // Evitiamo duplicati case-insensitive (es. "Rosso" e "rosso")
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