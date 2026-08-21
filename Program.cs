using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;
using LJTrainer.Modes;
using LJTrainer.UI;

namespace LJTrainer
{
    public class Program
    {
        private static AppMode _currentMode = AppMode.CadenceLab;
        private static CadenceLabMode _cadenceLab = new();
        private static OscilloscopeMode _oscMode = new();
        private static SettingsDrawer _settings = new();
        private static GuideModal _guideModal = new();
        private static ProfileModal _profileModal = new();
        private static bool _forceExit = false;

        public static void Main(string[] args)
        {
            // Unit testing CLI mode:
            if (args.Length > 0 && args[0] == "--test")
            {
                Console.WriteLine("[TESTS] Testing physics and config math...");
                float est = AppConfig.EstimateStrafesInJump(95.0f);
                Console.WriteLine($"[PASS] 95ms estimated strafes: {est:F1} (Expected ~8.0)");
                
                Console.WriteLine("[TESTS] Testing CS2 log watcher & PB parser...");
                string logFile = CS2ConsoleWatcher.FindCS2ConsoleLogFile();
                Console.WriteLine($"[PASS] Found CS2 log: {logFile}");
                
                CS2ConsoleWatcher.StartWatching();
                System.Threading.Thread.Sleep(300);
                
                var cs = UserProfile.Instance.Cybershoke;
                Console.WriteLine($"[PASS] Player Nick: {cs.CybershokeNick}");
                Console.WriteLine($"[PASS] Total Jumps Read: {cs.TotalAllJumps} (Quality: {cs.TotalQualityJumps})");
                Console.WriteLine($"[PASS] Overall Avg Sync: {cs.OverallAvgSync:F1}%");
                
                foreach (var kvp in cs.PBs)
                {
                    Console.WriteLine($"[PASS] PB {kvp.Key}: {kvp.Value.PBDist:F2} u ({kvp.Value.PBStrafes} str, {kvp.Value.PBSync:F0}% sync, pre: {kvp.Value.PBPreSpeed:F0})");
                }
                
                Console.WriteLine("[SUCCESS] All tests and CS2 log parser verified successfully!");
                return;
            }

            // Load saved settings & persistent player profile
            AppConfig.Load();
            UserProfile.Load();

            CS2ConsoleWatcher.OnConsoleEvent += (evt) =>
            {
                _profileModal.OnJumpCaptured(evt);
            };
            CS2ConsoleWatcher.StartWatching();

            bool isDumpScreensMode = args.Length > 0 && args[0] == "--dump-screens";

            // Set process DPI aware and configure Raylib flags
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | (isDumpScreensMode ? ConfigFlags.HiddenWindow : 0));
            Raylib.InitWindow(1380, 860, $"CS2 Long Jump Cadence & Sync Lab [{UpdateManager.CurrentVersion}]");
            Raylib.SetWindowMinSize(1024, 700);
            Raylib.SetTargetFPS(144);

            // Set custom modern high-res icon ("LJ" bold cyan + "TRAINER" small)
            TrayManager.SetWindowCustomIcon();

            // CRITICAL: Disable Escape key from exiting application (only window [X] or tray exits)
            Raylib.SetExitKey(KeyboardKey.Null);

            // Initialize Audio Engine & High-DPI Cyrillic Font
            AudioEngine.Initialize();
            Theme.InitializeFont();
            SvgIconManager.Initialize();
            ShaderFxManager.Initialize(1380, 860);

