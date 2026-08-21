using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;
using PresentationTimer.Core.Services;
using PresentationTimer.Core.Timing;
using PresentationTimer.PowerPoint;
using PresentationTimer.Remote;
using Serilog;
using Serilog.Core;

namespace PresentationTimer.App.Tests.Composition;

/// <summary>Verifies the application dependency graph and coordinated lifecycle.</summary>
[TestClass]
public sealed class AppServiceProviderTests
{
    private static readonly string[] ExpectedShutdownSequence =
    [
        "remote.stop",
        "presentation.detach",
        "remote.detach",
        "remote.pairing.detach",
        "powerpoint.stop",
    ];

    /// <summary>Verifies the container resolves one coherent process-lifetime service graph.</summary>
    /// <returns>A task that completes after coordinated shutdown and container disposal.</returns>
    [TestMethod]
    public async Task Create_ValidatedContainer_ResolvesSingletonGraphAndShutsDownOnce()
    {
        // Arrange
        using Logger logger = new LoggerConfiguration().CreateLogger();
        await using ServiceProvider services = AppServiceProvider.Create(logger);

        // Act
        IPresentationSessionService session = services.GetRequiredService<IPresentationSessionService>();
        var concreteSession = services.GetRequiredService<PresentationSessionService>();
        IRemoteSessionHost remote = services.GetRequiredService<IRemoteSessionHost>();
        var concreteRemote = services.GetRequiredService<RemoteSessionHost>();
        IPresentationController presentation = services.GetRequiredService<IPresentationController>();
        var concretePresentation = services.GetRequiredService<PowerPointPresentationController>();
        var compositionRoot = services.GetRequiredService<AppCompositionRoot>();
        Task firstShutdown = compositionRoot.ShutdownAsync();
        Task repeatedShutdown = compositionRoot.ShutdownAsync();
        await Task.WhenAll(firstShutdown, repeatedShutdown);

        // Assert
        Assert.AreSame(session, concreteSession);
        Assert.AreSame(remote, concreteRemote);
        Assert.AreSame(presentation, concretePresentation);
        Assert.AreSame(firstShutdown, repeatedShutdown);
    }

    /// <summary>Verifies subsystem exceptions cannot interrupt the remaining shutdown sequence.</summary>
    /// <returns>A task that completes after repeated coordinated shutdown.</returns>
    [TestMethod]
    public async Task ShutdownAsync_SubsystemsThrow_ContinuesAndInvokesEachOnce()
    {
        // Arrange
        var sequence = new List<string>();
        var remote = new ThrowingRemoteHost(sequence);
        var presentation = new ThrowingPresentationController(sequence);
        var timer = new MonotonicPresentationTimer(new StopwatchMonotonicClock());
        using var session = new PresentationSessionService(timer, presentation, remote);
        var compositionRoot = new AppCompositionRoot(
            session,
            presentation,
            remote,
            new WindowController(),
            NullLogger<AppCompositionRoot>.Instance);

        // Act
        await compositionRoot.ShutdownAsync();
        await compositionRoot.ShutdownAsync();

        // Assert
        Assert.AreEqual(1, remote.StopCalls);
        Assert.AreEqual(1, presentation.StopCalls);
        CollectionAssert.AreEqual(
            ExpectedShutdownSequence,
            sequence);
        Assert.AreEqual(ErrorCodes.ApplicationClosing, session.StartTimer().ErrorCode);
    }

    private sealed class ThrowingRemoteHost : IRemoteSessionHost
    {
        private readonly IList<string> _sequence;

        public ThrowingRemoteHost(IList<string> sequence)
        {
            this._sequence = sequence;
        }

        public event Action<DesktopPairingDescriptor?>? PairingChanged
        {
            add { }
            remove { this._sequence.Add("remote.pairing.detach"); }
        }

        public event Action<RemoteSessionPublicState>? StateChanged
        {
            add { }
            remove { this._sequence.Add("remote.detach"); }
        }

        public RemoteSessionPublicState State => RemoteSessionPublicState.Initial;

        public int StopCalls { get; private set; }

        public Task<OperationResult<DesktopPairingDescriptor>> StartAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Failure<DesktopPairingDescriptor>("unused", "unused"));

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            this.StopCalls++;
            this._sequence.Add("remote.stop");
            return Task.FromException(new InvalidOperationException("simulated remote stop failure"));
        }
    }

    private sealed class ThrowingPresentationController : IPresentationController
    {
        private readonly IList<string> _sequence;

        public ThrowingPresentationController(IList<string> sequence)
        {
            this._sequence = sequence;
        }

        public event Action<PresentationSnapshot>? StateChanged
        {
            add { }
            remove { this._sequence.Add("presentation.detach"); }
        }

        public PresentationSnapshot State => PresentationSnapshot.Initial;

        public int StopCalls { get; private set; }

        public Task StartMonitoringAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            this.StopCalls++;
            this._sequence.Add("powerpoint.stop");
            return Task.FromException(new InvalidOperationException("simulated PowerPoint stop failure"));
        }

        public Task<OperationResult> NextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success());

        public Task<OperationResult> PreviousAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success());
    }
}
