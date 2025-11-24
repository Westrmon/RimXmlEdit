using RimXmlEdit.Core.AI;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Utils;
using System.Text.RegularExpressions;

namespace RimXmlEdit.Core;

public partial class NodeDefineInfo
{
    private string _lang;
    private string _mainDbPath;
    private readonly string _baseDir;
    private NodeDefinitionDatabase _globalDb;

    private readonly List<TypeSchema> Schemas;
    private const int MaxDepth = 2;
    private static readonly Regex DigitRegex = FiliterDigit();

    public NodeDefineInfo(DefCache cache, string lang = "zh_CN")
    {
        _lang = lang;
        _baseDir = Path.Combine(TempConfig.AppPath, "NodeDefine");
        _mainDbPath = Path.Combine(_baseDir, $"rimworld_{_lang}.db.bin");
        Schemas = cache.Schemas;
        Init();
    }

    public void Init()
    {
        Directory.CreateDirectory(_baseDir);
        _globalDb = new NodeDefinitionDatabase();
        if (File.Exists(_mainDbPath))
        {
            var mainDb = NodeDefinitionDatabase.LoadFromFile(_mainDbPath);
            _globalDb.MergeWith(mainDb, true);
        }
        var files = Directory.GetFiles(_baseDir, $"*_{_lang}.db.bin");
        foreach (var file in files)
        {
            if (Path.GetFileName(file) == Path.GetFileName(_mainDbPath)) continue;
            try
            {
                var modDb = NodeDefinitionDatabase.LoadFromFile(file);
                _globalDb.MergeWith(modDb, true);
            }
            catch { }
        }
    }

    public void AddItem(string modName)
    {
        string dbPath = Path.Combine(_baseDir, $"{modName}_{_lang}.db.bin");
        if (File.Exists(dbPath))
        {
            var mainDb = NodeDefinitionDatabase.LoadFromFile(_mainDbPath);
            _globalDb.MergeWith(mainDb, true);
        }
    }

    public void ChangeCulture(string lang)
    {
        _lang = lang;
        _mainDbPath = Path.Combine(_baseDir, $"rimworld_{_lang}.db.bin");
        Init();
    }

    public string GetDefine(string nodeName) => _globalDb.GetDescription(nodeName);

    public void UpdateDefine(string nodeName, string newDesc) => _globalDb.SetDescription(nodeName, newDesc);

    public void Save() => _globalDb.SaveToFileAsync(_mainDbPath);

    public void CreateModDefineFile(
        RXStruct? structDefine,
        string modName,
        string? outputPath = null)
    {
        if (structDefine == null || structDefine.Defs == null) return;

        string fileName = $"{modName}.db.bin";
        string targetPath = outputPath ?? Path.Combine(TempConfig.AppPath, "NodeDefine", fileName);
        var modDb = new NodeDefinitionDatabase();
        if (File.Exists(targetPath))
        {
            var existing = NodeDefinitionDatabase.LoadFromFile(targetPath);
            modDb.MergeWith(existing, true);
        }
        var globalSchemas = Schemas ?? new List<TypeSchema>();

        var visitedSchemas = new HashSet<int>();
        foreach (var def in structDefine.Defs)
        {
            if (IsSystemOrUnity(def.FullName)) continue;
            if (string.IsNullOrEmpty(modDb.GetDescription(def.TagName)))
            {
                modDb.SetDescription(def.TagName, "");
            }

            visitedSchemas.Clear();
            CollectFieldsRecursive(modDb, def.Fields, globalSchemas, visitedSchemas, 1, def.TagName);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        modDb.SaveToFileAsync(targetPath);
    }

    public async Task AutoFillDescriptionsWithAI(string apiKey, string endpoint, string model, string? targetModName = null)
    {
        var ai = new AIGenerator(apiKey, endpoint, model);

        NodeDefinitionDatabase targetDb;

        if (string.IsNullOrEmpty(targetModName))
        {
            targetDb = _globalDb;
        }
        else
        {
            string path = Path.Combine(_baseDir, $"{targetModName}.db.bin");
            targetDb = NodeDefinitionDatabase.LoadFromFile(path);
        }
        var schemas = Schemas ?? new List<TypeSchema>();

        await ai.GenerateDescriptionsForDbAsync(targetDb, schemas);
        if (string.IsNullOrEmpty(targetModName))
        {
            Save();
        }
        else
        {
            string path = Path.Combine(_baseDir, $"{targetModName}.db.bin");
            await targetDb.SaveToFileAsync(path);
            _globalDb.MergeWith(targetDb, true);
        }
    }

    private static void CollectFieldsRecursive(
        NodeDefinitionDatabase db,
        List<XmlFieldInfo> fields,
        List<TypeSchema> schemas,
        HashSet<int> visitedSchemas,
        int currentDepth,
        string parentPath)
    {
        if (fields == null) return;
        if (currentDepth > MaxDepth) return;

        var processedPatterns = new HashSet<string>();

        foreach (var field in fields)
        {
            if (IsSystemOrUnity(field.FieldTypeName)) continue;
            string normalizedKey = GetNormalizedKey(field.Name);
            if (processedPatterns.Contains(normalizedKey)) continue;
            processedPatterns.Add(normalizedKey);

            string currentPath = $"{parentPath}.{field.Name}";
            if (!db.NodeMap.ContainsKey(currentPath))
            {
                db.SetDescription(currentPath, "");
            }
            bool expanded = false;
            if (currentDepth < MaxDepth && field.SchemaId >= 0 && field.SchemaId < schemas.Count)
            {
                if (!visitedSchemas.Contains(field.SchemaId))
                {
                    visitedSchemas.Add(field.SchemaId);
                    CollectFieldsRecursive(db, schemas[field.SchemaId].Fields, schemas, visitedSchemas, currentDepth + 1, currentPath);
                    visitedSchemas.Remove(field.SchemaId);
                    expanded = true;
                }
            }
            if (!expanded && field.Children != null && field.Children.Count > 0)
            {
                CollectFieldsRecursive(db, field.Children, schemas, visitedSchemas, currentDepth + 1, currentPath);
            }
        }
    }

    private static string GetNormalizedKey(string fieldName)
    {
        if (fieldName.Length == 1 && char.IsLetter(fieldName[0])) return "#SingleLetter#";
        return DigitRegex.Replace(fieldName, "#");
    }

    private static bool IsSystemOrUnity(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        if (typeName[0] == 'S' && typeName.StartsWith("System")) return true;
        if (typeName[0] == 'U' && typeName.StartsWith("UnityEngine")) return true;
        if (typeName.Contains("<System") || typeName.Contains("<UnityEngine")) return true;
        if (typeName == "String" || typeName == "Int32" || typeName == "Boolean" || typeName == "Single") return true;
        return false;
    }

    [GeneratedRegex(@"\d+", RegexOptions.Compiled)]
    private static partial Regex FiliterDigit();
}
