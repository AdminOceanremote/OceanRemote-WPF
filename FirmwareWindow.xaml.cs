using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OceanMonitor2
{
    public partial class FirmwareWindow : Window
    {
        private class SensorRow
        {
            public TextBox IdBox { get; set; }
            public ComboBox TypeCombo { get; set; }
            public ComboBox PinCombo { get; set; }
            public Grid Container { get; set; }
        }

        private System.Collections.Generic.List<SensorRow> _sensors = new();

        public FirmwareWindow()
        {
            InitializeComponent();
            CmbBoard.SelectionChanged += CmbBoard_SelectionChanged;
            // Start empty
        }

        private void BtnAddSensor_Click(object sender, RoutedEventArgs e)
        {
            // Check how many pins are available for the current board
            string board = ((ComboBoxItem)CmbBoard.SelectedItem)?.Content?.ToString() ?? "ESP32";
            var maxPins = GetAvailablePins(board, "DHT22").Length + GetAvailablePins(board, "NTC (Steinhart)").Length;
            
            if (_sensors.Count >= maxPins)
            {
                MessageBox.Show($"Maximum sensors reached for {board}. No more available pins.");
                return;
            }

            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Device ID
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Type
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Pin
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // X

            var idBox = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = $"device{_sensors.Count + 1}", Background = System.Windows.Media.Brushes.White };
            var typeCombo = new ComboBox { Margin = new Thickness(0, 0, 10, 0) };
            typeCombo.Items.Add("DHT22"); typeCombo.Items.Add("DS18B20"); typeCombo.Items.Add("NTC (Steinhart)");
            typeCombo.SelectedIndex = 0;
            
            var pinCombo = new ComboBox { Margin = new Thickness(0, 0, 10, 0) };
            var delBtn = new Button { Content = "X", Background = System.Windows.Media.Brushes.Red, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };

            var row = new SensorRow { IdBox = idBox, TypeCombo = typeCombo, PinCombo = pinCombo, Container = grid };

            typeCombo.SelectionChanged += (s, args) => UpdatePins();
            pinCombo.SelectionChanged += (s, args) => UpdatePins();
            delBtn.Click += (s, args) => { SensorsList.Children.Remove(grid); _sensors.Remove(row); UpdatePins(); };

            Grid.SetColumn(idBox, 0);
            Grid.SetColumn(typeCombo, 1);
            Grid.SetColumn(pinCombo, 2);
            Grid.SetColumn(delBtn, 3);

            grid.Children.Add(idBox);
            grid.Children.Add(typeCombo);
            grid.Children.Add(pinCombo);
            grid.Children.Add(delBtn);

            SensorsList.Children.Add(grid);
            _sensors.Add(row);
            UpdatePins();
        }

        private bool _isUpdating = false;
        private void UpdatePins()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                string board = ((ComboBoxItem)CmbBoard.SelectedItem)?.Content?.ToString() ?? "ESP32";

                var usedPins = _sensors.Select(s => s.PinCombo.SelectedItem?.ToString()).Where(p => p != null).ToList();

                foreach (var row in _sensors)
                {
                    string currentPin = row.PinCombo.SelectedItem?.ToString();
                    string sensor = row.TypeCombo.SelectedItem?.ToString() ?? "DHT22";
                    
                    row.PinCombo.Items.Clear();
                    var availablePins = GetAvailablePins(board, sensor).Where(p => p == currentPin || !usedPins.Contains(p)).ToList();

                    foreach (var p in availablePins) row.PinCombo.Items.Add(p);
                    if (row.PinCombo.Items.Count > 0) row.PinCombo.SelectedItem = currentPin ?? row.PinCombo.Items[0];
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private string[] GetAvailablePins(string board, string sensor)
        {
            if (sensor == "NTC (Steinhart)")
            {
                if (board == "ESP32") return new[] { "32", "33", "34", "35", "36", "39" };
                if (board.StartsWith("ESP8266")) return new[] { "A0" };
                if (board == "Pico W") return new[] { "26", "27", "28" };
            }
            else
            {
                if (board == "ESP32") return new[] { "4", "5", "13", "14", "15", "16", "17", "18", "19", "21", "22", "23", "25", "26", "27" };
                if (board == "ESP8266 D1 Mini") return new[] { "D1", "D2", "D5", "D6", "D7" };
                if (board == "ESP8266 D1 Large") return new[] { "D1", "D2", "D3", "D4", "D5", "D6", "D7" };
                if (board == "Pico W") return new[] { "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15" };
            }
            return Array.Empty<string>();
        }

        private void CmbBoard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            SensorsList.Children.Clear();
            _sensors.Clear();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sensors.Count == 0) { MessageBox.Show("Add at least one sensor."); return; }
                
                if (_sensors.Any(s => s.PinCombo.SelectedItem == null))
                {
                    MessageBox.Show("Some sensors do not have a valid pin selected. Please remove them or select a valid board.");
                    return;
                }

                // Validate duplicate Device IDs
                var ids = _sensors.Select(s => s.IdBox.Text.Trim()).ToList();
                if (ids.Count != ids.Distinct().Count()) { MessageBox.Show("Duplicate Device IDs found. Please make them unique."); return; }
                if (ids.Any(string.IsNullOrWhiteSpace)) { MessageBox.Show("Device ID cannot be empty."); return; }

                string board = ((ComboBoxItem)CmbBoard.SelectedItem)?.Content?.ToString() ?? "ESP32";
                string ssid = TxtSsid.Text.Trim();
                string pass = TxtPass.Text;

                if (string.IsNullOrEmpty(ssid)) { MessageBox.Show("Please enter WiFi SSID."); return; }

                string templateFile = board switch {
                    "ESP8266 D1 Mini" => "esp8266.txt",
                    "ESP8266 D1 Large" => "esp8266.txt",
                    "Pico W" => "picow.txt",
                    _ => "esp32.txt"
                };

                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FirmwareTemplates", templateFile);
                if (!File.Exists(templatePath)) { MessageBox.Show($"Template not found: {templateFile}"); return; }

                string code = File.ReadAllText(templatePath);
                string serverUrl = AppSettings.ServerUrl ?? "https://kio33.pythonanywhere.com";

                string serverUrlNoHttp = serverUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');

                code = code.Replace("{{WIFI_SSID}}", ssid)
                           .Replace("{{WIFI_PASSWORD}}", pass)
                           .Replace("{{SERVER_URL}}", serverUrl)
                           .Replace("{{SERVER_URL_NO_HTTP}}", serverUrlNoHttp);

                string includes = "";
                string setupCode = "";
                string readCode = "";
                int sensorIdx = 0;

                foreach (var s in _sensors)
                {
                    string type = s.TypeCombo.SelectedItem?.ToString() ?? "DHT22";
                    string pin = s.PinCombo.SelectedItem?.ToString() ?? "4";
                    string sId = s.IdBox.Text.Trim();
                    sensorIdx++;

                    if (type == "DHT22")
                    {
                        includes += $"#include \"DHT.h\"\nDHT dht{sensorIdx}({pin}, DHT22);\n";
                        setupCode += $"  dht{sensorIdx}.begin();\n";
                        readCode += $"  float t{sensorIdx} = dht{sensorIdx}.readTemperature();\n  sendTemp(\"{sId}\", isnan(t{sensorIdx}) ? -999.0 : t{sensorIdx});\n  delay(1000);\n";
                    }
                    else if (type == "DS18B20")
                    {
                        includes += $"#include <OneWire.h>\n#include <DallasTemperature.h>\nOneWire ow{sensorIdx}({pin});\nDallasTemperature ds{sensorIdx}(&ow{sensorIdx});\n";
                        setupCode += $"  ds{sensorIdx}.begin();\n";
                        readCode += $"  ds{sensorIdx}.requestTemperatures();\n  float t{sensorIdx} = ds{sensorIdx}.getTempCByIndex(0);\n  sendTemp(\"{sId}\", t{sensorIdx} == -127.0 ? -999.0 : t{sensorIdx});\n  delay(1000);\n";
                    }
                    else if (type == "NTC (Steinhart)")
                    {
                        string adcMax = board.StartsWith("ESP8266") ? "1023" : "4095";
                        string adcPin = board.StartsWith("ESP8266") ? "A0" : pin;
                        
                        readCode += $"  int raw{sensorIdx} = analogRead({adcPin});\n  if (raw{sensorIdx} <= 5 || raw{sensorIdx} >= {adcMax}) {{\n    sendTemp(\"{sId}\", -999.0);\n  }} else {{\n    float v{sensorIdx} = (raw{sensorIdx} / (float){adcMax}) * 3.3;\n    float r{sensorIdx} = 10000.0 * ((3.3 / v{sensorIdx}) - 1.0);\n    float sh{sensorIdx} = log(r{sensorIdx} / 10000.0);\n    sh{sensorIdx} /= 3950.0; sh{sensorIdx} += (1.0 / (25.0 + 273.15));\n    sendTemp(\"{sId}\", (1.0 / sh{sensorIdx}) - 273.15);\n  }}\n  delay(1000);\n";
                    }
                }

                code = code.Replace("{{SENSOR_INCLUDES}}", includes)
                           .Replace("{{SENSOR_SETUP}}", setupCode)
                           .Replace("{{SENSOR_READ}}", readCode);

                TxtCode.Text = code;
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtCode.Text))
            {
                Clipboard.SetText(TxtCode.Text);
                MessageBox.Show("Code copied to clipboard!");
            }
        }
    }
}