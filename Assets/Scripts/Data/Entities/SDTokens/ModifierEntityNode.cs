
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// AST node representing an advanced gameplay modifier (Curses, Blessings, Level tweaks).
/// Safely delegates complex payload handling (e.g., adding monsters, ending turns, transforming).
/// </summary>
[System.Serializable]
public class ModifierEntityNode : SDRootNode
{
    public List<string> GlobalTags { get; set; } = new List<string>(); // Supports &hidden, &temporary

    // Timing / Cadence
    public string FloorLevel { get; set; }
    public string Turn { get; set; }
    public string EveryXFights { get; set; }
    public string EveryXFightsOffset { get; set; }
    public string EveryXTurns { get; set; }
    public string Phase { get; set; }

    // Stacking / Scaling
    public string RepeatTimes { get; set; }
    public bool PerFightStack { get; set; }
    public bool PerBossStack { get; set; }
    public bool PerTurnStack { get; set; }

    // Target Scopes
    public bool InvertTarget { get; set; }
    public string HeroPosition { get; set; }
    public bool TargetAllHeroes { get; set; }
    public bool TargetAllMonsters { get; set; }
    public string DiceFaceTarget { get; set; }
    public bool Unpack { get; set; }

    // Action Payload
    public ModifierActionType ActionType { get; set; }
    public string CoreEffectName { get; set; }
    public SDNode Payload { get; set; } // Can hold ItemEntityNode, HeroEntityNode, MonsterEntityNode, AbilityEntityNode
    public string StringPayload { get; set; } // For party strings (hero+hero), delivery, or phase payloads

    // Combinators & Suffixes
    public ModifierEntityNode SplicedModifier { get; set; }
    public ModifierEntityNode ChainedModifier { get; set; }
    public int? PartIndex { get; set; }
    public string ModName { get; set; }
    public string DocDescription { get; set; }
    public string ModTier { get; set; }
    public bool IsSpirit { get; set; }

    public override string Export()
    {
        return ExportInternal(isRoot: true);
    }

