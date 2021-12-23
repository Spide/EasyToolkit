using System.Text.RegularExpressions;
namespace Easy.Utils
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