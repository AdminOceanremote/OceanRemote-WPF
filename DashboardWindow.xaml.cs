using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OceanMonitor2.Services;
using SkiaSharp;

namespace OceanMonitor2
{
    public partial class DashboardWindow : UserControl
    {
        private readonly ApiService _api;
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _clock;
        private bool _noNetShown = false;

        public ObservableCollection<DeviceCard> Devices { get; set; } = new();
        public ObservableCollection<LogEntry> SystemLog { get; set; } = new();
        private readonly Dictionary<string, Microsoft.Web.WebView2.Wpf.WebView2> _gauges = new();

        public DashboardWindow(ApiService api)
        {
            InitializeComponent();
            _api = api;
            UserLabel.Text = $"OPERATOR: {_api.Username?.ToUpper() ?? "USER"}";
            DevicesList.ItemsSource = Devices;
            SystemLogList.ItemsSource = SystemLog;

            AddLog("INFO", "SCADA Console Initialized. Cold room matrix active.");
            AddLog("INFO", "Connecting to telemetry endpoint...");

            ApplyTranslations();

            Loaded += async (_, __) => await LoadDevices();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timer.Tick += async (_, __) => await LoadDevices();
            _timer.Start();

            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (_, __) => ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            _clock.Start();
        }

        private void ApplyTranslations()
        {
            TitleText.Text = AppSettings.T("dash.title") ?? "OCEANREMOTE MONITOR";
            LblTotal.Text = AppSettings.T("dash.total") ?? "ASSIGNED NODES";
            LblOnline.Text = AppSettings.T("dash.online") ?? "ONLINE LINK";
            LblOffline.Text = AppSettings.T("dash.offline") ?? "OFFLINE ISOLATED";
            LblLastUpdate.Text = AppSettings.T("dash.lastupdate") ?? "TELEMETRY CYCLE";
            BtnAlerts.Content = AppSettings.T("dash.alerts") ?? "ALERTS";
            BtnRefresh.Content = AppSettings.T("dash.refresh") ?? "POLL";
            BtnLogout.Content = AppSettings.T("nav.logout") ?? "LOGOUT";
        }

        private void AddLog(string level, string message)
        {
            var color = level switch
            {
                "ERR" => Color.FromRgb(239, 68, 68),
                "WARN" => Color.FromRgb(251, 191, 36),
                "OK" => Color.FromRgb(16, 185, 129),
                _ => Color.FromRgb(56, 189, 248)
            };

            SystemLog.Insert(0, new LogEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Message = message,
                Color = new SolidColorBrush(color)
            });

            while (SystemLog.Count > 50)
                SystemLog.RemoveAt(SystemLog.Count - 1);
        }

