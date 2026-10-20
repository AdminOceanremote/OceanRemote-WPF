using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OceanMonitor2.Services
{
    public class ApiService
    {
        public static string BaseUrl => AppSettings.ServerUrl;

        private readonly HttpClient _http;
        public string? Token { get; private set; }
        public string? Username { get; private set; }

        public ApiService()
        {
            var url = BaseUrl.TrimEnd('/') + "/";
            _http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(120) };
        }

        public async Task<(bool ok, string? error)> LoginAsync(string username, string password)
        {
            var body = JsonSerializer.Serialize(new { username, password });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.PostAsync("/api/app/login/", content);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    var err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "Login failed";
                    return (false, err);
                }

                using var doc2 = JsonDocument.Parse(json);
                Token = doc2.RootElement.GetProperty("token").GetString();
                Username = doc2.RootElement.GetProperty("user").GetProperty("username").GetString();
                _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", Token);
                return (true, null);
            }
            catch (Exception)
            {
                return (false, "Cannot connect. Please check your internet connection.");
            }
        }

        public async Task<string> GetDevicesAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/api/app/devices/");
                return await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                return "";
            }
        }

        public async Task<string> GetHistoryDayAsync(string deviceId, string date)
        {
            try
            {
                var resp = await _http.GetAsync($"/api/app/history/{deviceId}/?date={date}");
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        public async Task<string> GetHistoryRangeAsync(string deviceId, string start, string end)
        {
            try
            {
                var resp = await _http.GetAsync($"/api/app/history/{deviceId}/range/?start={start}&end={end}");
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        public async Task<string> GetReportAsync(string deviceId, string start, string end)
        {
            try
            {
                var resp = await _http.GetAsync($"/api/app/report-fast/{deviceId}/?start={start}&end={end}");
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        public async Task<string> GetNotificationsAsync(string? deviceId = null, string? date = null)
        {
            try
            {
                var url = "/api/app/notifications/?";
                if (!string.IsNullOrEmpty(deviceId)) url += $"device_id={deviceId}&";
                if (!string.IsNullOrEmpty(date)) url += $"date={date}&";
                var resp = await _http.GetAsync(url);
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        public async Task<string> MarkNotificationReadAsync(int notificationId)
        {
            try
            {
                var resp = await _http.PostAsync($"/api/app/notifications/{notificationId}/read/", null);
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        public async Task<string> MarkAllNotificationsReadAsync()
        {
            try
            {
                var resp = await _http.PostAsync("/api/app/notifications/mark-all-read/", null);
                return await resp.Content.ReadAsStringAsync();
            }
            catch { return ""; }
        }

        // ===== SAVED CREDENTIALS =====
        private static readonly string CredFile =
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OceanRemoteMonitor", "creds.dat");

        public static (string? user, string? pass) LoadSavedCredentials()
        {
            try
            {
                if (!System.IO.File.Exists(CredFile)) return (null, null);
                var enc = System.IO.File.ReadAllBytes(CredFile);
                var bytes = System.Security.Cryptography.ProtectedData.Unprotect(
                    enc, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                var parts = text.Split('|');
                if (parts.Length == 2) return (parts[0], parts[1]);
            }
            catch { }
            return (null, null);
        }

        public static void SaveCredentials(string user, string pass)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CredFile)!);
                var bytes = System.Text.Encoding.UTF8.GetBytes(user + "|" + pass);
                var enc = System.Security.Cryptography.ProtectedData.Protect(
                    bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                System.IO.File.WriteAllBytes(CredFile, enc);
            }
            catch { }
        }

        public static void ClearCredentials()
        {
            try { if (System.IO.File.Exists(CredFile)) System.IO.File.Delete(CredFile); }
            catch { }
        }

        public async Task<(bool ok, string? error)> UpdateDeviceAsync(string deviceId, string name, double? offset)
        {
            var body = JsonSerializer.Serialize(new { name = name, offset = offset });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var resp = await _http.PostAsync($"/api/app/device/{deviceId}/update/", content);
                if (resp.IsSuccessStatusCode) return (true, null);
                var json = await resp.Content.ReadAsStringAsync();
                return (false, json);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public async Task<(bool ok, string? error)> DeleteDeviceAsync(string deviceId)
        {
            try
            {
                var resp = await _http.PostAsync($"/api/app/device/{deviceId}/delete/", null);
                if (resp.IsSuccessStatusCode) return (true, null);
                var json = await resp.Content.ReadAsStringAsync();
                return (false, json);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}