// ============================================================================================
// COMPREHENSIVE HERO ENTITY AST NODE
// ============================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Fully articulated AST Node representing a complete HeroData structure.
/// Owns grammatical sequence, default color suppression, face modifier placement, 
/// late-inner ability routing (Spells/Tactics), and outer blessing/learn routing.
/// </summary>
[System.Serializable]
public class HeroEntityNode : SDRootNode
{
    // Domain Fields
    public string BaseReplica { get; set; } = "Statue";
    public string EntityName { get; set; } = "";
    public string ColorClass { get; set; } = "";
    public int Hp { get; set; } = 0;
    public int Tier { get; set; } = -1;
    public int? Adj { get; set; }
    public string Speech { get; set; } = "";
    public string ImageOverride { get; set; } = "";
    public string Doc { get; set; } = "";
    public string Doc2 { get; set; } = "";

    // Face Modifications & Dice Sides
    public SdClause DiceSides { get; set; } = new SdClause();

    // Payload Collections
    public List<SDClause> FaceModifiers { get; set; } = new List<SDClause>();
    public List<SDClause> InnerPayloads { get; set; } = new List<SDClause>(); // Items, Hats
    public List<SDClause> LateInnerPayloads { get; set; } = new List<SDClause>(); // Spells, Tactics
    public List<SDClause> Blessings { get; set; } = new List<SDClause>(); // gift.<blessing>
    public List<SDClause> LearnedAbilities { get; set; } = new List<SDClause>(); // learn.<ability>
    public List<SDClause> OuterPayloads { get; set; } = new List<SDClause>(); // Curses, Orbs, Modifiers

    /// <summary>
    /// Calculates effective tier falling back to base replica default if unassigned.
    /// </summary>
    public int GetEffectiveTier()
    {
        if (Tier >= 0) return Tier;
        if (!string.IsNullOrEmpty(BaseReplica) && SDColors.heroTiers.TryGetValue(BaseReplica, out int inherentTier))
            return inherentTier;
        return 1;
    }

    public override string Export()
    {
        StringBuilder sb = new StringBuilder();

        // 1. Base Replica Identifier
        if (!string.IsNullOrWhiteSpace(BaseReplica))
            sb.Append($"replica.{SDData.FormatSpecialImageName(BaseReplica.Trim())}");

        // 2. Name
        if (!string.IsNullOrWhiteSpace(EntityName))
            sb.Append($".n.{EntityName.Trim()}");

        // 3. Color Class (Suppresses default hero color automatically)
        if (!string.IsNullOrWhiteSpace(ColorClass))
        {
            ColClause colClause = new ColClause(ColorClass, BaseReplica);
            string colExp = colClause.Export();
            if (!string.IsNullOrEmpty(colExp)) sb.Append($".{colExp}");
        }

        // 4. HP
        if (Hp > 0) sb.Append($".hp.{Hp}");

        // 5. Tier
        if (Tier >= 0) sb.Append($".tier.{Tier}");

        // 6. Adj
        if (Adj.HasValue) sb.Append($".adj.{Adj.Value}");

        // 7. Dice Sides
        if (DiceSides != null)
        {
            string sdExp = DiceSides.Export();
            if (!string.IsNullOrEmpty(sdExp)) sb.Append($".{sdExp}");
        }

        // 8. Speech
        if (!string.IsNullOrWhiteSpace(Speech))
            sb.Append($".speech.{Speech.Trim()}");

        // 9. Face Modifiers (Grouped aliases/items)
        foreach (var fm in FaceModifiers)
        {
            string exp = fm?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        // 10. Inner Payloads (Dice-affecting items & hats)
        foreach (var inner in InnerPayloads)
        {
            string exp = inner?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        // 11. Image Override & Color/Visual Modifiers
        bool hasImageOverride = !string.IsNullOrWhiteSpace(ImageOverride) &&
                                !ImageOverride.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                                !ImageOverride.Equals(BaseReplica, StringComparison.OrdinalIgnoreCase);

        if (hasImageOverride)
            sb.Append($".img.{SDData.FormatSpecialImageName(ImageOverride.Trim())}");

        // Append Visual Clauses (HSV, Hue, P, THue, B, Draw, Rect)
        var visualClauses = Clauses.Where(c => c.Category == ClauseCategory.Visuals);
        foreach (var vc in visualClauses)
        {
            string exp = vc?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        // 12. Late Inner Payloads (Spells/Tactics - MUST be after visuals)
        foreach (var late in LateInnerPayloads)
        {
            string exp = late?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        string coreBody = sb.ToString().TrimStart('.');
        coreBody = $"({coreBody})";

        // 13. Outer Payloads (Blessings, Learn, Curses, Orbs, Outer Modifiers)
        StringBuilder outerSb = new StringBuilder();

        foreach (var blessing in Blessings)
        {
            string exp = blessing?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".gift.{exp}");
        }

        foreach (var learn in LearnedAbilities)
        {
            string exp = learn?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".learn.{exp}");
        }

        foreach (var outer in OuterPayloads)
        {
            string exp = outer?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".{exp}");
        }

        // 14. Trailing Documentation
        if (!string.IsNullOrWhiteSpace(Doc)) outerSb.Append($".doc.{Doc.Trim()}");
        if (!string.IsNullOrWhiteSpace(Doc2)) outerSb.Append($".doc.{Doc2.Trim()}");

        string fullExport = $"{coreBody}{outerSb}";

        // 15. Centrally apply xMultiplier
        if (XMultiplier >= 2 && XMultiplier <= 9)
        {
            return $"x{XMultiplier}.{fullExport}";
        }

        return fullExport;
    }
}