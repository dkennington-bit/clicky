using System.Windows;
using Clicky.Windows.AI;
using Clicky.Windows.Audio;
using Clicky.Windows.Input;
using Clicky.Windows.Logging;
using Clicky.Windows.Models;
using Clicky.Windows.ScreenCapture;
using Clicky.Windows.Security;
using Clicky.Windows.Services;
using Clicky.Windows.Settings;
using Clicky.Windows.UI;
using WpfApplication = System.Windows.Application;

namespace Clicky.Windows.Infrastructure;

public sealed class ClickyApplicationCoordinator : IDisposable
{
    private readonly IOpenAIApiKeyStore openAIApiKeyStore;
    private readonly IOpenAIConversationClient openAIConversationClient;
    private readonly IOpenAITranscriptionClient openAITranscriptionClient;
    private readonly IOpenAISpeechClient openAISpeechClient;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly MicrophonePushToTalkRecorder microphonePushToTalkRecorder;
    private readonly GlobalPushToTalkHotkeyListener globalPushToTalkHotkeyListener;
    private readonly UserSettingsStore userSettingsStore;
    private readonly OverlayWindowManager overlayWindowManager;
    private readonly CompanionPanelWindow companionPanelWindow;
    private readonly TrayIconService trayIconService;
    private readonly List<ConversationTurn> conversationHistory = [];

    private ClickyUserSettings clickyUserSettings;
    private CompanionVoiceState voiceState = CompanionVoiceState.Idle;
    private CancellationTokenSource? currentInteractionCancellationTokenSource;
    private string? lastTranscript;
    private string? currentResponse;
    private string? statusMessage;

    private ClickyApplicationCoordinator(
        IOpenAIApiKeyStore openAIApiKeyStore,
        IOpenAIConversationClient openAIConversationClient,
        IOpenAITranscriptionClient openAITranscriptionClient,
        IOpenAISpeechClient openAISpeechClient,
        IScreenCaptureService screenCaptureService,
        MicrophonePushToTalkRecorder microphonePushToTalkRecorder,
        GlobalPushToTalkHotkeyListener globalPushToTalkHotkeyListener,
        UserSettingsStore userSettingsStore,
        OverlayWindowManager overlayWindowManager,
        CompanionPanelWindow companionPanelWindow,
        TrayIconService trayIconService)
    {
        this.openAIApiKeyStore = openAIApiKeyStore;
        this.openAIConversationClient = openAIConversationClient;
        this.openAITranscriptionClient = openAITranscriptionClient;
        this.openAISpeechClient = openAISpeechClient;
        this.screenCaptureService = screenCaptureService;
        this.microphonePushToTalkRecorder = microphonePushToTalkRecorder;
        this.globalPushToTalkHotkeyListener = globalPushToTalkHotkeyListener;
        this.userSettingsStore = userSettingsStore;
        this.overlayWindowManager = overlayWindowManager;
        this.companionPanelWindow = companionPanelWindow;
        this.trayIconService = trayIconService;
        clickyUserSettings = userSettingsStore.Load();
        ClickyLogger.Info("Application coordinator created.");
    }

    public static ClickyApplicationCoordinator CreateDefault()
    {
        var pointTagParser = new PointTagParser();
        var overlayWindowManager = new OverlayWindowManager();

        return new ClickyApplicationCoordinator(
            openAIApiKeyStore: new CredentialManagerOpenAIApiKeyStore(),
            openAIConversationClient: new OpenAIConversationClient(pointTagParser),
            openAITranscriptionClient: new OpenAITranscriptionClient(),
            openAISpeechClient: new OpenAISpeechClient(),
            screenCaptureService: new WindowsScreenCaptureService(),
            microphonePushToTalkRecorder: new MicrophonePushToTalkRecorder(),
            globalPushToTalkHotkeyListener: new GlobalPushToTalkHotkeyListener(),
            userSettingsStore: new UserSettingsStore(),
            overlayWindowManager: overlayWindowManager,
            companionPanelWindow: new CompanionPanelWindow(),
            trayIconService: new TrayIconService());
    }

    public void Start()
    {
        trayIconService.TogglePanelRequested += HandleTogglePanelRequested;
        trayIconService.QuitRequested += HandleQuitRequested;

        companionPanelWindow.SaveApiKeyRequested += HandleSaveApiKeyRequested;
        companionPanelWindow.DeleteApiKeyRequested += HandleDeleteApiKeyRequested;
        companionPanelWindow.CursorOverlayEnabledChanged += HandleCursorOverlayEnabledChanged;
        companionPanelWindow.QuitRequested += HandleQuitRequested;

        microphonePushToTalkRecorder.AudioLevelChanged += HandleAudioLevelChanged;
        globalPushToTalkHotkeyListener.PushToTalkPressed += HandlePushToTalkPressed;
        globalPushToTalkHotkeyListener.PushToTalkReleased += HandlePushToTalkReleased;
        globalPushToTalkHotkeyListener.Start();
        ClickyLogger.Info($"Application started. Log file: {ClickyLogger.CurrentLogFilePath}");

        if (clickyUserSettings.IsCursorOverlayEnabled)
        {
            overlayWindowManager.Show();
            ClickyLogger.Info("Cursor overlay shown on startup.");
        }

        if (string.IsNullOrWhiteSpace(openAIApiKeyStore.ReadApiKey()))
        {
            statusMessage = "add your OpenAI API key to start";
            companionPanelWindow.ShowNearCursor();
            ClickyLogger.Info("No OpenAI API key found. Showing setup panel.");
        }

        UpdatePanelState();
    }

