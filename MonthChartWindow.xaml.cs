using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class MonthChartWindow : UserControl
    {
        private readonly ApiService _api;
        private readonly string _deviceId;
        private readonly string _deviceName;

        public MonthChartWindow(ApiService api, string deviceId, string deviceName)
        {
            InitializeComponent();
            _api = api;
            _deviceId = deviceId;
            _deviceName = deviceName;

            TitleLabel.Text = $"MONTH // {_deviceName}".ToUpper();
            DeviceSubtitle.Text = $"NODE ID: {_deviceId.ToUpper()} // 30-DAY ROLLING PROGRESSION";

            EndPicker.SelectedDate = DateTime.Today;
            StartPicker.SelectedDate = DateTime.Today.AddDays(-30);

            ChartControl.DrawMarginFrame = new DrawMarginFrame
            {
                Fill = new SolidColorPaint(new SKColor(2, 4, 10)),
                Stroke = new SolidColorPaint(new SKColor(12, 29, 56)) { StrokeThickness = 1 }
            };

            Loaded += async (_, __) => await LoadChart();
        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e) => _ = LoadChart();

        private async System.Threading.Tasks.Task LoadChart()
        {
            try
            {
                var startDate = StartPicker.SelectedDate ?? DateTime.Today.AddDays(-30);
                var endDate = EndPicker.SelectedDate ?? DateTime.Today;

                var totalDaysSpan = (endDate - startDate).Days + 1;
                RangeDaysText.Text = $"{totalDaysSpan} DAYS";

                var start = startDate.ToString("yyyy-MM-dd");
                var end = endDate.ToString("yyyy-MM-dd");

                var json = await _api.GetHistoryRangeAsync(_deviceId, start, end);
                if (string.IsNullOrWhiteSpace(json))
                {
                    ResetEmptyState();
                    return;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var points = new List<DateTimePoint>();
                var allTemps = new List<double>();

                if (root.TryGetProperty("days", out var daysArr))
                {
                    foreach (var day in daysArr.EnumerateArray())
                    {
                        if (day.TryGetProperty("date", out var dProp) && day.TryGetProperty("avg", out var aProp))
                        {
                            var date = DateTime.Parse(dProp.GetString()!);
                            var avgVal = aProp.GetDouble();
                            points.Add(new DateTimePoint(date, avgVal));
                            allTemps.Add(avgVal);
                        }
                    }
                }

                if (points.Count == 0)
                {
                    ResetEmptyState();
                    return;
                }

                ChartControl.Series = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = points,
                        GeometrySize = 5,
                        GeometryStroke = new SolidColorPaint(new SKColor(4, 8, 20), 1),
                        GeometryFill = new SolidColorPaint(new SKColor(167, 139, 250)),
                        LineSmoothness = 0.5,
                        Stroke = new SolidColorPaint(new SKColor(167, 139, 250), 2),
                        Fill = new LinearGradientPaint(
                            new[] { new SKColor(167, 139, 250, 70), new SKColor(56, 189, 248, 15), new SKColor(3, 7, 18, 0) },
                            new SKPoint(0.5f, 0),
                            new SKPoint(0.5f, 1)
                        ),
                        Name = "Daily Average"
                    }
                };

                int stepDays = Math.Max(1, totalDaysSpan / 8);
                ChartControl.XAxes = new Axis[]
                {
                    new DateTimeAxis(TimeSpan.FromDays(stepDays), d => d.ToString("dd MMM"))
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

                double min = allTemps.Min();
                double max = allTemps.Max();
                double avg = allTemps.Average();
                double spread = max - min;

                // Update Overview Metric Strip
                TotalDaysLabel.Text = allTemps.Count.ToString();
                TopMinLabel.Text = $"{min:F1}°C";
                TopAvgLabel.Text = $"{avg:F1}°C";
                TopMaxLabel.Text = $"{max:F1}°C";
                TopSpreadLabel.Text = $"{spread:F1}°C";

                // Update Card Header Sub-elements
                CardRangeTitle.Text = $"DAILY MEAN TRACE // {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}".ToUpper();
                CardDaysCountText.Text = $"[{allTemps.Count} DAYS]";
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
            TotalDaysLabel.Text = "0";
            TopMinLabel.Text = "--°";
            TopAvgLabel.Text = "--°";
            TopMaxLabel.Text = "--°";
            TopSpreadLabel.Text = "--°";

            CardDaysCountText.Text = "[0 DAYS]";
            CardSpreadText.Text = "Δ 0.0°C";

            MinLabel.Text = "--";
            AvgLabel.Text = "--";
            MaxLabel.Text = "--";
            StabilityLabel.Text = "NO DATA";
            StabilityLabel.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }
    }
}