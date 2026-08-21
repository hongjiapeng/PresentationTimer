namespace PresentationTimer.Core.Results;

/// <summary>
/// Represents the safe result of a command without leaking infrastructure exceptions.
/// </summary>
public record OperationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationResult"/> class.
    /// </summary>
    /// <param name="isSuccess">Whether the command succeeded.</param>
    /// <param name="errorCode">A stable error code when the command failed.</param>
    /// <param name="message">A safe display message.</param>
    protected OperationResult(bool isSuccess, string? errorCode, string? message)
    {
        this.IsSuccess = isSuccess;
        this.ErrorCode = errorCode;
        this.Message = message;
    }

    /// <summary>Gets a value indicating whether the command succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a stable error code when the command failed.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets a safe display message.</summary>
    public string? Message { get; }

    /// <summary>Creates a successful command result.</summary>
    /// <returns>A successful result.</returns>
    public static OperationResult Success() => new OperationResult(true, null, null);

    /// <summary>Creates a successful command result containing a value.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static OperationResult<T> Success<T>(T value) => new OperationResult<T>(true, value, null, null);

    /// <summary>Creates a failed command result.</summary>
    /// <param name="errorCode">The stable failure code.</param>
    /// <param name="message">The safe display message.</param>
    /// <returns>A failed result.</returns>
    public static OperationResult Failure(string errorCode, string message) =>
        new OperationResult(false, errorCode, message);

    /// <summary>Creates a failed command result without a value.</summary>
    /// <typeparam name="T">The successful value type.</typeparam>
    /// <param name="errorCode">The stable failure code.</param>
    /// <param name="message">The safe display message.</param>
    /// <returns>A failed result without a value.</returns>
    public static OperationResult<T> Failure<T>(string errorCode, string message) =>
        new OperationResult<T>(false, default, errorCode, message);
}
