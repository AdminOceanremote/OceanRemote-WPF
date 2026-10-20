# OceanMonitor2 — Oceamic SCADA Telemetry Console

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/Platform-WPF%20Windows-0078D7?logo=windows&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
[![LiveCharts2](https://img.shields.io/badge/Charts-LiveCharts2%20(SkiaSharp)-38BDF8)](https://livecharts.dev/)
[![WebView2](https://img.shields.io/badge/Gauges-Microsoft%20WebView2-10B981)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
[![Status](https://img.shields.io/badge/Build-Passing-emerald)](https://github.com/)

A modern, SCADA-grade dark HUD desktop telemetry console engineered for the **Oceamic II** refrigeration facilities and cold-chain infrastructure. Connects to cloud or edge IoT gateways to deliver real-time environmental monitoring, compliance auditing, and early excursion alarms.

---

## 🖥️ User Interface & SCADA Design System

Built with a custom industrial dark HUD aesthetic inspired by mission-control telemetry panels:
- **Base Canvas**: Deep Slate (`#040814`) with a dark vector grid texture
- **Accent Hierarchy**:
  - 🌐 **Cyber Cyan (`#38BDF8`)**: Primary telemetry streams, nominal readouts, diurnal curves
  - 🟣 **Electric Purple (`#A78BFA`)**: Node ID badges, system architecture, secondary highlights
  - 🟡 **Warning Amber (`#FBBF24`)**: Range averages, multi-day progressions, aggregate metrics
  - 🟢 **Nominal Emerald (`#10B981`)**: Normal operating bands, online node links, acknowledgment triggers
  - 🔴 **Critical Rose (`#EF4444`)**: Thermal excursions, threshold breaches, node outages

---

## ✨ Features

- **Dynamic Cold Room Matrix**: Real-time tile view with animated Chromium SVG radial gauges hosted via Microsoft WebView2 (`gauge.html`).
  - Target temperature bounds automatically configured per chamber:
    - *Négative 1*: `-25.0°C` to `-18.0°C`
    - *Négative 2*: `-22.0°C` to `-15.0°C`
    - *Positive*: `0.0°C` to `4.0°C`
    - *Ingrédients*: `4.0°C` to `8.0°C`
- **Rolling Sparkline Trends**: 3-hour micro sparklines with live rolling minimum, maximum, mean, and drift calculation (`+X.X°C`).
- **Diurnal Inspection (`Today`)**: Single-day high-resolution hourly temperature curve with SkiaSharp hardware acceleration.
- **Multi-Day Rollup (`Month`)**: Rolling daily average progressions across custom date ranges.
- **Fast Audit Reporting (`Report`)**: Comprehensive historical inspection with day-by-day charts, excursion spreads, and stability badges (`NOMINAL STABLE`, `HIGH VARIANCE`, `CRITICAL BREACH`).
- **Alarm Center (`Alerts`)**: Event stream of threshold trips with device/date filters and individual/bulk acknowledgment.
- **Server-Agnostic Architecture**: Switch telemetry endpoints directly from the Login Window or the Settings page (`AppSettings.ServerUrl`) to connect to local edge gateways, Docker instances, or cloud backends.
- **Security & Auto-Login**: Windows DPAPI (`System.Security.Cryptography.ProtectedData`) encryption for local credential retention (`creds.dat`).
- **Single Instance Enforcement**: OS-level Mutex ensures a single instance runs, automatically focusing the active window on repeat launches.
- **Bilingual Interface**: Instant translation between English (`EN`) and French (`FR`).

---

## 🌐 Companion Backend (Django Server)

OceanMonitor2 communicates with a companion **Django REST Telemetry Server**:
- **Default Cloud Endpoint**: `https://esp8266.pythonanywhere.com`
- **Companion Repository**: [OceanRemote Django IoT Backend](https://github.com/esp8266/oceanremote-backend) *(or deploy your own server using the API schema below)*

### API Specification

| Endpoint | Method | Description |
| :--- | :--- | :--- |
| `/api/app/login/` | `POST` | Operator authentication and bearer token issue |
| `/api/app/devices/` | `GET` | Current device inventory, telemetry readings, and status |
| `/api/app/history/{id}/?date=YYYY-MM-DD` | `GET` | Full-day timestamped temperature readings |
| `/api/app/history/{id}/range/?start=&end=` | `GET` | Multi-day daily averages and bounds |
| `/api/app/report-fast/{id}/?start=&end=` | `GET` | Optimized multi-day reporting matrix |
| `/api/app/notifications/?device_id=&date=` | `GET` | Incident list with read/unread flags |
| `/api/app/notifications/{id}/read/` | `POST` | Acknowledge single incident |
| `/api/app/notifications/mark-all-read/` | `POST` | Acknowledge all active incidents |

---

## 🚀 Building & Publishing

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 / Windows 11 (x64)
- WebView2 Runtime (pre-installed on modern Windows)

### Build Debug
```powershell
dotnet restore
dotnet build
```

### Publish Single-File Executable
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

## 📜 License & Support

Developed for **Oceamic II** refrigeration systems.  
For technical support or deployment inquiries, contact **support@oceanremote.net**.
