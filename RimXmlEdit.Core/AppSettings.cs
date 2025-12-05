using System.Text.Json;
using System.Text.Json.Serialization;
using RimXmlEdit.Core.AI;
using RimXmlEdit.Core.Utils;
using static RimXmlEdit.Core.Utils.LoggerFactoryInstance;

namespace RimXmlEdit.Core;

public class AppSettings
{
    public string Author { get; set; } = "RimXmlEdit User";

    public string GamePath { get; set; } = string.Empty;

    public LogLevelConfig FileLoggingLevel { get; set; } = LogLevelConfig.Information;

    public LogLevelConfig NotificationLoggingLevel { get; set; } = LogLevelConfig.Warning;

    public int AutoSaveInterval { get; set; } = 1;

    public bool AutoValidateValuesAfterOpen { get; set; } = false;

    public int ValueValidationInterval { get; set; } = 500;

    public bool AutoExpandNodes { get; set; } = true;

    public bool AutoLoadDllDependencies { get; set; } = true;

    public string Language { get; set; } = "zh-CN";

    public Ai AI { get; set; } = new();

    public List<RecentPorjectsItem> RecentProjects { get; set; } = [];

    [JsonIgnore] public RecentPorjectsItem CurrentProject { get; set; } = null;

    public static event Action? OnSettingChanged;

    internal void UpdateSetting()
    {
        OnSettingChanged?.Invoke();
    }
}

public class RecentPorjectsItem
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public List<string> DependentPaths { get; set; } = [];
}

public class Ai
{
    public AiProvider AIProvider { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string AiPromptForGenNodeString { get; set; } = string.Empty;
    public string AiPromptForTransNodeString { get; set; } = string.Empty;
    public string AiPromptForDefineString { get; set; } = string.Empty;
}

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Ai))]
[JsonSerializable(typeof(AiProvider))]
[JsonSerializable(typeof(LogLevelConfig))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<RecentPorjectsItem>))]
[JsonSerializable(typeof(Dictionary<string, AppSettings>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public static class AppSettingsExtensions
{
    private static readonly JsonSerializerOptions _option = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        TypeInfoResolver = SourceGenerationContext.Default
    };

    public static void SaveAppSettings(this AppSettings settings, string filePath = "appsettings.json")
    {
        var wrapper = new Dictionary<string, AppSettings> { { "AppSettings", settings } };
        var json = JsonSerializer.Serialize(wrapper, _option);
        using var stream = File.Open(TempConfig.ConfigPath, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        writer.WriteAsync(json);
        settings.UpdateSetting();
    }
}