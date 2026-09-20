using Microsoft.Windows.Globalization;

namespace PresentationTimer.App.Localization;

internal static class LanguageManager
{
    internal const string SimplifiedChinese = "zh-CN";
    internal const string English = "en-US";

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PresentationTimer");

    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "language.txt");

    internal static string CurrentLanguageTag
    {
        get
        {
            string primaryLanguageOverride = ApplicationLanguages.PrimaryLanguageOverride;
            if (IsSupported(primaryLanguageOverride))
            {
                return primaryLanguageOverride;
            }

            return ApplicationLanguages.Languages.Any(static language =>
                language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                ? SimplifiedChinese
                : English;
        }
    }

    internal static void ApplySavedLanguage()
    {
        string? savedLanguage = ReadSavedLanguage();
        if (savedLanguage is not null)
        {
            ApplicationLanguages.PrimaryLanguageOverride = savedLanguage;
        }
    }

    internal static bool SetLanguage(string languageTag)
    {
        if (!IsSupported(languageTag))
        {
            throw new ArgumentException("The requested language is not supported.", nameof(languageTag));
        }

        if (string.Equals(CurrentLanguageTag, languageTag, StringComparison.OrdinalIgnoreCase))
        {
            SaveLanguage(languageTag);
            return false;
        }

        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        SaveLanguage(languageTag);
        return true;
    }

    private static bool IsSupported(string? languageTag) =>
        string.Equals(languageTag, SimplifiedChinese, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(languageTag, English, StringComparison.OrdinalIgnoreCase);

    private static string? ReadSavedLanguage()
    {
        try
        {
            string savedLanguage = File.ReadAllText(SettingsFile).Trim();
            return IsSupported(savedLanguage) ? savedLanguage : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void SaveLanguage(string languageTag)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsFile, languageTag);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }
}
