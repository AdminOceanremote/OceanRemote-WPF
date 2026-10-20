using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using OceanMonitor2.Services;
using SkiaSharp;

namespace OceanMonitor2
{
    public partial class ReportWindow : UserControl
    {
        private readonly ApiService _api;
        private readonly string _deviceId;
        private readonly string _deviceName;

        public ObservableCollection<DayReport> Days { get; set; } = new();

        public ReportWindow(ApiService api, string deviceId, string deviceName)
        {
            InitializeComponent();
            _api = api;
            _deviceId = deviceId;
            _deviceName = deviceName;

            TitleLabel.Text = $"REPORT // {deviceName}".ToUpper();
            DeviceSubtitle.Text = $"NODE ID: {deviceId.ToUpper()} // ENCRYPTED TELEMETRY AUDIT";

            EndPicker.SelectedDate = DateTime.Today;
            StartPicker.SelectedDate = DateTime.Today.AddDays(-30);

            ChartsList.ItemsSource = Days;

            Loaded += async (_, __) => await LoadReport();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadReport();

        private async Task LoadReport()
        {
            try
            {
                var startDate = StartPicker.SelectedDate ?? DateTime.Today.AddDays(-30);
                var endDate = EndPicker.SelectedDate ?? DateTime.Today;

                var totalDaysSpan = (endDate - startDate).Days + 1;
                RangeDaysText.Text = $"{totalDaysSpan} DAYS";

                var start = startDate.ToString("yyyy-MM-dd");
                var end = endDate.ToString("yyyy-MM-dd");

                var json = await _api.GetReportAsync(_deviceId, start, end);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Days.Clear();

                if (!root.TryGetProperty("days", out var daysArr)) return;

                var daysList = daysArr.EnumerateArray().ToList();
                daysList.Reverse();

                double globalMin = double.MaxValue;
                double globalMax = double.MinValue;
                double globalSum = 0;
                int globalCount = 0;

                foreach (var day in daysList)
                {
                    var date = day.GetProperty("date").GetString() ?? "";
                    if (!day.TryGetProperty("timestamps", out var tsArr) || !day.TryGetProperty("temperatures", out var tArr))
                        continue;

                    var points = new List<DateTimePoint>();
                    var temps = new List<double>();
                    var rawPoints = new List<(DateTime time, double temp)>();

                    int i = 0;
                    foreach (var t in tsArr.EnumerateArray())
                    {
                        if (i >= tArr.GetArrayLength()) break;
                        var dt = DateTime.Parse(t.GetString()!);
                        var v = tArr[i++].GetDouble();
                        points.Add(new DateTimePoint(dt, v));
                        rawPoints.Add((dt, v));
                        temps.Add(v);

                        if (v < globalMin) globalMin = v;
                        if (v > globalMax) globalMax = v;
                        globalSum += v;
                        globalCount++;
                    }

                    if (points.Count == 0) continue;

                    double min = temps.Min();
                    double max = temps.Max();
                    double avg = temps.Average();
                    double spread = max - min;

                    // Neon SCADA styling: Smooth gradient fill with zero-glow edges
                    var series = new ISeries[]
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
                            )
                        }
                    };

                    // Dark grid axes
                    var xaxes = new Axis[]
                    {
                        new DateTimeAxis(TimeSpan.FromHours(4), d => d.ToString("HH:mm"))
                        {
                            TextSize = 10,
                            LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                            SeparatorsPaint = new SolidColorPaint(new SKColor(22, 34, 56)) { StrokeThickness = 1 }
                        }
                    };

                    var yaxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = v => $"{v:F1}°C",
                            TextSize = 10,
                            LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                            SeparatorsPaint = new SolidColorPaint(new SKColor(22, 34, 56)) { StrokeThickness = 1 }
                        }
                    };

