using System;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public static class JumpStatsView
    {
        public static void Draw(JumpResult result, int x, int y, int width, int height)
        {
            Theme.DrawPanel(x, y, width, height, "CKZ JUMPSTATS TELEMETRY");

            if (result == null || result.Distance <= 0)
            {
                string msg = "NO JUMP DATA - PRESS SPACE TO JUMP & STRAFE";
                int mw = Raylib.MeasureText(msg, 16);
                Raylib.DrawText(msg, x + (width - mw) / 2, y + height / 2 - 10, 16, Theme.TextDim);
                return;
            }

            int contentY = y + 36;

            // Tier Banner & Summary
            Color tierCol = Theme.GetTierColor(result.Tier);
            string mainStat = $"{result.Distance:F2} units";
            Raylib.DrawText(mainStat, x + 15, contentY, 28, Theme.NeonCyan);

            int distWidth = Raylib.MeasureText(mainStat, 28);
            Raylib.DrawRectangle(x + 25 + distWidth, contentY + 2, 130, 26, new Color(tierCol.R, tierCol.G, tierCol.B, (byte)40));
            Raylib.DrawRectangleLines(x + 25 + distWidth, contentY + 2, 130, 26, tierCol);
            
            string tierText = $"[ {result.Tier} ]";
            int tw = Raylib.MeasureText(tierText, 16);
            Raylib.DrawText(tierText, x + 25 + distWidth + (130 - tw) / 2, contentY + 7, 16, tierCol);

            // Sub-header stats row
            contentY += 34;
            string overviewLine1 = $"Mode: {AppConfig.Instance.Mode} | {result.StrafeCount} Strafes | Sync: {result.AvgSync:F1}% | Pre: {result.PreSpeed:F2} u/s | Max: {result.MaxSpeed:F2} u/s | Loss: {result.AvgLoss:F2} u/s";
            Raylib.DrawText(overviewLine1, x + 15, contentY, 14, Theme.TextWhite);

            contentY += 20;
            string wTag = result.W_Released ? "[OK] W-Released" : "[BAD] W Held in Air";
            Color wCol = result.W_Released ? Theme.NeonGreen : Theme.NeonRed;
            string overviewLine2 = $"Deviation: {result.Deviation:F2}u | Airpath: {result.Airpath:F3} | GainEff: {result.AvgGainEff:F1}% | BadAng: {result.AvgBadAngles:F1}% | Overlap: {result.AvgOverlap:F1}% | DeadAir: {result.AvgDeadAir:F1}% | Offset: {result.EdgeOffset:F2}u | ";
            Raylib.DrawText(overviewLine2, x + 15, contentY, 13, Theme.TextMuted);
            int line2Width = Raylib.MeasureText(overviewLine2, 13);
            Raylib.DrawText(wTag, x + 15 + line2Width, contentY, 13, wCol);

            // 12-Column Table Headers
            contentY += 28;
            int tableX = x + 12;
            int rowHeight = 22;

            // Column Widths
            int[] colW = { 26, 60, 56, 52, 60, 56, 64, 58, 58, 54, 56, 56, 120 };
            string[] headers = { "#", "Sync", "Gain", "Loss", "Max", "Air%", "BadAng", "Overl", "Dead", "Width", "AvGain", "GainEff", "AngRatio(Avg/Med/Min)" };

            // Header Background
            Raylib.DrawRectangle(tableX, contentY, width - 24, 22, Theme.BgPanelHeader);
            Raylib.DrawRectangleLines(tableX, contentY, width - 24, 22, Theme.Border);

            int cx = tableX + 4;
            for (int c = 0; c < headers.Length; c++)
            {
                Raylib.DrawText(headers[c], cx, contentY + 4, 12, Theme.TextMuted);
                cx += colW[c];
            }

            contentY += 24;

            // Strafe Rows
            for (int i = 0; i < result.Strafes.Count; i++)
            {
                var s = result.Strafes[i];
                Color rowBg = (i % 2 == 0) ? Theme.BgPanel : new Color(17, 20, 28, 255);
                Raylib.DrawRectangle(tableX, contentY, width - 24, rowHeight, rowBg);

                cx = tableX + 4;

                // Strafe number with color tag
                Color strafeColor = Theme.StrafeColors[i % Theme.StrafeColors.Length];
                Raylib.DrawCircle(cx + 6, contentY + rowHeight / 2, 4, strafeColor);
                Raylib.DrawText($"{s.Number}.", cx + 14, contentY + 4, 12, Theme.TextWhite);
                cx += colW[0];

                // Sync
                Color syncCol = s.Sync >= 85 ? Theme.NeonGreen : (s.Sync >= 70 ? Theme.NeonCyan : (s.Sync >= 50 ? Theme.NeonOrange : Theme.NeonRed));
                Raylib.DrawText($"{s.Sync:F1}%", cx, contentY + 4, 12, syncCol);
                cx += colW[1];

                // Gain
                Raylib.DrawText($"+{s.Gain:F2}", cx, contentY + 4, 12, Theme.NeonGreen);
                cx += colW[2];

                // Loss
                Color lossCol = s.Loss > 0.1f ? Theme.NeonRed : Theme.TextDim;
                Raylib.DrawText($"-{s.Loss:F2}", cx, contentY + 4, 12, lossCol);
                cx += colW[3];

                // Max Speed
                Raylib.DrawText($"{s.MaxSpeed:F1}", cx, contentY + 4, 12, Theme.TextWhite);
                cx += colW[4];

                // Airtime %
                Raylib.DrawText($"{s.AirtimePct:F1}%", cx, contentY + 4, 12, Theme.TextMuted);
                cx += colW[5];

                // BadAngles %
                Color badCol = s.BadAnglesPct > 25 ? Theme.NeonRed : (s.BadAnglesPct > 10 ? Theme.NeonOrange : Theme.NeonGreen);
                Raylib.DrawText($"{s.BadAnglesPct:F1}%", cx, contentY + 4, 12, badCol);
                cx += colW[6];

                // Overlap %
                Color overCol = s.OverlapPct > 0 ? Theme.NeonRed : Theme.TextDim;
                Raylib.DrawText($"{s.OverlapPct:F1}%", cx, contentY + 4, 12, overCol);
                cx += colW[7];

                // DeadAir %
                Color deadCol = s.DeadAirPct > 5 ? Theme.NeonOrange : Theme.TextDim;
                Raylib.DrawText($"{s.DeadAirPct:F1}%", cx, contentY + 4, 12, deadCol);
                cx += colW[8];

                // Width (degrees)
                Raylib.DrawText($"{s.WidthDegrees:F1} deg", cx, contentY + 4, 12, Theme.TextWhite);
                cx += colW[9];

                // AvgGain
                Raylib.DrawText($"{s.AvgGainPerTick:F2}", cx, contentY + 4, 12, Theme.NeonCyan);
                cx += colW[10];

                // GainEff
                Color effCol = s.GainEff >= 70 ? Theme.NeonGreen : (s.GainEff >= 50 ? Theme.NeonCyan : Theme.NeonOrange);
                Raylib.DrawText($"{s.GainEff:F1}%", cx, contentY + 4, 12, effCol);
                cx += colW[11];

                // AngRatio (Avg / Med / Min)
                Raylib.DrawText($"{s.AngRatioAvg:+0.00;-0.00;0.00} | {s.AngRatioMed:+0.00;-0.00;0.00} | {s.AngRatioMin:+0.00;-0.00;0.00}", cx, contentY + 4, 11, Theme.TextMuted);

                contentY += rowHeight;
            }

            // Key Timeline Matrix
            contentY += 12;
            Raylib.DrawText("KEY TIMELINE MATRIX:", tableX, contentY, 12, Theme.NeonCyan);
            contentY += 18;

            Raylib.DrawText("LEFT KEY  (A) | ", tableX, contentY, 12, Theme.TextMuted);
            int leftLabelW = Raylib.MeasureText("LEFT KEY  (A) | ", 12);
            DrawTimelineText(result.LeftKeyTimeline, tableX + leftLabelW, contentY, 'L', Theme.NeonCyan);

            contentY += 16;
            Raylib.DrawText("RIGHT KEY (D) | ", tableX, contentY, 12, Theme.TextMuted);
            int rightLabelW = Raylib.MeasureText("RIGHT KEY (D) | ", 12);
            DrawTimelineText(result.RightKeyTimeline, tableX + rightLabelW, contentY, 'R', Theme.NeonOrange);
        }

        private static void DrawTimelineText(string timeline, int x, int y, char keyChar, Color activeColor)
        {
            int cx = x;
            foreach (char c in timeline)
            {
                if (c == keyChar)
                {
                    Raylib.DrawText(c.ToString(), cx, y, 12, activeColor);
                }
                else
                {
                    Raylib.DrawText(".", cx, y, 12, Theme.TextDim);
                }
                cx += 8;
            }
        }
    }
}
