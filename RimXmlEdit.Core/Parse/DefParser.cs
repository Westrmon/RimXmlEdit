using MessagePack;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using System.Reflection;
using System.Text;

namespace RimXmlEdit.Core.Parse;

// pathes 节点使用PatchOperation派生
public class DefParser
{
    private readonly ILogger _log;
    private string _dllPath;
    private Assembly _rimworldAssembly;
    private ILookup<Type, Type> _subclassLookup;
    private List<Type> _allTypes;

    private Dictionary<Type, RawTypeInfo> _rawTypeDict = new Dictionary<Type, RawTypeInfo>();
    private Dictionary<Type, List<string>> _defOfValuesCache = new();
    private Dictionary<Type, int> _schemaIdMap = new();
    private List<TypeSchema> _schemaList = new();

    private Type? _tDef;
    private Type? _tDefOf;

    public DefParser(string? dllPath = null)
    {
        _log = this.Log(); // 扩展方法
        var finalDllPath = dllPath ?? Path.Combine(TempConfig.GamePath, "RimWorldWin64_Data", "Managed", "Assembly-CSharp.dll");
        Init(finalDllPath);
    }

    public bool Init(string dllPath)
    {
        _dllPath = dllPath;
        if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath)) return false;

        try
        {
            _rimworldAssembly = Assembly.LoadFrom(_dllPath);
            _allTypes = _rimworldAssembly.GetTypes().ToList();
            // 建立子类索引，优化查找速度
            _subclassLookup = _allTypes.Where(t => t.BaseType != null).ToLookup(t => t.BaseType!);

            if (dllPath.EndsWith("Assembly-CSharp.dll"))
            {
                _tDef = _rimworldAssembly.GetType("Verse.Def");
                _tDefOf = _rimworldAssembly.GetType("RimWorld.DefOf");
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load assembly: {Path}", dllPath);
            return false;
        }
    }

    /// <summary>
    /// 解析dll信息, (Def, DefOf...)
    /// </summary>
    /// <param name="forceSave"> 是否强制保存为缓存 </param>
    /// <param name="defOutputPath"> 解析出定义的输出路径 </param>
    /// <param name="setPrefixDef"> 是否保留def的命名空间前缀(一般用于社区模组) </param>
    /// <returns> </returns>
    public DefCache Parse(bool forceSave = false, string? defOutputPath = null, bool setPrefixDef = false)
    {
        DefCache? cache = null;
        string cachePath = Path.Combine(TempConfig.AppPath, "cache", Path.GetFileNameWithoutExtension(_dllPath) + "Cache.bin");

        if (!File.Exists(cachePath) || forceSave)
        {
            _log.LogNotify("Starting fresh parse...");

            // Reset State
            _schemaList.Clear();
            _schemaIdMap.Clear();
            _rawTypeDict.Clear();
            _defOfValuesCache.Clear();

            ExtractDataFromAssemblies();

            var result = new List<DefInfo>();
            var allDefTypes = _allTypes.Where(t => _tDef != null && t.IsSubclassOf(_tDef));

            foreach (Type defType in allDefTypes)
            {
                // 优先使用提取的 RawInfo，但也允许直接反射
                if (_rawTypeDict.TryGetValue(defType, out var rawInfo))
                {
                    result.Add(MapToDefInfo(rawInfo, defType, setPrefixDef));
                }
            }

            result = result.OrderBy(d => d.TagName).ToList();

            cache = new DefCache(result,
                _defOfValuesCache.ToDictionary(r => r.Key.FullName ?? r.Key.Name, r => r.Value.AsEnumerable()),
                _schemaList);

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

            // 1. Save DefCache
            using (var fs = File.Create(cachePath))
            {
                MessagePackSerializer.Serialize(fs, cache);
            }

            _log.LogNotify("Parse complete. {Name} Defs: {Count}", Path.GetFileNameWithoutExtension(_dllPath), result.Count);
        }
        else
        {
            // Load existing
            using (var fs = File.OpenRead(cachePath))
            {
                cache = MessagePackSerializer.Deserialize<DefCache>(fs);
            }
            try
            {
                _schemaList = cache.Schemas ?? new List<TypeSchema>();
                // Rebuild ID Map
                _schemaIdMap.Clear();
                for (int i = 0; i < _schemaList.Count; i++)
                {
                    var t = _allTypes.FirstOrDefault(x => x.FullName == _schemaList[i].FullName);
                    if (t != null) _schemaIdMap[t] = i;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to load schemas, will regenerate if needed.");
            }
            _log.LogNotify("Loading successfully from cache. {Name} Defs: {Count}",
                Path.GetFileNameWithoutExtension(_dllPath),
                cache.DefInfos.Count);
        }

        if (!string.IsNullOrEmpty(defOutputPath))
        {
            WriteDefOutput(cache!.DefInfos, defOutputPath);
        }

        return cache!;
    }

    #region Extraction (BFS)

    private void ExtractDataFromAssemblies()
    {
        if (_tDef == null) return;

        LoadDefOfClasses();

        // 初始种子：所有的 Def 和 CompProperties
        var initialTypes = _allTypes.Where(t => t != null && (t.IsSubclassOf(_tDef) || t.Name.Contains("CompProperties")));

        CollectData_BFS(initialTypes);
        var relatedTypes = _allTypes.Where(t =>
            t != null &&
            !t.IsPrimitive &&
            t.Namespace != null &&
            !t.Namespace.StartsWith("System") &&
            !_rawTypeDict.ContainsKey(t) &&
            !TypeFilter.IsBannedType(t)
        );
    }

    private void CollectData_BFS(IEnumerable<Type> types)
    {
        var queue = new Queue<Type>(types);
        var visited = new HashSet<Type>();

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (type == null || visited.Contains(type) || TypeFilter.IsBannedType(type))
                continue;

            visited.Add(type);

            if (!_rawTypeDict.ContainsKey(type))
            {
                if (TryNewRawTypeInfo(type, out var info))
                    _rawTypeDict[type] = info;
                else
                    continue; // 无法分析该类型，跳过子字段分析
            }

            // 分析字段，将新发现的类型加入队列
            var infoRef = _rawTypeDict[type];
            foreach (var field in infoRef.fields.Values)
            {
                var ft = field.FieldType;
                if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                {
                    ft = ft.GetGenericArguments()[0];
                }

                if (!visited.Contains(ft) && !IsPrimitive(ft) && !_rawTypeDict.ContainsKey(ft))
                {
                    queue.Enqueue(ft);
                }
            }
        }
    }

    #endregion Extraction (BFS)

    #region Mapping & Schema

    private DefInfo MapToDefInfo(RawTypeInfo rawInfo, Type defType, bool setPrefixDef)
    {
        var defInfo = new DefInfo
        {
            TagName = setPrefixDef ? rawInfo.fullName : rawInfo.className,
            IsAbstract = defType.IsAbstract,
            FullName = rawInfo.fullName,
            Name = defType.Name,
            ParentName = defType.BaseType?.Name ?? ""
        };

        // Def 的顶层字段，使用 Schema 逻辑生成，但 Def 本身不作为 Schema (因为它是根) 这里我们需要“扁平化”继承链上的所有字段
        defInfo.Fields = CollectFieldsFlattened(defType);

        return defInfo;
    }

    private List<XmlFieldInfo> CollectFieldsFlattened(Type type)
    {
        var fields = new List<XmlFieldInfo>();
        var processedNames = new HashSet<string>();

        var current = type;
        while (current != null && current != typeof(object))
        {
            // 优先从缓存取，取不到则反射（容错）
            IEnumerable<RawFieldInfo> currentFields = Enumerable.Empty<RawFieldInfo>();
            if (_rawTypeDict.TryGetValue(current, out var raw))
            {
                currentFields = raw.fields.Values;
            }
            else
            {
                currentFields = current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(f => !TypeFilter.IsBannedField(f))
                    .Select(f => new RawFieldInfo(f));
            }

            foreach (var field in currentFields)
            {
                if (processedNames.Add(field.name))
                {
                    fields.Add(CreateXmlFieldInfo(field.name, field.FieldType));
                }
            }
            current = current.BaseType;
        }
        return fields;
    }

    /// <summary>
    /// 核心方法：创建字段信息。如果是复杂类型，自动生成或引用 Schema。
    /// </summary>
    private XmlFieldInfo CreateXmlFieldInfo(string name, Type type)
    {
        var info = new XmlFieldInfo { Name = name };

        // 1. 处理 List<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = type.GetGenericArguments()[0];
            info.FieldTypeName = $"List<{SimplifyTypeName(itemType)}>";

            // 判断多态
            if (itemType.IsAbstract || itemType.IsInterface || (itemType.BaseType != null && _subclassLookup.Contains(itemType)))
            {
                info.Type |= XmlFieldType.PolymorphicList;
                info.PossibleClassValues = GetAllSubclasses(itemType)
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Select(t => t.Name)
                    .ToList();
            }
            else
            {
                info.Type |= XmlFieldType.SimpleClass; // List of Simple Class
            }

            // List 内部元素的 Schema
            if (!IsPrimitive(itemType))
            {
                info.SchemaId = GetOrCreateSchema(itemType);
            }
            return info;
        }

        // 2. 处理普通类型
        Type resolvedType = type; // 这里的 type 已经是 FieldType

        if (IsPrimitive(resolvedType))
        {
            info.Type = XmlFieldType.Primitive;
            info.FieldTypeName = SimplifyTypeName(resolvedType);
            return info;
        }

        if (resolvedType.IsEnum)
        {
            info.Type = XmlFieldType.Enumable;
            info.FieldTypeName = resolvedType.Name;
            info.EnumValues = Enum.GetNames(resolvedType).ToList();
            return info;
        }

        // 3. 复杂对象 (Complex Class)
        info.Type = XmlFieldType.SimpleClass; // 默认为简单类结构
        info.FieldTypeName = SimplifyTypeName(resolvedType);
        info.SchemaId = GetOrCreateSchema(resolvedType);

        return info;
    }

    /// <summary>
    /// 获取或创建 Schema ID。如果是基本类型则返回 -1。
    /// </summary>
    private int GetOrCreateSchema(Type type)
    {
        if (type == null || IsPrimitive(type) || TypeFilter.IsBannedType(type))
            return -1;

        // 检查缓存
        if (_schemaIdMap.TryGetValue(type, out int existingId))
            return existingId;

        // 预占位：防止循环引用 (Recursive definition) 在处理字段前先分配 ID 并加入 Map
        int newId = _schemaList.Count;
        _schemaIdMap[type] = newId;

        var schema = new TypeSchema { FullName = type.FullName ?? type.Name };
        // 先添加到 List 占位，虽然 Fields 还是空的，但在递归回到这里时能读到 ID
        _schemaList.Add(schema);

        // 收集字段 (不包含基类字段？通常 XML 嵌套对象不需要扁平化，除非它是 Def 的一部分。 RimWorld XML 中，
        // <compClass> ... </compClass>
        // 对应的对象通常只需要自己的字段。
        // *修正*：如果是继承结构，XML 允许配置父类字段。所以这里应该包含继承链吗？ 通常 Def 的子节点如果是复杂对象，也是扁平化配置的。 为了保险，我们收集所有
        // Public/Private 字段，包括基类的（RimWorld 行为）。

        var fields = CollectFieldsFlattened(type);
        // 注意：CollectFieldsFlattened 内部调用 CreateXmlFieldInfo -> GetOrCreateSchema 由于 _schemaIdMap
        // 已经有了 type，递归会终止。

        schema.Fields = fields;

        return newId;
    }

    #endregion Mapping & Schema

    #region Helpers & Internal Classes

    private bool IsPrimitive(Type t)
    {
        if (t.IsPrimitive) return true;
        if (t == typeof(string)) return true;
        if (t == typeof(decimal)) return true;
        if (t == typeof(DateTime)) return true;

        if (t.Namespace == "UnityEngine" && (t.Name == "Vector2" || t.Name == "Vector3" || t.Name == "Color")) return true;
        if (t.Name == "IntRange" || t.Name == "FloatRange") return true;

        return false;
    }

    private string SimplifyTypeName(Type t)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            return $"List<{SimplifyTypeName(t.GetGenericArguments()[0])}>";
        if (Nullable.GetUnderlyingType(t) is Type type)
            return type.FullName;
        return t.FullName;
    }

    private IEnumerable<Type> GetAllSubclasses(Type baseType)
    {
        if (!_subclassLookup.Contains(baseType)) return Enumerable.Empty<Type>();
        var children = _subclassLookup[baseType];
        return children.Concat(children.SelectMany(GetAllSubclasses));
    }

    private void LoadDefOfClasses()
    {
        if (_tDefOf == null) return;
        foreach (var t in _allTypes.Where(x => x.IsDefined(_tDefOf, false)))
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (_tDef != null && _tDef.IsAssignableFrom(f.FieldType))
                {
                    if (!_defOfValuesCache.ContainsKey(f.FieldType))
                        _defOfValuesCache[f.FieldType] = new List<string>();
                    _defOfValuesCache[f.FieldType].Add(f.Name);
                }
            }
        }
    }

    private void WriteDefOutput(List<DefInfo> result, string path)
    {
        // 简易输出用于 Debug
        var sb = new StringBuilder();
        foreach (var def in result)
        {
            sb.AppendLine($"{def.TagName} : {def.ParentName}");
            foreach (var item in def.Fields)
            {
                sb.AppendLine($"  -- {item.Name} : {item.FieldTypeName}");
            }
        }
        File.WriteAllText(path, sb.ToString());
    }

    private bool TryNewRawTypeInfo(Type t, out RawTypeInfo info)
    {
        info = default;
        if (TypeFilter.IsBannedType(t))
            return false;
        try
        {
            info = new RawTypeInfo(t);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private struct RawFieldInfo
    {
        public string name;
        public Type FieldType;

        public RawFieldInfo(FieldInfo fi)
        {
            name = fi.Name; FieldType = fi.FieldType;
        }
    }

    private struct RawTypeInfo
    {
        public string fullName;
        public string className;
        public Dictionary<string, RawFieldInfo> fields = new();

        public RawTypeInfo(Type t)
        {
            fullName = t.FullName ?? t.Name;
            className = t.Name;
            // 仅提取当前类的字段，基类由 Flatten 逻辑处理
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!TypeFilter.IsBannedField(f))
                    fields[f.Name] = new RawFieldInfo(f);
            }
        }
    }

    #endregion Helpers & Internal Classes
}

internal static class TypeFilter
{
    public static bool IsBannedType(Type type)
    {
        if (type == null) return true;
        if (type.IsGenericParameter) return true;
        if (type.FullName != null && (type.FullName.StartsWith("System") || type.FullName.StartsWith("UnityEngine"))) return true;
        return type.IsSpecialName || type.Name.StartsWith('<');
    }

    public static bool IsBannedField(FieldInfo field)
    {
        // 过滤掉 backing fields (<Prop>k__BackingField)
        return field.IsSpecialName || field.Name.Contains('>');
    }
}
