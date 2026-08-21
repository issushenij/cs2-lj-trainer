using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public class SettingsDrawer
    {
        public bool IsOpen { get; set; } = false;

        private float _openAnimProgress = 0.0f;
        private float _scrollY = 0f;
        private float _targetScrollY = 0f;
        private string _importStatusMessage = "";
        private bool _importStatusSuccess = false;

        public void Draw(int screenWidth, int screenHeight)
        {
            if (!IsOpen)
            {
                _openAnimProgress = 0.0f;
                return;
            }

            // Smooth entrance animation
            _openAnimProgress += Raylib.GetFrameTime() * 8.0f;
            if (_openAnimProgress > 1.0f) _openAnimProgress = 1.0f;

            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            Vector2 mouse = Raylib.GetMousePosition();

            // Backdrop dimming overlay with frosted tint
            byte dimAlpha = (byte)(230 * _openAnimProgress);
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color((byte)4, (byte)7, (byte)12, dimAlpha));

            int modalW = Math.Min((int)(840 * MathF.Min(scale, 1.15f)), screenWidth - 32);
            int modalH = Math.Min((int)(640 * MathF.Min(scale, 1.15f)), screenHeight - 40);

            int animOffsetY = (int)((1.0f - _openAnimProgress) * 16.0f);
            int modalX = (screenWidth - modalW) / 2;
            int modalY = (screenHeight - modalH) / 2 + animOffsetY;

            // Optical diffusion blur behind modal
            Theme.DrawBackdropBlur(modalX, modalY, modalW, modalH, 16);

            // Technical container box
            Theme.DrawTechnicalBox(modalX, modalY, modalW, modalH, null, Theme.Border, Theme.BgDark, true);

            // Top Header (Height: 48px)
            int headerH = (int)(48 * scale);
            Raylib.DrawRectangle(modalX, modalY, modalW, headerH, Theme.BgPanelHeader);
            Raylib.DrawLine(modalX, modalY + headerH, modalX + modalW, modalY + headerH, Theme.Border);

            // Specular top glass edge
            Raylib.DrawLine(modalX + 1, modalY + 1, modalX + modalW - 2, modalY + 1, new Color(255, 255, 255, 30));

            // Header Title
            Theme.DrawText("[ SETTINGS // CONFIGURATION ]", modalX + 18, modalY + (headerH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonCyan);

            // Close button [ESC]
            int closeW = (int)(76 * scale);
            int closeH = (int)(28 * scale);
            int closeX = modalX + modalW - closeW - 14;
            int closeY = modalY + (headerH - closeH) / 2;

            if (Theme.DrawButton(closeX, closeY, closeW, closeH, "[ESC]", false, 11) ||
                Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                IsOpen = false;
                AppConfig.Save();
                return;
            }

            // Close on click outside modal
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (mouse.X < modalX || mouse.X > modalX + modalW || mouse.Y < modalY || mouse.Y > modalY + modalH)
                {
                    IsOpen = false;
                    AppConfig.Save();
                    return;
                }
            }

            // Single Scrollable Content Box (No tab buttons)
            int contentX = modalX + 16;
            int contentY = modalY + headerH + (int)(12 * scale);
            int contentW = modalW - 32;
            int contentH = modalH - headerH - (int)(24 * scale);

            Theme.DrawTechnicalBox(contentX, contentY, contentW, contentH, null, Theme.Border, Theme.BgPanel, false);

            // Mouse wheel scroll within settings content
            if (mouse.X >= contentX && mouse.X <= contentX + contentW && mouse.Y >= contentY && mouse.Y <= contentY + contentH)
            {
                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0)
                {
                    _targetScrollY -= wheel * (65 * scale);
                }
            }

            _scrollY += (_targetScrollY - _scrollY) * 0.35f;

            Raylib.BeginScissorMode(contentX + 2, contentY + 2, contentW - 4, contentH - 4);

            int pad = (int)(16 * scale);
            int innerX = contentX + pad;
            int curY = contentY + pad - (int)_scrollY;
            int innerW = contentW - pad * 2 - 8; // Leave room for scrollbar
            int gap = (int)(18 * scale);

            // 1. Section: Mouse & Sensitivity
            curY = DrawMouseSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            // 2. Section: Trainer & Cadence
            curY = DrawTrainerSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            // 3. Section: Audio & Feedback
            curY = DrawAudioSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            // 4. Section: Visuals & UI Scale
            curY = DrawVisualsSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            // 5. Section: CS2 & Cybershoke Live Sync
            curY = DrawSyncSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            // 6. Section: Software Updates & Version Info
            curY = DrawUpdateSection(innerX, curY, innerW, scale, cfg);
            curY += gap;

            Raylib.EndScissorMode();

            int totalHeight = (curY + (int)_scrollY) - (contentY + pad);
            float maxScroll = Math.Max(0, totalHeight - (contentH - pad * 2));
            _targetScrollY = Math.Clamp(_targetScrollY, 0, maxScroll);
            if (maxScroll <= 0) _scrollY = 0;

            // Draw clean scrollbar
            if (maxScroll > 0)
            {
                int sbX = contentX + contentW - 8;
                int sbY = contentY + 6;
                int sbH = contentH - 12;
                float thumbPct = Math.Clamp((float)(contentH - pad * 2) / totalHeight, 0.12f, 1.0f);
                int thumbH = (int)(sbH * thumbPct);
                int thumbY = sbY + (int)((sbH - thumbH) * (_scrollY / maxScroll));

                Raylib.DrawRectangle(sbX, sbY, 3, sbH, new Color(255, 255, 255, 18));
                Raylib.DrawRectangle(sbX, thumbY, 3, thumbH, Theme.NeonCyan);
            }
        }

        // =========================================================================
        // SECTION 1: SENSITIVITY & MOUSE
        // =========================================================================
        private int DrawMouseSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(30 * scale);

            Theme.DrawText("// 1. MOUSE SENSITIVITY & IN-GAME SENS:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            // Step buttons & sens value
            int stepW = (int)(40 * scale);
            int sensDisplayW = (int)(110 * scale);

            if (Theme.DrawButton(x, curY, stepW, btnH, "-", false, 14))
            {
                cfg.Sensitivity = MathF.Max(0.1f, MathF.Round((cfg.Sensitivity - 0.05f) * 100f) / 100f);
                AppConfig.Save();
            }

            Raylib.DrawRectangle(x + stepW + 6, curY, sensDisplayW, btnH, Theme.BgDark);
            Raylib.DrawRectangleLines(x + stepW + 6, curY, sensDisplayW, btnH, Theme.NeonCyan);
            string sensStr = cfg.Sensitivity.ToString("F2");
            int sw = Theme.MeasureDisplayText(sensStr, 16);
            Theme.DrawDisplayText(sensStr, x + stepW + 6 + (sensDisplayW - sw) / 2, curY + 6, 16, Theme.NeonCyan);

            if (Theme.DrawButton(x + stepW + sensDisplayW + 12, curY, stepW, btnH, "+", false, 14))
            {
                cfg.Sensitivity = MathF.Min(10.0f, MathF.Round((cfg.Sensitivity + 0.05f) * 100f) / 100f);
                AppConfig.Save();
            }

            // Quick Sens Presets
            int presetStartX = x + stepW * 2 + sensDisplayW + 18;
            int presetAvailW = x + w - presetStartX;
            float[] sensPresets = { 0.8f, 1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.5f };
            int pBtnW = (presetAvailW - (sensPresets.Length - 1) * 6) / sensPresets.Length;

            for (int i = 0; i < sensPresets.Length; i++)
            {
                float sp = sensPresets[i];
                bool active = MathF.Abs(cfg.Sensitivity - sp) < 0.01f;
                int bx = presetStartX + i * (pBtnW + 6);
                if (Theme.DrawButton(bx, curY, pBtnW, btnH, sp.ToString("F1"), active, 11))
                {
                    cfg.Sensitivity = sp;
                    AppConfig.Save();
                }
            }

            curY += btnH + (int)(10 * scale);

            // CS2 Config Auto-Import Row
            int halfW = (w - 12) / 2;
            if (Theme.DrawButton(x, curY, halfW, btnH, "Авто-импорт sens из CS2", false, 11))
            {
                var res = CS2ConfigImporter.TryAutoImport();
                if (res.Success)
                {
                    cfg.Sensitivity = res.Sensitivity;
                    cfg.YawFactor = res.YawFactor;
                    _importStatusMessage = $"[OK] {res.Message}";
                    _importStatusSuccess = true;
                    AppConfig.Save();
                }
                else
                {
                    _importStatusMessage = $"[ERROR] {res.Message}";
                    _importStatusSuccess = false;
                }
            }

            // Reversal Trigger Mode
            int trigW = (halfW - 8) / 2;
            int trigX = x + halfW + 12;
            if (Theme.DrawButton(trigX, curY, trigW, btnH, "Разворот: Мышь", cfg.FreestyleTrigger == ReversalTriggerMode.ByMouseMovement, 11))
            {
                cfg.FreestyleTrigger = ReversalTriggerMode.ByMouseMovement;
                AppConfig.Save();
            }
            if (Theme.DrawButton(trigX + trigW + 8, curY, trigW, btnH, "Разворот: A / D", cfg.FreestyleTrigger == ReversalTriggerMode.ByKeyPress, 11))
            {
                cfg.FreestyleTrigger = ReversalTriggerMode.ByKeyPress;
                AppConfig.Save();
            }

            if (!string.IsNullOrEmpty(_importStatusMessage))
            {
                curY += btnH + 6;
                Color sc = _importStatusSuccess ? Theme.NeonGreen : Theme.NeonRed;
                Theme.DrawText(_importStatusMessage, x, curY, 10, sc);
            }

            return curY + btnH;
        }

        // =========================================================================
        // SECTION 2: TRAINER & CADENCE
        // =========================================================================
        private int DrawTrainerSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(30 * scale);

            Theme.DrawText("// 2. TRAINER TARGET STRAFES & METRONOME:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            int[] strafePresets = { 4, 6, 7, 8, 9, 10, 12 };
            int sBtnW = (w - (strafePresets.Length - 1) * 8) / strafePresets.Length;

            for (int i = 0; i < strafePresets.Length; i++)
            {
                int count = strafePresets[i];
                bool active = (cfg.TargetStrafes == count);
                int bx = x + i * (sBtnW + 8);
                if (Theme.DrawButton(bx, curY, sBtnW, btnH, $"{count} str", active, 11))
                {
                    cfg.TargetStrafes = count;
                    AppConfig.Save();
                }
            }

            curY += btnH + (int)(10 * scale);

            Theme.DrawText($"// TARGET DURATION: {cfg.TargetStrafeDurationMs:F0}ms ({cfg.CalculatedMetronomeBpm} BPM):", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            float[] durPresets = { 75f, 85f, 90f, 95f, 105f, 115f, 125f };
            int dBtnW = (w - (durPresets.Length - 1) * 8) / durPresets.Length;

            for (int i = 0; i < durPresets.Length; i++)
            {
                float d = durPresets[i];
                bool active = MathF.Abs(cfg.TargetStrafeDurationMs - d) < 1.0f;
                int bx = x + i * (dBtnW + 8);
                if (Theme.DrawButton(bx, curY, dBtnW, btnH, $"{d:F0}ms", active, 11))
                {
                    cfg.TargetStrafeDurationMs = d;
                    AppConfig.Save();
                }
            }

            curY += btnH + (int)(10 * scale);

            int halfW = (w - 12) / 2;
            string metroLabel = cfg.MetronomeEnabled ? "Метроном: [ ВКЛЮЧЕН ]" : "Метроном: [ ВЫКЛЮЧЕН ]";
            if (Theme.DrawButton(x, curY, halfW, btnH, metroLabel, cfg.MetronomeEnabled, 11))
            {
                cfg.MetronomeEnabled = !cfg.MetronomeEnabled;
                AppConfig.Save();
            }

            return curY + btnH;
        }

        // =========================================================================
        // SECTION 3: AUDIO & FEEDBACK
        // =========================================================================
        private int DrawAudioSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(30 * scale);

            Theme.DrawText("// 3. MASTER AUDIO & VOLUME:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            int halfW = (w - 12) / 2;
            string soundLabel = cfg.SoundEnabled ? "Звуковые эффекты: [ ВКЛ ]" : "Звуковые эффекты: [ ВЫКЛ ]";
            if (Theme.DrawButton(x, curY, halfW, btnH, soundLabel, cfg.SoundEnabled, 11))
            {
                cfg.SoundEnabled = !cfg.SoundEnabled;
                AppConfig.Save();
            }

            // Volume Buttons
            int volStartX = x + halfW + 12;
            int volAvailW = w - halfW - 12;
            float[] vols = { 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };
            int vBtnW = (volAvailW - (vols.Length - 1) * 6) / vols.Length;

            for (int i = 0; i < vols.Length; i++)
            {
                float v = vols[i];
                bool active = MathF.Abs(cfg.MasterVolume - v) < 0.05f;
                int bx = volStartX + i * (vBtnW + 6);
                if (Theme.DrawButton(bx, curY, vBtnW, btnH, $"{(int)(v * 100)}%", active, 11))
                {
                    cfg.MasterVolume = v;
                    AppConfig.Save();
                }
            }

            return curY + btnH;
        }

        // =========================================================================
        // SECTION 4: VISUALS & UI
        // =========================================================================
        private int DrawVisualsSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(30 * scale);

            Theme.DrawText("// 4. COLOR THEMES & PALETTES:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            ColorTheme[] themes = { ColorTheme.CyberCLI, ColorTheme.AmberCRT, ColorTheme.PhosphorMatrix, ColorTheme.OLEDMonochrome };
            string[] themeNames = { "CYAN NEON", "AMBER CRT", "MATRIX GREEN", "OLED MONO" };
            int tBtnW = (w - (themes.Length - 1) * 8) / themes.Length;

            for (int i = 0; i < themes.Length; i++)
            {
                var th = themes[i];
                bool active = (cfg.Theme == th);
                int bx = x + i * (tBtnW + 8);
                if (Theme.DrawButton(bx, curY, tBtnW, btnH, themeNames[i], active, 10))
                {
                    cfg.Theme = th;
                    AppConfig.Save();
                }
            }

            curY += btnH + (int)(12 * scale);

            Theme.DrawText("// UI INTERFACE SCALE:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            float[] scales = { 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.5f };
            int scBtnW = (w - (scales.Length - 1) * 8) / scales.Length;

            for (int i = 0; i < scales.Length; i++)
            {
                float sc = scales[i];
                bool active = MathF.Abs(cfg.UiScale - sc) < 0.03f;
                int bx = x + i * (scBtnW + 8);
                if (Theme.DrawButton(bx, curY, scBtnW, btnH, $"{sc:F1}x", active, 11))
                {
                    cfg.UiScale = sc;
                    AppConfig.Save();
                }
            }

            curY += btnH + (int)(10 * scale);

            int halfW = (w - 12) / 2;

            string tipsLabel = cfg.ShowTooltips ? "Подсказки: [ ВКЛ ]" : "Подсказки: [ ВЫКЛ ]";
            if (Theme.DrawButton(x, curY, halfW, btnH, tipsLabel, cfg.ShowTooltips, 11))
            {
                cfg.ShowTooltips = !cfg.ShowTooltips;
                AppConfig.Save();
            }

            string crtLabel = cfg.ShowCrtScanlines ? "CRT Scanlines: [ ВКЛ ]" : "CRT Scanlines: [ ВЫКЛ ]";
            if (Theme.DrawButton(x + halfW + 12, curY, halfW, btnH, crtLabel, cfg.ShowCrtScanlines, 11))
            {
                cfg.ShowCrtScanlines = !cfg.ShowCrtScanlines;
                AppConfig.Save();
            }

            return curY + btnH;
        }

        // =========================================================================
        // SECTION 5: CS2 & CYBERSHOKE SYNC
        // =========================================================================
        private int DrawSyncSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(32 * scale);

            var prof = UserProfile.Instance;
            var cs = prof.Cybershoke;

            Theme.DrawText("// 5. STEAM & CYBERSHOKE LIVE SYNC:", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            string sidText = !string.IsNullOrEmpty(cs.SteamId64) ? $"STEAM_ID: {cs.SteamId64} [ LINKED ]" : "STEAM: [ NOT LINKED ]";
            Theme.DrawTechnicalBox(x, curY, w, btnH, null, Theme.Border, Theme.BgDark, false);
            Theme.DrawText(sidText, x + 14, curY + (btnH - Theme.GetScaledFontSize(11)) / 2, 11, !string.IsNullOrEmpty(cs.SteamId64) ? Theme.NeonGreen : Theme.NeonOrange);

            curY += btnH + (int)(10 * scale);

            string syncLabel = CybershokeWebSync.IsSyncing ? "Синхронизация профиля..." : "Синхронизировать профиль Cybershoke / CS2";
            if (Theme.DrawButton(x, curY, w, btnH, syncLabel, CybershokeWebSync.IsSyncing, 11, enabled: !CybershokeWebSync.IsSyncing))
            {
                CybershokeWebSync.StartAutoSync(cs.SteamId64, (ok, msg) =>
                {
                    _importStatusMessage = ok ? $"[OK] {msg}" : $"[ERROR] {msg}";
                    _importStatusSuccess = ok;
                });
            }

            curY += btnH + (int)(10 * scale);

            // CS2 Console Watcher Status
            Theme.DrawText("// CS2 CONSOLE WATCHER (-condebug):", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            string logFileName = !string.IsNullOrEmpty(CS2ConsoleWatcher.ActiveLogPath) ? Path.GetFileName(CS2ConsoleWatcher.ActiveLogPath) : "console.log";
            string logStatus = (!string.IsNullOrEmpty(CS2ConsoleWatcher.ActiveLogPath) && File.Exists(CS2ConsoleWatcher.ActiveLogPath))
                ? $"LOG: {logFileName} [ ACTIVE ] (Захвачено: {CS2ConsoleWatcher.EventsCaptured} соб.)"
                : "LOG: [ НЕ НАЙДЕН — ДОБАВЬТЕ -condebug В CS2 ]";

            Color logCol = (!string.IsNullOrEmpty(CS2ConsoleWatcher.ActiveLogPath) && File.Exists(CS2ConsoleWatcher.ActiveLogPath))
                ? Theme.NeonGreen 
                : Theme.NeonOrange;

            Theme.DrawTechnicalBox(x, curY, w, btnH, null, Theme.Border, Theme.BgDark, false);
            Theme.DrawText(logStatus, x + 14, curY + (btnH - Theme.GetScaledFontSize(10)) / 2, 10, logCol);
            curY += btnH + (int)(10 * scale);

            int halfW = (w - 12) / 2;

            if (Theme.DrawButton(x, curY, halfW, btnH, "Выбрать console.log...", false, 10))
            {
                OpenConsoleLogFileDialog();
            }

            if (Theme.DrawButton(x + halfW + 12, curY, halfW, btnH, "Пересканировать лог", false, 10))
            {
                CS2ConsoleWatcher.ReScanFullLogFromBeginning();
                _importStatusMessage = "[OK] Полное сканирование console.log запущено с начала файла.";
                _importStatusSuccess = true;
            }
            curY += btnH + (int)(10 * scale);

            string capLabel = cfg.CaptureAllConsoleJumps ? "Захват: [ ВСЕ ПРЫЖКИ ИЗ КОНСОЛИ ]" : "Захват: [ ТОЛЬКО МОЙ НИК ]";
            if (Theme.DrawButton(x, curY, w, btnH, capLabel, cfg.CaptureAllConsoleJumps, 10))
            {
                cfg.CaptureAllConsoleJumps = !cfg.CaptureAllConsoleJumps;
                AppConfig.Save();
                CS2ConsoleWatcher.ReScanFullLogFromBeginning();
            }

            if (!string.IsNullOrEmpty(_importStatusMessage))
            {
                curY += btnH + 6;
                Color sc = _importStatusSuccess ? Theme.NeonGreen : Theme.NeonRed;
                Theme.DrawText(_importStatusMessage, x, curY, 10, sc);
            }

            return curY + btnH;
        }

        private static void OpenConsoleLogFileDialog()
        {
            var t = new Thread(() =>
            {
                try
                {
                    using var ofd = new System.Windows.Forms.OpenFileDialog
                    {
                        Title = "Выберите файл console.log Counter-Strike 2",
                        Filter = "CS2 Console Log (console.log;*.log;*.txt)|console.log;*.log;*.txt|Все файлы (*.*)|*.*",
                        CheckFileExists = true
                    };
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        AppConfig.Instance.CustomConsoleLogPath = ofd.FileName;
                        AppConfig.Save();
                        CS2ConsoleWatcher.ReScanFullLogFromBeginning();
                    }
                }
                catch { }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        // =========================================================================
        // SECTION 6: SOFTWARE UPDATES & VERSION
        // =========================================================================
        private int DrawUpdateSection(int x, int y, int w, float scale, AppConfig cfg)
        {
            int curY = y;
            int btnH = (int)(32 * scale);

            Theme.DrawText($"// 6. VERSION & UPDATES ({UpdateManager.CurrentVersion}):", x, curY, 11, Theme.NeonGold);
            curY += (int)(22 * scale);

            string updateBtnLabel = UpdateManager.IsChecking ? "Проверка обновлений..." : "Проверить обновления GitHub";
            if (Theme.DrawButton(x, curY, w, btnH, updateBtnLabel, false, 11, enabled: !UpdateManager.IsChecking))
            {
                _ = UpdateManager.CheckForUpdatesAsync(false);
            }

            if (!string.IsNullOrEmpty(UpdateManager.StatusMessage))
            {
                curY += btnH + 6;
                Theme.DrawText(UpdateManager.StatusMessage, x, curY, 10, Theme.NeonCyan);
            }

            return curY + btnH;
        }
    }
}
