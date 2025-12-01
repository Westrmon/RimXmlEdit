using MessagePack;

namespace RimXmlEdit.Core.Entries;

[MessagePackObject]
public class DefCache
{
    public DefCache()
    {
    }

    public DefCache(
        List<DefInfo> defInfos,
        Dictionary<string,
            IEnumerable<string>> defOfEnums,
        List<TypeSchema> schemas,
        List<PossibleClass> comps,
        Dictionary<string, string>? defCasts = null)
    {
        DefInfos = defInfos;
        DefOfEnums = defOfEnums;
        Schemas = schemas;
        Comps = comps;
        if (defCasts != null) DefCasts = defCasts;
    }

    [Key(0)] public string TypeTag { get; set; } = "DefCache";

    [Key(1)] public List<DefInfo> DefInfos { get; set; }

    [Key(2)] public Dictionary<string, IEnumerable<string>> DefOfEnums { get; set; }

    [Key(3)] public List<TypeSchema> Schemas { get; set; } = new();

    [Key(4)] public Dictionary<string, string> DefCasts { get; set; }

    /// <summary>
    ///     存储所有多态类型的类信息（如 Component, Verbs 等），
    ///     XmlFieldInfo 中的 PossibleClassValues 存储的是此列表的索引。
    /// </summary>
    [Key(5)]
    public List<PossibleClass> Comps { get; set; } = new();

    /// <summary>
    ///     将另一个 Cache 合并到当前 Cache 之后，并返回一个新的 DefCache 实例。 原有的两个对象不会被修改。
    /// </summary>
    public DefCache Concat(DefCache other)
    {
        if (other == null) return this;
        var newCache = new DefCache
        {
            TypeTag = TypeTag,
            DefInfos = new List<DefInfo>(DefInfos),
            DefOfEnums = new Dictionary<string, IEnumerable<string>>(DefOfEnums),
            Schemas = new List<TypeSchema>(Schemas),
            Comps = new List<PossibleClass>(Comps),
            DefCasts = new Dictionary<string, string>(DefCasts)
        };

        newCache.MergeWith(other);

        return newCache;
    }

    /// <summary>
    ///     将另一个 Cache 的数据合并入当前实例。
    /// </summary>
    public void MergeWith(DefCache other)
    {
        if (other == null) return;

        // 1. Merge Schemas
        var schemaMap = new Dictionary<int, int>();
        var existingSchemas = new Dictionary<string, List<int>>();
        for (var i = 0; i < Schemas.Count; i++)
        {
            var name = Schemas[i].FullName;
            if (!existingSchemas.TryGetValue(name, out var list))
            {
                list = new List<int>();
                existingSchemas[name] = list;
            }

            list.Add(i);
        }

        var newSchemasToAdd = new List<(TypeSchema Schema, int OriginalId)>();

        for (var i = 0; i < other.Schemas.Count; i++)
        {
            var otherSchema = other.Schemas[i];
            var matchedIndex = -1;

            if (existingSchemas.TryGetValue(otherSchema.FullName, out var indices))
                foreach (var idx in indices)
                {
                    TypeSchema candidate;
                    if (idx < Schemas.Count)
                        candidate = Schemas[idx];
                    else
                        candidate = newSchemasToAdd[idx - Schemas.Count].Schema;
                    if (IsSchemaEqual(candidate, otherSchema))
                    {
                        matchedIndex = idx;
                        break;
                    }
                }

            if (matchedIndex != -1)
            {
                schemaMap[i] = matchedIndex;
            }
            else
            {
                var newIndex = Schemas.Count + newSchemasToAdd.Count;
                schemaMap[i] = newIndex;

                var clonedSchema = new TypeSchema
                {
                    FullName = otherSchema.FullName,
                    Fields = CloneFields(otherSchema.Fields) // 初步Clone，稍后Remap
                };
                newSchemasToAdd.Add((clonedSchema, i));
                if (!existingSchemas.TryGetValue(otherSchema.FullName, out var list))
                {
                    list = new List<int>();
                    existingSchemas[otherSchema.FullName] = list;
                }

                list.Add(newIndex);
            }
        }

        // 2. Merge Comps (PossibleClasses)
        var compMap = new Dictionary<int, int>();
        var existingComps = new Dictionary<string, int>();
        for (var i = 0; i < Comps.Count; i++)
        {
            var name = Comps[i].FullName;
            existingComps.TryAdd(name, i);
        }

        for (var i = 0; i < other.Comps.Count; i++)
        {
            var otherComp = other.Comps[i];
            if (existingComps.TryGetValue(otherComp.FullName, out var existingIndex))
            {
                compMap[i] = existingIndex;
            }
            else
            {
                var newIndex = Comps.Count;
                compMap[i] = newIndex;

                // 需要重新映射 SchemaId
                var mappedSchemaId = otherComp.SchemaId;
                if (mappedSchemaId >= 0 && schemaMap.TryGetValue(mappedSchemaId, out var newSchemaId))
                    mappedSchemaId = newSchemaId;

                var newComp = new PossibleClass
                {
                    FullName = otherComp.FullName,
                    SchemaId = mappedSchemaId,
                    IsThingComp = otherComp.IsThingComp,
                    Fields = CloneFields(otherComp.Fields)
                };

                Comps.Add(newComp);
                existingComps[newComp.FullName] = newIndex;
            }
        }

        // 3. Apply Schema Remap & Comp Remap to new Schemas
        foreach (var item in newSchemasToAdd)
        {
            RemapFieldsInPlace(item.Schema.Fields, schemaMap, compMap);
            Schemas.Add(item.Schema);
        }

        // 4. Merge DefInfos
        foreach (var def in other.DefInfos)
        {
            var newDef = new DefInfo
            {
                TagName = def.TagName,
                Name = def.Name,
                ParentName = def.ParentName,
                IsAbstract = def.IsAbstract,
                IgnoreConfigErrors = def.IgnoreConfigErrors,
                Ref = def.Ref,
                Value = def.Value,
                FullName = def.FullName,
                EnumValues = def.EnumValues,

                Fields = CloneAndRemapFields(def.Fields, schemaMap, compMap)
            };
            DefInfos.Add(newDef);
        }

        // 5. Merge Enums & Casts
        foreach (var kvp in other.DefOfEnums)
            if (!DefOfEnums.ContainsKey(kvp.Key))
                DefOfEnums[kvp.Key] = kvp.Value;
            else
                DefOfEnums[kvp.Key] = DefOfEnums[kvp.Key].Concat(kvp.Value).Distinct().ToList();

        if (other.DefCasts != null)
            foreach (var kvp in other.DefCasts)
                DefCasts[kvp.Key] = kvp.Value;
    }

