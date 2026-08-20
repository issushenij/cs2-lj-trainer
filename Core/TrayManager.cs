using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Raylib_cs;

namespace LJTrainer.Core
{
    public static class TrayManager
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_RESTORE = 9;

        private static NotifyIcon? _notifyIcon;
        private static bool _isMinimizedToTray = false;
        private static Action? _onRestore;
        private static Action? _onOpenSettings;
        private static Action? _onExit;

        public static bool IsMinimizedToTray => _isMinimizedToTray;

        public static void Initialize(Action onRestore, Action onOpenSettings, Action onExit)
        {
            _onRestore = onRestore;
            _onOpenSettings = onOpenSettings;
            _onExit = onExit;

            try
            {
                var contextMenu = new ContextMenuStrip();
                var itemOpen = new ToolStripMenuItem("🎮 Открыть LJ Trainer", null, (s, e) => RestoreFromTray());
                var itemSettings = new ToolStripMenuItem("⚙️ Настройки", null, (s, e) => { RestoreFromTray(); _onOpenSettings?.Invoke(); });
                var itemSep = new ToolStripSeparator();
                var itemExit = new ToolStripMenuItem("❌ Выход из приложения", null, (s, e) => { ExitApplication(); });

                itemOpen.Font = new System.Drawing.Font(itemOpen.Font, System.Drawing.FontStyle.Bold);

                contextMenu.Items.Add(itemOpen);
                contextMenu.Items.Add(itemSettings);
                contextMenu.Items.Add(itemSep);
                contextMenu.Items.Add(itemExit);

                _notifyIcon = new NotifyIcon
                {
                    Text = "CS2 LJ Trainer — Трекер прыжков активен",
                    ContextMenuStrip = contextMenu,
                    Visible = true
                };

                try
                {
                    _notifyIcon.Icon = CreateCustomLJIcon();
                }
                catch
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }

                _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrayManager] Init warning: {ex.Message}");
            }
        }

        public static void MinimizeToTray()
        {
            _isMinimizedToTray = true;
            unsafe
            {
                IntPtr hwnd = (IntPtr)Raylib.GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_HIDE);
                }
            }

            ShowBalloon("LJ Trainer свёрнут в трей", "Трекинг прыжков и звуки рекордов CS2 продолжают работать в фоне.");
        }

        public static void RestoreFromTray()
        {
            _isMinimizedToTray = false;
            unsafe
            {
                IntPtr hwnd = (IntPtr)Raylib.GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
            }
            _onRestore?.Invoke();
        }

        public static void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _notifyIcon?.ShowBalloonTip(2500, title, text, icon);
            }
            catch { }
        }

        public static void ExitApplication()
        {
            _isMinimizedToTray = false;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _onExit?.Invoke();
        }

        public static System.Drawing.Icon CreateCustomLJIcon()
        {
            string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icons", "lj_logo_white.svg");
            if (!System.IO.File.Exists(logoPath)) logoPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", "icons", "lj_logo_white.svg");

            using var bmp = new System.Drawing.Bitmap(64, 64);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.FromArgb(255, 10, 14, 22));

                // Cyan border
                using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 0, 229, 255), 2f);
                g.DrawRectangle(borderPen, 1, 1, 61, 61);

                // If SVG exists, rasterize onto tray icon
                if (System.IO.File.Exists(logoPath))
                {
                    try
                    {
                        var svg = new Svg.Skia.SKSvg();
                        using var stream = System.IO.File.OpenRead(logoPath);
                        var skPic = svg.Load(stream);
                        if (skPic != null)
                        {
                            using var skBmp = new SkiaSharp.SKBitmap(48, 44, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
                            using var skCanvas = new SkiaSharp.SKCanvas(skBmp);
                            skCanvas.Clear(SkiaSharp.SKColors.Transparent);
                            skCanvas.Scale(48f / skPic.CullRect.Width, 44f / skPic.CullRect.Height);
                            skCanvas.DrawPicture(skPic);
                            skCanvas.Flush();

                            using var ms = new System.IO.MemoryStream();
                            skBmp.Encode(ms, SkiaSharp.SKEncodedImageFormat.Png, 100);
                            using var iconBmp = new System.Drawing.Bitmap(ms);
                            g.DrawImage(iconBmp, 8, 10, 48, 44);
                        }
                    }
                    catch { }
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        public static void SetWindowCustomIcon()
        {
            try
            {
                string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icons", "lj_logo_white.svg");
                if (!System.IO.File.Exists(logoPath)) logoPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets", "icons", "lj_logo_white.svg");

                using var bmp = new System.Drawing.Bitmap(128, 128);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.FromArgb(255, 10, 14, 22));

                    using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 0, 229, 255), 3f);
                    g.DrawRectangle(borderPen, 2, 2, 123, 123);

                    if (System.IO.File.Exists(logoPath))
                    {
                        var svg = new Svg.Skia.SKSvg();
                        using var stream = System.IO.File.OpenRead(logoPath);
                        var skPic = svg.Load(stream);
                        if (skPic != null)
                        {
                            using var skBmp = new SkiaSharp.SKBitmap(100, 92, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
                            using var skCanvas = new SkiaSharp.SKCanvas(skBmp);
                            skCanvas.Clear(SkiaSharp.SKColors.Transparent);
                            skCanvas.Scale(100f / skPic.CullRect.Width, 92f / skPic.CullRect.Height);
                            skCanvas.DrawPicture(skPic);
                            skCanvas.Flush();

                            using var ms = new System.IO.MemoryStream();
                            skBmp.Encode(ms, SkiaSharp.SKEncodedImageFormat.Png, 100);
                            using var iconBmp = new System.Drawing.Bitmap(ms);
                            g.DrawImage(iconBmp, 14, 18, 100, 92);
                        }
                    }
                }

                // Save app_icon.ico for Windows executable
                try
                {
                    string rootIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                    using var icoFs = System.IO.File.OpenWrite(rootIco);
                    using var icoObj = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                    icoObj.Save(icoFs);
                }
                catch { }

                // Convert Bitmap to Raylib Image and set as Window Icon
                string iconTempPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.png");
                bmp.Save(iconTempPath, System.Drawing.Imaging.ImageFormat.Png);
                
                if (System.IO.File.Exists(iconTempPath))
                {
                    var rayImg = Raylib.LoadImage(iconTempPath);
                    Raylib.SetWindowIcon(rayImg);
                    Raylib.UnloadImage(rayImg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrayManager] SetWindowIcon error: {ex.Message}");
            }
        }

        public static void Dispose()
        {
            try
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
            }
            catch { }
        }
    }
}
