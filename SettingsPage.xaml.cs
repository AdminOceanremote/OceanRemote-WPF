using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OceanMonitor2
{
    public partial class SettingsPage : UserControl
    {
        private bool _loaded = false;

        public SettingsPage()
        {
            InitializeComponent();

            // Show current saved values
            ServerUrlBox.Text = AppSettings.ServerUrl;

            if (AppSettings.Language == "fr") LangFrench.IsChecked = true;
            else LangEnglish.IsChecked = true;

            if (AppSettings.Theme == "light") ThemeLight.IsChecked = true;
            else ThemeDark.IsChecked = true;

            ApplyTranslations();
            _loaded = true;
        }

        private void ApplyTranslations()
        {
            TitleText.Text = AppSettings.T("settings.title");
            LblServer.Text = AppSettings.T("settings.server");
            BtnSaveServer.Content = AppSettings.T("settings.serversave");
            LblLanguage.Text = AppSettings.T("settings.language");
            LblTheme.Text = AppSettings.T("settings.theme");
            LblUpdate.Text = AppSettings.T("settings.update");
            BtnCheckUpdates.Content = AppSettings.T("settings.check");
        }

        private void SaveServer_Click(object sender, RoutedEventArgs e)
        {
            var input = ServerUrlBox.Text.Trim();
            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ServerStatus.Text = "Invalid URL (must start with http:// or https://)";
                ServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                return;
            }

            AppSettings.ServerUrl = input.TrimEnd('/');
            AppSettings.Save();
            ServerStatus.Text = AppSettings.T("settings.serverrestart");
            ServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }

        private void Lang_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            AppSettings.Language = LangFrench.IsChecked == true ? "fr" : "en";
            AppSettings.Save();

            UpdateStatus.Text = AppSettings.T("settings.restart");
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            AppSettings.Theme = ThemeLight.IsChecked == true ? "light" : "dark";
            AppSettings.Save();
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus.Text = AppSettings.T("settings.noupdate");
        }
    }
}