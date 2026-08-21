using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;

namespace LJTrainer.UI
{
    public static class Theme
    {
        // Dual-font Architecture: Display (Bahnschrift / Clean Grotesk) + Mono (Consolas / Technical)
        public static Font FontDisplaySmall  { get; private set; } // ~18-22px
        public static Font FontDisplayMedium { get; private set; } // ~28-36px
        public static Font FontDisplayLarge  { get; private set; } // ~44-56px

        public static Font FontMonoSmall     { get; private set; } // ~18-22px
        public static Font FontMonoMedium    { get; private set; } // ~28-36px
        public static Font FontMonoLarge     { get; private set; } // ~44-56px

        public static bool HasCustomFont { get; private set; } = false;

        public static void InitializeFont()
        {
            try
            {
                // 1. High-precision Technical Display Font (Bahnschrift / Segoe UI Bold / Arial Bold)
                string displayPath = @"C:\Windows\Fonts\bahnschrift.ttf";
                if (!File.Exists(displayPath)) displayPath = @"C:\Windows\Fonts\segoeuib.ttf";
                if (!File.Exists(displayPath)) displayPath = @"C:\Windows\Fonts\arialbd.ttf";

                // 2. Monospace Technical Font (Consolas Bold - Data, telemetry, code)
                string monoPath = @"C:\Windows\Fonts\consolab.ttf";
                if (!File.Exists(monoPath)) monoPath = @"C:\Windows\Fonts\consola.ttf";
                if (!File.Exists(monoPath)) monoPath = @"C:\Windows\Fonts\courbd.ttf";

                var cpList = new HashSet<int>();
                for (int i = 32;     i <= 255;   i++) cpList.Add(i); // Basic Latin
                for (int i = 0x0100; i <= 0x024F; i++) cpList.Add(i); // Latin Ext
                for (int i = 0x0400; i <= 0x052F; i++) cpList.Add(i); // Cyrillic (Full Russian)
                for (int i = 0x2000; i <= 0x206F; i++) cpList.Add(i); // Punctuation
                for (int i = 0x2190; i <= 0x22FF; i++) cpList.Add(i); // Arrows & Math
                for (int i = 0x2500; i <= 0x257F; i++) cpList.Add(i); // Box Drawing
                for (int i = 0x2580; i <= 0x259F; i++) cpList.Add(i); // Blocks
                for (int i = 0x25A0; i <= 0x27BF; i++) cpList.Add(i); // Shapes

                int[] codepoints = new int[cpList.Count];
                cpList.CopyTo(codepoints);
                Array.Sort(codepoints);

                // Load Display Fonts with smooth Bilinear filtering
                if (File.Exists(displayPath))
                {
                    FontDisplaySmall = Raylib.LoadFontEx(displayPath, 24, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontDisplaySmall.Texture, TextureFilter.Bilinear);

                    FontDisplayMedium = Raylib.LoadFontEx(displayPath, 38, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontDisplayMedium.Texture, TextureFilter.Bilinear);

                    FontDisplayLarge = Raylib.LoadFontEx(displayPath, 58, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontDisplayLarge.Texture, TextureFilter.Bilinear);
                }

                // Load Monospace Fonts with smooth Bilinear filtering
                if (File.Exists(monoPath))
                {
                    FontMonoSmall = Raylib.LoadFontEx(monoPath, 22, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontMonoSmall.Texture, TextureFilter.Bilinear);

                    FontMonoMedium = Raylib.LoadFontEx(monoPath, 34, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontMonoMedium.Texture, TextureFilter.Bilinear);

                    FontMonoLarge = Raylib.LoadFontEx(monoPath, 52, codepoints, codepoints.Length);
                    Raylib.SetTextureFilter(FontMonoLarge.Texture, TextureFilter.Bilinear);
                }

                HasCustomFont = true;
            }
            catch
            {
                HasCustomFont = false;
            }
        }

        private static Font GetDisplayFont(int scaledSize)
        {
            if (scaledSize <= 22) return FontDisplaySmall;
            if (scaledSize <= 38) return FontDisplayMedium;
            return FontDisplayLarge;
        }

        private static Font GetMonoFont(int scaledSize)
        {
            if (scaledSize <= 22) return FontMonoSmall;
            if (scaledSize <= 38) return FontMonoMedium;
            return FontMonoLarge;
        }

        public static int GetScaledFontSize(int baseSize)
        {
            float scale = Math.Clamp(AppConfig.Instance.UiScale, 0.8f, 1.6f);
            return Math.Max(8, (int)Math.Round(baseSize * scale));
        }

