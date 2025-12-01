using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Parse;

namespace RimXmlEdit.Core.Trans;

/// <summary>
///     可以提取需要翻译的节点, 同时序列化到文件
/// </summary>
public class TransNode : IDisposable
{
    private static readonly HashSet<string> ExcludeTranslatableTags = new()
    {
        "verbClass", "def", "defName", "compClass", "hediff"
    };

    private static readonly List<string> IncludeTranslatableTags = new()
    {
        "label"
    };

    private readonly Dictionary<string, DefInfo> _defDatabase;

    private readonly HashSet<string> _excludedTags;
    private readonly Dictionary<DefInfo, List<XmlFieldInfo>> _inheritedFieldsCache;

    private readonly ILogger _log;

    private readonly NodeInfoManager _manager;

    private readonly ModParser _modParser;
    private bool _isExist;
    private bool _isInit;
    private List<ModParser.ModInfo> _modInfos;
    private string _projectPath;
    private TransWorkspaceManager _workspaceManager;
    private TransWriter _writer;

    public TransNode(ModParser modParser, NodeInfoManager manager)
    {
        _log = this.Log();
        _manager = manager;
        _modParser = modParser;
        _excludedTags = new HashSet<string>();
        _defDatabase = new Dictionary<string, DefInfo>();
        _inheritedFieldsCache = new Dictionary<DefInfo, List<XmlFieldInfo>>();
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    /// <summary>
    ///     初始化并预解析文件
    /// </summary>
    /// <param name="projectPath">项目目录</param>
    /// <param name="targetLanguage">目标语言</param>
    public void TransInit(string projectPath, string targetLanguage)
    {
        _projectPath = projectPath;
        var path = Path.Combine(_projectPath, "Languages", targetLanguage);
        _isExist = Directory.Exists(path);
        _writer = new TransWriter(targetLanguage);
        _writer.Start();
        _workspaceManager = new TransWorkspaceManager(_projectPath, this);
        _excludedTags.Clear();
        _defDatabase.Clear();
        _inheritedFieldsCache.Clear();

        var parsed = _modParser.Parse([_projectPath], ModParser.ParseRange.CommunityMod, true).ToList();
        if (parsed.Count == 0) _log.LogWarning($"{nameof(TransNode)} parsed 0 files.");
        _modInfos = parsed;

        foreach (var mod in _modInfos)
        foreach (var rxStruct in mod.Defs)
        foreach (var def in rxStruct.Defs)
            if (!string.IsNullOrEmpty(def.Name))
                _defDatabase[def.Name] = def;

        _isInit = true;
    }

    /// <summary>
    ///     获取所有翻译 Token
    /// </summary>
    public IEnumerable<TransToken> GetTransToken(string baseLang = "English", TransMode mode = TransMode.GroupByFile)
    {
        if (!_isInit)
            throw new Exception("TransNode not initialized");
        var tokens = new List<TransToken>();
        tokens.AddRange(ExtractKeyedFromPath(_projectPath, baseLang));

        // Clear cache at start of extraction
        _inheritedFieldsCache.Clear();

        foreach (var mod in _modInfos)
        {
            var rootPath = mod.About.FilePath.Split("About")[0];
            foreach (var rxStruct in mod.Defs)
            foreach (var def in rxStruct.Defs)
                tokens.AddRange(rxStruct.IsPatch
                    ? ExtractFromPatch(def, rxStruct.FilePath, rootPath)
                    : ExtractFromDef(def, rxStruct.FilePath, rootPath));
        }

        return mode == TransMode.Random
            ? tokens.OrderBy(_ => Guid.NewGuid())
            : tokens.OrderBy(t => t.SourceFile).ThenBy(t => t.Key);
    }

    public void AddTransToken(TransToken token)
    {
        _writer.Enqueue(token);
    }

    /// <summary>
    ///     读取指定语言目录下现有的所有翻译数据
    /// </summary>
    /// <param name="languageFolder">语言文件夹路径</param>
    /// <returns></returns>
    public Dictionary<string, string> LoadExistingLanguageData(string languageFolder)
    {
        var result = new Dictionary<string, string>();
        if (!Directory.Exists(languageFolder)) return result;

        var xmlFiles = Directory.EnumerateFiles(languageFolder, "*.xml", SearchOption.AllDirectories);

        foreach (var file in xmlFiles)
            try
            {
                var doc = XDocument.Load(file);
                if (doc.Root == null) continue;

                foreach (var element in doc.Root.Elements())
                {
                    var key = element.Name.LocalName;
                    var value = element.Value;
                    result[key] = value;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load existing translation file: {File}", file);
            }

        return result;
    }

    public Task ExportWorkspaceAsync(IEnumerable<TransToken> tokens, string savePath)
    {
        if (!_isExist)
            return _workspaceManager.ExportWorkspaceAsync(tokens, savePath);
        return _workspaceManager.CreateMergedWorkspaceAsync(
            tokens,
            Path.Combine(_projectPath, "Languages", _writer.TargetLanguage),
            savePath);
    }

    public Task ApplyWorkspaceAsync(string workspaceFilePath, bool saveEmptyTranslations = false)
    {
        return _workspaceManager.ApplyWorkspaceAsync(workspaceFilePath, _writer, saveEmptyTranslations);
    }

    #region Keyed Logic

    private IEnumerable<TransToken> ExtractKeyedFromPath(string rootPath, string baseLang)
    {
        var keyedPath = Path.Combine(rootPath, "Languages", baseLang, "Keyed");
        if (!Directory.Exists(keyedPath)) yield break;

        var xmlFiles = Directory.EnumerateFiles(keyedPath, "*.xml", SearchOption.AllDirectories);

        foreach (var file in xmlFiles)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load Keyed file: {File}", file);
                continue;
            }

            if (doc.Root == null) continue;

            foreach (var element in doc.Root.Elements())
                if (!string.IsNullOrWhiteSpace(element.Name.LocalName))
                    yield return new TransToken
                    {
                        Key = element.Name.LocalName,
                        OriginalValue = element.Value,
                        Type = TransNodeType.Key,
                        SourceFile = Path.GetRelativePath(rootPath, file),
                        RootFile = rootPath,
                        IsList = false
                    };
        }
    }

