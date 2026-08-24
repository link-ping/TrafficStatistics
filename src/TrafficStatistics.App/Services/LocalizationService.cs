using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace TrafficStatistics.App.Services;

/// <summary>
/// Model representing a supported language option.
/// </summary>
public record LanguageItem(string CultureCode, string DisplayName);

/// <summary>
/// Service responsible for managing application localization and dynamic language switching.
/// </summary>
public class LocalizationService
{
    private const string ResourceUriFormat = "pack://application:,,,/TrafficStatistics.App;component/Resources/Languages/Strings.{0}.xaml";

    public static LocalizationService Instance { get; private set; } = null!;

    public event Action<string>? LanguageChanged;

    public string CurrentLanguage { get; private set; } = "en-US";

    public IReadOnlyList<LanguageItem> SupportedLanguages { get; } = new List<LanguageItem>
    {
        new("en-US", "English (English)"),
        new("zh-CN", "简体中文 (Chinese Simplified)")
    };

    public LocalizationService()
    {
        Instance = this;
    }

    /// <summary>
    /// Applies the specified culture/language to the application resources and current thread culture.
    /// </summary>
    /// <param name="cultureCode">Culture code, e.g., "en-US" or "zh-CN".</param>
    public void ApplyLanguage(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode) || !SupportedLanguages.Any(l => l.CultureCode.Equals(cultureCode, StringComparison.OrdinalIgnoreCase)))
        {
            cultureCode = "en-US";
        }

        CurrentLanguage = cultureCode;

        var app = Application.Current;
        if (app != null)
        {
            try
            {
                var uri = new Uri(string.Format(ResourceUriFormat, cultureCode), UriKind.Absolute);
                var newDict = new ResourceDictionary { Source = uri };

                // Find existing language dictionary
                var existingDict = app.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Resources/Languages/Strings."));

                if (existingDict != null)
                {
                    var index = app.Resources.MergedDictionaries.IndexOf(existingDict);
                    app.Resources.MergedDictionaries[index] = newDict;
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(newDict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load language dictionary for {cultureCode}: {ex.Message}");
            }
        }

        try
        {
            var culture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch
        {
            // Ignore culture setting error
        }

        LanguageChanged?.Invoke(cultureCode);
    }

    /// <summary>
    /// Retrieves a localized string by its key from the current application resources.
    /// </summary>
    /// <param name="key">Resource key name.</param>
    /// <param name="defaultValue">Default value if key not found.</param>
    /// <returns>Localized string value.</returns>
    public string GetString(string key, string defaultValue = "")
    {
        var app = Application.Current;
        if (app != null)
        {
            if (app.TryFindResource(key) is string str)
            {
                return str;
            }
        }
        return string.IsNullOrEmpty(defaultValue) ? key : defaultValue;
    }

    public string this[string key] => GetString(key);
}
