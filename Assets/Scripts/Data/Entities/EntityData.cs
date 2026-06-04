using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public abstract class EntityData : SDData
{
    [Header("Core Shared Info")]
    public int hp = 7;

    [Header("Colors (Mutually Exclusive)")]
    public int h = 0;
    public int s = 0;
    public int v = 0;
    public int? hue;
    public string hsl;

    [Header("Shared Extended Modifiers")] 
    public List<string> items = new List<string>();     // i
    public List<ItemData> customItems = new List<ItemData>();     // i
    public List<string> traits = new List<string>();    // t
    public List<string> blessings = new List<string>();     // gift.    
    public List<string> curses = new List<string>();     // i.t.jinx.<curse>

    public string p;
    public string b;
    public string rect;
    public string draw;
    public string thue;

    [Header("Post-Dice Info")]
    public string doc;

    public DiceSideData[] diceSides = new DiceSideData[6];

    public EntityData()
    {
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData();
    }

    // ==========================================
    // SHARED STRING BUILDERS
    // ==========================================
    protected void AppendColorModifier(StringBuilder sb)
    {
        bool hasHsv = h != 0 || s != 0 || v != 0;
        if (hasHsv) sb.Append($".hsv.{h}:{s}:{v}");
        else if (hue.HasValue) sb.Append($".hue.{hue.Value}");
        else if (!string.IsNullOrEmpty(hsl)) sb.Append($".hsl.{hsl}");
    }

    protected void AppendListAsChained(StringBuilder sb, string prefix, List<string> list)
    {
        if (list == null || list.Count == 0) return;
        List<string> validItems = new List<string>();
        foreach (var item in list)
        {
            if (!string.IsNullOrEmpty(item)) validItems.Add(item);
        }
        if (validItems.Count > 0)
        {
            sb.Append($".{prefix}.{string.Join("#", validItems)}");
        }
    }

    protected void AppendDiceSides(StringBuilder sb)
    {
        sb.Append(".sd.");
        for (int i = 0; i < 6; i++)
        {
            var side = diceSides[i];
            if (side.effectID == 0) sb.Append("0");
            else sb.Append($"{side.effectID}-{side.pips}");

            if (i < 5) sb.Append(":");
        }
    }

    protected string BuildFaceModifiers(bool allowFacade)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            List<string> chunks = new List<string>();

            foreach (var kw in face.keywords)
            {
                if (!string.IsNullOrWhiteSpace(kw)) chunks.Add($"k.{kw.Trim().ToLower()}");
            }

            // Heroes can use facade keywords for dice sides, monsters strictly cannot.
            if (allowFacade && !string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";
                if (!string.IsNullOrWhiteSpace(face.facadeColor)) facStr += $":{face.facadeColor}";
                chunks.Add(facStr);
            }

            if (chunks.Count > 0)
            {
                string modString = string.Join("#", chunks);
                int faceMask = 1 << i;

                if (groupedModifiers.ContainsKey(modString)) groupedModifiers[modString] |= faceMask;
                else groupedModifiers[modString] = faceMask;
            }
        }

        foreach (var kvp in groupedModifiers)
        {
            string modString = kvp.Key;
            int mask = kvp.Value;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(mask);
            foreach (string alias in optimalAliases)
            {
                modSb.Append($".i.{alias}.{modString}");
            }
        }

        return modSb.ToString();
    }

    // ==========================================
    // SHARED PARSING TOOLS
    // ==========================================
    protected static List<string> TokenizeString(string data)
    {
        List<string> tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(data)) return tokens;

        data = data.Trim();
        if (data.StartsWith("(")) data = data.Substring(1);
        if (data.EndsWith(")")) data = data.Substring(0, data.Length - 1);

        int depth = 0;
        int startIndex = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == '(') depth++;
            else if (data[i] == ')') depth--;
            else if (data[i] == '.' && depth == 0)
            {
                tokens.Add(data.Substring(startIndex, i - startIndex));
                startIndex = i + 1;
            }
        }
        tokens.Add(data.Substring(startIndex));
        return tokens;
    }

    protected static string FormatName(string name)
    {
        return name ?? "";
    }
}