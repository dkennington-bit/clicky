using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Clicky.Windows.UI;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon notifyIcon;

    public event EventHandler? TogglePanelRequested;
    public event EventHandler? QuitRequested;

    public TrayIconService()
    {
        notifyIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Clicky",
            Visible = true,
            ContextMenuStrip = BuildContextMenuStrip()
        };

        notifyIcon.MouseClick += (_, mouseEventArguments) =>
        {
            if (mouseEventArguments.Button == WinForms.MouseButtons.Left)
            {
                TogglePanelRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void ShowNotification(string title, string message)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    private WinForms.ContextMenuStrip BuildContextMenuStrip()
    {
        var contextMenuStrip = new WinForms.ContextMenuStrip();
        contextMenuStrip.Items.Add("Open Clicky", null, (_, _) => TogglePanelRequested?.Invoke(this, EventArgs.Empty));
        contextMenuStrip.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));
        return contextMenuStrip;
    }
}
