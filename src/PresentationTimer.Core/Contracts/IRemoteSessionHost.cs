using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Contracts;

/// <summary>
/// Controls the in-process, ephemeral local presenter remote host.
/// </summary>
public interface IRemoteSessionHost
{
    /// <summary>Occurs when desktop-only pairing material changes or must be withdrawn.</summary>
    event Action<DesktopPairingDescriptor?>? PairingChanged;

    /// <summary>Occurs when token-free remote public state changes.</summary>
    event Action<RemoteSessionPublicState>? StateChanged;

    /// <summary>Gets the current token-free remote public state.</summary>
    RemoteSessionPublicState State { get; }

    /// <summary>Creates an ephemeral session and returns desktop-only pairing material.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>The pairing descriptor or a structured failure.</returns>
    Task<OperationResult<DesktopPairingDescriptor>> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Revokes credentials and stops the remote session.</summary>
    /// <param name="cancellationToken">Cancels the bounded stop wait.</param>
    /// <returns>A task representing the operation.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
