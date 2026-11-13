using System.Text;

namespace CrypTool.Web.Core;

/// <summary>
/// Minimal RTF to plain text conversion for the notes of old workspaces (TextModel data was RTF in early CT2 versions).
/// Handles paragraphs, tabs, escaped characters (\'hh, \uN) and skips header tables and "{\*" destinations.
/// </summary>
static class RtfText
{
    /// <summary>Converts only when the text looks like RTF.</summary>
    public static string Snippet(string text) => text.Contains('\\') || text.Contains('{') ? ToText(text) : text;

    public static string ToText(string rtf)
    {
        var text = new StringBuilder();
        int depth = 0;       // nesting level of { }
        bool skipping = false; // inside a destination whose content is not text (font table, picture, ...)
        int skipDepth = 0;     // the level at which skipping started; it ends with the matching }

        for (int i = 0; i < rtf.Length; i++)
        {
            char c = rtf[i];
            if (c == '{')
            {
                depth++;
                bool ignorableDestination = i + 2 < rtf.Length && rtf[i + 1] == '\\' && rtf[i + 2] == '*';
                if (ignorableDestination)
                {
                    skipping = true;
                    skipDepth = depth;
                }
            }
            else if (c == '}')
            {
                if (skipping && depth == skipDepth) skipping = false;
                depth--;
            }
            else if (c == '\\')
            {
                i = ReadControl(rtf, i, text, ref skipping, ref skipDepth, depth);
            }
            else if (!skipping && c != '\r' && c != '\n')
            {
                text.Append(c);
            }
        }
        return text.ToString().Trim();
    }

    /// <summary>Handles the control symbol or word that starts at the backslash at <paramref name="start"/>; returns the index of its last character.</summary>
    static int ReadControl(string rtf, int start, StringBuilder text, ref bool skipping, ref int skipDepth, int depth)
    {
        int j = start + 1;

        // control symbol: backslash followed by one non-letter
        if (j < rtf.Length && !char.IsLetter(rtf[j]))
        {
            if (rtf[j] == '\'' && j + 2 < rtf.Length)
            {
                if (!skipping) text.Append((char)Convert.ToInt32(rtf.Substring(j + 1, 2), 16)); // \'hh: one byte
                return j + 2;
            }
            bool escapedLiteral = rtf[j] == '\\' || rtf[j] == '{' || rtf[j] == '}';
            if (!skipping && escapedLiteral) text.Append(rtf[j]);
            return j;
        }

        // control word: letters, then an optional (negative) number, then an optional delimiting space
        int wordEnd = j;
        while (wordEnd < rtf.Length && char.IsLetter(rtf[wordEnd])) wordEnd++;
        var word = rtf[j..wordEnd];

        int end = wordEnd;
        if (end < rtf.Length && (rtf[end] == '-' || char.IsDigit(rtf[end])))
        {
            end++;
            while (end < rtf.Length && char.IsDigit(rtf[end])) end++;
        }

        if (!skipping)
        {
            if (word is "par" or "line") text.Append('\n');
            else if (word == "tab") text.Append('\t');
            else if (word == "u" && int.TryParse(rtf[wordEnd..end], out var code))
            {
                text.Append((char)(code < 0 ? code + 65536 : code)); // \uN: UTF-16 unit, negative values wrap
                if (end < rtf.Length && rtf[end] == '?') end++;      // the "?" is the ANSI fallback character
            }
        }

        if (end < rtf.Length && rtf[end] == ' ') end++;
        if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict")
        {
            skipping = true;
            skipDepth = depth;
        }
        return end - 1;
    }
}
