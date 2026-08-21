using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using PresentationTimer.App.Commands;
using PresentationTimer.App.Imaging;
using PresentationTimer.App.Localization;
using PresentationTimer.Core.Contracts;
using PresentationTimer.Core.Models;
using PresentationTimer.Core.Results;

namespace PresentationTimer.App.ViewModels;

internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DelegateCommand _pauseCommand;
    private readonly DelegateCommand _previousSlideCommand;
    private readonly DelegateCommand _resetCommand;
    private readonly DelegateCommand _resumeCommand;
    private readonly DelegateCommand _nextSlideCommand;
    private readonly DelegateCommand _startRemoteCommand;
    private readonly DelegateCommand _endRemoteCommand;
    private readonly IPresentationSessionService _sessionService;
    private readonly DelegateCommand _startCommand;
    private readonly LocalizedStrings _strings;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _timerNotifier;
    private bool _canPause;
    private bool _canResume;
    private bool _canStart;
    private bool _canNavigateSlides;
    private bool _canEndRemote;
    private bool _canStartRemote;
    private string _durationText = "15:00";
    private bool _isDisposed;
    private bool _isOvertime;
    private bool _isNavigating;
    private bool _isRemoteOperationPending;
    private bool _isValidationOpen;
    private string _powerPointStatusText = string.Empty;
    private string _slidePositionText = string.Empty;
    private string _speakerNotes = string.Empty;
    private string _remoteStatusText = string.Empty;
    private string _remoteConnectionText = string.Empty;
    private string _pairingUrl = string.Empty;
    private ImmutableArray<DesktopPairingCandidate> _pairingCandidates =
        ImmutableArray<DesktopPairingCandidate>.Empty;

    private IReadOnlyList<string> _pairingCandidateLabels = Array.Empty<string>();
    private BitmapImage? _pairingQrImage;
    private long _pairingRevision;
    private int _selectedPairingCandidateIndex = -1;
    private string _timerDisplay = "15:00";
    private string _timerModeLabel = string.Empty;
    private string _timerStatusText = string.Empty;
    private string _validationMessage = string.Empty;

    public MainViewModel(
        IPresentationSessionService sessionService,
        DispatcherQueue dispatcherQueue,
        LocalizedStrings strings)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        ArgumentNullException.ThrowIfNull(strings);
        this._sessionService = sessionService;
        this._dispatcherQueue = dispatcherQueue;
        this._strings = strings;
        this._startCommand = new DelegateCommand(this.Start, () => this.CanStart);
        this._pauseCommand = new DelegateCommand(this.Pause, () => this.CanPause);
        this._resumeCommand = new DelegateCommand(this.Resume, () => this.CanResume);
        this._resetCommand = new DelegateCommand(this.Reset);
        this._previousSlideCommand = new DelegateCommand(
            () => this.StartNavigation(forward: false),
            () => this.CanNavigateSlides);
        this._nextSlideCommand = new DelegateCommand(
            () => this.StartNavigation(forward: true),
            () => this.CanNavigateSlides);
        this._startRemoteCommand = new DelegateCommand(
            () => this.StartRemoteOperation(start: true),
            () => this.CanStartRemote);
        this._endRemoteCommand = new DelegateCommand(
            () => this.StartRemoteOperation(start: false),
            () => this.CanEndRemote);
        this._sessionService.StateChanged += this.OnSessionStateChanged;
        this._sessionService.PairingChanged += this.OnPairingChanged;
        this._timerNotifier = dispatcherQueue.CreateTimer();
        this._timerNotifier.Interval = TimeSpan.FromMilliseconds(200);
        this._timerNotifier.IsRepeating = true;
        this._timerNotifier.Tick += this.OnTimerTick;
        this.ApplyState(this._sessionService.State);
        this._timerNotifier.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DurationText
    {
        get => this._durationText;
        set
        {
            if (this.SetProperty(ref this._durationText, value))
            {
                this.IsValidationOpen = false;
            }
        }
    }

    public string TimerDisplay
    {
        get => this._timerDisplay;
        private set => this.SetProperty(ref this._timerDisplay, value);
    }

    public string TimerModeLabel
    {
        get => this._timerModeLabel;
        private set => this.SetProperty(ref this._timerModeLabel, value);
    }

    public string TimerStatusText
    {
        get => this._timerStatusText;
        private set => this.SetProperty(ref this._timerStatusText, value);
    }

    public string PowerPointStatusText
    {
        get => this._powerPointStatusText;
        private set => this.SetProperty(ref this._powerPointStatusText, value);
    }

    public string SlidePositionText
    {
        get => this._slidePositionText;
        private set => this.SetProperty(ref this._slidePositionText, value);
    }

    public string SpeakerNotes
    {
        get => this._speakerNotes;
        private set => this.SetProperty(ref this._speakerNotes, value);
    }

    public string RemoteStatusText
    {
        get => this._remoteStatusText;
        private set => this.SetProperty(ref this._remoteStatusText, value);
    }

    public string RemoteConnectionText
    {
        get => this._remoteConnectionText;
        private set => this.SetProperty(ref this._remoteConnectionText, value);
    }

    public string PairingUrl
    {
        get => this._pairingUrl;
        private set
        {
            if (this.SetProperty(ref this._pairingUrl, value))
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsPairingVisible)));
            }
        }
    }

    public BitmapImage? PairingQrImage
    {
        get => this._pairingQrImage;
        private set
        {
            if (this.SetProperty(ref this._pairingQrImage, value))
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsPairingVisible)));
            }
        }
    }

    public bool IsPairingVisible => !string.IsNullOrWhiteSpace(this.PairingUrl);

    public IReadOnlyList<string> PairingCandidateLabels
    {
        get => this._pairingCandidateLabels;
        private set
        {
            if (this.SetProperty(ref this._pairingCandidateLabels, value))
            {
                this.PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(this.HasMultiplePairingCandidates)));
            }
        }
    }

    public bool HasMultiplePairingCandidates => this.PairingCandidateLabels.Count > 1;

    public int SelectedPairingCandidateIndex
    {
        get => this._selectedPairingCandidateIndex;
        set
        {
            if (!this.SetProperty(ref this._selectedPairingCandidateIndex, value) ||
                value < 0 ||
                value >= this._pairingCandidates.Length)
            {
                return;
            }

            long revision = Interlocked.Increment(ref this._pairingRevision);
            _ = this.ApplyPairingCandidateAsync(this._pairingCandidates[value], revision);
        }
    }

    public string ValidationMessage
    {
        get => this._validationMessage;
        private set => this.SetProperty(ref this._validationMessage, value);
    }

    public bool IsValidationOpen
    {
        get => this._isValidationOpen;
        set => this.SetProperty(ref this._isValidationOpen, value);
    }

    public bool IsOvertime
    {
        get => this._isOvertime;
        private set
        {
            if (this.SetProperty(ref this._isOvertime, value))
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsCountdown)));
            }
        }
    }

    public bool IsCountdown => !this.IsOvertime;

    public bool CanStart
    {
        get => this._canStart;
        private set => this.SetProperty(ref this._canStart, value);
    }

    public bool CanPause
    {
        get => this._canPause;
        private set => this.SetProperty(ref this._canPause, value);
    }

    public bool CanResume
    {
        get => this._canResume;
        private set => this.SetProperty(ref this._canResume, value);
    }

    public bool CanNavigateSlides
    {
        get => this._canNavigateSlides;
        private set => this.SetProperty(ref this._canNavigateSlides, value);
    }

    public bool CanStartRemote
    {
        get => this._canStartRemote;
        private set => this.SetProperty(ref this._canStartRemote, value);
    }

    public bool CanEndRemote
    {
        get => this._canEndRemote;
        private set => this.SetProperty(ref this._canEndRemote, value);
    }

    public ICommand StartCommand => this._startCommand;

    public ICommand PauseCommand => this._pauseCommand;

    public ICommand ResumeCommand => this._resumeCommand;

    public ICommand ResetCommand => this._resetCommand;

    public ICommand PreviousSlideCommand => this._previousSlideCommand;

    public ICommand NextSlideCommand => this._nextSlideCommand;

    public ICommand StartRemoteCommand => this._startRemoteCommand;

    public ICommand EndRemoteCommand => this._endRemoteCommand;

    public void Dispose()
    {
        if (this._isDisposed)
        {
            return;
        }

        this._timerNotifier.Stop();
        this._timerNotifier.Tick -= this.OnTimerTick;
        this._sessionService.StateChanged -= this.OnSessionStateChanged;
        this._sessionService.PairingChanged -= this.OnPairingChanged;
        this._isDisposed = true;
    }

    private static string FormatTimer(TimeSpan value)
    {
        long totalSeconds = Math.Max(0, checked((long)value.TotalSeconds));
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;

        return hours > 0
            ? string.Format(CultureInfo.CurrentCulture, "{0}:{1:00}:{2:00}", hours, minutes, seconds)
            : string.Format(CultureInfo.CurrentCulture, "{0:00}:{1:00}", minutes, seconds);
    }

    private void ApplyState(PresentationSessionState state)
    {
        this.TimerDisplay = FormatTimer(state.Timer.DisplayValue);
        this.IsOvertime = state.Timer.IsOvertime;
        this.TimerModeLabel = this._strings.Get(state.Timer.IsOvertime
            ? "TimerModeOvertime"
            : "TimerModeRemaining");
        this.TimerStatusText = this._strings.Get(state.Timer.RunState switch
        {
            TimerRunState.Ready => "TimerStateReady",
            TimerRunState.Running => "TimerStateRunning",
            TimerRunState.Paused => "TimerStatePaused",
            _ => "TimerStateReady",
        });
        this.PowerPointStatusText = this._strings.Get(state.Presentation.Connection switch
        {
            PresentationConnectionState.Running => "PowerPointStateRunning",
            PresentationConnectionState.NoPresentation => "PowerPointStateNoPresentation",
            PresentationConnectionState.NoSlideShow => "PowerPointStateNoSlideShow",
            PresentationConnectionState.NotRunning => "PowerPointStateNotRunning",
            PresentationConnectionState.Disconnected => "PowerPointStateDisconnected",
            _ => "PowerPointStateUnavailable",
        });
        this.SlidePositionText = state.Presentation.CurrentSlideIndex is int current &&
            state.Presentation.TotalSlides is int total
            ? string.Format(
                CultureInfo.CurrentCulture,
                this._strings.Get("PowerPointSlidePositionFormat"),
                current,
                total)
            : string.Empty;
        this.SpeakerNotes = string.IsNullOrWhiteSpace(state.Presentation.SpeakerNotes)
            ? this._strings.Get("PowerPointNoNotes")
            : state.Presentation.SpeakerNotes;
        this.RemoteStatusText = this._strings.Get(state.Remote.LastErrorCode == ErrorCodes.RemoteNoLanAddress
            ? "RemoteStateNoLanAddress"
            : state.Remote.Status switch
        {
            RemoteSessionStatus.Starting => "RemoteStateStarting",
            RemoteSessionStatus.Ready => "RemoteStateReady",
            RemoteSessionStatus.Failed => "RemoteStateFailed",
            RemoteSessionStatus.Stopping => "RemoteStateStopping",
            _ => "RemoteStateStopped",
        });
        this.RemoteConnectionText = state.Remote.AuthenticatedConnectionCount switch
        {
            0 => this._strings.Get("RemoteNoPhones"),
            1 => this._strings.Get("RemoteOnePhone"),
            int count => string.Format(
                CultureInfo.CurrentCulture,
                this._strings.Get("RemotePhoneCountFormat"),
                count),
        };
        this.CanStart = state.Timer.RunState == TimerRunState.Ready;
        this.CanPause = state.Timer.RunState == TimerRunState.Running;
        this.CanResume = state.Timer.RunState == TimerRunState.Paused;
        this.CanNavigateSlides = state.Presentation.Connection == PresentationConnectionState.Running &&
            !this._isNavigating;
        this.CanStartRemote = state.Remote.Status is RemoteSessionStatus.Stopped or RemoteSessionStatus.Failed &&
            !this._isRemoteOperationPending;
        this.CanEndRemote = (state.Remote.Status is RemoteSessionStatus.Starting or
            RemoteSessionStatus.Ready or RemoteSessionStatus.Failed) &&
            !this._isRemoteOperationPending;
        this._startCommand.RaiseCanExecuteChanged();
        this._pauseCommand.RaiseCanExecuteChanged();
        this._resumeCommand.RaiseCanExecuteChanged();
        this._previousSlideCommand.RaiseCanExecuteChanged();
        this._nextSlideCommand.RaiseCanExecuteChanged();
        this._startRemoteCommand.RaiseCanExecuteChanged();
        this._endRemoteCommand.RaiseCanExecuteChanged();
    }

    private void OnSessionStateChanged(PresentationSessionState state)
    {
        if (this._dispatcherQueue.HasThreadAccess)
        {
            this.ApplyState(state);
            return;
        }

        _ = this._dispatcherQueue.TryEnqueue(() => this.ApplyState(state));
    }

    private void OnPairingChanged(DesktopPairingDescriptor? descriptor)
    {
        long revision = Interlocked.Increment(ref this._pairingRevision);
        if (this._dispatcherQueue.HasThreadAccess)
        {
            _ = this.ApplyPairingAsync(descriptor, revision);
            return;
        }

        _ = this._dispatcherQueue.TryEnqueue(() => _ = this.ApplyPairingAsync(descriptor, revision));
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (this._sessionService.State.Timer.RunState == TimerRunState.Running)
        {
            this._sessionService.RefreshTimer();
        }
    }

    private void Start()
    {
        OperationResult<TimerSnapshot> configured = this._sessionService.ConfigureTimer(this.DurationText);
        if (!configured.IsSuccess)
        {
            this.ShowTimerValidation();
            return;
        }

        OperationResult<TimerSnapshot> started = this._sessionService.StartTimer();
        if (!started.IsSuccess)
        {
            this.ShowTimerValidation();
        }
    }

    private void Pause() => this._sessionService.PauseTimer();

    private void Resume() => this._sessionService.ResumeTimer();

    private void Reset()
    {
        this._sessionService.ResetTimer();
        this.IsValidationOpen = false;
    }

    private void ShowTimerValidation()
    {
        this.ValidationMessage = this._strings.Get("TimerInvalidDuration");
        this.IsValidationOpen = true;
    }

    private void StartNavigation(bool forward)
    {
        if (this._isNavigating || !this.CanNavigateSlides)
        {
            return;
        }

        this._isNavigating = true;
        this.CanNavigateSlides = false;
        this._previousSlideCommand.RaiseCanExecuteChanged();
        this._nextSlideCommand.RaiseCanExecuteChanged();
        _ = this.NavigateAsync(forward);
    }

    private async Task NavigateAsync(bool forward)
    {
        try
        {
            OperationResult result = forward
                ? await this._sessionService.NextSlideAsync()
                : await this._sessionService.PreviousSlideAsync();
            if (!result.IsSuccess)
            {
                this.PowerPointStatusText = result.ErrorCode == ErrorCodes.PresentationBusy
                    ? this._strings.Get("PowerPointCommandBusy")
                    : this._strings.Get("PowerPointCommandUnavailable");
            }
        }
        finally
        {
            this._isNavigating = false;
            this.CanNavigateSlides =
                this._sessionService.State.Presentation.Connection == PresentationConnectionState.Running;
            this._previousSlideCommand.RaiseCanExecuteChanged();
            this._nextSlideCommand.RaiseCanExecuteChanged();
        }
    }

    private void StartRemoteOperation(bool start)
    {
        if (this._isRemoteOperationPending || (start ? !this.CanStartRemote : !this.CanEndRemote))
        {
            return;
        }

        this._isRemoteOperationPending = true;
        if (!start)
        {
            this.PairingUrl = string.Empty;
            this.PairingQrImage = null;
        }

        this.RefreshRemoteCommandState();
        _ = this.RunRemoteOperationAsync(start);
    }

    private async Task RunRemoteOperationAsync(bool start)
    {
        try
        {
            if (start)
            {
                OperationResult<DesktopPairingDescriptor> result =
                    await this._sessionService.StartRemoteSessionAsync();
                if (result.IsSuccess && result.Value is DesktopPairingDescriptor descriptor)
                {
                    long revision = Interlocked.Increment(ref this._pairingRevision);
                    await this.ApplyPairingAsync(descriptor, revision);
                }
            }
            else
            {
                await this._sessionService.EndRemoteSessionAsync();
            }
        }
        finally
        {
            this._isRemoteOperationPending = false;
            this.ApplyState(this._sessionService.State);
        }
    }

    private void RefreshRemoteCommandState()
    {
        this.CanStartRemote = false;
        this.CanEndRemote = false;
        this._startRemoteCommand.RaiseCanExecuteChanged();
        this._endRemoteCommand.RaiseCanExecuteChanged();
    }

    private async Task ApplyPairingAsync(DesktopPairingDescriptor? descriptor, long revision)
    {
        if (descriptor is null)
        {
            if (revision == Volatile.Read(ref this._pairingRevision))
            {
                this._pairingCandidates = ImmutableArray<DesktopPairingCandidate>.Empty;
                this.PairingCandidateLabels = Array.Empty<string>();
                this.SelectedPairingCandidateIndex = -1;
                this.PairingUrl = string.Empty;
                this.PairingQrImage = null;
            }

            return;
        }

        ImmutableArray<DesktopPairingCandidate> candidates = descriptor.Candidates.IsEmpty
            ? ImmutableArray.Create(new DesktopPairingCandidate(
                descriptor.PairingUri.Host,
                descriptor.PairingUri,
                descriptor.QrPayload)
            {
                QrPng = descriptor.QrPng,
            })
            : descriptor.Candidates;
        if (revision == Volatile.Read(ref this._pairingRevision))
        {
            this._pairingCandidates = candidates;
            this.PairingCandidateLabels = candidates
                .Select(static candidate => string.Concat(
                    candidate.InterfaceLabel,
                    " — ",
                    candidate.PairingUri.GetLeftPart(UriPartial.Authority)))
                .ToArray();
            int selectedIndex = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].PairingUri == descriptor.PairingUri)
                {
                    selectedIndex = index;
                    break;
                }
            }

            this._selectedPairingCandidateIndex = selectedIndex;
            this.PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(this.SelectedPairingCandidateIndex)));
            await this.ApplyPairingCandidateAsync(candidates[selectedIndex], revision);
        }
    }

    private async Task ApplyPairingCandidateAsync(
        DesktopPairingCandidate candidate,
        long revision)
    {
        BitmapImage image = await PairingBitmapFactory.CreateAsync(candidate.QrPng.ToArray());
        if (revision == Volatile.Read(ref this._pairingRevision))
        {
            this.PairingUrl = candidate.PairingUri.AbsoluteUri;
            this.PairingQrImage = image;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