    public void Dispose()
    {
        currentInteractionCancellationTokenSource?.Cancel();
        currentInteractionCancellationTokenSource?.Dispose();
        openAISpeechClient.StopPlayback();
        trayIconService.Dispose();
        overlayWindowManager.Dispose();
        globalPushToTalkHotkeyListener.Dispose();
        microphonePushToTalkRecorder.Dispose();

        if (openAIConversationClient is IDisposable disposableConversationClient)
        {
            disposableConversationClient.Dispose();
        }

        if (openAITranscriptionClient is IDisposable disposableTranscriptionClient)
        {
            disposableTranscriptionClient.Dispose();
        }

        if (openAISpeechClient is IDisposable disposableSpeechClient)
        {
            disposableSpeechClient.Dispose();
        }
    }

    private void HandleTogglePanelRequested(object? sender, EventArgs eventArguments)
    {
        if (companionPanelWindow.IsVisible)
        {
            companionPanelWindow.Hide();
        }
        else
        {
            companionPanelWindow.ShowNearCursor();
            UpdatePanelState();
        }
    }

    private void HandleSaveApiKeyRequested(object? sender, string apiKey)
    {
        try
        {
            openAIApiKeyStore.SaveApiKey(apiKey.Trim());
            statusMessage = "OpenAI key saved";
            ClickyLogger.Info("OpenAI API key saved.");
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            ClickyLogger.Error("Failed to save OpenAI API key.", exception);
        }

        UpdatePanelState();
    }

    private void HandleDeleteApiKeyRequested(object? sender, EventArgs eventArguments)
    {
        openAIApiKeyStore.DeleteApiKey();
        statusMessage = "OpenAI key removed";
        ClickyLogger.Info("OpenAI API key removed.");
        UpdatePanelState();
    }

    private void HandleCursorOverlayEnabledChanged(object? sender, bool isEnabled)
    {
        clickyUserSettings.IsCursorOverlayEnabled = isEnabled;
        userSettingsStore.Save(clickyUserSettings);

        if (isEnabled)
        {
            overlayWindowManager.Show();
            ClickyLogger.Info("Cursor overlay enabled.");
        }
        else
        {
            overlayWindowManager.Hide();
            ClickyLogger.Info("Cursor overlay disabled.");
        }

        UpdatePanelState();
    }

    private void HandleQuitRequested(object? sender, EventArgs eventArguments)
    {
        WpfApplication.Current.Shutdown();
    }

    private void HandlePushToTalkPressed(object? sender, EventArgs eventArguments)
    {
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(async () => await StartPushToTalkAsync());
    }

    private void HandlePushToTalkReleased(object? sender, EventArgs eventArguments)
    {
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(async () => await StopPushToTalkAndRespondAsync());
    }

    private void HandleAudioLevelChanged(object? sender, double normalizedAudioLevel)
    {
        _ = WpfApplication.Current.Dispatcher.InvokeAsync(() =>
        {
            overlayWindowManager.SetAudioLevel(normalizedAudioLevel);
        });
    }

    private async Task StartPushToTalkAsync()
    {
        string? apiKey = openAIApiKeyStore.ReadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            statusMessage = "add your OpenAI API key before talking";
            ClickyLogger.Info("Push-to-talk ignored because no OpenAI API key is saved.");
            trayIconService.ShowNotification("Clicky needs an OpenAI key", "Open the panel and save your API key.");
            companionPanelWindow.ShowNearCursor();
            UpdatePanelState();
            return;
        }

        currentInteractionCancellationTokenSource?.Cancel();
        currentInteractionCancellationTokenSource?.Dispose();
        currentInteractionCancellationTokenSource = new CancellationTokenSource();

        openAISpeechClient.StopPlayback();
        currentResponse = string.Empty;
        statusMessage = null;
        voiceState = CompanionVoiceState.Listening;
        ClickyLogger.Info("Push-to-talk started.");

        if (clickyUserSettings.IsCursorOverlayEnabled && !overlayWindowManager.IsVisible)
        {
            overlayWindowManager.Show();
        }

        overlayWindowManager.SetListening();
        overlayWindowManager.SetResponseText(string.Empty);
        UpdatePanelState();

