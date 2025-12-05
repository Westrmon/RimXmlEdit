using System.Reflection;

namespace RimXmlEdit.Core.Utils;

public class TempConfig
{
    private static Dictionary<string, string>? _projectFolders = null;
    private static string? _projectPath = null;

    private readonly Dictionary<string, string> _config = new();

    public static Version AppVersion = Assembly.GetExecutingAssembly().GetName().Version!;

    public static string AppPath
    {
        get => AppDomain.CurrentDomain.BaseDirectory;
    }

    public static string ConfigPath
    {
        get => Path.Combine(AppPath, "appsettings.json");
    }

    public static string TemplatesPath
    {
        get => Path.Combine(AppPath, "Templates");
    }

    public static string ProjectPath
    {
        get => _projectPath ?? throw new ArgumentNullException($"No project path is set");
        set
        {
            _projectPath = value;
            _projectFolders = new Dictionary<string, string>
            {
                { "About", Path.Combine(_projectPath, "About") },
                { "Languages", Path.Combine(_projectPath, "Languages") },
                { "Textures", Path.Combine(_projectPath, "Textures") }
            };
        }
    }

    public static string GamePath { get; set; }

    public static Dictionary<string, string> ProjectFolders
        => _projectFolders ?? throw new ArgumentNullException("No project path is set");

    public string Get(string key)
    {
        if (_config.TryGetValue(key, out string? value))
        {
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return string.Empty;
    }

    public string Set(string key, string value)
        => _config[key] = value;
}
