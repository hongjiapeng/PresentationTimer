using System.Net.NetworkInformation;

namespace PresentationTimer.Remote.Networking;

/// <summary>Provides a controllable notification source for local address changes.</summary>
internal interface INetworkChangeNotifier
{
    /// <summary>Occurs when the set of local network addresses may have changed.</summary>
    event Action? Changed;

    /// <summary>Starts observing system network changes.</summary>
    void Start();

    /// <summary>Stops observing system network changes.</summary>
    void Stop();
}

internal sealed class NetworkChangeNotifier : INetworkChangeNotifier
{
    private bool _started;

    public event Action? Changed;

    public void Start()
    {
        if (this._started)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged += this.OnNetworkAddressChanged;
        this._started = true;
    }

    public void Stop()
    {
        if (!this._started)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged -= this.OnNetworkAddressChanged;
        this._started = false;
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs args) => this.Changed?.Invoke();
}
