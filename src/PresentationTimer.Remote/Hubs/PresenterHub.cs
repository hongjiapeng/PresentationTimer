using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Results;
using PresentationTimer.Remote.Dtos;

namespace PresentationTimer.Remote.Hubs;

/// <summary>Exposes the authenticated, deliberately narrow phone presenter API.</summary>
[Authorize]
public sealed class PresenterHub : Hub
{
    private readonly RemoteConnectionTracker _connections;
    private readonly IPresentationSessionService _sessionService;

    /// <summary>Initializes a new instance of the <see cref="PresenterHub"/> class.</summary>
    /// <param name="sessionService">The authoritative application service.</param>
    /// <param name="connections">The token-free connection counter.</param>
    public PresenterHub(
        IPresentationSessionService sessionService,
        RemoteConnectionTracker connections)
    {
        this._sessionService = sessionService;
        this._connections = connections;
    }

    /// <summary>Gets the latest full presenter snapshot.</summary>
    /// <returns>The current allow-listed state.</returns>
    public PresenterStateDto GetState() => PresenterStateDto.FromState(this._sessionService.State);

    /// <summary>Navigates exactly once to the next slide.</summary>
    /// <returns>A safe command acknowledgement.</returns>
    public async Task<PresenterCommandResultDto> Next()
    {
        OperationResult result = await this._sessionService.NextSlideAsync(this.Context.ConnectionAborted);
        return PresenterCommandResultDto.FromResult(result);
    }

    /// <summary>Navigates exactly once to the previous slide.</summary>
    /// <returns>A safe command acknowledgement.</returns>
    public async Task<PresenterCommandResultDto> Previous()
    {
        OperationResult result = await this._sessionService.PreviousSlideAsync(this.Context.ConnectionAborted);
        return PresenterCommandResultDto.FromResult(result);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        this._connections.Add();
        await base.OnConnectedAsync();
        await this.Clients.Caller.SendAsync(
            "stateChanged",
            PresenterStateDto.FromState(this._sessionService.State),
            this.Context.ConnectionAborted);
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        this._connections.Remove();
        await base.OnDisconnectedAsync(exception);
    }
}
