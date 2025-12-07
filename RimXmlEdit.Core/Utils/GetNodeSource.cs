using RimXmlEdit.Core.Parse;

namespace RimXmlEdit.Core.Utils;

public static class GetNodeSource
{
    public static IEnumerable<string> GetSource(string keyPath, IEnumerable<ModParser.ModInfo> modInfos)
    {
        throw new NotImplementedException();
        foreach (var info in modInfos)
        {
            var currentModName = info.About.Defs.First(t => t.TagName == "packageId");
            var keys = keyPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var item = info.Defs.SelectMany(t => t.Defs.Where(d => d.TagName == keys[0]));
            foreach (var key in item)
            {
                // key.Fields.Where(g => g.Name == key)
            }
        }

        return [];
    }
}