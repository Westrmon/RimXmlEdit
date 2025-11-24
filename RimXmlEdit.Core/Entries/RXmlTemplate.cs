using System.Xml.Linq;

namespace RimXmlEdit.Core.Entries;

public class RXmlTemplate
{
    public XElement Root { get; set; }
    public List<NodeMeta> Nodes { get; set; }

    public RXmlTemplate()
    {
        Nodes = new List<NodeMeta>();
    }
}
