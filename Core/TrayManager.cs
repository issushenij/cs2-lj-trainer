using System;
using System.Drawing;
using System.IO;
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
                var itemOpen = new ToolStripMenuItem("Открыть LJ Trainer", null, (s, e) => RestoreFromTray());
                var itemSettings = new ToolStripMenuItem("Настройки", null, (s, e) => { RestoreFromTray(); _onOpenSettings?.Invoke(); });
                var itemSep = new ToolStripSeparator();
                var itemExit = new ToolStripMenuItem("Выход", null, (s, e) => { ExitApplication(); });

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
            // 1. Try extracting icon from executable
            try
            {
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var extIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (extIcon != null) return extIcon;
                }
            }
            catch { }

            // 2. Try loading app_icon.ico
            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(icoPath))
                {
                    return new System.Drawing.Icon(icoPath);
                }
            }
            catch { }

            // 3. Crisp Programmatic Tray Icon (32x32)
            using var bmp = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Dark rounded badge background
                using var brushBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(14, 18, 24));
                g.FillRectangle(brushBg, 0, 0, 32, 32);

                // Neon orange border
                using var penBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 120, 0), 2);
                g.DrawRectangle(penBorder, 1, 1, 30, 30);

                // Cyan & Orange text "LJ"
                using var font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                using var brushL = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255));
                using var brushJ = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 140, 0));

                g.DrawString("L", font, brushL, 4, 9);
                g.DrawString("J", font, brushJ, 15, 9);
            }

            IntPtr hIcon = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        public static void SetWindowCustomIcon()
        {
            try
            {
                string iconTempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.png");
                if (!File.Exists(iconTempPath))
                {
                    using var bmp = new System.Drawing.Bitmap(128, 128);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using var brushBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(14, 18, 24));
                        g.FillRectangle(brushBg, 0, 0, 128, 128);

                        using var penBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 120, 0), 6);
                        g.DrawRectangle(penBorder, 3, 3, 122, 122);

                        using var font = new System.Drawing.Font("Arial", 48, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
                        using var brushL = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255));
                        using var brushJ = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 140, 0));

                        g.DrawString("L", font, brushL, 16, 36);
                        g.DrawString("J", font, brushJ, 60, 36);
                    }
                    bmp.Save(iconTempPath, System.Drawing.Imaging.ImageFormat.Png);
                }

                if (File.Exists(iconTempPath))
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