    private bool IsSchemaEqual(TypeSchema a, TypeSchema b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        if (a.FullName != b.FullName) return false;

        return AreFieldsEqual(a.Fields, b.Fields);
    }

    private bool AreFieldsEqual(List<XmlFieldInfo> a, List<XmlFieldInfo> b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        for (var i = 0; i < a.Count; i++)
        {
            var f1 = a[i];
            var f2 = b[i];

            if (f1.Name != f2.Name) return false;
            if (f1.FieldTypeName != f2.FieldTypeName) return false;
            if (f1.Type != f2.Type) return false;
            if (f1.Value != f2.Value) return false;
            if (f1.Ref != f2.Ref) return false;

            // 比较集合
            if (!AreStringListsEqual(f1.EnumValues, f2.EnumValues)) return false;

            // 比较 PossibleClassValues (List<int>)
            if (!AreIntListsEqual(f1.PossibleClassValues, f2.PossibleClassValues)) return false;

            // 递归比较子字段
            if (!AreFieldsEqual(f1.Children, f2.Children)) return false;
        }

        return true;
    }

    private bool AreStringListsEqual(List<string> a, List<string> b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.SequenceEqual(b);
    }

    private bool AreIntListsEqual(List<int> a, List<int> b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.SequenceEqual(b);
    }

    private List<XmlFieldInfo> CloneAndRemapFields(List<XmlFieldInfo> fields, Dictionary<int, int> schemaMap,
        Dictionary<int, int>? compMap)
    {
        if (fields == null) return new List<XmlFieldInfo>();
        var list = new List<XmlFieldInfo>(fields.Count);
        foreach (var f in fields) list.Add(CloneAndRemap(f, schemaMap, compMap));
        return list;
    }

    private XmlFieldInfo CloneAndRemap(XmlFieldInfo f, Dictionary<int, int> schemaMap, Dictionary<int, int>? compMap)
    {
        var newField = new XmlFieldInfo
        {
            Name = f.Name,
            FieldTypeName = f.FieldTypeName,
            Type = f.Type,
            EnumValues = f.EnumValues,
            PossibleClassValues = null, // 下面处理
            Value = f.Value,
            Ref = f.Ref,
            IsHaveTranslationHandle = f.IsHaveTranslationHandle,
            MustTranslate = f.MustTranslate,
            SchemaId = f.SchemaId >= 0 && schemaMap.TryGetValue(f.SchemaId, out var newId) ? newId : f.SchemaId
        };

        if (f.PossibleClassValues != null)
        {
            newField.PossibleClassValues = new List<int>();
            foreach (var pcIndex in f.PossibleClassValues)
                if (compMap != null && compMap.TryGetValue(pcIndex, out var mappedIndex))
                    newField.PossibleClassValues.Add(mappedIndex);
                else
                    newField.PossibleClassValues.Add(pcIndex);
        }

        if (f.Children != null && f.Children.Count > 0)
            newField.Children = CloneAndRemapFields(f.Children, schemaMap, compMap);

        return newField;
    }

    private List<XmlFieldInfo> CloneFields(List<XmlFieldInfo> fields)
    {
        if (fields == null) return new List<XmlFieldInfo>();
        return fields.Select(f => new XmlFieldInfo
        {
            Name = f.Name,
            FieldTypeName = f.FieldTypeName,
            Type = f.Type,
            EnumValues = f.EnumValues,
            PossibleClassValues = f.PossibleClassValues?.ToList(), // List<int> copy
            Value = f.Value,
            Ref = f.Ref,
            IsHaveTranslationHandle = f.IsHaveTranslationHandle,
            MustTranslate = f.MustTranslate,
            SchemaId = f.SchemaId,
            Children = f.Children != null ? CloneFields(f.Children) : null
        }).ToList();
    }

    private void RemapFieldsInPlace(List<XmlFieldInfo> fields, Dictionary<int, int> schemaMap,
        Dictionary<int, int>? compMap)
    {
        if (fields == null) return;
        foreach (var f in fields)
        {
            if (f.SchemaId >= 0 && schemaMap.TryGetValue(f.SchemaId, out var newId))
                f.SchemaId = newId;

            if (f.PossibleClassValues != null && compMap != null)
                for (var i = 0; i < f.PossibleClassValues.Count; i++)
                    if (compMap.TryGetValue(f.PossibleClassValues[i], out var mappedId))
                        f.PossibleClassValues[i] = mappedId;

            if (f.Children != null)
                RemapFieldsInPlace(f.Children, schemaMap, compMap);
        }
    }
}