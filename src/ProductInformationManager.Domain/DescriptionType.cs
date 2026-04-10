using AlbyOnContainers.Shared.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class DescriptionType : AuditableEntity
{
    private readonly List<DescriptionValue> _values = [];
    private readonly List<CategoryDescriptionRule> _categoryRules = [];

    private DescriptionType() { }

    public DescriptionType(string name, string? description = null, bool isGlobal = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Il nome del tipo di descrizione è obbligatorio.");

        Id = DescriptionTypeId.New;
        Name = name;
        Description = description;
        IsGlobal = isGlobal;
    }

    public DescriptionTypeId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsGlobal { get; private set; }

    public IReadOnlyCollection<DescriptionValue> Values => _values;
    public IReadOnlyCollection<CategoryDescriptionRule> CategoryRules => _categoryRules;

    /// <summary>
    /// Updates all mutable metadata. When IsGlobal transitions to true the
    /// application layer is responsible for removing any existing
    /// CategoryDescriptionRules (the domain signals the intent via the returned flag).
    /// </summary>
    public bool UpdateMetadata(string name, string? description, bool isGlobal)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Il nome non può essere vuoto.");

        var wasSpecialized = !IsGlobal;
        Name = name;
        Description = description;
        IsGlobal = isGlobal;

        // Return true when the type just became Global so the consumer can
        // purge the category rules (invariant).
        return isGlobal && wasSpecialized;
    }

    public void AddValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Il valore non può essere vuoto.");

        if (_values.Any(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
            return;

        _values.Add(new DescriptionValue(value, Id));
    }

    public void RemoveValue(DescriptionValueId valueId)
    {
        var toRemove = _values.FirstOrDefault(v => v.Id == valueId);
        if (toRemove is not null)
            _values.Remove(toRemove);
    }
}
