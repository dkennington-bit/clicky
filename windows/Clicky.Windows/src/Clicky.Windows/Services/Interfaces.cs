using Clicky.Windows.Models;

namespace Clicky.Windows.Services;

public interface IOpenAIApiKeyStore
{
    string? ReadApiKey();
    void SaveApiKey(string apiKey);
    void DeleteApiKey();
}

public interface IOpenAIConversationClient
{
    Task<VisionTurnResult> SendVisionTurnAsync(
        string apiKey,
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages,
        IReadOnlyList<ConversationTurn> conversationHistory,
        string userTranscript,
        Func<string, Task> onAccumulatedTextUpdated,
        CancellationToken cancellationToken);
}

public interface IOpenAITranscriptionClient
{
    Task<string> TranscribePushToTalkAsync(
        string apiKey,
        byte[] wavAudioBytes,
        CancellationToken cancellationToken);
}

public interface IOpenAISpeechClient
{
    Task SpeakAsync(
        string apiKey,
        string text,
        CancellationToken cancellationToken);

    void StopPlayback();
}

public interface IScreenCaptureService
{
    Task<IReadOnlyList<CapturedDisplayImage>> CaptureAllDisplaysAsync(
        CancellationToken cancellationToken = default);
}

public interface IPointTagParser
{
    PointTagResult Parse(string responseText);
    string RemovePointTag(string responseText);
}
