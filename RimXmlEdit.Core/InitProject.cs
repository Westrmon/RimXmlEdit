using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Core;

public class InitProject
{
    public InitProject()
    {
    }

    public static void Init(Dictionary<string, object> userData, string projectPath, int extra = 7)
    {
        string projectName = ((string)userData["modName"]).Replace(' ', '_');
        var version = userData["gameVersion"] as IEnumerable<string>;
        if (!Directory.Exists(Path.Combine(TempConfig.AppPath, "cache")))
        {
            Directory.CreateDirectory(Path.Combine(TempConfig.AppPath, "cache"));
        }

        var aboutPath = Path.Combine(projectPath, projectName, "About");
        Directory.CreateDirectory(aboutPath);
        foreach (var vers in version)
        {
            Directory.CreateDirectory(Path.Combine(projectPath, projectName, vers));
            Directory.CreateDirectory(Path.Combine(projectPath, projectName, vers, "Defs"));
            Directory.CreateDirectory(Path.Combine(projectPath, projectName, vers, "Patches"));

            if ((extra & 4) != 0)
                Directory.CreateDirectory(Path.Combine(projectPath, projectName, vers, "Assemblies"));
        }
        Directory.CreateDirectory(Path.Combine(projectPath, projectName, "Languages"));

        if ((extra & 1) != 0)
            Directory.CreateDirectory(Path.Combine(projectPath, projectName, "Textures"));
        if ((extra & 2) != 0)
            Directory.CreateDirectory(Path.Combine(projectPath, projectName, "Sounds"));
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
