using RimXmlEdit.Core.Entries;

namespace RimXmlEdit.Core.Parse;

// 作用就是tab补全
public class XPathParser
{
    public static XpathDefNameMatch GetXpathContent(string xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
            return default;

        string defName = null;
        string classRef = null;

        var segments = xpath.Split('/');
        var xmlPathParts = new List<string>();

        foreach (var segment in segments.Skip(1))
        {
            var left = segment;
            string bracket = null;

            var idx = segment.IndexOf('[');
            if (idx >= 0)
            {
                left = segment[..idx];
                bracket = segment[(idx + 1)..^1];
            }

            if (!string.IsNullOrWhiteSpace(left))
                xmlPathParts.Add(left);

            if (!string.IsNullOrWhiteSpace(bracket))
            {
                // defName="xxx"
                var defKey = ParseKeyVal(bracket, "defName");
                if (!string.IsNullOrEmpty(defKey))
                    defName = defKey;

                var classKey = ParseKeyVal(bracket, "@Class");
                if (!string.IsNullOrEmpty(classKey))
                    classRef = classKey;
            }
        }

        return new XpathDefNameMatch
        {
            DefName = defName,
            XmlPath = string.Join("/", xmlPathParts)
        };
    }

    public static string Build(XpathDefNameMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.XmlPath))
            return string.Empty;

        var parts = match.XmlPath.Split('/');
        var builder = new List<string>();

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (i == 0)
            {
                if (!string.IsNullOrWhiteSpace(match.DefName))
                    builder.Add($"{part}[defName=\"{match.DefName}\"]");
                else
                    builder.Add(part);
            }
            else
            {
                if (i == parts.Length - 1 && !string.IsNullOrWhiteSpace(match.XmlField.Ref))
                    builder.Add($"{part}[@Class=\"{match.XmlField.Ref}\"]");
                else
                    builder.Add(part);
            }
        }

        return "Defs/" + string.Join("/", builder);
    }

    private static string ParseKeyVal(string bracket, string key)
    {
        var pos = bracket.IndexOf(key);
        if (pos < 0) return null;

        var eq = bracket.IndexOf('=', pos);
        if (eq < 0) return null;

        var right = bracket[(eq + 1)..].Trim().Trim('"', '\'');
        return right;
    }
}

public struct XpathDefNameMatch
{
    public string DefName;
    public string XmlPath;
    public XmlFieldInfo XmlField;

    public XpathDefNameMatch(string defName, string xmlPath, XmlFieldInfo xmlField)
    {
        DefName = defName;
        XmlPath = xmlPath;
        XmlField = xmlField;
    }

    public override string ToString()
    {
        return $"{{ DefName: \"{DefName}\", XmlPath: \"{XmlPath}\"}}";
    }
}