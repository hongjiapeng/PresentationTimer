using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PresentationTimer.App.Localization;
using PresentationTimer.App.ViewModels;
using PresentationTimer.Core.Contracts;

namespace PresentationTimer.App;

/// <summary>
/// Displays the compact timer and expanded presenter control center over one session view model.
/// </summary>
public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private readonly WindowController _windowController;
    private bool _isAlwaysOnTop;
    private bool _isPreparedForShutdown;
    private DesktopShellMode _shellMode = DesktopShellMode.Compact;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    internal MainPage(
        IPresentationSessionService sessionService,
        LocalizedStrings strings,
        WindowController windowController)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(windowController);
        this._windowController = windowController;
        this.InitializeComponent();
        this.ViewModel = new MainViewModel(
            sessionService,
            this.DispatcherQueue,
            strings);
        this.ViewModel.PropertyChanged += this.OnViewModelPropertyChanged;
        this.ActualThemeChanged += this.OnActualThemeChanged;
        this.Unloaded += this.OnUnloaded;
    }

    /// <summary>Raised when shell or presentation properties change.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    internal event Action<FrameworkElement>? DragRegionLoaded;

    private enum ControlCenterSection
    {
        Timer,
        PowerPoint,
        Remote,
        Duration,
    }

    private enum DesktopShellMode
    {
        Compact,
        PresentationHud,
        Expanded,
    }

    /// <summary>
    /// Gets or sets a value indicating whether the presenter window remains above other windows.
    /// </summary>
    public bool IsAlwaysOnTop
    {
        get => this._isAlwaysOnTop;
        set
        {
            if (!this.SetProperty(ref this._isAlwaysOnTop, value))
            {
                return;
            }

            this._windowController.SetAlwaysOnTop(value);
        }
    }

    /// <summary>Gets a value indicating whether the compact timer root is active.</summary>
    public bool IsCompactMode => this._shellMode == DesktopShellMode.Compact;

    /// <summary>Gets a value indicating whether the presentation HUD root is active.</summary>
    public bool IsPresentationHudMode => this._shellMode == DesktopShellMode.PresentationHud;

    /// <summary>Gets a value indicating whether the expanded control center root is active.</summary>
    public bool IsExpandedMode => this._shellMode == DesktopShellMode.Expanded;

    /// <summary>Gets the semantic brush for the current timer presentation state.</summary>
    public Brush TimerForeground
    {
        get
        {
            string resourceKey;
            switch (this.ViewModel.TimerVisualState)
            {
                case TimerVisualState.Warning:
                    resourceKey = "PresenterTimerWarningBrush";
                    break;
                case TimerVisualState.Overtime:
                    resourceKey = "PresenterTimerOvertimeBrush";
                    break;
                default:
                    resourceKey = "PresenterTimerNormalBrush";
                    break;
            }

            return (Brush)Application.Current.Resources[resourceKey];
        }
    }

    internal FrameworkElement? ActiveDragRegion => this._shellMode switch
    {
        DesktopShellMode.Compact => this.CompactDragRegion,
        DesktopShellMode.PresentationHud => this.PresentationHudDragRegion,
        _ => null,
    };

    internal MainViewModel ViewModel { get; }

    internal void PrepareForShutdown()
    {
        if (this._isPreparedForShutdown)
        {
            return;
        }

        this._isPreparedForShutdown = true;
        this.Unloaded -= this.OnUnloaded;
        this.ActualThemeChanged -= this.OnActualThemeChanged;
        this.ViewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
        this.ViewModel.Dispose();
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs args)
    {
        if (this.ViewModel.CanStart)
        {
            this.EnterCompactMode();
        }
        else
        {
            this.EnterPresentationHudMode();
        }
    }

    private void CompactDragRegion_Loaded(object sender, RoutedEventArgs args)
    {
        if (this.IsCompactMode && sender is FrameworkElement dragRegion)
        {
            this.DragRegionLoaded?.Invoke(dragRegion);
        }
    }

    private async void CustomDurationButton_Click(object sender, RoutedEventArgs args)
    {
        this.ViewModel.IsValidationOpen = false;
        this.CustomDurationInput.Text = this.ViewModel.DurationText;
        this.CustomDurationDialog.XamlRoot = this.XamlRoot;
        this.CustomDurationDialog.RequestedTheme = this.ActualTheme;
        _ = await this.CustomDurationDialog.ShowAsync();
    }

    private void CustomDurationDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = !this.ViewModel.TryConfigureDuration(this.CustomDurationInput.Text);
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs args) =>
        this._windowController.RequestClose();

    private void EnterPresentationHudMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (!this.ViewModel.CanStart)
        {
            this.EnterPresentationHudMode();
        }
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs args) =>
        this.OpenControlCenter(ControlCenterSection.Timer);

    private void OnActualThemeChanged(FrameworkElement sender, object args) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimerForeground)));

    private void OnUnloaded(object sender, RoutedEventArgs args) => this.PrepareForShutdown();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainViewModel.TimerVisualState))
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimerForeground)));
        }

        if (args.PropertyName == nameof(MainViewModel.CanStart))
        {
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                if (this._shellMode == DesktopShellMode.PresentationHud && this.ViewModel.CanStart)
                {
                    this.EnterCompactMode();
                }
            });
        }
    }

    private void OpenControlCenterMenuItem_Click(object sender, RoutedEventArgs args) =>
        this.OpenControlCenter(ControlCenterSection.Timer);

    private void PowerPointMenuItem_Click(object sender, RoutedEventArgs args) =>
        this.OpenControlCenter(ControlCenterSection.PowerPoint);

    private void PresentationHudDragRegion_Loaded(object sender, RoutedEventArgs args)
    {
        if (this.IsPresentationHudMode && sender is FrameworkElement dragRegion)
        {
            this.DragRegionLoaded?.Invoke(dragRegion);
        }
    }

    private void RemoteMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (this.ViewModel.CanStartRemote)
        {
            this.ViewModel.StartRemoteCommand.Execute(null);
        }

        this.OpenControlCenter(ControlCenterSection.Remote);
    }

    private void TimerSettingsMenuItem_Click(object sender, RoutedEventArgs args) =>
        this.OpenControlCenter(ControlCenterSection.Duration);

    private void OpenControlCenter(ControlCenterSection section)
    {
        this.SetShellMode(DesktopShellMode.Expanded);
        this._windowController.EnterExpanded();
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            this.UpdateLayout();
            FrameworkElement target = section switch
            {
                ControlCenterSection.PowerPoint => this.PowerPointSection,
                ControlCenterSection.Remote => this.RemoteSection,
                ControlCenterSection.Duration => this.DurationSection,
                _ => this.TimerHero,
            };
            _ = target.Focus(FocusState.Programmatic);
        });
    }

    private void EnterCompactMode()
    {
        this.SetShellMode(DesktopShellMode.Compact);
        this._windowController.EnterCompact();
        _ = this.DispatcherQueue.TryEnqueue(() =>
            this.CompactExpandButton.Focus(FocusState.Programmatic));
    }

    private void EnterPresentationHudMode()
    {
        this.SetShellMode(DesktopShellMode.PresentationHud);
        this._windowController.EnterPresentationHud();
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            Button target = this.ViewModel.CanPause
                ? this.HudPauseButton
                : this.HudResumeButton;
            _ = target.Focus(FocusState.Programmatic);
        });
    }

    private void SetShellMode(DesktopShellMode value)
    {
        if (this._shellMode == value)
        {
            return;
        }

        this._shellMode = value;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsCompactMode)));
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsPresentationHudMode)));
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsExpandedMode)));
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
