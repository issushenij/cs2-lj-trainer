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
                    _notifyIcon.Icon = SystemIcons.Application;
                }
                catch
                {
                    _notifyIcon.Icon = SystemIcons.Information;
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

        public static void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
