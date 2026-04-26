using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Clicky.Windows.Models;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfOrientation = System.Windows.Controls.Orientation;
using WinForms = System.Windows.Forms;

namespace Clicky.Windows.UI;

public sealed class CompanionPanelWindow : Window
{
    private readonly TextBlock statusTextBlock = new();
    private readonly TextBlock transcriptTextBlock = new();
    private readonly TextBlock responseTextBlock = new();
    private readonly TextBlock apiKeyStatusTextBlock = new();
    private readonly PasswordBox apiKeyPasswordBox = new();
    private readonly WpfCheckBox showCursorCheckBox = new();
    private bool isUpdatingState;

    public event EventHandler<string>? SaveApiKeyRequested;
    public event EventHandler? DeleteApiKeyRequested;
    public event EventHandler<bool>? CursorOverlayEnabledChanged;
    public event EventHandler? QuitRequested;

    public CompanionPanelWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = WpfBrushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Width = 380;
        Height = 500;
        Content = BuildContent();
        Deactivated += (_, _) => Hide();
    }

    public void ShowNearCursor()
    {
        WinForms.Screen currentScreen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        double desiredLeft = WinForms.Cursor.Position.X - Width + 24;
        double desiredTop = WinForms.Cursor.Position.Y + 18;

        Rect workArea = new(
            currentScreen.WorkingArea.Left,
            currentScreen.WorkingArea.Top,
            currentScreen.WorkingArea.Width,
            currentScreen.WorkingArea.Height);

        Left = Math.Clamp(desiredLeft, workArea.Left + 8, workArea.Right - Width - 8);
        Top = Math.Clamp(desiredTop, workArea.Top + 8, workArea.Bottom - Height - 8);
        Show();
        Activate();
    }

    public void UpdateState(
        CompanionVoiceState voiceState,
        bool hasApiKey,
        bool isCursorOverlayEnabled,
        string? lastTranscript,
        string? currentResponse,
        string? statusMessage)
    {
        statusTextBlock.Text = statusMessage ?? voiceState switch
        {
            CompanionVoiceState.Idle => "ready",
            CompanionVoiceState.Listening => "listening",
            CompanionVoiceState.Processing => "processing",
            CompanionVoiceState.Responding => "responding",
            _ => "ready"
        };
        apiKeyStatusTextBlock.Text = hasApiKey ? "OpenAI key saved" : "OpenAI key required";
        isUpdatingState = true;
        showCursorCheckBox.IsChecked = isCursorOverlayEnabled;
        isUpdatingState = false;
        transcriptTextBlock.Text = string.IsNullOrWhiteSpace(lastTranscript) ? "hold Ctrl+Alt and speak" : lastTranscript;
        responseTextBlock.Text = string.IsNullOrWhiteSpace(currentResponse) ? "response will appear here" : currentResponse;
    }

    private UIElement BuildContent()
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(WpfColor.FromRgb(17, 24, 39)),
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(55, 65, 81)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 24,
                ShadowDepth = 10,
                Opacity = 0.38
            }
        };

        var stackPanel = new StackPanel();
        border.Child = stackPanel;

        stackPanel.Children.Add(new TextBlock
        {
            Text = "Clicky",
            Foreground = WpfBrushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 24
        });

        statusTextBlock.Foreground = new SolidColorBrush(WpfColor.FromRgb(96, 165, 250));
        statusTextBlock.FontSize = 13;
        statusTextBlock.Margin = new Thickness(0, 2, 0, 18);
        stackPanel.Children.Add(statusTextBlock);

        stackPanel.Children.Add(CreateSectionLabel("openai key"));
        apiKeyStatusTextBlock.Foreground = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219));
        apiKeyStatusTextBlock.Margin = new Thickness(0, 0, 0, 8);
        stackPanel.Children.Add(apiKeyStatusTextBlock);

        apiKeyPasswordBox.Height = 34;
        apiKeyPasswordBox.Margin = new Thickness(0, 0, 0, 8);
        apiKeyPasswordBox.Background = new SolidColorBrush(WpfColor.FromRgb(31, 41, 55));
        apiKeyPasswordBox.Foreground = WpfBrushes.White;
        apiKeyPasswordBox.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(75, 85, 99));
        stackPanel.Children.Add(apiKeyPasswordBox);

        var apiKeyButtonPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };
        apiKeyButtonPanel.Children.Add(CreateButton("save key", () =>
        {
            SaveApiKeyRequested?.Invoke(this, apiKeyPasswordBox.Password);
            apiKeyPasswordBox.Clear();
        }));
        apiKeyButtonPanel.Children.Add(CreateButton("forget", () => DeleteApiKeyRequested?.Invoke(this, EventArgs.Empty)));
        stackPanel.Children.Add(apiKeyButtonPanel);

        showCursorCheckBox.Content = "show clicky cursor";
        showCursorCheckBox.Foreground = WpfBrushes.White;
        showCursorCheckBox.Margin = new Thickness(0, 0, 0, 18);
        showCursorCheckBox.Checked += (_, _) =>
        {
            if (!isUpdatingState)
            {
                CursorOverlayEnabledChanged?.Invoke(this, true);
            }
        };
        showCursorCheckBox.Unchecked += (_, _) =>
        {
            if (!isUpdatingState)
            {
                CursorOverlayEnabledChanged?.Invoke(this, false);
            }
        };
        stackPanel.Children.Add(showCursorCheckBox);

        stackPanel.Children.Add(CreateSectionLabel("push to talk"));
        stackPanel.Children.Add(new TextBlock
        {
            Text = "hold Ctrl+Alt, speak, then release",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(209, 213, 219)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        stackPanel.Children.Add(CreateSectionLabel("last transcript"));
        transcriptTextBlock.Foreground = new SolidColorBrush(WpfColor.FromRgb(229, 231, 235));
        transcriptTextBlock.TextWrapping = TextWrapping.Wrap;
        transcriptTextBlock.Margin = new Thickness(0, 0, 0, 14);
        transcriptTextBlock.MaxHeight = 70;
        stackPanel.Children.Add(transcriptTextBlock);

        stackPanel.Children.Add(CreateSectionLabel("response"));
        responseTextBlock.Foreground = new SolidColorBrush(WpfColor.FromRgb(229, 231, 235));
        responseTextBlock.TextWrapping = TextWrapping.Wrap;
        responseTextBlock.MaxHeight = 100;
        stackPanel.Children.Add(responseTextBlock);

        var footerPanel = new DockPanel
        {
            Margin = new Thickness(0, 18, 0, 0)
        };
        WpfButton quitButton = CreateButton("quit", () => QuitRequested?.Invoke(this, EventArgs.Empty));
        DockPanel.SetDock(quitButton, Dock.Right);
        footerPanel.Children.Add(quitButton);
        stackPanel.Children.Add(footerPanel);

        return border;
    }

    private static TextBlock CreateSectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(148, 163, 184)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
    }

    private static WpfButton CreateButton(string text, Action action)
    {
        var button = new WpfButton
        {
            Content = text,
            MinWidth = 74,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 0, 10, 0),
            Cursor = WpfCursors.Hand,
            Background = new SolidColorBrush(WpfColor.FromRgb(37, 99, 235)),
            Foreground = WpfBrushes.White,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(59, 130, 246))
        };
        button.Click += (_, _) => action();
        return button;
    }
}
