using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Contracts;

/// <summary>
/// Exposes managed presentation operations without leaking Office types.
/// </summary>
public interface IPresentationController
{
    /// <summary>Occurs after the authoritative presentation snapshot changes.</summary>
    event Action<PresentationSnapshot>? StateChanged;

    /// <summary>Gets the last authoritative presentation snapshot.</summary>
    PresentationSnapshot State { get; }

    /// <summary>Starts presentation attachment and reconciliation.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>A task representing the operation.</returns>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops monitoring and releases owned infrastructure resources.</summary>
    /// <param name="cancellationToken">Cancels the bounded stop wait.</param>
    /// <returns>A task representing the operation.</returns>
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates exactly once to the next slide.</summary>
    /// <param name="cancellationToken">Cancels the invocation before it is submitted.</param>
    /// <returns>The structured invocation result.</returns>
    Task<OperationResult> NextAsync(CancellationToken cancellationToken = default);

    /// <summary>Navigates exactly once to the previous slide.</summary>
    /// <param name="cancellationToken">Cancels the invocation before it is submitted.</param>
    /// <returns>The structured invocation result.</returns>
    Task<OperationResult> PreviousAsync(CancellationToken cancellationToken = default);
}
