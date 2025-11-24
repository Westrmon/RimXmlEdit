using MessagePack;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Core.XmlOperator;
using System.Text.RegularExpressions;
using System.Xml;

namespace RimXmlEdit.Core.Parse;

// 暂时没用, 后续可以作为对mod搜索/xpath解析等, 也可以作为翻译工具类
/// <summary>
/// 通过mod解析为 <see cref="RXStruct" />
/// </summary>
public class ModParser // 多线程标记
{
    private ILogger _log = LoggerFactoryInstance.Factory.CreateLogger(nameof(ModParser));

    private Regex _regex = new Regex(@"\d\.\d", RegexOptions.Compiled);

    public IEnumerable<string>? LastParseErrorXMLPaths { get; private set; } = null;

    public Dictionary<string, int> ModReferenceCount { get; private set; } = null;

    public IEnumerable<ModInfo> Parse(
        List<string>? modDir = null,
        ParseRange? parseRange = null,
        bool isforceSave = false)
    {
        int count = 0;
        var mods = new List<string>();
        parseRange ??= ParseRange.Core | ParseRange.DLC;
        List<ModInfo> modInfo = new List<ModInfo>();
        string cachePath = Path.Combine(TempConfig.AppPath, "cache", "officialCache.bin");
        string countCachePath = Path.Combine(TempConfig.AppPath, "cache", "officialRefCountCache.bin");
        if (!File.Exists(cachePath) || isforceSave)
        {
            if ((parseRange & ParseRange.Core) is ParseRange.Core)
            {
                mods.Add(Path.Combine(TempConfig.GamePath, "Data", "Core"));
                if (mods.Count > 0)
                    _log.LogDebug("读取到Core");
                count = mods.Count;
            }
            if ((parseRange & ParseRange.DLC) is ParseRange.DLC)
            {
                var paths = Directory.GetDirectories(Path.Combine(TempConfig.GamePath, "Data")).Where(x => !x.EndsWith("Core")).ToList();
                if (paths.Count > 0)
                    _log.LogDebug("共识别到{}个DLC", paths.Count);
                count += paths.Count;
                mods.AddRange(paths);
            }

            if (mods.Count > 0)
            {
                ParseMod(mods, modInfo);

                ModReferenceCount = modInfo.SelectMany(m => m.Defs)
                                           .SelectMany(d => d.Defs)
                                           .GroupBy(t => t.TagName.Split('_')[0])
                                           .Select(g => new { Name = g.Key, Count = g.Count() })
                                           .OrderByDescending(c => c.Count)
                                           .ToDictionary(e => e.Name, e => e.Count);

                using var fs = File.Create(cachePath);
                MessagePackSerializer.Serialize(fs, modInfo);
                using var fs2 = File.Create(countCachePath);
                MessagePackSerializer.Serialize(fs2, ModReferenceCount);

                mods.Clear();
            }
        }
        else
        {
            if (File.Exists(cachePath))
            {
                using var fs = File.OpenRead(cachePath);
                modInfo = MessagePackSerializer.Deserialize<List<ModInfo>>(fs);
            }
            if (File.Exists(countCachePath))
            {
                using var fs2 = File.OpenRead(countCachePath);
                ModReferenceCount = MessagePackSerializer.Deserialize<Dictionary<string, int>>(fs2);
            }
        }

        int officalInfoCount = modInfo.Count;

        if (modDir != null && (parseRange & ParseRange.CommunityMod) is ParseRange.CommunityMod)
        {
            for (int i = 0; i < modDir.Count; i++)
            {
                var path = modDir[i];
                var childDirs = Directory.GetDirectories(path);
                if (childDirs.Any(x => x.EndsWith("About")))
                    mods.Add(path);
                else
                    mods.AddRange(childDirs);
            }
        }

        if (mods.Count > 0)
        {
            FiliterExistCache(mods, modInfo);
            ParseMod(mods, modInfo);
            var infoAndPath = modInfo.Skip(officalInfoCount).Zip(mods, (info, path) => (info, path));
            // 自动保存为缓存(以mod为单位, 主要是为了可以获取结构信息)
            foreach (var item in infoAndPath)
            {
                string modName = item.path.Split(Path.DirectorySeparatorChar).Last();
                string modPath = Path.Combine(TempConfig.AppPath, "cache", modName + "Cache.bin");
                using var fs = File.Create(modPath);
                MessagePackSerializer.Serialize(fs, modInfo);
            }
        }
        return modInfo;
    }

    private void ParseMod(IEnumerable<string> modsPath, List<ModInfo> infos, bool isOverrideError = false)
    {
        List<string> errorXmls = new List<string>();
        foreach (var mod in modsPath)
        {
            RXStruct about = null!;
            List<RXStruct> modDefs = new List<RXStruct>();
            foreach (string filePath in Directory.EnumerateFiles(mod, "*.xml", SearchOption.AllDirectories))
            {
                var match = _regex.Match(filePath);
                if (match.Success)
                {
                    // 此处直接使用最新版本, 后续再扩展为多版本
                    var version = match.Value;
                    if (version != "1.6")
                        continue;
                }
                try
                {
                    var data = XmlConverter.Deserialize(File.ReadAllText(filePath));
                    if (data == null)
                        continue;
                    if (data.IsModMetaData)
                        about = data;
                    else
                        modDefs.Add(data);
                }
                catch (XmlException e)
                {
                    _log.LogError(e, "解析文件 {} 失败", filePath);
                    errorXmls.Add(filePath);
                }
            }
            _log.LogDebug("已解析Mod {} , 共有 {} 个定义",
                about.Defs.FirstOrDefault(t => t.TagName == "packageId")?.Value
                    ?? about.Defs.First(t => t.TagName == "name").Value,
                modDefs.Count);
            infos.Add(new(about, modDefs));
        }
        _log.LogDebug("成功解析 {} 个mod, 解析失败 {} 个xml文件", infos.Count, errorXmls.Count);
        if (errorXmls.Count > 0)
        {
            if (isOverrideError)
                LastParseErrorXMLPaths = errorXmls;
            else
                LastParseErrorXMLPaths = LastParseErrorXMLPaths?.Concat(errorXmls) ?? errorXmls;
        }
        else
            LastParseErrorXMLPaths = null;
    }

    private static void FiliterExistCache(List<string> path, List<ModInfo> infos)
    {
        string cachePath = Path.Combine(TempConfig.AppPath, "cache");
        var pending = new HashSet<string>(path);

        foreach (var item in Directory.EnumerateFiles(cachePath, "*.bin"))
        {
            var modName = Path.GetFileNameWithoutExtension(item);
            if (pending.Remove(modName))
            {
                using var fs = File.OpenRead(item);
                var info = MessagePackSerializer.Deserialize<ModInfo>(fs);
                infos.Add(info);
            }

            if (pending.Count == 0)
                break;
        }

        path.Clear();
        path.AddRange(pending);
    }

    [MessagePackObject]
    public class ModInfo
    {
        [Key(0)]
        public RXStruct About { get; }

        [Key(1)]
        public IEnumerable<RXStruct> Defs { get; }

        public ModInfo(RXStruct about, IEnumerable<RXStruct> defs)
        {
            About = about;
            Defs = defs;
        }
    }

    public enum ParseRange
    {
        Core = 1,
        DLC,
        CommunityMod = 4
    }
}
