using System;
using System.Text;
using System.Text.RegularExpressions;

public abstract class IdeSyntaxConfig
{
    public abstract Regex SyntaxRegex { get; }
    public abstract string DefaultTextColor { get; }

    // Intercepts and processes matched groups with custom coloring segments
    public abstract void ProcessMatch(
        Match match,
        string input,
        StringBuilder sb,
        Action<StringBuilder, string, int, int, string> accumulateText
    );
}