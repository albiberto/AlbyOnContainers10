using ProductDataManager.Models.Abstract;
using ProductDataManager.Models.ValueObjects;

namespace ProductDataManager.Models;

public class CategoryDescriptionRule : AuditableEntity
{
    private CategoryDescriptionRule() { } // Requisito di EF Core

    // Internal: forziamo l'uso di Category.AddDescriptionRule()
    internal CategoryDescriptionRule(DescriptionTypeId descriptionTypeId, CategoryId categoryId, bool isMandatory)
    {
        DescriptionTypeId = descriptionTypeId;
        CategoryId = categoryId;
        IsMandatory = isMandatory;
    }

    // Usiamo init per le chiavi esterne in modo che non cambino mai dopo la creazione
    public DescriptionTypeId DescriptionTypeId { get; init; } = null!;
    public CategoryId CategoryId { get; init; } = null!;
    
    // Lo stato "Mandatory" invece può evolvere nel tempo (una regola che prima era opzionale diventa obbligatoria)
    public bool IsMandatory { get; private set; }

    public DescriptionType DescriptionType { get; private set; } = null!;
    public Category Category { get; private set; } = null!;

    // Comportamento esplicito
    internal void ChangeMandatoryStatus(bool isMandatory) 
    {
        IsMandatory = isMandatory;
    }
}