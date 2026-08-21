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
            int popH = Math.Min((int)(420 * scale), screenHeight - 40);
            int popX = (screenWidth - popW) / 2;
            int popY = (screenHeight - popH) / 2;

            // TUI Modal Frame
            Theme.DrawTuiFrame(popX, popY, popW, popH, "UPDATE AVAILABLE", Theme.NeonGold, 12, false);

            // Header bar
            int headH = (int)(44 * scale);
            Raylib.DrawRectangle(popX, popY, popW, headH, Theme.BgPanelHeader);
            Raylib.DrawLine(popX, popY + headH, popX + popW, popY + headH, Theme.Border);

            string title = "NEW UPDATE AVAILABLE";
            Theme.DrawText(title, popX + 16, popY + (headH - Theme.GetScaledFontSize(13)) / 2, 13, Theme.NeonGold);

            var rel = UpdateManager.LatestRelease;
            string newVer = rel?.TagName ?? "new version";

            int curY = popY + headH + (int)(18 * scale);

            // Version info
            string verText = $"current: {UpdateManager.CurrentVersion}   -->   new: {newVer}";
            Theme.DrawText(verText, popX + 16, curY, 13, Theme.NeonCyan);
            curY += (int)(28 * scale);

            // Clean & Parse Changelog
            string rawBody = rel?.Body ?? "";
            string cleanBody = CleanMarkdown(rawBody);

            if (string.IsNullOrWhiteSpace(cleanBody))
            {
                cleanBody = "• Новые тиры блоков и рекорды блоков\n• Поддержка рекордов Sideways и Backwards\n• Обновленный интерфейс и оптимизация физики";
            }

            // Changelog header
            Theme.DrawText("Changelog:", popX + 16, curY, 11, Theme.TextMuted);
            curY += (int)(18 * scale);

            // Changelog box - TUI style
            int logBoxW = popW - 36;
            int logBoxH = (int)(160 * scale);
            Raylib.DrawRectangle(popX + 16, curY, logBoxW, logBoxH, new Color(4, 6, 10, 240));
            Raylib.DrawRectangleLines(popX + 16, curY, logBoxW, logBoxH, Theme.Border);

            string[] lines = cleanBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int lineY = curY + 8;
            for (int i = 0; i < lines.Length && lineY + (int)(22 * scale) <= curY + logBoxH - 8; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("-") && !line.StartsWith("*")) line = "- " + line;
                Theme.DrawText(line, popX + 28, lineY, 11, Theme.TextWhite);
                lineY += (int)(22 * scale);
            }

            curY += logBoxH + (int)(12 * scale);

            // Safety notice
            string notice = "[OK] All records, settings and profile are preserved.";
            Theme.DrawText(notice, popX + 16, curY, 11, Theme.NeonGreen);

            if (UpdateManager.IsDownloading)
            {
                int pbX = popX + 16;
                int pbY = popY + popH - (int)(50 * scale);
                int pbW = popW - 32;
                int pbH = (int)(28 * scale);

                Raylib.DrawRectangle(pbX, pbY, pbW, pbH, new Color(4, 8, 16, 255));
                Raylib.DrawRectangleLines(pbX, pbY, pbW, pbH, Theme.Border);

                int fillW = (int)(pbW * UpdateManager.DownloadProgress);
                if (fillW > 2)
                    Raylib.DrawRectangle(pbX + 1, pbY + 1, fillW - 2, pbH - 2, new Color(0, 100, 200, 200));

                string progText = $"downloading... {UpdateManager.DownloadProgress * 100:F0}%";
                int progW = Theme.MeasureText(progText, 11);
                Theme.DrawText(progText, pbX + (pbW - progW) / 2, pbY + (pbH - Theme.GetScaledFontSize(11)) / 2, 11, Theme.NeonCyan);
            }
            else
            {
                int btnH = (int)(38 * scale);
                int btnW = (popW - 52) / 2;
                int btnY = popY + popH - btnH - 14;

                if (Theme.DrawButton(popX + 16, btnY, btnW, btnH, "[UPDATE NOW]", true, 12, enabled: inputActive))
                    _ = UpdateManager.PerformInAppUpdateAsync();

                if (Theme.DrawButton(popX + 16 + btnW + 10, btnY, btnW, btnH, "[LATER]", false, 12, enabled: inputActive))
                    UpdateManager.ShowUpdatePrompt = false;
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
