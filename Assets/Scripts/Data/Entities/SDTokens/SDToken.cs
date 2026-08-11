using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// ============================================================================================
// 1. BASE AST ARCHITECTURE
// ============================================================================================

/// <summary>
/// Defines the strict grammatical order required by the game engine.
/// ONLY used when inserting brand-new elements from the UI (Greenfield).
/// Existing parsed elements NEVER change their index order.
/// </summary>
public enum ClauseCategory
{
    BaseIdentifier = 1, // e.g. replica.Fey
    CoreMetadata = 2,   // n, hp, tier, col, adj, speech
    DiceSides = 3,      // sd
    Mechanics = 4,      // left.facade.bas1, i.Fly, t.jinx
    Visuals = 5,        // img, hsv, hue, p, thue, b, draw, rect
    Trailing = 6        // doc, bal
}

/// <summary>
/// Base class for all nodes in the Syntax Tree.
/// </summary>
[System.Serializable]
public abstract class SDNode
{
    public string RawTrivia { get; set; } = string.Empty;
    public abstract string Export();
    public override string ToString() => Export();
}

/// <summary>
/// A Clause is a complete, top-level statement in the entity's root list.
/// </summary>
[System.Serializable]
public abstract class SDClause : SDNode
{
    public abstract ClauseCategory Category { get; }
}

/// <summary>
/// Root container for a parsed or assembled SD entity AST string.
/// Centrally owns xMultiplier formatting and clause aggregation.
/// </summary>
[System.Serializable]
public class SDRootNode : SDNode
{
    private int _xMultiplier = 1;
    public int XMultiplier
    {
        get => (_xMultiplier >= 2 && _xMultiplier <= 9) ? _xMultiplier : 1;
        set => _xMultiplier = (value >= 2 && value <= 9) ? value : 1;
    }

    public List<SDClause> Clauses { get; set; } = new List<SDClause>();

    public override string Export()
    {
        var validExports = Clauses
            .Select(c => c?.Export())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        string rawExport = string.Join(".", validExports);
        if (string.IsNullOrWhiteSpace(rawExport)) return string.Empty;

        if (XMultiplier >= 2 && XMultiplier <= 9)
        {
            if (rawExport.StartsWith("(") && rawExport.EndsWith(")") &&
                StaticBranchTracing.StripOuterParens(rawExport) == rawExport.Substring(1, rawExport.Length - 2))
            {
                return $"(x{XMultiplier}.{rawExport.Substring(1)}";
            }
            return $"x{XMultiplier}.{rawExport}";
        }

        return rawExport;
    }
}

// ============================================================================================
// 2. BASE IDENTIFIER & CORE METADATA CLAUSES
// ============================================================================================

[System.Serializable]
public class BaseIdentifierClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.BaseIdentifier;
    public string Identifier { get; set; } = "";

    public BaseIdentifierClause(string identifier = "") { Identifier = identifier; }
    public override string Export() => Identifier?.Trim() ?? "";
}

[System.Serializable]
public class NameClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public string Name { get; set; } = "";

    public NameClause(string name = "") { Name = name; }
    public override string Export() => string.IsNullOrEmpty(Name) ? "" : $"n.{Name.Trim()}";
}

[System.Serializable]
public class HpClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public int Hp { get; set; }

    public HpClause(int hp = 0) { Hp = hp; }
    public override string Export() => Hp > 0 ? $"hp.{Hp}" : "";
}

[System.Serializable]
public class TierClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public int Tier { get; set; }

    public TierClause(int tier = 1) { Tier = tier; }
    public override string Export() => $"tier.{Tier}";
}



// ============================================================================================
// CORE METADATA CLAUSE UPDATES
// ============================================================================================

[System.Serializable]
public class AdjClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public int? Value { get; set; }

    public AdjClause(int? value = null) { Value = value; }
    public override string Export() => Value.HasValue ? $"adj.{Value.Value}" : "";
}

[System.Serializable]
public class SpeechClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public string Text { get; set; } = "";

    public SpeechClause(string text = "") { Text = text; }
    public override string Export() => string.IsNullOrWhiteSpace(Text) ? "" : $"speech.{Text.Trim()}";
}


