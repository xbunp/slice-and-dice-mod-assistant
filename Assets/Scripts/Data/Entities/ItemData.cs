using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using ModEditor;

// ==========================================
// BASE DATA & STUBS
// ==========================================

[System.Serializable]
public class ItemAbility
{
    public string Prefix; // Safely catches deep chains like "self.ea.sstatue"
    public AbilityData Ability; // Assuming AbilityData is implemented elsewhere
}

// ==========================================
// THE MECHANIC ENGINE (Upgraded Core AST)
// ==========================================

[System.Serializable]
public class ItemMechanic
{
    [Header("Raw Engine Data")]
    public string RawString;
    public bool IsWrapped;

    [Header("Parsed Components")]
    public List<string> Positions = new List<string>();

    // Upgraded from a single string to a List to support Operator Chaining
    // Example: "t", "jinx", "allitem", "learn"
    public List<string> Operations = new List<string>();

    public string BaseItem;
    public string Payload;

    // --- Parser Dictionaries ---
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

    private static readonly HashSet<string> KnownUnaryOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hat", "sticker", "cast", "triggerhpdata", "onhitdata", "k", "self",
        "pertier", "peritem", "allitem", "alliteme", "unpack", "ea", "learn",
        "t", "replica", "egg", "facade"
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

        // 1. Extract Positional Prefixes (e.g., "right2.mid.hat...")
        while (i < tokens.Count && ValidPositions.Contains(tokens[i].ToLower()))
        {
            mech.Positions.Add(tokens[i].ToLower());
            i++;
        }

        // 2. Extract Operator Chains (e.g., "t.jinx.alliteme.learn...")
        while (i < tokens.Count)
        {
            string t = tokens[i].ToLower();
            if (KnownUnaryOps.Contains(t) || Regex.IsMatch(t, @"^x\d+$") || Regex.IsMatch(t, @"^et\d+$"))
            {
                mech.Operations.Add(tokens[i]); // Preserve original case
                i++;
            }
            else
            {
                break;
            }
        }

        // 3. Scan remaining tokens for Binary Engine Operators
        int binaryOpIdx = -1;
        string foundBinaryOp = null;
        for (int j = i; j < tokens.Count; j++)
        {
            string t = tokens[j].ToLower();
            if (t == "splice" || t == "mrg" || t == "adj" || t == "m" || t == "part")
            {
                binaryOpIdx = j;
                foundBinaryOp = tokens[j]; // Preserve case
                break;
            }
        }

        if (binaryOpIdx != -1)
        {
            // Binary Operator Found: Splits BaseItem and Payload
            mech.Operations.Add(foundBinaryOp);
            mech.BaseItem = string.Join(".", tokens.Skip(i).Take(binaryOpIdx - i));
            mech.Payload = string.Join(".", tokens.Skip(binaryOpIdx + 1));
        }
        else
        {
            // No Binary Operator. It's either a Tog Scratchpad action or a simple entity/item Payload.
            if (i < tokens.Count && TogItems.Contains(tokens[i].ToLower()))
            {
                mech.Operations.Add(tokens[i]);
                mech.Payload = string.Join(".", tokens.Skip(i + 1)); // Usually empty, unless highly customized
            }
            else
            {
                // Unary operators wrap a BaseItem/Entity payload
                if (mech.Operations.Count > 0)
                {
                    mech.Payload = string.Join(".", tokens.Skip(i));
                }
                else
                {
                    mech.BaseItem = string.Join(".", tokens.Skip(i));
                }
            }
        }

