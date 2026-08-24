using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficStatistics.Data.Repositories;
using Microsoft.Win32;
using System;
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
    public int MinuteDataRetentionDays { get; set; } = 7;
    public int DailyDataRetentionDays { get; set; } = 365;
}

/// <summary>
/// View model for the settings tab.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ITrafficRepository _trafficRepository;
    private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private const string RegistryKeyName = "TrafficStatisticsTool";

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private int _minuteDataRetentionDays = 7;

    [ObservableProperty]
    private int _dailyDataRetentionDays = 365;

    public SettingsViewModel(ITrafficRepository trafficRepository)
    {
        _trafficRepository = trafficRepository;
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

            MessageBox.Show("设置保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PurgeDataAsync()
    {
        if (MessageBox.Show("确定要清空数据库中的所有历史流量记录吗？此操作无法撤销。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                await _trafficRepository.PurgeOldDataAsync(0, 0);
                MessageBox.Show("历史数据已成功清空！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清空数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