    public string ExportInternal(bool isRoot)
    {
        List<string> parts = new List<string>();

        if (Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(FloorLevel)) parts.Add(FloorLevel);
        if (!string.IsNullOrEmpty(EveryXFights))
        {
            parts.Add(EveryXFights);
            if (!string.IsNullOrEmpty(EveryXFightsOffset)) parts.Add(EveryXFightsOffset);
        }
        if (!string.IsNullOrEmpty(EveryXTurns)) parts.Add(EveryXTurns);
        if (!string.IsNullOrEmpty(Turn)) parts.Add(Turn);
        if (!string.IsNullOrEmpty(Phase)) parts.Add(Phase);
        if (!string.IsNullOrEmpty(RepeatTimes)) parts.Add(RepeatTimes);
        if (PerFightStack) parts.Add("pl");
        if (PerBossStack) parts.Add("pb");
        if (PerTurnStack) parts.Add("pt");
        if (!string.IsNullOrEmpty(HeroPosition)) { parts.Add("h"); parts.Add(HeroPosition); }
        if (InvertTarget) parts.Add("inv");
        if (!string.IsNullOrEmpty(DiceFaceTarget)) parts.Add(DiceFaceTarget);
        if (TargetAllHeroes) parts.Add("hero");
        if (TargetAllMonsters) parts.Add("monster");

        // Action Payload Routing
        switch (ActionType)
        {
            case ModifierActionType.Choosable:
                parts.Add("ch"); parts.Add(StringPayload); break;
            case ModifierActionType.Phase:
                parts.Add("ph"); parts.Add(StringPayload); break;
            case ModifierActionType.PhaseIndexed:
                parts.Add("phi"); parts.Add(StringPayload); break;
            case ModifierActionType.PhaseModPick:
                parts.Add("phmp"); parts.Add(StringPayload); break;
            case ModifierActionType.AddMonster:
            case ModifierActionType.AddHero:
                parts.Add("add"); parts.Add(Payload?.Export() ?? ""); break;
            case ModifierActionType.GiveItem:
                parts.Add("i"); parts.Add(Payload?.Export() ?? ""); break;
            case ModifierActionType.AllItem:
                parts.Add("allitem"); parts.Add(Payload?.Export() ?? ""); break;
            case ModifierActionType.AllItemE:
                parts.Add("alliteme"); parts.Add(Payload?.Export() ?? ""); break;
            case ModifierActionType.PerItem:
                parts.Add("peritem"); parts.Add(Payload?.Export() ?? "()"); break;
            case ModifierActionType.Delivery:
                parts.Add("delivery"); parts.Add(StringPayload); break;
            case ModifierActionType.RMod:
                parts.Add("rmod"); parts.Add(StringPayload); break;
            case ModifierActionType.PartyHeroes:
                parts.Add("party"); parts.Add(StringPayload); break;
            case ModifierActionType.EndTurnAbility:
                parts.Add("ea");
                if (Payload != null) parts.Add($"sThief.abilitydata.{Payload.Export()}");
                break;
            case ModifierActionType.TransformHero:
                parts.Add("b"); parts.Add(Payload?.Export() ?? ""); break;
            case ModifierActionType.Jinx:
                parts.Add("jinx"); parts.Add((Payload as ModifierEntityNode)?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Vase:
                parts.Add("vase"); parts.Add((Payload as ModifierEntityNode)?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Self:
                parts.Add("self"); parts.Add((Payload as ModifierEntityNode)?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.MonsterSpirit:
                if (Payload is MonsterEntityNode mNode) parts.Add(mNode.ExportAsSpirit());
                parts.Add("spirit"); break;
            case ModifierActionType.InlineMonster:
                if (Payload is MonsterEntityNode mNodeInline) parts.Add(mNodeInline.ExportAsSpirit());
                break;
            case ModifierActionType.InlineHero:
                if (Payload != null) parts.Add(Payload.Export());
                break;
            case ModifierActionType.CoreModifier:
                if (!string.IsNullOrEmpty(CoreEffectName)) parts.Add(CoreEffectName);
                break;
        }

        if (PartIndex.HasValue) { parts.Add("part"); parts.Add(PartIndex.Value.ToString()); }
        if (!string.IsNullOrEmpty(ModTier)) { parts.Add("modtier"); parts.Add(ModTier); }

        string blockString = string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p)));

        if (SplicedModifier != null) blockString = $"{blockString}.splice.{SplicedModifier.ExportInternal(false)}";
        if (ChainedModifier != null) blockString = $"{blockString}&{ChainedModifier.ExportInternal(false)}";

        bool hasSuffixes = !string.IsNullOrEmpty(ModName) || !string.IsNullOrEmpty(DocDescription) || (IsSpirit && ActionType != ModifierActionType.MonsterSpirit) || GlobalTags.Count > 0;
        if (isRoot)
        {
            if (!string.IsNullOrEmpty(ModName)) blockString += $".mn.{ModName}";
            if (!string.IsNullOrEmpty(DocDescription)) blockString += $".doc.{DocDescription}";
            if (IsSpirit && ActionType != ModifierActionType.MonsterSpirit) blockString += ".spirit";
            foreach (var tag in GlobalTags) blockString += $"&{tag.Trim()}";
            return blockString;
        }
        if (hasSuffixes)
        {
            string suffixedBlock = StaticBranchTracing.SafeBracket(blockString);
            if (!string.IsNullOrEmpty(ModName)) suffixedBlock += $".mn.{ModName}";
            if (!string.IsNullOrEmpty(DocDescription)) suffixedBlock += $".doc.{DocDescription}";
            if (IsSpirit && ActionType != ModifierActionType.MonsterSpirit) suffixedBlock += ".spirit";
            return StaticBranchTracing.SafeBracket(suffixedBlock); // GlobalTags intentionally omitted from inner contexts
        }
        return StaticBranchTracing.SafeBracket(blockString);
    }
}