// ============================================================================================
// 3. DICE SIDES CLAUSES
// ============================================================================================

[System.Serializable]
public class SideClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.DiceSides;
    public string SideValue { get; set; } = "";

    public SideClause(string sideValue = "") { SideValue = sideValue; }
    public override string Export() => string.IsNullOrEmpty(SideValue) ? "" : $"sd.{SideValue}";
}

// ============================================================================================
// 4. VISUAL CLAUSES
// ============================================================================================

[System.Serializable]
public class ImgClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public string ImageOverride { get; set; } = "";

    public ImgClause(string imageOverride = "") { ImageOverride = imageOverride; }

    public override string Export()
    {
        if (string.IsNullOrWhiteSpace(ImageOverride) || ImageOverride.Equals("None", StringComparison.OrdinalIgnoreCase))
            return "";

        string formatted = SDData.FormatSpecialImageName(ImageOverride);
        return $"img.{formatted}";
    }
}

[System.Serializable]
public class BorderClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public string BorderValue { get; set; } = "";

    public BorderClause(string borderValue = "") { BorderValue = borderValue; }
    public override string Export() => string.IsNullOrWhiteSpace(BorderValue) ? "" : $"b.{BorderValue}";
}

[System.Serializable]
public class HsvClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public int Hue { get; set; }
    public int Saturation { get; set; }
    public int Value { get; set; }

    public HsvClause(int h = 0, int s = 0, int v = 0) { Hue = h; Saturation = s; Value = v; }

    public override string Export() =>
        (Hue != 0 || Saturation != 0 || Value != 0) ? $"hsv.{Hue}:{Saturation}:{Value}" : "";
}

[System.Serializable]
public class HueClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public int Hue { get; set; }

    public HueClause(int hue = 0) { Hue = hue; }
    public override string Export() => Hue != 0 ? $"hue.{Hue}" : "";
}

[System.Serializable]
public class PhueClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public Phue PhueData { get; set; }
    public string RawValue { get; set; }

    public PhueClause(Phue phue = null) { PhueData = phue ?? new Phue(); }
    public PhueClause(string rawValue) { RawValue = rawValue; }

    public override string Export()
    {
        if (PhueData != null && PhueData.colorRange > 0)
        {
            string hexStart = ColorToHex(PhueData.colorStart);
            string hexDest = ColorToHex(PhueData.colorDestination);
            string rangeStr = PhueData.colorRange.ToString("D2");
            return $"p.{hexStart}:{hexDest}:{rangeStr}";
        }

        return !string.IsNullOrWhiteSpace(RawValue) ? $"p.{RawValue}" : "";
    }

    private static string ColorToHex(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255f);
        int g = Mathf.RoundToInt(color.g * 255f);
        int b = Mathf.RoundToInt(color.b * 255f);
        if (r % 17 == 0 && g % 17 == 0 && b % 17 == 0)
            return $"{(r / 17):x}{(g / 17):x}{(b / 17):x}";
        return ColorUtility.ToHtmlStringRGB(color).ToLower();
    }
}

[System.Serializable]
public class ThueClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public Thue ThueData { get; set; }

    public ThueClause(Thue thue = null) { ThueData = thue ?? new Thue(); }

    public override string Export()
    {
        if (ThueData == null || (ThueData.colorRange == 0 && ThueData.colorOffset == 0))
            return "";

        string hex = ColorToHex(ThueData.colorHex);
        string rangeStr = ThueData.colorRange.ToString("D2");
        return $"thue.{hex}:{rangeStr}:{ThueData.colorOffset}";
    }

    private static string ColorToHex(Color color)
    {
        int r = Mathf.RoundToInt(color.r * 255f);
        int g = Mathf.RoundToInt(color.g * 255f);
        int b = Mathf.RoundToInt(color.b * 255f);
        if (r % 17 == 0 && g % 17 == 0 && b % 17 == 0)
            return $"{(r / 17):x}{(g / 17):x}{(b / 17):x}";
        return ColorUtility.ToHtmlStringRGB(color).ToLower();
    }
}

