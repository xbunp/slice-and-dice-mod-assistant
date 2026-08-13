using System.Collections.Generic;

public static class PhaseStringSplitter
{
    public static List<string> TopLevelSplit(string text, string delimiter)
    {
        var parts = new List<string>();
        if (string.IsNullOrEmpty(text)) return parts;

        int parenDepth = 0;
        int bracketDepth = 0;
        int lastSplit = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;

            if (parenDepth == 0 && bracketDepth == 0 && i <= text.Length - delimiter.Length)
            {
                if (text.Substring(i, delimiter.Length) == delimiter)
                {
                    parts.Add(text.Substring(lastSplit, i - lastSplit));
                    lastSplit = i + delimiter.Length;
                    i += delimiter.Length - 1;
                }
            }
        }

        parts.Add(text.Substring(lastSplit));
        return parts;
    }

    public static string StripOuter(string text, string start, string end)
    {
        text = text.Trim();
        if (text.StartsWith(start) && text.EndsWith(end))
        {
            return text.Substring(start.Length, text.Length - (start.Length + end.Length));
        }
        return text;
    }
}