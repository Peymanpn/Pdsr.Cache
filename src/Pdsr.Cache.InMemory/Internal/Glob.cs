using System.Text;
using System.Text.RegularExpressions;

namespace Pdsr.Cache.InMemory.Internal;

/// <summary>
/// Redis-style glob patterns: <c>*</c>, <c>?</c>, <c>[abc]</c>, <c>[^a]</c>, <c>[a-z]</c> and <c>\</c> escapes.
/// </summary>
internal static class Glob
{
    public static Regex ToRegex(string pattern)
    {
        var regex = new StringBuilder("^", pattern.Length + 8);
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '\\' when i + 1 < pattern.Length:
                    regex.Append(Regex.Escape(pattern[++i].ToString()));
                    break;
                case '*':
                    regex.Append(".*");
                    break;
                case '?':
                    regex.Append('.');
                    break;
                case '[':
                    var close = pattern.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        regex.Append(@"\[");
                        break;
                    }
                    var body = pattern.Substring(i + 1, close - i - 1);
                    var negate = body.StartsWith("^", StringComparison.Ordinal);
                    if (negate) body = body.Substring(1);
                    regex.Append('[');
                    if (negate) regex.Append('^');
                    regex.Append(body.Replace(@"\", @"\\").Replace("[", @"\["));
                    regex.Append(']');
                    i = close;
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }
        regex.Append('$');
        return new Regex(regex.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }
}
