using System.IO;

namespace Clicky.Windows.Logging;

public static class ClickyLogger
{
    private static readonly object LogLock = new();

    public static string LogDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Clicky.Windows",
        "logs");

    public static string CurrentLogFilePath { get; } = Path.Combine(LogDirectoryPath, "current.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Interaction(string message)
    {
        Write("INTERACTION", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        lock (LogLock)
        {
            Directory.CreateDirectory(LogDirectoryPath);
            RotateIfNeeded();
            File.AppendAllText(
                CurrentLogFilePath,
                $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
        }
    }

    private static void RotateIfNeeded()
    {
        var currentLogFileInfo = new FileInfo(CurrentLogFilePath);
        if (!currentLogFileInfo.Exists || currentLogFileInfo.Length < 2_000_000)
        {
            return;
        }

        string archivedLogFilePath = Path.Combine(
            LogDirectoryPath,
            $"clicky-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
        File.Move(CurrentLogFilePath, archivedLogFilePath);
    }
}
