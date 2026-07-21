using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public class Thue
{
    //thue syntax: .img.thue.<hex>:int:int.
    public Color colorHex = Color.white;
    public int colorRange;
    public int colorOffset;
}

public class Phue
{
    //phue syntax: .img.p.<hex>:<hex>:int.
    public Color colorStart = Color.white;
    public Color colorDestination = Color.white;
    public int colorRange;
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
    public Phue phue = new Phue();

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
            case "img":
                {
                    if (TryParseSpecialOrNormalImage(tokens, ref i, out string parsedImg))
                    {
                        imageOverride = parsedImg;
                    }
                    return true;
                }
                break;
            case "doc": doc = nextVal; break;
            case "hsv":
                string[] hsvParts = nextVal.Split(':');
                if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out h) && int.TryParse(hsvParts[1], out s) && int.TryParse(hsvParts[2], out v)) { }
                break;
            case "hue": if (int.TryParse(nextVal, out int hVal)) hue = hVal; break;

            case "thue": thue = UnpackTHue(nextVal); break;
            case "p": phue = UnpackPHue(nextVal); break;

            case "b": b = nextVal; break;
            case "draw": draw = nextVal; break;
            case "rect": rect = nextVal; break;
            default: return false;
        }
        i++; // Consume the value token
        return true;
    }

    //protected static string FormatName(string name) => name?.Replace(" ", "") ?? "";
    protected static string FormatName(string name) => name?.Trim() ?? ""; //spaces are allowed.

    protected static string PackPHue(Phue phue)
    {
        if (phue == null) return string.Empty;

        string hexStart = ColorToHex(phue.colorStart);
        string hexDest = ColorToHex(phue.colorDestination);
        string rangeStr = phue.colorRange.ToString("D2");
        return $"p.{hexStart}:{hexDest}:{rangeStr}";
    }

    protected static Phue UnpackPHue(string phue)
    {
        if (string.IsNullOrWhiteSpace(phue)) return null;

        string payload = phue.Trim();
        if (payload.StartsWith("p.", System.StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(2);

        string[] parts = payload.Split(':');
        if (parts.Length < 3) return null;

        Phue result = new Phue();
        result.colorStart = ParseColor(parts[0]);
        result.colorDestination = ParseColor(parts[1]);
        if (int.TryParse(parts[2].Trim(), out int range)) result.colorRange = range;

        return result;
    }

    /*
    protected static string PackTHue(Thue thue)
    {
        if (thue == null) return string.Empty;

        string hex = ColorToHex(thue.colorHex);
        string rangeStr = thue.colorRange.ToString("D2");

        return $"thue.{hex}:{rangeStr}:{thue.colorOffset}";
    }

    protected static Thue UnpackTHue(string thue)
    {
        if (string.IsNullOrWhiteSpace(thue)) return null;

        string payload = thue.Trim();
        if (payload.StartsWith("thue.", System.StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(5);

        string[] parts = payload.Split(':');
        if (parts.Length < 3) return null;

        Thue result = new Thue();
        result.colorHex = ParseColor(parts[0]);
        if (int.TryParse(parts[1].Trim(), out int range)) result.colorRange = range;
        if (int.TryParse(parts[2].Trim(), out int offset)) result.colorOffset = offset;

        return result;
    }
    */

    protected static Color ParseColor(string hexStr)
    {
        hexStr = hexStr.Trim();
        if (!hexStr.StartsWith("#")) hexStr = "#" + hexStr;

        if (UnityEngine.ColorUtility.TryParseHtmlString(hexStr, out UnityEngine.Color parsedColor))
            return parsedColor;
        return UnityEngine.Color.white;
    }

    protected static string ColorToHex(Color colorHex)
    {
        int r = UnityEngine.Mathf.RoundToInt(colorHex.r * 255f);
        int g = UnityEngine.Mathf.RoundToInt(colorHex.g * 255f);
        int b = UnityEngine.Mathf.RoundToInt(colorHex.b * 255f);

        if (r % 17 == 0 && g % 17 == 0 && b % 17 == 0)
            return $"{(r / 17):x}{(g / 17):x}{(b / 17):x}";
        else
            return UnityEngine.ColorUtility.ToHtmlStringRGB(colorHex).ToLower();
    }

    /// <summary>
    /// Converts short/special names (e.g. "b1", "jinx") to their full name override ("b1.75", "jinx.uhh").
    /// </summary>
    public static string FormatSpecialImageName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return rawName;
        string trimmed = rawName.Trim();
        if (NameFixes.SpecialNameOverrides.TryGetValue(trimmed, out string overrideName))
        {
            return overrideName;
        }
        return trimmed;
    }

    /// NAME FIXES

    /// <summary>
    /// Parses image/replica tokens, safely consuming 2 tokens if they form a dotted override name like "b1.75".
    /// </summary>
    /// <summary>
    /// Parses image/replica tokens, reverse-looking up exported override names ("n1.75") back to UI lookup keys ("n1").
    /// </summary>
    protected bool TryParseSpecialOrNormalImage(List<string> tokens, ref int index, out string resultImageName)
    {
        resultImageName = null;
        if (index + 1 >= tokens.Count) return false;

        string firstToken = tokens[index + 1];

        // 1. Check if firstToken + "." + secondToken matches an exported override value (e.g. "n1" + "." + "75" == "n1.75")
        if (index + 2 < tokens.Count)
        {
            string combinedDot = $"{firstToken}.{tokens[index + 2]}";
            foreach (var kvp in NameFixes.SpecialNameOverrides)
            {
                if (string.Equals(kvp.Value, combinedDot, StringComparison.OrdinalIgnoreCase))
                {
                    resultImageName = kvp.Key; // Return UI shorthand key ("n1")
                    index += 2; // Consume both tokens ("n1" and "75")
                    return true;
                }
            }
        }

        // 2. Check if firstToken matches a single-token exported override value (e.g. "deathSigil" -> returns "totemdeath")
        foreach (var kvp in NameFixes.SpecialNameOverrides)
        {
            if (string.Equals(kvp.Value, firstToken, StringComparison.OrdinalIgnoreCase))
            {
                resultImageName = kvp.Key; // Return UI shorthand key
                index += 1;
                return true;
            }
        }

        // 3. Standard single-token image name (or already a shorthand key like "n1")
        resultImageName = firstToken;
        index += 1;
        return true;
    }


    protected void AppendColorModifier(StringBuilder sb)
    {
        if (phue != null && phue.colorRange != 0) sb.Append($".{PackPHue(phue)}");

        if (thue != null && (thue.colorRange != 0 || thue.colorOffset != 0))
        {
            string packed = PackTHue(thue);
            Debug.Log($"[THue Debug] AppendColorModifier called. PackTHue returned: '{packed}'");
            sb.Append($".{packed}");
        }

        if (h != 0 || s != 0 || v != 0) sb.Append($".hsv.{h}:{s}:{v}");
        else if (hue != 0) sb.Append($".hue.{hue}");
    }
    protected static string PackTHue(Thue thue)
    {
        if (thue == null) return string.Empty;

        string hex = ColorToHex(thue.colorHex);
        string rangeStr = thue.colorRange.ToString("D2");

        string result = $"thue.{hex}:{rangeStr}:{thue.colorOffset}";

        if (!result.Contains(":"))
            Debug.LogError($"[THue FATAL] PackTHue generated a string WITHOUT colons! Hex: {hex}, Range: {rangeStr}, Offset: {thue.colorOffset}");

        return result;
    }
    protected static Thue UnpackTHue(string thue)
    {
        if (string.IsNullOrWhiteSpace(thue)) return null;

        string payload = thue.Trim();
        Debug.Log($"[THue Debug] UnpackTHue received raw payload: '{payload}'");

        if (payload.StartsWith("thue.", System.StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(5);

        string[] parts = payload.Split(':');
        if (parts.Length < 3)
        {
            Debug.LogError($"[THue FATAL] UnpackTHue failed to split 3 parts! It only found {parts.Length} parts. Payload was: '{payload}'");
            return null;
        }

        Thue result = new Thue();
        result.colorHex = ParseColor(parts[0]);
        if (int.TryParse(parts[1].Trim(), out int range)) result.colorRange = range;
        if (int.TryParse(parts[2].Trim(), out int offset)) result.colorOffset = offset;

        return result;
    }

}