using System.Reflection;
using System.Text;
using MessagePack;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Core.Parse;

public class DefParser
{
    private readonly ILogger _log;

    private string _dllPath;
    private bool _isFirst;
    private Type? _mustTransAttr;
    private RawTypeCache _rawTypeCache;
    private readonly AssemblyScanner _scanner;
    private SchemaBuilder _schemaBuilder;

    // Core Types Cache
    private Type? _tDef;
    private Type? _tDefOf;
    private Type? _transAttr;

    public DefParser(string? dllPath = null)
    {
        _log = this.Log();
        var finalDllPath = dllPath ??
                           Path.Combine(TempConfig.GamePath, "RimWorldWin64_Data", "Managed", "Assembly-CSharp.dll");
        _dllPath = finalDllPath;
        _isFirst = true;
        _scanner = new AssemblyScanner(_log);
    }

    public bool Init(string? dllPath = null)
    {
        if (!_isFirst && string.IsNullOrEmpty(dllPath)) return false;
        _isFirst = false;
        if (dllPath != null)
            _dllPath = dllPath;
        if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath)) return false;

        try
        {
            if (!_scanner.Load(_dllPath)) return false;
            IdentifyCoreTypes();
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize parser for: {Path}", dllPath);
            return false;
        }
    }

    private void IdentifyCoreTypes()
    {
        var verseAssembly = _scanner.CoreAssembly;
        _tDef = verseAssembly.GetType("Verse.Def");
        _tDefOf = verseAssembly.GetType("RimWorld.DefOf");
        _transAttr = verseAssembly.GetType("Verse.TranslationHandleAttribute");
        _mustTransAttr = verseAssembly.GetType("Verse.MustTranslateAttribute");
        _scanner.GetCompsBaseType();
    }

    public DefCache Parse(bool forceSave = false, string? defOutputPath = null, bool setPrefixDef = false)
    {
        var cachePath = Path.Combine(TempConfig.AppPath, "cache",
            Path.GetFileNameWithoutExtension(_dllPath) + "Cache.bin");

        if (File.Exists(cachePath) && !forceSave) return LoadFromCache(cachePath);

        return ParseFresh(cachePath, defOutputPath, setPrefixDef);
    }

    private DefCache ParseFresh(string cachePath, string? defOutputPath, bool setPrefixDef)
    {
        _log.LogNotify("Starting fresh parse for {Dll}", Path.GetFileName(_dllPath));

        _schemaBuilder = new SchemaBuilder();
        _rawTypeCache = new RawTypeCache();
        var defOfMap = ExtractDefOfs();
        PerformDeepTypeScan();
        RegisterComps();
        var defCasts = AnalyzeDefCasts();
        var defInfos = GenerateDefInfos(setPrefixDef);
        var cache = new DefCache(
            defInfos,
            defOfMap,
            _schemaBuilder.GetSchemas(),
            _schemaBuilder.GetPolyClasses(),
            defCasts
        );

        SaveCache(cache, cachePath);
        if (!string.IsNullOrEmpty(defOutputPath)) WriteDebugOutput(defInfos, defOutputPath);

        _log.LogNotify("Parse complete. Defs: {Count}", defInfos.Count);
        return cache;
    }

    private DefCache LoadFromCache(string cachePath)
    {
        try
        {
            using var fs = File.OpenRead(cachePath);
            var cache = MessagePackSerializer.Deserialize<DefCache>(fs);
            _schemaBuilder = new SchemaBuilder();
            _schemaBuilder.LoadFromCache(cache.Schemas, cache.Comps);

            _log.LogNotify("Loaded from cache: {Name}", Path.GetFileNameWithoutExtension(_dllPath));
            return cache;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cache corrupted, parsing fresh.");
            return ParseFresh(cachePath, null, false);
        }
    }

    #region Extraction Logic

    private Dictionary<string, IEnumerable<string>> ExtractDefOfs()
    {
        var result = new Dictionary<string, IEnumerable<string>>();
        if (_tDefOf == null || _tDef == null) return result;
        foreach (var type in _scanner.AllTypes)
        {
            if (!type.IsDefined(_tDefOf, false)) continue;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => _tDef.IsAssignableFrom(f.FieldType))
                .Select(f => f.Name)
                .ToList();

            if (fields.Count <= 0) continue;

            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!_tDef.IsAssignableFrom(f.FieldType)) continue;

                var typeKey = f.FieldType.FullName ?? f.FieldType.Name;
                if (!result.ContainsKey(typeKey))
                    result[typeKey] = new List<string>();
                ((List<string>)result[typeKey]).Add(f.Name);
            }
        }

        return result;
    }

    private void RegisterComps()
    {
        var baseComps = _scanner.GetCompsBaseType().ToList();

        foreach (var type in _scanner.AllTypes.Where(type => type is { IsAbstract: false, IsInterface: false } 
                                                             && !TypeHelper.IsBannedType(type))
                     .Where(type => baseComps.Any(type.IsSubclassOf)))
        {
            _schemaBuilder.GetOrCreatePolyClassId(type, t => CollectAndMapFields(t));
            
        }
    }

    private void PerformDeepTypeScan()
    {
        if (_tDef == null) return;

        // 种子节点：所有的 Def 子类 + CompProperties
        var seeds = _scanner.AllTypes.Where(t => t.Name.StartsWith("CompProperties") && !TypeHelper.IsBannedType(t)).ToList();
        var seeds2 = _scanner.AllTypes
            .Where(t => t.IsSubclassOf(_tDef) && !TypeHelper.IsBannedType(t)).ToList();
        seeds2.AddRange(seeds);
        var queue = new Queue<Type>(seeds2);
        var visited = new HashSet<Type>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            if (!_rawTypeCache.TryGet(current, out var rawInfo))
            {
                rawInfo = new RawTypeInfo(current);
                _rawTypeCache.Add(current, rawInfo);
            }

            foreach (var finalType in rawInfo.ReferencedTypes.Select(TypeHelper.UnwrapListType)
                         .Where(finalType => !visited.Contains(finalType)
                                             && !TypeHelper.IsPrimitive(finalType)
                                             && !TypeHelper.IsBannedType(finalType)))
                queue.Enqueue(finalType);
        }

        foreach (var item in seeds)
        {
            _schemaBuilder.GetOrCreatePolyClassId(item, t => CollectAndMapFields(t));
        }
    }

    private Dictionary<string, string> AnalyzeDefCasts()
    {
        var casts = new Dictionary<string, string>();
        if (_tDef == null) return casts;

        foreach (var type in _scanner.AllTypes)
        {
            if (TypeHelper.IsBannedType(type) || TypeHelper.IsPrimitive(type)) continue;

            var defFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => !TypeHelper.IsBannedField(f) && f.FieldType.IsSubclassOf(_tDef))
                .ToList();

            if (defFields.Count == 1)
                casts[type.FullName ?? type.Name] = defFields[0].FieldType.FullName ?? defFields[0].FieldType.Name;
        }
        return casts;
    }

    private List<DefInfo> GenerateDefInfos(bool setPrefixDef)
    {
        var result = new List<DefInfo>();
        var defTypes =
            _scanner.AllTypes.Where(t => _tDef != null && t.IsSubclassOf(_tDef) && !TypeHelper.IsBannedType(t));

        foreach (var defType in defTypes)
        {
            if (!_rawTypeCache.TryGet(defType, out var rawInfo))
                rawInfo = new RawTypeInfo(defType);

            var defInfo = new DefInfo
            {
                TagName = setPrefixDef ? rawInfo.FullName : rawInfo.ClassName,
                IsAbstract = defType.IsAbstract,
                FullName = rawInfo.FullName,
                Name = defType.Name,
                ParentName = defType.BaseType?.Name ?? ""
            };
            defInfo.Fields = CollectAndMapFields(defType);
            result.Add(defInfo);
        }

        return result.OrderBy(d => d.TagName).ToList();
    }

    private List<XmlFieldInfo> CollectAndMapFields(Type startType)
    {
        var fields = new List<XmlFieldInfo>();
        var processedNames = new HashSet<string>();
        var current = startType;

        while (current != null && current != typeof(object))
        {
            if (!_rawTypeCache.TryGet(current, out var rawInfo))
                rawInfo = new RawTypeInfo(current);

            foreach (var field in rawInfo.Fields)
                if (processedNames.Add(field.Name))
                    fields.Add(CreateFieldInfo(field.Name, field.FieldType, field.Info));

            foreach (var prop in rawInfo.Properties)
                if (processedNames.Add(prop.Name))
                    fields.Add(CreateFieldInfo(prop.Name, prop.PropertyType, null, prop.Info));

            current = current.BaseType;
        }

        return fields;
    }

    private XmlFieldInfo CreateFieldInfo(string name, Type type, FieldInfo? fi = null, PropertyInfo? pi = null)
    {
        var info = new XmlFieldInfo { Name = name };

        if (fi != null)
        {
            if (_transAttr != null && Attribute.IsDefined(fi, _transAttr)) info.IsHaveTranslationHandle = true;
            if (_mustTransAttr != null && Attribute.IsDefined(fi, _mustTransAttr)) info.MustTranslate = true;
        }

        if (TypeHelper.IsList(type, out var itemType))
        {
            info.FieldTypeName = $"List<{TypeHelper.GetSimplifiedName(itemType)}>";

            if (IsPolymorphic(itemType))
            {
                info.Type |= XmlFieldType.PolymorphicList;
                info.PossibleClassValues = GetPolymorphicIds(itemType);
            }
            else
            {
                info.Type |= XmlFieldType.SimpleClass;
            }

            if (!TypeHelper.IsPrimitive(itemType))
                info.SchemaId = GetSchemaId(itemType);

            return info;
        }

        info.FieldTypeName = TypeHelper.GetSimplifiedName(type);

        if (TypeHelper.IsPrimitive(type))
        {
            info.Type = XmlFieldType.Primitive;
        }
        else if (type.IsEnum)
        {
            info.Type = XmlFieldType.Enumable;
            info.EnumValues = Enum.GetNames(type).ToList();
        }
        else
        {
            info.Type = XmlFieldType.SimpleClass;
            info.SchemaId = GetSchemaId(type);
        }

        return info;
    }

    private bool IsPolymorphic(Type type)
    {
        return type.IsAbstract || type.IsInterface || _scanner.HasSubclasses(type);
    }

    private List<int> GetPolymorphicIds(Type baseType)
    {
        var list = new List<int>();
        foreach (var sub in _scanner.GetAllSubclasses(baseType))
            if (sub is { IsAbstract: false, IsInterface: false })
                list.Add(_schemaBuilder.GetOrCreatePolyClassId(sub, t => CollectAndMapFields(t)));

        return list;
    }

    private int GetSchemaId(Type type)
    {
        return _schemaBuilder.GetOrCreateSchemaId(type, t => CollectAndMapFields(t));
    }

    private void SaveCache(DefCache cache, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        MessagePackSerializer.Serialize(fs, cache);
    }

    private void WriteDebugOutput(List<DefInfo> result, string path)
    {
        var sb = new StringBuilder();
        foreach (var def in result)
        {
            sb.AppendLine($"{def.TagName} : {def.ParentName}");
            foreach (var item in def.Fields) sb.AppendLine($"  -- {item.Name} ({item.FieldTypeName})");
        }

        File.WriteAllText(path, sb.ToString());
    }

    #endregion
}

