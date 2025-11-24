using RimXmlEdit.Core.Entries;
using System;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.Core.ValueValid;

internal class EnumableValidator : IValueValidator
{
    public CheckResult IsValid(XmlFieldInfo xmlField, string value)
    {
        if ((xmlField.Type & XmlFieldType.Enumable) is not XmlFieldType.Enumable) return CheckResult.Empty;

        if (xmlField.EnumValues!.Contains(value))
        {
            return CheckResult.Success;
        }

        string inputLower = value.ToLower();

        var suggestions = xmlField.EnumValues
                                  .Select(enumValue =>
                                  {
                                      string enumLower = enumValue.ToLower();

                                      return new
                                      {
                                          Original = enumValue,
                                          StartsWith = enumLower.StartsWith(inputLower),
                                          Contains = enumLower.Contains(inputLower),
                                          Distance = GetLevenshteinDistance(enumLower, inputLower)
                                      };
                                  })
                                  .OrderByDescending(x => x.StartsWith)
                                  .ThenByDescending(x => x.Contains)
                                  .ThenBy(x => x.Distance)
                                  .ThenBy(x => Math.Abs(x.Original.Length - value.Length))
                                  .Select(x => x.Original)
                                  .Take(5);

        string suggestionMsg = string.Join(", ", suggestions);
        string errorMsg = string.IsNullOrEmpty(suggestionMsg)
            ? $"Value '{value}' is invalid."
            : $"Error. Did you mean: {suggestionMsg}?";

        return new CheckResult(false, errorMsg);
    }

    private static int GetLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        if (n > m) { (s, t) = (t, s); (n, m) = (m, n); }

        int[] d = new int[n + 1];
        for (int i = 0; i <= n; i++) d[i] = i;

        for (int j = 1; j <= m; j++)
        {
            int prev = d[0];
            d[0] = j;

            for (int i = 1; i <= n; i++)
            {
                int temp = d[i];
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                int min = Math.Min(Math.Min(d[i - 1] + 1, d[i] + 1), prev + cost);

                prev = temp;
                d[i] = min;
            }
        }
        return d[n];
    }
}
