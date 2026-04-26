using System.IO;
using NAudio.Wave;
using NAudio.Utils;

namespace Clicky.Windows.Audio;

public sealed class MicrophonePushToTalkRecorder : IDisposable
{
    private readonly object recorderLock = new();

    private WaveInEvent? waveInEvent;
    private WaveFileWriter? waveFileWriter;
    private MemoryStream? audioMemoryStream;
    private TaskCompletionSource<byte[]>? stopRecordingTaskCompletionSource;

    public event EventHandler<double>? AudioLevelChanged;

    public bool IsRecording { get; private set; }

    public Task StartRecordingAsync()
    {
        lock (recorderLock)
        {
            if (IsRecording)
            {
                return Task.CompletedTask;
            }

            audioMemoryStream = new MemoryStream();
            var waveFormat = new WaveFormat(16000, 16, 1);
            waveFileWriter = new WaveFileWriter(new IgnoreDisposeStream(audioMemoryStream), waveFormat);
            stopRecordingTaskCompletionSource = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            waveInEvent = new WaveInEvent
            {
                WaveFormat = waveFormat,
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };
            waveInEvent.DataAvailable += HandleAudioDataAvailable;
            waveInEvent.RecordingStopped += HandleRecordingStopped;
            waveInEvent.StartRecording();
            IsRecording = true;
        }

        return Task.CompletedTask;
    }

    public Task<byte[]> StopRecordingAsync()
    {
        lock (recorderLock)
        {
            if (!IsRecording || waveInEvent is null || stopRecordingTaskCompletionSource is null)
            {
                return Task.FromResult(Array.Empty<byte>());
            }

            waveInEvent.StopRecording();
            return stopRecordingTaskCompletionSource.Task;
        }
    }

    public void Dispose()
    {
        lock (recorderLock)
        {
            waveInEvent?.Dispose();
            waveFileWriter?.Dispose();
            audioMemoryStream?.Dispose();
        }
    }

    private void HandleAudioDataAvailable(object? sender, WaveInEventArgs waveInEventArguments)
    {
        lock (recorderLock)
        {
            if (!IsRecording || waveFileWriter is null)
            {
                return;
            }

            waveFileWriter.Write(waveInEventArguments.Buffer, 0, waveInEventArguments.BytesRecorded);
            waveFileWriter.Flush();
        }

        AudioLevelChanged?.Invoke(this, CalculateNormalizedAudioLevel(waveInEventArguments.Buffer, waveInEventArguments.BytesRecorded));
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs stoppedEventArguments)
    {
        TaskCompletionSource<byte[]>? completionSource;
        byte[] wavAudioBytes;

        lock (recorderLock)
        {
            IsRecording = false;

            waveFileWriter?.Dispose();
            waveFileWriter = null;

            wavAudioBytes = audioMemoryStream?.ToArray() ?? Array.Empty<byte>();
            audioMemoryStream?.Dispose();
            audioMemoryStream = null;

            waveInEvent?.Dispose();
            waveInEvent = null;

            completionSource = stopRecordingTaskCompletionSource;
            stopRecordingTaskCompletionSource = null;
        }

        if (stoppedEventArguments.Exception is not null)
        {
            completionSource?.TrySetException(stoppedEventArguments.Exception);
        }
        else
        {
            completionSource?.TrySetResult(wavAudioBytes);
        }
    }

    private static double CalculateNormalizedAudioLevel(byte[] audioBuffer, int bytesRecorded)
    {
        if (bytesRecorded <= 0)
        {
            return 0;
        }

        double sumOfSquares = 0;
        int sampleCount = bytesRecorded / 2;
        for (int byteIndex = 0; byteIndex + 1 < bytesRecorded; byteIndex += 2)
        {
            short sample = BitConverter.ToInt16(audioBuffer, byteIndex);
            double normalizedSample = sample / 32768.0;
            sumOfSquares += normalizedSample * normalizedSample;
        }

        double rootMeanSquare = Math.Sqrt(sumOfSquares / Math.Max(1, sampleCount));
        return Math.Clamp(rootMeanSquare * 6.0, 0, 1);
    }
}
