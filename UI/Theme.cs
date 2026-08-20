using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public static class Theme
    {
        public static Font FontSmall { get; private set; }   // 16-22px
        public static Font FontMedium { get; private set; }  // 24-34px
        public static Font FontLarge { get; private set; }   // 36-54px
        public static Font FontHuge { get; private set; }    // 56-80px
        public static bool HasCustomFont { get; private set; } = false;

        public static void InitializeFont()
        {
            try
            {
                // Prefer clean UI fonts with high legibility and strong letterforms (Segoe UI SemiBold / Regular / Arial)
                string fontPath = @"C:\Windows\Fonts\segoeuib.ttf";
                if (!File.Exists(fontPath)) fontPath = @"C:\Windows\Fonts\segoeui.ttf";
                if (!File.Exists(fontPath)) fontPath = @"C:\Windows\Fonts\arialbd.ttf";
                if (!File.Exists(fontPath)) fontPath = @"C:\Windows\Fonts\arial.ttf";

                if (File.Exists(fontPath))
                {
                    var cpList = new HashSet<int>();

                    // 1. Basic Latin & Latin-1 Supplement (0x0020..0x00FF) - contains °, «, », ±, ×, etc.
                    for (int i = 32; i <= 255; i++) cpList.Add(i);

                    // 2. Latin Extended A & B (0x0100..0x024F)
                    for (int i = 0x0100; i <= 0x024F; i++) cpList.Add(i);

                    // 3. Cyrillic & Cyrillic Supplement (0x0400..0x052F) - Full Russian А..я, Ё, ё
                    for (int i = 0x0400; i <= 0x052F; i++) cpList.Add(i);

                    // 4. General Punctuation (0x2000..0x206F) - em-dash, en-dash, bullet, quotes
                    for (int i = 0x2000; i <= 0x206F; i++) cpList.Add(i);

                    // 5. Arrows & Math Operators (0x2190..0x22FF) - →, ←, ↔, ≈, ≠, ≤, ≥, ≡, ▲, ▼
                    for (int i = 0x2190; i <= 0x22FF; i++) cpList.Add(i);

                    // 6. Geometric Shapes & Misc Symbols (0x2500..0x27BF) - ▶, ⏸, ■, ✓, ✕, ⚙, ⭐, 🗺, 🏁
                    for (int i = 0x2500; i <= 0x27BF; i++) cpList.Add(i);

                    int[] codepoints = new int[cpList.Count];
                    cpList.CopyTo(codepoints);
                    Array.Sort(codepoints);

                    // Multi-tier rasterization so text is never downscaled or blurred
                    FontSmall = Raylib.LoadFontEx(fontPath, 20, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontSmall.Texture, TextureFilter.Bilinear);

                    FontMedium = Raylib.LoadFontEx(fontPath, 32, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontMedium.Texture, TextureFilter.Bilinear);

                    FontLarge = Raylib.LoadFontEx(fontPath, 48, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontLarge.Texture, TextureFilter.Bilinear);

                    FontHuge = Raylib.LoadFontEx(fontPath, 72, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontHuge.Texture, TextureFilter.Bilinear);

                    HasCustomFont = true;
                }
            }
            catch
            {
                HasCustomFont = false;
            }
        }

        private static Font GetBestFont(int scaledSize)
        {
            if (scaledSize <= 22) return FontSmall;
            if (scaledSize <= 36) return FontMedium;
            if (scaledSize <= 56) return FontLarge;
            return FontHuge;
        }

        public static int GetScaledFontSize(int baseSize)
        {
            float scale = Math.Clamp(AppConfig.Instance.UiScale, 0.8f, 1.6f);
            return Math.Max(8, (int)Math.Round(baseSize * scale));
        }

        public static void DrawText(string text, int x, int y, int fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont)
            {
                Font f = GetBestFont(scaled);
                Raylib.DrawTextEx(f, text, new Vector2(x, y), scaled, 0.0f, color);
            }
            else
            {
                Raylib.DrawText(text, x, y, scaled, color);
            }
        }

        public static int MeasureText(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont)
            {
                Font f = GetBestFont(scaled);
                return (int)Math.Ceiling(Raylib.MeasureTextEx(f, text, scaled, 0.0f).X);
            }
            return Raylib.MeasureText(text, scaled);
        }

        public static int DrawWrappedText(string text, int x, int y, int maxWidth, int fontSize, int lineSpacing, Color color)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int scaledSize = GetScaledFontSize(fontSize);
            int curY = y;
            string[] paragraphs = text.Split('\n');

            foreach (var para in paragraphs)
            {
                string[] words = para.Split(' ');
                string curLine = "";

                foreach (var word in words)
                {
                    string testLine = string.IsNullOrEmpty(curLine) ? word : $"{curLine} {word}";
                    int testW = MeasureText(testLine, fontSize);

                    if (testW > maxWidth && !string.IsNullOrEmpty(curLine))
                    {
                        DrawText(curLine, x, curY, fontSize, color);
                        curY += scaledSize + lineSpacing;
                        curLine = word;
                    }
                    else
                    {
                        curLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(curLine))
                {
                    DrawText(curLine, x, curY, fontSize, color);
                    curY += scaledSize + lineSpacing;
                }
            }

            return curY - y;
        }

        // Dynamic Palette based on AppConfig.Instance.Theme
        public static Color BgDark => AppConfig.Instance.Theme switch
        {
            ColorTheme.OLEDMonochrome => new Color(4, 5, 8, 255),
            ColorTheme.AmberSunset => new Color(15, 12, 18, 255),
            _ => new Color(11, 14, 22, 255)
        };

        public static Color BgPanel => AppConfig.Instance.Theme switch
        {
            ColorTheme.OLEDMonochrome => new Color(14, 16, 22, 220),
            ColorTheme.AmberSunset => new Color(26, 20, 32, 220),
            _ => new Color(16, 22, 34, 220)
        };

        public static Color BgPanelHeader => AppConfig.Instance.Theme switch
        {
            ColorTheme.OLEDMonochrome => new Color(20, 24, 32, 240),
            ColorTheme.AmberSunset => new Color(36, 26, 44, 240),
            _ => new Color(24, 32, 48, 240)
        };

        public static Color Border => AppConfig.Instance.Theme switch
        {
            ColorTheme.OLEDMonochrome => new Color(45, 52, 65, 220),
            ColorTheme.AmberSunset => new Color(85, 50, 95, 220),
            _ => new Color(45, 65, 95, 220)
        };

        public static Color TextWhite => new(245, 248, 252, 255);
        public static Color TextMuted => new(145, 160, 185, 255);
        public static Color TextDim => new(90, 105, 125, 255);

        // Accent Colors - ALWAYS vibrant and saturated across all themes!
        public static Color NeonCyan => AppConfig.Instance.Theme switch
        {
            ColorTheme.AmberSunset => new Color(255, 179, 0, 255),
            _ => new Color(0, 229, 255, 255)
        };

        public static Color NeonGreen => new(0, 230, 118, 255);
        public static Color NeonGold => new(255, 215, 0, 255);
        public static Color NeonOrange => new(255, 145, 0, 255);
        public static Color NeonRed => new(255, 23, 68, 255);
        public static Color NeonPurple => new(213, 0, 249, 255);

        // Strafe Colors for visual distinction
        public static readonly Color[] StrafeColors =
        {
            new(0, 229, 255, 255),   // Cyan
            new(0, 230, 118, 255),   // Green
            new(255, 215, 0, 255),   // Gold
            new(255, 145, 0, 255),   // Orange
            new(213, 0, 249, 255),   // Purple
            new(0, 176, 255, 255),   // Light Blue
            new(118, 255, 3, 255),   // Lime
            new(255, 61, 0, 255)     // Deep Orange
        };

        public static Color GetTierColor(string tier)
        {
            return tier.ToUpperInvariant() switch
            {
                "MONSTER" or "WR TIER" => NeonPurple,
                "HOLY SHIT" => NeonRed,
                "OWNAGE" or "WICKED" => NeonOrange,
                "GODLIKE" => NeonGold,
                "PERFECT" => NeonGreen,
                "IMPRESSIVE" => NeonCyan,
                "DECENT" => new Color(140, 200, 255, 255),
                _ => TextMuted
            };
        }

        public static void DrawGlassPanel(int x, int y, int width, int height, string? title = null)
        {
            // Drop Shadow
            Raylib.DrawRectangle(x - 2, y + 4, width + 4, height + 4, new Color((byte)0, (byte)0, (byte)0, (byte)60));

            // Translucent Frosted Glass Backplate
            Color glassBase = AppConfig.Instance.Theme switch
            {
                ColorTheme.OLEDMonochrome => new Color((byte)10, (byte)12, (byte)16, (byte)225),
                ColorTheme.AmberSunset => new Color((byte)24, (byte)18, (byte)28, (byte)215),
                _ => new Color((byte)14, (byte)20, (byte)32, (byte)215)
            };
            Raylib.DrawRectangle(x, y, width, height, glassBase);

            // Border
            Raylib.DrawRectangleLines(x, y, width, height, new Color((byte)Border.R, (byte)Border.G, (byte)Border.B, (byte)180));

            // Specular Top Highlight
            Raylib.DrawLine(x + 1, y + 1, x + width - 2, y + 1, new Color((byte)255, (byte)255, (byte)255, (byte)70));

            // Title Bar if specified
            if (!string.IsNullOrEmpty(title))
            {
                int titleH = (int)(36 * AppConfig.Instance.UiScale);
                Color headerGlass = new((byte)BgPanelHeader.R, (byte)BgPanelHeader.G, (byte)BgPanelHeader.B, (byte)230);
                Raylib.DrawRectangle(x, y, width, titleH, headerGlass);
                Raylib.DrawLine(x, y + titleH, x + width, y + titleH, new Color((byte)Border.R, (byte)Border.G, (byte)Border.B, (byte)160));
                Raylib.DrawLine(x + 1, y + 1, x + width - 2, y + 1, new Color((byte)255, (byte)255, (byte)255, (byte)85));
                DrawText(title, x + 16, y + (titleH - GetScaledFontSize(15)) / 2, 15, TextWhite);
            }
        }

        public static void DrawPanel(int x, int y, int width, int height, string? title = null)
        {
            DrawGlassPanel(x, y, width, height, title);
        }

        public static bool DrawButton(int x, int y, int width, int height, string text, bool active = false, int fontSize = 14, bool enabled = true)
        {
            Vector2 mouse = Raylib.GetMousePosition();
            bool hovered = enabled && mouse.X >= x && mouse.X <= x + width && mouse.Y >= y && mouse.Y <= y + height;

            Color bg;
            Color textCol;
            Color borderCol;

            float time = (float)Raylib.GetTime();

            if (active)
            {
                // Active glowing gradient fill
                bg = NeonCyan;
                textCol = new Color(10, 14, 22, 255);
                borderCol = NeonCyan;
            }
            else if (hovered)
            {
                // Smooth glowing hover state
                float glowPulse = MathF.Sin(time * 6f) * 0.15f + 0.85f;
                bg = new Color((byte)30, (byte)45, (byte)68, (byte)235);
                textCol = TextWhite;
                borderCol = new Color((byte)(NeonCyan.R * glowPulse), (byte)(NeonCyan.G * glowPulse), (byte)(NeonCyan.B * glowPulse), (byte)255);
            }
            else
            {
                bg = new Color((byte)BgPanel.R, (byte)BgPanel.G, (byte)BgPanel.B, (byte)190);
                textCol = enabled ? TextMuted : TextDim;
                borderCol = new Color((byte)Border.R, (byte)Border.G, (byte)Border.B, (byte)160);
            }

            Raylib.DrawRectangle(x, y, width, height, bg);
            Raylib.DrawRectangleLines(x, y, width, height, borderCol);

            // Specular top highlight and hover bottom glow line
            if (!active)
            {
                Raylib.DrawLine(x + 1, y + 1, x + width - 2, y + 1, new Color((byte)255, (byte)255, (byte)255, (byte)(hovered ? 75 : 35)));
                if (hovered)
                {
                    Raylib.DrawLine(x + 2, y + height - 1, x + width - 3, y + height - 1, new Color((byte)NeonCyan.R, (byte)NeonCyan.G, (byte)NeonCyan.B, (byte)180));
                }
            }
            else
            {
                // Active button bright top shine
                Raylib.DrawLine(x + 1, y + 1, x + width - 2, y + 1, new Color(255, 255, 255, 160));
            }

            int textWidth = MeasureText(text, fontSize);
            int textHeight = GetScaledFontSize(fontSize);
            DrawText(text, x + (width - textWidth) / 2, y + (height - textHeight) / 2, fontSize, textCol);

            return enabled && hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        public static void DrawMetronomeIcon(int cx, int cy, int size, Color color, bool active)
        {
            float s = size / 20.0f;
            
            // Metronome Body (Trapezoid)
            Vector2 topL = new(cx - 3.5f * s, cy - 8.0f * s);
            Vector2 topR = new(cx + 3.5f * s, cy - 8.0f * s);
            Vector2 botL = new(cx - 7.5f * s, cy + 7.5f * s);
            Vector2 botR = new(cx + 7.5f * s, cy + 7.5f * s);

            Raylib.DrawLineV(topL, topR, color);
            Raylib.DrawLineV(topR, botR, color);
            Raylib.DrawLineV(botR, botL, color);
            Raylib.DrawLineV(botL, topL, color);

            // Base feet
            Raylib.DrawLine(cx - (int)(9 * s), cy + (int)(8.5f * s), cx + (int)(9 * s), cy + (int)(8.5f * s), color);

            // Swinging Needle (Pivoting from bottom center)
            float angleDeg = active ? MathF.Sin((float)Raylib.GetTime() * 8.0f) * 24.0f : -14.0f;
            float rad = angleDeg * (MathF.PI / 180.0f);
            float needleLen = 14.0f * s;

            Vector2 pivot = new(cx, cy + 6.0f * s);
            Vector2 tip = new(pivot.X + MathF.Sin(rad) * needleLen, pivot.Y - MathF.Cos(rad) * needleLen);

            Raylib.DrawLineV(pivot, tip, color);

            // Needle Weight Bead
            Vector2 beadPos = new(pivot.X + MathF.Sin(rad) * (needleLen * 0.65f), pivot.Y - MathF.Cos(rad) * (needleLen * 0.65f));
            Raylib.DrawCircleV(beadPos, 2.0f * s, color);
        }

        public static void DrawSpeakerIcon(int cx, int cy, int size, Color color, bool muted, float volume = 1.0f)
        {
            // Exact vector representation of user's SVG icons (speaker_high, speaker_low, speaker_mute)
            // Normalized from 24x24 SVG coordinate space to screen pixel space
            float s = size / 24.0f;
            float ox = cx - 12.0f * s;
            float oy = cy - 12.0f * s;
            float strokeW = MathF.Max(1.6f, 1.7f * s);

            if (muted)
            {
                // speaker_mute.svg exact paths:
                // 1. Diagonal slash: M3 3L21 21
                Color slashCol = new(255, 75, 75, 255);
                Raylib.DrawLineEx(new Vector2(ox + 4.0f * s, oy + 4.0f * s), new Vector2(ox + 20.0f * s, oy + 20.0f * s), strokeW + 0.3f, slashCol);

                // 2. Speaker Top Cone segment: M10.6 5L13 3V8
                Raylib.DrawLineEx(new Vector2(ox + 10.6f * s, oy + 5.0f * s), new Vector2(ox + 13.0f * s, oy + 3.0f * s), strokeW, color);
                Raylib.DrawLineEx(new Vector2(ox + 13.0f * s, oy + 3.0f * s), new Vector2(ox + 13.0f * s, oy + 8.0f * s), strokeW, color);

                // 3. Speaker Body Box: M7 16H5C3.89543 16 3 15.1046 3 14V10C3 9.63571 3.09739 9.29417 3.26756 9
                Raylib.DrawLineEx(new Vector2(ox + 7.0f * s, oy + 16.0f * s), new Vector2(ox + 5.0f * s, oy + 16.0f * s), strokeW, color);
                Raylib.DrawLineEx(new Vector2(ox + 5.0f * s, oy + 16.0f * s), new Vector2(ox + 3.0f * s, oy + 14.0f * s), strokeW, color);
                Raylib.DrawLineEx(new Vector2(ox + 3.0f * s, oy + 14.0f * s), new Vector2(ox + 3.0f * s, oy + 10.0f * s), strokeW, color);
                Raylib.DrawLineEx(new Vector2(ox + 3.0f * s, oy + 10.0f * s), new Vector2(ox + 3.3f * s, oy + 9.0f * s), strokeW, color);

                // 4. Speaker Bottom Cone segment: M13 18V21L10 18.5
                Raylib.DrawLineEx(new Vector2(ox + 13.0f * s, oy + 18.0f * s), new Vector2(ox + 13.0f * s, oy + 21.0f * s), strokeW, color);
                Raylib.DrawLineEx(new Vector2(ox + 13.0f * s, oy + 21.0f * s), new Vector2(ox + 10.0f * s, oy + 18.5f * s), strokeW, color);
            }
            else
            {
                // speaker_high.svg & speaker_low.svg:
                // 1. Full Speaker Solid Poly & Outline: M13 3L7 8H5C3.89543 8 3 8.89543 3 10V14C3 15.1046 3.89543 16 5 16H7L13 21V3Z
                Vector2 b1 = new(ox + 3.2f * s, oy + 10.0f * s);
                Vector2 b2 = new(ox + 7.0f * s, oy + 8.0f * s);
                Vector2 b3 = new(ox + 13.0f * s, oy + 3.0f * s);
                Vector2 b4 = new(ox + 13.0f * s, oy + 21.0f * s);
                Vector2 b5 = new(ox + 7.0f * s, oy + 16.0f * s);
                Vector2 b6 = new(ox + 3.2f * s, oy + 14.0f * s);

                // Subtle semi-transparent inner fill
                Color fillCol = new(color.R, color.G, color.B, (byte)45);
                Raylib.DrawTriangle(b1, b3, b4, fillCol);
                Raylib.DrawTriangle(b1, b4, b6, fillCol);

                // Smooth Vector Outer Lines
                Raylib.DrawLineEx(b3, b4, strokeW, color); // Vertical flare
                Raylib.DrawLineEx(b4, b5, strokeW, color); // Bottom slant
                Raylib.DrawLineEx(b5, b6, strokeW, color); // Bottom box
                Raylib.DrawLineEx(b6, b1, strokeW, color); // Left box
                Raylib.DrawLineEx(b1, b2, strokeW, color); // Top box
                Raylib.DrawLineEx(b2, b3, strokeW, color); // Top slant

                // Smooth rounded corners on outer vertices
                Raylib.DrawCircleV(b1, strokeW * 0.5f, color);
                Raylib.DrawCircleV(b3, strokeW * 0.5f, color);
                Raylib.DrawCircleV(b4, strokeW * 0.5f, color);
                Raylib.DrawCircleV(b6, strokeW * 0.5f, color);

                // 2. Sound Wave 1 (Inner Arc): M16 9C16.5 9.5 17 10.5 17 12C17 13.5 16.5 14.5 16 15
                Vector2 w1Center = new(ox + 11.5f * s, oy + 12.0f * s);
                Raylib.DrawCircleSectorLines(w1Center, 5.0f * s, -38.0f, 38.0f, 12, color);
                Raylib.DrawCircleSectorLines(w1Center, 5.0f * s + strokeW * 0.5f, -38.0f, 38.0f, 12, color);

                // 3. Sound Wave 2 (Outer Arc, for high volume): M19 6C20.5 7.5 21 10 21 12C21 14 20.5 16.5 19 18
                if (volume > 0.4f)
                {
                    Vector2 w2Center = new(ox + 11.5f * s, oy + 12.0f * s);
                    Raylib.DrawCircleSectorLines(w2Center, 8.8f * s, -45.0f, 45.0f, 16, color);
                    Raylib.DrawCircleSectorLines(w2Center, 8.8f * s + strokeW * 0.5f, -45.0f, 45.0f, 16, color);
                }
            }
        }

        public static void DrawProfileIcon(int cx, int cy, int size, Color color)
        {
            float s = size / 20.0f;

            // Head (Circle)
            Vector2 headCenter = new(cx, cy - 3.8f * s);
            float headRadius = 4.2f * s;
            Raylib.DrawCircleV(headCenter, headRadius, color);

            // Shoulders & Body Arc
            Vector2 bodyCenter = new(cx, cy + 7.8f * s);
            Raylib.DrawCircleSector(bodyCenter, 7.8f * s, 200.0f, 340.0f, 16, color);

            // Subtle online / connected dot indicator in bottom right
            bool isLinked = UserProfile.Instance.Cybershoke.IsLinked;
            Color dotCol = isLinked ? NeonGreen : NeonOrange;
            Raylib.DrawCircle(cx + (int)(6.5f * s), cy + (int)(5.5f * s), 2.2f * s, dotCol);
        }

        public static void DrawGearSettingsIcon(int cx, int cy, int size, Color color)
        {
            float s = size / 22.0f;
            Vector2 center = new(cx, cy);

            // Center hole / ring
            Raylib.DrawCircleSectorLines(center, 4.0f * s, 0, 360, 20, color);
            Raylib.DrawCircleSectorLines(center, 4.0f * s + 1.2f * s, 0, 360, 20, color);
            Raylib.DrawCircleLines(cx, cy, 7.5f * s, color);

            // 6 Gear Cogs around circle
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (MathF.PI / 3.0f);
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                Vector2 p1 = new(cx + cos * 6.5f * s - sin * 2.0f * s, cy + sin * 6.5f * s + cos * 2.0f * s);
                Vector2 p2 = new(cx + cos * 10.0f * s - sin * 1.5f * s, cy + sin * 10.0f * s + cos * 1.5f * s);
                Vector2 p3 = new(cx + cos * 10.0f * s + sin * 1.5f * s, cy + sin * 10.0f * s - cos * 1.5f * s);
                Vector2 p4 = new(cx + cos * 6.5f * s + sin * 2.0f * s, cy + sin * 6.5f * s - cos * 2.0f * s);

                Raylib.DrawTriangle(p1, p2, p3, color);
                Raylib.DrawTriangle(p1, p3, p4, color);
            }
        }

        public static bool DrawIconButton(int x, int y, int width, int height, Action<int, int, int, Color> drawIcon, string? text, bool active = false, int fontSize = 12)
        {
            Vector2 mouse = Raylib.GetMousePosition();
            bool hovered = mouse.X >= x && mouse.X <= x + width && mouse.Y >= y && mouse.Y <= y + height;

            Color bg;
            Color iconCol;
            Color textCol;
            Color borderCol;

            if (active)
            {
                bg = NeonCyan;
                iconCol = new Color(10, 14, 22, 255);
                textCol = new Color(10, 14, 22, 255);
                borderCol = NeonCyan;
            }
            else if (hovered)
            {
                bg = new Color((byte)35, (byte)48, (byte)72, (byte)230);
                iconCol = TextWhite;
                textCol = TextWhite;
                borderCol = NeonCyan;
            }
            else
            {
                bg = new Color((byte)BgPanel.R, (byte)BgPanel.G, (byte)BgPanel.B, (byte)190);
                iconCol = TextMuted;
                textCol = TextMuted;
                borderCol = new Color((byte)Border.R, (byte)Border.G, (byte)Border.B, (byte)160);
            }

            Raylib.DrawRectangle(x, y, width, height, bg);
            Raylib.DrawRectangleLines(x, y, width, height, borderCol);

            if (!active)
            {
                Raylib.DrawLine(x + 1, y + 1, x + width - 2, y + 1, new Color((byte)255, (byte)255, (byte)255, (byte)35));
            }

            int iconSize = (int)(18 * AppConfig.Instance.UiScale);

            if (string.IsNullOrEmpty(text))
            {
                // Just the icon centered
                drawIcon(x + width / 2, y + height / 2, iconSize, iconCol);
            }
            else
            {
                // Icon on left, text on right
                int textW = MeasureText(text, fontSize);
                int gap = (int)(6 * AppConfig.Instance.UiScale);
                int totalW = iconSize + gap + textW;
                int startX = x + (width - totalW) / 2;

                drawIcon(startX + iconSize / 2, y + height / 2, iconSize, iconCol);
                int textHeight = GetScaledFontSize(fontSize);
                DrawText(text, startX + iconSize + gap, y + (height - textHeight) / 2, fontSize, textCol);
            }

            return hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        public static void DrawTooltip(int mouseX, int mouseY, string title, string description, string proBenchmark)
        {
            int tooltipW = (int)(340 * AppConfig.Instance.UiScale);
            int tooltipH = (int)(95 * AppConfig.Instance.UiScale);
            int tx = mouseX + 14;
            int ty = mouseY - tooltipH - 8;

            int screenW = Raylib.GetScreenWidth();
            if (tx + tooltipW > screenW - 10) tx = mouseX - tooltipW - 14;
            if (ty < 50) ty = mouseY + 20;

            DrawGlassPanel(tx, ty, tooltipW, tooltipH);

            DrawText(title, tx + 12, ty + 8, 13, NeonCyan);
            DrawWrappedText(description, tx + 12, ty + 30, tooltipW - 24, 12, 3, TextWhite);
            DrawText($"PRO BENCHMARK: {proBenchmark}", tx + 12, ty + tooltipH - GetScaledFontSize(11) - 8, 11, NeonGold);
        }
    }
}
