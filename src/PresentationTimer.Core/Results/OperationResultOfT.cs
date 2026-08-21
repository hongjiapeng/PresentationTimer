namespace PresentationTimer.Core.Results;

/// <summary>
/// Represents a safe command result that carries a value on success.
/// </summary>
/// <typeparam name="T">The successful value type.</typeparam>
public sealed record OperationResult<T> : OperationResult
{
    internal OperationResult(bool isSuccess, T? value, string? errorCode, string? message)
        : base(isSuccess, errorCode, message)
    {
        this.Value = value;
    }

    /// <summary>Gets the successful value, or the default value after failure.</summary>
    public T? Value { get; }
}
