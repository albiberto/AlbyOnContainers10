using AlbyOnContainers.Shared.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;

namespace ProductInformationManager.Domain;

public class DescriptionValue : AuditableEntity
{
    private DescriptionValue() { } // Requisito di EF Core

    // Internal: forziamo l'uso di DescriptionType.AddValue()
    internal DescriptionValue(string value, DescriptionTypeId descriptionTypeId)
    {
        if (string.IsNullOrWhiteSpace(value)) 
            throw new DomainException("Il valore non può essere vuoto.");

        Id = DescriptionValueId.New;
        Value = value;
        DescriptionTypeId = descriptionTypeId;
    }

    public DescriptionValueId Id { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public DescriptionTypeId DescriptionTypeId { get; init; } = null!;

    public DescriptionType DescriptionType { get; private set; } = null!;

    internal void UpdateValue(string newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue)) 
            throw new DomainException("Il valore non può essere vuoto.");
        
        Value = newValue;
    }
}