using CommunityToolkit.Mvvm.ComponentModel;
using TrafficStatistics.Core.Models;
using TrafficStatistics.Core.Services;
using TrafficStatistics.Core.Helpers;
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
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrafficStatistics.App.ViewModels;

/// <summary>
/// View model representing a process row in the real-time grid.
/// </summary>
public class ProcessTrafficItem : ObservableObject
{
    private long _uploadSpeed;
    private long _downloadSpeed;
    private long _totalSent;
    private long _totalRecv;
    private ImageSource? _icon;
    private bool _iconLoaded;

    public int Pid { get; init; }
    public required string ProcessName { get; init; }
    public string? ProcessPath { get; init; }

    public long UploadSpeed
    {
        get => _uploadSpeed;
        set
        {
            if (SetProperty(ref _uploadSpeed, value))
            {
                OnPropertyChanged(nameof(UploadSpeedText));
            }
        }
    }

    public long DownloadSpeed
    {
        get => _downloadSpeed;
        set
        {
            if (SetProperty(ref _downloadSpeed, value))
            {
                OnPropertyChanged(nameof(DownloadSpeedText));
            }
        }
    }

    public long TotalSent
    {
        get => _totalSent;
        set
        {
            if (SetProperty(ref _totalSent, value))
            {
                OnPropertyChanged(nameof(TotalSentText));
            }
        }
    }

    public long TotalRecv
    {
        get => _totalRecv;
        set
        {
            if (SetProperty(ref _totalRecv, value))
            {
                OnPropertyChanged(nameof(TotalRecvText));
            }
        }
    }

    public string UploadSpeedText => ByteFormatter.FormatSpeed(UploadSpeed);
    public string DownloadSpeedText => ByteFormatter.FormatSpeed(DownloadSpeed);
    public string TotalSentText => ByteFormatter.FormatBytes(TotalSent);
    public string TotalRecvText => ByteFormatter.FormatBytes(TotalRecv);

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
/// View model for real-time monitoring screen.
/// </summary>
public partial class RealtimeViewModel : ObservableObject
{
    private readonly IAggregationService _aggregationService;
    private readonly ObservableCollection<double> _uploadPoints = new();
    private readonly ObservableCollection<double> _downloadPoints = new();
    private readonly Dictionary<int, ProcessTrafficItem> _processCache = new();

    [ObservableProperty]
    private ObservableCollection<ProcessTrafficItem> _processes = new();

    [ObservableProperty]
    private ProcessTrafficItem? _selectedProcess;

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private string _activeProcessesCount = "0";

    public ObservableCollection<ISeries> SpeedChartSeries { get; set; }

    public Axis[] XAxes { get; set; } = [
        new Axis 
        { 
            LabelsPaint = null, 
            SeparatorsPaint = null 
        }
    ];

    public Axis[] YAxes { get; set; } = [
        new Axis
        {
            Labeler = value => ByteFormatter.FormatSpeed((long)value),
            MinLimit = 0,
            Name = "速率",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }
    ];

    public RealtimeViewModel(IAggregationService aggregationService)
    {
        _aggregationService = aggregationService;

        SpeedChartSeries = [
            new LineSeries<double>
            {
                Values = _uploadPoints,
                Name = "上传速率",
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.Orange, 2),
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.6
            },
            new LineSeries<double>
            {
                Values = _downloadPoints,
                Name = "下载速率",
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.SeaGreen, 2),
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.6
            }
        ];

        // Fill chart with 60 default values (0)
        for (int i = 0; i < 60; i++)
        {
            _uploadPoints.Add(0);
            _downloadPoints.Add(0);
        }

        _aggregationService.OnSnapshotsUpdated += OnSnapshotsUpdated;
    }

    private void OnSnapshotsUpdated()
    {
        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
        {
            var snapshots = _aggregationService.GetCurrentSnapshots();

            // Update chart data
            _uploadPoints.Add(_aggregationService.TotalUploadSpeed);
            if (_uploadPoints.Count > 60) _uploadPoints.RemoveAt(0);

            _downloadPoints.Add(_aggregationService.TotalDownloadSpeed);
            if (_downloadPoints.Count > 60) _downloadPoints.RemoveAt(0);

            // Update process list
            var currentPids = new HashSet<int>();
            var updatedItems = new List<ProcessTrafficItem>();

            foreach (var kvp in snapshots)
            {
                var snapshot = kvp.Value;
                currentPids.Add(snapshot.Pid);

                if (!_processCache.TryGetValue(snapshot.Pid, out var item))
                {
                    item = new ProcessTrafficItem
                    {
                        Pid = snapshot.Pid,
                        ProcessName = snapshot.ProcessName,
                        ProcessPath = snapshot.ProcessPath
                    };
                    _processCache[snapshot.Pid] = item;
                }

                item.UploadSpeed = snapshot.UploadSpeed;
                item.DownloadSpeed = snapshot.DownloadSpeed;
                item.TotalSent = snapshot.TotalSent;
                item.TotalRecv = snapshot.TotalRecv;

                updatedItems.Add(item);
            }

            // Remove exited processes from cache
            var exitedPids = _processCache.Keys.Where(pid => !currentPids.Contains(pid)).ToList();
            foreach (var pid in exitedPids)
            {
                _processCache.Remove(pid);
            }

            // Filter and sort items
            var filtered = updatedItems.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var query = FilterText.Trim();
                filtered = filtered.Where(p => 
                    p.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    p.Pid.ToString().Contains(query));
            }

            IOrderedEnumerable<ProcessTrafficItem> sorted;
            if (SortColumn == "ProcessName")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.ProcessName) 
                    : filtered.OrderBy(p => p.ProcessName);
            }
            else if (SortColumn == "Pid")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.Pid) 
                    : filtered.OrderBy(p => p.Pid);
            }
            else if (SortColumn == "UploadSpeed")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.UploadSpeed) 
                    : filtered.OrderBy(p => p.UploadSpeed);
            }
            else if (SortColumn == "DownloadSpeed")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.DownloadSpeed) 
                    : filtered.OrderBy(p => p.DownloadSpeed);
            }
            else if (SortColumn == "TotalSent")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.TotalSent) 
                    : filtered.OrderBy(p => p.TotalSent);
            }
            else if (SortColumn == "TotalRecv")
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.TotalRecv) 
                    : filtered.OrderBy(p => p.TotalRecv);
            }
            else
            {
                sorted = SortDescending 
                    ? filtered.OrderByDescending(p => p.UploadSpeed + p.DownloadSpeed) 
                    : filtered.OrderBy(p => p.UploadSpeed + p.DownloadSpeed);
            }

            var sortedList = sorted.ToList();
            Processes = new ObservableCollection<ProcessTrafficItem>(sortedList);
            ActiveProcessesCount = snapshots.Count.ToString();
        }));
    }

    public string SortColumn { get; set; } = "TotalTraffic";
    public bool SortDescending { get; set; } = true;

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
        OnSnapshotsUpdated();
    }

    partial void OnFilterTextChanged(string value)
    {
        // Force refresh list immediately on filter change
        OnSnapshotsUpdated();
    }
}
