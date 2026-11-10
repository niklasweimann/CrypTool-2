using System.Collections.Generic;
using System.Text;

namespace System.Windows.Markup
{
    /// <summary>A parsed <c>{Name positional, Key=Value, ...}</c> attribute value.</summary>
    internal sealed class MarkupExpression
    {
        public string Name { get; private set; }

        public List<string> Positional { get; } = new List<string>();

        public List<KeyValuePair<string, string>> Named { get; } = new List<KeyValuePair<string, string>>();

        /// <summary>True for "{Binding ...}" style values; "{}" is the escape for a literal brace.</summary>
        public static bool IsExpression(string text)
        {
            return text != null && text.Length > 2 && text[0] == '{' && text[1] != '}' && text[text.Length - 1] == '}';
        }

        public static MarkupExpression Parse(string text)
        {
            string body = text.Trim().Substring(1, text.Trim().Length - 2).Trim();
            int nameEnd = 0;
            while (nameEnd < body.Length && !char.IsWhiteSpace(body[nameEnd]) && body[nameEnd] != ',')
            {
                nameEnd++;
            }

            var expression = new MarkupExpression { Name = body.Substring(0, nameEnd) };
            foreach (string part in SplitTopLevel(body.Substring(nameEnd), ','))
            {
                string argument = part.Trim();
                if (argument.Length == 0)
                {
                    continue;
                }

                int equals = IndexOfTopLevel(argument, '=');
                if (equals > 0 && IsIdentifier(argument.Substring(0, equals).Trim()))
                {
                    expression.Named.Add(new KeyValuePair<string, string>(argument.Substring(0, equals).Trim(), Unquote(argument.Substring(equals + 1).Trim())));
                }
                else
                {
                    expression.Positional.Add(Unquote(argument));
                }
            }
            return expression;
        }

        private static bool IsIdentifier(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != ':')
                {
                    return false;
                }
            }
            return text.Length > 0;
        }

        private static string Unquote(string text)
        {
            if (text.Length >= 2 && text[0] == '\'' && text[text.Length - 1] == '\'')
            {
                text = text.Substring(1, text.Length - 2);
            }
            return text.Replace("\\,", ",").Replace("\\'", "'");
        }

        /// <summary>Splits at separators that are outside of nested braces and quotes.</summary>
        private static List<string> SplitTopLevel(string text, char separator)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    current.Append(c).Append(text[++i]);
                    continue;
                }
                if (c == '\'')
                {
                    quoted = !quoted;
                }
                else if (!quoted && c == '{')
                {
                    depth++;
                }
                else if (!quoted && c == '}')
                {
                    depth--;
                }

                if (c == separator && depth == 0 && !quoted)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            parts.Add(current.ToString());
            return parts;
        }

        private static int IndexOfTopLevel(string text, char wanted)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\'')
                {
                    quoted = !quoted;
                }
                else if (!quoted && c == '{')
                {
                    depth++;
                }
                else if (!quoted && c == '}')
                {
                    depth--;
                }
                else if (c == wanted && depth == 0 && !quoted)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
