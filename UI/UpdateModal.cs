using System;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public static class UpdateModal
    {
        public static void Draw(int screenWidth, int screenHeight, float scale, bool inputActive = true)
        {
            if (!UpdateManager.ShowUpdatePrompt && !UpdateManager.IsDownloading) return;

            // Semi-transparent backdrop overlay
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 190));

            int popW = Math.Min((int)(540 * scale), screenWidth - 40);
            int popH = Math.Min((int)(310 * scale), screenHeight - 40);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            // Glass Modal Container
            Raylib.DrawRectangle(popX, popY, popW, popH, new Color(13, 18, 28, 252));
            Raylib.DrawRectangleLines(popX, popY, popW, popH, Theme.NeonCyan);
            Raylib.DrawRectangle(popX, popY, 4, popH, Theme.NeonCyan);

            // Header
            int headH = (int)(42 * scale);
            Raylib.DrawRectangle(popX, popY, popW, headH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + headH, popX + popW, popY + headH, Theme.Border);

            string title = "ДОСТУПНО ОБНОВЛЕНИЕ";
            Theme.DrawText(title, popX + 16, popY + (headH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.NeonGold);

            var rel = UpdateManager.LatestRelease;
            string newVer = rel?.TagName ?? "Новая версия";

            int curY = popY + headH + (int)(16 * scale);

            // Version info banner
            string verText = $"Текущая версия: {UpdateManager.CurrentVersion}  ➔  Новая версия: {newVer}";
            Theme.DrawText(verText, popX + 16, curY, 11, Theme.NeonCyan);
            curY += (int)(26 * scale);

            // Release description / changelog preview
            string desc = !string.IsNullOrEmpty(rel?.Body) 
                ? rel.Body.Trim() 
                : "В новой версии улучшена производительность, добавлены тиры блоков, рекорды Sideways/Backwards и обновлен интерфейс.";

            // Wrap / truncate description to 3 lines
            if (desc.Length > 220) desc = desc[..217] + "...";
            Theme.DrawText(desc, popX + 16, curY, 9, Theme.TextWhite);

            curY += (int)(68 * scale);

            // Safety notice: user profile is 100% preserved
            string notice = "✓ Все ваши рекорды, настройки и базы карт сохранятся автоматически!";
            Theme.DrawText(notice, popX + 16, curY, 9, Theme.NeonGreen);

            curY += (int)(32 * scale);

            if (UpdateManager.IsDownloading)
            {
                // Download progress bar
                int pbX = popX + 16;
                int pbY = curY;
                int pbW = popW - 32;
                int pbH = (int)(22 * scale);

                Raylib.DrawRectangle(pbX, pbY, pbW, pbH, new Color(20, 26, 38, 255));
                Raylib.DrawRectangleLines(pbX, pbY, pbW, pbH, Theme.Border);

                int fillW = (int)(pbW * UpdateManager.DownloadProgress);
                Raylib.DrawRectangle(pbX + 1, pbY + 1, fillW, pbH - 2, Theme.NeonCyan);

                string progText = $"Загрузка и установка: {UpdateManager.DownloadProgress * 100:F0}%";
                int progW = Theme.MeasureText(progText, 9);
                Theme.DrawText(progText, pbX + (pbW - progW) / 2, pbY + 4, 9, Theme.TextWhite);
            }
            else
            {
                // Action Buttons
                int btnH = (int)(32 * scale);
                int btnW = (popW - 44) / 2;
                int btnY = popY + popH - btnH - 16;

                // [ОБНОВИТЬ СЕЙЧАС]
                int btnUpdateX = popX + 16;
                if (Theme.DrawButton(btnUpdateX, btnY, btnW, btnH, "ОБНОВИТЬ СЕЙЧАС", true, 10, enabled: inputActive))
                {
                    _ = UpdateManager.PerformInAppUpdateAsync();
                }

                // [ПОЗЖЕ]
                int btnLaterX = btnUpdateX + btnW + 12;
                if (Theme.DrawButton(btnLaterX, btnY, btnW, btnH, "НАПОМНИТЬ ПОЗЖЕ", false, 10, enabled: inputActive))
                {
                    UpdateManager.ShowUpdatePrompt = false;
                }
            }
        }
    }
}
