using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Core.XmlOperator;
using System.Xml.Linq;

namespace RimXmlEdit.Core;

public class InitProject
{
    public InitProject()
    {
    }

    public static void Init(Dictionary<string, object> userData, string projectPath, int extra = 7)
    {
        string projectName = ((string)userData["modName"]).Replace(' ', '_');
        string version = (string)userData["gameVersion"];
        if (!Directory.Exists(Path.Combine(TempConfig.AppPath, "cache")))
        {
            Directory.CreateDirectory(Path.Combine(TempConfig.AppPath, "cache"));
        }

        var aboutPath = Path.Combine(projectPath, projectName, "About");
        Directory.CreateDirectory(aboutPath);
        foreach (var vers in version.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

        var manager = new TemplateManager();
        manager.LoadTemplates(TempConfig.TemplatesPath);
        var rx = manager.ParseTemplate("About");

        XElement finalXml = RXmlWriter.Finalize(rx.Root, userData, manager);
        finalXml.Save(Path.Combine(aboutPath, "About.xml"));
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
