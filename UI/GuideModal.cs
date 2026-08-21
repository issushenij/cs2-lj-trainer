using System;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public class GuideModal
    {
        public bool IsOpen { get; set; } = true;
        private int _activeTab = 0; // 0 = Как тренироваться, 1 = Словарь понятий LJ
        private float _openAnimProgress = 0.0f;

        public void Draw(int screenWidth, int screenHeight)
        {
            if (!IsOpen)
            {
                _openAnimProgress = 0.0f;
                return;
            }

            // Smooth spring pop-in entrance
            _openAnimProgress += Raylib.GetFrameTime() * 6.0f;
            if (_openAnimProgress > 1.0f) _openAnimProgress = 1.0f;

            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;

            int modalW = Math.Min(1180, screenWidth - 24);
            int modalH = Math.Min(740, screenHeight - 30);

            // Pop-in slide offset
            int animOffsetY = (int)((1.0f - _openAnimProgress) * 20.0f);
            int modalX = (screenWidth - modalW) / 2;
            int modalY = (screenHeight - modalH) / 2 + animOffsetY;

            // Semi-transparent backdrop overlay with smooth fade
            byte dimAlpha = (byte)(225 * _openAnimProgress);
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color((byte)0, (byte)0, (byte)0, dimAlpha));

            Vector2 mouse = Raylib.GetMousePosition();
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (mouse.X < modalX || mouse.X > modalX + modalW || mouse.Y < modalY || mouse.Y > modalY + modalH)
                {
                    IsOpen = false;
                    return;
                }
            }

            // TUI glass modal container
            Theme.DrawTuiFrame(modalX, modalY, modalW, modalH, "CS2 LJ LAB - GUIDE", Theme.Border, 12);

            // Header bar
            int headerH = (int)(42 * scale);
            Raylib.DrawRectangle(modalX, modalY, modalW, headerH, Theme.BgPanelHeader);
            Raylib.DrawLine(modalX, modalY + headerH, modalX + modalW, modalY + headerH, Theme.Border);

            Theme.DrawText("CS2 LONG JUMP LAB -- GUIDE & REFERENCE", modalX + 16, modalY + (headerH - Theme.GetScaledFontSize(14)) / 2, 14, Theme.NeonCyan);

            int closeBtnW = (int)(110 * scale);
            int closeBtnH = (int)(28 * scale);
            if (Theme.DrawButton(modalX + modalW - closeBtnW - 12, modalY + (headerH - closeBtnH) / 2, closeBtnW, closeBtnH, "[CLOSE] ESC", false, 11))
            {
                IsOpen = false;
            }

            // Navigation Tabs
            int tabY = modalY + headerH + 8;
            int tabW = (int)(260 * scale);
            int tabH = (int)(34 * scale);

            if (Theme.DrawButton(modalX + 18, tabY, tabW, tabH, "[1. Как тренироваться]", _activeTab == 0, 13))
            {
                _activeTab = 0;
            }
            if (Theme.DrawButton(modalX + 24 + tabW, tabY, tabW, tabH, "[2. Словарь понятий LJ]", _activeTab == 1, 13))
            {
                _activeTab = 1;
            }

            // Bottom Action Bar
            int bottomH = (int)(52 * scale);
            int bottomY = modalY + modalH - bottomH - 8;
            Raylib.DrawLine(modalX, bottomY - 4, modalX + modalW, bottomY - 4, Theme.Border);

            // Don't show again toggle checkbox button
            int checkW = (int)(250 * scale);
            int checkH = (int)(32 * scale);
            string checkLabel = cfg.ShowWelcomeGuideOnStartup ? "[ ] Больше не показывать" : "[x] Больше не показывать";
            Color checkCol = cfg.ShowWelcomeGuideOnStartup ? Theme.TextDim : Theme.NeonCyan;
            if (Theme.DrawButton(modalX + 18, bottomY + (bottomH - checkH) / 2, checkW, checkH, checkLabel, !cfg.ShowWelcomeGuideOnStartup, 11))
            {
                cfg.ShowWelcomeGuideOnStartup = !cfg.ShowWelcomeGuideOnStartup;
                AppConfig.Save();
            }

            int startBtnW = (int)(300 * scale);
            int startBtnH = (int)(38 * scale);
            int startBtnX = modalX + modalW - startBtnW - 18;

            if (Theme.DrawButton(startBtnX, bottomY + (bottomH - startBtnH) / 2, startBtnW, startBtnH, "[SPACE] START TRAINING", true, 12))
            {
                IsOpen = false;
            }

            // Available Content Area (guaranteed no overlap)
            int contentY = tabY + tabH + 10;
            int availableH = bottomY - contentY - 8;
            int cx = modalX + 18;
            int cw = modalW - 36;

            if (_activeTab == 0)
            {
                DrawHowToTrainTab(cx, contentY, cw, availableH, scale);
            }
            else
            {
                DrawGlossaryTab(cx, contentY, cw, availableH, scale);
            }
        }

        private void DrawHowToTrainTab(int cx, int cy, int cw, int availableH, float scale)
        {
            int cardCount = 4;
            int gap = 8;
            int cardH = (availableH - (cardCount - 1) * gap) / cardCount;

            DrawStepCard(cx, cy, cw, cardH,
                "ШАГ 1: ЗАПУСК И ЗАХВАТ МЫШИ (ПРОБЕЛ)",
                "Нажмите клавишу ПРОБЕЛ (Space), чтобы начать тренировку. Мышь блокируется в прямой режим CS2 Raw Input без системного ускорения Windows.",
                Theme.NeonCyan);

            cy += cardH + gap;
            DrawStepCard(cx, cy, cw, cardH,
                "ШАГ 2: ПЛАВНОСТЬ СИНУСОИДЫ (БЕЗ РЫВКОВ)",
                "Ведите мышь влево и вправо непрерывной плавной синусоидой. Зелёный шлейф означает идеальную плавность, красный - резкий рывок руки (Jerk).",
                Theme.NeonGreen);

            cy += cardH + gap;
            DrawStepCard(cx, cy, cw, cardH,
                "ШАГ 3: СИНХРОНИЗАЦИЯ A / D (СИНХРА)",
                "При движении мыши влево - держите [A]. При движении вправо - держите [D]. В момент смены направления сразу отпускайте одну кнопку и жмите другую.",
                Theme.NeonGold);

            cy += cardH + gap;
            DrawStepCard(cx, cy, cw, cardH,
                "ШАГ 4: ТЕМП МЕТРОНОМА И УГОЛ 30-35 ГРАДУСОВ",
                "Слушайте щелчки звука - это ваш темп. Не размахивайте мышью слишком широко: идеальный угол поворота мыши для CS2 LJ - 30-35 градусов.",
                Theme.NeonOrange);
        }

        private void DrawStepCard(int x, int y, int w, int h, string title, string desc, Color accent)
        {
            // TUI box frame with accent-colored title
            Theme.DrawTuiFrame(x, y, w, h, title, accent, 11);
            // Content text below the border row
            int charH = Theme.GetScaledFontSize(11);
            Theme.DrawWrappedText(desc, x + 14, y + charH + 6, w - 28, 12, 3, Theme.TextWhite);
        }

        private void DrawGlossaryTab(int cx, int cy, int cw, int availableH, float scale)
        {
            int colW = (cw - 14) / 2;
            int cardCount = 4; // 4 rows per column
            int gap = 8;
            int cardH = (availableH - (cardCount - 1) * gap) / cardCount;

            // Column 1
            int c1Y = cy;
            DrawTermCard(cx, c1Y, colW, cardH, "SYNC (СИНХРОНИЗАЦИЯ)",
                "Процент времени полёта, когда поворот мыши совпадает с нажатой клавишей A или D. Норма PRO: > 85%.", Theme.NeonGreen);

            c1Y += cardH + gap;
            DrawTermCard(cx, c1Y, colW, cardH, "BAD ANGLES (ПЛОХИЕ УГЛЫ / ЗАНОС / РЫВКИ)",
                "Доля времени, когда мышь разворачивается слишком широко (>40° за мах) или делает резкие рывки (Jerk). В CS2 срывает траекторию и тормозит игрока. Норма: < 3%!", Theme.NeonRed);

            c1Y += cardH + gap;
            DrawTermCard(cx, c1Y, colW, cardH, "OVERLAP (ПЕРЕКРЫТИЕ КЛАВИШ)",
                "Время, когда одновременно зажаты [A] и [D]. В CS2 это мгновенно обнуляет ускорение и сбрасывает скорость. Должно быть 0 мс!", Theme.NeonOrange);

            c1Y += cardH + gap;
            DrawTermCard(cx, c1Y, colW, cardH, "DEAD AIR (ПУСТОЙ ПОВОРОТ)",
                "Пауза между отпусканием одной клавиши и нажатием другой во время движения мыши. Приводит к потере ускорения.", Theme.NeonGold);

            // Column 2
            int c2X = cx + colW + 14;
            int c2Y = cy;

            DrawTermCard(c2X, c2Y, colW, cardH, "STRAFE CADENCE (ТЕМП СТРЕЙФОВ)",
                "Длительность одного стрейфа в мс (BPM). Стандарт CS2: 8 стрейфов за 800 мс прыжка (по ~90-95 мс на стрейф).", Theme.NeonCyan);

            c2Y += cardH + gap;
            DrawTermCard(c2X, c2Y, colW, cardH, "STRAFE SWEEP (УГОЛ ПОВОРОТА)",
                "Амплитуда разворота мыши за один мах. Идеальный угол в CS2: 30-35 градусов. < 25° - мало скорости, > 40° - срыв и потеря.", Theme.NeonCyan);

            c2Y += cardH + gap;
            DrawTermCard(c2X, c2Y, colW, cardH, "GAIN EFFICIENCY (ПРИРОСТ СКОРОСТИ)",
                "Набранная скорость в units/sec. При хорошей технике: +12..+16 u/s за каждый стрейф в прыжке.", Theme.NeonPurple);

            c2Y += cardH + gap;
            DrawTermCard(c2X, c2Y, colW, cardH, "SPEED LOSS (ПОТЕРЯ СКОРОСТИ)",
                "Потери скорости при контр-стрейфе, зажатии A+D или заносе. При чистом прыжке должно быть строго 0.00 u/s!", Theme.NeonRed);
        }

        private void DrawTermCard(int x, int y, int w, int h, string term, string meaning, Color accent)
        {
            Theme.DrawTuiFrame(x, y, w, h, term, accent, 10);
            int charH = Theme.GetScaledFontSize(10);
            Theme.DrawWrappedText(meaning, x + 12, y + charH + 5, w - 24, 11, 3, Theme.TextMuted);
        }
    }
}
