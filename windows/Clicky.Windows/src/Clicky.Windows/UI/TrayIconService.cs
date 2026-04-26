using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Clicky.Windows.UI;

public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon notifyIcon;
    private readonly WinForms.ToolStripMenuItem voiceMenuItem = new("Voice");

    public event EventHandler? TogglePanelRequested;
    public event EventHandler? QuitRequested;
    public event EventHandler<string>? VoiceSelected;

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

    public void ConfigureVoiceMenu(IReadOnlyList<string> supportedVoices, string selectedVoice)
    {
        voiceMenuItem.DropDownItems.Clear();

        foreach (string supportedVoice in supportedVoices)
        {
            var voiceMenuChoice = new WinForms.ToolStripMenuItem(supportedVoice)
            {
                Checked = string.Equals(supportedVoice, selectedVoice, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false
            };
            voiceMenuChoice.Click += (_, _) => VoiceSelected?.Invoke(this, supportedVoice);
            voiceMenuItem.DropDownItems.Add(voiceMenuChoice);
        }
    }

    public void SetSelectedVoice(string selectedVoice)
    {
        foreach (WinForms.ToolStripMenuItem voiceMenuChoice in voiceMenuItem.DropDownItems.OfType<WinForms.ToolStripMenuItem>())
        {
            voiceMenuChoice.Checked = string.Equals(voiceMenuChoice.Text, selectedVoice, StringComparison.OrdinalIgnoreCase);
        }
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
        contextMenuStrip.Items.Add(voiceMenuItem);
        contextMenuStrip.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));
        return contextMenuStrip;
    }
}
