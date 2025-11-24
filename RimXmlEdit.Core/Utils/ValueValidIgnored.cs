namespace RimXmlEdit.Core.Utils;

public class ValueValidIgnored
{
    private static HashSet<string> Ignored { get; set; } = new();

    static ValueValidIgnored()
    {
        // 经测试无法正确获取的类型或枚举, 且无法解决的忽略
        Ignored.Add("Pawn_MeleeDodge");
    }

    /// <summary>
    /// 添加一个忽略项
    /// </summary>
    /// <param name="name">值或xpath</param>
    public static void Add(string name)
    {
        Ignored.Add(name);
    }

    public static void Remove(string name)
    {
        Ignored.Remove(name);
    }

    public static bool IsIgnored(string name)
    {
        return Ignored.Contains(name);
    }
}
