using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Parse;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Core.ValueValid;

namespace RimXmlEdit.Core;

/// <summary>
/// 此处主要是为了获取 <see cref="RXStruct" /> 的各类信息而提供的工具集合类
/// </summary>
public class NodeInfoManager
{
    private bool _isInited = false;
    private readonly ILogger _log;
    private readonly DefParser _parser;
    private Dictionary<string, DefInfo> _defInfoDict = new();
    private DefCache _cache;
    private ValidatorManager _validatorManager;
    private readonly AppSettings _setting;
    private readonly HashSet<string> _loadedDlls = new();
    public DefCache DataCache => _cache;

    public IEnumerable<string> LoadedDlls => _loadedDlls;

    // 暂时使用硬编码, 若模组内容中含有此项较多考虑反射获取
    public static IEnumerable<string> PatchesClassEnums => new List<string>
    {
        "PatchOperationAdd", "PatchOperationRemove", "PatchOperationReplace",
        "PatchOperationInsert", "PatchOperationSequence", "PatchOperationSetName",
        "PatchOperationAttributeAdd", "PatchOperationAttributeSet", "PatchOperationAttributeRemove",
        "PatchOperationFindMod", "PatchOperationConditional"
    };

    public NodeInfoManager(IOptions<AppSettings> options)
    {
        _log = this.Log();
        _parser = new DefParser();
        _validatorManager = new ValidatorManager();
        _setting = options?.Value;
    }

    public void Init()
    {
        if (!_isInited)
        {
            _cache = _parser.Parse();
            _defInfoDict = _cache.DefInfos.ToDictionary(info => info.TagName, info => info);

            if (_setting != null && _setting.AutoLoadDllDependencies)
            {
                // path后续可以通过解析模组id来自动导航到指定的目录下
                foreach (var dll in _setting.CurrentProject.DependentPaths)
                {
                    LoadDllDependencies(dll);
                }
            }

            _isInited = true;
        }
    }

    // 对于其他dll模组, 则需要在def上加上命名空间的前缀
    public void AddDll(string dllPath)
    {
        Init();
        LoadDllDependencies(dllPath);
    }

    private void LoadDllDependencies(string dllPath)
    {
        if (!_loadedDlls.Add(dllPath))
            return;

        if (!_parser.Init(dllPath))
        {
            _log.LogWarning("Unable to load {} dll file", Path.GetFileNameWithoutExtension(dllPath));
            return;
        }
        var infos = _parser.Parse(setPrefixDef: true);
        _cache.MergeWith(infos);
        _defInfoDict = _cache.DefInfos.ToDictionary(info => info.TagName, info => info);
    }

    public IEnumerable<string> GetRootList(bool isPatches = false)
    {
        return isPatches ? ["Operation"] : _defInfoDict.Keys;
    }

    /// <summary>
    /// 获取子节点列表
    /// </summary>
    /// <param name="name"> 节点所在的xpath路径 </param>
    /// <returns> </returns>
    public IEnumerable<XmlFieldInfo> GetChildList(string name)
    {
        var path = GetParsePath(name);
        return GetChildList(path).Item2;
    }

    /// <summary>
    /// 获取子节点名称或枚举值
    /// </summary>
    /// <param name="name"> 节点所在的xpath路径 </param>
    /// <returns> </returns>
    public IEnumerable<string> GetChildNameOrEnumValues(string name)
    {
        var path = GetParsePath(name);
        (XmlFieldInfo? Node, IEnumerable<XmlFieldInfo> Child) = GetChildList(path);

        if ((Node?.Type & XmlFieldType.PolymorphicList) is XmlFieldType.PolymorphicList)
        {
            if (Node?.PossibleClassValues != null && Node.PossibleClassValues.Count > 0)
                return Node.PossibleClassValues;
            else
                return ["li"];
        }

        // 无论有没有子项, 都可能是枚举(DefOf)
        if (Node != null && Node.FieldTypeName.StartsWith("List", StringComparison.OrdinalIgnoreCase))
        {
            var type = Node.FieldTypeName[5..^1];

            if (_cache.DefCasts.TryGetValue(type, out var value)
                && _cache.DefOfEnums.TryGetValue(value, out var info))
                return info;
            else if (name.EndsWith("li"))
                return Node.ResolveChildren(_cache.Schemas).Select(t => t.Name);
            else
                return ["li"];
        }

        // 此处虽然是defof, 但是不是list类型, 所以算是枚举填空, 只存在于enumable
        //if (Node != null && _cache.DefOfEnums.TryGetValue(Node.FieldTypeName, out var info2))
        //{
        //    return info2;
        //}
        List<string>? result;
        // 暂时只对Def有嵌套处理
        if (Child.FirstOrDefault(t => t.FieldTypeName == "Verse.Def") is XmlFieldInfo baseInfo)
        {
            result = Child.Where(t => t != baseInfo).Select(t => t.Name).ToList();
            result.AddRange(GetRootList());
        }
        else
        {
            result = Child.Select(t => t.Name).ToList();
        }
        if (result == null || result.Count == 0)
            _log.LogNotify("No child nodes found for this node");
        return result ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// 保守判断某个节点的值是否合法, 值类型未知则返回true
    /// </summary>
    /// <param name="name"> 节点所在的xpath路径 </param>
    /// <param name="value"> 使用的值 </param>
    /// <returns> 是否合法 </returns>
    public CheckResult CheckValueIsValid(string name, string value)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(name))
            return new CheckResult(false, "Empty value or name");

