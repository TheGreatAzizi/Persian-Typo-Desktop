using System.Text.Encodings.Web;
using System.Text.Json;

namespace PersianTypoFixer.Core;

public sealed class AppConfig : FixOptions
{
    public string Hotkey { get; set; } = "Ctrl+Shift+F";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DirectoryPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersianTypoFixer")
                : Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "persian-typo-fixer");
        }
    }

    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions);
                if (config != null)
                {
                    config.DisabledSites ??= new List<string>();
                    config.CustomWords ??= new List<CustomWord>();
                    return config;
                }
            }
        }
        catch
        {
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
        }
    }
}
