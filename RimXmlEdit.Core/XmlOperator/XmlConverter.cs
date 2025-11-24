using RimXmlEdit.Core.Entries;
using System.Xml.Linq;

namespace RimXmlEdit.Core.XmlOperator;

public static class XmlConverter
{
    #region Public API

    public static string Serialize(RXStruct rxStruct)
    {
        XElement rootElement = new XElement(rxStruct.IsPatch ? "Patch" : "Defs");
        XComment comment = new XComment("此文件由 RimXmlEdit 自动生成");
        rootElement.Add(comment);
        XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);

        foreach (var genericInfo in rxStruct.Defs)
        {
            var topLevelElement = new XElement(genericInfo.TagName);

            if (!rxStruct.IsPatch)
            {
                if (!string.IsNullOrEmpty(genericInfo.ParentName))
                    topLevelElement.SetAttributeValue("ParentName", genericInfo.ParentName);
                if (genericInfo.IsAbstract)
                    topLevelElement.SetAttributeValue("Abstract", "True");
                if (genericInfo.IgnoreConfigErrors)
                    topLevelElement.SetAttributeValue("ignoreConfigErrors", "True");
                if (!string.IsNullOrEmpty(genericInfo.Name))
                    topLevelElement.Add(new XElement("defName", genericInfo.Name));
            }
            else
            {
                if (!string.IsNullOrEmpty(genericInfo.Ref))
                {
                    topLevelElement.SetAttributeValue("Class", genericInfo.Ref);
                }
            }

            foreach (var field in genericInfo.Fields)
            {
                var fieldElement = BuildXmlElement(field);
                if (fieldElement != null)
                {
                    topLevelElement.Add(fieldElement);
                }
            }
            rootElement.Add(topLevelElement);
        }
        return doc.ToString();
    }

    public static string SerializeAbout(RXStruct rxStruct)
    {
        XElement rootElement = new XElement("ModMetaData");
        XComment comment = new XComment("此文件由 RimXmlEdit 自动生成");
        rootElement.Add(comment);
        XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);
        foreach (var genericInfo in rxStruct.Defs)
        {
            var topLevelElement = new XElement(genericInfo.TagName);

            if (!string.IsNullOrEmpty(genericInfo.Value))
            {
                topLevelElement.Value = genericInfo.Value;
            }
            else
            {
                foreach (var field in genericInfo.Fields)
                {
                    var fieldElement = BuildXmlElement(field);
                    if (fieldElement != null)
                    {
                        topLevelElement.Add(fieldElement);
                    }
                }
            }
            rootElement.Add(topLevelElement);
        }
        return doc.ToString();
    }

    public static RXStruct? Deserialize(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            return null;

        var rxStruct = new RXStruct();
        XDocument doc = XDocument.Parse(xmlContent, LoadOptions.SetLineInfo);
        var root = doc.Root ?? throw new ArgumentException("Invalid XML content: No root element found.");
        if (root.Name.LocalName != "Defs"
            && root.Name.LocalName != "Patch"
            && root.Name.LocalName != "ModMetaData")
            return null;
        rxStruct.IsPatch = root.Name.LocalName == "Patch";
        rxStruct.IsModMetaData = root.Name.LocalName == "ModMetaData";

        foreach (var topLevelElement in root.Elements())
        {
            var genericInfo = new DefInfo
            {
                TagName = topLevelElement.Name.LocalName
            };

            genericInfo.Value = topLevelElement.HasElements ? string.Empty : topLevelElement.Value;

            if (!rxStruct.IsPatch)
            {
                genericInfo.ParentName = topLevelElement.Attribute("ParentName")?.Value ?? string.Empty;
                genericInfo.IsAbstract = bool.TryParse(topLevelElement.Attribute("Abstract")?.Value, out var isAbstract) && isAbstract;
                genericInfo.IgnoreConfigErrors = bool.TryParse(topLevelElement.Attribute("ignoreConfigErrors")?.Value, out var ignoreConfigErrors) && ignoreConfigErrors;
            }
            else
            {
                genericInfo.Ref = topLevelElement.Attribute("Class")?.Value ?? string.Empty;
            }

            foreach (var fieldElement in topLevelElement.Elements())
            {
                if (!rxStruct.IsPatch && fieldElement.Name.LocalName == "defName" || fieldElement.Name.LocalName == "name")
                {
                    genericInfo.Name = fieldElement.Value;
                }

                var fieldInfo = ParseXmlElement(fieldElement);
                genericInfo.Fields.Add(fieldInfo);
            }
            rxStruct.Defs.Add(genericInfo);
        }

        return rxStruct;
    }

    #endregion Public API

    #region Private Recursive Helpers

    private static XElement? BuildXmlElement(XmlFieldInfo field)
    {
        if (field.Value == null && string.IsNullOrEmpty(field.Ref))
            return null;

        var element = new XElement(field.Name);

        if (!string.IsNullOrEmpty(field.Ref))
        {
            element.SetAttributeValue("Class", field.Ref);
        }

        void BuildInternal(XElement currentElement, object? value)
        {
            if (value == null) return;
            switch (value)
            {
                case Dictionary<string, XmlFieldInfo> dict:
                    foreach (var kvp in dict)
                    {
                        var childElement = BuildXmlElement(kvp.Value);
                        if (childElement != null)
                            currentElement.Add(childElement);
                    }
                    break;

                case List<XmlFieldInfo> list:
                    foreach (var itemField in list)
                    {
                        var listItemElement = BuildXmlElement(itemField);
                        if (listItemElement != null)
                            currentElement.Add(listItemElement);
                    }
                    break;

                default:
                    currentElement.Value = value.ToString() ?? string.Empty;
                    break;
            }
        }
        BuildInternal(element, field.Value);
        // 如果元素既没有子节点，也没有值，并且不是一个带Class的空节点，则可能没必要创建
        //if (!element.HasElements && string.IsNullOrEmpty(element.Value) && string.IsNullOrEmpty(field.Ref))
        //{
        //}

        return element;
    }

    private static XmlFieldInfo ParseXmlElement(XElement element)
    {
        var fieldInfo = new XmlFieldInfo
        {
            Name = element.Name.LocalName,
            Ref = element.Attribute("Class")?.Value ?? string.Empty
        };

        bool isList = element.HasElements && (element.Elements().All(e => e.Name.LocalName == "li") ||
                                              element.Elements().GroupBy(e => e.Name).Any(g => g.Count() > 1));

        if (isList)
        {
            var list = new List<XmlFieldInfo>();
            foreach (var child in element.Elements())
            {
                list.Add(ParseXmlElement(child));
            }
            fieldInfo.Value = list;
        }
        else if (element.HasElements)
        {
            var dict = new Dictionary<string, XmlFieldInfo>();
            foreach (var child in element.Elements())
            {
                dict[child.Name.LocalName] = ParseXmlElement(child);
            }
            fieldInfo.Value = dict;
        }
        else
        {
            fieldInfo.Value = element.Value;
        }

        return fieldInfo;
    }

    #endregion Private Recursive Helpers
}