        if (ValueValidIgnored.IsIgnored(name)) return CheckResult.Empty;

        var path = GetParsePath(name);
        var field = GetDeepestAccessibleField(path);
        // 没有发现节点, 可能是没有导入dll, 不必提醒
        if (field == null) return CheckResult.Empty;

        // 如果是defof, 那么直接获取所有枚举, 转化到附属节点下面
        if (!field.Type.HasFlag(XmlFieldType.Enumable)
            && _cache.DefOfEnums.TryGetValue(field.FieldTypeName, out var info))
        {
            field.Type |= XmlFieldType.Enumable;
            field.EnumValues = info.ToList();
        }

        value = value.Replace(" ", "");

        if (ValueValidIgnored.IsIgnored(value)) return CheckResult.Empty;

        foreach (var validator in _validatorManager.ValueValids)
        {
            var result = validator.IsValid(field, value);
            if (result.IsEqual(CheckResult.Success)) return result;
            else if (!result.IsValid) return result;
        }
        return CheckResult.Empty;
    }

    private (XmlFieldInfo?, IEnumerable<XmlFieldInfo>) GetChildList(string[] path)
    {
        if (path.Length == 1)
        {
            return _defInfoDict.TryGetValue(path[0], out var defInfo)
                ? (null, defInfo.Fields)
                : (null, Enumerable.Empty<XmlFieldInfo>());
        }
        var field = GetDeepestAccessibleField(path);

        if (field == null) return (null, Enumerable.Empty<XmlFieldInfo>());

        if (field.Type.HasFlag(XmlFieldType.SimpleClass))
        {
            var fields = field.ResolveChildren(_cache.Schemas);
            return (fields == null ? (field, Enumerable.Empty<XmlFieldInfo>()) : (field, fields));
        }
        else
        {
            _log.LogInformation("Node \"{}\" has no child nodes", field?.Name);
            return (field, Enumerable.Empty<XmlFieldInfo>());
        }
    }

    private string[] GetParsePath(string originPath)
    {
        var path = originPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // 只返回最后Def节点开始的片段
        for (int i = path.Length - 1; i > 0; i--)
        {
            if (_defInfoDict.ContainsKey(path[i]))
                return path[i..];
        }
        return path;
    }

    /// <summary>
    /// 获取路径中最后一个可访问的有效节点（尽力查找）
    /// </summary>
    private XmlFieldInfo? GetDeepestAccessibleField(string[] path)
    {
        if (path == null || path.Length == 0) return null;

        if (!_defInfoDict.TryGetValue(path[0], out var defInfo))
        {
            _log.LogDebug("Definition \"{DefName}\" cannot be found. It may be from a missing community mod.", path[0]);
            return null;
        }

        if (path.Length == 1) return null;

        var currentFields = defInfo.Fields;
        XmlFieldInfo? lastValidNode = null;
        for (int i = 1; i < path.Length; i++)
        {
            var currentPathName = path[i];
            var matchedField = currentFields?.FirstOrDefault(t => t.Name == currentPathName);
            if (matchedField == null)
            {
                _log.LogDebug("Sub-node \"{Node}\" not found under \"{Parent}\". Returning last valid parent.", currentPathName, path[i - 1]);
                return lastValidNode;
            }
            lastValidNode = matchedField;

            // 已经获取最后一个节点, 不需要再获取此节点的子节点
            if (matchedField.Name == path[^1])
                break;

            if (matchedField.Type.HasFlag(XmlFieldType.SimpleClass))
            {
                currentFields = matchedField.ResolveChildren(_cache.Schemas);
            }
            else
            {
                return matchedField;
            }
        }
        return lastValidNode;
    }

    public struct CheckResult
    {
        public bool IsValid;
        public string ValueTypeOrErrMs;

        public static CheckResult Empty => new(true, null);

        public static CheckResult Success => new(true, "Ok");

        public CheckResult(bool isValid, string recommendType)
        {
            IsValid = isValid;
            ValueTypeOrErrMs = recommendType;
        }

        public override string ToString()
        {
            return "IsValid:" + IsValid + " ValueTypeOrErrMs:" + ValueTypeOrErrMs;
        }

        public bool IsEqual(CheckResult other)
        {
            return IsValid == other.IsValid && ValueTypeOrErrMs == other.ValueTypeOrErrMs;
        }
    }
}
