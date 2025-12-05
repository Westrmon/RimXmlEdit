using MessagePack;

namespace RimXmlEdit.Core.NodeDefine;

[MessagePackObject(AllowPrivate = true)]
public class NodeDefinitionDatabase
{
    [IgnoreMember] private readonly object _lock = new();

    [IgnoreMember] private Dictionary<string, int> _reversePaletteLookup;

    public NodeDefinitionDatabase()
    {
        RebuildReverseLookup();
    }

    [Key(0)] public List<string> DescriptionPalette { get; set; } = new() { "" };

    [Key(1)] public Dictionary<string, int> NodeMap { get; set; } = new();

    public void RebuildReverseLookup()
    {
        _reversePaletteLookup = new Dictionary<string, int>(DescriptionPalette.Count);
        for (var i = 0; i < DescriptionPalette.Count; i++)
            if (!_reversePaletteLookup.ContainsKey(DescriptionPalette[i]))
                _reversePaletteLookup[DescriptionPalette[i]] = i;
    }

    public string GetDescription(string nodePath)
    {
        if (NodeMap.TryGetValue(nodePath, out var index))
            if (index >= 0 && index < DescriptionPalette.Count)
                return DescriptionPalette[index];
        return string.Empty;
    }

    public void SetDescription(string nodePath, string description)
    {
        if (description == null) description = string.Empty;

        lock (_lock)
        {
            if (!_reversePaletteLookup.TryGetValue(description, out var index))
            {
                index = DescriptionPalette.Count;
                DescriptionPalette.Add(description);
                _reversePaletteLookup[description] = index;
            }

            NodeMap[nodePath] = index;
        }
    }

    /// <summary>
    ///     核心功能：将另一个数据库合并入当前库
    /// </summary>
    /// <param name="other"> 要合并的数据库（如朋友分享的模组定义） </param>
    /// <param name="overwrite"> 如果节点已存在，是否覆盖描述 </param>
    public void MergeWith(NodeDefinitionDatabase other, bool overwrite = false)
    {
        if (other == null || other.NodeMap == null) return;

        lock (_lock)
        {
            if (_reversePaletteLookup == null) RebuildReverseLookup();

            foreach (var kvp in other.NodeMap)
            {
                var nodeName = kvp.Key;
                var otherIndex = kvp.Value;

                var desc = string.Empty;
                if (otherIndex >= 0 && otherIndex < other.DescriptionPalette.Count)
                    desc = other.DescriptionPalette[otherIndex];

                var exists = NodeMap.ContainsKey(nodeName);
                var shouldUpdate = false;

                if (!exists)
                {
                    shouldUpdate = true;
                }
                else
                {
                    if (overwrite)
                        if (!string.IsNullOrEmpty(desc))
                            shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    // 添加到描述池并更新索引
                    if (!_reversePaletteLookup.TryGetValue(desc, out var myIndex))
                    {
                        myIndex = DescriptionPalette.Count;
                        DescriptionPalette.Add(desc);
                        _reversePaletteLookup[desc] = myIndex;
                    }

                    NodeMap[nodeName] = myIndex;
                }
            }
        }
    }

    public Task SaveToFileAsync(string path)
    {
        return Task.Run(() =>
        {
            var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            MessagePackSerializer.Serialize(fs, this, options);
        });
    }

    public static NodeDefinitionDatabase LoadFromFile(string path)
    {
        if (!File.Exists(path)) return new NodeDefinitionDatabase();
        try
        {
            var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var db = MessagePackSerializer.Deserialize<NodeDefinitionDatabase>(fs, options);
            db.RebuildReverseLookup();
            return db;
        }
        catch
        {
            return new NodeDefinitionDatabase();
        }
    }

    public void BatchUpdate(Dictionary<string, string> newDescriptions)
    {
        if (newDescriptions == null || newDescriptions.Count == 0) return;

        lock (_lock)
        {
            foreach (var kvp in newDescriptions)
            {
                var nodePath = kvp.Key;
                var description = kvp.Value ?? string.Empty;
                if (!_reversePaletteLookup.TryGetValue(description, out var index))
                {
                    index = DescriptionPalette.Count;
                    DescriptionPalette.Add(description);
                    _reversePaletteLookup[description] = index;
                }

                NodeMap[nodePath] = index;
            }
        }
    }
}