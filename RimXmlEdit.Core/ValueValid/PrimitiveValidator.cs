using RimXmlEdit.Core.Entries;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

internal class PrimitiveValidator : IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value)
    {
        if ((xmlField.Type & XmlFieldType.Primitive) != XmlFieldType.Primitive) return CheckResult.Empty;

        return xmlField.FieldTypeName switch
        {
            "System.String" => CheckResult.Success,
            "System.Int32" => new CheckResult(int.TryParse(value, out _), "int32"),
            "System.Single" => new CheckResult(float.TryParse(value, out _), "float"),
            "System.Boolean" => new CheckResult(bool.TryParse(value, out _), "bool"),
            _ => new CheckResult(true, "Unknown type"),
        };
    }
}