[System.Serializable]
public class DrawClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public string SpriteRef { get; set; } = "";
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }

    public DrawClause(string spriteRef = "", int x = 0, int y = 0)
    {
        SpriteRef = spriteRef; OffsetX = x; OffsetY = y;
    }

    public override string Export()
    {
        if (string.IsNullOrWhiteSpace(SpriteRef)) return "";
        return (OffsetX != 0 || OffsetY != 0) ? $"draw.{SpriteRef}:{OffsetX}:{OffsetY}" : $"draw.{SpriteRef}";
    }
}

[System.Serializable]
public class RectClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Visuals;
    public string RawValue { get; set; } = "";

    public RectClause(string rawValue = "") { RawValue = rawValue; }

    // rect supports empty string as a valid parameter (e.g. rect.)
    public override string Export() => RawValue != null ? $"rect.{RawValue}" : "";
}

// ============================================================================================
// 5. TRAILING CLAUSES
// ============================================================================================

[System.Serializable]
public class DocClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Trailing;
    public string Description { get; set; } = "";

    public DocClause(string description = "") { Description = description; }
    public override string Export() => string.IsNullOrEmpty(Description) ? "" : $"doc.{Description}";
}

[System.Serializable]
public class BalClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Trailing;
    public string Balance { get; set; } = "";

    public BalClause(string balance = "") { Balance = balance; }
    public override string Export() => string.IsNullOrEmpty(Balance) ? "" : $"bal.{Balance}";
}

// ============================================================================================
// 6. MECHANIC CLAUSES (The powerhouse of Items, Modifiers, and Face targeting)
// ============================================================================================

public enum DiceTarget
{
    All, Left, Mid, Top, Bot, Right, Rightmost, Left2, Mid2, Right2, Right3, Right5, TopBot, Row, Col
}

[System.Serializable]
public class MechanicClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;

    public List<DiceTarget> Targets { get; set; } = new List<DiceTarget>();
    public int Multiplier { get; set; } = 1;
    public bool Unpack { get; set; } = false;

    // The core action/prefix (e.g., "facade", "sticker", "k", "i")
    public string Prefix { get; set; } = "";

    // The payload (Can be a raw string, an ItemNode, EntityNode, etc.)
    public SDNode Payload { get; set; }

    // Chained mechanics (e.g., #k.cleave)
    public List<SDNode> ChainedPayloads { get; set; } = new List<SDNode>();

    public override string Export()
    {
        List<string> parts = new List<string>();

        // 1. Targets
        if (Targets.Count > 0)
            parts.AddRange(Targets.Select(t => t.ToString().ToLower()));

        // 2. Modifiers
        if (Multiplier >= 2) parts.Add($"x{Multiplier}");
        if (Unpack) parts.Add("unpack");

        // 3. Prefix (if any)
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);

        // 4. Payload
        string corePayload = Payload?.Export() ?? "";
        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);

        // 5. Assemble base chain
        string mechanicString = string.Join(".", parts);

        // 6. Append chains via '#'
        if (ChainedPayloads.Count > 0)
        {
            string chains = string.Join("#", ChainedPayloads.Select(p => p.Export()));
            mechanicString = $"{mechanicString}#{chains}";
        }

        return mechanicString;
    }
}

[System.Serializable]
public class RawStringNode : SDNode
{
    public string Value { get; set; } = "";
    public RawStringNode(string value) { Value = value; }
    public override string Export() => Value;
}

// ============================================================================================
// 7. AST HELPER EXTENSIONS
// ============================================================================================

public static class ASTExtensions
{
    /// <summary>
    /// Inserts a new clause into the AST at the correct grammatical location.
    /// Existing clauses are completely untouched. Sequence is 100% preserved.
    /// </summary>
    public static void InsertSafe(this List<SDClause> ast, SDClause newClause)
    {
        if (ast == null || newClause == null) return;

        // 1. Find the last clause in the AST that shares this category, or an earlier category.
        int insertionIndex = -1;
        for (int i = 0; i < ast.Count; i++)
        {
            if (ast[i].Category <= newClause.Category)
            {
                insertionIndex = i;
            }
        }

        // 2. Insert immediately after it.
        if (insertionIndex >= 0)
        {
            ast.Insert(insertionIndex + 1, newClause);
        }
        else
        {
            ast.Insert(0, newClause); // Belongs at the very beginning
        }
    }
}

