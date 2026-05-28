using System;
using System.Text;
using System.Text.RegularExpressions;

public class SDTextmodSyntaxConfig : IdeSyntaxConfig
{
    private static readonly Regex _regex = new Regex(
        @"(?<=^|[(&@!~\[])(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
        @"|(?<phase>ph\.[!0-9bcedglrstz]|\.phi\b)" +
        @"|(?<delimiter>&|@\d+|;|\b(?i:Hidden|skip(?: all)?|temporary|Delevel|Level Up|No Flee)\b)" +
        @"|(?<sq_bracket>\[[^\]]*\])" +
        @"|(?<bracket>[{}()])" +
        @"|(?<itempool_block>itempool\.[^\.]*)" +
        @"|(?<sd_block>sd\.[^.)]*)" +
        @"|(?<ritemx>(?i)(?:i\.)?ritemx\.[^.)]*)" +
        @"|(?<hsv_block>(?i)hsv\.[^\.]*)" +
        @"|(?<k_block>k\.[^.#]*)" +
        @"|(?<tog>\btog[a-zA-Z0-9_]*\b)" +
        @"|(?<method>\b(?:img|col|n|tier|facade|sidesc|heropool|learn|hp|bal|mn|hat|abilitydata|replica|h|ch|hsv|part|difficulty|diff|splice|jinx|allitem|self|p|topbot|brittle|left|right|row|all|right5|right3|right2|mid2|left2|rightmost|bot|top|mid)\.|\.(?:modtier|add|doc|all|egg|hsv|speech|unpack)\b)" +
        @"|(?<reward>\(?[miglrqovs]\.[a-zA-Z0-9_\-~\^\/ ]*\)?|(?<=[\!&@+=])\(?[miglrqovs]\b\)?|\(?[miglrqovs][a-zA-Z0-9_\-~\^\/]*[\~^\/][a-zA-Z0-9_\-~\^\/]*\)?)" +
        @"|(?<number>\b\d+\b)" +
        @"|(?<text>[a-zA-Z_][a-zA-Z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    public override Regex SyntaxRegex => _regex;
    public override string DefaultTextColor => ColorText;

    private const string ColorFloor = "#569CD6";
    private const string ColorPhasePrefix = "#569CD6";
    private const string ColorPhaseCode = "#4EC9B0";
    private const string ColorDelimiter = "#C586C0";
    private const string ColorNumber = "#B5CEA8";
    private const string ColorText = "#D4D4D4";
    private const string ColorBracket = "#569cd6";
    private const string ColorMethod = "#dcdcaa";
    private const string ColorSdRed = "#FFA961";
    private const string ColorMossGreen = "#A4C365";
    private const string ColorNeonGreen = "#39FF14";
    private const string ColorMod = "#CE9178";
    private const string ColorItem = "#9CDCFE";
    private const string ColorLvl = "#B5CEA8";
    private const string ColorHero = "#DCDCAA";
    private const string ColorRand = "#D8A0DF";
    private const string ColorValue = "#4FC1FF";
    private const string ColorSkip = "#FF7575";
    private const string ColorDefaultReward = "#FFD700";

    public override void ProcessMatch(
        Match m,
        string input,
        StringBuilder sb,
        Action<StringBuilder, string, int, int, string> accumulateText
    )
    {
        int idx = m.Index;
        int len = m.Length;

        if (m.Groups["floor"].Success) accumulateText(sb, input, idx, len, ColorFloor);
        else if (m.Groups["phase"].Success)
        {
            if (input[idx] == 'p' && len > 3)
            {
                accumulateText(sb, input, idx, 3, ColorPhasePrefix);
                accumulateText(sb, input, idx + 3, len - 3, ColorPhaseCode);
            }
            else accumulateText(sb, input, idx, len, ColorPhasePrefix);
        }
        else if (m.Groups["delimiter"].Success) accumulateText(sb, input, idx, len, ColorDelimiter);
        else if (m.Groups["sq_bracket"].Success || m.Groups["bracket"].Success) accumulateText(sb, input, idx, len, ColorBracket);
        else if (m.Groups["itempool_block"].Success)
        {
            accumulateText(sb, input, idx, 9, ColorMethod);
            accumulateText(sb, input, idx + 9, len - 9, ColorItem);
        }
        else if (m.Groups["sd_block"].Success)
        {
            accumulateText(sb, input, idx, 3, ColorMethod);
            accumulateText(sb, input, idx + 3, len - 3, ColorSdRed);
        }
        else if (m.Groups["ritemx"].Success) accumulateText(sb, input, idx, len, ColorItem);
        else if (m.Groups["hsv_block"].Success)
        {
            accumulateText(sb, input, idx, 4, ColorMethod);
            accumulateText(sb, input, idx + 4, len - 4, ColorNumber);
        }
        else if (m.Groups["k_block"].Success) accumulateText(sb, input, idx, len, ColorMossGreen);
        else if (m.Groups["tog"].Success) accumulateText(sb, input, idx, len, ColorNeonGreen);
        else if (m.Groups["method"].Success) accumulateText(sb, input, idx, len, ColorMethod);
        else if (m.Groups["reward"].Success)
        {
            char tagChar = GetRewardTagChar(input, idx, len);
            string rewardColor = tagChar switch
            {
                'm' => ColorMod,
                'i' => ColorItem,
                'l' => ColorLvl,
                'g' => ColorHero,
                'r' => ColorRand,
                'q' => ColorRand,
                'o' => ColorRand,
                'v' => ColorValue,
                's' => ColorSkip,
                _ => ColorDefaultReward
            };
            accumulateText(sb, input, idx, len, rewardColor);
        }
        else if (m.Groups["number"].Success) accumulateText(sb, input, idx, len, ColorNumber);
        else if (m.Groups["text"].Success) accumulateText(sb, input, idx, len, ColorText);
        else accumulateText(sb, input, idx, len, null);
    }

    private char GetRewardTagChar(string input, int start, int length)
    {
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            if (input[i] != '(') return input[i];
        }
        return '\0';
    }
}