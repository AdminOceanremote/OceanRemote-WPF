using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace OceanMonitor2
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "OceanRemoteMonitor_SingleInstance";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                {
                    if (proc.Id != current.Id && proc.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindowAsync(proc.MainWindowHandle, 9);
                        SetForegroundWindow(proc.MainWindowHandle);
                        break;
                    }
                }
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }
}
