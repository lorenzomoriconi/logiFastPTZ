using System.Text.Json;

public sealed class AppSettings
{
    public int? Zoom { get; init; }

    public bool LivePreview { get; init; } = true;

    public bool MirrorPreview { get; init; }

    public bool AlwaysOnTop { get; init; }
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public AppSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogiFastPTZ",
            "settings.json");
    }

    public string SettingsPath { get; }

    public bool TryLoad(out AppSettings settings, out string? error)
    {
        settings = new AppSettings();
        error = null;

        if (!File.Exists(SettingsPath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SettingsPath);
            settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);

        if (directory is null)
        {
            throw new InvalidOperationException("Could not determine the settings directory.");
        }

        Directory.CreateDirectory(directory);

        string temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}
