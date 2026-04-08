using ProductDataManager.Models;

namespace ProductDataManager.Infrastructure.Messages.DescriptionTypes;

// === Queries ===

public record GetDescriptionTypes;

public record GetDescriptionTypesResult(List<DescriptionTypeDto> DescriptionTypes);

public record GetDescriptionTypeById(Guid Id);

public record GetDescriptionTypeByIdResult(DescriptionTypeDto? DescriptionType);

// === Commands ===

public record CreateDescriptionType(string Name, string? Description);

public record CreateDescriptionTypeResult(Guid Id);

public record UpdateDescriptionType(Guid Id, string Name, string? Description);

public record UpdateDescriptionTypeResult(bool Success);

public record DeleteDescriptionType(Guid Id);

public record DeleteDescriptionTypeResult(bool Success);

// === Description Values ===

public record AddDescriptionValue(Guid DescriptionTypeId, string Value);

public record AddDescriptionValueResult(Guid Id);

public record DeleteDescriptionValue(Guid Id);

public record DeleteDescriptionValueResult(bool Success);

// === DTOs ===

public record DescriptionTypeDto(Guid Id, string Name, string? Description, List<DescriptionValueDto> Values)
{
    public static DescriptionTypeDto FromEntity(DescriptionType entity) =>
        new(entity.Id, entity.Name, entity.Description,
            entity.Values?.Select(v => new DescriptionValueDto(v.Id, v.Value)).ToList() ?? []);
}

public record DescriptionValueDto(Guid Id, string Value);
