// ============================================================================================
// COMPREHENSIVE MONSTER ENTITY AST NODE
// ============================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Fully articulated AST Node representing a complete MonsterData structure.
/// Owns pre-name visual modifier ordering, balance (.bal.) trailing placement, 
/// nested container payloads (egg/vase/jinx/orb), and trait/curse item routing.
/// </summary>
[System.Serializable]
public class MonsterEntityNode : SDRootNode
{
    // Domain Fields
    public string BaseMonster { get; set; } = "Wolf";
    public MonsterContainerNode ContainerPayload { get; set; }
    public string EntityName { get; set; } = "";
    public int Hp { get; set; } = 0;
    public string Bal { get; set; } = "";
    public MonsterSize Size { get; set; } = MonsterSize.HeroSized;
    public string ImageOverride { get; set; } = "";
    public string Doc { get; set; } = "";
    public string Doc2 { get; set; } = "";

    // Face Modifications & Dice Sides
    public SdClause DiceSides { get; set; } = new SdClause();

    // Payload Collections
    public List<SDClause> FaceModifiers { get; set; } = new List<SDClause>();
    public List<SDClause> InnerPayloads { get; set; } = new List<SDClause>(); // Dice-affecting items / Hats
    public List<SDClause> Traits { get; set; } = new List<SDClause>(); // t.<trait>
    public List<SDClause> Curses { get; set; } = new List<SDClause>(); // t.jinx.(curse)
    public List<SDClause> OuterPayloads { get; set; } = new List<SDClause>(); // Non-dice items, custom abilities

    /// <summary>
    /// Synchronizes Monster size constraint based on baseMonster name.
    /// </summary>
    public void SyncMonsterSize()
    {
        if (string.IsNullOrEmpty(BaseMonster)) return;

        string cleanName = BaseMonster;
        int dotIndex = cleanName.IndexOf('.');
        if (dotIndex != -1)
        {
            cleanName = cleanName.Substring(dotIndex + 1);
            cleanName = StaticBranchTracing.StripOuterParens(cleanName);
            int nextDot = cleanName.IndexOf('.');
            if (nextDot != -1) cleanName = cleanName.Substring(0, nextDot);
        }

        if (Enum.TryParse<MonsterType>(cleanName, true, out MonsterType parsedType))
        {
            if (MonsterDatabase.SizeMapping.TryGetValue(parsedType, out MonsterSize mappedSize))
            {
                Size = mappedSize;
                return;
            }
        }
        Size = MonsterSize.HeroSized;
    }

    /// <summary>
    /// Formats the monster into a lightweight spirit expression: (baseMonster.doc.description)
    /// </summary>
    public string ExportAsSpirit()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(SDData.FormatSpecialImageName(BaseMonster));
        if (!string.IsNullOrWhiteSpace(Doc))
            sb.Append($".doc.{Doc.Trim()}");
        return $"({sb})";
    }

    public override string Export()
    {
        StringBuilder sb = new StringBuilder();

        // 1. Base Monster Identifier (or Container Node)
        string baseIdStr = ContainerPayload != null ? ContainerPayload.Export() : BaseMonster;
        if (!string.IsNullOrWhiteSpace(baseIdStr))
            sb.Append(SDData.FormatSpecialImageName(baseIdStr.Trim()));

        bool hasImageOverride = !string.IsNullOrWhiteSpace(ImageOverride) &&
                                !ImageOverride.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                                !ImageOverride.Equals(BaseMonster, StringComparison.OrdinalIgnoreCase);

        // 2. Pre-Name Visual Modifiers (Monsters insert color modifiers BEFORE name if no image override)
        var visualClauses = Clauses.Where(c => c.Category == ClauseCategory.Visuals).ToList();
        if (!hasImageOverride)
        {
            foreach (var vc in visualClauses)
            {
                string exp = vc?.Export();
                if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
            }
        }

        // 3. Name (Omit if identical to base monster name)
        if (!string.IsNullOrWhiteSpace(EntityName) && !string.Equals(EntityName, BaseMonster, StringComparison.OrdinalIgnoreCase))
            sb.Append($".n.{EntityName.Trim()}");

        // 4. HP
        if (Hp > 0) sb.Append($".hp.{Hp}");

        // 5. Dice Sides
        if (DiceSides != null)
        {
            string sdExp = DiceSides.Export();
            if (!string.IsNullOrEmpty(sdExp)) sb.Append($".{sdExp}");
        }

        // 6. Face Modifiers
        foreach (var fm in FaceModifiers)
        {
            string exp = fm?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        // 7. Inner Payloads (Hats & Dice-affecting items)
        foreach (var inner in InnerPayloads)
        {
            string exp = inner?.Export();
            if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
        }

        // 8. Image Override AND Visual Modifiers (Appended at end of core body if image override present)
        if (hasImageOverride)
        {
            sb.Append($".img.{SDData.FormatSpecialImageName(ImageOverride.Trim())}");
            foreach (var vc in visualClauses)
            {
                string exp = vc?.Export();
                if (!string.IsNullOrEmpty(exp)) sb.Append($".{exp}");
            }
        }

        string coreBody = sb.ToString().TrimStart('.');
        coreBody = $"({coreBody})";

        // 9. Outer Payloads (Traits, Curses, Non-dice Items, Custom Abilities)
        StringBuilder outerSb = new StringBuilder();

        foreach (var trait in Traits)
        {
            string exp = trait?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".i.({exp})");
        }

        foreach (var curse in Curses)
        {
            string exp = curse?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".i.({exp})");
        }

        foreach (var outer in OuterPayloads)
        {
            string exp = outer?.Export();
            if (!string.IsNullOrEmpty(exp)) outerSb.Append($".{exp}");
        }

        // 10. Balance Clause (.bal.)
        if (!string.IsNullOrWhiteSpace(Bal))
            outerSb.Append($".bal.{Bal.Trim()}");

        // 11. Trailing Documentation (.doc.)
        if (!string.IsNullOrWhiteSpace(Doc)) outerSb.Append($".doc.{Doc.Trim()}");
        if (!string.IsNullOrWhiteSpace(Doc2)) outerSb.Append($".doc.{Doc2.Trim()}");

        string fullExport = $"{coreBody}{outerSb}";

        // 12. Centrally apply xMultiplier
        if (XMultiplier >= 2 && XMultiplier <= 9)
        {
            return $"x{XMultiplier}.{fullExport}";
        }

        return fullExport;
    }
}