// ============================================================================================
// DICE SIDES CLAUSE
// ============================================================================================

/// <summary>
/// Represents the .sd. face definitions clause across all 6 dice sides.
/// Handles zero-truncation and dash-pip formatting matching EntityData.AppendDiceSides.
/// </summary>
[System.Serializable]
public class SdClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.DiceSides;
    public DiceSideData[] Sides { get; set; } = new DiceSideData[6];

    public SdClause()
    {
        for (int i = 0; i < 6; i++) Sides[i] = new DiceSideData();
    }

    public SdClause(DiceSideData[] sides)
    {
        if (sides != null && sides.Length == 6) Sides = sides;
        else
        {
            Sides = new DiceSideData[6];
            for (int i = 0; i < 6; i++) Sides[i] = new DiceSideData();
        }
    }

    public override string Export()
    {
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            if (Sides[i] != null && (Sides[i].effectID != 0 || Sides[i].pips != 0))
                lastActiveIndex = i;
        }

        if (lastActiveIndex == -1) return "";

        List<string> sideExports = new List<string>();
        for (int i = 0; i <= lastActiveIndex; i++)
        {
            var side = Sides[i];
            if (side == null || (side.effectID == 0 && side.pips == 0))
            {
                sideExports.Add("0");
            }
            else
            {
                sideExports.Add(side.pips == 0 ? $"{side.effectID}" : $"{side.effectID}-{side.pips}");
            }
        }

        return $"sd.{string.Join(":", sideExports)}";
    }
}

// ============================================================================================
// COLLECTION & SPECIAL ABILITY PAYLOAD CLAUSES
// ============================================================================================

[System.Serializable]
public class GiftClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public GiftClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? $"gift.{Payload.Export()}" : "";
}

[System.Serializable]
public class LearnClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public LearnClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? $"learn.{Payload.Export()}" : "";
}

[System.Serializable]
public class OrbClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public string OrbName { get; set; } = "";
    public SDNode CustomOrbData { get; set; }

    public OrbClause(string orbName = "") { OrbName = orbName; }
    public OrbClause(SDNode customOrb) { CustomOrbData = customOrb; }

    public override string Export()
    {
        if (CustomOrbData != null) return CustomOrbData.Export();
        return !string.IsNullOrWhiteSpace(OrbName) ? $"orb.{OrbName.Trim()}" : "";
    }
}

[System.Serializable]
public class TriggerHPClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public TriggerHPClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? $"triggerhpdata.({Payload.Export()})" : "";
}

[System.Serializable]
public class OnHitClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public OnHitClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? $"onhitdata.({Payload.Export()})" : "";
}

[System.Serializable]
public class AbilityDataClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public AbilityDataClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? $"abilitydata.({Payload.Export()})" : "";
}

// ============================================================================================
// HERO-SPECIFIC AST CLAUSES
// ============================================================================================

/// <summary>
/// Represents the explicit base replica clause: replica.<baseReplica>
/// </summary>
[System.Serializable]
public class ReplicaClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.BaseIdentifier;
    public string BaseReplica { get; set; } = "";

    public ReplicaClause(string baseReplica = "") { BaseReplica = baseReplica; }

    public override string Export() =>
        string.IsNullOrWhiteSpace(BaseReplica) ? "" : $"replica.{SDData.FormatSpecialImageName(BaseReplica.Trim())}";
}

