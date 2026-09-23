using System.Windows;
using PCDoctor.Core.Enums;
using PCDoctor.Core.Interfaces;
using Forms = System.Windows.Forms;

namespace PCDoctor.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly ILocalizationService _loc;
    private readonly Action _showWindow;
    private readonly Action _exit;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _showItem;
    private readonly Forms.ToolStripMenuItem _exitItem;

    public TrayIconService(ILocalizationService localization, Action showWindow, Action exit)
    {
        _loc = localization;
        _showWindow = showWindow;
        _exit = exit;
        _showItem = new Forms.ToolStripMenuItem();
        _exitItem = new Forms.ToolStripMenuItem();
        _showItem.Click += (_, _) => _showWindow();
        _exitItem.Click += (_, _) => _exit();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_showItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _icon = new Forms.NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
            Icon = LoadIcon()
        };
        _icon.MouseClick += OnMouseClick;
        ApplyLanguage();
        _loc.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }

    private void OnLanguageChanged(object? sender, AppLanguage e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        _icon.Text = _loc["Tray.Tip"];
        _showItem.Text = _loc["Tray.Show"];
        _exitItem.Text = _loc["Tray.Exit"];
    }

    private void OnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            _showWindow();
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (resource?.Stream is not null)
        {
            return new System.Drawing.Icon(resource.Stream);
        }

        var exe = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(exe)
            ? System.Drawing.Icon.ExtractAssociatedIcon(exe) ?? System.Drawing.SystemIcons.Application
            : System.Drawing.SystemIcons.Application;
    }
}