        return mech;
    }

    public string Export()
    {
        StringBuilder sb = new StringBuilder();

        // 1. Positions
        if (Positions.Count > 0)
        {
            sb.Append(string.Join(".", Positions));
            sb.Append(".");
        }

        // 2. Unary Operators
        List<string> unaryOps = Operations.Where(op => !IsBinaryOp(op)).ToList();
        if (unaryOps.Count > 0)
        {
            sb.Append(string.Join(".", unaryOps));
            sb.Append(".");
        }

        // 3. Binary Structure OR Payload Output
        string binaryOp = Operations.FirstOrDefault(op => IsBinaryOp(op));
        if (binaryOp != null)
        {
            sb.Append($"{BaseItem}.{binaryOp}.{Payload}");
        }
        else
        {
            if (!string.IsNullOrEmpty(BaseItem)) sb.Append(BaseItem);
            else if (!string.IsNullOrEmpty(Payload)) sb.Append(Payload);
        }

        string result = sb.ToString().TrimEnd('.');
        return IsWrapped ? $"({result})" : result;
    }

    private bool IsBinaryOp(string op)
    {
        string t = op.ToLower();
        return t == "splice" || t == "mrg" || t == "adj" || t == "m" || t == "part";
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

    [Header("Mod-Level System Tags")]
    public bool isHidden = false;
    public string modName;
    public string modDoc;

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

        if (item.Mechanics != null) allMechanics.AddRange(item.Mechanics.Select(m => m.Export()));

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
            if (allMechanics.Count > 1) sb.Append($"({joinedEffects})");
            else sb.Append(joinedEffects);
        }

        // 2. Trailing Local Metadata
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

        // 4. Mod-Level Global Metadata Tags (&)
        if (item.isHidden) sb.Append("&Hidden");
        if (!string.IsNullOrEmpty(item.modName)) sb.Append($".mn.{item.modName}");
        if (!string.IsNullOrEmpty(item.modDoc)) sb.Append($".doc.{item.modDoc}");

        return sb.ToString();
    }

    public static ItemData Parse(string data)
    {
        ItemData item = new ItemData();
        if (string.IsNullOrWhiteSpace(data)) return item;

        // 1. Intercept Mod-Level '&' Triggers First
        List<string> fileChunks = SafeSplit(data.Trim(), '&');
        string itemCore = fileChunks[0];

        // Parse global mod-flags (e.g. &Hidden.mn.CommunityItems.doc.Q1)
        for (int c = 1; c < fileChunks.Count; c++)
        {
            string chunk = fileChunks[c];
            if (chunk.StartsWith("Hidden", StringComparison.OrdinalIgnoreCase))
            {
                item.isHidden = true;
                List<string> hiddenTokens = SafeSplit(chunk, '.');
                for (int j = 1; j < hiddenTokens.Count; j++)
                {
                    if (hiddenTokens[j].ToLower() == "mn" && j + 1 < hiddenTokens.Count)
                    {
                        item.modName = hiddenTokens[j + 1];
                        j++;
                    }
                    else if (hiddenTokens[j].ToLower() == "doc" && j + 1 < hiddenTokens.Count)
                    {
                        item.modDoc = hiddenTokens[j + 1];
                        j++;
                    }
                }
            }
        }

        // 2. Parse Standard Dot-Tokens from the Core Item String
        List<string> tokens = SafeSplit(itemCore, '.');

        // Backwards scan for Local Metadata
        int metaIdx = tokens.Count;
        while (metaIdx >= 2)
        {
            if (MetadataKeys.Contains(tokens[metaIdx - 2].ToLower())) metaIdx -= 2;
            else break;
        }

        // 3. Parse Mechanical Effects & Deep Operator Chains
        if (metaIdx > 0)
        {
            string rawEffects = string.Join(".", tokens.Take(metaIdx));

            while (IsFullyWrapped(rawEffects)) rawEffects = rawEffects.Substring(1, rawEffects.Length - 2);

            List<string> effectChunks = SafeSplit(rawEffects, '#');
            foreach (var effect in effectChunks)
            {
                // Isolate inline Ability definitions before AST processing
                int abIdx = effect.IndexOf("abilitydata.", StringComparison.OrdinalIgnoreCase);
                if (abIdx != -1)
                {
                    string prefix = abIdx > 0 && effect[abIdx - 1] == '.' ? effect.Substring(0, abIdx - 1) : effect.Substring(0, abIdx);
                    string abilityStr = effect.Substring(abIdx + 12);

                    while (IsFullyWrapped(abilityStr)) abilityStr = abilityStr.Substring(1, abilityStr.Length - 2);

                    item.GrantedAbilities.Add(new ItemAbility
                    {
                        Prefix = prefix,
                        Ability = AbilityData.Parse(abilityStr)
                    });
                }
                else
                {
                    item.Mechanics.Add(ItemMechanic.Parse(effect));
                }
            }
        }

        // 4. Parse Extracted Local Metadata
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
                case "sidesc": item.sidesc = value; break;
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

    // --- Utilities ---
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