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

    public int x, y;

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
    public string doc2;
    [SerializeField] private int _xMultiplier = 1;

    // Value Safety:
    public int xMultiplier
    {
        get => (_xMultiplier >= 2 && _xMultiplier <= 9) ? _xMultiplier : 1;
        set => _xMultiplier = (value >= 2 && value <= 9) ? value : 1;
    }

    [Header("Visual Modifiers")]
    public List<VisualModifier> visuals = new List<VisualModifier>();

    [System.NonSerialized]
    protected bool _hasClearedVisualsForParse = false;

    #region Backwards Compatibility Properties
    public int h
    {
        get => GetVisual(VisualType.HSV)?.h ?? 0;
        set
        {
            var vis = GetVisual(VisualType.HSV);
            if (vis == null)
            {
                if (value == 0) return; // Don't inject a new modifier for 0
                vis = GetOrAddVisual(VisualType.HSV);
            }
            vis.h = value;

            if (vis.h == 0 && vis.s == 0 && vis.v == 0)
                visuals.Remove(vis);
        }
    }
    public int s
    {
        get => GetVisual(VisualType.HSV)?.s ?? 0;
        set
        {
            var vis = GetVisual(VisualType.HSV);
            if (vis == null)
            {
                if (value == 0) return; // Don't inject a new modifier for 0
                vis = GetOrAddVisual(VisualType.HSV);
            }
            vis.s = value;

            if (vis.h == 0 && vis.s == 0 && vis.v == 0)
                visuals.Remove(vis);
        }
    }
    public int v
    {
        get => GetVisual(VisualType.HSV)?.v ?? 0;
        set
        {
            var vis = GetVisual(VisualType.HSV);
            if (vis == null)
            {
                if (value == 0) return; // Don't inject a new modifier for 0
                vis = GetOrAddVisual(VisualType.HSV);
            }
            vis.v = value;

            if (vis.h == 0 && vis.s == 0 && vis.v == 0)
                visuals.Remove(vis);
        }
    }
    public int hue
    {
        get => GetVisual(VisualType.Hue)?.hue ?? 0;
        set
        {
            if (value == 0)
            {
                var existing = GetVisual(VisualType.Hue);
                if (existing != null) visuals.Remove(existing);
                return;
            }
            var vis = GetOrAddVisual(VisualType.Hue);
            vis.hue = value;
        }
    }
    public string b
    {
        get => GetVisual(VisualType.B)?.RawValue;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var existing = GetVisual(VisualType.B);
                if (existing != null) visuals.Remove(existing);
                return;
            }
            var vis = GetOrAddVisual(VisualType.B);
            vis.RawValue = value;
        }
    }
    public string draw
    {
        get => GetVisual(VisualType.Draw)?.RawValue;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var existing = GetVisual(VisualType.Draw);
                if (existing != null) visuals.Remove(existing);
                return;
            }
            var vis = GetOrAddVisual(VisualType.Draw);
            vis.RawValue = value;
        }
    }
    public string rect
    {
        get => GetVisual(VisualType.Rect)?.RawValue;
        set
        {
            // rect specifically supports "" (empty string) as a valid parameter in the parser. 
            // Therefore, we only strip it if it is strictly null (like during JSON mapping)
            if (value == null)
            {
                var existing = GetVisual(VisualType.Rect);
                if (existing != null) visuals.Remove(existing);
                return;
            }
            var vis = GetOrAddVisual(VisualType.Rect);
            vis.RawValue = value;
        }
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
            if (string.IsNullOrWhiteSpace(value))
            {
                var existing = GetVisual(VisualType.P);
                if (existing != null)
                {
                    existing.RawValue = null;
                    if (existing.p == null || existing.p.colorRange == 0)
                        visuals.Remove(existing);
                }
                return;
            }
            var vis = GetOrAddVisual(VisualType.P);
            vis.p = UnpackP(value);
            vis.RawValue = value;
        }
    }
    public Phue phue
    {
        get => GetVisual(VisualType.P)?.p;
        set
        {
            var existing = GetVisual(VisualType.P);
            if (value == null || value.colorRange == 0)
            {
                if (existing != null)
                {
                    existing.p = null;
                    if (string.IsNullOrWhiteSpace(existing.RawValue))
                        visuals.Remove(existing);
                }
                return;
            }
            var vis = GetOrAddVisual(VisualType.P);
            vis.p = value;
        }
    }
    public Thue thue
    {
        get => GetVisual(VisualType.THue)?.thue;
        set
        {
            var existing = GetVisual(VisualType.THue);
            if (value == null || (value.colorRange == 0 && value.colorOffset == 0))
            {
                if (existing != null) visuals.Remove(existing);
                return;
            }
            var vis = GetOrAddVisual(VisualType.THue);
            vis.thue = value;
        }
    }
    #endregion

    [System.NonSerialized]
    public bool SuppressAutoRegister = false;

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

    /// <summary>
    /// Seeds default visual modifiers (P, THue, HSV) for brand-new entities.
    /// </summary>
    public void InitializeDefaultVisuals()
    {
        visuals = new List<VisualModifier>
        {
            new VisualModifier { Type = VisualType.P, p = new Phue() },
            new VisualModifier { Type = VisualType.THue, thue = new Thue() },
            new VisualModifier { Type = VisualType.HSV }
        };
    }


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

    public virtual void Parse(string data)
    {
        _hasClearedVisualsForParse = false;
        xMultiplier = 1;
        if (string.IsNullOrWhiteSpace(data)) return;
        string clean = data.Trim();
        clean = StaticBranchTracing.StripOuterParens(clean);
        if (clean.StartsWith("x", StringComparison.OrdinalIgnoreCase) && clean.Length > 1 && char.IsDigit(clean[1]))
        {
            int dotIdx = clean.IndexOf('.');
            if (dotIdx > 1)
            {
                string multStr = clean.Substring(1, dotIdx - 1);
                if (int.TryParse(multStr, out int mult))
                {
                    xMultiplier = mult;
                    clean = clean.Substring(dotIdx + 1).Trim();
                }
            }
        }
        if (clean.StartsWith("x", StringComparison.OrdinalIgnoreCase) && clean.Length > 1 && char.IsDigit(clean[1]))
        {
            int dotIdx = clean.IndexOf('.');
            if (dotIdx > 1)
            {
                string multStr = clean.Substring(1, dotIdx - 1);
                if (int.TryParse(multStr, out int mult) && mult >= 2 && mult <= 9)
                {
                    xMultiplier = mult;
                    clean = clean.Substring(dotIdx + 1).Trim();
                }
            }
        }
        ParseCore(clean);

        // REINABLE IF YOU WANT A PARSED ENTITY TO UNPACK BACK INTO THE FULL UI.

        // --- NEW CODE: Automatically bubble valid top-level custom data to the Mod Editor ---
        if (!SuppressAutoRegister && ModPackage.Instance != null && ModPackage.Instance.IsModLoaded)
        {
            ModPackage.Instance.TryAutoRegisterParsedEntity(this);
        }

    }
    protected abstract void ParseCore(string cleanData);

    protected void ProcessRecursiveParentheses(string originalToken, Action<List<string>> processingDelegate)
    {
        string inner = originalToken.Substring(1, originalToken.Length - 2);
        List<string> innerTokens = StaticBranchTracing.TopLevelSplit(inner, '.');
        processingDelegate(innerTokens);
    }
    protected bool TryParseSpecialOrNormalImage(TokenStream stream, out string resultImageName)
    {
        resultImageName = null;
        if (stream.IsEOF) return false;

        string firstToken = stream.Consume();
        if (!stream.IsEOF)
        {
            string combinedDot = $"{firstToken}.{stream.Peek()}";
            foreach (var kvp in NameFixes.SpecialNameOverrides)
            {
                if (string.Equals(kvp.Value, combinedDot, StringComparison.OrdinalIgnoreCase))
                {
                    resultImageName = kvp.Key;
                    stream.Consume(); // Consume the second part safely
                    return true;
                }
            }
        }

        foreach (var kvp in NameFixes.SpecialNameOverrides)
        {
            if (string.Equals(kvp.Value, firstToken, StringComparison.OrdinalIgnoreCase))
            {
                resultImageName = kvp.Key;
                return true;
            }
        }

        resultImageName = firstToken;
        return true;
    }
    protected bool TryProcessCommonMetadata(TokenStream stream)
    {
        if (stream.IsEOF) return false;
        string tokenLower = stream.Peek().ToLower();

        if (!_hasClearedVisualsForParse)
        {
            visuals.Clear();
            _hasClearedVisualsForParse = true;
        }

        switch (tokenLower)
        {
            case "n":
                stream.Consume();
                if (!stream.IsEOF) entityName = stream.Consume();
                return true;
            case "img":
                stream.Consume(); // Consume 'img'
                if (TryParseSpecialOrNormalImage(stream, out string parsedImg)) imageOverride = parsedImg;
                return true;
            case "doc":
                stream.Consume();
                if (!stream.IsEOF)
                {
                    string parsedDoc = stream.Consume();
                    if (string.IsNullOrEmpty(doc)) doc = parsedDoc;
                    else doc2 = parsedDoc;
                }
                return true;
            case "hsv":
                stream.Consume();
                if (!stream.IsEOF)
                {
                    string[] hsvParts = stream.Consume().Split(':');
                    if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out int h) && int.TryParse(hsvParts[1], out int s) && int.TryParse(hsvParts[2], out int v))
                        visuals.Add(new VisualModifier { Type = VisualType.HSV, h = h, s = s, v = v });
                }
                return true;
            case "hue":
                stream.Consume();
                if (!stream.IsEOF && int.TryParse(stream.Consume(), out int hueVal))
                    visuals.Add(new VisualModifier { Type = VisualType.Hue, hue = hueVal });
                return true;
            case "thue":
                stream.Consume();
                if (!stream.IsEOF) visuals.Add(new VisualModifier { Type = VisualType.THue, thue = UnpackTHue(stream.Consume()) });
                return true;
            case "p":
                stream.Consume();
                if (!stream.IsEOF)
                {
                    string pVal = stream.Consume();
                    visuals.Add(new VisualModifier { Type = VisualType.P, p = UnpackP(pVal), RawValue = pVal });
                }
                return true;
            case "b":
                stream.Consume();
                if (!stream.IsEOF) visuals.Add(new VisualModifier { Type = VisualType.B, RawValue = stream.Consume() });
                return true;
            case "draw":
                stream.Consume();
                if (!stream.IsEOF)
                {
                    string nextVal = stream.Consume();
                    string spriteRef = nextVal;
                    int x = 0, y = 0;
                    if (nextVal.Contains(":"))
                    {
                        string[] parts = nextVal.Split(':');
                        spriteRef = parts[0];
                        if (parts.Length > 1 && int.TryParse(parts[1], out int px)) x = px;
                        if (parts.Length > 2 && int.TryParse(parts[2], out int py)) y = py;
                    }
                    visuals.Add(new VisualModifier { Type = VisualType.Draw, RawValue = spriteRef, x = x, y = y });
                }
                return true;
            case "rect":
                stream.Consume();
                if (!stream.IsEOF)
                {
                    string peek = stream.Peek().ToLower();
                    if (peek != "tier" && peek != "doc" && peek != "n" && peek != "p" && peek != "img" && peek != "b")
                        visuals.Add(new VisualModifier { Type = VisualType.Rect, RawValue = stream.Consume() });
                    else
                        visuals.Add(new VisualModifier { Type = VisualType.Rect, RawValue = "" });
                }
                else visuals.Add(new VisualModifier { Type = VisualType.Rect, RawValue = "" });
                return true;
        }
        return false;
    }
    protected bool TryProcessXMultiplier(List<string> tokens)
    {
        if (tokens.Count > 0)
        {
            string tokenLower = tokens[0].ToLower();
            if (tokenLower.StartsWith("x") && tokenLower.Length > 1 && int.TryParse(tokenLower.Substring(1), out int mult))
            {
                xMultiplier = mult;
                tokens.RemoveAt(0); // Consume the multiplier so the parser can see the real name
                return true;
            }
        }
        return false;
    }
    protected void AppendColorModifier(StringBuilder sb)
    {
        foreach (var vis in visuals)
        {
            switch (vis.Type)
            {
                case VisualType.P:
                    if (vis.p != null && vis.p.colorRange != 0) sb.Append($".{PackP(vis.p)}");
                    break;
                case VisualType.THue:
                    if (vis.thue != null && (vis.thue.colorRange != 0 || vis.thue.colorOffset != 0)) sb.Append($".{PackTHue(vis.thue)}");
                    break;
                case VisualType.HSV:
                    if (vis.h != 0 || vis.s != 0 || vis.v != 0) sb.Append($".hsv.{vis.h}:{vis.s}:{vis.v}");
                    break;
                case VisualType.Hue:
                    if (vis.hue != 0) sb.Append($".hue.{vis.hue}");
                    break;
                case VisualType.B:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue)) sb.Append($".b.{vis.RawValue}");
                    break;
                case VisualType.Draw:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue))
                    {
                        if (vis.x != 0 || vis.y != 0) sb.Append($".draw.{vis.RawValue}:{vis.x}:{vis.y}");
                        else sb.Append($".draw.{vis.RawValue}");
                    }
                    break;
                case VisualType.Rect:
                    if (!string.IsNullOrWhiteSpace(vis.RawValue)) sb.Append($".rect.{vis.RawValue}");
                    break;
            }
        }
    }

    // TEMPLATE METHOD: Sole entry point for all exports. Centrally owns xMultiplier formatting.
    public string Export()
    {
        string rawExport = ExportCore();
        if (string.IsNullOrWhiteSpace(rawExport)) return string.Empty;

        if (xMultiplier >= 2 && xMultiplier <= 9)
        {
            // Law 3: If the core perfectly self-brackets the entire expression, place the multiplier cleanly inside
            if (rawExport.StartsWith("(") && rawExport.EndsWith(")") &&
                StaticBranchTracing.StripOuterParens(rawExport) == rawExport.Substring(1, rawExport.Length - 2))
            {
                return $"(x{xMultiplier}.{rawExport.Substring(1)}";
            }
            return $"x{xMultiplier}.{rawExport}";
        }
        return rawExport;
    }

    // Subclasses only define their domain-specific body
    protected abstract string ExportCore();

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
        //Debug.Log($"[THue Debug] UnpackTHue received raw payload: '{payload}'");

        if (payload.StartsWith("thue.", System.StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(5);

        string[] parts = payload.Split(':');
        if (parts.Length < 3)
        {
            //Debug.LogError($"[THue FATAL] UnpackTHue failed to split 3 parts! It only found {parts.Length} parts. Payload was: '{payload}'");
            return null;
        }

        Thue result = new Thue();
        result.colorHex = ParseColor(parts[0]);
        if (int.TryParse(parts[1].Trim(), out int range)) result.colorRange = range;
        if (int.TryParse(parts[2].Trim(), out int offset)) result.colorOffset = offset;

        return result;
    }
}