using RimXmlEdit.Core.Entries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

internal partial class UnityEntryValidator : IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value)
    {
        if (!xmlField.FieldTypeName.StartsWith("UnityEngine")) return CheckResult.Empty;
        if (xmlField.FieldTypeName == "UnityEngine.Vector2")
        {
            if (Regex.Match(value, @"\(-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?\)").Success)
                return CheckResult.Success;
            else
                return new CheckResult(false, "Vector2");
        }

        if (xmlField.FieldTypeName == "UnityEngine.Vector3")
        {
            if (Regex.Match(value, @"\(-?\d+(?:\.\d+)?(?:e-?\d+)?,-?\d+(?:\.\d+)?(?:e-?\d+)?,-?\d+(?:\.\d+)?(?:e-?\d+)?\)").Success)
                return CheckResult.Success;
            else
                return new CheckResult(false, "Vector3");
        }
        return CheckResult.Empty;
    }
}
