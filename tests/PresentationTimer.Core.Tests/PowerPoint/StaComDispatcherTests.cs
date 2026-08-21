using PresentationTimer.PowerPoint.Threading;

namespace PresentationTimer.Core.Tests.PowerPoint;

/// <summary>
/// Verifies the Office-independent behavior of the pumped STA dispatcher.
/// </summary>
[TestClass]
public sealed class StaComDispatcherTests
{
    /// <summary>Verifies callbacks share one STA thread.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task InvokeAsync_ExecutesEveryCallbackOnOneStaThread()
    {
        // Arrange
        await using var dispatcher = new StaComDispatcher();

        // Act
        Task<(int ThreadId, ApartmentState Apartment)>[] calls = Enumerable.Range(0, 24)
            .Select(_ => dispatcher.InvokeAsync(
                () => (Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState())))
            .ToArray();
        (int ThreadId, ApartmentState Apartment)[] results = await Task.WhenAll(calls);

        // Assert
        Assert.HasCount(1, results.Select(static result => result.ThreadId).Distinct());
        Assert.IsTrue(results.All(static result => result.Apartment == ApartmentState.STA));
    }

    /// <summary>Verifies callback failures do not terminate the dispatcher.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task InvokeAsync_WhenCallbackFails_PropagatesAndContinuesProcessing()
    {
        // Arrange
        await using var dispatcher = new StaComDispatcher();

        // Act
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.InvokeAsync<int>(() => throw new InvalidOperationException("sentinel")));
        int laterResult = await dispatcher.InvokeAsync(() => 42);

        // Assert
        Assert.AreEqual("sentinel", exception.Message);
        Assert.AreEqual(42, laterResult);
    }

    /// <summary>Verifies pre-canceled work never executes.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task InvokeAsync_WithCanceledToken_DoesNotRunCallback()
    {
        // Arrange
        await using var dispatcher = new StaComDispatcher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool executed = false;

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(
                () =>
                {
                    executed = true;
                    return 1;
                },
                cancellation.Token));

        // Assert
        Assert.IsFalse(executed);
    }

    /// <summary>Verifies orderly bounded shutdown and post-stop rejection.</summary>
    /// <returns>A task that completes after verification.</returns>
    [TestMethod]
    public async Task StopAsync_CompletesWithinBoundedTimeout_AndRejectsNewWork()
    {
        // Arrange
        var dispatcher = new StaComDispatcher();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        _ = await dispatcher.InvokeAsync(() => true);

        // Act
        await dispatcher.StopAsync(timeout.Token);

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => dispatcher.InvokeAsync(() => true));
        await dispatcher.DisposeAsync();
    }
}