    #endregion

    #region DefInjected Logic

    private IEnumerable<TransToken> ExtractFromDef(DefInfo def, string filePath, string rootPath)
    {
        var effectiveFields = GetInheritedFields(def);
        var defNameField = effectiveFields.FirstOrDefault(f => f.Name == "defName");
        if (defNameField == null || defNameField.Value is not string defNameStr) yield break;
        foreach (var token in ExtractRecursive(effectiveFields, defNameStr, def.TagName, filePath, rootPath))
            yield return token;
    }

    private List<XmlFieldInfo> GetInheritedFields(DefInfo def)
    {
        if (_inheritedFieldsCache.TryGetValue(def, out var cached))
            return cached;

        if (string.IsNullOrEmpty(def.ParentName) || !_defDatabase.TryGetValue(def.ParentName, out var parentDef))
        {
            _inheritedFieldsCache[def] = def.Fields;
            return def.Fields;
        }

        if (parentDef == def) return def.Fields;

        var parentFields = GetInheritedFields(parentDef);
        var mergedMap = new Dictionary<string, XmlFieldInfo>();
        foreach (var pField in parentFields) mergedMap[pField.Name] = pField;
        foreach (var cField in def.Fields) mergedMap[cField.Name] = cField;
        var result = mergedMap.Values.ToList();
        _inheritedFieldsCache[def] = result;
        return result;
    }

    private IEnumerable<TransToken> ExtractRecursive(object nodeValue, string currentKeyPath, string currentXmlPath,
        string filePath,
        string rootPath)
    {
        if (nodeValue is List<XmlFieldInfo> list)
        {
            var itemHandles = new string?[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item.Name != "li")
                    itemHandles[i] = item.Name;
                else
                    itemHandles[i] = GetBestHandleForListItem(item, currentXmlPath);
            }

            var finalKeys = ResolveHandleCollisions(itemHandles.ToList());

            for (var i = 0; i < list.Count; i++)
            {
                var field = list[i];
                var segmentName = finalKeys[i];
                if (string.IsNullOrEmpty(segmentName)) segmentName = field.Name == "li" ? i.ToString() : field.Name;

                var nextKeyPath = $"{currentKeyPath}.{segmentName}";

                var nextXmlPath = field.Name == "li"
                    ? $"{currentXmlPath}/{(string.IsNullOrEmpty(field.Ref)
                        ? "li"
                        : field.Ref)}"
                    : $"{currentXmlPath}/{field.Name}";

