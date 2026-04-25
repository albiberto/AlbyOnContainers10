using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class Attribute : AuditableEntity
{
    private Attribute() { } // Requisito EF Core
    
    internal Attribute(AttributeTypeId attributeTypeId, string name, string value)
    {
        Id = AttributeId.New;
        AttributeTypeId = attributeTypeId;
        Name = name;
        Value = value;
    }

    public AttributeId Id { get; private set; } = null!;
    public AttributeTypeId AttributeTypeId { get; init; } = null!;
    public string Name { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    public AttributeType AttributeType { get; private set; } = null!;

    internal void UpdateDetails(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) throw new DomainException("Nome e valore non possono essere vuoti.");
            
        Name = name;
        Value = value;
    }
}