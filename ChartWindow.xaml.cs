using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using OceanMonitor2.Services;

namespace OceanMonitor2
{
    public partial class ChartWindow : UserControl
    {
        private readonly ApiService _api;
        private readonly string _deviceId;
        private readonly string _deviceName;

        public ChartWindow(ApiService api, string deviceId, string deviceName)
        {
            InitializeComponent();
            _api = api;
            _deviceId = deviceId;
            _deviceName = deviceName;

            TitleLabel.Text = $"TODAY // {_deviceName}".ToUpper();
            DeviceSubtitle.Text = $"NODE ID: {_deviceId.ToUpper()} // ENCRYPTED TELEMETRY STREAM";
            DatePickerBox.SelectedDate = DateTime.Today;

            ChartControl.DrawMarginFrame = new DrawMarginFrame
            {
                Fill = new SolidColorPaint(new SKColor(2, 4, 10)),
                Stroke = new SolidColorPaint(new SKColor(12, 29, 56)) { StrokeThickness = 1 }
            };

            Loaded += async (_, __) => await LoadChart();
        }

        private async void DatePicker_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await LoadChart();
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e) => _ = LoadChart();

        private async System.Threading.Tasks.Task LoadChart()
        {
            try
            {
                var selectedDate = DatePickerBox.SelectedDate ?? DateTime.Today;
                var date = selectedDate.ToString("yyyy-MM-dd");
                CardDateTitle.Text = selectedDate.ToString("dddd, dd MMMM yyyy").ToUpper();

                var json = await _api.GetHistoryDayAsync(_deviceId, date);

                if (string.IsNullOrWhiteSpace(json))
                {
                    ResetEmptyState();
                    return;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var timestamps = new List<DateTime>();
                var temps = new List<double>();

                if (root.TryGetProperty("timestamps", out var tsProp))
                {
                    foreach (var t in tsProp.EnumerateArray())
                        timestamps.Add(DateTime.Parse(t.GetString()!).ToLocalTime());
                }

                if (root.TryGetProperty("temperatures", out var tempProp))
                {
                    foreach (var v in tempProp.EnumerateArray())
                        temps.Add(v.GetDouble());
                }

                if (temps.Count == 0)
                {
                    ResetEmptyState();
                    return;
                }

                var points = new List<DateTimePoint>();
                for (int i = 0; i < timestamps.Count && i < temps.Count; i++)
                    points.Add(new DateTimePoint(timestamps[i], temps[i]));

                ChartControl.Series = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = points,
                        GeometrySize = 0,
                        LineSmoothness = 0.5,
                        Stroke = new SolidColorPaint(new SKColor(167, 139, 250), 2),
                        Fill = new LinearGradientPaint(
                            new[] { new SKColor(167, 139, 250, 70), new SKColor(56, 189, 248, 15), new SKColor(3, 7, 18, 0) },
                            new SKPoint(0.5f, 0),
                            new SKPoint(0.5f, 1)
                        ),
                        Name = "Temperature"
                    }
                };

                ChartControl.XAxes = new Axis[]
                {
                    new DateTimeAxis(TimeSpan.FromHours(4), d => d.ToString("HH:mm"))
                    {
                        TextSize = 10,
                        LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                        SeparatorsPaint = new SolidColorPaint(new SKColor(22, 34, 56)) { StrokeThickness = 1 }
                    }
                };

                ChartControl.YAxes = new Axis[]
                {
                    new Axis
                    {
                        Labeler = v => $"{v:F1}°C",
                        TextSize = 10,
                        LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                        SeparatorsPaint = new SolidColorPaint(new SKColor(22, 34, 56)) { StrokeThickness = 1 }
                    }
                };

                double min = double.MaxValue;
                double max = double.MinValue;
                double sum = 0;
                foreach (var v in temps)
                {
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                }
                double avg = sum / temps.Count;
                double spread = max - min;

                // Update Overview Metric Strip
                TotalPointsLabel.Text = temps.Count.ToString("N0");
                TopMinLabel.Text = $"{min:F1}°C";
                TopAvgLabel.Text = $"{avg:F1}°C";
                TopMaxLabel.Text = $"{max:F1}°C";
                TopSpreadLabel.Text = $"{spread:F1}°C";

                // Update Card Header Sub-elements
                CardSampleCountText.Text = $"[{temps.Count} SAMPLES]";
                CardSpreadText.Text = $"Δ {spread:F1}°C";

                // Update Bottom Statistics Ribbon
                MinLabel.Text = $"{min:F1}°C";
                AvgLabel.Text = $"{avg:F1}°C";
                MaxLabel.Text = $"{max:F1}°C";

                // Stability Evaluation
                if (spread > 6.0)
                {
                    StabilityLabel.Text = "HIGH VOLATILITY";
                    StabilityLabel.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
                else if (spread > 3.0)
                {
                    StabilityLabel.Text = "THERMAL DRIFT";
                    StabilityLabel.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                }
                else
                {
                    StabilityLabel.Text = "NOMINAL STABLE";
                    StabilityLabel.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
            }
            catch { }
        }

        private void ResetEmptyState()
        {
            ChartControl.Series = Array.Empty<ISeries>();
            TotalPointsLabel.Text = "0";
            TopMinLabel.Text = "--°";
            TopAvgLabel.Text = "--°";
            TopMaxLabel.Text = "--°";
            TopSpreadLabel.Text = "--°";

            CardSampleCountText.Text = "[0 SAMPLES]";
            CardSpreadText.Text = "Δ 0.0°C";

            MinLabel.Text = "--";
            AvgLabel.Text = "--";
            MaxLabel.Text = "--";
            StabilityLabel.Text = "NO DATA";
            StabilityLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }
}