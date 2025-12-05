using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.AI;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;

namespace RimXmlEdit.Core.NodeDefine;

public class AIDefineGenerator
{
    private readonly AppSettings _appSettings;
    private readonly ILogger _log;

    private readonly JsonSerializerOptions _option = new()
    {
        WriteIndented = false,
        TypeInfoResolver = JsonRequestContent.Default
    };

    private AiAssistant _aiAssistant;

    public AIDefineGenerator(AppSettings appSetting)
    {
        _log = this.Log();
        _appSettings = appSetting;
        AppSettings.OnSettingChanged += AppSettingsOnOnSettingChanged;
        AppSettingsOnOnSettingChanged();
    }

    private void AppSettingsOnOnSettingChanged()
    {
        _aiAssistant = new AiAssistant(AiClientFactory.CreateClient(
            _appSettings.AI.AIProvider,
            _appSettings.AI.ModelId,
            _appSettings.AI.ApiKey,
            _appSettings.AI.Endpoint
        ));
        _aiAssistant.SetSystemPrompt(_appSettings.AI.AiPromptForDefineString);
    }

    public async Task GenerateDescriptionsForDbAsync(
        NodeDefinitionDatabase db,
        List<TypeSchema> schemas,
        int batchSize = 50)
    {
        var schemaLookup = schemas
            .GroupBy(s => GetShortName(s.FullName))
            .ToDictionary(g => g.Key, g => g.First());
        var schemaById = schemas;
        var pendingNodes = new List<NodeContext>();

        foreach (var kvp in db.NodeMap)
            if (string.IsNullOrEmpty(db.GetDescription(kvp.Key)))
            {
                var typeInfo = ResolveNodeType(kvp.Key, schemaLookup, schemaById);

                pendingNodes.Add(new NodeContext
                {
                    Key = kvp.Key,
                    TypeName = typeInfo
                });
            }

        _log.LogInformation("Found {Count} nodes waiting for AI descriptions.", pendingNodes.Count);

        for (var i = 0; i < pendingNodes.Count; i += batchSize)
        {
            var batch = pendingNodes.Skip(i).Take(batchSize).ToList();
            _log.LogNotify($"Processing batch {i / batchSize + 1}/{pendingNodes.Count / batchSize + 1}...");

            try
            {
                var results = await CallAiApi(batch);
                if (results != null && results.Count > 0)
                {
                    db.BatchUpdate(results);
                    _log.LogNotify($"Batch saved. Updated {results.Count} nodes.");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "AI generation failed for current batch.");
            }

            await Task.Delay(1000);
        }
    }

    private string ResolveNodeType(
        string nodePath,
        Dictionary<string, TypeSchema> rootLookup,
        List<TypeSchema> schemaById)
    {
        if (string.IsNullOrEmpty(nodePath)) return "Unknown";

        var parts = nodePath.Split('.');
        if (parts.Length == 0) return "Unknown";
        if (!rootLookup.TryGetValue(parts[0], out var currentSchema)) return "Unknown";
        if (parts.Length == 1) return currentSchema.FullName;
        for (var i = 1; i < parts.Length; i++)
        {
            var fieldName = parts[i];
            var field = currentSchema.Fields.FirstOrDefault(f => f.Name == fieldName);

            if (field == null)
            {
                if (fieldName == "li") continue;

                return "Unknown";
            }

            if (i == parts.Length - 1) return field.FieldTypeName;
            if (field.SchemaId >= 0 && field.SchemaId < schemaById.Count)
                currentSchema = schemaById[field.SchemaId];
            else
                return field.FieldTypeName;
        }

        return "Unknown";
    }

    private string GetShortName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        var idx = fullName.LastIndexOf('.');
        return idx >= 0 ? fullName.Substring(idx + 1) : fullName;
    }

    private async Task<Dictionary<string, string>> CallAiApi(List<NodeContext> nodes)
    {
        var sb = new StringBuilder();

        foreach (var node in nodes)
        {
            var typeHint = node.TypeName != "Unknown" ? $" [类型: {node.TypeName}]" : "";
            sb.AppendLine($"- {node.Key}{typeHint}");
        }

        try
        {
            var responseString = await _aiAssistant.AskAsync(sb.ToString());
            responseString = CleanJsonString(responseString);
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(responseString, _option);
            return result ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AI generation failed.");
            return new Dictionary<string, string>();
        }
    }

    private string CleanJsonString(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var result = source.Trim();
        if (result.StartsWith("```json"))
            result = result.Substring(7);
        else if (result.StartsWith("```")) result = result.Substring(3);

        if (result.EndsWith("```")) result = result.Substring(0, result.Length - 3);

        return result.Trim();
    }

    private struct NodeContext
    {
        public string Key;
        public string TypeName;
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class JsonRequestContent : JsonSerializerContext
{
}