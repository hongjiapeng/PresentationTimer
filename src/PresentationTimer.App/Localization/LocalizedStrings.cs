using Microsoft.Windows.ApplicationModel.Resources;

namespace PresentationTimer.App.Localization;

internal sealed class LocalizedStrings
{
    private ResourceLoader _resourceLoader = new ResourceLoader();

    public string Get(string resourceName) => this._resourceLoader.GetString(resourceName);

    internal void Reload() => this._resourceLoader = new ResourceLoader();
}
