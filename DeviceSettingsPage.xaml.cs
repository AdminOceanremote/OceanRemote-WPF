using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OceanMonitor2.Services;

namespace OceanMonitor2
{
    public partial class DeviceSettingsPage : UserControl
    {
        private ApiService _api;
        private System.Collections.Generic.List<DeviceItem> _devices = new();

        public DeviceSettingsPage(ApiService api)
        {
            InitializeComponent();
            _api = api;
            Loaded += async (_, __) => await LoadDevices();
        }

        private async System.Threading.Tasks.Task LoadDevices()
        {
            try
            {
                var json = await _api.GetDevicesAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.GetProperty("devices");
                _devices.Clear();
                CmbDevices.Items.Clear();

                foreach (var d in root.EnumerateArray())
                {
                    var id = d.GetProperty("device_id").GetString();
                    var name = d.GetProperty("name").GetString();
                    double offset = d.TryGetProperty("offset", out var off) ? off.GetDouble() : 0.0;
                    _devices.Add(new DeviceItem { Id = id, Name = name, Offset = offset });
                    CmbDevices.Items.Add($"{name} ({id})");
                }

                if (CmbDevices.Items.Count > 0)
                    CmbDevices.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Failed to load devices: {ex.Message}";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void CmbDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDevices.SelectedIndex < 0 || CmbDevices.SelectedIndex >= _devices.Count) return;

            EditorPanel.Visibility = Visibility.Visible;
            var dev = _devices[CmbDevices.SelectedIndex];
            TxtName.Text = dev.Name;
            TxtOffset.Text = dev.Offset.ToString("0.0");
            TxtStatus.Text = "";
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbDevices.SelectedIndex < 0) return;
            var dev = _devices[CmbDevices.SelectedIndex];

            string name = TxtName.Text.Trim();
            double offset = 0;
            double.TryParse(TxtOffset.Text, out offset);

            TxtStatus.Text = "Saving...";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Yellow;

            var (ok, error) = await _api.UpdateDeviceAsync(dev.Id, name, offset);
            if (ok)
            {
                TxtStatus.Text = "Saved successfully! Refresh dashboard to see changes.";
                TxtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                dev.Name = name;
                // Update combo item text
                CmbDevices.Items[CmbDevices.SelectedIndex] = $"{name} ({dev.Id})";
            }
            else
            {
                TxtStatus.Text = $"Error: {error}";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private class DeviceItem { public string Id { get; set; } public string Name { get; set; } public double Offset { get; set; } }
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CmbDevices.SelectedIndex < 0) return;
            var dev = _devices[CmbDevices.SelectedIndex];

            var result = MessageBox.Show($"Are you sure you want to DELETE '{dev.Name}'?\\n\\nThis will remove the device and ALL its historical data. This cannot be undone.", 
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                var (ok, error) = await _api.DeleteDeviceAsync(dev.Id);
                if (ok)
                {
                    MessageBox.Show("Device deleted successfully.");
                    CmbDevices.Items.Clear();
                    _devices.Clear();
                    EditorPanel.Visibility = Visibility.Collapsed;
                    await LoadDevices(); // Reload list safely
                }
                else
                {
                    MessageBox.Show($"Failed to delete: {error}");
                }
            }
        }
    }
}