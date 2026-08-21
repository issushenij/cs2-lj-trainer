using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public static class TrajectoryView
    {
        public static void Draw(JumpResult result, int x, int y, int width, int height)
        {
            Theme.DrawTechnicalBox(x, y, width, height, "2D FLIGHT TRAJECTORY (TOP-DOWN)", Theme.Border, Theme.BgPanel);

            int viewX = x + 10;
            int viewY = y + 36;
            int viewW = width - 20;
            int viewH = height - 46;

            // Background grid area - deep matte black with subtle dot matrix
            Raylib.DrawRectangle(viewX, viewY, viewW, viewH, Theme.BgDark);
            Raylib.DrawRectangleLines(viewX, viewY, viewW, viewH, Theme.Border);

            if (result == null || result.Trajectory2D == null || result.Trajectory2D.Count < 2)
            {
                string hint = "> WAITING FOR JUMP TELEMETRY <";
                int hw = Theme.MeasureText(hint, 12);
                Theme.DrawText(hint, viewX + (viewW - hw) / 2, viewY + viewH / 2 - 6, 12, Theme.TextDim);
                return;
            }

            // Calculate bounding box of trajectory
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var pt in result.Trajectory2D)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            float rangeX = MathF.Max(300.0f, maxX - minX + 40.0f);
            float rangeY = MathF.Max(120.0f, maxY - minY + 40.0f);

            float scaleX = (viewW - 60) / rangeX;
            float scaleY = (viewH - 40) / rangeY;
            float scale = MathF.Min(scaleX, scaleY);

            float originScreenX = viewX + 30;
            float originScreenY = viewY + viewH / 2;

            // Draw Target Unit Distance Markers (250, 260, 270, 275, 280, 285, 290)
            float[] markers = { 250, 260, 270, 275, 280, 285, 290 };
            foreach (var m in markers)
            {
                float markerScreenX = originScreenX + m * scale;
                if (markerScreenX < viewX + viewW - 10)
                {
                    Color mCol = m >= 275 ? Theme.NeonGold : Theme.TextDim;
                    Raylib.DrawLine((int)markerScreenX, viewY + 10, (int)markerScreenX, viewY + viewH - 10, new Color(mCol.R, mCol.G, mCol.B, (byte)45));
                    Theme.DrawText($"{m:0}", (int)markerScreenX - 8, viewY + viewH - 20, 10, mCol);
                }
            }

            // Draw Takeoff Platform Block
            Raylib.DrawRectangle(viewX + 5, (int)(originScreenY - 30), (int)(originScreenX - viewX - 5), 60, Theme.BgPanel);
            Raylib.DrawRectangleLines(viewX + 5, (int)(originScreenY - 30), (int)(originScreenX - viewX - 5), 60, Theme.Border);
            Raylib.DrawLine((int)originScreenX, (int)(originScreenY - 30), (int)originScreenX, (int)(originScreenY + 30), Theme.NeonGreen);
            Theme.DrawText("TAKEOFF", viewX + 8, (int)(originScreenY - 5), 9, Theme.NeonGreen);

            // Draw Trajectory as Console Matrix Points & Telemetry Filament
            int totalPts = result.Trajectory2D.Count;
            int strafeIndex = 0;
            int ptsPerStrafe = result.StrafeCount > 0 ? (totalPts / result.StrafeCount) : totalPts;

            for (int i = 1; i < totalPts; i++)
            {
                strafeIndex = Math.Min(result.StrafeCount - 1, i / Math.Max(1, ptsPerStrafe));
                Color segColor = Theme.StrafeColors[strafeIndex % Theme.StrafeColors.Length];

                var p0 = result.Trajectory2D[i - 1];
                var p1 = result.Trajectory2D[i];

                Vector2 s0 = new(originScreenX + p0.X * scale, originScreenY + p0.Y * scale);
                Vector2 s1 = new(originScreenX + p1.X * scale, originScreenY + p1.Y * scale);

                // Connecting filament
                Raylib.DrawLine((int)s0.X, (int)s0.Y, (int)s1.X, (int)s1.Y, new Color(segColor.R, segColor.G, segColor.B, (byte)70));

                // Console matrix square dot
                Raylib.DrawRectangle((int)s1.X - 1, (int)s1.Y - 1, 3, 3, segColor);
                if (i % 3 == 0)
                {
                    Raylib.DrawRectangle((int)s1.X, (int)s1.Y, 1, 1, Theme.TextWhite);
                }
            }

            // Landing Point Marker
            if (totalPts > 0)
            {
                var endPt = result.Trajectory2D[^1];
                Vector2 endScreen = new(originScreenX + endPt.X * scale, originScreenY + endPt.Y * scale);
                Raylib.DrawRectangle((int)endScreen.X - 2, (int)endScreen.Y - 2, 5, 5, Theme.NeonCyan);
                Raylib.DrawRectangleLines((int)endScreen.X - 6, (int)endScreen.Y - 6, 13, 13, Theme.NeonGold);

                string landText = $"[{result.Distance:F2}u]";
                Theme.DrawDisplayText(landText, (int)endScreen.X + 10, (int)endScreen.Y - 8, 14, Theme.NeonGold);
            }
        }
    }
}
