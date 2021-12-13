using System.Text.RegularExpressions;

public static class RegexUtils
{
    public static Regex WildcardMatcher(string pattern)
    {
        return new Regex("^" + Regex.Escape(pattern).
        Replace("\\*", ".*").
        Replace("\\?", ".") + "$");
    }
}