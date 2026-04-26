using System.Net.Http;
using System.Net;
using Clicky.Windows.Services;

namespace Clicky.Windows.AI;

public sealed class OpenAITranscriptionClient : IOpenAITranscriptionClient, IDisposable
{
    private readonly HttpClient httpClient;

    public OpenAITranscriptionClient()
        : this(new HttpClient())
    {
    }

    public OpenAITranscriptionClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
        this.httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<string> TranscribePushToTalkAsync(
        string apiKey,
        byte[] wavAudioBytes,
        CancellationToken cancellationToken)
    {
        if (wavAudioBytes.Length == 0)
        {
            throw new InvalidOperationException("No audio was captured.");
        }

        using HttpRequestMessage request = OpenAIRequestFactory.CreateTranscriptionRequest(apiKey, wavAudioBytes);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenAIServiceException(
                $"OpenAI transcription failed with HTTP {(int)response.StatusCode}: {responseText}",
                response.StatusCode);
        }

        string transcript = responseText.Trim();
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new InvalidOperationException("OpenAI returned an empty transcript.");
        }

        return transcript;
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
