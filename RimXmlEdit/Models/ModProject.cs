namespace RimXmlEdit.Models;

public class ModProject
{
    public string Name { get; set; }
    public string Path { get; set; }

    public ModProject(string name, string path)
    {
        Name = name;
        Path = path;
    }
}
