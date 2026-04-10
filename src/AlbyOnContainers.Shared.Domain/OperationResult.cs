namespace AlbyOnContainers.Shared.Domain;

/// <summary>
/// Represents the result of an operation with success values and warning messages.
/// Adapted from the Databook Result pattern for the PIM domain.
/// </summary>
public interface IOperationResult<out TValue>
{
    IReadOnlyCollection<TValue> Success { get; }
    IReadOnlyCollection<OperationWarning> Warnings { get; }

    bool HasErrors => Warnings.Count != 0;
    static IOperationResult<TValue> Empty => new OperationResult<TValue>([], []);
}

public sealed class OperationResult<TValue>(IEnumerable<TValue>? success, IEnumerable<OperationWarning>? warnings)
    : IOperationResult<TValue>
{
    public IReadOnlyCollection<TValue> Success { get; } = success?.ToArray() ?? [];
    public IReadOnlyCollection<OperationWarning> Warnings { get; } = warnings?.ToArray() ?? [];
}

public static class OperationResult
{
    #region Creational

    public static IOperationResult<TValue> Ok<TValue>(TValue value) =>
        new OperationResult<TValue>([value], []);

    public static IOperationResult<TValue> OkMany<TValue>(IEnumerable<TValue> values) =>
        new OperationResult<TValue>(values, []);

    public static IOperationResult<TValue> Fail<TValue>(OperationWarning warning) =>
        new OperationResult<TValue>([], [warning]);

    public static IOperationResult<TValue> Fail<TValue>(string message) =>
        new OperationResult<TValue>([], [new OperationWarning.Validation(message)]);

    public static IOperationResult<TValue> FailMany<TValue>(IEnumerable<OperationWarning> warnings) =>
        new OperationResult<TValue>([], warnings);

    #endregion

    #region Mutations

    public static IOperationResult<TValue> AddValue<TValue>(this IOperationResult<TValue> result, TValue value) =>
        new OperationResult<TValue>(result.Success.Concat([value]), result.Warnings);

    public static IOperationResult<TValue> AddWarning<TValue>(this IOperationResult<TValue> result, OperationWarning warning) =>
        new OperationResult<TValue>(result.Success, result.Warnings.Concat([warning]));

    public static IOperationResult<TValue> AddWarnings<TValue>(this IOperationResult<TValue> result, IEnumerable<OperationWarning> warnings) =>
        new OperationResult<TValue>(result.Success, result.Warnings.Concat(warnings));

    public static IOperationResult<TValue> Merge<TValue>(this IOperationResult<TValue> left, IOperationResult<TValue> right) =>
        new OperationResult<TValue>(left.Success.Concat(right.Success), left.Warnings.Concat(right.Warnings));

    #endregion
}

/// <summary>
/// Unit type for operations that don't return a value.
/// </summary>
public record Unit
{
    public static Unit Value => new();
}

/// <summary>
/// Represents a warning or error from an operation.
/// </summary>
public abstract record OperationWarning(string Message)
{
    /// <summary>Validation error (e.g. FluentValidation failure, business rule violation).</summary>
    public sealed record Validation(string Message) : OperationWarning(Message);

    /// <summary>Entity not found.</summary>
    public sealed record NotFound(string Message) : OperationWarning(Message);

    /// <summary>Referential integrity constraint prevents the operation.</summary>
    public sealed record Conflict(string Message) : OperationWarning(Message);

    /// <summary>Unexpected exception during the operation.</summary>
    public sealed record Exceptional(string Message, Exception Exception) : OperationWarning(Message);
}
