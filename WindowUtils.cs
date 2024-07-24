using System.Diagnostics;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace EasyFramework;

public static class WindowUtils
{
    // region Tray Icon
    /**
     * Import an icon as a resource in the resource editor, then import it with the below:
     * - Properties.Resources.Logo.Clone() as System.Drawing.Icon;
     */
    private static NotifyIcon? _notifyIcon;

    public static void CreateTrayIcon(Window window, Icon? icon, string appName, string version, string? clickUrl)
    {
        if (_notifyIcon != null || icon == null) return;
        var notifyIconText = clickUrl != null
            ? $"{appName} {version}\nClick to open {clickUrl}"
            : $"Click to show {appName} {version}";
        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = notifyIconText
        };
        _notifyIcon.MouseClick += (_, evt) =>
        {
            if (clickUrl != null && evt.Button == MouseButtons.Left)
            {
                MiscUtils.OpenUrl(clickUrl);
            }
            else if (evt.Button == MouseButtons.Left) Restore(window, true);
        };

        _notifyIcon.ContextMenuStrip = new ContextMenuStrip(new System.ComponentModel.Container());
        _notifyIcon.ContextMenuStrip.SuspendLayout();
        var openMenuItem = new ToolStripMenuItem("Show");
        openMenuItem.Click += (_, _) => Restore(window, true);
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => Application.Current.Shutdown();
        _notifyIcon.ContextMenuStrip.Items.Add(openMenuItem);
        _notifyIcon.ContextMenuStrip.Items.Add(exitMenuItem);
        _notifyIcon.ContextMenuStrip.Name = "NotifyIconContextMenu";
        _notifyIcon.ContextMenuStrip.ResumeLayout(false);

        _notifyIcon.Visible = true;
    }

    /**
     * Run this on OnClosing(), override it on Window.
     */
    public static void DestroyTrayIcon()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
    // endregion

    // region Mutex
    // ReSharper disable once NotAccessedField.Local
    private static Mutex? _mutex;

    public static void CheckIfAlreadyRunning(string appName)
    {
        _mutex = new Mutex(true, appName, out var createdNew);
        if (createdNew) return;
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            MessageBox.Show(
                mainWindow,
                "This application is already running!",
                appName,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        Application.Current.Shutdown();
    }
    // endregion

    // region Window State
    public static void Minimize(Window window, bool onTaskbar)
    {
        window.Hide();
        window.WindowStyle = WindowStyle.ToolWindow;
        window.WindowState = WindowState.Minimized;
        window.ShowInTaskbar = onTaskbar;
    }

    public static void Restore(Window window, bool onTaskbar)
    {
        window.WindowStyle = WindowStyle.ThreeDBorderWindow;
        window.WindowState = WindowState.Normal;
        window.ShowInTaskbar = true;
        window.Show();
        window.Activate();
    }

    /**
     * Run this on OnStateChanged(), override it on Window.
     */
    public static void OnStateChange(Window window, bool onTaskbar)
    {
        switch (window.WindowState)
        {
            case WindowState.Minimized: // For tray icon
                window.WindowStyle = WindowStyle.ToolWindow;
                window.ShowInTaskbar = onTaskbar;
                break;
            default:
                window.WindowStyle = WindowStyle.ThreeDBorderWindow;
                window.ShowInTaskbar = true;
                break;
        }
    }

    // endregion
}