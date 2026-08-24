using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficStatistics.App.Services;
using TrafficStatistics.Data.Repositories;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace TrafficStatistics.App.ViewModels;

/// <summary>
/// App settings data structure for serialization.
/// </summary>
public class AppSettingsData
{
    public bool AutoStart { get; set; }
    public string Language { get; set; } = "zh-CN";
    public int MinuteDataRetentionDays { get; set; } = 7;
    public int DailyDataRetentionDays { get; set; } = 365;
}

/// <summary>
/// View model for the settings tab.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ITrafficRepository _trafficRepository;
    private readonly LocalizationService _localizationService;
    private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private const string RegistryKeyName = "TrafficStatisticsTool";

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private string _language = "zh-CN";

    [ObservableProperty]
    private int _minuteDataRetentionDays = 7;

    [ObservableProperty]
    private int _dailyDataRetentionDays = 365;

    public IReadOnlyList<LanguageItem> SupportedLanguages => _localizationService.SupportedLanguages;

    public SettingsViewModel(ITrafficRepository trafficRepository, LocalizationService localizationService)
    {
        _trafficRepository = trafficRepository;
        _localizationService = localizationService;
        _language = _localizationService.CurrentLanguage;
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettingsData>(json);
                if (settings != null)
                {
                    AutoStart = settings.AutoStart;
                    if (!string.IsNullOrEmpty(settings.Language))
                    {
                        Language = settings.Language;
                    }
                    MinuteDataRetentionDays = settings.MinuteDataRetentionDays;
                    DailyDataRetentionDays = settings.DailyDataRetentionDays;
                }
            }
        }
        catch
        {
            // Fallback to defaults
        }

        // Verify registry key match
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            var value = key?.GetValue(RegistryKeyName);
            AutoStart = value != null;
        }
        catch
        {
            // Ignore
        }
    }

    partial void OnLanguageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && _localizationService.CurrentLanguage != value)
        {
            _localizationService.ApplyLanguage(value);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            // Save registry startup
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null)
                {
                    if (AutoStart)
                    {
                        var exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(RegistryKeyName, $"\"{exePath}\" --startup");
                        }
                    }
                    else
                    {
                        key.DeleteValue(RegistryKeyName, false);
                    }
                }
            }

            // Save JSON config
            var settings = new AppSettingsData
            {
                AutoStart = AutoStart,
                Language = Language,
                MinuteDataRetentionDays = MinuteDataRetentionDays,
                DailyDataRetentionDays = DailyDataRetentionDays
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);

            // Trigger db retention cleanup
            Task.Run(async () =>
            {
                try
                {
                    await _trafficRepository.PurgeOldDataAsync(MinuteDataRetentionDays, DailyDataRetentionDays);
                }
                catch
                {
                    // Ignore background purge error
                }
            });

            MessageBox.Show(_localizationService.GetString("Msg_SaveSuccess"), 
                            _localizationService.GetString("Msg_Prompt"), 
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{_localizationService.GetString("Msg_SaveFailed")}{ex.Message}", 
                            _localizationService.GetString("Msg_Error"), 
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PurgeDataAsync()
    {
        var confirmMsg = _localizationService.GetString("Msg_PurgeConfirm");
        var warningTitle = _localizationService.GetString("Msg_Warning");
        if (MessageBox.Show(confirmMsg, warningTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                await _trafficRepository.PurgeOldDataAsync(0, 0);
                MessageBox.Show(_localizationService.GetString("Msg_PurgeSuccess"), 
                                _localizationService.GetString("Msg_Prompt"), 
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{_localizationService.GetString("Msg_PurgeFailed")}{ex.Message}", 
                                _localizationService.GetString("Msg_Error"), 
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
