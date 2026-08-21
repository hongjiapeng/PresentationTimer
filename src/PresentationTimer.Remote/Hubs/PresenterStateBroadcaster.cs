using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Remote.Dtos;

namespace PresentationTimer.Remote.Hubs;

internal sealed class PresenterStateBroadcaster : BackgroundService
{
    private readonly Channel<PresentationSessionState> _changes = Channel.CreateBounded<PresentationSessionState>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly IHubContext<PresenterHub> _hubContext;
    private readonly IPresentationSessionService _sessionService;

    public PresenterStateBroadcaster(
        IHubContext<PresenterHub> hubContext,
        IPresentationSessionService sessionService)
    {
        this._hubContext = hubContext;
        this._sessionService = sessionService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        this._sessionService.StateChanged += this.OnStateChanged;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        this._sessionService.StateChanged -= this.OnStateChanged;
        this._changes.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (PresentationSessionState state in
            this._changes.Reader.ReadAllAsync(stoppingToken))
        {
            await this._hubContext.Clients.All.SendAsync(
                "stateChanged",
                PresenterStateDto.FromState(state),
                stoppingToken);
        }
    }

    private void OnStateChanged(PresentationSessionState state) => this._changes.Writer.TryWrite(state);
}
