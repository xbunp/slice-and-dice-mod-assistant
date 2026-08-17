using System;
using System.Collections.Generic;
using System.Linq;

public interface IPayloadContainer
{
    List<string> BaseItems { get; }
    List<string> Traits { get; }
    List<string> Curses { get; }
    List<string> Blessings { get; }
    List<string> BaseAbilities { get; }
    List<CustomPayload> CustomPayloads { get; }
}

public enum PayloadType { Item, Hero, Monster, Ability, Modifier }

[System.Serializable]
public class CustomPayload
{
    public string Prefix;
    public object Data;
    public PayloadType Type;

    public string Export()
    {
        if (Data is SDData sd)
        {
            string exported = sd.Export();
            if (string.IsNullOrEmpty(Prefix)) return exported;
            if (exported.StartsWith($"{Prefix}.", StringComparison.OrdinalIgnoreCase)) return exported;
            return $"{Prefix}.{exported}";
        }
        return "";
    }
}

// ---------------------------------------------------------
// THE TOKEN STREAM (Bug Tracker / Index Encapsulator)
// ---------------------------------------------------------
public class TokenStream
{
    private readonly List<string> _tokens;
    public int Index { get; private set; }

    public TokenStream(List<string> tokens)
    {
        _tokens = tokens;
        Index = 0;
    }

    public bool IsEOF => Index >= _tokens.Count;
    public string Peek() => IsEOF ? "" : _tokens[Index];
    public string PeekNext() => Index + 1 >= _tokens.Count ? "" : _tokens[Index + 1];

    public string Consume() => IsEOF ? "" : _tokens[Index++];
    public string ConsumeNext() { Consume(); return Consume(); }

    // Backwards compatibility for external block-length evaluators
    public List<string> GetRawList() => _tokens;
    public void Advance(int amount) => Index += amount;

    public List<string> ConsumeRange(int count)
    {
        int safeCount = Math.Min(count, _tokens.Count - Index);
        var range = _tokens.GetRange(Index, safeCount);
        Index += safeCount;
        return range;
    }
}

public static class StaticBranchTracing
{
    public static List<string> TopLevelSplit(string input, char separator)
    {
        List<string> result = new List<string>();
        int p = 0, b = 0, br = 0, start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '[') b++;
            else if (c == ']') b--;
            else if (c == '{') br++;
            else if (c == '}') br--;
            else if (c == separator && p == 0 && b == 0 && br == 0)
            {
                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }
        }
        result.Add(input.Substring(start));
        return result;
    }

    public static string StripOuterParens(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string t = text.Trim();
        while (t.StartsWith("(") && t.EndsWith(")"))
        {
            int depth = 0; bool matching = true;
            for (int k = 0; k < t.Length - 1; k++)
            {
                if (t[k] == '(') depth++; else if (t[k] == ')') depth--;
                if (depth == 0) { matching = false; break; }
            }
            if (matching) t = t.Substring(1, t.Length - 2).Trim();
            else break;
        }
        return t;
    }


    public static string SafeBracket(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;
        string stripped = StripOuterParens(content);

        int depth = 0;
        for (int i = 0; i < stripped.Length; i++)
        {
            if (stripped[i] == '(') depth++;
            else if (stripped[i] == ')') depth--;
            else if (depth == 0)
            {
                // Top-level delimiters dictate the expression is complex and MUST be wrapped
                if (stripped[i] == '#' || stripped[i] == ':' || stripped[i] == '-' || stripped[i] == '&')
                    return $"({stripped})";
            }
        }
        return stripped;
    }

    public static bool IsMonsterEntity(string core)
    {
        if (string.IsNullOrEmpty(core)) return false;
        string firstToken = TopLevelSplit(core, '.')[0].ToLower();
        while (firstToken.StartsWith("(") && firstToken.EndsWith(")"))
        {
            string stripped = StripOuterParens(firstToken);
            if (stripped == firstToken) break;
            firstToken = stripped;
            firstToken = TopLevelSplit(firstToken, '.')[0].ToLower();
        }

        if (firstToken.StartsWith("x") && firstToken.Length > 1 && char.IsDigit(firstToken[1]))
        {
            var tokens = TopLevelSplit(core, '.');
            if (tokens.Count > 1)
            {
                firstToken = tokens[1].ToLower();
                while (firstToken.StartsWith("(") && firstToken.EndsWith(")"))
                {
                    string stripped = StripOuterParens(firstToken);
                    if (stripped == firstToken) break;
                    firstToken = stripped;
                    firstToken = TopLevelSplit(firstToken, '.')[0].ToLower();
                }
            }
        }

        if (firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx" || firstToken == "rmon") return true;

        if (firstToken == "replica") return false;

        foreach (string monsterName in EntityHelper.FormattedMonsterNames)
            if (string.Equals(firstToken, monsterName, StringComparison.OrdinalIgnoreCase)) return true;

        if (firstToken.Contains("jinx") || firstToken.Contains("vase") || firstToken.Contains("orb") || firstToken.Contains("rmon")) return true;

        return false;
    }

    public static bool IsHeroEntity(string core)
    {
        if (string.IsNullOrEmpty(core)) return false;
        List<string> tokens = TopLevelSplit(core, '.');
        string firstToken = tokens[0].ToLower();
        while (firstToken.StartsWith("(") && firstToken.EndsWith(")"))
        {
            string stripped = StripOuterParens(firstToken);
            if (stripped == firstToken) break; // PREVENT INFINITE LOOP
            firstToken = stripped;
            tokens = TopLevelSplit(firstToken, '.');
            firstToken = tokens[0].ToLower();
        }
        if (EntityHelper.HeroNames.Contains(firstToken)) return true;
        if (firstToken == "replica" && tokens.Count > 1)
        {
            string secondToken = tokens[1].ToLower();
            while (secondToken.StartsWith("(") && secondToken.EndsWith(")"))
            {
                string stripped = StripOuterParens(secondToken);
                if (stripped == secondToken) break; // PREVENT INFINITE LOOP
                secondToken = stripped;
                secondToken = TopLevelSplit(secondToken, '.')[0].ToLower();
            }
            if (EntityHelper.HeroNames.Contains(secondToken)) return true;
        }
        return false;
    }
}