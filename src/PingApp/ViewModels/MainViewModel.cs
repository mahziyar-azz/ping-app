using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingApp.Models;
using PingApp.Services;

namespace PingApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HistoryService _historyService;
        private readonly DispatcherTimer _pingTimer;
        private CancellationTokenSource? _geoIpCts;

        public HistoryViewModel HistoryVm { get; }

        public ObservableCollection<ProbeTarget> DomesticTargets { get; } = new();
        public ObservableCollection<ProbeTarget> TahrimTargets { get; } = new();
        public ObservableCollection<ProbeTarget> FilteredTargets { get; } = new();

        [ObservableProperty]
        private string _ip = "Checking...";

        [ObservableProperty]
        private string _country = "Checking...";

        [ObservableProperty]
        private string _continent = "Checking...";

        [ObservableProperty]
        private string _isp = "Checking...";

        [ObservableProperty]
        private Bitmap? _flagImage;

        [ObservableProperty]
        private bool _isGeoIpLoading;

        [ObservableProperty]
        private bool _useProxyOrHttp;

        [ObservableProperty]
        private bool _isTimerRunning;

        [ObservableProperty]
        private int _countdown = 6;

        [ObservableProperty]
        private bool _isHistoryOpen;

        [ObservableProperty]
        private string _timerButtonText = "Start Auto-Ping";

        [ObservableProperty]
        private string _statusNotification = string.Empty;

        public event Action? RequestShowHistory;

        public MainViewModel()
        {
            _historyService = new HistoryService();
            HistoryVm = new HistoryViewModel(_historyService);

            InitializeTargets();

            _pingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _pingTimer.Tick += OnPingTimerTick;
        }

        private void InitializeTargets()
        {
            // Domestic / NIN
            DomesticTargets.Add(new ProbeTarget("whatismyipaddress.com", "WhatIsMyIP", TargetCategory.Domestic));
            DomesticTargets.Add(new ProbeTarget("google.com", "Google", TargetCategory.Domestic));
            DomesticTargets.Add(new ProbeTarget("speedtest.net", "Speedtest", TargetCategory.Domestic));
            DomesticTargets.Add(new ProbeTarget("divar.ir", "Divar", TargetCategory.Domestic));
            DomesticTargets.Add(new ProbeTarget("yandex.com", "Yandex", TargetCategory.Domestic));
            DomesticTargets.Add(new ProbeTarget("aparat.com", "Aparat", TargetCategory.Domestic));

            // Sanctions / Tahrim
            TahrimTargets.Add(new ProbeTarget("openai.com", "OpenAI", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("claude.ai", "Claude AI", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("amd.com", "AMD", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("apps.microsoft.com", "MS Store", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("microsoft.com", "Microsoft", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("cloudflare.com", "Cloudflare", TargetCategory.Tahrim));
            TahrimTargets.Add(new ProbeTarget("adobe.com", "Adobe", TargetCategory.Tahrim));

            // Censored / Filtered
            FilteredTargets.Add(new ProbeTarget("4.4.4.4", "DNS 4.4.4.4", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("edition.cnn.com", "CNN", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("bbc.com", "BBC", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("youtube.com", "YouTube", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("reddit.com", "Reddit", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("instagram.com", "Instagram", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("telegram.org", "Telegram", TargetCategory.Filtered));
            FilteredTargets.Add(new ProbeTarget("pornhub.com", "Pornhub", TargetCategory.Filtered));
        }

        public async Task InitializeAsync()
        {
            await _historyService.LoadHistoryAsync();
            await CheckGeoIpAsync();
        }

        [RelayCommand]
        public async Task CheckGeoIpAsync()
        {
            _geoIpCts?.Cancel();
            _geoIpCts = new CancellationTokenSource();

            IsGeoIpLoading = true;
            Ip = "Resolving...";
            Country = "Resolving...";
            Continent = "Resolving...";
            Isp = "Resolving...";

            try
            {
                var geo = await NetworkProbeService.FetchGeoIpAsync();
                Ip = geo.Ip;
                Country = geo.Country;
                Continent = geo.Continent;
                Isp = geo.Isp;

                await _historyService.AddRecordAsync(geo.Ip, geo.Country);
                await HistoryVm.RefreshAsync();

                var flag = await NetworkProbeService.FetchFlagAsync(geo.CountryCode);
                FlagImage = flag;
            }
            catch (Exception ex)
            {
                Ip = "Unavailable";
                Country = "Unavailable";
                Continent = "Unavailable";
                Isp = "Unavailable";
                FlagImage = null;
                StatusNotification = $"GeoIP fetch failed: {ex.Message}";
            }
            finally
            {
                IsGeoIpLoading = false;
            }
        }

        private async Task ProbeTargetListAsync(ObservableCollection<ProbeTarget> targets)
        {
            bool useHttp = UseProxyOrHttp;
            var tasks = targets.Select(async target =>
            {
                target.SetProbing();
                var result = await NetworkProbeService.ProbeEndpointAsync(target.HostOrUrl, useHttp);
                target.ApplyResult(result);
            });

            await Task.WhenAll(tasks);
        }

        [RelayCommand]
        public async Task ProbeDomesticAsync()
        {
            await ProbeTargetListAsync(DomesticTargets);
        }

        [RelayCommand]
        public async Task ProbeTahrimAsync()
        {
            await ProbeTargetListAsync(TahrimTargets);
        }

        [RelayCommand]
        public async Task ProbeFilteredAsync()
        {
            await ProbeTargetListAsync(FilteredTargets);
        }

        [RelayCommand]
        public async Task ProbeAllAsync()
        {
            var t1 = ProbeDomesticAsync();
            var t2 = ProbeTahrimAsync();
            var t3 = ProbeFilteredAsync();
            await Task.WhenAll(t1, t2, t3);
        }

        [RelayCommand]
        public void ToggleTimer()
        {
            if (IsTimerRunning)
            {
                _pingTimer.Stop();
                IsTimerRunning = false;
                TimerButtonText = "Start Auto-Ping";
                Countdown = 6;
            }
            else
            {
                _ = ProbeAllAsync();
                _ = CheckGeoIpAsync();
                Countdown = 6;
                _pingTimer.Start();
                IsTimerRunning = true;
                TimerButtonText = "Stop Auto-Ping";
            }
        }

        private void OnPingTimerTick(object? sender, EventArgs e)
        {
            Countdown--;
            if (Countdown <= 0)
            {
                Countdown = 6;
                _ = ProbeAllAsync();
                _ = CheckGeoIpAsync();
            }
        }

        [RelayCommand]
        public void ToggleHistory()
        {
            IsHistoryOpen = !IsHistoryOpen;
            RequestShowHistory?.Invoke();
        }

        [RelayCommand]
        public void OpenProjectUrl()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/mahziyar-azz/ping-app",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
