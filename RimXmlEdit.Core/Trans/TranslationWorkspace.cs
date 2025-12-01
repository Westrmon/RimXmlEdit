using System.Text.Json.Serialization;

namespace RimXmlEdit.Core.Trans;

public class TranslationUnit
{
    public string Key { get; set; } = string.Empty;
    public string Original { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public TransNodeType Type { get; set; }
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(TransNodeType))]
[JsonSerializable(typeof(TranslationUnit))]
[JsonSerializable(typeof(List<TranslationUnit>))]
public partial class TransJsonSerializerContext : JsonSerializerContext
{
}