/// <summary>
/// Represents the hero color class clause: col.<colorClass>
/// Includes logic to omit default colors matching HeroData.IsDefaultHeroColor.
/// </summary>
[System.Serializable]
public class ColClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public string ColorClass { get; set; } = "";
    public string BaseReplica { get; set; } = ""; // Used to suppress default color export

    public ColClause(string colorClass = "", string baseReplica = "")
    {
        ColorClass = colorClass;
        BaseReplica = baseReplica;
    }

    public override string Export()
    {
        if (string.IsNullOrWhiteSpace(ColorClass)) return "";

        string cleanCode = ColorClass.Replace("col.", "").Trim();

        // Check if color is the default for this replica type
        if (!string.IsNullOrEmpty(BaseReplica) && Enum.TryParse(BaseReplica, true, out HeroType heroType))
        {
            if (SDColors.HeroColorMap.TryGetValue(heroType, out var defaultOption))
            {
                string defaultCode = SDColors.GetColorCode(defaultOption);
                if (string.Equals(cleanCode, defaultCode, StringComparison.OrdinalIgnoreCase))
                    return ""; // Omit default color
            }
        }

        return $"col.{cleanCode}";
    }
}

/// <summary>
/// Represents spell abilities scoped to Hero entities.
/// Exports into late-inner payload position to prevent engine parser bugs.
/// </summary>
[System.Serializable]
public class SpellClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public SpellClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? Payload.Export() : "";
}

/// <summary>
/// Represents tactic abilities scoped to Hero entities.
/// Exports into late-inner payload position to prevent engine parser bugs.
/// </summary>
[System.Serializable]
public class TacticClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public SDNode Payload { get; set; }

    public TacticClause(SDNode payload = null) { Payload = payload; }
    public override string Export() => Payload != null ? Payload.Export() : "";
}

// ============================================================================================
// ABILITY AST TYPES & UTILITIES
// ============================================================================================

public enum AbilityType
{
    Spell,
    Tactic,
    OnHit,
    TriggerHP,
    Orb
}

public static class TriggerHPHelper
{
    /// <summary>
    /// Translates TriggerHP integer HP intervals into human-readable descriptions.
    /// Matches AbilityData.GetPipsAffectedDescription.
    /// </summary>
    public static string GetPipsAffectedDescription(int hp)
    {
        if (hp <= 0) return "None";

        switch (hp)
        {
            case 1: return "All HP";
            case 2: return "Every 2nd HP";
            case 3: return "Every 3rd HP";
            case 4: return "Every 4th HP";
            case 5: return "Every 5th HP";
            case 6: return "Every 10th HP";
            case 7: return "Every 10th HP, starting with the 5th";
            case 8: return "Every 2nd HP, starting with the 1st";
            case 9: return "Every 3rd HP, starting with the 1st";
            case 10: return "Inner 1 HP";
            case 11: return "Inner 2 HP";
            case 12: return "Inner 3 HP";
            case 13: return "Inner 5 HP";
            case 14: return "Outer 1 HP";
            case 15: return "Outer 2 HP";
            case 16: return "Outer 3 HP";
            case 17: return "Outer 5 HP";
            case 18: return "Middle HP";
            case 19: return "2 Evenly Spaced HP";
            case 20: return "3 Evenly Spaced HP";
            case 21: return "4 Evenly Spaced HP";
            default:
                int offset = hp - 20;
                return $"The {offset}{GetOrdinalSuffix(offset)} HP";
        }
    }

    private static string GetOrdinalSuffix(int num)
    {
        if (num % 100 >= 11 && num % 100 <= 13) return "th";
        switch (num % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }
}

// ============================================================================================
// PAYLOAD TARGET HELPER
// ============================================================================================

public static class PayloadTargetHelper
{
    /// <summary>
    /// Formats payload target overrides according to game engine rules.
    /// Handles enemy redirection (#togfri) and hat wrappers for group targets (AllAllies, AllEnemies, Everyone, Self).
    /// </summary>
    public static string FormatTargetedPayload(string innerPayload, DiceSideData.PayloadTarget target, bool togtime)
    {
        if (string.IsNullOrWhiteSpace(innerPayload)) return "";

        string result = innerPayload.Trim();
        if (togtime) result += "#togtime";

        switch (target)
        {
            case DiceSideData.PayloadTarget.Enemy:
                return $"{result}#togfri";

            case DiceSideData.PayloadTarget.AllAllies:
                return $"hat.(Fey.sd.179.i.{{0}}.{result}#togtarg)";

            case DiceSideData.PayloadTarget.AllEnemies:
                return $"hat.(Fey.sd.179.i.{{0}}.{result}#togtarg#togfri)";

            case DiceSideData.PayloadTarget.Everyone:
                return $"hat.(Fey.sd.185.i.{{0}}.{result}#togtarg)";

            case DiceSideData.PayloadTarget.Self:
                return $"hat.(Fey.sd.186.i.{{0}}.{result}#togtarg)";

            case DiceSideData.PayloadTarget.Ally:
            case DiceSideData.PayloadTarget.None:
            default:
                return result;
        }
    }
}


// ============================================================================================
// ITEM MECHANIC AST NODES & CLAUSES
// ============================================================================================

[System.Serializable]
public class ItemPropertyNode : SDNode
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";

