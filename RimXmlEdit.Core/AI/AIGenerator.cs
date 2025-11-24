using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static RimXmlEdit.Core.AI.AIGenerator;

namespace RimXmlEdit.Core.AI;

public class AIGenerator
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly ILogger _log;
    private readonly HttpClient _client;

    private readonly JsonSerializerOptions _option = new JsonSerializerOptions
    {
        WriteIndented = false,
        TypeInfoResolver = JsonRequestContent.Default
    };

    public AIGenerator(string apiKey, string endpoint = "https://api.openai.com/v1", string model = "gpt-3.5-turbo")
    {
        _apiKey = apiKey;
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
        _log = this.Log();
        _client = new HttpClient();
        _client.Timeout = TimeSpan.FromMinutes(2);
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
        {
            if (string.IsNullOrEmpty(db.GetDescription(kvp.Key)))
            {
                string typeInfo = ResolveNodeType(kvp.Key, schemaLookup, schemaById);

                pendingNodes.Add(new NodeContext
                {
                    Key = kvp.Key,
                    TypeName = typeInfo
                });
            }
        }

        _log.LogInformation("Found {Count} nodes waiting for AI descriptions.", pendingNodes.Count);

        for (int i = 0; i < pendingNodes.Count; i += batchSize)
        {
            var batch = pendingNodes.Skip(i).Take(batchSize).ToList();
            _log.LogNotify($"Processing batch {i / batchSize + 1}/{(pendingNodes.Count / batchSize) + 1}...");

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
        if (!rootLookup.TryGetValue(parts[0], out var currentSchema))
        {
            return "Unknown";
        }
        if (parts.Length == 1) return currentSchema.FullName;
        for (int i = 1; i < parts.Length; i++)
        {
            string fieldName = parts[i];
            var field = currentSchema.Fields.FirstOrDefault(f => f.Name == fieldName);

            if (field == null)
            {
                if (fieldName == "li")
                {
                    continue;
                }

                return "Unknown";
            }
            if (i == parts.Length - 1)
            {
                return field.FieldTypeName;
            }
            if (field.SchemaId >= 0 && field.SchemaId < schemaById.Count)
            {
                currentSchema = schemaById[field.SchemaId];
            }
            else
            {
                return field.FieldTypeName;
            }
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
        sb.AppendLine("你是一个 RimWorld 模组 XML 专家。请根据 XML 节点路径和 C# 数据类型，解释该节点的作用。");
        sb.AppendLine("要求：中文，简练(30字内)，必须且只能返回纯 JSON 格式 (Key=路径, Value=描述)，不要包含 Markdown 代码块标记。");
        sb.AppendLine("待处理列表：");

        foreach (var node in nodes)
        {
            string typeHint = node.TypeName != "Unknown" ? $" [类型: {node.TypeName}]" : "";
            sb.AppendLine($"- {node.Key}{typeHint}");
        }
        var requestBody = new RequestBody
        {
            model = _model,
            messages = new List<MessagesItem>
            {
                new MessagesItem { role = "system", content = "你是一个只输出 JSON 的 API 接口。" },
                new MessagesItem { role = "user", content = sb.ToString() }
            }
        };

        try
        {
            var jsonContent = JsonSerializer.Serialize(requestBody, _option);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            requestMessage.Content = httpContent;
            //var response = _client.Send(requestMessage);
            var response = await _client.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new Dictionary<string, string>();
            }
            content = CleanJsonString(content);
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(content, _option);
            return result ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API 调用失败: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    private string CleanJsonString(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var result = source.Trim();
        if (result.StartsWith("```json"))
        {
            result = result.Substring(7);
        }
        else if (result.StartsWith("```"))
        {
            result = result.Substring(3);
        }

        if (result.EndsWith("```"))
        {
            result = result.Substring(0, result.Length - 3);
        }

        return result.Trim();
    }

    private struct NodeContext
    {
        public string Key;
        public string TypeName;
    }

    public class RequestBody
    {
        public string model { get; set; }
        public List<MessagesItem> messages { get; set; }
        public double temperature { get; set; } = 0.3;

        public bool stream { get; set; } = false;
    }

    public class MessagesItem
    {
        public string role { get; set; }

        public string content { get; set; }
    }
}

[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(RequestBody))]
[JsonSerializable(typeof(MessagesItem))]
[JsonSerializable(typeof(List<MessagesItem>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = false)]
public partial class JsonRequestContent : JsonSerializerContext
{
}
