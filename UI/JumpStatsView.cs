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
            Theme.DrawTechnicalBox(x, y, width, height, "CKZ JUMPSTATS TELEMETRY", Theme.Border, Theme.BgPanel);

            if (result == null || result.Distance <= 0)
            {
                string msg = "> NO JUMP TELEMETRY // ENGAGE [SPACE] TO RECORD JUMP <";
                int mw = Theme.MeasureText(msg, 13);
                Theme.DrawText(msg, x + (width - mw) / 2, y + height / 2 - 8, 13, Theme.TextDim);
                return;
            }

            int contentY = y + 36;

            // Tier Banner & Summary
            Color tierCol = Theme.GetTierColor(result.Tier);
            string mainStat = $"{result.Distance:F2}u";
            Theme.DrawDisplayText(mainStat, x + 15, contentY, 32, Theme.NeonCyan);

            int distWidth = Theme.MeasureDisplayText(mainStat, 32);
            Raylib.DrawRectangle(x + 25 + distWidth, contentY + 2, 120, 26, new Color(tierCol.R, tierCol.G, tierCol.B, (byte)35));
            Raylib.DrawRectangleLines(x + 25 + distWidth, contentY + 2, 120, 26, tierCol);
            
            string tierText = $"[ {result.Tier.ToUpperInvariant()} ]";
            int tw = Theme.MeasureText(tierText, 12);
            Theme.DrawText(tierText, x + 25 + distWidth + (120 - tw) / 2, contentY + 7, 12, tierCol);

            // Sub-header stats row
            contentY += 32;
            string overviewLine1 = $"ENGINE: {AppConfig.Instance.Mode} // STRAFES: {result.StrafeCount} // SYNC: {result.AvgSync:F1}% // PRE: {result.PreSpeed:F2} u/s // MAX: {result.MaxSpeed:F2} u/s // LOSS: {result.AvgLoss:F2} u/s";
            Theme.DrawText(overviewLine1, x + 15, contentY, 11, Theme.TextWhite);

            contentY += 18;
            string wTag = result.W_Released ? "[OK] W-RELEASED" : "[WARN] W-HELD IN AIR";
            Color wCol = result.W_Released ? Theme.NeonGreen : Theme.NeonRed;
            string overviewLine2 = $"DEV: {result.Deviation:F2}u | AIRPATH: {result.Airpath:F3} | GAIN_EFF: {result.AvgGainEff:F1}% | BAD_ANG: {result.AvgBadAngles:F1}% | OVERLAP: {result.AvgOverlap:F1}% | DEAD_AIR: {result.AvgDeadAir:F1}% | ";
            Theme.DrawText(overviewLine2, x + 15, contentY, 10, Theme.TextMuted);
            int line2Width = Theme.MeasureText(overviewLine2, 10);
            Theme.DrawText(wTag, x + 15 + line2Width, contentY, 10, wCol);

            // 12-Column Table Headers
            contentY += 24;
            int tableX = x + 12;
            int rowHeight = 20;

            // Column Widths
            int[] colW = { 26, 60, 56, 52, 60, 56, 64, 58, 58, 54, 56, 56, 120 };
            string[] headers = { "#", "SYNC", "GAIN", "LOSS", "MAX", "AIR%", "BAD_ANG", "OVERLAP", "DEAD_AIR", "SWEEP", "AVG_GAIN", "EFF%", "ANG_RATIO" };

            // Header Background
            Raylib.DrawRectangle(tableX, contentY, width - 24, rowHeight, Theme.BgPanel);
            Raylib.DrawRectangleLines(tableX, contentY, width - 24, rowHeight, Theme.Border);

            int cx = tableX + 4;
            for (int c = 0; c < headers.Length; c++)
            {
                Theme.DrawText(headers[c], cx, contentY + 4, 9, Theme.TextMuted);
                cx += colW[c];
            }

            contentY += 22;

            // Strafe Rows
            for (int i = 0; i < result.Strafes.Count; i++)
            {
                var s = result.Strafes[i];
                Color rowBg = (i % 2 == 0) ? Theme.BgPanel : Theme.BgDark;
                Raylib.DrawRectangle(tableX, contentY, width - 24, rowHeight, rowBg);

                cx = tableX + 4;

                // Strafe number with color tag
                Color strafeColor = Theme.StrafeColors[i % Theme.StrafeColors.Length];
                Raylib.DrawRectangle(cx + 2, contentY + 6, 3, 8, strafeColor);
                Theme.DrawText($"{s.Number}", cx + 8, contentY + 3, 10, Theme.TextWhite);
                cx += colW[0];

                // Sync
                Color syncCol = s.Sync >= 85 ? Theme.NeonGreen : (s.Sync >= 70 ? Theme.NeonCyan : (s.Sync >= 50 ? Theme.NeonOrange : Theme.NeonRed));
                Theme.DrawText($"{s.Sync:F1}%", cx, contentY + 3, 10, syncCol);
                cx += colW[1];

                // Gain
                Theme.DrawText($"+{s.Gain:F2}", cx, contentY + 3, 10, Theme.NeonBlue);
                cx += colW[2];

                // Loss
                Color lossCol = s.Loss > 0.1f ? Theme.NeonRed : Theme.TextDim;
                Theme.DrawText($"-{s.Loss:F2}", cx, contentY + 3, 10, lossCol);
                cx += colW[3];

                // Max Speed
                Theme.DrawText($"{s.MaxSpeed:F1}", cx, contentY + 3, 10, Theme.TextWhite);
                cx += colW[4];

                // Airtime %
                Theme.DrawText($"{s.AirtimePct:F1}%", cx, contentY + 3, 10, Theme.TextMuted);
                cx += colW[5];

                // BadAngles %
                Color badCol = s.BadAnglesPct > 25 ? Theme.NeonRed : (s.BadAnglesPct > 10 ? Theme.NeonOrange : Theme.NeonGreen);
                Theme.DrawText($"{s.BadAnglesPct:F1}%", cx, contentY + 3, 10, badCol);
                cx += colW[6];

                // Overlap %
                Color overCol = s.OverlapPct > 0 ? Theme.NeonRed : Theme.TextDim;
                Theme.DrawText($"{s.OverlapPct:F1}%", cx, contentY + 3, 10, overCol);
                cx += colW[7];

                // DeadAir %
                Color deadCol = s.DeadAirPct > 5 ? Theme.NeonOrange : Theme.TextDim;
                Theme.DrawText($"{s.DeadAirPct:F1}%", cx, contentY + 3, 10, deadCol);
                cx += colW[8];

                // Width (degrees)
                Theme.DrawText($"{s.WidthDegrees:F1} deg", cx, contentY + 3, 10, Theme.TextWhite);
                cx += colW[9];

                // AvgGain
                Theme.DrawText($"{s.AvgGainPerTick:F2}", cx, contentY + 3, 10, Theme.NeonCyan);
                cx += colW[10];

                // GainEff
                Color effCol = s.GainEff >= 70 ? Theme.NeonGreen : (s.GainEff >= 50 ? Theme.NeonCyan : Theme.NeonOrange);
                Theme.DrawText($"{s.GainEff:F1}%", cx, contentY + 3, 10, effCol);
                cx += colW[11];

                // AngRatio (Avg / Med / Min)
                Theme.DrawText($"{s.AngRatioAvg:+0.00;-0.00;0.00} | {s.AngRatioMed:+0.00;-0.00;0.00}", cx, contentY + 3, 10, Theme.TextMuted);

                contentY += rowHeight;
            }

            // Key Timeline Matrix
            contentY += 10;
            Theme.DrawText("KEY TIMELINE MATRIX:", tableX, contentY, 10, Theme.NeonCyan);
            contentY += 16;

            Theme.DrawText("LEFT KEY  (A) | ", tableX, contentY, 10, Theme.TextMuted);
            int leftLabelW = Theme.MeasureText("LEFT KEY  (A) | ", 10);
            DrawTimelineText(result.LeftKeyTimeline, tableX + leftLabelW, contentY, 'L', Theme.NeonCyan);

            contentY += 14;
            Theme.DrawText("RIGHT KEY (D) | ", tableX, contentY, 10, Theme.TextMuted);
            int rightLabelW = Theme.MeasureText("RIGHT KEY (D) | ", 10);
            DrawTimelineText(result.RightKeyTimeline, tableX + rightLabelW, contentY, 'R', Theme.NeonOrange);
        }

        private static void DrawTimelineText(string timeline, int x, int y, char keyChar, Color activeColor)
        {
            int cx = x;
            foreach (char c in timeline)
            {
                if (c == keyChar)
                {
                    Theme.DrawText(c.ToString(), cx, y, 10, activeColor);
                }
                else
                {
                    Theme.DrawText(".", cx, y, 10, Theme.TextDim);
                }
                cx += 8;
            }
        }
    }
}