    public ItemPropertyNode(string key = "", string value = "")
    {
        Key = key;
        Value = value;
    }

    public override string Export()
    {
        if (string.IsNullOrWhiteSpace(Key)) return "";
        string cleanVal = StaticBranchTracing.StripOuterParens(Value);
        return $"{Key.Trim()}.({cleanVal})";
    }
}

/// <summary>
/// Full AST Node representing an ItemMechanic.
/// Handles explicit target chains, repeat multipliers (x2), pertier, unpack, prefixes, 
/// payloads, chained keywords (#), part indices, mrg, and splice suffixes.
/// </summary>
[System.Serializable]
public class ItemMechanicNode : SDNode
{
    public List<DiceTarget> TargetEnums { get; set; } = new List<DiceTarget>();
    public List<string> TargetStrings { get; set; } = new List<string>(); // Supports custom target aliases
    public int RepeatTimes { get; set; } = 1;
    public bool PerTier { get; set; } = false;
    public bool Unpack { get; set; } = false;
    public string Prefix { get; set; } = "";

    public SDNode PayloadNode { get; set; }
    public string RawPayloadString { get; set; } = "";

    public List<string> ChainedKeywords { get; set; } = new List<string>();
    public int? PartIndex { get; set; } = null;
    public int Multiplier { get; set; } = 1;
    public string MergedItem { get; set; } = "";
    public string SplicedItem { get; set; } = "";

    public override string Export()
    {
        List<string> parts = new List<string>();

        // 1. Targets
        if (TargetStrings.Count > 0)
            parts.AddRange(TargetStrings);
        else if (TargetEnums.Count > 0)
            parts.AddRange(TargetEnums.Select(t => t.ToString().ToLower()));

        // 2. Repeat Multipliers / Flags
        bool payloadHandlesMultiplier = PayloadNode is SDRootNode root && root.XMultiplier >= 2;
        if (RepeatTimes >= 2 && RepeatTimes <= 9 && !payloadHandlesMultiplier)
            parts.Add($"x{RepeatTimes}");

        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");

        // 3. Prefix
        if (!string.IsNullOrWhiteSpace(Prefix)) parts.Add(Prefix.Trim());

        // 4. Core Payload
        string corePayload = RawPayloadString;
        if (PayloadNode != null)
        {
            if (Prefix == "hat" && PayloadNode is HeroEntityNode hero)
            {
                string safeHat = hero.Export();
                corePayload = safeHat.StartsWith("(") ? safeHat : $"({safeHat})";
            }
            else
            {
                corePayload = PayloadNode.Export();
            }
        }

        // 5. Chained Keywords (#)
        if (ChainedKeywords.Count > 0)
        {
            corePayload += "#" + string.Join("#", ChainedKeywords);
        }

        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);

        // 6. Suffixes (part, m, mrg, splice)
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) { parts.Add("m"); parts.Add(Multiplier.ToString()); }
        if (!string.IsNullOrWhiteSpace(MergedItem)) parts.Add($"mrg.{MergedItem.Trim()}");
        if (!string.IsNullOrWhiteSpace(SplicedItem)) parts.Add($"splice.{SplicedItem.Trim()}");

        return string.Join(".", parts);
    }
}

// ============================================================================================
// ITEM FLAG CLAUSES
// ============================================================================================

[System.Serializable]
public class ClearDescClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public override string Export() => "cleardesc";
}

[System.Serializable]
public class ClearIconClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.CoreMetadata;
    public override string Export() => "clearicon";
}

// ============================================================================================
// COMPREHENSIVE ITEM ENTITY AST NODE
// ============================================================================================

