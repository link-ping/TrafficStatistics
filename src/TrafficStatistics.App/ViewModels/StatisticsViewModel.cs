using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficStatistics.Core.Models;
using TrafficStatistics.Core.Helpers;
using TrafficStatistics.Data.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrafficStatistics.App.ViewModels;

/// <summary>
/// Row item representing a ranked process in the historical statistics tab.
/// </summary>
public class ProcessTrafficRankItem : ObservableObject
{
    private ImageSource? _icon;
    private bool _iconLoaded;

    public int Rank { get; init; }
    public int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ProcessPath { get; init; }
    public long TotalSent { get; init; }
    public long TotalRecv { get; init; }
    public long TotalTraffic => TotalSent + TotalRecv;

    public string TotalSentText => ByteFormatter.FormatBytes(TotalSent);
    public string TotalRecvText => ByteFormatter.FormatBytes(TotalRecv);
    public string TotalTrafficText => ByteFormatter.FormatBytes(TotalTraffic);

    public ImageSource? Icon
    {
        get
        {
            if (!_iconLoaded)
            {
                _iconLoaded = true;
                _icon = LoadIcon();
            }
            return _icon;
        }
    }

    private ImageSource? LoadIcon()
    {
        if (string.IsNullOrEmpty(ProcessPath) || !File.Exists(ProcessPath))
        {
            return null;
        }

        try
        {
            using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(ProcessPath);
            if (sysIcon != null)
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    sysIcon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                imageSource.Freeze();
                return imageSource;
            }
        }
        catch
        {
            // Ignore icon extraction failures
        }
        return null;
    }
}

/// <summary>
/// View model for the historical statistics tab.
/// </summary>
public partial class StatisticsViewModel : ObservableObject
{
    private readonly ITrafficRepository _trafficRepository;
    private readonly ObservableCollection<double> _historicalUpload = new();
    private readonly ObservableCollection<double> _historicalDownload = new();
    private readonly ObservableCollection<string> _chartLabels = new();
    private List<ProcessTrafficRankItem> _loadedProcesses = new();

    [ObservableProperty]
    private string _selectedPeriod = "日"; // Default is Day

    [ObservableProperty]
    private DateTime _customStartDate = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime _customEndDate = DateTime.Today;

    [ObservableProperty]
    private bool _isCustomPeriodActive;

    [ObservableProperty]
    private ObservableCollection<ProcessTrafficRankItem> _topProcesses = new();

    [ObservableProperty]
    private string _totalPeriodTraffic = "0 B";

    [ObservableProperty]
    private string _totalPeriodSent = "0 B";

    [ObservableProperty]
    private string _totalPeriodRecv = "0 B";

    public ObservableCollection<ISeries> TrafficChartSeries { get; set; }

    public ObservableCollection<Axis> XAxes { get; set; }

    public ObservableCollection<Axis> YAxes { get; set; }

    public List<string> PeriodOptions => ["小时", "日", "周", "月", "自定义"];

    public string SortColumn { get; set; } = "TotalTraffic";
    public bool SortDescending { get; set; } = true;

    public StatisticsViewModel(ITrafficRepository trafficRepository)
    {
        _trafficRepository = trafficRepository;

        TrafficChartSeries = [
            new LineSeries<double>
            {
                Values = _historicalUpload,
                Name = "上传流量",
                Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(30)),
                Stroke = new SolidColorPaint(SKColors.Orange, 2),
                GeometrySize = 5,
                LineSmoothness = 0.4
            },
            new LineSeries<double>
            {
                Values = _historicalDownload,
                Name = "下载流量",
                Fill = new SolidColorPaint(SKColors.SeaGreen.WithAlpha(30)),
                Stroke = new SolidColorPaint(SKColors.SeaGreen, 2),
                GeometrySize = 5,
                LineSmoothness = 0.4
            }
        ];

        XAxes = [
            new Axis
            {
                Labels = _chartLabels,
                LabelsRotation = 15,
                LabelsPaint = new SolidColorPaint(SKColors.Gray)
            }
        ];

