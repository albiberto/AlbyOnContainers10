using ProductDataManager.Models.Abstract;
using ProductDataManager.Models.Exceptions;
using ProductDataManager.Models.ValueObjects;

namespace ProductDataManager.Models;

public class AttributeType : AuditableEntity
{
    private readonly List<Attribute> _attributes = [];

    private AttributeType() { } // Requisito EF Core

    public AttributeType(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) 
            throw new DomainException("Il nome della famiglia di attributi è obbligatorio.");

        Id = AttributeTypeId.New;
        Name = name;
        Description = description;
    }

    public AttributeTypeId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    public IReadOnlyCollection<Attribute> Attributes => _attributes;

    // --- Comportamenti ---

    public void Rename(string newName, string? newDescription)
    {
        if (string.IsNullOrWhiteSpace(newName)) 
            throw new DomainException("Il nome non può essere vuoto.");
        
        Name = newName;
        Description = newDescription;
    }

    // L'aggregato root è responsabile di creare i suoi figli
    public void AddAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) 
            throw new DomainException("Nome e valore dell'attributo sono obbligatori.");

        // Preveniamo duplicati esatti all'interno dello stesso tipo
        if (_attributes.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && 
                                 a.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
            return;

        _attributes.Add(new Attribute(this.Id, name, value));
    }

    public void RemoveAttribute(AttributeId attributeId)
    {
        var attribute = _attributes.FirstOrDefault(a => a.Id == attributeId);
        if (attribute != null)
        {
            _attributes.Remove(attribute);
        }
    }
}