                foreach (var token in ProcessSingleField(field, nextKeyPath, nextXmlPath, filePath, rootPath))
                    yield return token;
            }
        }
        else if (nodeValue is Dictionary<string, XmlFieldInfo> dict)
        {
            foreach (var kvp in dict)
            {
                var field = kvp.Value;
                var nextKeyPath = $"{currentKeyPath}.{field.Name}";
                var nextXmlPath = $"{currentXmlPath}/{field.Name}";

                foreach (var token in ProcessSingleField(field, nextKeyPath, nextXmlPath, filePath, rootPath))
                    yield return token;
            }
        }
    }

    private string? GetBestHandleForListItem(XmlFieldInfo listItem, string currentXmlPath)
    {
        if (listItem.Name != "li") return null;
        if (listItem.TKey != null) return listItem.TKey;
        var refName = listItem.Ref;
        if (listItem.Value is List<XmlFieldInfo> childrenList)
            return FindHandleInFields(childrenList, refName, currentXmlPath);

        if (listItem.Value is Dictionary<string, XmlFieldInfo> childrenDict)
            return FindHandleInFields(childrenDict.Values, refName, currentXmlPath);
        return null;
    }

    private string? FindHandleInFields(IEnumerable<XmlFieldInfo> fields, string refName, string currentXmlPath)
    {
        var fieldList = fields as List<XmlFieldInfo> ?? fields.ToList();
        var tkey = fieldList.FirstOrDefault(f => f.Name == "key");
        var defNameField = fieldList.FirstOrDefault(f => f.Name == "defName");
        if (defNameField is { Value: string idVal } && !string.IsNullOrWhiteSpace(idVal))
            return idVal;
        if (tkey is { Value: string key } && !string.IsNullOrWhiteSpace(key))
            return key;


        if (!string.IsNullOrEmpty(refName))
        {
            var haveName = fieldList.FirstOrDefault(t => t.Name == "compClass");
            if (haveName != null)
            {
                refName = (haveName.Value as string)!.Split('.').Last();
                return refName;
            }

            var className = refName.Split('.').Last();
            if (className.StartsWith("CompProperties_Ability"))
                return $"CompAbilityEffect_{className["CompProperties_Ability".Length..]}";
            if (className.StartsWith("CompProperties_"))
                return className.Replace("CompProperties_", "Comp");
            else if (className.StartsWith("HediffComp"))
                return className.Replace("Properties", "");
        }

        foreach (var field in fieldList)
            if (MatchTag(currentXmlPath + '/' + field.Name) is not TransHandle.No
                && field.Value is string strVal
                && !string.IsNullOrWhiteSpace(strVal)
                && strVal.Length < 25)
            {
                var normalized = NormalizedHandle(strVal);
                if (!string.IsNullOrEmpty(normalized)) return normalized;
            }

        return null;
    }

    private IEnumerable<TransToken> ProcessSingleField(XmlFieldInfo field, string currentKeyPath, string currentXmlPath,
        string filePath,
        string rootPath)
    {
        switch (field.Value)
        {
            case string strVal:
            {
                if (field.TKey != null ||
                    (MatchTag(currentXmlPath) is not TransHandle.No && !string.IsNullOrWhiteSpace(strVal)))
                    if (_excludedTags.Add(currentKeyPath))
                        yield return new TransToken
                        {
                            Key = currentKeyPath,
                            OriginalValue = strVal,
                            Type = TransNodeType.Defs,
                            RootFile = rootPath,
                            SourceFile = filePath,
                            IsList = field.Name == "li"
                        };

                break;
            }
            case List<XmlFieldInfo>:
            case Dictionary<string, XmlFieldInfo>:
            {
                foreach (var childToken in ExtractRecursive(field.Value, currentKeyPath, currentXmlPath, filePath,
                             rootPath))
                    yield return childToken;
                break;
            }
        }
    }

    /// <summary>
    ///     处理 Patch 文件。
    /// </summary>
    private IEnumerable<TransToken> ExtractFromPatch(DefInfo patchRoot, string filePath, string rootPath)
    {
        return FindDefContextInPatchRecursive(patchRoot.Fields, patchRoot.TagName, filePath, rootPath);
    }

    private IEnumerable<TransToken> FindDefContextInPatchRecursive(object nodeValue, string xmlPath, string filePath,
        string rootPath)
    {
        string? keyPath = null;
        string? foundDefName = null;
        object? foundContext = null;
        XmlFieldInfo? foundDef = null;
        XmlFieldInfo? context = null;
        if (nodeValue is List<XmlFieldInfo> list)
        {
            foundDef = list.FirstOrDefault(t => t.Name == "xpath");
            list.Remove(foundDef);
            context = list.FirstOrDefault();
        }
        else if (nodeValue is Dictionary<string, XmlFieldInfo> dict)
        {
            dict.TryGetValue("xpath", out foundDef);
            dict.Remove("xpath");
            context = dict.FirstOrDefault().Value;
        }

        if (foundDef != null && context != null)
        {
            var xpath = foundDef.Value as string;
            var result = XPathParser.GetXpathContent(xpath);
            if (!string.IsNullOrEmpty(result.DefName))
            {
                foundDefName = result.DefName;
                foundContext = context.Value;
                if (result.XmlPath.EndsWith("li"))
                    result.XmlPath = result.XmlPath[..^3];
                xmlPath = result.XmlPath;
                keyPath = foundDefName + '.' + xmlPath[(xmlPath.IndexOf('/') + 1)..];
            }
        }

        if (foundDefName != null && foundContext != null)
            foreach (var token in ExtractRecursive(foundContext, keyPath, xmlPath, filePath, rootPath))
                yield return token;
        if (nodeValue is List<XmlFieldInfo> listToRecurse)
        {
            foreach (var item in listToRecurse)
                if (item.Value is not string)
                    foreach (var token in FindDefContextInPatchRecursive(item.Value, xmlPath + '/' + item.Name,
                                 filePath, rootPath))
                        yield return token;
        }
        else if (nodeValue is Dictionary<string, XmlFieldInfo> dictToRecurse)
        {
            foreach (var kvp in dictToRecurse)
                if (kvp.Value.Value is not string)
                    foreach (var token in FindDefContextInPatchRecursive(kvp.Value.Value,
                                 xmlPath + '/' + kvp.Value.Name, filePath, rootPath))
                        yield return token;
        }
    }

    private TransHandle MatchTag(string currentXmlPath)
    {
        var pathSegments =
            currentXmlPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pathSegments[^1] is "label" or "description") return TransHandle.Key;
        if (ExcludeTranslatableTags.Contains(pathSegments[^1])) return TransHandle.No;
        var info = _manager.GetDeepestAccessibleField(pathSegments);
        if (info == null) return TransHandle.No;
        if (info.IsHaveTranslationHandle
            || (info.FieldTypeName == "System.String"
                && IncludeTranslatableTags.Any(t =>
                    info.Name.EndsWith(t, StringComparison.OrdinalIgnoreCase))))
            return TransHandle.Key;
        if (info.MustTranslate)
            return TransHandle.Value;
        return TransHandle.No;
    }

    #endregion

    # region Source

    // === Verse.TranslationHandleUtility ===
    private static readonly Regex StringFormatSymbolsRegex = new("{.*?}");
    private static readonly StringBuilder TmpStringBuilder = new();

    // If property have attribute TranslationHandle
    private static string NormalizedHandle(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return handle;
        handle = handle.Trim();
        handle = handle.Replace(' ', '_');
        handle = handle.Replace('\n', '_');
        handle = handle.Replace("\r", "");
        handle = handle.Replace('\t', '_');
        handle = handle.Replace(".", "");
        if (handle.IndexOf('-') >= 0) handle = handle.Replace('-'.ToString(), "");
        if (handle.IndexOf('{') >= 0) handle = StringFormatSymbolsRegex.Replace(handle, "");
        TmpStringBuilder.Length = 0;
        for (var i = 0; i < handle.Length; i++)
            if ("qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM1234567890-_".IndexOf(handle[i]) >= 0)
                TmpStringBuilder.Append(handle[i]);

        handle = TmpStringBuilder.ToString();
        TmpStringBuilder.Length = 0;
        for (var j = 0; j < handle.Length; j++)
            if (j == 0 || handle[j] != '_' || handle[j - 1] != '_')
                TmpStringBuilder.Append(handle[j]);

        handle = TmpStringBuilder.ToString();
        handle = handle.Trim('_');
        if (!string.IsNullOrEmpty(handle) && handle.All(char.IsDigit)) handle = "_" + handle;
        return handle;
    }

    // GetBestHandleWithIndexForListElement
    private List<string> ResolveHandleCollisions(List<string?> handles)
    {
        var results = new string[handles.Count];
        var counts = new Dictionary<string, int>();
        foreach (var h in handles)
        {
            if (h == null) continue;
            counts.TryAdd(h, 0);
            counts[h]++;
        }

        var currentIndices = new Dictionary<string, int>();
        for (var i = 0; i < handles.Count; i++)
        {
            var h = handles[i];

            if (h == null)
            {
                results[i] = i.ToString();
            }
            else
            {
                var count = counts[h];

                if (count <= 1)
                {
                    results[i] = h;
                }
                else
                {
                    currentIndices.TryAdd(h, 0);
                    var subIndex = currentIndices[h];

                    results[i] = $"{h}-{subIndex}";

                    currentIndices[h]++;
                }
            }
        }

        return results.ToList();
    }

    # endregion
}

public enum TransHandle : byte
{
    No,
    Key,
    Value
}

public enum TransMode : byte
{
    None,
    GroupByFile,
    Random
}

public enum TransNodeType : byte
{
    Key,
    Defs
}

public struct TransToken
{
    /// <summary>
    ///     翻译 Key (例如: MyDef.label 或 MyKey_Message)
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    ///     英文原文本
    /// </summary>
    public string OriginalValue { get; set; }

    /// <summary>
    ///     节点类型 (Keyed UI 文本 或 DefInjected 数据文本)
    /// </summary>
    public TransNodeType Type { get; set; }

    /// <summary>
    ///     来源文件路径
    /// </summary>
    public string SourceFile { get; set; }

    public string RootFile { get; set; }

    /// <summary>
    ///     是否是列表项 (辅助标记)
    /// </summary>
    public bool IsList { get; set; }

    public override string ToString()
    {
        return $"[{Type}] {Key}: {OriginalValue}";
    }
}