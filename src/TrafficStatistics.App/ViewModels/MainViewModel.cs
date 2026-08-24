using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrafficStatistics.Core.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace TrafficStatistics.App.ViewModels;

/// <summary>
/// Main view model that handles navigation, global speeds, and theme management.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAggregationService _aggregationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _speedTimer;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private int _selectedNavIndex;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _totalUploadSpeed = "0 B/s";

    [ObservableProperty]
    private string _totalDownloadSpeed = "0 B/s";

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel(IAggregationService aggregationService, IServiceProvider serviceProvider)
    {
        _aggregationService = aggregationService;
        _serviceProvider = serviceProvider;

        // Default to dark theme
        ApplyTheme(true);

        // Start on RealtimeView
        SelectedNavIndex = 0;
        NavigateToRealtime();

        _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _speedTimer.Tick += (s, e) => UpdateTotalSpeeds();
        _speedTimer.Start();
    }

    private void UpdateTotalSpeeds()
    {
        TotalUploadSpeed = Core.Helpers.ByteFormatter.FormatSpeed(_aggregationService.TotalUploadSpeed);
        TotalDownloadSpeed = Core.Helpers.ByteFormatter.FormatSpeed(_aggregationService.TotalDownloadSpeed);
    }

    [RelayCommand]
    private void NavigateToRealtime()
    {
        CurrentView = (ObservableObject)_serviceProvider.GetService(typeof(RealtimeViewModel))!;
        SelectedNavIndex = 0;
    }

    [RelayCommand]
    private void NavigateToStatistics()
    {
        CurrentView = (ObservableObject)_serviceProvider.GetService(typeof(StatisticsViewModel))!;
        SelectedNavIndex = 1;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = (ObservableObject)_serviceProvider.GetService(typeof(SettingsViewModel))!;
        SelectedNavIndex = 2;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme(IsDarkTheme);
    }

    /// <summary>
    /// Applies the specified theme to the application.
    /// </summary>
    public static void ApplyTheme(bool isDark)
    {
        var app = Application.Current;
        if (app == null) return;

        var themePath = isDark
            ? "pack://application:,,,/TrafficStatistics.App;component/Resources/Themes/DarkTheme.xaml"
            : "pack://application:,,,/TrafficStatistics.App;component/Resources/Themes/LightTheme.xaml";

        var themeUri = new Uri(themePath, UriKind.Absolute);

        var existingTheme = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme.xaml") || d.Source.OriginalString.Contains("LightTheme.xaml")));

        if (existingTheme != null)
        {
            app.Resources.MergedDictionaries.Remove(existingTheme);
        }

        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
    }
}
