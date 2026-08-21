using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Raylib_cs;

namespace LJTrainer.Core
{
    public static class CybershokeWebSync
    {
        public static bool IsSyncing { get; private set; } = false;
        public static string SyncStatusMessage { get; private set; } = "";
        public static double SyncStartTime { get; private set; } = 0;
        public static bool LastSyncSuccess { get; private set; } = false;

        private static Thread? _syncThread;

        public static void StartAutoSync(string? steamId = null, Action<bool, string>? onCompleted = null)
        {
            if (IsSyncing) return;

            var cs = UserProfile.Instance.Cybershoke;
            string? targetSid = !string.IsNullOrEmpty(steamId) 
                ? steamId 
                : (!string.IsNullOrEmpty(cs.SteamId64) ? cs.SteamId64 : CS2ConfigImporter.DetectLocalSteamId64());

            if (string.IsNullOrEmpty(targetSid))
            {
                onCompleted?.Invoke(false, "SteamID64 не найден. Введите SteamID в профиле или запустите CS2.");
                return;
            }

            // Save detected SteamID
            cs.SteamId64 = targetSid;

            IsSyncing = true;
            LastSyncSuccess = false;
            SyncStatusMessage = "Подключение к Cybershoke.net...";
            SyncStartTime = Raylib.GetTime();

            _syncThread = new Thread(() => RunSync(targetSid, onCompleted));
            _syncThread.SetApartmentState(ApartmentState.STA);
            _syncThread.IsBackground = true;
            _syncThread.Start();
        }

        private static void RunSync(string steamId, Action<bool, string>? onCompleted)
        {
            bool completed = false;

            void Finish(bool success, string msg)
            {
                if (completed) return;
                completed = true;

                IsSyncing = false;
                LastSyncSuccess = success;
                SyncStatusMessage = msg;

                if (success)
                {
                    UserProfile.Instance.Cybershoke.IsLinked = true;
                    UserProfile.Instance.Cybershoke.LastSyncTime = DateTime.Now;
                    UserProfile.Save();
                }

                onCompleted?.Invoke(success, msg);
            }

            // 1. Try Headless Chromium Edge WebView2 on STA UI Thread
            Form? form = null;
            WebView2? webView = null;
            System.Windows.Forms.Timer? readTimer = null;
            System.Windows.Forms.Timer? timeoutTimer = null;

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

                form = new Form
                {
                    Width = 1024,
                    Height = 768,
                    WindowState = FormWindowState.Minimized,
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None
                };

                webView = new WebView2
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(webView);

                form.Load += async (s, e) =>
                {
                    try
                    {
                        SyncStatusMessage = "Инициализация движка Edge...";
                        string userData = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "LJTrainer", "WebView2Profile");

                        var env = await CoreWebView2Environment.CreateAsync(null, userData);
                        await webView.EnsureCoreWebView2Async(env);

                        var wv = webView.CoreWebView2;
                        wv.Settings.IsScriptEnabled = true;
                        wv.Settings.AreDefaultScriptDialogsEnabled = false;

                        SyncStatusMessage = "Загрузка профиля Cybershoke...";

                        webView.NavigationCompleted += (s2, e2) =>
                        {
                            SyncStatusMessage = "Чтение данных рекордов...";

                            // UI Thread Timer: avoids background thread cross-apartment errors
                            readTimer = new System.Windows.Forms.Timer { Interval = 2500 };
                            readTimer.Tick += async (ts, te) =>
                            {
                                readTimer.Stop();
                                try
                                {
                                    string rawJson = await webView.ExecuteScriptAsync("document.body.innerText");
                                    string text = rawJson;
                                    try
                                    {
                                        text = JsonSerializer.Deserialize<string>(rawJson) ?? rawJson;
                                    }
                                    catch { }

                                    if (!string.IsNullOrWhiteSpace(text) && text.Length > 30)
                                    {
                                        var (ok, summary) = UserProfile.Instance.Cybershoke.ImportFromText(text);
                                        Finish(true, ok ? summary : "Данные успешно синхронизированы");
                                    }
                                    else
                                    {
                                        // Fallback to HTTP scraper
                                        await FallbackHttpSync(steamId, Finish);
                                    }
                                }
                                catch
                                {
                                    // Fallback to HTTP scraper
                                    await FallbackHttpSync(steamId, Finish);
                                }
                                finally
                                {
                                    try { form?.Close(); } catch { }
                                }
                            };
                            readTimer.Start();
                        };

                        string profileUrl = $"https://cybershoke.net/ru/cs2/leaderboard/kz/maps/{steamId}";
                        wv.Navigate(profileUrl);

                        // 12s overall timeout
                        timeoutTimer = new System.Windows.Forms.Timer { Interval = 12000 };
                        timeoutTimer.Tick += async (ts, te) =>
                        {
                            timeoutTimer.Stop();
                            if (!completed)
                            {
                                await FallbackHttpSync(steamId, Finish);
                                try { form?.Close(); } catch { }
                            }
                        };
                        timeoutTimer.Start();
                    }
                    catch
                    {
                        // WebView2 runtime missing or failed: fallback to direct HTTP
                        await FallbackHttpSync(steamId, Finish);
                        try { form?.Close(); } catch { }
                    }
                };

                Application.Run(form);
            }
            catch
            {
                // Fallback direct HTTP
                _ = FallbackHttpSync(steamId, Finish);
            }
        }

        private static async Task FallbackHttpSync(string steamId, Action<bool, string> finish)
        {
            try
            {
                SyncStatusMessage = "HTTP запрос к Cybershoke...";
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                client.Timeout = TimeSpan.FromSeconds(8);

                string url = $"https://cybershoke.net/ru/cs2/leaderboard/kz/maps/{steamId}";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string html = await response.Content.ReadAsStringAsync();
                    // Strip basic HTML tags for text parser
                    string cleanText = Regex.Replace(html, "<.*?>", " ");
                    cleanText = System.Net.WebUtility.HtmlDecode(cleanText);

                    var (ok, summary) = UserProfile.Instance.Cybershoke.ImportFromText(cleanText);
                    finish(ok, ok ? summary : "Данные получены");
                }
                else
                {
                    finish(false, $"Cybershoke HTTP статус: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                finish(false, $"Ошибка синхронизации: {ex.Message}");
            }
        }
    }
}
