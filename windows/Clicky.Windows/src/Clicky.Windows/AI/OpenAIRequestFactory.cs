using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Clicky.Windows.Models;

namespace Clicky.Windows.AI;

public static class OpenAIRequestFactory
{
    public const string VisionModel = "gpt-5.5";
    public const string TranscriptionModel = "gpt-4o-mini-transcribe";
    public const string SpeechModel = "gpt-4o-mini-tts";
    public const string DefaultVoice = "coral";

    public static HttpRequestMessage CreateVisionRequest(
        string apiKey,
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages,
        IReadOnlyList<ConversationTurn> conversationHistory,
        string userTranscript,
        bool stream)
    {
        List<object> currentContent = [];
        if (conversationHistory.Count > 0)
        {
            currentContent.Add(new
            {
                type = "input_text",
                text = BuildConversationHistoryText(conversationHistory)
            });
        }

        foreach (CapturedDisplayImage capturedDisplayImage in capturedDisplayImages)
        {
            string base64Image = Convert.ToBase64String(capturedDisplayImage.ImageBytes);
            currentContent.Add(new
            {
                type = "input_image",
                image_url = $"data:image/jpeg;base64,{base64Image}"
            });
            currentContent.Add(new
            {
                type = "input_text",
                text = $"{capturedDisplayImage.Label}. pixel dimensions: {capturedDisplayImage.ScreenshotWidthInPixels}x{capturedDisplayImage.ScreenshotHeightInPixels}."
            });
        }

        currentContent.Add(new
        {
            type = "input_text",
            text = userTranscript
        });

        object[] inputItems =
        [
            new
            {
                role = "user",
                content = currentContent
            }
        ];

        var requestBody = new
        {
            model = VisionModel,
            instructions = CompanionPrompts.CompanionVoiceResponseSystemPrompt,
            input = inputItems,
            max_output_tokens = 1024,
            stream,
            store = false
        };

        return CreateJsonRequest(HttpMethod.Post, "https://api.openai.com/v1/responses", apiKey, requestBody);
    }

    private static string BuildConversationHistoryText(IReadOnlyList<ConversationTurn> conversationHistory)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("conversation so far:");

        foreach (ConversationTurn conversationTurn in conversationHistory.TakeLast(8))
        {
            stringBuilder.AppendLine($"user: {conversationTurn.UserTranscript}");
            stringBuilder.AppendLine($"clicky: {conversationTurn.AssistantResponse}");
        }

        return stringBuilder.ToString();
    }

    public static HttpRequestMessage CreateSpeechRequest(string apiKey, string text)
    {
        var requestBody = new
        {
            model = SpeechModel,
            voice = DefaultVoice,
            input = text,
            instructions = CompanionPrompts.SpeechInstructions,
            response_format = "wav"
        };

        return CreateJsonRequest(HttpMethod.Post, "https://api.openai.com/v1/audio/speech", apiKey, requestBody);
    }

    public static MultipartFormDataContent CreateTranscriptionMultipartContent(byte[] wavAudioBytes)
    {
        var multipartFormDataContent = new MultipartFormDataContent();

        var audioContent = new ByteArrayContent(wavAudioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        multipartFormDataContent.Add(audioContent, "file", "push-to-talk.wav");
        multipartFormDataContent.Add(new StringContent(TranscriptionModel), "model");
        multipartFormDataContent.Add(new StringContent("text"), "response_format");
        multipartFormDataContent.Add(new StringContent(CompanionPrompts.TranscriptionPrompt), "prompt");

        return multipartFormDataContent;
    }

    public static HttpRequestMessage CreateTranscriptionRequest(string apiKey, byte[] wavAudioBytes)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
        {
            Content = CreateTranscriptionMultipartContent(wavAudioBytes)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod httpMethod,
        string url,
        string apiKey,
        object requestBody)
    {
        string json = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
        var request = new HttpRequestMessage(httpMethod, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = null
    };
}