/// <summary>
/// Fully articulated AST Node representing an ItemData structure.
/// Enforces self-bracketing doctrine, global tag concatenation (&hidden),
/// container properties, optimized mechanics chaining, and item metadata export.
/// </summary>
[System.Serializable]
public class ItemEntityNode : SDRootNode
{
    public string UnityName { get; set; } = "New Item";
    public string EntityName { get; set; } = "";
    public int? Tier { get; set; } = null;
    public bool ClearDescription { get; set; } = false;
    public bool ClearIcon { get; set; } = false;
    public string ImageOverride { get; set; } = "";
    public string Doc { get; set; } = "";

    public List<string> GlobalTags { get; set; } = new List<string>();
    public List<ItemPropertyNode> Containers { get; set; } = new List<ItemPropertyNode>();
    public List<ItemMechanicNode> Mechanics { get; set; } = new List<ItemMechanicNode>();

    public override string Export()
    {
        List<string> chainParts = new List<string>();

        // 1. Containers
        foreach (var container in Containers)
        {
            string exp = container?.Export();
            if (!string.IsNullOrEmpty(exp)) chainParts.Add(exp);
        }

        // 2. Mechanics
        if (Mechanics.Count > 0)
        {
            List<string> mechExports = Mechanics.Select(m => m.Export()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (mechExports.Count > 0)
                chainParts.Add(string.Join("#", mechExports));
        }

        // 3. Visual Modifiers
        var visualClauses = Clauses.Where(c => c.Category == ClauseCategory.Visuals);
        foreach (var vc in visualClauses)
        {
            string exp = vc?.Export();
            if (!string.IsNullOrEmpty(exp)) chainParts.Add(exp);
        }

        if (!string.IsNullOrWhiteSpace(ImageOverride) && !ImageOverride.Equals("None", StringComparison.OrdinalIgnoreCase))
            chainParts.Add($"img.{SDData.FormatSpecialImageName(ImageOverride.Trim())}");

        // 4. Flags & Metadata
        if (ClearDescription) chainParts.Add("cleardesc");
        if (ClearIcon) chainParts.Add("clearicon");
        if (Tier.HasValue) chainParts.Add($"tier.{Tier.Value}");
        if (!string.IsNullOrWhiteSpace(EntityName)) chainParts.Add($"n.{EntityName.Trim()}");
        if (!string.IsNullOrWhiteSpace(Doc)) chainParts.Add($"doc.{Doc.Trim()}");

        StringBuilder sb = new StringBuilder(string.Join(".", chainParts));

        // 5. Global Tags (&hidden, &temporary)
        foreach (var tag in GlobalTags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                sb.Append($"&{tag.Trim()}");
        }

        string payload = sb.ToString();
        if (string.IsNullOrWhiteSpace(payload)) return "";

        // Strictly enforce self-bracketing doctrine: (payload)
        return $"({payload})";
    }
}

// ============================================================================================
// ITEM CLAUSE DEFINITION
// ============================================================================================

/// <summary>
/// AST clause representing stock items or item payloads (e.g. i.Fly or i.(top.cast.DSVarhest)).
/// </summary>
[System.Serializable]
public class ItemClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public string ItemName { get; set; } = "";
    public SDNode ItemNode { get; set; }

    public ItemClause(string itemName = "")
    {
        ItemName = itemName;
    }

    public ItemClause(SDNode itemNode)
    {
        ItemNode = itemNode;
    }

    public override string Export()
    {
        if (ItemNode != null)
        {
            string nodeExport = ItemNode.Export();
            if (string.IsNullOrWhiteSpace(nodeExport)) return "";
            return nodeExport.StartsWith("i.", StringComparison.OrdinalIgnoreCase) ? nodeExport : $"i.{nodeExport}";
        }

        if (string.IsNullOrWhiteSpace(ItemName)) return "";
        string clean = ItemName.Trim();

        // If it already starts with i. or is self-bracketed item payload, export cleanly
        if (clean.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
            return clean;

        if (clean.StartsWith("(") && clean.EndsWith(")"))
            return $"i.{clean}";

        return $"i.{clean}";
    }
}