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
            Theme.DrawTechnicalBox(x, y, width, height, "LIVE SYNCHRONIZATION OSCILLOSCOPE", Theme.Border, Theme.BgPanel);

            int graphX = x + 12;
            int graphY = y + 36;
            int graphW = width - 24;
            int graphH = height - 110;

            // Background of graph - deep matte black
            Raylib.DrawRectangle(graphX, graphY, graphW, graphH, Theme.BgDark);
            Raylib.DrawRectangleLines(graphX, graphY, graphW, graphH, Theme.Border);

            // Center zero line
            int midY = graphY + graphH / 2;
            Raylib.DrawLine(graphX, midY, graphX + graphW, midY, new Color(45, 45, 45, 180));
            Theme.DrawText("0 deg/s", graphX + 6, midY - 6, 10, Theme.TextDim);
            Theme.DrawText("+ TURN LEFT (A)", graphX + graphW - 120, graphY + 6, 10, Theme.NeonCyan);
            Theme.DrawText("- TURN RIGHT (D)", graphX + graphW - 120, graphY + graphH - 16, 10, Theme.NeonOrange);

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
                        Raylib.DrawRectangle(px, graphY + 1, pw, graphH - 2, new Color(240, 70, 70, 60));
                    }
                    else if (pt.KeyA)
                    {
                        Raylib.DrawRectangle(px, graphY + 1, pw, graphH / 2 - 1, new Color(75, 170, 255, 30));
                    }
                    else if (pt.KeyD)
                    {
                        Raylib.DrawRectangle(px, midY + 1, pw, graphH / 2 - 2, new Color(245, 120, 40, 30));
                    }
                }

                // 2. Draw Mouse Yaw Velocity Curve with Smooth Catmull-Rom Spline
                float maxVal = 25.0f;
                List<Vector2> rawWave = new();
                for (int i = 0; i < count; i++)
                {
                    var p = history[i];
                    float pyVal = midY - Math.Clamp(p.MouseYawVelocity / maxVal, -1.0f, 1.0f) * (graphH / 2 - 10);
                    rawWave.Add(new Vector2(graphX + i * stepX, pyVal));
                }

                if (rawWave.Count >= 2)
                {
                    int subDiv = 4;
                    for (int i = 0; i < rawWave.Count - 1; i++)
                    {
                        Vector2 p0 = i > 0 ? rawWave[i - 1] : rawWave[i];
                        Vector2 p1 = rawWave[i];
                        Vector2 p2 = rawWave[i + 1];
                        Vector2 p3 = i + 2 < rawWave.Count ? rawWave[i + 2] : p2;

                        var pt = history[i + 1];
                        Color waveCol = pt.IsOverlap ? Theme.NeonRed : (pt.IsSync ? Theme.NeonGreen : Theme.NeonOrange);

                        Vector2 prevSpline = p1;
                        for (int s = 1; s <= subDiv; s++)
                        {
                            float t = s / (float)subDiv;
                            float t2 = t * t;
                            float t3 = t2 * t;

                            Vector2 currSpline = 0.5f * (
                                (2f * p1) +
                                (-p0 + p2) * t +
                                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                            );

                            Raylib.DrawLineEx(prevSpline, currSpline, 3.5f, new Color(waveCol.R, waveCol.G, waveCol.B, (byte)40));
                            Raylib.DrawLineEx(prevSpline, currSpline, 1.8f, waveCol);

                            prevSpline = currSpline;
                        }
                    }
                }

                // 3. Draw Sync Status strip at the bottom of graph
                int stripY = graphY + graphH - 6;
                for (int i = 0; i < count; i++)
                {
                    var pt = history[i];
                    int px = (int)(graphX + i * stepX);
                    int pw = Math.Max(1, (int)stepX + 1);

                    Color syncCol = pt.IsOverlap ? Theme.NeonRed : (pt.IsSync ? Theme.NeonGreen : Theme.TextDim);
                    Raylib.DrawRectangle(px, stripY, pw, 5, syncCol);
                }
            }

            // Live Telemetry Cards at bottom (Obsidian minimal blocks)
            int cardY = graphY + graphH + 10;
            int cardW = (width - 24 - 36) / 4;
            int cardH = 54;

            DrawMeterCard(graphX, cardY, cardW, cardH, "LIVE SYNC", $"{liveSync:F1}%", liveSync >= 85 ? Theme.NeonGreen : (liveSync >= 65 ? Theme.NeonCyan : Theme.NeonRed));
            DrawMeterCard(graphX + cardW + 12, cardY, cardW, cardH, "LIVE GAIN", $"+{liveGain:F2} u/s", liveGain > 1.0f ? Theme.NeonGreen : Theme.NeonCyan);
            DrawMeterCard(graphX + (cardW + 12) * 2, cardY, cardW, cardH, "OVERLAP", $"{liveOverlapMs:F0} ms", liveOverlapMs > 0 ? Theme.NeonRed : Theme.NeonGreen);
            DrawMeterCard(graphX + (cardW + 12) * 3, cardY, cardW, cardH, "DEAD AIR", $"{liveDeadAirMs:F0} ms", liveDeadAirMs > 20 ? Theme.NeonOrange : Theme.TextMuted);
        }

        private static void DrawMeterCard(int x, int y, int width, int height, string label, string val, Color valCol)
        {
            Theme.DrawTechnicalBox(x, y, width, height, null, Theme.Border, Theme.BgPanel, false);
            Theme.DrawText(label, x + 10, y + 6, 10, Theme.TextMuted);
            Theme.DrawDisplayText(val, x + 10, y + 22, 18, valCol);
        }
    }
}
