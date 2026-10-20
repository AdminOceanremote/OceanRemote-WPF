using System;
using System.IO;
using System.Text.Json;

namespace OceanMonitor2
{
    public static class AppSettings
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "OceanRemoteMonitor");

        private static readonly string FilePath =
            Path.Combine(Dir, "settings.json");

        private static string _language = "en";
        private static string _theme = "dark";
        private static string _serverUrl = "https://esp8266.pythonanywhere.com";

        public static string Language
        {
            get => _language;
            set => _language = value;
        }

        public static string Theme
        {
            get => _theme;
            set => _theme = value;
        }

        public static string ServerUrl
        {
            get => _serverUrl;
            set => _serverUrl = string.IsNullOrWhiteSpace(value) ? "https://esp8266.pythonanywhere.com" : value.Trim().TrimEnd('/');
        }

        static AppSettings()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (!System.IO.File.Exists(FilePath)) return;
                var json = System.IO.File.ReadAllText(FilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("language", out var l)) _language = l.GetString() ?? "en";
                if (doc.RootElement.TryGetProperty("theme", out var t)) _theme = t.GetString() ?? "dark";
                if (doc.RootElement.TryGetProperty("serverUrl", out var s) && !string.IsNullOrWhiteSpace(s.GetString()))
                    _serverUrl = s.GetString()!.Trim().TrimEnd('/');
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(new { language = _language, theme = _theme, serverUrl = _serverUrl });
                System.IO.File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        // ==================== TRANSLATION ====================
        public static string T(string key)
        {
            if (_language == "fr") return Fr.ContainsKey(key) ? Fr[key] : key;
            return En.ContainsKey(key) ? En[key] : key;
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> En =
            new()
            {
                { "nav.dashboard", "DASHBOARD" },
                { "nav.alerts", "ALERTS" },
                { "nav.settings", "SETTINGS" },
                { "nav.about", "ABOUT" },
                { "nav.logout", "LOGOUT" },

                { "dash.title", "OCEANREMOTE DASHBOARD" },
                { "dash.total", "TOTAL DEVICES" },
                { "dash.online", "ONLINE" },
                { "dash.offline", "OFFLINE" },
                { "dash.lastupdate", "LAST UPDATE" },
                { "dash.alerts", "ALERTS" },
                { "dash.refresh", "REFRESH" },
                { "dash.today", "TODAY" },
                { "dash.month", "MONTH" },
                { "dash.report", "REPORT" },

                { "chart.title", "TODAY" },
                { "chart.refresh", "REFRESH" },
                { "chart.min", "MIN" },
                { "chart.avg", "AVG" },
                { "chart.max", "MAX" },

                { "month.title", "MONTH" },
                { "month.start", "Start:" },
                { "month.end", "End:" },

                { "report.title", "REPORT" },

                { "alerts.title", "SYSTEM ALERTS" },
                { "alerts.device", "DEVICE" },
                { "alerts.date", "DATE (optional)" },
                { "alerts.apply", "APPLY FILTER" },
                { "alerts.markread", "MARK READ" },
                { "alerts.markall", "MARK ALL READ" },

                { "settings.title", "SETTINGS" },
                { "settings.server", "SERVER ENDPOINT" },
                { "settings.serversave", "SAVE SERVER" },
                { "settings.serverrestart", "Server URL saved. Restart required to apply changes." },
                { "settings.language", "LANGUAGE" },
                { "settings.theme", "THEME" },
                { "settings.update", "SOFTWARE UPDATE" },
                { "settings.check", "CHECK FOR UPDATES" },
                { "settings.noupdate", "No updates available. You are running the latest version (1.0.0)." },
                { "settings.restart", "Restart to apply language." },

                { "login.welcome", "Welcome Back" },
                { "login.subtitle", "Log in to monitor your devices" },
                { "login.username", "USERNAME" },
                { "login.password", "PASSWORD" },
                { "login.server", "SERVER ENDPOINT" },
                { "login.signin", "SIGN IN" },

                { "about.title", "OCEANREMOTE MONITOR" },
                { "about.version", "Version 1.0.0" },
            };

        private static readonly System.Collections.Generic.Dictionary<string, string> Fr =
            new()
            {
                { "nav.dashboard", "TABLEAU DE BORD" },
                { "nav.alerts", "ALERTES" },
                { "nav.settings", "PARAMÈTRES" },
                { "nav.about", "À PROPOS" },
                { "nav.logout", "DÉCONNEXION" },

                { "dash.title", "TABLEAU DE BORD OCEANREMOTE" },
                { "dash.total", "APPAREILS" },
                { "dash.online", "EN LIGNE" },
                { "dash.offline", "HORS LIGNE" },
                { "dash.lastupdate", "DERNIÈRE MÀJ" },
                { "dash.alerts", "ALERTES" },
                { "dash.refresh", "RAFRAÎCHIR" },
                { "dash.today", "AUJOURD'HUI" },
                { "dash.month", "MOIS" },
                { "dash.report", "RAPPORT" },

                { "chart.title", "AUJOURD'HUI" },
                { "chart.refresh", "RAFRAÎCHIR" },
                { "chart.min", "MIN" },
                { "chart.avg", "MOY" },
                { "chart.max", "MAX" },

                { "month.title", "MOIS" },
                { "month.start", "Début :" },
                { "month.end", "Fin :" },

                { "report.title", "RAPPORT" },

                { "alerts.title", "ALERTES SYSTÈME" },
                { "alerts.device", "APPAREIL" },
                { "alerts.date", "DATE (facultatif)" },
                { "alerts.apply", "APPLIQUER" },
                { "alerts.markread", "MARQUER LU" },
                { "alerts.markall", "TOUT MARQUER LU" },

                { "settings.title", "PARAMÈTRES" },
                { "settings.server", "SERVEUR TÉLÉMÉTRIE" },
                { "settings.serversave", "ENREGISTRER" },
                { "settings.serverrestart", "URL enregistrée. Redémarrage requis pour appliquer." },
                { "settings.language", "LANGUE" },
                { "settings.theme", "THÈME" },
                { "settings.update", "MISE À JOUR" },
                { "settings.check", "VÉRIFIER MÀJ" },
                { "settings.noupdate", "Aucune mise à jour disponible. Vous utilisez la dernière version (1.0.0)." },
                { "settings.restart", "Redémarrez pour appliquer la langue." },

                { "login.welcome", "Bon Retour" },
                { "login.subtitle", "Connectez-vous pour surveiller vos appareils" },
                { "login.username", "IDENTIFIANT" },
                { "login.password", "MOT DE PASSE" },
                { "login.server", "SERVEUR TÉLÉMÉTRIE" },
                { "login.signin", "CONNEXION" },

                { "about.title", "OCEANREMOTE MONITOR" },
                { "about.version", "Version 1.0.0" },
            };
    }
}