        try
        {
            await microphonePushToTalkRecorder.StartRecordingAsync();
        }
        catch (Exception exception)
        {
            statusMessage = $"microphone error: {exception.Message}";
            ClickyLogger.Error("Microphone recording failed to start.", exception);
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            UpdatePanelState();
        }
    }

    private async Task StopPushToTalkAndRespondAsync()
    {
        if (!microphonePushToTalkRecorder.IsRecording)
        {
            return;
        }

        byte[] wavAudioBytes;
        try
        {
            wavAudioBytes = await microphonePushToTalkRecorder.StopRecordingAsync();
        }
        catch (Exception exception)
        {
            statusMessage = $"audio capture failed: {exception.Message}";
            ClickyLogger.Error("Microphone recording failed to stop.", exception);
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            UpdatePanelState();
            return;
        }

        await ProcessCapturedAudioAsync(wavAudioBytes);
    }

    private async Task ProcessCapturedAudioAsync(byte[] wavAudioBytes)
    {
        string? apiKey = openAIApiKeyStore.ReadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            statusMessage = "add your OpenAI API key before talking";
            ClickyLogger.Info("Captured audio ignored because no OpenAI API key is saved.");
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            UpdatePanelState();
            return;
        }

        CancellationToken cancellationToken = currentInteractionCancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            voiceState = CompanionVoiceState.Processing;
            statusMessage = "transcribing";
            ClickyLogger.Info($"Processing captured audio. WAV bytes: {wavAudioBytes.Length}.");
            overlayWindowManager.SetProcessing();
            UpdatePanelState();

            string transcript = await openAITranscriptionClient.TranscribePushToTalkAsync(apiKey, wavAudioBytes, cancellationToken);
            lastTranscript = transcript;
            ClickyLogger.Interaction($"User transcript: {transcript}");

            statusMessage = "capturing screen";
            UpdatePanelState();
            IReadOnlyList<CapturedDisplayImage> capturedDisplayImages = await overlayWindowManager.CaptureWithoutOverlayAsync(
                screenCaptureService,
                companionPanelWindow,
                cancellationToken);
            ClickyLogger.Info($"Captured {capturedDisplayImages.Count} display image(s): {string.Join("; ", capturedDisplayImages.Select(displayImage => $"{displayImage.ScreenNumber}:{displayImage.ScreenshotWidthInPixels}x{displayImage.ScreenshotHeightInPixels} cursor={displayImage.IsCursorScreen}"))}.");

            voiceState = CompanionVoiceState.Responding;
            statusMessage = "thinking";
            overlayWindowManager.SetResponding();
            UpdatePanelState();

            VisionTurnResult visionTurnResult = await openAIConversationClient.SendVisionTurnAsync(
                apiKey,
                capturedDisplayImages,
                conversationHistory,
                transcript,
                async accumulatedText =>
                {
                    await WpfApplication.Current.Dispatcher.InvokeAsync(() =>
                    {
                        currentResponse = accumulatedText;
                        overlayWindowManager.SetResponseText(accumulatedText);
                        UpdatePanelState();
                    });
                },
                cancellationToken);

            currentResponse = visionTurnResult.SpokenResponseText;
            conversationHistory.Add(new ConversationTurn(transcript, visionTurnResult.SpokenResponseText));
            ClickyLogger.Interaction($"Assistant response: {visionTurnResult.SpokenResponseText}");
            ClickyLogger.Info($"Point tag: shouldPoint={visionTurnResult.PointTagResult.ShouldPoint}, x={visionTurnResult.PointTagResult.X}, y={visionTurnResult.PointTagResult.Y}, label={visionTurnResult.PointTagResult.Label}, screen={visionTurnResult.PointTagResult.ScreenNumber?.ToString() ?? "cursor"}.");

            statusMessage = "speaking";
            UpdatePanelState();
            await openAISpeechClient.SpeakAsync(apiKey, visionTurnResult.SpokenResponseText, cancellationToken);

            if (visionTurnResult.PointTagResult.ShouldPoint && clickyUserSettings.IsCursorOverlayEnabled)
            {
                statusMessage = "pointing";
                UpdatePanelState();
                await overlayWindowManager.PointAtAsync(visionTurnResult, capturedDisplayImages, cancellationToken);
            }

            statusMessage = "ready";
            ClickyLogger.Info("Interaction completed successfully.");
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            overlayWindowManager.SetResponseText(string.Empty);
            UpdatePanelState();
        }
        catch (OperationCanceledException)
        {
            statusMessage = "cancelled";
            ClickyLogger.Info("Interaction cancelled.");
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            UpdatePanelState();
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
            ClickyLogger.Error("Interaction failed.", exception);
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            overlayWindowManager.SetResponseText(statusMessage);
            trayIconService.ShowNotification("Clicky error", "Open the panel for details.");
            UpdatePanelState();
        }
    }

    private void UpdatePanelState()
    {
        companionPanelWindow.UpdateState(
            voiceState,
            hasApiKey: !string.IsNullOrWhiteSpace(openAIApiKeyStore.ReadApiKey()),
            isCursorOverlayEnabled: clickyUserSettings.IsCursorOverlayEnabled,
            lastTranscript,
            currentResponse,
            statusMessage);
    }
}
