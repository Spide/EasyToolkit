using System.Text.RegularExpressions;
namespace Easy.Logging
{
    public static class RegexUtils
    {
        public static Regex WildcardMatcher(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).
            Replace("\\*", ".*").
            Replace("\\?", ".") + "$");
        }
    }
}