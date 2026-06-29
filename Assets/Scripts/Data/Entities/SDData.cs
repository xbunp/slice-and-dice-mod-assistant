using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Thue
{
    public Color colorHex;
    public int colorRange;
    public int colorOffset;
}

[System.Serializable]
public abstract class SDData
{
    public string entityName = "";
    public string imageOverride = "None";

    public string doc;
    public int h, s, v;
    public int hue;
    public string p, b, draw, rect;
    public Thue thue = new Thue();

    [Header("Deep Payloads")]
    public List<CustomPayload> customPayloads = new List<CustomPayload>();
    public List<ItemData> customItems =>
        customPayloads?.Where(p => p.Type == PayloadType.Item).Select(p => p.Data as ItemData).ToList() ?? new List<ItemData>();
    public List<AbilityData> customAbilities =>
        customPayloads?.Where(p => p.Type == PayloadType.Ability).Select(p => p.Data as AbilityData).ToList() ?? new List<AbilityData>();
    public List<HeroData> customHeroes =>
        customPayloads?.Where(p => p.Type == PayloadType.Hero).Select(p => p.Data as HeroData).ToList() ?? new List<HeroData>();
    public List<MonsterData> customMonsters =>
        customPayloads?.Where(p => p.Type == PayloadType.Monster).Select(p => p.Data as MonsterData).ToList() ?? new List<MonsterData>();

    public virtual string Export()
    {
        return $"n.{entityName}.img.{imageOverride}";
    }

    public virtual void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        string[] tokens = data.Split('.');
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            string token = tokens[i].ToLower();
            if (token == "n")
            {
                entityName = tokens[++i];
            }
            else if (token == "img")
            {
                imageOverride = tokens[++i];
            }
        }
    }

    protected void ProcessRecursiveParentheses(string originalToken, Action<List<string>> processingDelegate)
    {
        string inner = originalToken.Substring(1, originalToken.Length - 2);
        List<string> innerChains = StaticBranchTracing.TopLevelSplit(inner, '#');
        foreach (var chain in innerChains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> innerTokens = StaticBranchTracing.TopLevelSplit(chain, '.');
            processingDelegate(innerTokens);
        }
    }

    protected bool TryProcessCommonMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        string nextVal = tokens[i + 1];

        switch (tokenLower)
        {
            case "n": entityName = nextVal; break;
            case "img": imageOverride = nextVal; break;
            case "doc": doc = nextVal; break;
            case "hsv":
                string[] hsvParts = nextVal.Split(':');
                if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out h) && int.TryParse(hsvParts[1], out s) && int.TryParse(hsvParts[2], out v)) { }
                break;
            case "hue": if (int.TryParse(nextVal, out int hVal)) hue = hVal; break;

            case "thue": thue = UnpackTHue(nextVal); break;

            case "p": p = nextVal; break;
            case "b": b = nextVal; break;
            case "draw": draw = nextVal; break;
            case "rect": rect = nextVal; break;
            default: return false;
        }
        i++; // Consume the value token
        return true;
    }
    protected static string FormatName(string name) => name?.Replace(" ", "") ?? "";

    protected static string PackTHue(Thue thue)
    {
        if (thue == null) return string.Empty;

        int r = UnityEngine.Mathf.RoundToInt(thue.colorHex.r * 255f);
        int g = UnityEngine.Mathf.RoundToInt(thue.colorHex.g * 255f);
        int b = UnityEngine.Mathf.RoundToInt(thue.colorHex.b * 255f);

        string hex;
        // Hex double-digit pairs (0x00, 0x11, ... 0xFF) are always multiples of 17 (0, 17, ... 255)
        if (r % 17 == 0 && g % 17 == 0 && b % 17 == 0)
        {
            hex = $"{(r / 17):x}{(g / 17):x}{(b / 17):x}";
        }
        else
        {
            hex = UnityEngine.ColorUtility.ToHtmlStringRGB(thue.colorHex).ToLower();
        }

        // FIX: Pad the middle range value to always be 2 characters (e.g., 5 becomes "05")
        string rangeStr = thue.colorRange.ToString("D2");

        return $"thue.{hex}:{rangeStr}:{thue.colorOffset}";
    }

    protected static Thue UnpackTHue(string thue)
    {
        if (string.IsNullOrWhiteSpace(thue)) return null;

        // Strip hidden spaces from copy-pasting
        string payload = thue.Trim();

        if (payload.StartsWith("thue.", System.StringComparison.OrdinalIgnoreCase))
        {
            payload = payload.Substring(5);
        }

        string[] parts = payload.Split(':');
        if (parts.Length < 3) return null;

        Thue result = new Thue();

        // 1. Safely Parse Color
        string hexStr = parts[0].Trim();
        if (!hexStr.StartsWith("#")) hexStr = "#" + hexStr;

        if (UnityEngine.ColorUtility.TryParseHtmlString(hexStr, out UnityEngine.Color parsedColor))
        {
            result.colorHex = parsedColor;
        }
        else
        {
            result.colorHex = UnityEngine.Color.white;
        }

        // 2. Safely Parse Range and Offset
        if (int.TryParse(parts[1].Trim(), out int range))
        {
            result.colorRange = range;
        }

        if (int.TryParse(parts[2].Trim(), out int offset))
        {
            result.colorOffset = offset;
        }

        return result;
    }
}