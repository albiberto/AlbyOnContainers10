namespace ProductInformationManager.Messages.AttributeTypes;

// === Queries ===

public record GetAttributeTypes;

public record GetAttributeTypesResult(List<AttributeTypeDto> AttributeTypes);

public record GetAttributeTypeById(Guid Id);

public record GetAttributeTypeByIdResult(AttributeTypeDto? AttributeType);

// === Commands ===

public record CreateAttributeType(string Name, string? Description);

public record CreateAttributeTypeResult(Guid Id);

public record UpdateAttributeType(Guid Id, string Name, string? Description);

public record UpdateAttributeTypeResult(bool Success);

public record DeleteAttributeType(Guid Id);

public record DeleteAttributeTypeResult(bool Success);

// === Attributes ===

public record CreateAttribute(string Name, string Value, Guid AttributeTypeId);

public record CreateAttributeResult(Guid Id);

public record DeleteAttribute(Guid Id);

public record DeleteAttributeResult(bool Success);

// === DTOs ===

public record AttributeTypeDto(Guid Id, string Name, string? Description, List<AttributeDto> Attributes)
{
    public static AttributeTypeDto FromEntity(AttributeType entity) =>
        new(entity.Id, entity.Name, entity.Description, []);
}

public record AttributeDto(Guid Id, string Name, string Value)
{
    public static AttributeDto FromEntity(Attribute entity) =>
        new(entity.Id, entity.Name, entity.Value);
}
