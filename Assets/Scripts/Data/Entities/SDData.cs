using System;
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

[System.Serializable]
public class Phue
{
    //p syntax: .img.p.<hex>:<hex>:int.
    public Color colorStart = Color.white;
    public Color colorDestination = Color.white;
    public int colorRange;
}

public enum VisualType
{
    HSV,
    Hue,
    P,
    THue,
    B,
    Draw,
    Rect
}

[System.Serializable]
public class VisualModifier
{
    public VisualType Type;
    public string RawValue; // Used for b, draw, rect

    // Structured payloads
    public int h, s, v;
    public int hue;
    public Phue p;
    public Thue thue;
}

[System.Serializable]
public abstract class SDData
{
    public string entityName = "";
    public string imageOverride = "None";
    public string doc;

    [Header("Visual Modifiers")]
    public List<VisualModifier> visuals = new List<VisualModifier>();

    #region Backwards Compatibility Properties
    public int h
    {
        get => GetVisual(VisualType.HSV)?.h ?? 0;
        set { var vis = GetOrAddVisual(VisualType.HSV); vis.h = value; }
    }
    public int s
    {
        get => GetVisual(VisualType.HSV)?.s ?? 0;
        set { var vis = GetOrAddVisual(VisualType.HSV); vis.s = value; }
    }
    public int v
    {
        get => GetVisual(VisualType.HSV)?.v ?? 0;
        set { var vis = GetOrAddVisual(VisualType.HSV); vis.v = value; }
    }
    public int hue
    {
        get => GetVisual(VisualType.Hue)?.hue ?? 0;
        set { var vis = GetOrAddVisual(VisualType.Hue); vis.hue = value; }
    }
    public string b
    {
        get => GetVisual(VisualType.B)?.RawValue;
        set { var vis = GetOrAddVisual(VisualType.B); vis.RawValue = value; }
    }
    public string draw
    {
        get => GetVisual(VisualType.Draw)?.RawValue;
        set { var vis = GetOrAddVisual(VisualType.Draw); vis.RawValue = value; }
    }
    public string rect
    {
        get => GetVisual(VisualType.Rect)?.RawValue;
        set { var vis = GetOrAddVisual(VisualType.Rect); vis.RawValue = value; }
    }
    public string p
    {
        get
        {
            var vis = GetVisual(VisualType.P);
            if (vis == null) return null;

            // If there's valid phue data, return it formatted without the "p." prefix
            if (vis.p != null && vis.p.colorRange > 0)
            {
                string packed = PackP(vis.p);
                if (packed.StartsWith("p.", StringComparison.OrdinalIgnoreCase))
                    return packed.Substring(2);
                return packed;
            }

            return vis.RawValue;
        }
        set
        {
            var vis = GetOrAddVisual(VisualType.P);
            vis.p = UnpackP(value);
            vis.RawValue = value;
        }
    }

    public Phue phue
    {
        get
        {
            var vis = GetVisual(VisualType.P);
            if (vis == null)
            {
                vis = GetOrAddVisual(VisualType.P);
                vis.p = new Phue();
            }
            else if (vis.p == null)
            {
                vis.p = new Phue();
            }
            return vis.p;
        }
        set { var vis = GetOrAddVisual(VisualType.P); vis.p = value; }
    }

    public Thue thue
    {
        get
        {
            var vis = GetVisual(VisualType.THue);
            if (vis == null)
            {
                vis = GetOrAddVisual(VisualType.THue);
                vis.thue = new Thue();
            }
            else if (vis.thue == null)
            {
                vis.thue = new Thue();
            }
            return vis.thue;
        }
        set { var vis = GetOrAddVisual(VisualType.THue); vis.thue = value; }
    }

