using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.Storage.Pickers;
using PresentationTimer.App.Localization;
using PresentationTimer.App.ViewModels;
using PresentationTimer.Core.Contracts;

namespace PresentationTimer.App;

/// <summary>
/// Displays the compact timer and expanded presenter control center over one session view model.
/// </summary>
public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private const double ExpandedArcDashLength = 106d;
    private const double FloatingProgressWidth = 104d;
    private static readonly TimeSpan FloatingRevealDuration = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan FloatingHideDelay = TimeSpan.FromMilliseconds(1250);
    private readonly WindowController _windowController;
    private readonly DispatcherQueueTimer _floatingHideTimer;
    private readonly string _languageTag = LanguageManager.CurrentLanguageTag;
    private bool _isAlwaysOnTop;
    private bool _isHiddenFromCapture = true;
    private bool _isPresentationPickerOpen;
    private bool _isPreparedForShutdown;
    private bool _isFloatingMenuOpen;
    private bool _isFloatingPointerOver;
    private Storyboard? _floatingAnimation;
    private DesktopShellMode _shellMode = DesktopShellMode.Expanded;

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
        this._floatingHideTimer = this.DispatcherQueue.CreateTimer();
        this._floatingHideTimer.Interval = FloatingHideDelay;
        this._floatingHideTimer.Tick += this.OnFloatingHideTick;
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

    /// <summary>Gets or sets a value indicating whether the compact presenter window is hidden from supported screen capture.</summary>
    public bool IsHiddenFromCapture
    {
        get => this._isHiddenFromCapture;
        set
        {
            if (this.SetProperty(ref this._isHiddenFromCapture, value))
            {
                this._windowController.SetHiddenFromCapture(value);
            }
        }
    }

    /// <summary>Gets a value indicating whether the compact timer root is active.</summary>
    public bool IsCompactMode => this._shellMode == DesktopShellMode.Compact;

    /// <summary>Gets a value indicating whether the presentation HUD root is active.</summary>
    public bool IsPresentationHudMode => this._shellMode == DesktopShellMode.PresentationHud;

    /// <summary>Gets a value indicating whether the expanded control center root is active.</summary>
    public bool IsExpandedMode => this._shellMode == DesktopShellMode.Expanded;

    /// <summary>Gets a value indicating whether Simplified Chinese is selected.</summary>
    public bool IsSimplifiedChineseLanguage =>
        string.Equals(
            this._languageTag,
            LanguageManager.SimplifiedChinese,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether English is selected.</summary>
    public bool IsEnglishLanguage =>
        string.Equals(
            this._languageTag,
            LanguageManager.English,
            StringComparison.OrdinalIgnoreCase);

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
                case TimerVisualState.Critical:
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

    /// <summary>Gets the noninteractive floating progress width in effective pixels.</summary>
    internal double CompactProgressWidth =>
        Math.Clamp(this.ViewModel.TimerProgressValue / 100d, 0d, 1d) * FloatingProgressWidth;

    /// <summary>Gets the dash offset used by the expanded progress arc.</summary>
    internal double ExpandedArcProgressDashOffset { get; private set; }

    internal MainViewModel ViewModel { get; }

    internal void PrepareForShutdown()
    {
        if (this._isPreparedForShutdown)
        {
            return;
        }

        this._isPreparedForShutdown = true;
        this._floatingHideTimer.Stop();
        this._floatingHideTimer.Tick -= this.OnFloatingHideTick;
        this._floatingAnimation?.Stop();
        this.Unloaded -= this.OnUnloaded;
        this.ActualThemeChanged -= this.OnActualThemeChanged;
        this.ViewModel.PropertyChanged -= this.OnViewModelPropertyChanged;
        this.ViewModel.Dispose();
    }

    internal void RestoreCompactMode() => this.SetShellMode(DesktopShellMode.Compact);

    internal void RestorePresentationHudMode() => this.SetShellMode(DesktopShellMode.PresentationHud);

    private static void AddFloatingAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        double from,
        double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(FloatingRevealDuration),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
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

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string languageTag)
        {
            ((App)Application.Current).ChangeLanguage(languageTag);
        }
    }

    private void EnterPresentationHudMenuItem_Click(object sender, RoutedEventArgs args)
    {
        if (!this.ViewModel.CanStart)
        {
            this.EnterPresentationHudMode();
        }
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs args) =>
        this.OpenControlCenter(ControlCenterSection.Timer);

    private void FloatingTimer_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        this._isFloatingPointerOver = true;
        this._floatingHideTimer.Stop();
        this.AnimateFloatingControls(show: true);
    }

    private void FloatingTimer_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        this._isFloatingPointerOver = false;
        if (!this._isFloatingMenuOpen)
        {
            this._floatingHideTimer.Stop();
            this._floatingHideTimer.Start();
        }
    }

    private void FloatingTimer_GotFocus(object sender, RoutedEventArgs args)
    {
        this._floatingHideTimer.Stop();
        this.AnimateFloatingControls(show: true);
    }

    private void FloatingTimer_LostFocus(object sender, RoutedEventArgs args) =>
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            if (!this._isFloatingPointerOver && !this._isFloatingMenuOpen && !this.HasFloatingKeyboardFocus())
            {
                this._floatingHideTimer.Stop();
                this._floatingHideTimer.Start();
            }
        });

    private bool HasFloatingKeyboardFocus()
    {
        if (this.XamlRoot is null || FocusManager.GetFocusedElement(this.XamlRoot) is not UIElement focused ||
            focused.FocusState != FocusState.Keyboard)
        {
            return false;
        }

        DependencyObject? current = focused;
        UIElement? root = this.IsCompactMode ? this.CompactRoot : this.IsPresentationHudMode ? this.PresentationHudRoot : null;
        while (current is not null)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void FloatingMenu_Opened(object sender, object args)
    {
        this._isFloatingMenuOpen = true;
        this._floatingHideTimer.Stop();
        this.AnimateFloatingControls(show: true);
    }

    private void FloatingMenu_Closed(object sender, object args)
    {
        this._isFloatingMenuOpen = false;
        if (!this._isFloatingPointerOver)
        {
            this._floatingHideTimer.Stop();
            this._floatingHideTimer.Start();
        }
    }

    private void OnFloatingHideTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (!this._isFloatingPointerOver && !this._isFloatingMenuOpen && !this.HasFloatingKeyboardFocus())
        {
            this.AnimateFloatingControls(show: false);
        }
    }

    private void AnimateFloatingControls(bool show)
    {
        Grid controls;
        Border overlay;
        if (this.IsCompactMode)
        {
            controls = this.CompactControls;
            overlay = this.CompactGlassOverlay;
        }
        else if (this.IsPresentationHudMode)
        {
            controls = this.HudControls;
            overlay = this.HudGlassOverlay;
        }
        else
        {
            return;
        }

        if (controls?.RenderTransform is not TranslateTransform translation || overlay is null)
        {
            return;
        }

        double currentOpacity = controls.Opacity;
        double currentX = translation.X;
        double currentOverlayOpacity = overlay.Opacity;
        this._floatingAnimation?.Stop();
        controls.Opacity = currentOpacity;
        translation.X = currentX;
        overlay.Opacity = currentOverlayOpacity;
        controls.IsHitTestVisible = show;

        double targetOpacity = show ? 1d : 0d;
        double targetX = show ? 0d : 6d;
        var animation = new Storyboard();
        AddFloatingAnimation(animation, controls, "Opacity", currentOpacity, targetOpacity);
        AddFloatingAnimation(animation, translation, "X", currentX, targetX);
        AddFloatingAnimation(animation, overlay, "Opacity", currentOverlayOpacity, targetOpacity);
        animation.Completed += (_, _) =>
        {
            controls.Opacity = targetOpacity;
            translation.X = targetX;
            overlay.Opacity = targetOpacity;
        };
        this._floatingAnimation = animation;
        animation.Begin();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimerForeground)));

    private async void OpenPowerPointFileButton_Click(object sender, RoutedEventArgs args) =>
        await this.SelectAndOpenPowerPointAsync();

    private async void OpenPowerPointFileMenuItem_Click(object sender, RoutedEventArgs args) =>
        await this.SelectAndOpenPowerPointAsync();

    private async Task SelectAndOpenPowerPointAsync()
    {
        if (this._isPresentationPickerOpen ||
            !this.ViewModel.CanOpenPresentation ||
            ((App)Application.Current).MainWindow is not MainWindow window)
        {
            return;
        }

        this._isPresentationPickerOpen = true;
        try
        {
            var picker = new FileOpenPicker(window.AppWindow.Id);
            picker.FileTypeFilter.Add(".ppt");
            picker.FileTypeFilter.Add(".pptx");
            picker.FileTypeFilter.Add(".pptm");
            picker.FileTypeFilter.Add(".pps");
            picker.FileTypeFilter.Add(".ppsx");
            PickFileResult? selection = await picker.PickSingleFileAsync();
            if (selection is not null)
            {
                await this.ViewModel.OpenPresentationAsync(selection.Path);
            }
        }
        finally
        {
            this._isPresentationPickerOpen = false;
        }
    }

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

        if (args.PropertyName == nameof(MainViewModel.TimerProgressValue))
        {
            this.UpdateArcProgressDashArrays();
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
    }

    private void EnterPresentationHudMode()
    {
        this.SetShellMode(DesktopShellMode.PresentationHud);
        this._windowController.EnterPresentationHud();
    }

    private void SetShellMode(DesktopShellMode value)
    {
        if (this._shellMode == value)
        {
            return;
        }

        this._floatingHideTimer.Stop();
        this._floatingAnimation?.Stop();
        this._isFloatingPointerOver = false;
        this._isFloatingMenuOpen = false;
        this._shellMode = value;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsCompactMode)));
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsPresentationHudMode)));
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsExpandedMode)));
    }

    private void UpdateArcProgressDashArrays()
    {
        double ratio = this.ViewModel.TimerProgressValue / 100d;
        this.ExpandedArcProgressDashOffset = (1d - Math.Clamp(ratio, 0d, 1d)) * ExpandedArcDashLength;
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.CompactProgressWidth)));
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ExpandedArcProgressDashOffset)));
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
