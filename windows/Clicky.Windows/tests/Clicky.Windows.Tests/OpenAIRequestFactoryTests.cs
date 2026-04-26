using System.Net.Http.Headers;
using Clicky.Windows.AI;
using Clicky.Windows.Models;
using Xunit;

namespace Clicky.Windows.Tests;

public sealed class OpenAIRequestFactoryTests
{
    [Fact]
    public async Task CreateVisionRequestBuildsResponsesImageInputPayload()
    {
        var capturedDisplayImage = new CapturedDisplayImage(
            ImageBytes: [0xFF, 0xD8, 0xFF],
            Label: "screen 1 of 1 - cursor is on this screen (primary focus)",
            ScreenNumber: 1,
            IsCursorScreen: true,
            PixelBounds: new System.Drawing.Rectangle(0, 0, 1920, 1080),
            ScreenshotWidthInPixels: 1920,
            ScreenshotHeightInPixels: 1080);

        using HttpRequestMessage request = OpenAIRequestFactory.CreateVisionRequest(
            "sk-test",
            [capturedDisplayImage],
            [new ConversationTurn("hello", "hi there")],
            "what should i click?",
            stream: true);

        string requestBody = await request.Content!.ReadAsStringAsync();

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "sk-test"), request.Headers.Authorization);
        Assert.Contains("\"model\":\"gpt-5.5\"", requestBody);
        Assert.Contains("\"stream\":true", requestBody);
        Assert.Contains("\"type\":\"input_image\"", requestBody);
        Assert.Contains("data:image/jpeg;base64,", requestBody);
        Assert.Contains("what should i click?", requestBody);
    }

    [Fact]
    public async Task CreateSpeechRequestBuildsMiniTtsPayload()
    {
        using HttpRequestMessage request = OpenAIRequestFactory.CreateSpeechRequest("sk-test", "hello");

        string requestBody = await request.Content!.ReadAsStringAsync();

        Assert.Equal("https://api.openai.com/v1/audio/speech", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "sk-test"), request.Headers.Authorization);
        Assert.Contains("\"model\":\"gpt-4o-mini-tts\"", requestBody);
        Assert.Contains("\"voice\":\"coral\"", requestBody);
        Assert.Contains("\"response_format\":\"wav\"", requestBody);
    }

    [Fact]
    public async Task CreateTranscriptionRequestBuildsMultipartUpload()
    {
        using HttpRequestMessage request = OpenAIRequestFactory.CreateTranscriptionRequest("sk-test", [1, 2, 3]);

        string multipartBody = await request.Content!.ReadAsStringAsync();

        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "sk-test"), request.Headers.Authorization);
        Assert.Contains("name=model", multipartBody);
        Assert.Contains("gpt-4o-mini-transcribe", multipartBody);
        Assert.Contains("name=response_format", multipartBody);
        Assert.Contains("text", multipartBody);
    }
}