        // Standard Text (Uses Technical Monospace Font for OpenCode / Obsidian feel)
        public static void DrawText(string text, int x, int y, int fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont && FontMonoSmall.Texture.Id > 0)
                Raylib.DrawTextEx(GetMonoFont(scaled), text, new Vector2(x, y), scaled, 0.5f, color);
            else
                Raylib.DrawText(text, x, y, scaled, color);
        }

        // Display Header Text (Uses Bold Grotesk Bahnschrift Font)
        public static void DrawDisplayText(string text, int x, int y, int fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont && FontDisplaySmall.Texture.Id > 0)
                Raylib.DrawTextEx(GetDisplayFont(scaled), text, new Vector2(x, y), scaled, 1.0f, color);
            else
                Raylib.DrawText(text, x, y, scaled, color);
        }

        public static int MeasureText(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont && FontMonoSmall.Texture.Id > 0)
                return (int)Math.Ceiling(Raylib.MeasureTextEx(GetMonoFont(scaled), text, scaled, 0.5f).X);
            return Raylib.MeasureText(text, scaled);
        }

        public static int MeasureDisplayText(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int scaled = GetScaledFontSize(fontSize);
            if (HasCustomFont && FontDisplaySmall.Texture.Id > 0)
                return (int)Math.Ceiling(Raylib.MeasureTextEx(GetDisplayFont(scaled), text, scaled, 1.0f).X);
            return Raylib.MeasureText(text, scaled);
        }

        public static int DrawWrappedText(string text, int x, int y, int maxWidth, int fontSize, int lineSpacing, Color color)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int scaledSize = GetScaledFontSize(fontSize);
            int curY = y;
            foreach (var para in text.Split('\n'))
            {
                string curLine = "";
                foreach (var word in para.Split(' '))
                {
                    string testLine = string.IsNullOrEmpty(curLine) ? word : $"{curLine} {word}";
                    if (MeasureText(testLine, fontSize) > maxWidth && !string.IsNullOrEmpty(curLine))
                    {
                        DrawText(curLine, x, curY, fontSize, color);
                        curY += scaledSize + lineSpacing;
                        curLine = word;
                    }
                    else curLine = testLine;
                }
                if (!string.IsNullOrEmpty(curLine))
                {
                    DrawText(curLine, x, curY, fontSize, color);
                    curY += scaledSize + lineSpacing;
                }
            }
            return curY - y;
        }

        // -- OBSIDIAN + OPENCODE MINIMALIST COLOR PALETTE ---------------------------
        public static Color BgDark => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(2, 6, 3, 255),
            ColorTheme.AmberCRT        => new Color(6, 4, 1, 255),
            ColorTheme.OLEDMonochrome  => new Color(0, 0, 0, 255),
            _                          => new Color(8, 8, 8, 255), // Pure Obsidian Dark
        };

        public static Color BgPanel => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(6, 14, 8, 255),
            ColorTheme.AmberCRT        => new Color(16, 10, 4, 255),
            ColorTheme.OLEDMonochrome  => new Color(12, 12, 12, 255),
            _                          => new Color(16, 16, 16, 255), // Matte card base
        };

        public static Color BgPanelHeader => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(10, 24, 14, 255),
            ColorTheme.AmberCRT        => new Color(24, 16, 6, 255),
            ColorTheme.OLEDMonochrome  => new Color(20, 20, 20, 255),
            _                          => new Color(22, 22, 22, 255),
        };

        public static Color Border => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(0, 160, 50, 255),
            ColorTheme.AmberCRT        => new Color(180, 105, 15, 255),
            ColorTheme.OLEDMonochrome  => new Color(80, 80, 80, 255),
            _                          => new Color(42, 42, 42, 255), // Crisp 1px border
        };

        public static Color BorderHighlight => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(80, 255, 130, 255),
            ColorTheme.AmberCRT        => new Color(255, 175, 45, 255),
            ColorTheme.OLEDMonochrome  => new Color(240, 240, 240, 255),
            _                          => new Color(240, 240, 240, 255), // Clean white wireframe
        };

        public static Color TextWhite => new(245, 245, 245, 255);
        public static Color TextMuted => new(150, 150, 150, 255);
        public static Color TextDim   => new(85,  85,  85,  255);

        public static Color NeonCyan => AppConfig.Instance.Theme switch
        {
            ColorTheme.PhosphorMatrix  => new Color(60, 240, 120, 255),
            ColorTheme.AmberCRT        => new Color(255, 165, 40, 255),
            ColorTheme.OLEDMonochrome  => new Color(240, 240, 240, 255),
            _                          => new Color(245, 130, 45, 255), // Obsidian Amber/Orange
        };

        public static Color NeonGreen  => new(75,  225, 115, 255); // Vivid Terminal Green
        public static Color NeonGold   => new(250, 185, 45,  255); // Pure Gold
        public static Color NeonOrange => new(245, 120, 40,  255); // Obsidian Orange
        public static Color NeonRed    => new(240, 70,  70,  255); // Signal Red
        public static Color NeonPurple => new(180, 120, 255, 255); // Violet
        public static Color NeonBlue   => new(75,  170, 255, 255); // Sky Blue

        public static readonly Color[] StrafeColors =
        {
            new(75,  170, 255, 255),
            new(75,  225, 115, 255),
            new(250, 185, 45,  255),
            new(245, 120, 40,  255),
            new(180, 120, 255, 255),
            new(60,  225, 235, 255),
            new(160, 245, 60,  255),
            new(240, 70,  70,  255),
        };

        public static Color GetTierColor(string tier) => tier.ToUpperInvariant() switch
        {
            "MONSTER" or "WR TIER" => NeonPurple,
            "HOLY SHIT"            => NeonRed,
            "OWNAGE" or "WICKED"   => NeonOrange,
            "GODLIKE"              => NeonGold,
            "PERFECT"              => NeonGreen,
            "IMPRESSIVE"           => NeonBlue,
            "DECENT"               => new Color(120, 180, 250, 255),
            _                      => TextMuted,
        };

        // -- SIGNATURE OBSIDIAN FLOATING CORNER RETICLES ----------------------------
        /// <summary>
        /// Draws the signature Obsidian floating corner marks (outer tick lines around panels)
        /// </summary>
        public static void DrawObsidianCornerReticles(int x, int y, int width, int height, int offset = 8, int tickLen = 14, Color? color = null)
        {
            Color c = color ?? new Color(75, 75, 75, 255);
            
            // Top-Left
            Raylib.DrawLine(x - offset, y - offset, x - offset, y - offset + tickLen, c);
            Raylib.DrawLine(x - offset, y - offset, x - offset + tickLen, y - offset, c);

            // Top-Right
            Raylib.DrawLine(x + width + offset, y - offset, x + width + offset, y - offset + tickLen, c);
            Raylib.DrawLine(x + width + offset, y - offset, x + width + offset - tickLen, y - offset, c);

            // Bottom-Left
            Raylib.DrawLine(x - offset, y + height + offset, x - offset, y + height + offset - tickLen, c);
            Raylib.DrawLine(x - offset, y + height + offset, x - offset + tickLen, y + height + offset, c);

            // Bottom-Right
            Raylib.DrawLine(x + width + offset, y + height + offset, x + width + offset, y + height + offset - tickLen, c);
            Raylib.DrawLine(x + width + offset, y + height + offset, x + width + offset - tickLen, y + height + offset, c);
        }

        /// <summary>
        /// Draws a multi-layer soft diffusion blur / shadow behind floating tooltips, modals, and popups
        /// </summary>
        public static void DrawBackdropBlur(int x, int y, int width, int height, int blurRadius = 8)
        {
            for (int r = blurRadius; r >= 1; r--)
            {
                byte a = (byte)(18 - (r * 14 / blurRadius));
                Raylib.DrawRectangle(x - r, y - r, width + r * 2, height + r * 2, new Color((byte)0, (byte)0, (byte)0, a));
            }
        }

        /// <summary>
        /// Draws a full Obsidian / OpenCode technical panel with title embedded directly into top border and tactical reticles
        /// </summary>
        public static void DrawTechnicalBox(int x, int y, int width, int height, string? title = null, Color? borderColor = null, Color? bgColor = null, bool drawReticles = true, Color? titleColor = null)
        {
            Color border = borderColor ?? Border;
            Color bg = bgColor ?? BgPanel;

            // 1. Solid Flat Background Fill (Clean matte dark, no artificial gradients)
            Raylib.DrawRectangle(x, y, width, height, bg);

            // 2. Outer Structural Wireframe
            Raylib.DrawLine(x, y, x, y + height, border);
            Raylib.DrawLine(x, y + height, x + width, y + height, border);
            Raylib.DrawLine(x + width, y, x + width, y + height, border);

            // 3. Top border line with authentic CMD/TUI title cutout
            if (!string.IsNullOrEmpty(title))
            {
                string cleanTitle = title.Trim('[', ']', ' ', '*');
                string t = $"[ {cleanTitle.ToUpperInvariant()} ]";
                int tw = MeasureText(t, 10);
                int titleX = x + 14;

                // Top line segment before title
                Raylib.DrawLine(x, y, titleX, y, border);

                // Clear line area for title
                Raylib.DrawRectangle(titleX, y - 6, tw + 8, 13, bg);

                // Embedded title text
                Color tc = titleColor ?? NeonCyan;
                DrawText(t, titleX + 4, y - 5, 10, tc);

                // Top line segment after title
                Raylib.DrawLine(titleX + tw + 8, y, x + width, y, border);
            }
            else
            {
                Raylib.DrawLine(x, y, x + width, y, border);
            }

            // 4. Tactical Floating Corner Marks
            if (drawReticles && width > 120 && height > 50)
            {
                DrawObsidianCornerReticles(x, y, width, height, 6, 10, new Color((byte)60, (byte)60, (byte)60, (byte)160));
            }
        }

        public static void DrawTuiFrame(int x, int y, int width, int height, string? title, Color borderColor, int charFontSize = 12, bool doubleLine = false)
        {
            DrawTechnicalBox(x, y, width, height, title, borderColor);
        }

        public static void DrawTerminalFrame(int x, int y, int width, int height, string? title, Color accent, int charFontSize = 12)
        {
            DrawTechnicalBox(x, y, width, height, title, Border);
        }

        public static void DrawGlassPanel(int x, int y, int width, int height, string? title = null)
        {
            DrawTechnicalBox(x, y, width, height, title, Border);
        }

        public static void DrawPanel(int x, int y, int width, int height, string? title = null)
        {
            DrawTechnicalBox(x, y, width, height, title, Border);
        }

        public static void DrawTerminalBadge(int x, int y, string label, Color color, int fontSize = 9)
        {
            DrawText($"[{label}]", x, y, fontSize, color);
        }

        public static void DrawCrtScanlines(int screenWidth, int screenHeight)
        {
            if (!AppConfig.Instance.ShowCrtScanlines) return;
            for (int y = 0; y < screenHeight; y += 3)
                Raylib.DrawLine(0, y, screenWidth, y, new Color(0, 0, 0, 30));
        }

        // -- CLEAN FLAT TECHNICAL BUTTON --------------------------------------------
        public static bool DrawButton(int x, int y, int width, int height, string text,
            bool active = false, int fontSize = 12, bool enabled = true)
        {
            Vector2 mouse   = Raylib.GetMousePosition();
            bool    hovered = enabled && mouse.X >= x && mouse.X <= x + width && mouse.Y >= y && mouse.Y <= y + height;

            Color bg;
            Color textCol;
            Color borderCol;

            if (active)
            {
                bg        = new Color(28, 30, 38, 255);
                textCol   = TextWhite;
                borderCol = NeonCyan;
            }
            else if (hovered)
            {
                bg        = new Color(24, 28, 36, 255);
                textCol   = TextWhite;
                borderCol = new Color((byte)120, (byte)135, (byte)155, (byte)255);
            }
            else
            {
                bg        = BgPanel;
                textCol   = enabled ? TextMuted : TextDim;
                borderCol = Border;
            }

            // 1. Flat Clean Base Fill (No artificial gradients)
            Raylib.DrawRectangle(x, y, width, height, bg);

            // 2. Crisp 1px Outline
            Raylib.DrawRectangleLines(x, y, width, height, borderCol);

            // 3. Active Marker or Hover Corner Accents
            if (active)
            {
                // Left active accent pill
                Raylib.DrawRectangle(x + 4, y + (height - 6) / 2, 3, 6, NeonCyan);
            }
            else if (hovered)
            {
                // Subtle 2px corner ticks on hover
                Raylib.DrawLine(x + 1, y + 1, x + 3, y + 1, TextWhite);
                Raylib.DrawLine(x + 1, y + 1, x + 1, y + 3, TextWhite);
                Raylib.DrawLine(x + width - 2, y + height - 2, x + width - 4, y + height - 2, TextWhite);
                Raylib.DrawLine(x + width - 2, y + height - 2, x + width - 2, y + height - 4, TextWhite);
            }

            int textWidth  = MeasureText(text, fontSize);
            int textHeight = GetScaledFontSize(fontSize);
            DrawText(text, x + (width - textWidth) / 2 + (active ? 2 : 0), y + (height - textHeight) / 2, fontSize, textCol);

            return enabled && hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        // -- ICON BUTTON -------------------------------------------------------------
        public static bool DrawIconButton(int x, int y, int width, int height,
            Action<int, int, int, Color> drawIcon, string? text, bool active = false, int fontSize = 11)
        {
            Vector2 mouse   = Raylib.GetMousePosition();
            bool    hovered = mouse.X >= x && mouse.X <= x + width && mouse.Y >= y && mouse.Y <= y + height;

            Color bg        = active ? new Color(34, 34, 34, 255) : (hovered ? new Color(24, 24, 24, 255) : BgPanel);
            Color iconCol   = active ? TextWhite : (hovered ? TextWhite : TextMuted);
            Color textCol   = iconCol;
            Color borderCol = active ? BorderHighlight : (hovered ? new Color(110, 110, 110, 255) : Border);

            Raylib.DrawRectangle(x, y, width, height, bg);
            Raylib.DrawRectangleLines(x, y, width, height, borderCol);

            int iconSize = (int)(15 * AppConfig.Instance.UiScale);

            if (string.IsNullOrEmpty(text))
            {
                drawIcon(x + width / 2, y + height / 2, iconSize, iconCol);
            }
            else
            {
                int textW  = MeasureText(text, fontSize);
                int gap    = (int)(6 * AppConfig.Instance.UiScale);
                int totalW = iconSize + gap + textW;
                int startX = x + (width - totalW) / 2;
                drawIcon(startX + iconSize / 2, y + height / 2, iconSize, iconCol);
                int textHeight = GetScaledFontSize(fontSize);
                DrawText(text, startX + iconSize + gap, y + (height - textHeight) / 2, fontSize, textCol);
            }

            return hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
        }

        // -- TOOLTIP -----------------------------------------------------------------
        public static void DrawTooltip(int mouseX, int mouseY, string title, string description, string proBenchmark)
        {
            float scale   = AppConfig.Instance.UiScale;
            int tooltipW  = (int)(320 * scale);
            int tooltipH  = (int)(90 * scale);
            int tx        = mouseX + 14;
            int ty        = mouseY - tooltipH - 8;

            int screenW = Raylib.GetScreenWidth();
            if (tx + tooltipW > screenW - 10) tx = mouseX - tooltipW - 14;
            if (ty < 50) ty = mouseY + 20;

            // Optical diffusion blur behind floating tooltip
            DrawBackdropBlur(tx, ty, tooltipW, tooltipH, 10);

            DrawTechnicalBox(tx, ty, tooltipW, tooltipH, title, BorderHighlight, BgPanel, false);
            DrawWrappedText(description,  tx + 12, ty + 20, tooltipW - 24, 11, 2, TextWhite);
            DrawText($"PRO BENCHMARK: {proBenchmark}", tx + 12, ty + tooltipH - GetScaledFontSize(10) - 6, 10, NeonGold);
        }

        // -- VECTOR ICONS (Technical Wireframe Style) -------------------------------
        public static void DrawMetronomeIcon(int cx, int cy, int size, Color color, bool active)
        {
            float s = size / 20.0f;
            Vector2 topL = new(cx - 3.0f * s, cy - 7.0f * s), topR = new(cx + 3.0f * s, cy - 7.0f * s);
            Vector2 botL = new(cx - 7.0f * s, cy + 7.0f * s), botR = new(cx + 7.0f * s, cy + 7.0f * s);
            Raylib.DrawLineV(topL, topR, color); Raylib.DrawLineV(topR, botR, color);
            Raylib.DrawLineV(botR, botL, color); Raylib.DrawLineV(botL, topL, color);
            Raylib.DrawLine(cx - (int)(8 * s), cy + (int)(8 * s), cx + (int)(8 * s), cy + (int)(8 * s), color);
            float rad = (active ? MathF.Sin((float)Raylib.GetTime() * 8.0f) * 24.0f : -14.0f) * (MathF.PI / 180.0f);
            float nl  = 13.0f * s;
            Vector2 pivot = new(cx, cy + 5.0f * s), tip = new(pivot.X + MathF.Sin(rad) * nl, pivot.Y - MathF.Cos(rad) * nl);
            Raylib.DrawLineV(pivot, tip, color);
            Vector2 bead = new(pivot.X + MathF.Sin(rad) * (nl * 0.65f), pivot.Y - MathF.Cos(rad) * (nl * 0.65f));
            Raylib.DrawCircleV(bead, 1.8f * s, color);
        }

        public static void DrawSpeakerIcon(int cx, int cy, int size, Color color, bool muted, float volume = 1.0f)
        {
            float s = size / 24.0f, ox = cx - 12.0f * s, oy = cy - 12.0f * s, sw = MathF.Max(1.4f, 1.5f * s);
            if (muted)
            {
                Raylib.DrawLineEx(new Vector2(ox + 4f * s, oy + 4f * s), new Vector2(ox + 20f * s, oy + 20f * s), sw + 0.2f, NeonRed);
                Raylib.DrawLineEx(new Vector2(ox + 10.6f * s, oy + 5f * s), new Vector2(ox + 13f * s, oy + 3f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 13f * s, oy + 3f * s), new Vector2(ox + 13f * s, oy + 8f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 7f * s, oy + 16f * s), new Vector2(ox + 5f * s, oy + 16f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 5f * s, oy + 16f * s), new Vector2(ox + 3f * s, oy + 14f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 3f * s, oy + 14f * s), new Vector2(ox + 3f * s, oy + 10f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 13f * s, oy + 18f * s), new Vector2(ox + 13f * s, oy + 21f * s), sw, color);
                Raylib.DrawLineEx(new Vector2(ox + 13f * s, oy + 21f * s), new Vector2(ox + 10f * s, oy + 18.5f * s), sw, color);
            }
            else
            {
                Vector2 b1 = new(ox + 3.5f * s, oy + 9.5f * s), b2 = new(ox + 7f * s, oy + 8f * s);
                Vector2 b3 = new(ox + 13f * s, oy + 3f * s),   b4 = new(ox + 13f * s, oy + 21f * s);
                Vector2 b5 = new(ox + 7f * s, oy + 16f * s),   b6 = new(ox + 3.5f * s, oy + 14.5f * s);
                Raylib.DrawLineEx(b3, b4, sw, color); Raylib.DrawLineEx(b4, b5, sw, color);
                Raylib.DrawLineEx(b5, b6, sw, color); Raylib.DrawLineEx(b6, b1, sw, color);
                Raylib.DrawLineEx(b1, b2, sw, color); Raylib.DrawLineEx(b2, b3, sw, color);
                Vector2 wc = new(ox + 11.5f * s, oy + 12f * s);
                Raylib.DrawCircleSectorLines(wc, 5f * s, -38f, 38f, 10, color);
                if (volume > 0.4f) Raylib.DrawCircleSectorLines(wc, 8.5f * s, -45f, 45f, 12, color);
            }
        }

        public static void DrawProfileIcon(int cx, int cy, int size, Color color)
        {
            float s = size / 20.0f;
            Raylib.DrawCircleLines(cx, (int)(cy - 3.8f * s), 4.0f * s, color);
            Raylib.DrawCircleSectorLines(new Vector2(cx, cy + 7.8f * s), 7.5f * s, 200f, 340f, 14, color);
            bool isLinked = UserProfile.Instance.Cybershoke.IsLinked;
            Raylib.DrawRectangle(cx + (int)(5.5f * s), cy + (int)(4.5f * s), (int)(3.5f * s), (int)(3.5f * s), isLinked ? NeonGreen : NeonOrange);
        }

        public static void DrawGearSettingsIcon(int cx, int cy, int size, Color color)
        {
            float s = size / 22.0f;
            Vector2 center = new(cx, cy);
            Raylib.DrawCircleLines(cx, cy, 3.8f * s, color);
            Raylib.DrawCircleLines(cx, cy, 7.2f * s, color);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (MathF.PI / 3.0f), cos = MathF.Cos(angle), sin = MathF.Sin(angle);
                Vector2 p1 = new(cx + cos * 6.5f * s - sin * 1.8f * s, cy + sin * 6.5f * s + cos * 1.8f * s);
                Vector2 p2 = new(cx + cos * 9.5f * s - sin * 1.4f * s, cy + sin * 9.5f * s + cos * 1.4f * s);
                Vector2 p3 = new(cx + cos * 9.5f * s + sin * 1.4f * s, cy + sin * 9.5f * s - cos * 1.4f * s);
                Vector2 p4 = new(cx + cos * 6.5f * s + sin * 1.8f * s, cy + sin * 6.5f * s - cos * 1.8f * s);
                Raylib.DrawLineV(p1, p2, color);
                Raylib.DrawLineV(p2, p3, color);
                Raylib.DrawLineV(p3, p4, color);
            }
        }
    }
}
