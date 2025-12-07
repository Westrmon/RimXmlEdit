using System.Text.RegularExpressions;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Parse;
using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Core.NodeDefine;

public partial class NodeDefineInfo
{
    private static readonly Regex DigitRegex = FiliterDigit();

    // Def中内容
    private static readonly HashSet<string> GlobalCommonFields = new()
    {
        "defName", "label", "description", "descriptionHyperlinks", "def"
    };

    private readonly AppSettings _appSettings;
    private readonly string _baseDir;

    private readonly DefCache _cache;
    private readonly ModParser _modParser;
    private readonly string _customDbPath;
    private NodeDefinitionDatabase _globalDb;
    private bool _isSetDes;
    private string _lang;
    private string _mainDbPath;

    public NodeDefineInfo(AppSettings appSettings, DefCache cache, string lang = "zh_CN")
    {
        _lang = lang;
        _appSettings = appSettings;
        _baseDir = Path.Combine(TempConfig.AppPath, "NodeDefine");
        _mainDbPath = Path.Combine(_baseDir, $"rimworld_{_lang}.db.bin");
        _customDbPath = Path.Combine(_baseDir, $"{_lang}.db.bin");
        _cache = cache;
        _modParser = new ModParser();
    }

    public int MaxDepth { get; set; } = 3;

    public void Init()
    {
        Directory.CreateDirectory(_baseDir);
        _globalDb = new NodeDefinitionDatabase();
        if (File.Exists(_mainDbPath))
        {
            var mainDb = NodeDefinitionDatabase.LoadFromFile(_mainDbPath);
            _globalDb.MergeWith(mainDb, true);
        }
        else
        {
            var info = _modParser.TryParse(null, ModParser.ParseRange.Core | ModParser.ParseRange.DLC);
            var structs = info.SelectMany(t => t.Defs);
            CreateModDefineFile(structs, "rimworld");
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
            catch (Exception ex)
            {
            }
        }

        if (File.Exists(_customDbPath))
        {
            var modDb = NodeDefinitionDatabase.LoadFromFile(_customDbPath);
            _globalDb.MergeWith(modDb, true);
        }
    }

    public void AddItem(string modName)
    {
        var dbPath = Path.Combine(_baseDir, $"{modName}_{_lang}.db.bin");
        if (!File.Exists(dbPath)) return;

        var mainDb = NodeDefinitionDatabase.LoadFromFile(_mainDbPath);
        _globalDb.MergeWith(mainDb, true);
    }

    public void ChangeCulture(string lang)
    {
        _lang = lang;
        _mainDbPath = Path.Combine(_baseDir, $"rimworld_{_lang}.db.bin");
        Init();
    }

    public string GetDefine(string nodeName)
    {
        return _globalDb.GetDescription(nodeName);
    }

    /// <summary>
    ///     更新节点叙述
    /// </summary>
    /// <param name="nodeName">节点的路径</param>
    /// <param name="newDesc">新的叙述</param>
    /// <param name="isGlobal">是否保存为全局</param>
    public void UpdateDefine(string nodeName, string newDesc, bool isGlobal)
    {
        _isSetDes = true;
        if (isGlobal)
            nodeName = nodeName.Split('.').Last();
        _globalDb.SetDescription(nodeName, newDesc);
    }

    public void Save()
    {
        if (!_isSetDes) return;
        _globalDb.SaveToFileAsync(_customDbPath);
        _isSetDes = false;
    }

