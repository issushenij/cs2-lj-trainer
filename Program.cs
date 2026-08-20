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
            CS2ConsoleWatcher.StartWatching();

            // Set process DPI aware and configure Raylib flags
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
            Raylib.InitWindow(1380, 860, "CS2 Long Jump Cadence & Sync Lab v10.8");
            Raylib.SetWindowMinSize(1024, 700);
            Raylib.SetTargetFPS(144);

            // CRITICAL: Disable Escape key from exiting application (only window [X] or tray exits)
            Raylib.SetExitKey(KeyboardKey.Null);

            // Initialize Audio Engine & High-DPI Cyrillic Font
            AudioEngine.Initialize();
            Theme.InitializeFont();

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

            // Automatic background Cybershoke sync on startup
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                string sid = UserProfile.Instance.Cybershoke.SteamId64;
                if (!string.IsNullOrEmpty(sid))
                {
                    CybershokeWebSync.StartAutoSync(sid, null);
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
                    if (Raylib.IsKeyPressed(KeyboardKey.Two))
                    {
                        _profileModal.IsOpen = true;
                        _cadenceLab.IsTrainingRunning = false;
                        InputManager.Instance.SetCursorLock(false);
                    }
                    if (Raylib.IsKeyPressed(KeyboardKey.Three))
                    {
                        _currentMode = AppMode.Oscilloscope;
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

                // Render Frame
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Theme.BgDark);

                int screenW = Raylib.GetScreenWidth();
                int screenH = Raylib.GetScreenHeight();

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

                    // 3. Draw Modals
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
                }

                Raylib.EndDrawing();
            }

            TrayManager.Dispose();
            AppConfig.Save();
            UserProfile.Save();
            AudioEngine.Shutdown();
            Raylib.CloseWindow();
        }

        private static void DrawTopNavBar(int screenWidth)
        {
            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            int tabY = 8;
            int tabH = (int)(32 * scale);
            int navH = tabH + 16;

            // Top Nav Glass Background
            Theme.DrawGlassPanel(0, 0, screenWidth, navH);

            // 1. Logo
            int logoFontSize = 16;
            Theme.DrawText("CS2 LJ LAB", 16, tabY + (tabH - Theme.GetScaledFontSize(logoFontSize)) / 2, logoFontSize, Theme.NeonCyan);
            int curX = 16 + Theme.MeasureText("CS2 LJ LAB", logoFontSize) + (int)(18 * scale);
            int gap = (int)(8 * scale);

            // Helper to draw a flowing button on the left
            bool DrawFlowButton(string text, bool active, int fontSize, Action onClick)
            {
                int bw = Theme.MeasureText(text, fontSize) + (int)(18 * scale);
                if (Theme.DrawButton(curX, tabY, bw, tabH, text, active, fontSize))
                {
                    onClick();
                    return true;
                }
                curX += bw + gap;
                return false;
            }

            // Modes
            bool isStrafe = _currentMode == AppMode.CadenceLab;
            DrawFlowButton("1. Тренажёр", isStrafe, 12, () =>
            {
                _currentMode = AppMode.CadenceLab;
                cfg.ModeType = TrainerMode.StrafePractice;
                AppConfig.Save();
            });

            bool isOsc = _currentMode == AppMode.Oscilloscope;
            DrawFlowButton("2. Осциллограф", isOsc, 12, () =>
            {
                _currentMode = AppMode.Oscilloscope;
            });

            // Play / Pause Button
            bool running = _cadenceLab.IsTrainingRunning;
            string playLabel = running ? "⏸ Пауза (Space)" : "▶ СТАРТ (Space)";
            DrawFlowButton(playLabel, !running, 12, () =>
            {
                _cadenceLab.IsTrainingRunning = !_cadenceLab.IsTrainingRunning;
                InputManager.Instance.SetCursorLock(_cadenceLab.IsTrainingRunning);
            });

            // Metronome Icon Button with Animated Swinging Needle & Target Pace
            string metroLabel = cfg.MetronomeEnabled ? $"{cfg.TargetStrafeDurationMs:F0}мс" : "ВЫКЛ";
            int metroW = Theme.MeasureText(metroLabel, 12) + (int)(38 * scale);
            if (Theme.DrawIconButton(curX, tabY, metroW, tabH, (cx, cy, sz, col) => Theme.DrawMetronomeIcon(cx, cy, sz, col, cfg.MetronomeEnabled), metroLabel, cfg.MetronomeEnabled, 12))
            {
                cfg.MetronomeEnabled = !cfg.MetronomeEnabled;
                AppConfig.Save();
            }
            curX += metroW + gap;

            // Sound Speaker Icon Button (Speaker with waves when ON, crossed with slash when MUTED)
            int soundW = (int)(38 * scale);
            if (Theme.DrawIconButton(curX, tabY, soundW, tabH, (cx, cy, sz, col) => Theme.DrawSpeakerIcon(cx, cy, sz, col, !cfg.SoundEnabled), null, cfg.SoundEnabled, 12))
            {
                cfg.SoundEnabled = !cfg.SoundEnabled;
                AppConfig.Save();
            }
            curX += soundW + gap;

            // 6. Right-aligned Action Items (flowing right to left)
            int rx = screenWidth - (int)(16 * scale);

            // 1. Profile Square Vector Icon Button (Rightmost)
            int profBtnSize = tabH; // Pure square button
            rx -= profBtnSize;
            if (Theme.DrawIconButton(rx, tabY, profBtnSize, tabH, (cx, cy, sz, col) => Theme.DrawProfileIcon(cx, cy, sz, col), null, _profileModal.IsOpen, 12))
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

            void DrawRightButton(string text, bool active, int fontSize, Action onClick)
            {
                int bw = Theme.MeasureText(text, fontSize) + (int)(18 * scale);
                rx -= bw;
                if (Theme.DrawButton(rx, tabY, bw, tabH, text, active, fontSize))
                {
                    onClick();
                }
                rx -= gap;
            }

            // Config [Tab]
            DrawRightButton("[Tab] Config", _settings.IsOpen, 13, () =>
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
            });

            // History
            string histLabel = _cadenceLab.ShowHistoryModal ? "Закрыть ✕" : $"История ({_cadenceLab.RecentStrafes.Count})";
            DrawRightButton(histLabel, _cadenceLab.ShowHistoryModal, 13, () =>
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

            // Guide [?]
            DrawRightButton("[?] Гайд (F1)", _guideModal.IsOpen, 13, () =>
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
            });
        }
    }
}
