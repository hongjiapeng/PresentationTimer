namespace PresentationTimer.App;

/// <summary>
/// Provides the page with the small portion of window behavior it owns.
/// </summary>
internal sealed class WindowController
{
    private MainWindow? _window;

    /// <summary>Associates the process window with this controller.</summary>
    /// <param name="window">The active process window.</param>
    public void Attach(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        this._window = window;
    }

    /// <summary>Removes an association when the supplied window is current.</summary>
    /// <param name="window">The window being closed.</param>
    public void Detach(MainWindow window)
    {
        if (ReferenceEquals(this._window, window))
        {
            this._window = null;
        }
    }

    /// <summary>Updates the always-on-top preference of the active window.</summary>
    /// <param name="isAlwaysOnTop">Whether the window should remain above other windows.</param>
    public void SetAlwaysOnTop(bool isAlwaysOnTop) =>
        this._window?.SetAlwaysOnTop(isAlwaysOnTop);

    /// <summary>Stops UI-owned timer notifications on the window dispatcher.</summary>
    /// <returns>A task that completes after the UI notification source is stopped.</returns>
    public Task StopUiNotificationsAsync()
    {
        MainWindow? window = this._window;
        if (window is null)
        {
            return Task.CompletedTask;
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            window.StopUiNotifications();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool enqueued = window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                window.StopUiNotifications();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        if (!enqueued)
        {
            completion.SetException(new InvalidOperationException("The UI dispatcher is unavailable during shutdown."));
        }

        return completion.Task;
    }
}