    private VisualModifier GetVisual(VisualType type) => visuals.FirstOrDefault(x => x.Type == type);
    private VisualModifier GetOrAddVisual(VisualType type)
    {
        var vis = GetVisual(type);
        if (vis == null)
        {
            vis = new VisualModifier { Type = type };
            visuals.Add(vis);
        }
        return vis;
    }
    #endregion

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
                if (TryParseSpecialOrNormalImage(tokens, ref i, out string parsedImg))
                {
                    imageOverride = parsedImg;
                }
                return true;
            case "doc": doc = nextVal; break;
            case "hsv":
                string[] hsvParts = nextVal.Split(':');
                if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out int hVal) && int.TryParse(hsvParts[1], out int sVal) && int.TryParse(hsvParts[2], out int vVal))
                {
                    visuals.Add(new VisualModifier { Type = VisualType.HSV, h = hVal, s = sVal, v = vVal });
                }
                break;
            case "hue":
                if (int.TryParse(nextVal, out int hueVal))
                    visuals.Add(new VisualModifier { Type = VisualType.Hue, hue = hueVal });
                break;

            case "thue":
                visuals.Add(new VisualModifier { Type = VisualType.THue, thue = UnpackTHue(nextVal) });
                break;
            case "p":
                visuals.Add(new VisualModifier { Type = VisualType.P, p = UnpackP(nextVal) });
                break;

            case "b":
                visuals.Add(new VisualModifier { Type = VisualType.B, RawValue = nextVal });
                break;
            case "draw":
                visuals.Add(new VisualModifier { Type = VisualType.Draw, RawValue = nextVal });
                break;
            case "rect":
                visuals.Add(new VisualModifier { Type = VisualType.Rect, RawValue = nextVal });
                break;
            default: return false;
        }
        i++; // Consume the value token
        return true;
    }

    protected void AppendColorModifier(StringBuilder sb)
    {
        foreach (var vis in visuals)
        {
            switch (vis.Type)
            {
                case VisualType.P:
                    if (vis.p != null && vis.p.colorRange != 0)
                        sb.Append($".{PackP(vis.p)}");
                    break;
                case VisualType.THue:
                    if (vis.thue != null && (vis.thue.colorRange != 0 || vis.thue.colorOffset != 0))
                    {
                        string packed = PackTHue(vis.thue);
                        Debug.Log($"[THue Debug] AppendColorModifier called. PackTHue returned: '{packed}'");
                        sb.Append($".{packed}");
                    }
                    break;
                case VisualType.HSV:
                    if (vis.h != 0 || vis.s != 0 || vis.v != 0)
                        sb.Append($".hsv.{vis.h}:{vis.s}:{vis.v}");
                    break;
                case VisualType.Hue:
                    if (vis.hue != 0)
                        sb.Append($".hue.{vis.hue}");
                    break;
                case VisualType.B:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue))
                        sb.Append($".b.{vis.RawValue}");
                    break;
                case VisualType.Draw:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue))
                        sb.Append($".draw.{vis.RawValue}");
                    break;
                case VisualType.Rect:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue))
                        sb.Append($".rect.{vis.RawValue}");
                    break;
            }
        }
    }

    protected static string FormatName(string name) => name?.Trim() ?? "";

    protected static string PackP(Phue p)
    {
        if (p == null) return string.Empty;

        string hexStart = ColorToHex(p.colorStart);
        string hexDest = ColorToHex(p.colorDestination);
        string rangeStr = p.colorRange.ToString("D2");
        return $"p.{hexStart}:{hexDest}:{rangeStr}";
    }

    protected static Phue UnpackP(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;

        string payload = p.Trim();
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

    protected bool TryParseSpecialOrNormalImage(List<string> tokens, ref int index, out string resultImageName)
    {
        resultImageName = null;
        if (index + 1 >= tokens.Count) return false;

        string firstToken = tokens[index + 1];

        if (index + 2 < tokens.Count)
        {
            string combinedDot = $"{firstToken}.{tokens[index + 2]}";
            foreach (var kvp in NameFixes.SpecialNameOverrides)
            {
                if (string.Equals(kvp.Value, combinedDot, StringComparison.OrdinalIgnoreCase))
                {
                    resultImageName = kvp.Key;
                    index += 2;
                    return true;
                }
            }
        }

        foreach (var kvp in NameFixes.SpecialNameOverrides)
        {
            if (string.Equals(kvp.Value, firstToken, StringComparison.OrdinalIgnoreCase))
            {
                resultImageName = kvp.Key;
                index += 1;
                return true;
            }
        }

        resultImageName = firstToken;
        index += 1;
        return true;
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