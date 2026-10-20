using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OceanMonitor2.Services;

namespace OceanMonitor2
{
    public partial class AlertsWindow : UserControl
    {
        private readonly ApiService _api;
        public ObservableCollection<AlertRow> Alerts { get; set; } = new();

        public AlertsWindow(ApiService api)
        {
            InitializeComponent();
            _api = api;

            AlertsList.ItemsSource = Alerts;
            DeviceFilter.Items.Add("All devices");

            ApplyTranslations();

            Loaded += async (_, __) =>
            {
                await LoadDeviceList();
                await LoadAlerts();
            };
        }

        private void ApplyTranslations()
        {
            TitleText.Text = AppSettings.T("alerts.title");
            LblDevice.Text = AppSettings.T("alerts.device");
            LblDate.Text = AppSettings.T("alerts.date");
            BtnApply.Content = AppSettings.T("alerts.apply");
            BtnRefresh.Content = AppSettings.T("dash.refresh");
            BtnMarkAll.Content = AppSettings.T("alerts.markall");
        }

        private async System.Threading.Tasks.Task LoadDeviceList()
        {
            try
            {
                var json = await _api.GetDevicesAsync();
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                foreach (var d in doc.RootElement.GetProperty("devices").EnumerateArray())
                {
                    var id = d.GetProperty("device_id").GetString();
                    var name = d.GetProperty("name").GetString();
                    if (!string.IsNullOrEmpty(id))
                        DeviceFilter.Items.Add($"{id}|{name}");
                }
                DeviceFilter.SelectedIndex = 0;
            }
            catch { }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadAlerts();

        private async System.Threading.Tasks.Task LoadAlerts()
        {
            try
            {
                string? deviceId = null;
                string? date = null;

                var sel = DeviceFilter.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(sel) && sel != "All devices" && sel.Contains("|"))
                    deviceId = sel.Split('|')[0];

                if (DateFilter.SelectedDate.HasValue)
                    date = DateFilter.SelectedDate.Value.ToString("yyyy-MM-dd");

                var json = await _api.GetNotificationsAsync(deviceId, date);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Alerts.Clear();

                if (!root.TryGetProperty("notifications", out var arr)) return;

                int count = 0;
                foreach (var n in arr.EnumerateArray())
                {
                    count++;
                    var isRead = n.GetProperty("is_read").GetBoolean();
                    var deviceName = n.GetProperty("device_name").GetString() ?? "SYS";
                    var message = n.GetProperty("message").GetString() ?? "";
                    var temp = n.TryGetProperty("temperature", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDouble() : (double?)null;
                    var thr = n.TryGetProperty("threshold", out var th) && th.ValueKind == JsonValueKind.Number
                        ? th.GetDouble() : (double?)null;

                    DateTime dt = DateTime.Parse(n.GetProperty("created_at").GetString()!);

                    Alerts.Add(new AlertRow
                    {
                        Id = n.GetProperty("id").GetInt32(),
                        Message = message,
                        DeviceName = deviceName,
                        TempThreshold = temp.HasValue && thr.HasValue
                            ? $"Current: {temp.Value:F1}°C / Limit: {thr.Value:F1}°C"
                            : "",
                        TimeText = dt.ToLocalTime().ToString("dd MMM HH:mm"),
                        BorderColor = isRead
                            ? new SolidColorBrush(Color.FromRgb(51, 65, 85))
                            : new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                        ReadButtonVisibility = isRead ? Visibility.Collapsed : Visibility.Visible
                    });
                }

                CountLabel.Text = $"{count} alert(s)";
            }
            catch { }
        }

        private async void MarkRead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AlertRow row)
            {
                try
                {
                    await _api.MarkNotificationReadAsync(row.Id);
                    row.ReadButtonVisibility = Visibility.Collapsed;
                    row.BorderColor = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                }
                catch { }
            }
        }

        private async void MarkAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _api.MarkAllNotificationsReadAsync();
                await LoadAlerts();
            }
            catch { }
        }
    }

    public class AlertRow
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string TempThreshold { get; set; } = "";
        public string TimeText { get; set; } = "";
        public Brush BorderColor { get; set; } = Brushes.Gray;
        public Visibility ReadButtonVisibility { get; set; } = Visibility.Visible;
    }
}