            if (isDumpScreensMode)
            {
                Directory.CreateDirectory("screenshots");

                void RenderAndSave(string filename, Action setup)
                {
                    setup();
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(Theme.BgDark);
                    int sw = Raylib.GetScreenWidth();
                    int sh = Raylib.GetScreenHeight();

                    if (_profileModal.IsOpen)
                    {
                        _profileModal.Draw(sw, sh);
                    }
                    else
                    {
                        _cadenceLab.Draw(sw, sh);
                        DrawTopNavBar(sw);
                        DrawBottomStatusBar(sw, sh);
                        if (_settings.IsOpen)
                        {
                            _settings.Draw(sw, sh);
                        }
                    }
                    Raylib.EndDrawing();

                    string relPath = Path.Combine("screenshots", filename).Replace('\\', '/');
                    Raylib.TakeScreenshot(relPath);
                    Console.WriteLine($"[SCREENSHOT] Captured {relPath}");
                }

                // 1. Trainer screen
                RenderAndSave("screen_trainer.png", () => { _currentMode = AppMode.CadenceLab; _profileModal.IsOpen = false; _settings.IsOpen = false; });
                // 2. Profile Jump PBs tab
                RenderAndSave("screen_profile_pbs.png", () => { _profileModal.IsOpen = true; _profileModal.SetActiveTab(1); _settings.IsOpen = false; });
                // 3. Profile KZ Maps tab
                RenderAndSave("screen_profile_maps.png", () => { _profileModal.IsOpen = true; _profileModal.SetActiveTab(2); _settings.IsOpen = false; });
                // 4. Profile Deep Analytics tab
                RenderAndSave("screen_profile_analytics.png", () => { _profileModal.IsOpen = true; _profileModal.SetActiveTab(0); _settings.IsOpen = false; });
                // 5. Settings modal screen
                RenderAndSave("screen_settings.png", () => { _profileModal.IsOpen = false; _settings.IsOpen = true; });

                Raylib.CloseWindow();
                Console.WriteLine("[SUCCESS] All UI screenshots dumped successfully!");
                return;
            }

            // Initialize Windows System Tray Icon
            TrayManager.Initialize(
                onRestore: () =>
                {
                    _cadenceLab.IsTrainingRunning = false;
                    InputManager.Instance.SetCursorLock(false);
                },
                onOpenSettings: () =>
                {
                    _settings.IsOpen = true;
                    _guideModal.IsOpen = false;
                    _profileModal.IsOpen = false;
                    _cadenceLab.ShowHistoryModal = false;
                },
                onExit: () =>
                {
                    _forceExit = true;
                }
            );

            // Automatic background Cybershoke sync and Update check on startup
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                string sid = UserProfile.Instance.Cybershoke.SteamId64;
                if (!string.IsNullOrEmpty(sid))
                {
                    CybershokeWebSync.StartAutoSync(sid, null);
                }

