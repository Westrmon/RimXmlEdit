using RimXmlEdit.Core.Entries;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

/// <summary>
/// 值验证接口, 可以拓展验证方案
/// </summary>
public interface IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value);
}