    public void CreateModDefineFile(
        IEnumerable<RXStruct> structDefines,
        string modName,
        string? outputPath = null)
    {
        if (structDefines == null) return;

        var fileName = $"{modName}_{_lang}.db.bin";
        var targetPath = outputPath ?? Path.Combine(TempConfig.AppPath, "NodeDefine", fileName);
        var modDb = new NodeDefinitionDatabase();
        if (File.Exists(targetPath))
        {
            var existing = NodeDefinitionDatabase.LoadFromFile(targetPath);
            modDb.MergeWith(existing, true);
        }

        var globalSchemas = _cache.Schemas ?? new List<TypeSchema>();
        var visitedSchemas = new HashSet<int>();

        foreach (var structDefine in structDefines)
        {
            if (structDefine?.Defs == null) continue;

            foreach (var def in structDefine.Defs)
            {
                if (IsSystemOrUnity(def.FullName)) continue;
                if (string.IsNullOrEmpty(modDb.GetDescription(def.TagName))) modDb.SetDescription(def.TagName, "");

                visitedSchemas.Clear();
                CollectFieldsRecursive(modDb, def.Fields, globalSchemas, visitedSchemas, 1, def.TagName);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        modDb.SaveToFileAsync(targetPath);
        _globalDb.MergeWith(modDb);
    }

    public async Task AutoFillDescriptionsWithAi(string? targetModName = null, int batches = 50)
    {
        var ai = new AIDefineGenerator(_appSettings);
        NodeDefinitionDatabase targetDb;
        if (string.IsNullOrEmpty(targetModName))
        {
            targetDb = _globalDb;
        }
        else
        {
            var path = Path.Combine(_baseDir, $"{targetModName}_{_lang}.db.bin");
            targetDb = NodeDefinitionDatabase.LoadFromFile(path);
        }

        var schemas = _cache.Schemas ?? new List<TypeSchema>();

        await ai.GenerateDescriptionsForDbAsync(targetDb, schemas, batches);
        if (string.IsNullOrEmpty(targetModName))
        {
            _isSetDes = true;
            Save();
        }
        else
        {
            var path = Path.Combine(_baseDir, $"{targetModName}_{_lang}.db.bin");
            await targetDb.SaveToFileAsync(path);
            _globalDb.MergeWith(targetDb, true);
        }
    }

    public static async Task MergeDbFilesAsync(string pathA, string pathB, string outputPath)
    {
        var dbA = NodeDefinitionDatabase.LoadFromFile(pathA);
        var dbB = NodeDefinitionDatabase.LoadFromFile(pathB);
        dbA.MergeWith(dbB, true);
        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        await dbA.SaveToFileAsync(outputPath);
    }

    public static async Task ApplySourceToExistingTargetAsync(string originPath, string targetPath)
    {
        if (!File.Exists(originPath) || !File.Exists(targetPath)) return;

        var originDb = NodeDefinitionDatabase.LoadFromFile(originPath);
        var targetDb = NodeDefinitionDatabase.LoadFromFile(targetPath);

        var updates = new Dictionary<string, string>();
        foreach (var nodeKey in targetDb.NodeMap.Keys)
        {
            var originDesc = originDb.GetDescription(nodeKey);

            if (!string.IsNullOrEmpty(originDesc)) updates[nodeKey] = originDesc;
        }

        if (updates.Count > 0)
        {
            targetDb.BatchUpdate(updates);
            await targetDb.SaveToFileAsync(targetPath);
        }
    }

    private void CollectFieldsRecursive(
        NodeDefinitionDatabase db,
        List<XmlFieldInfo> fields,
        List<TypeSchema> schemas,
        HashSet<int> visitedSchemas,
        int currentDepth,
        string parentPath)
    {
        if (fields == null) return;
        if (MaxDepth > 0 && currentDepth > MaxDepth) return;

        var processedPatterns = new HashSet<string>();

        foreach (var field in fields)
        {
            if (IsSystemOrUnity(field.FieldTypeName)) continue;

            string nextPath;
            var nextDepth = currentDepth + 1;

            if (field.Name == "li")
            {
                nextPath = parentPath;
            }
            else
            {
                var normalizedKey = GetNormalizedKey(field.Name);
                if (!processedPatterns.Add(normalizedKey)) continue;

                string dbKey;

                if (GlobalCommonFields.Contains(field.Name))
                {
                    dbKey = field.Name;
                    nextPath = $"{parentPath}.{field.Name}";
                }

                else
                {
                    dbKey = $"{parentPath}.{field.Name}";
                    nextPath = dbKey;
                }

                if (!db.NodeMap.ContainsKey(dbKey)) db.SetDescription(dbKey, "");
            }

            var expanded = false;

            if (nextDepth <= MaxDepth && field.SchemaId >= 0 && field.SchemaId < schemas.Count)
                if (visitedSchemas.Add(field.SchemaId))
                {
                    CollectFieldsRecursive(db, schemas[field.SchemaId].Fields, schemas, visitedSchemas,
                        nextDepth, nextPath);
                    visitedSchemas.Remove(field.SchemaId);
                    expanded = true;
                }

            if (!expanded && field.Value != null)
            {
                if (field.Value is List<XmlFieldInfo> listChildren)
                {
                    CollectFieldsRecursive(db, listChildren, schemas, visitedSchemas, nextDepth, nextPath);
                    expanded = true;
                }
                else if (field.Value is Dictionary<string, XmlFieldInfo> dictChildren)
                {
                    CollectFieldsRecursive(db, dictChildren.Values.ToList(), schemas, visitedSchemas, nextDepth,
                        nextPath);
                    expanded = true;
                }
            }

            if (!expanded && field.Children != null && field.Children.Count > 0)
                CollectFieldsRecursive(db, field.Children, schemas, visitedSchemas, nextDepth, nextPath);
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
        return typeName is "String" or "Int32" or "Boolean" or "Single";
    }

    [GeneratedRegex(@"\d+", RegexOptions.Compiled)]
    private static partial Regex FiliterDigit();
}