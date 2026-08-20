using System;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public class SettingsDrawer
    {
        public bool IsOpen { get; set; } = false;

        private float _scrollY = 0f;
        private float _targetScrollY = 0f;
        private string _importStatusMessage = "";
        private bool _importStatusSuccess = false;

        public void Draw(int screenWidth, int screenHeight)
        {
            if (!IsOpen) return;

            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            Vector2 mouse = Raylib.GetMousePosition();

            // Backdrop dimming overlay
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 220));

            int modalW = Math.Min((int)(820 * MathF.Min(scale, 1.15f)), screenWidth - 32);
            int modalH = Math.Min((int)(700 * MathF.Min(scale, 1.15f)), screenHeight - 40);
            int modalX = (screenWidth - modalW) / 2;
            int modalY = (screenHeight - modalH) / 2;

            // Frosted Glass Container
            Theme.DrawGlassPanel(modalX, modalY, modalW, modalH);

            // Modal Header
            int headerH = (int)(46 * scale);
            Raylib.DrawRectangle(modalX, modalY, modalW, headerH, Theme.BgPanelHeader);
            Raylib.DrawLine(modalX, modalY + headerH, modalX + modalW, modalY + headerH, Theme.Border);
            Raylib.DrawLine(modalX + 1, modalY + 1, modalX + modalW - 2, modalY + 1, new Color(255, 255, 255, 80));

            Theme.DrawText("⚙ НАСТРОЙКИ ТРЕНАЖЕРА (SETTINGS)", modalX + 18, modalY + (headerH - Theme.GetScaledFontSize(15)) / 2, 15, Theme.NeonCyan);

            // Close button [X]
            int closeW = (int)(95 * scale);
            int closeH = (int)(28 * scale);
            if (Theme.DrawButton(modalX + modalW - closeW - 14, modalY + (headerH - closeH) / 2, closeW, closeH, "ЗАКРЫТЬ", false, 12) ||
                Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                IsOpen = false;
                AppConfig.Save();
                return;
            }

            // Close on click outside
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (mouse.X < modalX || mouse.X > modalX + modalW || mouse.Y < modalY || mouse.Y > modalY + modalH)
                {
                    IsOpen = false;
                    AppConfig.Save();
                    return;
                }
            }

            int contentX = modalX + 18;
            int contentY = modalY + headerH + 12;
            int contentW = modalW - 36;
            int viewH = modalH - headerH - 24;

            // Handle Mouse Wheel Scroll within the modal
            if (mouse.X >= modalX && mouse.X <= modalX + modalW && mouse.Y >= contentY && mouse.Y <= contentY + viewH)
            {
                float wheel = Raylib.GetMouseWheelMove();
                if (wheel != 0)
                {
                    _targetScrollY -= wheel * (65 * scale);
                }
            }

            // Smooth scroll interpolation
            _scrollY += (_targetScrollY - _scrollY) * 0.35f;

            // Scissor clipping for clean scrollview
            Raylib.BeginScissorMode(modalX + 6, contentY, modalW - 12, viewH);

            int curY = contentY - (int)_scrollY;
            int cardW = contentW - 14; // Leave room for scrollbar
            int gap = (int)(16 * scale);

            // =========================================================================
            // 1. SECTION: MOUSE & SENSITIVITY
            // =========================================================================
            int s1H = DrawMouseSection(contentX, curY, cardW, scale, cfg);
            curY += s1H + gap;

            // =========================================================================
            // 2. SECTION: METRONOME & CADENCE
            // =========================================================================
            int s2H = DrawCadenceSection(contentX, curY, cardW, scale, cfg);
            curY += s2H + gap;

            // =========================================================================
            // 3. SECTION: AUDIO & BIOFEEDBACK
            // =========================================================================
            int s3H = DrawAudioSection(contentX, curY, cardW, scale, cfg);
            curY += s3H + gap;

            // =========================================================================
            // 4. SECTION: THEME & VISUALS
            // =========================================================================
            int s4H = DrawVisualsSection(contentX, curY, cardW, scale, cfg);
            curY += s4H + gap;

            // =========================================================================
            // 5. SECTION: PHYSICS & SIMULATION
            // =========================================================================
            int s5H = DrawPhysicsSection(contentX, curY, cardW, scale, cfg);
            curY += s5H + gap;

            // =========================================================================
            // 6. SECTION: SYSTEM TRAY & BACKGROUND MODE
            // =========================================================================
            int s6H = DrawTraySection(contentX, curY, cardW, scale, cfg);
            curY += s6H + gap;

            Raylib.EndScissorMode();

            // Total height calculation & Scroll clamping
            int totalContentHeight = (curY + (int)_scrollY) - contentY;
            float maxScroll = Math.Max(0, totalContentHeight - viewH);
            _targetScrollY = Math.Clamp(_targetScrollY, 0, maxScroll);
            if (maxScroll <= 0) _scrollY = 0;

            // Draw Scrollbar track & thumb
            if (maxScroll > 0)
            {
                int barX = modalX + modalW - 14;
                int barY = contentY;
                int barW = 6;
                int barH = viewH;

                // Track
                Raylib.DrawRectangle(barX, barY, barW, barH, new Color(20, 26, 38, 180));

                // Thumb
                float thumbRatio = (float)viewH / totalContentHeight;
                int thumbH = Math.Max(28, (int)(barH * thumbRatio));
                float scrollPct = _scrollY / maxScroll;
                int thumbY = barY + (int)((barH - thumbH) * scrollPct);

                Raylib.DrawRectangle(barX, thumbY, barW, thumbH, Theme.NeonCyan);
            }
        }

        // =========================================================================
        // SECTION 1: MOUSE & SENSITIVITY
        // =========================================================================
        private int DrawMouseSection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int h = (int)(175 * scale);
            if (!string.IsNullOrEmpty(_importStatusMessage)) h += (int)(24 * scale);

            DrawSectionCard(x, y, width, h, "1. ЧУВСТВИТЕЛЬНОСТЬ МЫШИ (CS2 SENSITIVITY)", Theme.NeonCyan);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);

            // Top Status Line: Current Sensitivity & m_yaw
            string sensInfo = $"Текущая сенса: {cfg.Sensitivity:F2}   (m_yaw: {cfg.YawFactor:F4})";
            Theme.DrawText(sensInfo, innerX, curY + 4, 13, Theme.NeonGold);
            curY += (int)(26 * scale);

            // Stepper [-] [+] and Sens Presets
            float[] sensPresets = { 0.8f, 1.0f, 1.2f, 1.5f, 2.0f, 2.5f, 3.0f };
            int stepBtnW = (int)(34 * scale);
            int gap = 6;
            int bx = innerX;

            // [-]
            if (Theme.DrawButton(bx, curY, stepBtnW, btnH, "−", false, 14))
            {
                cfg.Sensitivity = MathF.Max(0.1f, MathF.Round((cfg.Sensitivity - 0.05f) * 100f) / 100f);
                AppConfig.Save();
            }
            bx += stepBtnW + gap;

            // [+]
            if (Theme.DrawButton(bx, curY, stepBtnW, btnH, "+", false, 14))
            {
                cfg.Sensitivity = MathF.Min(10.0f, MathF.Round((cfg.Sensitivity + 0.05f) * 100f) / 100f);
                AppConfig.Save();
            }
            bx += stepBtnW + gap + 4;

            // Presets chips
            int remainingW = innerX + innerW - bx;
            int presetBtnW = (remainingW - (sensPresets.Length - 1) * gap) / sensPresets.Length;

            foreach (var sp in sensPresets)
            {
                bool active = MathF.Abs(cfg.Sensitivity - sp) < 0.01f;
                if (Theme.DrawButton(bx, curY, presetBtnW, btnH, sp.ToString("F1"), active, 11))
                {
                    cfg.Sensitivity = sp;
                    AppConfig.Save();
                }
                bx += presetBtnW + gap;
            }

            // Row 2: CS2 Auto Import & Reversal Trigger
            curY += (int)(36 * scale);
            int halfW = (innerW - gap) / 2;

            if (Theme.DrawButton(innerX, curY, halfW, btnH, "Авто-импорт sens из CS2", false, 12))
            {
                var res = CS2ConfigImporter.TryAutoImport();
                if (res.Success)
                {
                    cfg.Sensitivity = res.Sensitivity;
                    cfg.YawFactor = res.YawFactor;
                    _importStatusMessage = $"✓ {res.Message}";
                    _importStatusSuccess = true;
                    AppConfig.Save();
                }
                else
                {
                    _importStatusMessage = $"✕ {res.Message}";
                    _importStatusSuccess = false;
                }
            }

            // Reversal trigger switch
            int trigW = (halfW - gap) / 2;
            int trigX = innerX + halfW + gap;
            if (Theme.DrawButton(trigX, curY, trigW, btnH, "Разворот: Мышь", cfg.FreestyleTrigger == ReversalTriggerMode.ByMouseMovement, 11))
            {
                cfg.FreestyleTrigger = ReversalTriggerMode.ByMouseMovement;
                AppConfig.Save();
            }
            if (Theme.DrawButton(trigX + trigW + gap, curY, trigW, btnH, "Разворот: A / D", cfg.FreestyleTrigger == ReversalTriggerMode.ByKeyPress, 11))
            {
                cfg.FreestyleTrigger = ReversalTriggerMode.ByKeyPress;
                AppConfig.Save();
            }

            if (!string.IsNullOrEmpty(_importStatusMessage))
            {
                curY += (int)(32 * scale);
                Color statusCol = _importStatusSuccess ? Theme.NeonGreen : Theme.NeonOrange;
                Theme.DrawText(_importStatusMessage, innerX, curY, 11, statusCol);
            }

            return h;
        }

        // =========================================================================
        // SECTION 2: METRONOME & CADENCE
        // =========================================================================
        private int DrawCadenceSection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int h = (int)(165 * scale);
            DrawSectionCard(x, y, width, h, "2. МЕТРОНОМ И ТЕМП СТРЕЙФОВ (CADENCE)", Theme.NeonGreen);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);
            int gap = 6;

            // Master Toggle Button
            string mToggleLabel = cfg.MetronomeEnabled
                ? $"МЕТРОНОМ: ВКЛЮЧЕН  ({cfg.TargetStrafeDurationMs:F0} мс  |  ~{cfg.TargetStrafes} стрейфов в LJ)"
                : "МЕТРОНОМ: ВЫКЛЮЧЕН (Свободный темп + звук на каждом развороте)";
            if (Theme.DrawButton(innerX, curY, innerW, (int)(30 * scale), mToggleLabel, cfg.MetronomeEnabled, 12))
            {
                cfg.MetronomeEnabled = !cfg.MetronomeEnabled;
                AppConfig.Save();
            }


            // Strafe count presets
            curY += (int)(38 * scale);
            Theme.DrawText("Темп стрейфов:", innerX, curY + 5, 12, Theme.TextWhite);
            int labelW = (int)(105 * scale);
            int[] strafePresets = { 6, 7, 8, 9, 10, 12 };
            int cadBtnW = (innerW - labelW - (strafePresets.Length - 1) * gap) / strafePresets.Length;
            int curCadX = innerX + labelW;

            for (int i = 0; i < strafePresets.Length; i++)
            {
                int strNum = strafePresets[i];
                bool isCur = !cfg.UseCustomDuration && cfg.TargetStrafes == strNum;
                if (Theme.DrawButton(curCadX, curY, cadBtnW, btnH, $"{strNum} стр", isCur, 11))
                {
                    cfg.TargetStrafes = strNum;
                    cfg.UseCustomDuration = false;
                    AppConfig.Save();
                }
                curCadX += cadBtnW + gap;
            }

            // Precise Milliseconds Adjuster
            curY += (int)(34 * scale);
            Theme.DrawText("Длительность:", innerX, curY + 5, 12, Theme.TextWhite);
            int durAdjX = innerX + labelW;
            int durBtnW = (int)(32 * scale);

            if (Theme.DrawButton(durAdjX, curY, durBtnW, btnH, "−", false, 14))
            {
                cfg.CustomTargetDurationMs = Math.Clamp(cfg.TargetStrafeDurationMs - 5.0f, 40.0f, 200.0f);
                cfg.UseCustomDuration = true;
                AppConfig.Save();
            }
            durAdjX += durBtnW + gap;

            string durText = $"{cfg.TargetStrafeDurationMs:F0} мс   ({cfg.CalculatedMetronomeBpm} BPM)";
            Theme.DrawText(durText, durAdjX, curY + (btnH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonGold);
            durAdjX += Theme.MeasureText(durText, 12) + gap + 6;

            if (Theme.DrawButton(durAdjX, curY, durBtnW, btnH, "+", false, 14))
            {
                cfg.CustomTargetDurationMs = Math.Clamp(cfg.TargetStrafeDurationMs + 5.0f, 40.0f, 200.0f);
                cfg.UseCustomDuration = true;
                AppConfig.Save();
            }

            return h;
        }

        // =========================================================================
        // SECTION 3: AUDIO & BIOFEEDBACK
        // =========================================================================
        private int DrawAudioSection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int soundCols = 4;
            int rows = 4;
            int sndH = (int)(24 * scale);
            int soundGridH = rows * (sndH + 4);

            int h = (int)(185 * scale) + soundGridH;
            DrawSectionCard(x, y, width, h, "3. ЗВУК И АУДИО-БИОФИДБЕК (AUDIO FEEDBACK)", Theme.NeonPurple);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);
            int gap = 6;

            // Master Volume Row
            Theme.DrawText("Громкость:", innerX, curY + 5, 12, Theme.TextWhite);
            int labelW = (int)(85 * scale);
            int vBtnW = (int)(32 * scale);
            int vX = innerX + labelW;

            if (Theme.DrawButton(vX, curY, vBtnW, btnH, "−", false, 14))
            {
                cfg.MasterVolume = Math.Clamp(cfg.MasterVolume - 0.05f, 0.0f, 1.0f);
                AppConfig.Save();
            }
            vX += vBtnW + gap;

            string volText = $"{(int)(cfg.MasterVolume * 100)}%";
            Theme.DrawText(volText, vX, curY + (btnH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.TextWhite);
            vX += Theme.MeasureText(volText, 12) + gap + 6;

            if (Theme.DrawButton(vX, curY, vBtnW, btnH, "+", false, 14))
            {
                cfg.MasterVolume = Math.Clamp(cfg.MasterVolume + 0.05f, 0.0f, 1.0f);
                AppConfig.Save();
            }
            vX += vBtnW + gap + 10;

            int muteW = (int)(130 * scale);
            if (Theme.DrawButton(vX, curY, muteW, btnH, cfg.SoundEnabled ? "ЗВУК: ВКЛ" : "БЕЗ ЗВУКА", cfg.SoundEnabled, 11))
            {
                cfg.SoundEnabled = !cfg.SoundEnabled;
                AppConfig.Save();
            }

            // Biofeedback Toggle
            curY += (int)(34 * scale);
            string bioLabel = cfg.AudioBiofeedback ? "Звуковой биофидбек: ВКЛЮЧЕН" : "Звуковой биофидбек: ВЫКЛЮЧЕН";
            if (Theme.DrawButton(innerX, curY, innerW, btnH, bioLabel, cfg.AudioBiofeedback, 12))
            {
                cfg.AudioBiofeedback = !cfg.AudioBiofeedback;
                AppConfig.Save();
            }

            curY += (int)(32 * scale);
            Theme.DrawWrappedText("• Чистая синхра (>80%): перезвон  • Погрешность: -3 полутона  • Ошибка/Overlap: глухой щелчок", innerX, curY, innerW, 11, 2, Theme.TextMuted);
            curY += (int)(26 * scale);

            // Metronome Sound Profiles Matrix
            Theme.DrawText("Профиль звука метронома (кликните для прослушивания):", innerX, curY, 12, Theme.TextWhite);
            curY += (int)(20 * scale);

            int sndW = (innerW - (soundCols - 1) * 4) / soundCols;

            for (int i = 0; i < AudioEngine.SoundPresetNames.Length; i++)
            {
                int c = i % soundCols;
                int r = i / soundCols;
                int sx = innerX + c * (sndW + 4);
                int sy = curY + r * (sndH + 4);

                bool isSel = cfg.SoundPresetIndex == i;
                string sName = AudioEngine.SoundPresetNames[i];

                if (Theme.DrawButton(sx, sy, sndW, sndH, sName, isSel, 10))
                {
                    cfg.SoundPresetIndex = i;
                    AudioEngine.PlayMetronomeTick(i, false);
                    AppConfig.Save();
                }
            }

            return h;
        }

        // =========================================================================
        // SECTION 4: THEME & VISUALS
        // =========================================================================
        private int DrawVisualsSection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int h = (int)(130 * scale);
            DrawSectionCard(x, y, width, h, "4. ТЕМА И МАСШТАБ ИНТЕРФЕЙСА (THEME & UI)", Theme.NeonGold);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);
            int gap = 6;

            // Themes
            Theme.DrawText("Цветовая тема:", innerX, curY + 5, 12, Theme.TextWhite);
            int labelW = (int)(110 * scale);
            int thW = (innerW - labelW - 2 * gap) / 3;
            int thX = innerX + labelW;

            if (Theme.DrawButton(thX, curY, thW, btnH, "Cyber Neon", cfg.Theme == ColorTheme.CyberNeon, 11))
            {
                cfg.Theme = ColorTheme.CyberNeon;
                AppConfig.Save();
            }
            if (Theme.DrawButton(thX + thW + gap, curY, thW, btnH, "OLED Mono", cfg.Theme == ColorTheme.OLEDMonochrome, 11))
            {
                cfg.Theme = ColorTheme.OLEDMonochrome;
                AppConfig.Save();
            }
            if (Theme.DrawButton(thX + (thW + gap) * 2, curY, thW, btnH, "Amber Sunset", cfg.Theme == ColorTheme.AmberSunset, 11))
            {
                cfg.Theme = ColorTheme.AmberSunset;
                AppConfig.Save();
            }

            // UI Scale Chips
            curY += (int)(36 * scale);
            Theme.DrawText("Масштаб UI:", innerX, curY + 5, 12, Theme.TextWhite);
            float[] scales = { 1.0f, 1.25f, 1.50f, 1.75f };
            string[] scaleLabels = { "100%", "125%", "150%", "175%" };
            int scW = (innerW - labelW - (scales.Length - 1) * gap) / scales.Length;
            int curScX = innerX + labelW;

            for (int i = 0; i < scales.Length; i++)
            {
                float sc = scales[i];
                bool isCurScale = MathF.Abs(cfg.UiScale - sc) < 0.05f;
                if (Theme.DrawButton(curScX, curY, scW, btnH, scaleLabels[i], isCurScale, 11))
                {
                    cfg.UiScale = sc;
                    AppConfig.Save();
                }
                curScX += scW + gap;
            }

            return h;
        }

        // =========================================================================
        // SECTION 5: PHYSICS & SIMULATION
        // =========================================================================
        private int DrawPhysicsSection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int h = (int)(105 * scale);
            DrawSectionCard(x, y, width, h, "5. ФИЗИКА И СИМУЛЯЦИЯ (PHYSICS ENGINE)", Theme.NeonOrange);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);
            int gap = 6;

            Theme.DrawText("Режим физики:", innerX, curY + 5, 12, Theme.TextWhite);
            int labelW = (int)(110 * scale);
            int physW = (innerW - labelW - gap) / 2;
            int physX = innerX + labelW;

            if (Theme.DrawButton(physX, curY, physW, btnH, "CKZ / KZ (100 AA, 276 Pre)", cfg.Mode == PhysicsMode.CKZ, 11))
            {
                cfg.Mode = PhysicsMode.CKZ;
                AppConfig.Save();
            }
            if (Theme.DrawButton(physX + physW + gap, curY, physW, btnH, "Vanilla CS2 (12 AA, 250 Pre)", cfg.Mode == PhysicsMode.Vanilla, 11))
            {
                cfg.Mode = PhysicsMode.Vanilla;
                AppConfig.Save();
            }

            return h;
        }

        // =========================================================================
        // SECTION 6: SYSTEM TRAY & BACKGROUND MODE
        // =========================================================================
        private int DrawTraySection(int x, int y, int width, float scale, AppConfig cfg)
        {
            int h = (int)(115 * scale);
            DrawSectionCard(x, y, width, h, "6. ПОВЕДЕНИЕ ОКНА И ФОНОВЫЙ РЕЖИМ (SYSTEM TRAY)", Theme.NeonGreen);

            int innerX = x + 18;
            int innerW = width - 36;
            int curY = y + (int)(40 * scale);
            int btnH = (int)(28 * scale);

            string trayLabel = cfg.MinimizeToTrayOnClose 
                ? "Сворачивать в системный трей при закрытии [X]: ВКЛЮЧЕНО" 
                : "Сворачивать в системный трей при закрытии [X]: ВЫКЛЮЧЕНО (закрывать программу)";

            if (Theme.DrawButton(innerX, curY, innerW, btnH, trayLabel, cfg.MinimizeToTrayOnClose, 11))
            {
                cfg.MinimizeToTrayOnClose = !cfg.MinimizeToTrayOnClose;
                AppConfig.Save();
            }

            curY += (int)(34 * scale);
            Theme.DrawWrappedText("• В свёрнутом состоянии в трее звуки рекордов CS2 и парсинг прыжков продолжают работать в фоне!\n• Закрытие программы на клавишу Escape отключено (закрыть можно только на [X] или через меню трея).", innerX, curY, innerW, 10, 2, Theme.TextMuted);

            return h;
        }

        private void DrawSectionCard(int x, int y, int width, int height, string title, Color accent)
        {
            // Frosted panel card background
            Raylib.DrawRectangle(x, y, width, height, Theme.BgPanel);
            Raylib.DrawRectangleLines(x, y, width, height, Theme.Border);

            // Left neon accent indicator bar
            Raylib.DrawRectangle(x, y, 4, height, accent);

            // Card Header line
            Theme.DrawText(title, x + 18, y + 10, 12, accent);
            Raylib.DrawLine(x + 14, y + 32, x + width - 14, y + 32, new Color((byte)Theme.Border.R, (byte)Theme.Border.G, (byte)Theme.Border.B, (byte)100));
        }
    }
}
