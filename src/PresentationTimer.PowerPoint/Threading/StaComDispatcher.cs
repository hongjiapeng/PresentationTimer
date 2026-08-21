using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PresentationTimer.PowerPoint.Threading;

/// <summary>Represents one callback queued to the COM apartment.</summary>
internal interface IStaWorkItem
{
    /// <summary>Executes the queued callback.</summary>
    void Execute();

    /// <summary>Completes the queued callback with a dispatcher failure.</summary>
    /// <param name="exception">The dispatcher failure.</param>
    void Fail(Exception exception);
}

/// <summary>
/// Serializes COM work on a dedicated STA that continuously pumps Windows messages.
/// </summary>
internal sealed class StaComDispatcher : IAsyncDisposable, IDisposable
{
    private const uint PrivateWorkMessage = 0x8000 + 0x2A;
    private readonly object _gate = new object();
    private readonly ConcurrentQueue<IStaWorkItem> _workItems = new ConcurrentQueue<IStaWorkItem>();
    private readonly TaskCompletionSource _started = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _stopped = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Thread _thread;
    private bool _acceptingWork = true;
    private uint _threadId;

    public StaComDispatcher(string threadName = "PresentationTimer.PowerPoint.STA")
    {
        this._thread = new Thread(this.RunMessageLoop)
        {
            IsBackground = true,
            Name = threadName,
        };
        this._thread.SetApartmentState(ApartmentState.STA);
        this._thread.Start();
    }

    internal int ManagedThreadId { get; private set; }

    public async Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();
        await this._started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        var workItem = new WorkItem<T>(callback, cancellationToken);
        lock (this._gate)
        {
            if (!this._acceptingWork)
            {
                ObjectDisposedException.ThrowIf(!this._acceptingWork, this);
            }

            this._workItems.Enqueue(workItem);
            if (!StaNativeMethods.PostThreadMessage(this._threadId, PrivateWorkMessage, 0, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        return await workItem.Task.ConfigureAwait(false);
    }

    public async Task InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        await this.InvokeAsync(
            () =>
            {
                callback();
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        this.StopAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    internal async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await this._started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (this._gate)
        {
            if (this._acceptingWork)
            {
                this._acceptingWork = false;
                this._workItems.Enqueue(new StopWorkItem());
                if (!StaNativeMethods.PostThreadMessage(this._threadId, PrivateWorkMessage, 0, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        await this._stopped.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void DrainWorkItems()
    {
        while (this._workItems.TryDequeue(out IStaWorkItem? workItem))
        {
            if (workItem is StopWorkItem)
            {
                StaNativeMethods.PostQuitMessage(0);
                return;
            }

            workItem.Execute();
        }
    }

    private void RunMessageLoop()
    {
        int oleResult = StaNativeMethods.OleInitialize(0);
        if (oleResult < 0)
        {
            this._started.TrySetException(
                System.Runtime.InteropServices.Marshal.GetExceptionForHR(oleResult) ??
                new InvalidOperationException($"OLE initialization failed with HRESULT 0x{oleResult:X8}."));
            this._stopped.TrySetResult();
            return;
        }

        try
        {
            _ = StaNativeMethods.PeekMessage(out _, 0, 0, 0, 0);
            this._threadId = StaNativeMethods.GetCurrentThreadId();
            this.ManagedThreadId = Environment.CurrentManagedThreadId;
            this._started.TrySetResult();

            while (true)
            {
                int result = StaNativeMethods.GetMessage(out NativeMessage message, 0, 0, 0);
                if (result == 0)
                {
                    break;
                }

                if (result == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (message.Id == PrivateWorkMessage)
                {
                    this.DrainWorkItems();
                    continue;
                }

                _ = StaNativeMethods.TranslateMessage(in message);
                _ = StaNativeMethods.DispatchMessage(in message);
            }
        }
        catch (Exception exception)
        {
            this._started.TrySetException(exception);
            while (this._workItems.TryDequeue(out IStaWorkItem? workItem))
            {
                workItem.Fail(exception);
            }
        }
        finally
        {
            StaNativeMethods.OleUninitialize();
            this._stopped.TrySetResult();
        }
    }

    private sealed class StopWorkItem : IStaWorkItem
    {
        public void Execute()
        {
        }

        public void Fail(Exception exception)
        {
        }
    }

    private sealed class WorkItem<T> : IStaWorkItem
    {
        private readonly Func<T> _callback;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<T> _completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public WorkItem(Func<T> callback, CancellationToken cancellationToken)
        {
            this._callback = callback;
            this._cancellationToken = cancellationToken;
        }

        public Task<T> Task => this._completion.Task;

        public void Execute()
        {
            if (this._cancellationToken.IsCancellationRequested)
            {
                this._completion.TrySetCanceled(this._cancellationToken);
                return;
            }

            try
            {
                this._completion.TrySetResult(this._callback());
            }
            catch (Exception exception)
            {
                this._completion.TrySetException(exception);
            }
        }

        public void Fail(Exception exception) => this._completion.TrySetException(exception);
    }
}
