using MessagePack;

namespace RimXmlEdit.Core;

[MessagePackObject(AllowPrivate = true)]
public class NodeDefinitionDatabase
{
    [Key(0)]
    public List<string> DescriptionPalette { get; set; } = new List<string> { "" };

    [Key(1)]
    public Dictionary<string, int> NodeMap { get; set; } = new Dictionary<string, int>();

    [IgnoreMember]
    private Dictionary<string, int> _reversePaletteLookup;

    [IgnoreMember]
    private readonly object _lock = new object();

    public NodeDefinitionDatabase()
    {
        RebuildReverseLookup();
    }

    public void RebuildReverseLookup()
    {
        _reversePaletteLookup = new Dictionary<string, int>(DescriptionPalette.Count);
        for (int i = 0; i < DescriptionPalette.Count; i++)
        {
            if (!_reversePaletteLookup.ContainsKey(DescriptionPalette[i]))
            {
                _reversePaletteLookup[DescriptionPalette[i]] = i;
            }
        }
    }

    public string GetDescription(string nodePath)
    {
        if (NodeMap.TryGetValue(nodePath, out int index))
        {
            if (index >= 0 && index < DescriptionPalette.Count)
                return DescriptionPalette[index];
        }
        return string.Empty;
    }

    public void SetDescription(string nodePath, string description)
    {
        if (description == null) description = string.Empty;

        lock (_lock)
        {
            if (!_reversePaletteLookup.TryGetValue(description, out int index))
            {
                index = DescriptionPalette.Count;
                DescriptionPalette.Add(description);
                _reversePaletteLookup[description] = index;
            }
            NodeMap[nodePath] = index;
        }
    }

    /// <summary>
    /// 核心功能：将另一个数据库合并入当前库
    /// </summary>
    /// <param name="other"> 要合并的数据库（如朋友分享的模组定义） </param>
    /// <param name="overwrite"> 如果节点已存在，是否覆盖描述 </param>
    public void MergeWith(NodeDefinitionDatabase other, bool overwrite = false)
    {
        if (other == null || other.NodeMap == null) return;

        lock (_lock)
        {
            //以此防守：确保反向查找表已初始化
            if (_reversePaletteLookup == null) RebuildReverseLookup();

            foreach (var kvp in other.NodeMap)
            {
                string nodeName = kvp.Key;
                int otherIndex = kvp.Value;

                // 获取对方库里的实际描述文本
                string desc = string.Empty;
                if (otherIndex >= 0 && otherIndex < other.DescriptionPalette.Count)
                {
                    desc = other.DescriptionPalette[otherIndex];
                }

                // [修复点 1]：删除了 if (string.IsNullOrEmpty(desc)) continue; 我们需要保留即便是空描述的节点，因为它们代表了节点的存在(Key)。

                bool exists = NodeMap.ContainsKey(nodeName);
                bool shouldUpdate = false;

                if (!exists)
                {
                    // [情况 A]：节点不存在 无论描述是否为空，都必须添加，否则该节点将在编辑器中消失
                    shouldUpdate = true;
                }
                else
                {
                    // [情况 B]：节点已存在
                    if (overwrite)
                    {
                        // [修复点 2]：智能覆盖 如果传入的描述是空的，而本地已有描述，不要覆盖（防止把辛苦写的描述洗白） 只有当传入的描述有内容时，才执行覆盖
                        if (!string.IsNullOrEmpty(desc))
                        {
                            shouldUpdate = true;
                        }
                    }
                }

                if (shouldUpdate)
                {
                    // 添加到描述池并更新索引
                    if (!_reversePaletteLookup.TryGetValue(desc, out int myIndex))
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
            // 文件损坏或格式不对，返回空库
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
                string nodePath = kvp.Key;
                string description = kvp.Value ?? string.Empty;

                // 1. 查找描述是否已存在于池中 (去重)
                if (!_reversePaletteLookup.TryGetValue(description, out int index))
                {
                    // 不存在，添加到池中
                    index = DescriptionPalette.Count;
                    DescriptionPalette.Add(description);
                    _reversePaletteLookup[description] = index;
                }

                // 2. 更新映射 如果该节点已存在，直接覆盖索引；如果不存在，添加新记录
                NodeMap[nodePath] = index;
            }
        }
    }
}
