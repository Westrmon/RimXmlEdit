using RimXmlEdit.Core.Entries;
using System.Text.RegularExpressions;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

internal class SimpleInstanceValidator : IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value)
    {
        if ((xmlField.Type & XmlFieldType.SimpleClass) != XmlFieldType.SimpleClass
            && (xmlField.Type & XmlFieldType.SimpleList) != XmlFieldType.SimpleList
            && (xmlField.Type & XmlFieldType.PolymorphicList) != XmlFieldType.PolymorphicList)
            return CheckResult.Empty;

        if (xmlField.Type == XmlFieldType.SimpleClass)
        {
            if (xmlField.FieldTypeName.EndsWith("IntVec2"))
            {
                if (Regex.Match(value, @"\(-?\d+,-?\d+\)").Success)
                {
                    return CheckResult.Success;
                }
                return new CheckResult(false, "Invalid IntVec2 format");
            }
            else if (xmlField.FieldTypeName.EndsWith("IntVec3"))
            {
                if (Regex.Match(value, @"\(-?\d+,-?\d+,-?\d+\)").Success)
                {
                    return CheckResult.Success;
                }
                return new CheckResult(false, "Invalid IntVec3 format");
            }
            else if (xmlField.Name.EndsWith("Class"))
                return CheckResult.Success;
        }

        return CheckResult.Empty;
    }
}