#region Helper Components

public class AssemblyScanner
{
    private readonly ILogger _log;
    private ILookup<Type, Type> _subclassLookup;

    public AssemblyScanner(ILogger log)
    {
        _log = log;
    }

    public Assembly CoreAssembly { get; private set; }
    public Assembly TargetAssembly { get; private set; }
    public List<Type> AllTypes { get; private set; } = new();
    public IEnumerable<Type>? CompsBaseType { get; private set; }

    public void SetAssembly(Assembly assembly)
    {
        if (assembly.GetName().Name == "Assembly-CSharp")
            CoreAssembly = assembly;
        TargetAssembly = assembly;
    }

    public bool Load(string path)
    {
        try
        {
            SetAssembly(Assembly.LoadFrom(path));
            AllTypes = TargetAssembly.GetTypes().ToList();
            _subclassLookup = AllTypes.Where(t => t.BaseType != null).ToLookup(t => t.BaseType!);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Assembly load failed.");
            return false;
        }
    }

    public IEnumerable<Type> GetCompsBaseType()
    {
        if (CompsBaseType != null) return  CompsBaseType;
        CompsBaseType = AllTypes.Where(t => t.IsAbstract 
                                            && t.BaseType == typeof(object) 
                                            && t.Name.EndsWith("Comp"));
        return CompsBaseType;
    }

