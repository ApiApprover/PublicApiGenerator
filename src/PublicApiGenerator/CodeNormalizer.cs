using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PublicApiGenerator
{
    internal static class CodeNormalizer
    {
        const string AutoGeneratedHeader = @"^//-+\s*$.*^//-+\s*$";
        const string EmptyGetSet = @"\s+{\s+get\s+{\s+}\s+set\s+{\s+}\s+}";
        const string EmptyGet = @"\s+{\s+get\s+{\s+}\s+}";
        const string EmptySet = @"\s+{\s+set\s+{\s+}\s+}";
        const string GetSet = @"\s+{\s+get;\s+set;\s+}";
        const string Get = @"\s+{\s+get;\s+}";
        const string Set = @"\s+{\s+set;\s+}";

        // https://github.com/ApiApprover/ApiApprover/issues/80
        internal const string StaticMarker = "static_C91E2709_C00B-4CAB_8BBC_B2B11DC75E50 ";
        internal const string ReadonlyMarker = "readonly_79D3ED2A_0B60_4C3B_8432_941FE471A38B ";
        internal const string AttributeMarker = "_attribute_292C96C3_C42E_4C07_BEED_73F5DAA0A6DF_";
        internal const string EventModifierMarkerTemplate = "_{0}_292C96C3C42E4C07BEED73F5DAA0A6DF_";
        internal const string EventRemovePublicMarker = "removepublic";

        public static string NormalizeGeneratedCode(StringWriter writer)
        {
            var gennedClass = writer.ToString();

            gennedClass = Regex.Replace(gennedClass, AutoGeneratedHeader, string.Empty,
                RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline | RegexOptions.Singleline);
            gennedClass = Regex.Replace(gennedClass, EmptyGetSet, " { get; set; }",
                RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, GetSet, " { get; set; }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, EmptyGet, " { get; }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, EmptySet, " { set; }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, Get, " { get; }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, Set, " { set; }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, @"\s+{\s+}", " { }", RegexOptions.IgnorePatternWhitespace);
            gennedClass = Regex.Replace(gennedClass, @"\)\s+;", ");", RegexOptions.IgnorePatternWhitespace);
            var attributeMarkerEscaped = Regex.Escape(AttributeMarker);
            gennedClass = Regex.Replace(gennedClass, $@"
                (Attribute)?                               # Delete this if present. Would create a clash for Attribute1, Attribute1Attribute but that is a very rare edge case
                (                                          # Then require:
                {attributeMarkerEscaped}(\(\))?(?=\])      # - Empty parens (to delete) if present, immediately followed by closing square brace (not deleted),
                |
                {attributeMarkerEscaped}(?=\(.+\)\])       # - or non-empty parens immediately followed by closing square brace (not deleted).
                |
                {attributeMarkerEscaped}(?=new.+\}}\)\]))  # - or non-empty parens immediately followed by new, closing curlies, closing brace and square brace to cover new object [] {{ }} cases (not deleted).
                ",
                string.Empty,
                RegexOptions.Singleline |
                RegexOptions.IgnorePatternWhitespace); // SingleLine is required for multi line params arrays

            gennedClass = Regex.Replace(gennedClass, @"(.*) _(.*)_292C96C3C42E4C07BEED73F5DAA0A6DF_(.*)", EventModifierMatcher, RegexOptions.IgnorePatternWhitespace);
            gennedClass = gennedClass.Replace("class " + StaticMarker, "static class ");
            gennedClass = gennedClass.Replace("struct " + ReadonlyMarker, "readonly struct ");
            gennedClass = gennedClass.Replace(ReadonlyMarker, string.Empty); // remove magic marker from readonly struct ctor
            gennedClass = Regex.Replace(gennedClass, @"\r\n|\n\r|\r|\n", Environment.NewLine);

            gennedClass = RemoveUnnecessaryWhiteSpace(gennedClass);
            return gennedClass;
        }

        static string EventModifierMatcher(Match group)
        {
            var modifier = @group.Groups[2].Value;

            var replacementBuilder = new StringBuilder();
            if (modifier.EndsWith(EventRemovePublicMarker))
            {
                replacementBuilder.Append(modifier == EventRemovePublicMarker
                    ? "event "
                    : $"{modifier.Replace(EventRemovePublicMarker, string.Empty)} event ");
            }
            else
            {
                replacementBuilder.Append($"public {modifier} event ");
            }

            return group.ToString().Replace(string.Format(EventModifierMarkerTemplate, modifier), string.Empty)
                .Replace("public event ", replacementBuilder.ToString());
        }

        static string RemoveUnnecessaryWhiteSpace(string publicApi)
        {
            return string.Join(Environment.NewLine, publicApi.Split(new[]
                {
                    Environment.NewLine
                }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.TrimEnd())
            );
        }
    }
}