                    // Stability badge determination
                    string evalText = "NOMINAL STABLE";
                    Brush evalBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                    if (spread > 6.0)
                    {
                        evalText = "HIGH VOLATILITY";
                        evalBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    else if (spread > 3.0)
                    {
                        evalText = "THERMAL DRIFT";
                        evalBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                    }

                    Days.Add(new DayReport
                    {
                        DateTitle = DateTime.Parse(date).ToString("dddd, dd MMMM yyyy").ToUpper(),
                        SampleCountText = $"[{points.Count} SAMPLES]",
                        SpreadText = $"Δ {spread:F1}°C",
                        Series = series,
                        XAxes = xaxes,
                        YAxes = yaxes,
                        MinText = $"{min:F1}°C",
                        AvgText = $"{avg:F1}°C",
                        MaxText = $"{max:F1}°C",
                        StatusEvalText = evalText,
                        StatusEvalBrush = evalBrush,
                        RawPoints = rawPoints,
                        MinVal = min,
                        MaxVal = max,
                        AvgVal = avg
                    });

                    await Task.Delay(10);
                }

                // Populate Global Overview Strip
                TotalDaysLabel.Text = Days.Count.ToString();
                TotalPointsLabel.Text = globalCount.ToString("N0");

                if (globalCount > 0)
                {
                    GlobalMinLabel.Text = $"{globalMin:F1}°C";
                    GlobalAvgLabel.Text = $"{globalSum / globalCount:F1}°C";
                    GlobalMaxLabel.Text = $"{globalMax:F1}°C";
                }
                else
                {
                    GlobalMinLabel.Text = "--";
                    GlobalAvgLabel.Text = "--";
                    GlobalMaxLabel.Text = "--";
                }
            }
            catch { }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            if (Days.Count == 0)
            {
                MessageBox.Show("No telemetry data loaded to print. Please click PULL DATA first.", "Print Telemetry Report", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var printDialog = new PrintDialog();
            try
            {
                if (printDialog.PrintTicket != null)
                {
                    printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
                }
            }
            catch { }

            if (printDialog.ShowDialog() != true) return;

            // Enforce Landscape Dimensions
            double pageWidth = printDialog.PrintableAreaWidth;
            double pageHeight = printDialog.PrintableAreaHeight;

            if (pageWidth < pageHeight)
            {
                var t = pageWidth;
                pageWidth = pageHeight;
                pageHeight = t;
            }

            if (pageWidth <= 0 || pageHeight <= 0)
            {
                pageWidth = 1122.5; // A4 Landscape 96 DPI
                pageHeight = 793.7;
            }

            var doc = new FixedDocument();
            int totalDays = Days.Count;
            int chartsPerPage = 4;
            int totalPages = (int)Math.Ceiling(totalDays / (double)chartsPerPage);

            double marginX = 14;
            double marginY = 10;
            double contentWidth = pageWidth - (marginX * 2);
            double contentHeight = pageHeight - (marginY * 2);
            double headerHeight = 22;
            double gap = 4;
            double availableChartsHeight = contentHeight - headerHeight - (gap * (chartsPerPage - 1));
            double singleCardHeight = availableChartsHeight / chartsPerPage;
            int chartPixelWidth = (int)Math.Max(contentWidth - 16, 900);
            int chartPixelHeight = (int)Math.Max(singleCardHeight - 26, 90);

            for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
            {
                var page = new FixedPage
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Background = new SolidColorBrush(Color.FromRgb(4, 8, 20))
                };

                var pageContainer = new Grid
                {
                    Margin = new Thickness(marginX, marginY, marginX, marginY),
                    Width = contentWidth,
                    Height = contentHeight
                };

                pageContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headerHeight, GridUnitType.Pixel) });
                for (int r = 0; r < chartsPerPage; r++)
                {
                    pageContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }

                // Page Header Strip
                var pageHeader = new Grid();
                var hl = new TextBlock
                {
                    Text = $"OCEANREMOTE MONITOR // COLD CHAIN TELEMETRY AUDIT // ROOM: {_deviceName.ToUpper()} (NODE: {_deviceId.ToUpper()})",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var hr = new TextBlock
                {
                    Text = $"A4 LANDSCAPE // PAGE {pageIdx + 1} OF {totalPages} // {DateTime.Now:yyyy-MM-dd HH:mm}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pageHeader.Children.Add(hl);
                pageHeader.Children.Add(hr);
                Grid.SetRow(pageHeader, 0);
                pageContainer.Children.Add(pageHeader);

                // Up to 4 charts per page
                for (int c = 0; c < chartsPerPage; c++)
                {
                    int dayIdx = (pageIdx * chartsPerPage) + c;
                    if (dayIdx >= totalDays) break;

                    var day = Days[dayIdx];

                    var cardBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(6, 12, 27)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 56)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 2, 0, 2),
                        Padding = new Thickness(8, 3, 8, 3)
                    };

                    var cardGrid = new Grid();
                    cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18, GridUnitType.Pixel) });
                    cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    // Card Header with title and stats
                    var cardHeader = new Grid();
                    var titleTb = new TextBlock
                    {
                        Text = $"{day.DateTitle}  {day.SampleCountText}",
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var statsPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    statsPanel.Children.Add(new TextBlock { Text = "MIN: ", FontSize = 8.5, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                    statsPanel.Children.Add(new TextBlock { Text = day.MinText + "  ", FontSize = 8.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) });

                    statsPanel.Children.Add(new TextBlock { Text = "AVG: ", FontSize = 8.5, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                    statsPanel.Children.Add(new TextBlock { Text = day.AvgText + "  ", FontSize = 8.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)) });

                    statsPanel.Children.Add(new TextBlock { Text = "MAX: ", FontSize = 8.5, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                    statsPanel.Children.Add(new TextBlock { Text = day.MaxText + "  ", FontSize = 8.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)) });

                    statsPanel.Children.Add(new TextBlock { Text = "SPREAD: ", FontSize = 8.5, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
                    statsPanel.Children.Add(new TextBlock { Text = day.SpreadText + "  ", FontSize = 8.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) });

                    statsPanel.Children.Add(new TextBlock { Text = $"[{day.StatusEvalText}]", FontSize = 8.5, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = day.StatusEvalBrush });

                    cardHeader.Children.Add(titleTb);
                    cardHeader.Children.Add(statsPanel);
                    Grid.SetRow(cardHeader, 0);
                    cardGrid.Children.Add(cardHeader);

                    // High-resolution SkiaSharp-rendered chart
                    var chartImg = new Image
                    {
                        Source = RenderChartImage(day, chartPixelWidth, chartPixelHeight),
                        Stretch = Stretch.Fill,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    Grid.SetRow(chartImg, 1);
                    cardGrid.Children.Add(chartImg);

                    cardBorder.Child = cardGrid;
                    Grid.SetRow(cardBorder, c + 1);
                    pageContainer.Children.Add(cardBorder);
                }

                page.Children.Add(pageContainer);

                var pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(page);
                doc.Pages.Add(pageContent);
            }

            printDialog.PrintDocument(doc.DocumentPaginator, $"OceanRemote_Report_{_deviceName}_{DateTime.Now:yyyyMMdd}");
        }

        private BitmapSource RenderChartImage(DayReport day, int targetWidth, int targetHeight)
        {
            int renderWidth = Math.Max(targetWidth, 900);
            int renderHeight = Math.Max(targetHeight, 90);

            var info = new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(new SKColor(2, 4, 10));

            double minTemp = day.MinVal;
            double maxTemp = day.MaxVal;
            if (Math.Abs(maxTemp - minTemp) < 0.5)
            {
                minTemp -= 1.0;
                maxTemp += 1.0;
            }
            double pad = (maxTemp - minTemp) * 0.12;
            double yMin = minTemp - pad;
            double yMax = maxTemp + pad;

            float leftMargin = 50f;
            float rightMargin = 20f;
            float topMargin = 8f;
            float bottomMargin = 18f;
            float plotWidth = renderWidth - leftMargin - rightMargin;
            float plotHeight = renderHeight - topMargin - bottomMargin;

            // Gridlines & Y-Axis labels
            using var gridPaint = new SKPaint
            {
                Color = new SKColor(16, 26, 42),
                StrokeWidth = 1f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            using var labelPaint = new SKPaint
            {
                Color = new SKColor(100, 116, 139),
                TextSize = 9f,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Consolas")
            };

            int ySteps = 3;
            for (int s = 0; s <= ySteps; s++)
            {
                float y = topMargin + (plotHeight * s / ySteps);
                canvas.DrawLine(leftMargin, y, leftMargin + plotWidth, y, gridPaint);
                double val = yMax - (s * (yMax - yMin) / ySteps);
                canvas.DrawText($"{val:F1}°", 8, y + 3, labelPaint);
            }

            // X-Axis labels
            string[] timeLabels = { "00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00" };
            for (int i = 0; i < timeLabels.Length; i++)
            {
                float x = leftMargin + (plotWidth * i / (timeLabels.Length - 1));
                canvas.DrawLine(x, topMargin, x, topMargin + plotHeight, gridPaint);
                canvas.DrawText(timeLabels[i], x - 12, renderHeight - 4, labelPaint);
            }

            // Temperature curve & area gradient
            if (day.RawPoints.Count > 1)
            {
                var path = new SKPath();
                var fillPath = new SKPath();

                var startTime = day.RawPoints.First().time.Date;
                var totalDaySeconds = 24 * 3600.0;
                bool first = true;
                float firstX = leftMargin;
                float lastX = leftMargin + plotWidth;

                for (int p = 0; p < day.RawPoints.Count; p++)
                {
                    var pt = day.RawPoints[p];
                    double sec = (pt.time - startTime).TotalSeconds;
                    float x = leftMargin + (float)(Math.Clamp(sec / totalDaySeconds, 0.0, 1.0) * plotWidth);
                    float y = topMargin + (float)((yMax - pt.temp) / (yMax - yMin) * plotHeight);
                    y = Math.Clamp(y, topMargin, topMargin + plotHeight);

                    if (first)
                    {
                        path.MoveTo(x, y);
                        fillPath.MoveTo(x, topMargin + plotHeight);
                        fillPath.LineTo(x, y);
                        firstX = x;
                        first = false;
                    }
                    else
                    {
                        path.LineTo(x, y);
                        fillPath.LineTo(x, y);
                    }
                    lastX = x;
                }

                fillPath.LineTo(lastX, topMargin + plotHeight);
                fillPath.Close();

                using var fillPaint = new SKPaint
                {
                    Shader = SKShader.CreateLinearGradient(
                        new SKPoint(0, topMargin),
                        new SKPoint(0, topMargin + plotHeight),
                        new[] { new SKColor(167, 139, 250, 80), new SKColor(56, 189, 248, 15), new SKColor(2, 4, 10, 0) },
                        SKShaderTileMode.Clamp
                    ),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawPath(fillPath, fillPaint);

                using var strokePaint = new SKPaint
                {
                    Color = new SKColor(167, 139, 250),
                    StrokeWidth = 1.75f,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawPath(path, strokePaint);
            }

            using var img = surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }

    public class DayReport
    {
        public string DateTitle { get; set; } = "";
        public string SampleCountText { get; set; } = "";
        public string SpreadText { get; set; } = "--";
        public ISeries[] Series { get; set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; set; } = Array.Empty<Axis>();
        public string MinText { get; set; } = "--";
        public string AvgText { get; set; } = "--";
        public string MaxText { get; set; } = "--";
        public string StatusEvalText { get; set; } = "NOMINAL";
        public Brush StatusEvalBrush { get; set; } = new SolidColorBrush(Color.FromRgb(16, 185, 129));

        public List<(DateTime time, double temp)> RawPoints { get; set; } = new();
        public double MinVal { get; set; }
        public double MaxVal { get; set; }
        public double AvgVal { get; set; }

        public DrawMarginFrame MarginFrame { get; set; } = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(new SKColor(2, 4, 10)),
            Stroke = new SolidColorPaint(new SKColor(12, 29, 56)) { StrokeThickness = 1 }
        };
    }
}