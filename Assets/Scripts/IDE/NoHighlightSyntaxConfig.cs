using System;
using System.Text;
using System.Text.RegularExpressions;

public class NoHighlightSyntaxConfig : IdeSyntaxConfig
{
    // A regex that never matches, preventing any syntax highlights from triggering
    private static readonly Regex _noMatchRegex = new Regex(@"(?!)", RegexOptions.Compiled);

    public override Regex SyntaxRegex => _noMatchRegex;
    public override string DefaultTextColor => null; // Keeps default rendering colors

    public override void ProcessMatch(
        Match match,
        string input,
        StringBuilder sb,
        Action<StringBuilder, string, int, int, string> accumulateText
    )
    {
        // This will not be hit since the regex never matches, 
        // but if it is, we pass the text through without styling.
        accumulateText(sb, input, match.Index, match.Length, null);
    }
}