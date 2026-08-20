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
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 215));

            int popW = Math.Min((int)(720 * scale), screenWidth - 40);
            int popH = Math.Min((int)(460 * scale), screenHeight - 40);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            // Glass Modal Container
            Raylib.DrawRectangle(popX, popY, popW, popH, new Color(13, 18, 28, 252));
            Raylib.DrawRectangleLines(popX, popY, popW, popH, Theme.NeonCyan);
            Raylib.DrawRectangle(popX, popY, 6, popH, Theme.NeonCyan);

            // Header
            int headH = (int)(52 * scale);
            Raylib.DrawRectangle(popX, popY, popW, headH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + headH, popX + popW, popY + headH, Theme.Border);

            string title = "ДОСТУПНО НОВОЕ ОБНОВЛЕНИЕ";
            Theme.DrawText(title, popX + 24, popY + (headH - Theme.GetScaledFontSize(15)) / 2, 15, Theme.NeonGold);

            var rel = UpdateManager.LatestRelease;
            string newVer = rel?.TagName ?? "Новая версия";

            int curY = popY + headH + (int)(20 * scale);

            // Version info banner
            string verText = $"Текущая: {UpdateManager.CurrentVersion}   ➔   Новая: {newVer}";
            Theme.DrawText(verText, popX + 24, curY, 14, Theme.NeonCyan);
            curY += (int)(32 * scale);

            // Clean & Parse Changelog
            string rawBody = rel?.Body ?? "";
            string cleanBody = CleanMarkdown(rawBody);

            if (string.IsNullOrWhiteSpace(cleanBody))
            {
                cleanBody = "• Новые тиры блоков и рекорды блоков\n• Поддержка рекордов Sideways и Backwards\n• Обновленный интерфейс и оптимизация физики";
            }

            // Description Header
            Theme.DrawText("Что нового в этом обновлении:", popX + 24, curY, 12, Theme.TextWhite);
            curY += (int)(22 * scale);

            // Changelog Box
            int logBoxW = popW - 48;
            int logBoxH = (int)(180 * scale);
            Raylib.DrawRectangle(popX + 24, curY, logBoxW, logBoxH, new Color(9, 13, 20, 240));
            Raylib.DrawRectangleLines(popX + 24, curY, logBoxW, logBoxH, Theme.Border);

            string[] lines = cleanBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int lineY = curY + 12;
            for (int i = 0; i < lines.Length && lineY + (int)(22 * scale) <= curY + logBoxH; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("•") && !line.StartsWith("-")) line = "• " + line;

                Theme.DrawText(line, popX + 38, lineY, 12, Theme.TextWhite);
                lineY += (int)(24 * scale);
            }

            curY += logBoxH + (int)(16 * scale);

            // Safety notice: user profile is 100% preserved
            string notice = "✓ Все рекорды, пройденные карты и настройки сохраняются автоматически!";
            Theme.DrawText(notice, popX + 24, curY, 12, Theme.NeonGreen);

            if (UpdateManager.IsDownloading)
            {
                // Download progress bar
                int pbX = popX + 24;
                int pbY = popY + popH - (int)(54 * scale);
                int pbW = popW - 48;
                int pbH = (int)(32 * scale);

                Raylib.DrawRectangle(pbX, pbY, pbW, pbH, new Color(20, 26, 38, 255));
                Raylib.DrawRectangleLines(pbX, pbY, pbW, pbH, Theme.NeonCyan);

                int fillW = (int)(pbW * UpdateManager.DownloadProgress);
                Raylib.DrawRectangle(pbX + 1, pbY + 1, fillW, pbH - 2, new Color(0, 229, 255, 180));

                string progText = $"Загрузка и замена файлов: {UpdateManager.DownloadProgress * 100:F0}%";
                int progW = Theme.MeasureText(progText, 12);
                Theme.DrawText(progText, pbX + (pbW - progW) / 2, pbY + (pbH - Theme.GetScaledFontSize(12)) / 2, 12, Theme.TextWhite);
            }
            else
            {
                // Action Buttons
                int btnH = (int)(42 * scale);
                int btnW = (popW - 60) / 2;
                int btnY = popY + popH - btnH - 18;

                // [ОБНОВИТЬ СЕЙЧАС]
                int btnUpdateX = popX + 24;
                if (Theme.DrawButton(btnUpdateX, btnY, btnW, btnH, "ОБНОВИТЬ СЕЙЧАС", true, 13, enabled: inputActive))
                {
                    _ = UpdateManager.PerformInAppUpdateAsync();
                }

                // [ПОЗЖЕ]
                int btnLaterX = btnUpdateX + btnW + 12;
                if (Theme.DrawButton(btnLaterX, btnY, btnW, btnH, "НАПОМНИТЬ ПОЗЖЕ", false, 13, enabled: inputActive))
                {
                    UpdateManager.ShowUpdatePrompt = false;
                }
            }
        }

        private static string CleanMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // Remove markdown heading tokens and bold markers
            string res = text
                .Replace("###", "")
                .Replace("##", "")
                .Replace("#", "")
                .Replace("**", "")
                .Replace("`", "")
                .Replace("🚀", "")
                .Replace("🔄", "")
                .Replace("🏆", "")
                .Replace("🕹️", "")
                .Replace("🎨", "")
                .Replace("🧗", "")
                .Replace("🧱", "");

            return res.Trim();
        }
    }
}
