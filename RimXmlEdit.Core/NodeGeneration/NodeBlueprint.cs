using System.Diagnostics.CodeAnalysis;

namespace RimXmlEdit.Core.NodeGeneration;

public class NodeBlueprint
{
    public string TagName { get; set; }
    public string? Value { get; set; }
    public List<AttributeBlueprint> Attributes { get; set; } = new();
    public List<NodeBlueprint> Children { get; set; } = new();

    private static NodeBlueprint? none;

    public static NodeBlueprint None => none ??= new NodeBlueprint("63F83BCE-EDE1-4B84-A16E-104D2A5F6168", "None");

    public NodeBlueprint(string tagName, string? value = null)
    {
        TagName = tagName;
        Value = value;
    }

    public NodeBlueprint()
    {
    }

    public NodeBlueprint AddAttribute(string name, object value, bool isEnum = false, IEnumerable<string>? enumList = null)
    {
        Attributes.Add(new AttributeBlueprint(name, value, isEnum, enumList));
        return this;
    }

    public NodeBlueprint AddChild(NodeBlueprint child)
    {
        Children.Add(child);
        return this;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is NodeBlueprint blueprint && blueprint.GetHashCode() == GetHashCode();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TagName, Value, Attributes, Children);
    }

    public static bool operator ==(NodeBlueprint left, NodeBlueprint right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NodeBlueprint left, NodeBlueprint right)
    {
        return !(left == right);
    }
}

public class AttributeBlueprint
{
    public string Name { get; }
    public object Value { get; }
    public bool IsEnum { get; }
    public IEnumerable<string>? EnumList { get; }

    public AttributeBlueprint(string name, object value, bool isEnum, IEnumerable<string>? enumList = null)
    {
        Name = name;
        Value = value;
        IsEnum = isEnum;
        EnumList = enumList;
    }
}