    public bool HasSubclasses(Type t)
    {
        return _subclassLookup.Contains(t);
    }

    public IEnumerable<Type> GetAllSubclasses(Type baseType)
    {
        if (!_subclassLookup.Contains(baseType)) return Enumerable.Empty<Type>();
        var direct = _subclassLookup[baseType];
        return direct.Concat(direct.SelectMany(GetAllSubclasses));
    }
}

public class SchemaBuilder
{
    private readonly List<PossibleClass> _polyClasses = new();
    private readonly Dictionary<string, int> _polyClassIndexMap = new();
    private readonly Dictionary<Type, int> _schemaIdMap = new();
    private readonly List<TypeSchema> _schemas = new();

    public List<TypeSchema> GetSchemas()
    {
        return _schemas;
    }

    public List<PossibleClass> GetPolyClasses()
    {
        return _polyClasses;
    }

    public void LoadFromCache(List<TypeSchema>? schemas, List<PossibleClass>? comps)
    {
        if (schemas != null) _schemas.AddRange(schemas);
        if (comps != null) _polyClasses.AddRange(comps);
    }

    public int GetOrCreateSchemaId(Type type, Func<Type, List<XmlFieldInfo>> fieldResolver)
    {
        if (TypeHelper.IsPrimitive(type) || TypeHelper.IsBannedType(type)) return -1;
        if (_schemaIdMap.TryGetValue(type, out var id)) return id;

        id = _schemas.Count;
        _schemaIdMap[type] = id;
        var schema = new TypeSchema { FullName = type.FullName ?? type.Name };
        _schemas.Add(schema);

        schema.Fields = fieldResolver(type);

        return id;
    }

