using ProductInformationManager.Domain;

namespace ProductInformationManager.Messages;

// === Queries ===

public record GetAttributeTypes;

public record GetAttributeTypesResult(List<AttributeTypeDto> AttributeTypes);

public record GetAttributeTypeById(Guid Id);

public record GetAttributeTypeByIdResult(AttributeTypeDto? AttributeType);

// === Commands ===

public record CreateAttributeType(string Name, string? Description);

public record CreateAttributeTypeResult(bool Success, Guid Id = default, string? ErrorMessage = null);

public record UpdateAttributeType(Guid Id, string Name, string? Description);

public record UpdateAttributeTypeResult(bool Success, string? ErrorMessage = null);

public record DeleteAttributeType(Guid Id);

public record DeleteAttributeTypeResult(bool Success, string? ErrorMessage = null);

// === Attributes ===

public record CreateAttribute(string Name, string Value, Guid AttributeTypeId);

public record CreateAttributeResult(bool Success, Guid Id = default, string? ErrorMessage = null);

public record DeleteAttribute(Guid Id);

public record DeleteAttributeResult(bool Success, string? ErrorMessage = null);

// === DTOs ===

public record AttributeTypeDto(Guid Id, string Name, string? Description, List<AttributeDto> Attributes)
{
    public static AttributeTypeDto FromEntity(AttributeType entity) => new(entity.Id.Value, entity.Name, entity.Description, []);
}

public record AttributeDto(Guid Id, string Name, string Value)
{
    public static AttributeDto FromEntity(Domain.Attribute entity) => new(entity.Id.Value, entity.Name, entity.Value);
}