        private async void GaugeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Microsoft.Web.WebView2.Wpf.WebView2 wv) return;

            var udf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OceanRemoteMonitor", "WebView2");

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, udf);
            await wv.EnsureCoreWebView2Async(env);

            wv.CoreWebView2.Settings.AreDevToolsEnabled = false;
            wv.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            wv.CoreWebView2.Settings.IsZoomControlEnabled = false;

            var html = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gauge.html");
            wv.CoreWebView2.Navigate(new Uri(html).AbsoluteUri);

            wv.NavigationCompleted += (_, __) =>
            {
                if (wv.DataContext is DeviceCard card)
                {
                    _gauges[card.DeviceId] = wv;
                    PushGauge(card);
                }
            };
        }

        private async void PushGauge(DeviceCard card)
        {
            if (!_gauges.TryGetValue(card.DeviceId, out var wv)) return;
            if (wv.CoreWebView2 == null) return;

            var tempVal = card.TemperatureText == "ERR" ? "null" : card.TemperatureText;
            var minLimit = card.TargetMin.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            var maxLimit = card.TargetMax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            var js = $"window.setGauge({tempVal}, {minLimit}, {maxLimit});";

            try { await wv.CoreWebView2.ExecuteScriptAsync(js); }
            catch { }
        }

        private async Task LoadDevices()
        {
            try
            {
                var json = await _api.GetDevicesAsync();
                if (string.IsNullOrWhiteSpace(json)) throw new Exception("Empty response");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.GetProperty("devices");

                int online = 0, offline = 0, total = 0;
                var changed = new List<DeviceCard>();

                foreach (var d in root.EnumerateArray())
                {
                    total++;
                    var temp = d.TryGetProperty("temperature", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDouble() : (double?)null;

                    bool isOnline = d.GetProperty("status").GetString() == "online";
                    if (isOnline) online++; else offline++;

                    var id = d.GetProperty("device_id").GetString() ?? "";
                    var name = d.GetProperty("name").GetString() ?? "?";
                    var tempText = temp.HasValue ? $"{temp.Value:F2}" : "ERR";

                    var ts = d.TryGetProperty("timestamp", out var tst) && tst.ValueKind != JsonValueKind.Null
                        ? tst.GetString() ?? ""
                        : "";

                    double min = 0.0, max = 4.0;
                    if (name.Contains("Négative 1", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Negative 1", StringComparison.OrdinalIgnoreCase))
                    {
                        min = -25.0; max = -18.0;
                    }
                    else if (name.Contains("Négative 2", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Negative 2", StringComparison.OrdinalIgnoreCase))
                    {
                        min = -22.0; max = -15.0;
                    }
                    else if (name.Contains("Positive", StringComparison.OrdinalIgnoreCase))
                    {
                        min = 0.0; max = 4.0;
                    }
                    else if (name.Contains("Ingrédients", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Ingredients", StringComparison.OrdinalIgnoreCase))
                    {
                        min = 4.0; max = 8.0;
                    }

                    var existing = Devices.FirstOrDefault(x => x.DeviceId == id);

                    if (existing == null)
                    {
                        var newCard = new DeviceCard
                        {
                            DeviceId = id,
                            Name = name,
                            IdBadge = $"NODE_{id.ToUpper()}",
                            TemperatureText = tempText,
                            LastTimestamp = ts,
                            IsOnline = isOnline,
                            TargetMin = min,
                            TargetMax = max
                        };
                        newCard.RecordReading(temp);
                        Devices.Add(newCard);

                        _ = FetchLast3HoursHistoryAsync(newCard);
                    }
                    else
                    {
                        existing.IsOnline = isOnline;
                        if (existing.LastTimestamp != ts)
                        {
                            existing.LastTimestamp = ts;
                            existing.TemperatureText = tempText;
                            existing.RecordReading(temp);
                            changed.Add(existing);
                        }
                    }
                }

                var rand = new Random();
                foreach (var card in changed)
                {
                    var c = card;
                    var delay = rand.Next(80, 450);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delay);
                        Dispatcher.Invoke(() => PushGauge(c));
                    });
                }

                TotalLabel.Text = total.ToString();
                OnlineLabel.Text = online.ToString();
                OfflineLabel.Text = offline.ToString();
                LastUpdateLabel.Text = DateTime.Now.ToString("HH:mm:ss");

                double health = total > 0 ? ((double)online / total) * 100 : 0;
                SystemHealthBar.Value = health;
                HealthPercentLabel.Text = $"{health:F0}%";
                SystemHealthBar.Foreground = health >= 100
                    ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                    : new SolidColorBrush(Color.FromRgb(251, 191, 36));

                foreach (var card in changed)
                {
                    AddLog("OK", $"{card.Name}: {card.TemperatureText}°C ({card.StateBadgeText})");
                }

                _noNetShown = false;
                StatusLine.Text = "TELEMETRY MATRIX SYNCHRONIZED";
                StatusLine.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                TickerText.Text = $"LOG: {DateTime.Now:HH:mm:ss} - 3H Matrix Data Sync OK - Nodes [{online}/{total} Online].";
            }
            catch
            {
                TotalLabel.Text = "0";
                OnlineLabel.Text = "0";
                OfflineLabel.Text = "0";
                LastUpdateLabel.Text = "OFFLINE";
                SystemHealthBar.Value = 0;
                HealthPercentLabel.Text = "0%";
                StatusLine.Text = "CRITICAL: NETWORK DISCONNECTED";
                StatusLine.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));

                TickerText.Text = $"ALERT: {DateTime.Now:HH:mm:ss} - API connection failure. Local telemetry cache retention.";
                AddLog("ERR", "API unreachable. Polling retry queued.");

                if (!_noNetShown)
                {
                    _noNetShown = true;
                    MessageBox.Show("Please check your internet connection.", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task FetchLast3HoursHistoryAsync(DeviceCard card)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var historyJson = await _api.GetHistoryDayAsync(card.DeviceId, today);
                if (string.IsNullOrWhiteSpace(historyJson)) return;

                using var doc = JsonDocument.Parse(historyJson);
                if (!doc.RootElement.TryGetProperty("data", out var dataArr)) return;

                var thresholdTime = DateTime.Now.AddHours(-3);
                var readings = new List<double>();

                foreach (var item in dataArr.EnumerateArray())
                {
                    if (item.TryGetProperty("timestamp", out var tsProp) &&
                        DateTime.TryParse(tsProp.GetString(), out var dt) && dt >= thresholdTime)
                    {
                        if (item.TryGetProperty("temperature", out var tempProp) && tempProp.ValueKind == JsonValueKind.Number)
                        {
                            readings.Add(tempProp.GetDouble());
                        }
                    }
                }

                if (readings.Count > 0)
                {
                    Dispatcher.Invoke(() => card.InitializeHistory(readings));
                }
            }
            catch { }
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DeviceCard card)
            {
                if (Window.GetWindow(this) is MainWindow main)
                    main.PageHost.Content = new ChartWindow(_api, card.DeviceId, card.Name);
            }
        }

        private void Month_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DeviceCard card)
            {
                if (Window.GetWindow(this) is MainWindow main)
                    main.PageHost.Content = new MonthChartWindow(_api, card.DeviceId, card.Name);
            }
        }

        private void Report_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DeviceCard card)
            {
                if (Window.GetWindow(this) is MainWindow main)
                    main.PageHost.Content = new ReportWindow(_api, card.DeviceId, card.Name);
            }
        }

        private void Alerts_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.PageHost.Content = new AlertsWindow(_api);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadDevices();

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _clock.Stop();
            ApiService.ClearCredentials();
            new LoginWindow().Show();
            Window.GetWindow(this)?.Close();
        }
    }

    public class LogEntry
    {
        public string Time { get; set; } = "";
        public string Message { get; set; } = "";
        public Brush Color { get; set; } = Brushes.Gray;
    }

    public class DeviceCard : System.ComponentModel.INotifyPropertyChanged
    {
        private string _temperatureText = "--";
        private bool _isOnline = true;
        private string _lastTimestamp = "";
        private readonly ObservableCollection<double> _trendPoints = new();

        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string IdBadge { get; set; } = "";
        public double TargetMin { get; set; } = -20.0;
        public double TargetMax { get; set; } = 4.0;
        public string TargetRangeText => $"[TGT: {TargetMin:F0}°C to {TargetMax:F0}°C]";

        public string MinTempText { get; private set; } = "--";
        public string MaxTempText { get; private set; } = "--";
        public string MeanTempText { get; private set; } = "--";
        public string DriftText { get; private set; } = "+0.0°C";
        public Brush DriftColorBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        public ISeries[] SparklineSeries { get; set; }
        public Axis[] HiddenAxes { get; set; } = new Axis[] { new Axis { IsVisible = false } };

        public DeviceCard()
        {
            SparklineSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _trendPoints,
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(2, 132, 199, 130), new SKColor(3, 105, 161, 20), new SKColor(2, 6, 23, 0) },
                        new SKPoint(0.5f, 0),
                        new SKPoint(0.5f, 1)
                    ),
                    Stroke = new SolidColorPaint(new SKColor(56, 189, 248), 2),
                    GeometrySize = 3,
                    GeometryStroke = new SolidColorPaint(new SKColor(2, 6, 23), 1),
                    GeometryFill = new SolidColorPaint(new SKColor(56, 189, 248)),
                    LineSmoothness = 0.5
                }
            };
        }

        public string TemperatureText
        {
            get => _temperatureText;
            set
            {
                _temperatureText = value;
                OnChanged(nameof(TemperatureText));
                UpdateBadges();
            }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnChanged(nameof(IsOnline));
                UpdateBadges();
            }
        }

        public string LastTimestamp
        {
            get => _lastTimestamp;
            set
            {
                _lastTimestamp = value;
                OnChanged(nameof(LastTimestamp));
                OnChanged(nameof(FormattedLastSeen));
            }
        }

        public string FormattedLastSeen
        {
            get
            {
                if (DateTime.TryParse(LastTimestamp, out var dt))
                    return dt.ToString("HH:mm:ss");
                return !string.IsNullOrEmpty(LastTimestamp) ? LastTimestamp : "LIVE";
            }
        }

        public Brush CardBorderBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        public Brush StatusIndicatorBrush { get; private set; } = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        public Brush StateBadgeBg { get; private set; } = new SolidColorBrush(Color.FromRgb(6, 78, 59));
        public Brush StateBadgeFg { get; private set; } = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        public string StateBadgeText { get; private set; } = "NOMINAL";

        public void InitializeHistory(IEnumerable<double> readings)
        {
            _trendPoints.Clear();
            foreach (var r in readings) _trendPoints.Add(r);
            RecalculateAggregates();
        }

        public void RecordReading(double? val)
        {
            if (val.HasValue)
            {
                if (_trendPoints.Count >= 36)
                {
                    _trendPoints.RemoveAt(0);
                }
                _trendPoints.Add(val.Value);
                RecalculateAggregates();
            }
        }

        private void RecalculateAggregates()
        {
            if (_trendPoints.Count == 0) return;

            double min = _trendPoints.Min();
            double max = _trendPoints.Max();
            double avg = _trendPoints.Average();

            MinTempText = $"{min:F1}°C";
            MaxTempText = $"{max:F1}°C";
            MeanTempText = $"{avg:F1}°C";

            if (_trendPoints.Count > 1)
            {
                double drift = _trendPoints.Last() - _trendPoints.First();
                DriftText = $"{(drift >= 0 ? "+" : "")}{drift:F1}°C";

                if (drift > 1.0)
                    DriftColorBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                else if (drift < -1.0)
                    DriftColorBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                else
                    DriftColorBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }

            OnChanged(nameof(MinTempText));
            OnChanged(nameof(MaxTempText));
            OnChanged(nameof(MeanTempText));
            OnChanged(nameof(DriftText));
            OnChanged(nameof(DriftColorBrush));
        }

        private void UpdateBadges()
        {
            if (!IsOnline || TemperatureText == "ERR")
            {
                CardBorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                StatusIndicatorBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                StateBadgeBg = new SolidColorBrush(Color.FromRgb(69, 10, 10));
                StateBadgeFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                StateBadgeText = "OFFLINE";
            }
            else if (double.TryParse(TemperatureText, out var t))
            {
                if (t > TargetMax)
                {
                    CardBorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    StatusIndicatorBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    StateBadgeBg = new SolidColorBrush(Color.FromRgb(69, 10, 10));
                    StateBadgeFg = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                    StateBadgeText = "OVER TEMP";
                }
                else if (t < TargetMin)
                {
                    CardBorderBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                    StatusIndicatorBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    StateBadgeBg = new SolidColorBrush(Color.FromRgb(8, 47, 73));
                    StateBadgeFg = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    StateBadgeText = "UNDER TEMP";
                }
                else
                {
                    CardBorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                    StatusIndicatorBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    StateBadgeBg = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                    StateBadgeFg = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    StateBadgeText = "NOMINAL";
                }
            }

            OnChanged(nameof(CardBorderBrush));
            OnChanged(nameof(StatusIndicatorBrush));
            OnChanged(nameof(StateBadgeBg));
            OnChanged(nameof(StateBadgeFg));
            OnChanged(nameof(StateBadgeText));
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
    }
}