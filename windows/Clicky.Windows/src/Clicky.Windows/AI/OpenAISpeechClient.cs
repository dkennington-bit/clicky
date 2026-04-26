using System.IO;
using System.Net.Http;
using System.Net;
using Clicky.Windows.Services;
using NAudio.Wave;

namespace Clicky.Windows.AI;

public sealed class OpenAISpeechClient : IOpenAISpeechClient, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly object playbackLock = new();

    private WaveOutEvent? currentWaveOutEvent;
    private WaveStream? currentWaveStream;

    public OpenAISpeechClient()
        : this(new HttpClient())
    {
    }

    public OpenAISpeechClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
        this.httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task SpeakAsync(
        string apiKey,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        StopPlayback();

        using HttpRequestMessage request = OpenAIRequestFactory.CreateSpeechRequest(apiKey, text);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        byte[] audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorText = System.Text.Encoding.UTF8.GetString(audioBytes);
            throw new OpenAIServiceException(
                $"OpenAI text-to-speech failed with HTTP {(int)response.StatusCode}: {errorText}",
                response.StatusCode);
        }

        await PlayWaveAudioAsync(audioBytes, cancellationToken);
    }

    public void StopPlayback()
    {
        lock (playbackLock)
        {
            currentWaveOutEvent?.Stop();
            currentWaveOutEvent?.Dispose();
            currentWaveOutEvent = null;

            currentWaveStream?.Dispose();
            currentWaveStream = null;
        }
    }

    public void Dispose()
    {
        StopPlayback();
        httpClient.Dispose();
    }

    private async Task PlayWaveAudioAsync(byte[] audioBytes, CancellationToken cancellationToken)
    {
        var playbackCompletedTaskCompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audioMemoryStream = new MemoryStream(audioBytes);
        var waveFileReader = new WaveFileReader(audioMemoryStream);
        var waveOutEvent = new WaveOutEvent();

        waveOutEvent.Init(waveFileReader);
        waveOutEvent.PlaybackStopped += (_, playbackStoppedEventArguments) =>
        {
            if (playbackStoppedEventArguments.Exception is not null)
            {
                playbackCompletedTaskCompletionSource.TrySetException(playbackStoppedEventArguments.Exception);
            }
            else
            {
                playbackCompletedTaskCompletionSource.TrySetResult(null);
            }
        };

        lock (playbackLock)
        {
            currentWaveStream = waveFileReader;
            currentWaveOutEvent = waveOutEvent;
        }

        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() =>
        {
            StopPlayback();
            playbackCompletedTaskCompletionSource.TrySetCanceled(cancellationToken);
        });

        waveOutEvent.Play();
        try
        {
            await playbackCompletedTaskCompletionSource.Task;
        }
        finally
        {
            StopPlayback();
        }
    }
}
