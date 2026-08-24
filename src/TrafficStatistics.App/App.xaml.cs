using TrafficStatistics.App.Services;
using TrafficStatistics.Core.Services;
using TrafficStatistics.Data;
using TrafficStatistics.Data.Repositories;
using TrafficStatistics.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TrafficStatistics.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private DispatcherTimer? _dailyAggregationTimer;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Localization Service
                services.AddSingleton<LocalizationService>();

                // Core Services
                services.AddSingleton<ProcessInfoService>();
                services.AddSingleton<IAggregationService, AggregationService>();
                services.AddSingleton<ITrafficCaptureService, EtwTrafficCaptureService>();

                // Data Layer
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "traffic.db");
                services.AddDbContext<TrafficDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Scoped);
                
                services.AddScoped<ITrafficRepository, TrafficRepository>();

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<RealtimeViewModel>();
                services.AddTransient<StatisticsViewModel>();
                services.AddTransient<SettingsViewModel>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        // 0. Initialize language from saved settings
        var localizationService = _host.Services.GetRequiredService<LocalizationService>();
        var savedLanguage = LoadSavedLanguage();
        localizationService.ApplyLanguage(savedLanguage);

        // 1. Ensure database is created and migrated
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrafficDbContext>();
            db.Database.EnsureCreated();
        }

        // 2. Load services
        var captureService = _host.Services.GetRequiredService<ITrafficCaptureService>();
        var aggregationService = _host.Services.GetRequiredService<IAggregationService>();
        var processInfoService = _host.Services.GetRequiredService<ProcessInfoService>();

        // 3. Connect capture to aggregation
        captureService.OnTrafficEvent += (pid, bytes, isSend) =>
        {
            aggregationService.RecordTraffic(pid, bytes, isSend);
        };

        // 4. Connect aggregation to persistence (minutes flush)
        aggregationService.OnMinuteFlush += (flushData) =>
        {
            Task.Run(async () =>
            {
                try
                {
                    var timestamp = Core.Helpers.TimeRangeHelper.AlignToMinute(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    var recordsToSave = new List<(int processId, long timestamp, long bytesSent, long bytesRecv)>();

                    using var scope = _host.Services.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();

                    foreach (var kvp in flushData)
                    {
                        var pid = kvp.Key;
                        var (sent, recv) = kvp.Value;

                        var processInfo = processInfoService.GetProcessInfo(pid);
                        var name = processInfo?.Name ?? $"PID {pid}";
                        var path = processInfo?.Path;

                        var procEntity = await repo.GetOrCreateProcessAsync(name, path);
                        recordsToSave.Add((procEntity.Id, timestamp, sent, recv));
                    }

                    if (recordsToSave.Count > 0)
                    {
                        await repo.SaveTrafficRecordsAsync(recordsToSave);
                    }
                }
                catch
                {
                    // Ignore background database save error
                }
            });
        };

        // 5. Start capture session
        try
        {
            await captureService.StartAsync(default);
        }
        catch (Exception ex)
        {
            var errorMsg = string.Format(localizationService.GetString("Msg_CaptureStartError", "Failed to start network traffic capture service: {0}\nPlease ensure this application is run with Administrator privileges!"), ex.Message);
            var errorTitle = localizationService.GetString("Msg_Error", "Error");
            MessageBox.Show(errorMsg, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // 6. Startup DB Cleanup (Purge old records)
        _ = RunStartupCleanupAsync();

        // 7. Setup periodic daily aggregation
        _dailyAggregationTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _dailyAggregationTimer.Tick += async (s, ev) => await RunDailyAggregationAsync();
        _dailyAggregationTimer.Start();

        // 8. Show main window
        var mainWindow = new MainWindow(_host.Services.GetRequiredService<MainViewModel>());
        mainWindow.Show();
    }

    private static string LoadSavedLanguage()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettingsData>(json);
                if (!string.IsNullOrEmpty(settings?.Language))
                {
                    return settings.Language;
                }
            }
        }
        catch
        {
            // Ignore error
        }
        return "en-US";
    }

    private async Task RunStartupCleanupAsync()
    {
        try
        {
            int minuteRetention = 7;
            int dailyRetention = 365;

            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettingsData>(json);
                if (settings != null)
                {
                    minuteRetention = settings.MinuteDataRetentionDays;
                    dailyRetention = settings.DailyDataRetentionDays;
                }
            }

            using var scope = _host.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
            await repo.PurgeOldDataAsync(minuteRetention, dailyRetention);
        }
        catch
        {
            // Ignore startup cleanup error
        }
    }

    private async Task RunDailyAggregationAsync()
    {
        try
        {
            using var scope = _host.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrafficRepository>();
            
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await repo.AggregateDailySummaryAsync(today);

            var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            await repo.AggregateDailySummaryAsync(yesterday);
        }
        catch
        {
            // Ignore background aggregation error
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _dailyAggregationTimer?.Stop();

        var captureService = _host.Services.GetService<ITrafficCaptureService>();
        if (captureService != null)
        {
            await captureService.StopAsync(default);
        }

        using (_host)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }

        base.OnExit(e);
    }
}
