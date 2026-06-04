using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// ==========================================
// ITEM DATA
// ==========================================

[System.Serializable]
public class ItemAbility
{
    public string Prefix; // e.g., "learn.sThief" or "t.jinx.allitem"
    public AbilityData Ability;
}

[System.Serializable]
public class ItemData
{
    // These keys define the boundary of an item's top-level metadata.
    // Anything NOT in this list is treated as part of the mechanical effect chain.
    private static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "n", "tier", "img", "doc", "hsv", "hue", "hsl", "b", "draw", "rect", "thue", "p"
    };

    [Header("Core Info")]
    public string itemName;
    public int? tier;
    public string imageOverride;

    [Header("Item Effects")]
    public List<string> effects = new List<string>();

    [Header("Granted Abilities")]
    public List<ItemAbility> GrantedAbilities = new List<ItemAbility>();

    [Header("Aesthetics")]
    public int h = 0, s = 0, v = 0;
    public int? hue;
    public string hsl, p, b, rect, draw, thue, doc;

    public static string Export(ItemData item)
    {
        if (item == null) return "";
        StringBuilder sb = new StringBuilder();

        // 1. Compile Effects & Abilities
        List<string> allMechanics = new List<string>();
        if (item.effects != null)
        {
            allMechanics.AddRange(item.effects.Where(e => !string.IsNullOrWhiteSpace(e)));
        }
        if (item.GrantedAbilities != null)
        {
            foreach (var ability in item.GrantedAbilities)
            {
                string prefixStr = string.IsNullOrEmpty(ability.Prefix) ? "" : $"{ability.Prefix}.";
                allMechanics.Add($"{prefixStr}abilitydata.{ability.Ability.ExportWrapped()}");
            }
        }

        if (allMechanics.Count > 0)
        {
            string joinedEffects = string.Join("#", allMechanics);
            // Wrap if there are multiple mechanics to prevent bleeding
            if (allMechanics.Count > 1) sb.Append($"({joinedEffects})");
            else sb.Append(joinedEffects);
        }

        // 2. Metadata
        if (!string.IsNullOrEmpty(item.itemName)) sb.Append($".n.{FormatName(item.itemName)}");
        if (item.tier.HasValue) sb.Append($".tier.{item.tier.Value}");
        if (!string.IsNullOrEmpty(item.doc)) sb.Append($".doc.{item.doc}");
        if (!string.IsNullOrEmpty(item.imageOverride)) sb.Append($".img.{item.imageOverride}");

        // 3. Colors & Drawing
        if (item.h != 0 || item.s != 0 || item.v != 0) sb.Append($".hsv.{item.h}:{item.s}:{item.v}");
        else if (item.hue.HasValue) sb.Append($".hue.{item.hue.Value}");
        else if (!string.IsNullOrEmpty(item.hsl)) sb.Append($".hsl.{item.hsl}");

        if (!string.IsNullOrEmpty(item.p)) sb.Append($".p.{item.p}");
        if (!string.IsNullOrEmpty(item.b)) sb.Append($".b.{item.b}");
        if (!string.IsNullOrEmpty(item.rect)) sb.Append($".rect.{item.rect}");
        if (!string.IsNullOrEmpty(item.draw)) sb.Append($".draw.{item.draw}");
        if (!string.IsNullOrEmpty(item.thue)) sb.Append($".thue.{item.thue}");

        return sb.ToString();
    }

    public static ItemData Parse(string data)
    {
        ItemData item = new ItemData();
        if (string.IsNullOrWhiteSpace(data)) return item;

        List<string> tokens = SafeSplit(data.Trim(), '.');

        // We scan BACKWARDS through the dot-delimited tokens to isolate the Metadata blocks.
        // We do this because custom item effects can contain dots natively (e.g. "Leather_Vest.m.2"), 
        // but top-level metadata values (like ".tier.5") are always guaranteed to be structured as 
        // trailing key-value pairs at the very end of the raw string.
        int metaIdx = tokens.Count;
        while (metaIdx >= 2)
        {
            if (MetadataKeys.Contains(tokens[metaIdx - 2].ToLower())) metaIdx -= 2;
            else break;
        }

        // Parse Mechanical Effects & Abilities
        if (metaIdx > 0)
        {
            string rawEffects = string.Join(".", tokens.Take(metaIdx));

            // Clean up any outer-most grouping brackets before checking for chained '#' operators
            while (IsFullyWrapped(rawEffects)) rawEffects = rawEffects.Substring(1, rawEffects.Length - 2);

            List<string> effectChunks = SafeSplit(rawEffects, '#');
            foreach (var effect in effectChunks)
            {
                // Identify if this effect segment grants a custom ability
                int abIdx = effect.IndexOf("abilitydata.", StringComparison.OrdinalIgnoreCase);
                if (abIdx != -1)
                {
                    // Extract any custom prefix logic (e.g., "learn.sThief" or "t.jinx.allitem")
                    string prefix = abIdx > 0 && effect[abIdx - 1] == '.' ? effect.Substring(0, abIdx - 1) : effect.Substring(0, abIdx);
                    string abilityStr = effect.Substring(abIdx + 12); // length of "abilitydata."

                    while (IsFullyWrapped(abilityStr)) abilityStr = abilityStr.Substring(1, abilityStr.Length - 2);

                    item.GrantedAbilities.Add(new ItemAbility
                    {
                        Prefix = prefix,
                        Ability = AbilityData.Parse(abilityStr)
                    });
                }
                else
                {
                    item.effects.Add(effect);
                }
            }
        }

        // Parse Metadata
        for (int i = metaIdx; i < tokens.Count; i += 2)
        {
            string key = tokens[i].ToLower();
            string value = (i + 1 < tokens.Count) ? tokens[i + 1] : "";

            switch (key)
            {
                case "n": item.itemName = value; break;
                case "tier": if (int.TryParse(value, out int t)) item.tier = t; break;
                case "img": item.imageOverride = value; break;
                case "doc": item.doc = value; break;
                case "hsv":
                    string[] hsv = value.Split(':');
                    if (hsv.Length == 3) { int.TryParse(hsv[0], out item.h); int.TryParse(hsv[1], out item.s); int.TryParse(hsv[2], out item.v); }
                    break;
                case "hsl": item.hsl = value; break;
                case "hue": if (int.TryParse(value, out int hVal)) item.hue = hVal; break;
                case "p": item.p = value; break;
                case "b": item.b = value; break;
                case "rect": item.rect = value; break;
                case "draw": item.draw = value; break;
                case "thue": item.thue = value; break;
            }
        }

        return item;
    }

    // --- Utilites ---
    private static string FormatName(string name) => string.IsNullOrEmpty(name) ? "" : name.Replace(" ", "_");

    /// <summary>
    /// Splits a string by a character, but ONLY if the character is not trapped inside 
    /// parentheses (), brackets [], or braces {}. This protects nested internal parameters.
    /// </summary>
    public static List<string> SafeSplit(string input, char separator)
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

    /// <summary>
    /// Checks if a string is completely enclosed by balanced outer-most parentheses.
    /// Used to strip unnecessary grouping syntax.
    /// </summary>
    public static bool IsFullyWrapped(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 2 || s[0] != '(' || s[s.Length - 1] != ')') return false;
        int p = 0;
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (s[i] == '(') p++; else if (s[i] == ')') p--;
            if (p <= 0 && i > 0) return false;
        }
        return p == 1;
    }
}
