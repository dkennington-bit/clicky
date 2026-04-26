using System.IO;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using Clicky.Windows.Logging;
using Clicky.Windows.Models;
using Clicky.Windows.Services;

namespace Clicky.Windows.AI;

public sealed class OpenAIConversationClient : IOpenAIConversationClient, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly IPointTagParser pointTagParser;

    public OpenAIConversationClient(IPointTagParser pointTagParser)
        : this(new HttpClient(), pointTagParser)
    {
    }

    public OpenAIConversationClient(HttpClient httpClient, IPointTagParser pointTagParser)
    {
        this.httpClient = httpClient;
        this.pointTagParser = pointTagParser;
        this.httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<VisionTurnResult> SendVisionTurnAsync(
        string apiKey,
        IReadOnlyList<CapturedDisplayImage> capturedDisplayImages,
        IReadOnlyList<ConversationTurn> conversationHistory,
        string userTranscript,
        Func<string, Task> onAccumulatedTextUpdated,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = OpenAIRequestFactory.CreateVisionRequest(
            apiKey,
            capturedDisplayImages,
            conversationHistory,
            userTranscript,
            stream: true);

        using HttpResponseMessage response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        ClickyLogger.Info($"OpenAI vision HTTP {(int)response.StatusCode}.");

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new OpenAIServiceException(
                $"OpenAI vision request failed with HTTP {(int)response.StatusCode}: {errorBody}",
                response.StatusCode);
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var streamReader = new StreamReader(responseStream, Encoding.UTF8);

        string accumulatedResponseText = string.Empty;
        while (!streamReader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await streamReader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            string jsonPayload = line["data: ".Length..];
            if (jsonPayload == "[DONE]")
            {
                break;
            }

            string? textDelta = TryExtractTextDelta(jsonPayload);
            if (string.IsNullOrEmpty(textDelta))
            {
                continue;
            }

            accumulatedResponseText += textDelta;
            string displayText = pointTagParser.RemovePointTag(accumulatedResponseText);
            await onAccumulatedTextUpdated(displayText);
        }

        PointTagResult pointTagResult = pointTagParser.Parse(accumulatedResponseText);
        string spokenResponseText = pointTagParser.RemovePointTag(accumulatedResponseText);
        ClickyLogger.Info($"OpenAI vision stream completed. Response characters: {accumulatedResponseText.Length}.");

        return new VisionTurnResult(
            FullResponseText: accumulatedResponseText,
            SpokenResponseText: spokenResponseText,
            PointTagResult: pointTagResult);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    private static string? TryExtractTextDelta(string jsonPayload)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(jsonPayload);
        JsonElement rootElement = jsonDocument.RootElement;

        if (!rootElement.TryGetProperty("type", out JsonElement typeElement))
        {
            return null;
        }

        string? eventType = typeElement.GetString();
        if (eventType == "response.output_text.delta" &&
            rootElement.TryGetProperty("delta", out JsonElement deltaElement))
        {
            return deltaElement.GetString();
        }

        if (eventType == "response.content_part.delta" &&
            rootElement.TryGetProperty("delta", out JsonElement contentPartDeltaElement) &&
            contentPartDeltaElement.TryGetProperty("text", out JsonElement nestedTextElement))
        {
            return nestedTextElement.GetString();
        }

        return null;
    }
}

public sealed class OpenAIServiceException : Exception
{
    public OpenAIServiceException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