    public int GetOrCreatePolyClassId(Type type, Func<Type, List<XmlFieldInfo>> fieldResolver)
    {
        // var name = type.Name.Replace("Properties_", "");
        var name = type.Name;
        if (_polyClassIndexMap.TryGetValue(name, out var id)) return id;

        id = _polyClasses.Count;
        var pc = new PossibleClass { FullName = name, SchemaId = -1 };

        if (_schemaIdMap.TryGetValue(type, out var sid)) pc.SchemaId = sid;

        _polyClasses.Add(pc);
        _polyClassIndexMap[name] = id;
        
        try 
        {
            pc.Fields = fieldResolver(type);
        }
        catch
        {
            pc.Fields = new List<XmlFieldInfo>();
        }
        
        return id;
    }
}

/// <summary>
///     反射信息缓存，避免重复调用 GetFields
/// </summary>
public class RawTypeCache
{
    private readonly Dictionary<Type, RawTypeInfo> _dict = new();

    public void Add(Type t, RawTypeInfo info)
    {
        _dict[t] = info;
    }

    public bool TryGet(Type t, out RawTypeInfo info)
    {
        return _dict.TryGetValue(t, out info);
    }
}

public class RawTypeInfo
{
    public RawTypeInfo(Type t)
    {
        FullName = t.FullName ?? t.Name;
        ClassName = t.Name;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                   BindingFlags.DeclaredOnly;

        foreach (var f in t.GetFields(flags))
        {
            if (TypeHelper.IsBannedField(f)) continue;
            Fields.Add(new FieldEntry(f));
            ReferencedTypes.Add(f.FieldType);
        }

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            if (p.CanWrite && p.GetMethod != null && p.GetIndexParameters().Length == 0)
            {
                Properties.Add(new PropertyEntry(p));
                ReferencedTypes.Add(p.PropertyType);
            }
    }

    public string FullName { get; }
    public string ClassName { get; }
    public List<FieldEntry> Fields { get; } = new();
    public List<PropertyEntry> Properties { get; } = new();
    public HashSet<Type> ReferencedTypes { get; } = new();

    public record struct FieldEntry(FieldInfo Info)
    {
        public string Name => Info.Name;
        public Type FieldType => Info.FieldType;
    }

    public record struct PropertyEntry(PropertyInfo Info)
    {
        public string Name => Info.Name;
        public Type PropertyType => Info.PropertyType;
    }
}

public static class TypeHelper
{
    public static bool IsList(Type t, out Type itemType)
    {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
        {
            itemType = t.GetGenericArguments()[0];
            return true;
        }

        itemType = null!;
        return false;
    }

    public static Type UnwrapListType(Type t)
    {
        return IsList(t, out var item) ? item : t;
    }

    public static bool IsPrimitive(Type t)
    {
        if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime)) return true;
        // Fast check for Unity types by namespace or name
        if (t.Namespace == "UnityEngine" &&
            (t.Name == "Vector2" || t.Name == "Vector3" || t.Name == "Color")) return true;
        if (t.Name == "IntRange" || t.Name == "FloatRange") return true;
        return false;
    }

    public static bool IsBannedType(Type t)
    {
        if (t == null || t.IsGenericParameter) return true;
        if (t.FullName != null && (t.FullName.StartsWith("System") || t.FullName.StartsWith("UnityEngine")))
            return true;
        return t.IsSpecialName || t.Name.StartsWith('<');
    }

    public static bool IsBannedField(FieldInfo f)
    {
        return f.IsSpecialName || f.Name.Contains('>');
    }

    public static string GetSimplifiedName(Type t)
    {
        if (IsList(t, out var item)) return $"List<{GetSimplifiedName(item)}>";
        return Nullable.GetUnderlyingType(t)?.FullName ?? t.FullName ?? t.Name;
    }
}

#endregion