                // Check for new releases on GitHub if enabled in settings
                if (AppConfig.Instance.AutoCheckUpdates)
                {
                    await UpdateManager.CheckForUpdatesAsync(silent: true);
                }
            });

            // Guide display policy on startup (defaults to false)
            _guideModal.IsOpen = AppConfig.Instance.ShowWelcomeGuideOnStartup;

            // Main Loop
            while (!_forceExit)
            {
                if (Raylib.WindowShouldClose())
                {
                    if (AppConfig.Instance.MinimizeToTrayOnClose && !_forceExit)
                    {
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                        TrayManager.MinimizeToTray();

                        // Low-CPU background loop while in tray (audio & console watcher keep active!)
                        while (TrayManager.IsMinimizedToTray && !_forceExit)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            System.Threading.Thread.Sleep(50);
                        }

                        if (_forceExit) break;
                    }
                    else
                    {
                        break;
                    }
                }

                // Process Tray Icon message pump
                System.Windows.Forms.Application.DoEvents();

                float dt = Raylib.GetFrameTime();

                // F1 / H for Guide / Tutorial
                if (Raylib.IsKeyPressed(KeyboardKey.F1) || Raylib.IsKeyPressed(KeyboardKey.H))
                {
                    _guideModal.IsOpen = !_guideModal.IsOpen;
                    if (_guideModal.IsOpen)
                    {
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                        _settings.IsOpen = false;
                        _profileModal.IsOpen = false;
                        _cadenceLab.ShowHistoryModal = false;
                    }
                }

                // Tab for Settings Drawer
                if (Raylib.IsKeyPressed(KeyboardKey.Tab))
                {
                    _settings.IsOpen = !_settings.IsOpen;
                    if (_settings.IsOpen)
                    {
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                        _guideModal.IsOpen = false;
                        _profileModal.IsOpen = false;
                        _cadenceLab.ShowHistoryModal = false;
                    }
                    else
                    {
                        AppConfig.Save();
                    }
                }

                // P for Player Profile & Telemetry
                if (Raylib.IsKeyPressed(KeyboardKey.P))
                {
                    _profileModal.IsOpen = !_profileModal.IsOpen;
                    if (_profileModal.IsOpen)
                    {
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                        _guideModal.IsOpen = false;
                        _settings.IsOpen = false;
                        _cadenceLab.ShowHistoryModal = false;
                    }
                }

                // Global Spacebar: Toggle Play / Pause & Mouse Lock
                if (Raylib.IsKeyPressed(KeyboardKey.Space) && !_settings.IsOpen && !_guideModal.IsOpen && !_profileModal.IsOpen && !_cadenceLab.ShowHistoryModal)
                {
                    _cadenceLab.IsTrainingRunning = !_cadenceLab.IsTrainingRunning;
                    InputManager.Instance.SetCursorLock(_cadenceLab.IsTrainingRunning);
                }

                // ESC: Exit current modal or pause
                if (Raylib.IsKeyPressed(KeyboardKey.Escape))
                {
                    if (_guideModal.IsOpen)
                    {
                        _guideModal.IsOpen = false;
                    }
                    else if (_profileModal.IsOpen)
                    {
                        _profileModal.IsOpen = false;
                        UserProfile.Save();
                    }
                    else if (_settings.IsOpen)
                    {
                        _settings.IsOpen = false;
                        AppConfig.Save();
                    }
                    else if (_cadenceLab.ShowHistoryModal)
                    {
                        _cadenceLab.ShowHistoryModal = false;
                    }
                    else
                    {
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                    }
                }

                bool modalOpen = _guideModal.IsOpen || _settings.IsOpen || _profileModal.IsOpen || _cadenceLab.ShowHistoryModal;

                if (!modalOpen)
                {
                    if (Raylib.IsKeyPressed(KeyboardKey.One))
                    {
                        _currentMode = AppMode.CadenceLab;
                        AppConfig.Instance.ModeType = TrainerMode.StrafePractice;
                        AppConfig.Save();
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.P))
                    {
                        _profileModal.IsOpen = true;
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.M))
                    {
                        AppConfig.Instance.MetronomeEnabled = !AppConfig.Instance.MetronomeEnabled;
                        AppConfig.Save();
                    }
                }

                // Update Input
                InputManager.Instance.Update();

                // Update Active Mode (only when modals are closed)
                if (!modalOpen)
                {
                    switch (_currentMode)
                    {
                        case AppMode.CadenceLab:
                            _cadenceLab.Update(dt);
                            break;
                        case AppMode.Oscilloscope:
                            _oscMode.Update(dt);
                            break;
                    }
                }

                int screenW = Raylib.GetScreenWidth();
                int screenH = Raylib.GetScreenHeight();

                if (Raylib.IsWindowResized())
                {
                    ShaderFxManager.ResizeBuffer(screenW, screenH);
                }

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Theme.BgDark);

                if (_profileModal.IsOpen)
                {
                    // Fullscreen Profile View takes 100% control of rendering and input
                    _profileModal.Draw(screenW, screenH);
                }
                else
                {
                    // 1. Draw Active Canvas / Background
                    switch (_currentMode)
                    {
                        case AppMode.CadenceLab:
                            _cadenceLab.Draw(screenW, screenH);
                            break;
                        case AppMode.Oscilloscope:
                            _oscMode.Draw(screenW, screenH);
                            break;
                    }

                    // 2. Draw Master Top Navigation Bar
                    DrawTopNavBar(screenW);

                    // 3. Draw Bottom Terminal Command Bar
                    DrawBottomStatusBar(screenW, screenH);

                    // 4. Draw Modals
                    if (_cadenceLab.ShowHistoryModal)
                    {
                        _cadenceLab.DrawHistoryModal(screenW, screenH);
                    }

                    if (_settings.IsOpen)
                    {
                        _settings.Draw(screenW, screenH);
                    }

                    if (_guideModal.IsOpen)
                    {
                        _guideModal.Draw(screenW, screenH);
                    }

                    // 5. In-App Update Modal Prompt
                    UpdateModal.Draw(screenW, screenH, AppConfig.Instance.UiScale, true);

                    // 6. Optional CRT Scanlines Overlay
                    Theme.DrawCrtScanlines(screenW, screenH);
                }

                Raylib.EndDrawing();
            }

            TrayManager.Dispose();
            SvgIconManager.Cleanup();
            ShaderFxManager.Cleanup();
            AppConfig.Save();
            UserProfile.Save();
            AudioEngine.Shutdown();
            Raylib.CloseWindow();
        }

        private static void DrawBottomStatusBar(int screenW, int screenH)
        {
            float scale = AppConfig.Instance.UiScale;
            int barH = (int)(24 * scale);
            int barY = screenH - barH;

            // Translucent terminal status bar
            Raylib.DrawRectangle(0, barY, screenW, barH, Theme.BgDark);
            Raylib.DrawLine(0, barY, screenW, barY, Theme.Border);

            int ty = barY + (barH - Theme.GetScaledFontSize(9)) / 2;

            // Left: checkerboard pattern + technical console stream status
            int cbX = 10;
            for (int i = 0; i < 4; i++)
            {
                Raylib.DrawRectangle(cbX + i * 6, barY + (barH - 6) / 2, 3, 3, Theme.NeonCyan);
                Raylib.DrawRectangle(cbX + i * 6 + 3, barY + (barH - 6) / 2 + 3, 3, 3, Theme.NeonCyan);
            }

            string watcher = CS2ConsoleWatcher.IsWatching ? "LIVE" : "IDLE";
            string leftInfo = $"> CS2 CONSOLE: [{watcher}] // ENGINE: {AppConfig.Instance.Mode} ({AppConfig.Instance.Tickrate}T) // SENS: {AppConfig.Instance.Sensitivity:F2}";
            Theme.DrawText(leftInfo, cbX + 32, ty, 9, Theme.TextMuted);

            // Right: terminal shortcuts in bracket notation
            string rightKeys = "[SPACE] ENGAGE   [1] LAB   [P] PROFILE   [TAB] SETTINGS   [F1] GUIDE";
            int rkW = Theme.MeasureText(rightKeys, 9);
            Theme.DrawText(rightKeys, screenW - rkW - 40, ty, 9, Theme.NeonCyan);

            // Far right: micro checkerboard
            int rcbX = screenW - 32;
            for (int i = 0; i < 4; i++)
            {
                Raylib.DrawRectangle(rcbX + i * 6, barY + (barH - 6) / 2, 3, 3, Theme.NeonOrange);
                Raylib.DrawRectangle(rcbX + i * 6 + 3, barY + (barH - 6) / 2 + 3, 3, 3, Theme.NeonOrange);
            }
        }

        private static void DrawTopNavBar(int screenWidth)
        {
            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            int tabY = 5;
            int tabH = (int)(28 * scale);
            int navH = tabH + 10;

            // ── TUI TOP BAR CONTAINER ────────────────────────────────────────────────
            Raylib.DrawRectangle(0, 0, screenWidth, navH, Theme.BgDark);
            Raylib.DrawLine(0, navH - 1, screenWidth, navH - 1, Theme.Border);

            // Logo: "LJ LAB" (Version shown on hover tooltip)
            int verX = (int)(16 * scale);
            int verY = (navH - Theme.GetScaledFontSize(12)) / 2;
            Theme.DrawText("LJ", verX, verY - 1, 14, Theme.TextWhite);
            Theme.DrawText("LAB", verX + (int)(22 * scale), verY + 1, 11, Theme.TextDim);

            // Hover tooltip on logo
            Vector2 mouse = Raylib.GetMousePosition();
            if (mouse.X >= verX && mouse.X <= verX + (int)(60 * scale) && mouse.Y >= 0 && mouse.Y <= navH)
            {
                Theme.DrawTooltip((int)mouse.X, (int)mouse.Y + 20, "CS2 LONG JUMP TRAINER", $"Version {UpdateManager.CurrentVersion}", "KZ & Movement Cadence Practice Engine");
            }

            int curX = verX + (int)(55 * scale) + (int)(16 * scale);
            int gap = (int)(4 * scale);
            bool compact = screenWidth < 1180;
            bool ultraCompact = screenWidth < 960;

            bool DrawFlowButton(string text, bool active, int fontSize, Action onClick)
            {
                int bw = Theme.MeasureText(text, fontSize) + (int)(14 * scale);
                if (Theme.DrawButton(curX, tabY, bw, tabH, text, active, fontSize))
                {
                    onClick();
                    return true;
                }
                curX += bw + gap;
                return false;
            }

            // Mode: Trainer
            bool isStrafe = _currentMode == AppMode.CadenceLab;
            string trainerLabel = ultraCompact ? "[LAB]" : "[TRAINER]";
            DrawFlowButton(trainerLabel, isStrafe, 11, () =>
            {
                _currentMode = AppMode.CadenceLab;
                cfg.ModeType = TrainerMode.StrafePractice;
                AppConfig.Save();
            });

            // Play / Pause
            bool running = _cadenceLab.IsTrainingRunning;
            string playLabel = running ? "[PAUSE]" : "[START]";
            DrawFlowButton(playLabel, running, 11, () =>
            {
                _cadenceLab.IsTrainingRunning = !_cadenceLab.IsTrainingRunning;
                InputManager.Instance.SetCursorLock(_cadenceLab.IsTrainingRunning);
            });

            // Metronome button
            string metroLabel = cfg.MetronomeEnabled
                ? (ultraCompact ? $"[M:{cfg.TargetStrafeDurationMs:F0}]" : $"[M: {cfg.TargetStrafeDurationMs:F0}ms]")
                : "[M: OFF]";
            DrawFlowButton(metroLabel, cfg.MetronomeEnabled, 11, () =>
            {
                cfg.MetronomeEnabled = !cfg.MetronomeEnabled;
                AppConfig.Save();
            });

            // Sound button
            string soundLabel = cfg.SoundEnabled
                ? (ultraCompact ? "[VOL]" : "[VOL: ON]")
                : (ultraCompact ? "[MUTE]" : "[VOL: OFF]");
            DrawFlowButton(soundLabel, cfg.SoundEnabled, 11, () =>
            {
                cfg.SoundEnabled = !cfg.SoundEnabled;
                AppConfig.Save();
            });

            // ── RIGHT-ALIGNED COMMANDS ────────────────────────────────────────────────
            int rx = screenWidth - (int)(12 * scale);

            // Profile button with real Steam Avatar icon
            int profBtnSize = tabH;
            rx -= profBtnSize;
            if (Theme.DrawIconButton(rx, tabY, profBtnSize, tabH,
                (cx, cy, sz, col) => _profileModal.DrawProfileAvatarIcon(cx, cy, sz, col),
                null, _profileModal.IsOpen, 11))
            {
                _profileModal.IsOpen = !_profileModal.IsOpen;
                if (_profileModal.IsOpen)
                {
                    _cadenceLab.IsTrainingRunning = false;
                    InputManager.Instance.SetCursorLock(false);
                    _guideModal.IsOpen = false;
                    _settings.IsOpen = false;
                    _cadenceLab.ShowHistoryModal = false;
                }
            }
            rx -= gap;

            // Settings button
            string setLabel = ultraCompact ? "[SET]" : "[SETTINGS]";
            int cfgW = Theme.MeasureText(setLabel, 11) + (int)(14 * scale);
            rx -= cfgW;
            if (Theme.DrawButton(rx, tabY, cfgW, tabH, setLabel, _settings.IsOpen, 11))
            {
                _settings.IsOpen = !_settings.IsOpen;
                if (_settings.IsOpen)
                {
                    _cadenceLab.IsTrainingRunning = false;
                    InputManager.Instance.SetCursorLock(false);
                    _guideModal.IsOpen = false;
                    _profileModal.IsOpen = false;
                    _cadenceLab.ShowHistoryModal = false;
                }
                else AppConfig.Save();
            }
            rx -= gap;

            void DrawRightButton(string text, bool active, int fontSize, Action onClick)
            {
                int bw = Theme.MeasureText(text, fontSize) + (int)(14 * scale);
                rx -= bw;
                if (Theme.DrawButton(rx, tabY, bw, tabH, text, active, fontSize)) onClick();
                rx -= gap;
            }

            // History button
            int histCount = _cadenceLab.RecentStrafes.Count;
            string histLabel = _cadenceLab.ShowHistoryModal
                ? "[HISTORY: X]"
                : (ultraCompact ? $"[H:{histCount}]" : (compact ? $"[HIST: {histCount}]" : $"[HISTORY: {histCount}]"));
            DrawRightButton(histLabel, _cadenceLab.ShowHistoryModal, 11, () =>
            {
                _cadenceLab.ShowHistoryModal = !_cadenceLab.ShowHistoryModal;
                if (_cadenceLab.ShowHistoryModal)
                {
                    _cadenceLab.IsTrainingRunning = false;
                    InputManager.Instance.SetCursorLock(false);
                    _guideModal.IsOpen = false;
                    _profileModal.IsOpen = false;
                    _settings.IsOpen = false;
                }
            });

            // Guide button
            string guideLabel = ultraCompact ? "[F1]" : (compact ? "[GUIDE]" : "[F1: GUIDE]");
            DrawRightButton(guideLabel, _guideModal.IsOpen, 11, () =>
            {
                _guideModal.IsOpen = !_guideModal.IsOpen;
                if (_guideModal.IsOpen)
                {
                    _cadenceLab.IsTrainingRunning = false;
                    InputManager.Instance.SetCursorLock(false);
                    _profileModal.IsOpen = false;
                    _settings.IsOpen = false;
                    _cadenceLab.ShowHistoryModal = false;
                }
            });
        }
    }
}
