using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public struct WavePoint
    {
        public float MouseYawVelocity; // deg/s or deg/tick
        public bool KeyA;
        public bool KeyD;
        public bool IsSync;
        public bool IsOverlap;
        public float InstantGain;
    }

    public static class OscilloscopeView
    {
        public static void Draw(List<WavePoint> history, int x, int y, int width, int height, float liveSync, float liveGain, float liveOverlapMs, float liveDeadAirMs)
        {
            Theme.DrawPanel(x, y, width, height, "LIVE SYNCHRONIZATION OSCILLOSCOPE");

            int graphX = x + 12;
            int graphY = y + 36;
            int graphW = width - 24;
            int graphH = height - 120;

            // Background of graph
            Raylib.DrawRectangle(graphX, graphY, graphW, graphH, new Color(9, 11, 15, 255));
            Raylib.DrawRectangleLines(graphX, graphY, graphW, graphH, Theme.Border);

            // Center zero line
            int midY = graphY + graphH / 2;
            Raylib.DrawLine(graphX, midY, graphX + graphW, midY, new Color(45, 52, 70, 180));
            Raylib.DrawText("0 deg/s", graphX + 6, midY - 6, 10, Theme.TextDim);
            Raylib.DrawText("+ TURN LEFT (A)", graphX + graphW - 110, graphY + 6, 10, Theme.NeonCyan);
            Raylib.DrawText("- TURN RIGHT (D)", graphX + graphW - 110, graphY + graphH - 16, 10, Theme.NeonOrange);

            if (history != null && history.Count > 1)
            {
                int count = history.Count;
                float stepX = (float)graphW / Math.Max(1, count - 1);

                // 1. Draw Keyboard A & D background highlight bands
                for (int i = 0; i < count; i++)
                {
                    var pt = history[i];
                    int px = (int)(graphX + i * stepX);
                    int pw = Math.Max(1, (int)stepX + 1);

                    if (pt.IsOverlap)
                    {
                        // Overlap Warning (Red Bar full height)
                        Raylib.DrawRectangle(px, graphY + 1, pw, graphH - 2, new Color(255, 23, 68, 80));
                    }
                    else if (pt.KeyA)
                    {
                        // Key A (Left) Upper band
                        Raylib.DrawRectangle(px, graphY + 1, pw, graphH / 2 - 1, new Color(0, 229, 255, 35));
                    }
                    else if (pt.KeyD)
                    {
                        // Key D (Right) Lower band
                        Raylib.DrawRectangle(px, midY + 1, pw, graphH / 2 - 2, new Color(255, 145, 0, 35));
                    }
                }

                // 2. Draw Mouse Yaw Velocity Curve
                float maxVal = 25.0f; // degrees per tick scale
                for (int i = 1; i < count; i++)
                {
                    var p0 = history[i - 1];
                    var p1 = history[i];

                    float y0 = midY - Math.Clamp(p0.MouseYawVelocity / maxVal, -1.0f, 1.0f) * (graphH / 2 - 10);
                    float y1 = midY - Math.Clamp(p1.MouseYawVelocity / maxVal, -1.0f, 1.0f) * (graphH / 2 - 10);

                    Vector2 v0 = new(graphX + (i - 1) * stepX, y0);
                    Vector2 v1 = new(graphX + i * stepX, y1);

                    Color waveCol = p1.IsOverlap ? Theme.NeonRed : (p1.IsSync ? Theme.NeonGreen : Theme.NeonOrange);
                    Raylib.DrawLineEx(v0, v1, 2.5f, waveCol);
                }

                // 3. Draw Sync Status strip at the bottom of graph
                int stripY = graphY + graphH - 8;
                for (int i = 0; i < count; i++)
                {
                    var pt = history[i];
                    int px = (int)(graphX + i * stepX);
                    int pw = Math.Max(1, (int)stepX + 1);

                    Color syncCol = pt.IsOverlap ? Theme.NeonRed : (pt.IsSync ? Theme.NeonGreen : Theme.TextDim);
                    Raylib.DrawRectangle(px, stripY, pw, 6, syncCol);
                }
            }

            // Live Telemetry Cards at bottom
            int cardY = graphY + graphH + 12;
            int cardW = (width - 24 - 36) / 4;
            int cardH = 58;

            DrawMeterCard(graphX, cardY, cardW, cardH, "LIVE SYNC", $"{liveSync:F1}%", liveSync >= 85 ? Theme.NeonGreen : (liveSync >= 65 ? Theme.NeonCyan : Theme.NeonRed));
            DrawMeterCard(graphX + cardW + 12, cardY, cardW, cardH, "LIVE GAIN", $"+{liveGain:F2} u/s", liveGain > 1.0f ? Theme.NeonGreen : Theme.NeonCyan);
            DrawMeterCard(graphX + (cardW + 12) * 2, cardY, cardW, cardH, "OVERLAP", $"{liveOverlapMs:F0} ms", liveOverlapMs > 0 ? Theme.NeonRed : Theme.NeonGreen);
            DrawMeterCard(graphX + (cardW + 12) * 3, cardY, cardW, cardH, "DEAD AIR", $"{liveDeadAirMs:F0} ms", liveDeadAirMs > 20 ? Theme.NeonOrange : Theme.TextMuted);
        }

        private static void DrawMeterCard(int x, int y, int width, int height, string label, string val, Color valCol)
        {
            Raylib.DrawRectangle(x, y, width, height, Theme.BgPanelHeader);
            Raylib.DrawRectangleLines(x, y, width, height, Theme.Border);

            Raylib.DrawText(label, x + 10, y + 8, 11, Theme.TextMuted);
            Raylib.DrawText(val, x + 10, y + 26, 20, valCol);
        }
    }
}
