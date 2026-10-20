using System;
using System.Windows;
using System.Windows.Input;
using OceanMonitor2.Services;

namespace OceanMonitor2
{
    public partial class LoginWindow : Window
    {
        private ApiService _api = new();

        public LoginWindow()
        {
            InitializeComponent();

            ServerUrlBox.Text = AppSettings.ServerUrl;

            ApplyTranslations();

            PasswordBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    LoginButton_Click(s, new RoutedEventArgs());
            };

            ServerUrlBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    LoginButton_Click(s, new RoutedEventArgs());
            };

            Loaded += LoginWindow_Loaded;
        }

        private void ApplyTranslations()
        {
            WelcomeText.Text = AppSettings.T("login.welcome") ?? "OPERATOR AUTHENTICATION";
            SubtitleText.Text = AppSettings.T("login.subtitle") ?? "COLD CHAIN MONITORING SUBSYSTEM";
            UserLabelText.Text = AppSettings.T("login.username") ?? "OPERATOR IDENTIFIER";
            PassLabelText.Text = AppSettings.T("login.password") ?? "ACCESS CIPHER KEY";
            ServerLabelText.Text = AppSettings.T("login.server") ?? "SERVER TELEMETRY ENDPOINT";
            LoginButton.Content = AppSettings.T("login.signin") ?? "ESTABLISH LINK";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var (user, pass) = ApiService.LoadSavedCredentials();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return;

            UsernameBox.Text = user;
            PasswordBox.Password = pass;

            LoginButton.IsEnabled = false;
            LoginButton.Content = "VERIFYING ENCRYPTED SESSION...";

            var (ok, _) = await _api.LoginAsync(user, pass);
            if (ok)
            {
                var main = new MainWindow(_api);
                main.Show();
                this.Close();
            }
            else
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = AppSettings.T("login.signin") ?? "ESTABLISH LINK";
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "";
            LoginButton.IsEnabled = false;
            LoginButton.Content = "CONNECTING...";

            var serverUrl = ServerUrlBox.Text.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = AppSettings.T("login.signin") ?? "ESTABLISH LINK";
                ErrorText.Text = "Invalid server URL. Must start with http:// or https://";
                return;
            }

            if (serverUrl != AppSettings.ServerUrl)
            {
                AppSettings.ServerUrl = serverUrl;
                AppSettings.Save();
                _api = new ApiService();
            }

            var user = UsernameBox.Text.Trim();
            var pass = PasswordBox.Password;

            var (ok, error) = await _api.LoginAsync(user, pass);

            LoginButton.IsEnabled = true;
            LoginButton.Content = AppSettings.T("login.signin") ?? "ESTABLISH LINK";

            if (!ok)
            {
                ErrorText.Text = error ?? "Cannot connect. Please check your internet connection.";
                return;
            }

            AppSettings.ServerUrl = serverUrl;
            AppSettings.Save();
            ApiService.SaveCredentials(user, pass);

            var main = new MainWindow(_api);
            main.Show();
            this.Close();
        }
    }
}