using PresentationTimer.Core.Models;

namespace PresentationTimer.Core.Services;

/// <summary>
/// Stores one immutable, revisioned aggregate session snapshot.
/// </summary>
public sealed class SessionStateStore
{
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new ();
    private PresentationSessionState _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStateStore"/> class.
    /// </summary>
    /// <param name="presentation">The initial presentation slice.</param>
    /// <param name="timer">The initial timer slice.</param>
    /// <param name="remote">The initial remote slice.</param>
    /// <param name="timeProvider">The diagnostic UTC time provider.</param>
    public SessionStateStore(
        PresentationSnapshot presentation,
        TimerSnapshot timer,
        RemoteSessionPublicState remote,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(remote);

        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._state = new PresentationSessionState(
            0,
            this._timeProvider.GetUtcNow(),
            presentation,
            timer,
            remote);
    }

    /// <summary>Occurs after a changed slice is committed and the store lock is released.</summary>
    public event Action<PresentationSessionState>? StateChanged;

    /// <summary>Gets the latest immutable aggregate state.</summary>
    public PresentationSessionState State
    {
        get
        {
            lock (this._sync)
            {
                return this._state;
            }
        }
    }

    /// <summary>Updates only the presentation slice.</summary>
    /// <param name="presentation">The new presentation snapshot.</param>
    /// <returns>The current aggregate state.</returns>
    public PresentationSessionState UpdatePresentation(PresentationSnapshot presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return this.Update(
            state => state.Presentation == presentation,
            state => state with { Presentation = presentation });
    }

    /// <summary>Updates only the timer slice.</summary>
    /// <param name="timer">The new timer snapshot.</param>
    /// <returns>The current aggregate state.</returns>
    public PresentationSessionState UpdateTimer(TimerSnapshot timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        return this.Update(
            state => state.Timer == timer,
            state => state with { Timer = timer });
    }

    /// <summary>Updates only the remote-session slice.</summary>
    /// <param name="remote">The new remote-session snapshot.</param>
    /// <returns>The current aggregate state.</returns>
    public PresentationSessionState UpdateRemote(RemoteSessionPublicState remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        return this.Update(
            state => state.Remote == remote,
            state => state with { Remote = remote });
    }

    private PresentationSessionState Update(
        Func<PresentationSessionState, bool> isEqual,
        Func<PresentationSessionState, PresentationSessionState> apply)
    {
        PresentationSessionState stateToPublish;
        Action<PresentationSessionState>? handler;

        lock (this._sync)
        {
            if (isEqual(this._state))
            {
                return this._state;
            }

            PresentationSessionState updated = apply(this._state);
            this._state = updated with
            {
                Revision = checked(this._state.Revision + 1),
                ObservedAtUtc = this._timeProvider.GetUtcNow(),
            };
            stateToPublish = this._state;
            handler = this.StateChanged;
        }

        handler?.Invoke(stateToPublish);
        return stateToPublish;
    }
}
