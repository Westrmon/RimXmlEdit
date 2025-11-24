using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Utils;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

// 主要为了验证图片路径...
internal class PathValidator : IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value)
    {
        if (!xmlField.Name.EndsWith("Path")) return CheckResult.Empty;

        var fullBasePath = Path.Combine(
                    TempConfig.ProjectFolders["Textures"],
                    value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

        if (File.Exists($"{fullBasePath}.png"))
            return CheckResult.Success;

        var fileName = Path.GetFileName(value);
        string[] mandatoryDirs = ["north", "south"];
        foreach (var dir in mandatoryDirs)
        {
            if (!File.Exists($"{fullBasePath}_{dir}.png"))
            {
                return new CheckResult(false, $"贴图 '{fileName}' 缺失必要方向: {dir}");
            }
        }
        bool hasWest = File.Exists($"{fullBasePath}_west.png");
        bool hasEast = File.Exists($"{fullBasePath}_east.png");
        if (!hasWest && !hasEast)
        {
            return new CheckResult(false, $"贴图 '{fileName}' 水平方向缺失: 需至少包含 west 或 east 之一");
        }

        return CheckResult.Success;
    }
}
