using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[System.Serializable]
public class ItemAbility
{
    public string Prefix; // e.g., "learn.sThief" or "t.jinx.allitem"
    public AbilityData Ability; // Assuming AbilityData is implemented elsewhere
}

// ==========================================
// THE MECHANIC ENGINE (Core Logic Parser)
// ==========================================

[System.Serializable]
public class ItemMechanic
{
    [Header("Raw Engine Data")]
    public string RawString;
    public bool IsWrapped; // Tracks if this was enclosed in ()

    [Header("Parsed Components")]
    public List<string> Positions = new List<string>(); // left, topbot, rightmost, etc.
    public string Operation; // hat, sticker, splice, k, togres, etc.
    public string BaseItem; // Used for standard items or the left side of a splice
    public string Payload; // The target entity, item, or keyword

    // Known dictionary sets for intelligent parsing
    private static readonly HashSet<string> ValidPositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "all", "mid", "left", "right", "top", "bot", "rightmost", "row", "col",
        "topbot", "left2", "mid2", "right2", "right3", "right5"
    };

    private static readonly HashSet<string> TogItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "togtime", "togtarg", "togfri", "togvis", "togeft", "togpip", "togkey",
        "togorf", "togunt", "togres", "togresm", "togresa", "togreso", "togresx",
        "togress", "togresn"
    };

    public static ItemMechanic Parse(string rawData)
    {
        ItemMechanic mech = new ItemMechanic();
        string data = rawData.Trim();

        if (ItemData.IsFullyWrapped(data))
        {
            mech.IsWrapped = true;
            data = data.Substring(1, data.Length - 2);
        }
        mech.RawString = data;

        List<string> tokens = ItemData.SafeSplit(data, '.');
        if (tokens.Count == 0) return mech;

        int i = 0;

        // 1. Extract Positional Prefixes (e.g., "left.right.hat.ogre")
        while (i < tokens.Count && ValidPositions.Contains(tokens[i].ToLower()))
        {
            mech.Positions.Add(tokens[i].ToLower());
            i++;
        }

        // 2. Identify the Core Operation
        if (i < tokens.Count)
        {
            string token = tokens[i].ToLower();

            // Handle "splice" (which appears in the middle: BaseItem.splice.Payload)
            int spliceIndex = tokens.FindIndex(t => t.Equals("splice", StringComparison.OrdinalIgnoreCase));
            if (spliceIndex != -1)
            {
                mech.Operation = "splice";
                mech.BaseItem = string.Join(".", tokens.Skip(i).Take(spliceIndex - i));
                mech.Payload = string.Join(".", tokens.Skip(spliceIndex + 1));
                return mech;
            }

            // Handle Standard Operators
            if (token == "hat" || token == "sticker" || token == "cast" || token == "triggerhpdata" ||
                token == "k" || token == "self" || token == "pertier" || token == "unpack")
            {
                mech.Operation = token;
                mech.Payload = string.Join(".", tokens.Skip(i + 1));
            }
            // Handle Multipliers (e.g., x3.chainmail)
            else if (Regex.IsMatch(token, @"^x\d+$"))
            {
                mech.Operation = token;
                mech.Payload = string.Join(".", tokens.Skip(i + 1));
            }
            // Handle Tog Items (Tog items are often operations with no payload, acting on the scratchpad)
            else if (TogItems.Contains(token))
            {
                mech.Operation = token;
                mech.Payload = string.Join(".", tokens.Skip(i + 1)); // Usually empty unless chained
            }
            // Handle Base Item Modifications (e.g., ghost shield.m.3, shortsword.part.1)
            else if (i + 1 < tokens.Count && (tokens[i + 1].ToLower() == "m" || tokens[i + 1].ToLower() == "part"))
            {
                mech.BaseItem = string.Join(".", tokens.Take(i + 1)); // Includes positions if they were part of the name mistakenly
                mech.Operation = tokens[i + 1].ToLower();
                mech.Payload = string.Join(".", tokens.Skip(i + 2));
            }
            // Default: It's just a standard item or custom entity injection
            else
            {
                mech.Operation = "item";
                mech.BaseItem = string.Join(".", tokens.Skip(i));
            }
        }

        return mech;
    }

    public string Export()
    {
        StringBuilder sb = new StringBuilder();

        // Reconstruct Prefix Positions
        if (Positions.Count > 0)
        {
            sb.Append(string.Join(".", Positions));
            sb.Append(".");
        }

        // Reconstruct Operations
        if (Operation == "splice")
            sb.Append($"{BaseItem}.splice.{Payload}");
        else if (Operation == "item")
            sb.Append(BaseItem);
        else if (Operation == "m" || Operation == "part")
            sb.Append($"{BaseItem}.{Operation}.{Payload}");
        else if (!string.IsNullOrEmpty(Operation))
        {
            sb.Append(Operation);
            if (!string.IsNullOrEmpty(Payload)) sb.Append($".{Payload}");
        }

        string result = sb.ToString();
        return IsWrapped ? $"({result})" : result;
    }
}

