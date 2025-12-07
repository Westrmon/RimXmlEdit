using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Core;

public class InitProject
{
    public static string Init(Dictionary<string, object> userData, string projectPath, int extra = 7)
    {
        var version = userData["gameVersion"] as IEnumerable<string>;
        if (!Directory.Exists(Path.Combine(TempConfig.AppPath, "cache")))
            Directory.CreateDirectory(Path.Combine(TempConfig.AppPath, "cache"));

        var aboutPath = Path.Combine(projectPath, "About");
        Directory.CreateDirectory(aboutPath);
        foreach (var vers in version)
        {
            Directory.CreateDirectory(Path.Combine(projectPath, vers));
            Directory.CreateDirectory(Path.Combine(projectPath, vers, "Defs"));
            Directory.CreateDirectory(Path.Combine(projectPath, vers, "Patches"));

            if ((extra & 4) != 0)
                Directory.CreateDirectory(Path.Combine(projectPath, vers, "Assemblies"));
        }

        // 此处需要修改为可配置的目录, 可能会放在版本文件夹后面
        Directory.CreateDirectory(Path.Combine(projectPath, "Languages"));

        if ((extra & 1) != 0)
            Directory.CreateDirectory(Path.Combine(projectPath, "Textures"));
        if ((extra & 2) != 0)
            Directory.CreateDirectory(Path.Combine(projectPath, "Sounds"));

        return projectPath;
    }

    public static void Load()
    {
        MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new ObjectFieldInfoFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance }
            ));
        // .WithCompression(MessagePackCompression.Lz4Block);
    }
}