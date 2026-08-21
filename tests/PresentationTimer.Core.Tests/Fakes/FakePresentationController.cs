using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.Core.Tests.Fakes;

internal sealed class FakePresentationController : IPresentationController
{
    public event Action<PresentationSnapshot>? StateChanged;

    public PresentationSnapshot State { get; private set; } = PresentationSnapshot.Initial;

    public int NextInvocationCount { get; private set; }

    public int PreviousInvocationCount { get; private set; }

    public Task StartMonitoringAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopMonitoringAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<OperationResult> NextAsync(CancellationToken cancellationToken = default)
    {
        this.NextInvocationCount++;
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> PreviousAsync(CancellationToken cancellationToken = default)
    {
        this.PreviousInvocationCount++;
        return Task.FromResult(OperationResult.Success());
    }

    public void Publish(PresentationSnapshot state)
    {
        this.State = state;
        this.StateChanged?.Invoke(state);
    }
}
