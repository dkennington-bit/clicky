using System.Windows;
using Clicky.Windows.AI;
using Clicky.Windows.Audio;
using Clicky.Windows.Input;
using Clicky.Windows.Models;
using Clicky.Windows.ScreenCapture;
using Clicky.Windows.Security;
using Clicky.Windows.Services;
using Clicky.Windows.Settings;
using Clicky.Windows.UI;

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

        if (clickyUserSettings.IsCursorOverlayEnabled)
        {
            overlayWindowManager.Show();
        }

        if (string.IsNullOrWhiteSpace(openAIApiKeyStore.ReadApiKey()))
        {
            statusMessage = "add your OpenAI API key to start";
            companionPanelWindow.ShowNearCursor();
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
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
        }

        UpdatePanelState();
    }

    private void HandleDeleteApiKeyRequested(object? sender, EventArgs eventArguments)
    {
        openAIApiKeyStore.DeleteApiKey();
        statusMessage = "OpenAI key removed";
        UpdatePanelState();
    }

    private void HandleCursorOverlayEnabledChanged(object? sender, bool isEnabled)
    {
        clickyUserSettings.IsCursorOverlayEnabled = isEnabled;
        userSettingsStore.Save(clickyUserSettings);

        if (isEnabled)
        {
            overlayWindowManager.Show();
        }
        else
        {
            overlayWindowManager.Hide();
        }

        UpdatePanelState();
    }

    private void HandleQuitRequested(object? sender, EventArgs eventArguments)
    {
        Application.Current.Shutdown();
    }

    private void HandlePushToTalkPressed(object? sender, EventArgs eventArguments)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () => await StartPushToTalkAsync());
    }

    private void HandlePushToTalkReleased(object? sender, EventArgs eventArguments)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () => await StopPushToTalkAndRespondAsync());
    }

    private void HandleAudioLevelChanged(object? sender, double normalizedAudioLevel)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
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
            overlayWindowManager.SetProcessing();
            UpdatePanelState();

            string transcript = await openAITranscriptionClient.TranscribePushToTalkAsync(apiKey, wavAudioBytes, cancellationToken);
            lastTranscript = transcript;

            statusMessage = "capturing screen";
            UpdatePanelState();
            IReadOnlyList<CapturedDisplayImage> capturedDisplayImages = await overlayWindowManager.CaptureWithoutOverlayAsync(
                screenCaptureService,
                companionPanelWindow,
                cancellationToken);

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
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        currentResponse = accumulatedText;
                        overlayWindowManager.SetResponseText(accumulatedText);
                        UpdatePanelState();
                    });
                },
                cancellationToken);

            currentResponse = visionTurnResult.SpokenResponseText;
            conversationHistory.Add(new ConversationTurn(transcript, visionTurnResult.SpokenResponseText));

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
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            overlayWindowManager.SetResponseText(string.Empty);
            UpdatePanelState();
        }
        catch (OperationCanceledException)
        {
            statusMessage = "cancelled";
            voiceState = CompanionVoiceState.Idle;
            overlayWindowManager.SetIdle();
            UpdatePanelState();
        }
        catch (Exception exception)
        {
            statusMessage = exception.Message;
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
