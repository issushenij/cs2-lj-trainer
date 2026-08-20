using System;
using System.Collections.Generic;
using System.IO;
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
            SyncStatusMessage = "Запуск Chromium Edge WebView2...";
            SyncStartTime = Raylib.GetTime();

            _syncThread = new Thread(() => RunSyncForm(targetSid, onCompleted));
            _syncThread.SetApartmentState(ApartmentState.STA);
            _syncThread.IsBackground = true;
            _syncThread.Start();
        }

        private static void RunSyncForm(string steamId, Action<bool, string>? onCompleted)
        {
            Form? form = null;
            WebView2? webView = null;
            bool completed = false;

            void Finish(bool success, string msg)
            {
                if (completed) return;
                completed = true;

                IsSyncing = false;
                LastSyncSuccess = success;
                SyncStatusMessage = msg;

                try
                {
                    if (form != null && !form.IsDisposed)
                    {
                        if (form.InvokeRequired)
                            form.Invoke(new Action(() => form.Close()));
                        else
                            form.Close();
                    }
                }
                catch { }

                onCompleted?.Invoke(success, msg);
            }

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

                        SyncStatusMessage = "Подключение к Cybershoke.net...";

                        webView.NavigationCompleted += (s2, e2) =>
                        {
                            SyncStatusMessage = "Страница загружена, чтение данных...";

                            Task.Delay(3000).ContinueWith(async _ =>
                            {
                                try
                                {
                                    string rawJson = await webView.ExecuteScriptAsync("document.body.innerText");
                                    string text = rawJson;
                                    try
                                    {
                                        text = JsonSerializer.Deserialize<string>(rawJson) ?? rawJson;
                                    }
                                    catch { }

                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        var (ok, summary) = UserProfile.Instance.Cybershoke.ImportFromText(text);
                                        Finish(true, ok ? summary : "Данные получены");
                                    }
                                    else
                                    {
                                        Finish(false, "Пустой ответ со страницы");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Finish(false, $"Ошибка скрипта: {ex.Message}");
                                }
                            });
                        };

                        string profileUrl = $"https://cybershoke.net/ru/cs2/leaderboard/kz/maps/{steamId}";
                        wv.Navigate(profileUrl);

                        // 15 sec timeout fallback
                        _ = Task.Delay(15000).ContinueWith(_ =>
                        {
                            if (!completed)
                            {
                                Finish(false, "Таймаут (15 сек)");
                            }
                        });

                    }
                    catch (Exception ex)
                    {
                        Finish(false, $"Ошибка WebView2: {ex.Message}");
                    }
                };

                Application.Run(form);
            }
            catch (Exception ex)
            {
                Finish(false, $"Сбой: {ex.Message}");
            }
        }
    }
}
