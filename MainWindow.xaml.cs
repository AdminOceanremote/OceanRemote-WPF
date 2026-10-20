using System.Windows;
using System.Windows.Controls;
using OceanMonitor2.Services;

namespace OceanMonitor2
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _api;

        public MainWindow(ApiService api)
        {
            InitializeComponent();
            _api = api;
            UserLabel.Text = "👤 " + (_api.Username ?? "user");

            ApplyTranslations();
            NavDashboard_Click(null, null);
        }

        private void ApplyTranslations()
        {
            NavDashboard.Content = AppSettings.T("nav.dashboard");
            NavAlerts.Content = AppSettings.T("nav.alerts");
            NavSettings.Content = AppSettings.T("nav.settings");
            NavAbout.Content = AppSettings.T("nav.about");
        }

        private void NavDashboard_Click(object? sender, RoutedEventArgs? e)
            => PageHost.Content = new DashboardWindow(_api);

        private void NavAlerts_Click(object sender, RoutedEventArgs e)
            => PageHost.Content = new AlertsWindow(_api);

        private void NavFirmware_Click(object sender, RoutedEventArgs e)
        {
            var fw = new FirmwareWindow { Owner = this };
            fw.ShowDialog();
        }

        private void NavDevices_Click(object sender, RoutedEventArgs e)
        {
            PageHost.Content = new DeviceSettingsPage(_api);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
            => PageHost.Content = new SettingsPage();

        private void NavAbout_Click(object sender, RoutedEventArgs e)
            => PageHost.Content = new AboutPage();

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            ApiService.ClearCredentials();
            new LoginWindow().Show();
            this.Close();
        }
    }
}