// ==========================================
// ITEM DATA (The Wrapper & Metadata)
// ==========================================

[System.Serializable]
public class ItemData : SDData
{
    private static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "n", "tier", "img", "doc", "hsv", "hue", "hsl", "b", "draw", "rect", "thue", "p", "sidesc"
    };

    [Header("Core Info")]
    public int? tier;

    [Header("Item Mechanics")]
    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();

    [Header("Granted Abilities")]
    public List<ItemAbility> GrantedAbilities = new List<ItemAbility>();

    [Header("Aesthetics & Metadata")]
    public int h = 0, s = 0, v = 0;
    public int? hue;
    public string hsl, p, b, rect, draw, thue, doc, sidesc;

    public static string Export(ItemData item)
    {
        if (item == null) return "";
        StringBuilder sb = new StringBuilder();

        // 1. Compile Effects & Abilities
        List<string> allMechanics = new List<string>();

        if (item.Mechanics != null)
        {
            allMechanics.AddRange(item.Mechanics.Select(m => m.Export()));
        }
        if (item.GrantedAbilities != null)
        {
            foreach (var ability in item.GrantedAbilities)
            {
                string prefixStr = string.IsNullOrEmpty(ability.Prefix) ? "" : $"{ability.Prefix}.";
                // Assuming ExportWrapped exists on AbilityData
                allMechanics.Add($"{prefixStr}abilitydata.{ability.Ability.ExportWrapped()}");
            }
        }

        if (allMechanics.Count > 0)
        {
            string joinedEffects = string.Join("#", allMechanics);
            // Wrap if multiple mechanics to prevent bleeding into metadata
            if (allMechanics.Count > 1) sb.Append($"({joinedEffects})");
            else sb.Append(joinedEffects);
        }

        // 2. Metadata (Added backwards-compatible metadata keys like sidesc)
        if (!string.IsNullOrEmpty(item.entityName)) sb.Append($".n.{FormatName(item.entityName)}");
        if (item.tier.HasValue) sb.Append($".tier.{item.tier.Value}");
        if (!string.IsNullOrEmpty(item.doc)) sb.Append($".doc.{item.doc}");
        if (!string.IsNullOrEmpty(item.sidesc)) sb.Append($".sidesc.{item.sidesc}");
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

        // Backwards scan for Metadata
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

            // Clean global wrapping
            while (IsFullyWrapped(rawEffects)) rawEffects = rawEffects.Substring(1, rawEffects.Length - 2);

            List<string> effectChunks = SafeSplit(rawEffects, '#');
            foreach (var effect in effectChunks)
            {
                int abIdx = effect.IndexOf("abilitydata.", StringComparison.OrdinalIgnoreCase);
                if (abIdx != -1)
                {
                    string prefix = abIdx > 0 && effect[abIdx - 1] == '.' ? effect.Substring(0, abIdx - 1) : effect.Substring(0, abIdx);
                    string abilityStr = effect.Substring(abIdx + 12);

                    while (IsFullyWrapped(abilityStr)) abilityStr = abilityStr.Substring(1, abilityStr.Length - 2);

                    item.GrantedAbilities.Add(new ItemAbility
                    {
                        Prefix = prefix,
                        Ability = AbilityData.Parse(abilityStr) // Ensure AbilityData has a standard Parse
                    });
                }
                else
                {
                    // Generate rich AST Mechanic instead of raw string
                    item.Mechanics.Add(ItemMechanic.Parse(effect));
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
                case "n": item.entityName = value; break;
                case "tier": if (int.TryParse(value, out int t)) item.tier = t; break;
                case "img": item.imageOverride = value; break;
                case "doc": item.doc = value; break;
                case "sidesc": item.sidesc = value; break; // Custom Side Description Override
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