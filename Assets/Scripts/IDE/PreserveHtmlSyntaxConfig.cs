using System;
using System.Text;
using System.Text.RegularExpressions;

public class PreserveHtmlSyntaxConfig : IdeSyntaxConfig
{
    // Matches the entire input text so it is processed in one block
    private static readonly Regex _matchAllRegex = new Regex(@"[\s\S]+", RegexOptions.Compiled);

    public override Regex SyntaxRegex => _matchAllRegex;
    public override string DefaultTextColor => null;

    public override void ProcessMatch(
        Match match,
        string input,
        StringBuilder sb,
        Action<StringBuilder, string, int, int, string> accumulateText
    )
    {
        // By appending directly to 'sb' instead of calling 'accumulateText',
        // we bypass the internal HTML escaping process.
        sb.Append(input, match.Index, match.Length);
    }
}