using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ==========================================
// 1. INTERFACES & SHARED ARCHITECTURE
// ==========================================

public interface IPayloadContainer
{
    List<string> BaseItems { get; }
    List<string> Traits { get; }
    List<string> Curses { get; }
    List<string> Blessings { get; }
    List<string> BaseAbilities { get; }
    List<CustomPayload> CustomPayloads { get; }
}

public enum PayloadType
{
    Item,
    Hero,
    Monster,
    Ability,
    Modifier
}

[System.Serializable]
public class CustomPayload
{
    public string Prefix;
    public object Data;
    public PayloadType Type; // <-- Added explicit classification for UIs and Compilers

    public string Export()
    {
        if (Data is SDData sd)
        {
            if (string.IsNullOrEmpty(Prefix)) return sd.Export();
            if (Prefix == "add") return $"add.({sd.Export()})";
            return $"{Prefix}.({sd.Export()})";
        }
        return "";
    }
}

public static class StaticBranchTracing
{
    public static void ProcessAndRouteProperty(
        List<string> tokens,
        ref int i,
        HashSet<string> boundaryKeys,
        IPayloadContainer dest)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();

            // Allow immediate collection subtypes to chain without breaking (e.g. i.t.jinx)
            if (endIndex == startIndex && (peek == "t" || peek == "gift" || peek == "learn" || peek == "abilitydata"))
            {
                endIndex++; continue;
            }

            if (boundaryKeys.Contains(peek)) break;
            endIndex++;
        }

        int count = endIndex - startIndex;
        if (count == 0) return;

        List<string> itemTokens = tokens.GetRange(startIndex, count);
        string payload = string.Join(".", itemTokens);
        i = endIndex - 1; // Advance the outer iterator

        string propKey = itemTokens[0].ToLower();

        // 1. Trait / Curse routing
        if (propKey == "t" && itemTokens.Count > 1)
        {
            if (itemTokens[1].ToLower() == "jinx" && itemTokens.Count > 2)
            {
                string jinxPayload = string.Join(".", itemTokens.Skip(2));
                if (jinxPayload.Contains("("))
                {
                    ModifierData mod = new ModifierData(); mod.Parse(jinxPayload);
                    dest.CustomPayloads.Add(new CustomPayload { Prefix = "t.jinx", Data = mod, Type = PayloadType.Modifier });
                }
                else dest.Curses.AddRange(TopLevelSplit(jinxPayload, '#'));
                return;
            }

            string tPayload = string.Join(".", itemTokens.Skip(1));
            if (tPayload.Contains("("))
            {
                ModifierData mod = new ModifierData(); mod.Parse(tPayload);
                dest.CustomPayloads.Add(new CustomPayload { Prefix = "t", Data = mod, Type = PayloadType.Modifier });
            }
            else dest.Traits.AddRange(TopLevelSplit(tPayload, '#'));
            return;
        }

        // 2. Blessing & Learn routing
        if (propKey == "gift" && itemTokens.Count > 1)
        {
            dest.Blessings.AddRange(TopLevelSplit(string.Join(".", itemTokens.Skip(1)), '#'));
            return;
        }
        if ((propKey == "learn" || propKey == "abilitydata") && itemTokens.Count > 1)
        {
            string abPayload = string.Join(".", itemTokens.Skip(1));
            if (abPayload.StartsWith("("))
                dest.CustomPayloads.Add(new CustomPayload { Prefix = "abilitydata", Data = AbilityData.CreateAbility(abPayload), Type = PayloadType.Ability });
            else
                dest.BaseAbilities.AddRange(TopLevelSplit(abPayload, '#'));
            return;
        }

        // 3. Standard Items
        bool isComplexItem = payload.StartsWith("(") || (payload.Contains("(") && TopLevelSplit(payload, '#').Any(p => p.Contains(".")));
        if (TopLevelSplit(payload, '.')[0].ToLower() == "k") isComplexItem = false; // i.k.keyword exception

        if (isComplexItem)
        {
            ItemData item = new ItemData();
            item.Parse(payload);
            dest.CustomPayloads.Add(new CustomPayload { Prefix = "i", Data = item, Type = PayloadType.Item });
        }
        else
        {
            dest.BaseItems.AddRange(TopLevelSplit(payload, '#'));
        }
    }

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

    public static bool IsMonsterEntity(string core)
    {
        if (string.IsNullOrEmpty(core) || core.Contains("replica", StringComparison.OrdinalIgnoreCase)) return false;
        string firstToken = TopLevelSplit(core, '.')[0].ToLower();

        while (firstToken.StartsWith("(") && firstToken.EndsWith(")"))
        {
            firstToken = StripOuterParens(firstToken);
            firstToken = TopLevelSplit(firstToken, '.')[0].ToLower();
        }

        if (firstToken == "replica") return false;
        if (firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx" || firstToken == "rmon") return true;

        foreach (string monsterName in MonsterHelper.FormattedMonsterNames)
        {
            if (string.Equals(firstToken, monsterName, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (firstToken.Contains("jinx") || firstToken.Contains("vase") || firstToken.Contains("orb") || firstToken.Contains("rmon")) return true;

        return false;
    }
}