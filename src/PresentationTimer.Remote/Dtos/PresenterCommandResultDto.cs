using PresentationTimer.Core.Results;

namespace PresentationTimer.Remote.Dtos;

/// <summary>Contains a user-safe presenter command acknowledgement.</summary>
public sealed record PresenterCommandResultDto
{
    /// <summary>Initializes a new instance of the <see cref="PresenterCommandResultDto"/> class.</summary>
    /// <param name="isSuccess">Whether the command succeeded.</param>
    /// <param name="errorCode">The stable error code, when present.</param>
    /// <param name="message">The safe result message, when present.</param>
    public PresenterCommandResultDto(bool isSuccess, string? errorCode, string? message)
    {
        this.IsSuccess = isSuccess;
        this.ErrorCode = errorCode;
        this.Message = message;
    }

    /// <summary>Gets a value indicating whether the command succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the stable error code, when present.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets the user-safe result message, when present.</summary>
    public string? Message { get; }

    /// <summary>Creates the browser response from a Core command result.</summary>
    /// <param name="result">The Core operation result.</param>
    /// <returns>A safe command result.</returns>
    public static PresenterCommandResultDto FromResult(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new PresenterCommandResultDto(result.IsSuccess, result.ErrorCode, result.Message);
    }
}
