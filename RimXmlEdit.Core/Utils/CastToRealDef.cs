namespace RimXmlEdit.Core.Utils;

internal static class CastToRealDef
{
    private static Dictionary<string, string> value = new Dictionary<string, string>
    {
        { "RimWorld.SkillGain", "RimWorld.SkillDef" },
        { "RimWorld.StatModifier", "RimWorld.StatDef" }
    };

    public static string GetRealDef(string key)
    {
        value.TryGetValue(key, out string? realDef);
        return realDef ?? key;
    }
}
