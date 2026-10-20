# OceanMonitor2 — Project Context

## Stack
- WPF .NET 8, C# 12
- LiveCharts2 (SkiaSharp)
- WebView2 + gauge.html
- SCADA dark HUD (cyan #38BDF8, purple #A78BFA, emerald #10B981, rose #EF4444)

## Pages
LoginWindow, MainWindow, DashboardWindow (done), ChartWindow, MonthChartWindow, ReportWindow, AlertsWindow, SettingsPage, AboutPage

## Goals
1. Restyle Chart/Month/Report/Alerts/Settings/About/Login to match DashboardWindow.
2. Server-agnostic: replace hardcoded esp8266.pythonanywhere.com in ApiService.cs with configurable URL in AppSettings, editable in SettingsPage.
3. Prepare for GitHub release (README, Django server link).

## Rules
- Never hardcode server URLs.
- Never change x:Name, event handlers, or code-behind signatures.
- Never touch DashboardWindow.xaml or MainWindow.xaml unless asked.
- Match DashboardWindow styling.
- Show a plan before multi-file refactors.
