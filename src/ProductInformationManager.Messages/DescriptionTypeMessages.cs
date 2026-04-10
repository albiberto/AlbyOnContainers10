namespace ProductInformationManager.Messages;

// === Queries ===
public record GetDescriptionTypes;
public record GetDescriptionTypesResult(List<DescriptionTypeDto> DescriptionTypes);

public record GetDescriptionTypeById(Guid Id);
public record GetDescriptionTypeByIdResult(DescriptionTypeDto? DescriptionType);

// === Commands ===
public record CreateDescriptionType(
    string Name,
    string? Description,
    bool IsGlobal,
    List<Guid> CategoryIds);

public record CreateDescriptionTypeResult(bool Success, Guid Id = default, string? ErrorMessage = null);

public record UpdateDescriptionType(
    Guid Id,
    string Name,
    string? Description,
    bool IsGlobal,
    List<Guid> CategoryIds);

public record UpdateDescriptionTypeResult(bool Success, string? ErrorMessage = null);

public record DeleteDescriptionType(Guid Id);
public record DeleteDescriptionTypeResult(bool Success, string? ErrorMessage = null);

// === Description Values ===
public record AddDescriptionValue(Guid DescriptionTypeId, string Value);
public record AddDescriptionValueResult(bool Success, Guid Id = default, string? ErrorMessage = null);

public record DeleteDescriptionValue(Guid Id);
public record DeleteDescriptionValueResult(bool Success, string? ErrorMessage = null);

// === DTOs ===
public record DescriptionTypeDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsGlobal,
    List<DescriptionValueDto> Values,
    List<Guid> CategoryIds);

public record DescriptionValueDto(Guid Id, string Value);
