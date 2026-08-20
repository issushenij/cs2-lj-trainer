using System;
using System.IO;
using System.Text.RegularExpressions;
using Raylib_cs;
using SkiaSharp;
using Svg.Skia;

namespace LJTrainer.UI
{
    public static class SvgIconManager
    {
        private static Texture2D? _texSpeakerHigh;
        private static Texture2D? _texSpeakerLow;
        private static Texture2D? _texSpeakerMute;
        private static Texture2D? _texLjLogo;

        public static void Initialize()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string highPath = Path.Combine(baseDir, "Assets", "icons", "speaker_high.svg");
                string lowPath = Path.Combine(baseDir, "Assets", "icons", "speaker_low.svg");
                string mutePath = Path.Combine(baseDir, "Assets", "icons", "speaker_mute.svg");
                string logoPath = Path.Combine(baseDir, "Assets", "icons", "lj_logo_white.svg");

                if (!File.Exists(highPath)) highPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "icons", "speaker_high.svg");
                if (!File.Exists(lowPath)) lowPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "icons", "speaker_low.svg");
                if (!File.Exists(mutePath)) mutePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "icons", "speaker_mute.svg");
                if (!File.Exists(logoPath)) logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "icons", "lj_logo_white.svg");

                if (File.Exists(highPath)) _texSpeakerHigh = RasterizeSvgToTexture(highPath, 64, 64);
                if (File.Exists(lowPath)) _texSpeakerLow = RasterizeSvgToTexture(lowPath, 64, 64);
                if (File.Exists(mutePath)) _texSpeakerMute = RasterizeSvgToTexture(mutePath, 64, 64);
                if (File.Exists(logoPath)) _texLjLogo = RasterizeSvgToTexture(logoPath, 256, 235);
            }
            catch { }
        }

        public static void DrawLjLogoSvg(int x, int y, int width, int height, Color tint)
        {
            if (_texLjLogo.HasValue && _texLjLogo.Value.Id > 0)
            {
                var tex = _texLjLogo.Value;
                Rectangle src = new(0, 0, tex.Width, tex.Height);
                Rectangle dest = new(x, y, width, height);
                Raylib.DrawTexturePro(tex, src, dest, System.Numerics.Vector2.Zero, 0.0f, tint);
            }
        }

        private static unsafe Texture2D? RasterizeSvgToTexture(string svgPath, int width, int height)
        {
            try
            {
                string svgContent = File.ReadAllText(svgPath);
                // Replace black stroke/fill with pure white so we can tint it dynamically with Raylib Color
                svgContent = Regex.Replace(svgContent, @"stroke=""#000000""", @"stroke=""#FFFFFF""", RegexOptions.IgnoreCase);
                svgContent = Regex.Replace(svgContent, @"fill=""#000000""", @"fill=""#FFFFFF""", RegexOptions.IgnoreCase);

                var svg = new SKSvg();
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgContent));
                var skPic = svg.Load(stream);

                if (skPic == null) return null;

                using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);

                float scaleX = width / skPic.CullRect.Width;
                float scaleY = height / skPic.CullRect.Height;
                canvas.Scale(scaleX, scaleY);
                canvas.DrawPicture(skPic);
                canvas.Flush();

                // Convert SKBitmap to Raylib Texture2D
                var bytes = bitmap.Bytes;
                fixed (byte* ptr = bytes)
                {
                    Image rayImg = new Image
                    {
                        Data = ptr,
                        Width = width,
                        Height = height,
                        Mipmaps = 1,
                        Format = PixelFormat.UncompressedR8G8B8A8
                    };

                    Texture2D tex = Raylib.LoadTextureFromImage(rayImg);
                    Raylib.SetTextureFilter(tex, TextureFilter.Bilinear);
                    return tex;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void DrawSpeakerSvg(int cx, int cy, int size, Color tint, bool muted, float volume = 1.0f)
        {
            Texture2D? targetTex = muted 
                ? _texSpeakerMute 
                : (volume > 0.4f ? _texSpeakerHigh : _texSpeakerLow);

            if (targetTex.HasValue && targetTex.Value.Id > 0)
            {
                var tex = targetTex.Value;
                Rectangle src = new(0, 0, tex.Width, tex.Height);
                Rectangle dest = new(cx - size / 2f, cy - size / 2f, size, size);
                Raylib.DrawTexturePro(tex, src, dest, System.Numerics.Vector2.Zero, 0.0f, tint);
            }
            else
            {
                // Fallback
                Theme.DrawSpeakerIcon(cx, cy, size, tint, muted, volume);
            }
        }

        public static void Cleanup()
        {
            if (_texSpeakerHigh.HasValue) Raylib.UnloadTexture(_texSpeakerHigh.Value);
            if (_texSpeakerLow.HasValue) Raylib.UnloadTexture(_texSpeakerLow.Value);
            if (_texSpeakerMute.HasValue) Raylib.UnloadTexture(_texSpeakerMute.Value);
        }
    }
}
