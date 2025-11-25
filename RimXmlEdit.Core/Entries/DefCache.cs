using MessagePack;

namespace RimXmlEdit.Core.Entries;

[MessagePackObject(keyAsPropertyName: false)]
public class DefCache
{
    [Key(0)]
    public string TypeTag { get; set; } = "DefCache";

    [Key(1)]
    public List<DefInfo> DefInfos { get; set; }

    [Key(2)]
    public Dictionary<string, IEnumerable<string>> DefOfEnums { get; set; }

    [Key(3)]
    public List<TypeSchema> Schemas { get; set; } = new();

    [Key(4)]
    public Dictionary<string, string> DefCasts { get; set; }

    public DefCache()
    {
    }

    public DefCache(
        List<DefInfo> defInfos,
        Dictionary<string,
        IEnumerable<string>> defOfEnums,
        List<TypeSchema> schemas,
        Dictionary<string, string>? defCasts = null)
    {
        DefInfos = defInfos;
        DefOfEnums = defOfEnums;
        Schemas = schemas;
        if (defCasts != null) DefCasts = defCasts;
    }

    /// <summary>
    /// 将另一个 Cache 合并到当前 Cache 之后，并返回一个新的 DefCache 实例。 原有的两个对象不会被修改。
    /// </summary>
    public DefCache Concat(DefCache other)
    {
        if (other == null) return this;
        var newCache = new DefCache
        {
            TypeTag = this.TypeTag,
            DefInfos = new List<DefInfo>(this.DefInfos),
            DefOfEnums = new Dictionary<string, IEnumerable<string>>(this.DefOfEnums),
            Schemas = new List<TypeSchema>(this.Schemas),
            DefCasts = new Dictionary<string, string>(this.DefCasts)
        };

        newCache.MergeWith(other);

        return newCache;
    }

    /// <summary>
    /// 将另一个 Cache 的数据合并入当前实例。
    /// </summary>
    public void MergeWith(DefCache other)
    {
        if (other == null) return;

        var schemaMap = new Dictionary<int, int>();

        var existingSchemas = new Dictionary<string, int>();
        for (int i = 0; i < this.Schemas.Count; i++)
        {
            existingSchemas[this.Schemas[i].FullName] = i;
        }

        var newSchemasToAdd = new List<(TypeSchema Schema, int OriginalId)>();

        for (int i = 0; i < other.Schemas.Count; i++)
        {
            var otherSchema = other.Schemas[i];
            if (existingSchemas.TryGetValue(otherSchema.FullName, out int existingIndex))
            {
                schemaMap[i] = existingIndex;
            }
            else
            {
                int newIndex = this.Schemas.Count + newSchemasToAdd.Count;
                schemaMap[i] = newIndex;

                var clonedSchema = new TypeSchema
                {
                    FullName = otherSchema.FullName,
                    Fields = CloneFields(otherSchema.Fields)
                };
                newSchemasToAdd.Add((clonedSchema, i));
            }
        }

        foreach (var item in newSchemasToAdd)
        {
            RemapFieldsInPlace(item.Schema.Fields, schemaMap);
            this.Schemas.Add(item.Schema);
        }

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

                Fields = CloneAndRemapFields(def.Fields, schemaMap)
            };
            this.DefInfos.Add(newDef);
        }
        foreach (var kvp in other.DefOfEnums)
        {
            if (!this.DefOfEnums.ContainsKey(kvp.Key))
            {
                this.DefOfEnums[kvp.Key] = kvp.Value;
            }
            else
            {
                this.DefOfEnums[kvp.Key] = this.DefOfEnums[kvp.Key].Concat(kvp.Value).Distinct().ToList();
            }
        }
        if (other.DefCasts != null)
        {
            foreach (var kvp in other.DefCasts)
            {
                this.DefCasts[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// 深拷贝字段列表，并同时应用 ID 映射
    /// </summary>
    private List<XmlFieldInfo> CloneAndRemapFields(List<XmlFieldInfo> fields, Dictionary<int, int> idMap)
    {
        if (fields == null) return new List<XmlFieldInfo>();
        var list = new List<XmlFieldInfo>(fields.Count);
        foreach (var f in fields)
        {
            list.Add(CloneAndRemap(f, idMap));
        }
        return list;
    }

    private XmlFieldInfo CloneAndRemap(XmlFieldInfo f, Dictionary<int, int> idMap)
    {
        var newField = new XmlFieldInfo
        {
            Name = f.Name,
            FieldTypeName = f.FieldTypeName,
            Type = f.Type,
            EnumValues = f.EnumValues,
            PossibleClassValues = f.PossibleClassValues,
            Value = f.Value,
            Ref = f.Ref,
            // 核心：重映射 ID
            SchemaId = (f.SchemaId >= 0 && idMap.TryGetValue(f.SchemaId, out int newId)) ? newId : -1
        };
        if (f.Children != null && f.Children.Count > 0)
        {
            newField.Children = CloneAndRemapFields(f.Children, idMap);
        }

        return newField;
    }

    /// <summary>
    /// 仅深拷贝字段列表（不改变 ID），用于 Schema 的初步复制
    /// </summary>
    private List<XmlFieldInfo> CloneFields(List<XmlFieldInfo> fields)
    {
        if (fields == null) return new List<XmlFieldInfo>();
        return fields.Select(f => new XmlFieldInfo
        {
            Name = f.Name,
            FieldTypeName = f.FieldTypeName,
            Type = f.Type,
            EnumValues = f.EnumValues,
            PossibleClassValues = f.PossibleClassValues,
            Value = f.Value,
            Ref = f.Ref,
            SchemaId = f.SchemaId,
            Children = (f.Children != null) ? CloneFields(f.Children) : null
        }).ToList();
    }

    /// <summary>
    /// 原地修改字段列表的 ID (用于新创建的 Schema 对象)
    /// </summary>
    private void RemapFieldsInPlace(List<XmlFieldInfo> fields, Dictionary<int, int> idMap)
    {
        if (fields == null) return;
        foreach (var f in fields)
        {
            if (f.SchemaId >= 0 && idMap.TryGetValue(f.SchemaId, out int newId))
            {
                f.SchemaId = newId;
            }
            if (f.Children != null)
            {
                RemapFieldsInPlace(f.Children, idMap);
            }
        }
    }
}
