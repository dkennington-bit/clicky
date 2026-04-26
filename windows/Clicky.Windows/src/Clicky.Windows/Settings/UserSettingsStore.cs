using System.Text.Json;

namespace Clicky.Windows.Settings;

public sealed class UserSettingsStore
{
    private readonly string settingsFilePath;

    public UserSettingsStore()
    {
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Clicky.Windows");
        Directory.CreateDirectory(appDataDirectory);
        settingsFilePath = Path.Combine(appDataDirectory, "settings.json");
    }

    public ClickyUserSettings Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            return new ClickyUserSettings();
        }

        string json = File.ReadAllText(settingsFilePath);
        return JsonSerializer.Deserialize<ClickyUserSettings>(json) ?? new ClickyUserSettings();
    }

    public void Save(ClickyUserSettings clickyUserSettings)
    {
        string json = JsonSerializer.Serialize(clickyUserSettings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(settingsFilePath, json);
    }
}

public sealed class ClickyUserSettings
{
    public bool IsCursorOverlayEnabled { get; set; } = true;
}
