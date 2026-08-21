using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PresentationTimer.App.Localization;
using PresentationTimer.App.ViewModels;
using PresentationTimer.Core.Contracts;

namespace PresentationTimer.App;

/// <summary>
/// Displays the timer and compact presenter subsystem status.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly WindowController _windowController;
    private bool _isAlwaysOnTop;

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
        this.Unloaded += this.OnUnloaded;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the presenter window remains above other windows.
    /// </summary>
    public bool IsAlwaysOnTop
    {
        get => this._isAlwaysOnTop;
        set
        {
            if (this._isAlwaysOnTop == value)
            {
                return;
            }

            this._isAlwaysOnTop = value;
            this._windowController.SetAlwaysOnTop(value);
        }
    }

    internal MainViewModel ViewModel { get; }

    internal void PrepareForShutdown()
    {
        this.Unloaded -= this.OnUnloaded;
        this.ViewModel.Dispose();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        this.PrepareForShutdown();
    }
}
