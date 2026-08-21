using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Tests.Fakes;

internal sealed class FakeRemoteSessionHost : IRemoteSessionHost
{
    public event Action<DesktopPairingDescriptor?>? PairingChanged;

    public event Action<RemoteSessionPublicState>? StateChanged;

    public RemoteSessionPublicState State { get; private set; } = RemoteSessionPublicState.Initial;

    public int StartInvocationCount { get; private set; }

    public Task<OperationResult<DesktopPairingDescriptor>> StartAsync(
        CancellationToken cancellationToken = default)
    {
        this.StartInvocationCount++;
        this.Publish(this.State with { Status = RemoteSessionStatus.Ready });
        var descriptor = new DesktopPairingDescriptor(
            new Uri("http://192.168.1.2:5000/pair?t=test"),
            "http://192.168.1.2:5000/pair?t=test");
        this.PairingChanged?.Invoke(descriptor);
        return Task.FromResult(OperationResult.Success(descriptor));
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        this.PairingChanged?.Invoke(null);
        this.Publish(RemoteSessionPublicState.Initial);
        return Task.CompletedTask;
    }

    public void Publish(RemoteSessionPublicState state)
    {
        this.State = state;
        this.StateChanged?.Invoke(state);
    }
}