        YAxes = [
            new Axis
            {
                Labeler = value => ByteFormatter.FormatBytes((long)value),
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColors.Gray)
            }
        ];

        // Trigger initial data load
        _ = LoadDataAsync();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        IsCustomPeriodActive = value == "自定义";
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        long startTimestamp;
        long endTimestamp;
        var now = DateTime.UtcNow;

        switch (SelectedPeriod)
        {
            case "小时":
                // Last 24 hours
                var startHour = now.AddHours(-24);
                startTimestamp = new DateTimeOffset(startHour).ToUnixTimeSeconds();
                endTimestamp = new DateTimeOffset(now).ToUnixTimeSeconds();
                await LoadHourlyDataAsync(startHour, now);
                break;

            case "日":
                // Last 30 days
                var startDay = DateTime.Today.AddDays(-30);
                var endDay = DateTime.Today;
                startTimestamp = new DateTimeOffset(startDay).ToUnixTimeSeconds();
                endTimestamp = new DateTimeOffset(endDay.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
                await LoadDailyDataAsync(startDay, endDay);
                break;

            case "周":
                // Last 12 weeks
                var startWeek = DateTime.Today.AddDays(-84); // 12 * 7
                var endWeek = DateTime.Today;
                startTimestamp = new DateTimeOffset(startWeek).ToUnixTimeSeconds();
                endTimestamp = new DateTimeOffset(endWeek.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
                await LoadWeeklyDataAsync(startWeek, endWeek);
                break;

            case "月":
                // Last 12 months
                var startMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
                var endMonth = DateTime.Today;
                startTimestamp = new DateTimeOffset(startMonth).ToUnixTimeSeconds();
                endTimestamp = new DateTimeOffset(endMonth.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
                await LoadMonthlyDataAsync(startMonth, endMonth);
                break;

            case "自定义":
            default:
                startTimestamp = new DateTimeOffset(CustomStartDate.Date).ToUnixTimeSeconds();
                endTimestamp = new DateTimeOffset(CustomEndDate.Date.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
                await LoadCustomRangeDataAsync(CustomStartDate.Date, CustomEndDate.Date);
                break;
        }

        await LoadTopProcessesAsync(startTimestamp, endTimestamp);
    }

    private async Task LoadHourlyDataAsync(DateTime start, DateTime end)
    {
        long startTs = new DateTimeOffset(start).ToUnixTimeSeconds();
        long endTs = new DateTimeOffset(end).ToUnixTimeSeconds();

        var records = await _trafficRepository.GetTrafficAsync(null, startTs, endTs);

        // Group records by hour in local time
        var hourlyData = new Dictionary<string, (long upload, long download)>();
        for (var dt = start.ToLocalTime(); dt <= end.ToLocalTime(); dt = dt.AddHours(1))
        {
            var label = dt.ToString("HH:00");
            hourlyData[label] = (0, 0);
        }

        foreach (var r in records)
        {
            var dtLocal = DateTimeOffset.FromUnixTimeSeconds(r.Timestamp).LocalDateTime;
            var label = dtLocal.ToString("HH:00");
            if (hourlyData.TryGetValue(label, out var cur))
            {
                hourlyData[label] = (cur.upload + r.BytesSent, cur.download + r.BytesRecv);
            }
        }

        UpdateChart(hourlyData);
    }

    private async Task LoadDailyDataAsync(DateTime start, DateTime end)
    {
        var summaries = await _trafficRepository.GetDailySummariesAsync(null, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

        var dailyData = new Dictionary<string, (long upload, long download)>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            dailyData[d.ToString("MM-dd")] = (0, 0);
        }

        foreach (var s in summaries)
        {
            if (DateTime.TryParseExact(s.Date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d))
            {
                var label = d.ToString("MM-dd");
                if (dailyData.TryGetValue(label, out var cur))
                {
                    dailyData[label] = (cur.upload + s.TotalSent, cur.download + s.TotalRecv);
                }
            }
        }

        UpdateChart(dailyData);
    }

    private async Task LoadWeeklyDataAsync(DateTime start, DateTime end)
    {
        var summaries = await _trafficRepository.GetDailySummariesAsync(null, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

        var weeklyData = new Dictionary<string, (long upload, long download)>();
        // Group by ISO weeks or simplified 7-day windows
        for (var d = start; d <= end; d = d.AddDays(7))
        {
            weeklyData[d.ToString("yy-W" + (d.DayOfYear / 7 + 1))] = (0, 0);
        }

        foreach (var s in summaries)
        {
            if (DateTime.TryParseExact(s.Date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d))
            {
                var label = d.ToString("yy-W" + (d.DayOfYear / 7 + 1));
                if (weeklyData.TryGetValue(label, out var cur))
                {
                    weeklyData[label] = (cur.upload + s.TotalSent, cur.download + s.TotalRecv);
                }
            }
        }

        UpdateChart(weeklyData);
    }

    private async Task LoadMonthlyDataAsync(DateTime start, DateTime end)
    {
        var summaries = await _trafficRepository.GetDailySummariesAsync(null, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

        var monthlyData = new Dictionary<string, (long upload, long download)>();
        for (var d = start; d <= end; d = d.AddMonths(1))
        {
            monthlyData[d.ToString("yyyy-MM")] = (0, 0);
        }

        foreach (var s in summaries)
        {
            if (DateTime.TryParseExact(s.Date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d))
            {
                var label = d.ToString("yyyy-MM");
                if (monthlyData.TryGetValue(label, out var cur))
                {
                    monthlyData[label] = (cur.upload + s.TotalSent, cur.download + s.TotalRecv);
                }
            }
        }

        UpdateChart(monthlyData);
    }

    private async Task LoadCustomRangeDataAsync(DateTime start, DateTime end)
    {
        long days = (end - start).Ticks / TimeSpan.TicksPerDay;

        if (days <= 3)
        {
            // Use minute records grouped by hour for high resolution
            long startTs = new DateTimeOffset(start).ToUnixTimeSeconds();
            long endTs = new DateTimeOffset(end.AddDays(1).AddTicks(-1)).ToUnixTimeSeconds();
            var records = await _trafficRepository.GetTrafficAsync(null, startTs, endTs);

            var hourlyData = new Dictionary<string, (long upload, long download)>();
            for (var dt = start; dt <= end.AddDays(1); dt = dt.AddHours(2))
            {
                hourlyData[dt.ToString("MM-dd HH:00")] = (0, 0);
            }

            foreach (var r in records)
            {
                var dtLocal = DateTimeOffset.FromUnixTimeSeconds(r.Timestamp).LocalDateTime;
                // Round to 2-hour window
                var roundedHour = (dtLocal.Hour / 2) * 2;
                var label = new DateTime(dtLocal.Year, dtLocal.Month, dtLocal.Day, roundedHour, 0, 0).ToString("MM-dd HH:00");
                if (hourlyData.TryGetValue(label, out var cur))
                {
                    hourlyData[label] = (cur.upload + r.BytesSent, cur.download + r.BytesRecv);
                }
            }

            UpdateChart(hourlyData);
        }
        else
        {
            // Use daily summaries
            await LoadDailyDataAsync(start, end);
        }
    }

    private void UpdateChart(IDictionary<string, (long upload, long download)> chartPoints)
    {
        _historicalUpload.Clear();
        _historicalDownload.Clear();
        _chartLabels.Clear();

        long totalUpload = 0;
        long totalDownload = 0;

        foreach (var kvp in chartPoints)
        {
            _chartLabels.Add(kvp.Key);
            _historicalUpload.Add(kvp.Value.upload);
            _historicalDownload.Add(kvp.Value.download);

            totalUpload += kvp.Value.upload;
            totalDownload += kvp.Value.download;
        }

        TotalPeriodSent = ByteFormatter.FormatBytes(totalUpload);
        TotalPeriodRecv = ByteFormatter.FormatBytes(totalDownload);
        TotalPeriodTraffic = ByteFormatter.FormatBytes(totalUpload + totalDownload);
    }

    private async Task LoadTopProcessesAsync(long startTs, long endTs)
    {
        var topProcs = await _trafficRepository.GetTopProcessesAsync(startTs, endTs, 20);
        var dbProcesses = await _trafficRepository.GetAllProcessesAsync();
        var procMap = dbProcesses.ToDictionary(p => p.Id, p => p);

        var list = new List<ProcessTrafficRankItem>();
        int rank = 1;

        foreach (var kvp in topProcs)
        {
            if (procMap.TryGetValue(kvp.Key, out var proc))
            {
                list.Add(new ProcessTrafficRankItem
                {
                    Rank = rank++,
                    ProcessId = proc.Id,
                    ProcessName = proc.Name,
                    ProcessPath = proc.Path,
                    TotalSent = kvp.Value.totalSent,
                    TotalRecv = kvp.Value.totalRecv
                });
            }
        }

        _loadedProcesses = list;
        ApplySort();
    }

    public void SetSort(string columnName)
    {
        if (SortColumn == columnName)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = columnName;
            SortDescending = true;
        }
        ApplySort();
    }

    private void ApplySort()
    {
        if (_loadedProcesses == null || _loadedProcesses.Count == 0)
        {
            TopProcesses = new ObservableCollection<ProcessTrafficRankItem>();
            return;
        }

        IEnumerable<ProcessTrafficRankItem> sorted;
        switch (SortColumn)
        {
            case "Rank":
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.Rank) 
                    : _loadedProcesses.OrderBy(p => p.Rank);
                break;
            case "ProcessName":
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.ProcessName) 
                    : _loadedProcesses.OrderBy(p => p.ProcessName);
                break;
            case "ProcessId":
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.ProcessId) 
                    : _loadedProcesses.OrderBy(p => p.ProcessId);
                break;
            case "TotalSent":
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.TotalSent) 
                    : _loadedProcesses.OrderBy(p => p.TotalSent);
                break;
            case "TotalRecv":
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.TotalRecv) 
                    : _loadedProcesses.OrderBy(p => p.TotalRecv);
                break;
            case "TotalTraffic":
            default:
                sorted = SortDescending 
                    ? _loadedProcesses.OrderByDescending(p => p.TotalTraffic) 
                    : _loadedProcesses.OrderBy(p => p.TotalTraffic);
                break;
        }

        TopProcesses = new ObservableCollection<ProcessTrafficRankItem>(sorted);
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"流量统计_{SelectedPeriod}_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
                await writer.WriteLineAsync("排名,进程名称,可执行路径,上传流量(Bytes),下载流量(Bytes),总流量(Bytes)");

                foreach (var item in TopProcesses)
                {
                    var pathEscaped = string.IsNullOrEmpty(item.ProcessPath) ? "" : $"\"{item.ProcessPath.Replace("\"", "\"\"")}\"";
                    await writer.WriteLineAsync($"{item.Rank},{item.ProcessName},{pathEscaped},{item.TotalSent},{item.TotalRecv},{item.TotalTraffic}");
                }

                MessageBox.Show("数据导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
