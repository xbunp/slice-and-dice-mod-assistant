using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class AbilityDomainRules
{
    public static readonly HashSet<string> AbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "i", "t", "gift", "abilitydata", "triggerhpdata", "onhitdata", "n", "img", "hp", "col", "tier",
        "hsv", "hsl", "hue", "p", "b", "rect", "draw", "thue", "doc", "adj", "speech", "orb"
    };

    public static readonly HashSet<string> AbilityStartTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "orb", "triggerhpdata", "onhitdata", "abilitydata", "cast" };

    public static readonly string[] AbilityPrefixes = new string[]
    {
        "i.triggerhpdata.",
        "triggerhpdata.",
        "i.onhitdata.",
        "abilitydata.",
        "onhitdata.",
        "i.t.orb.",
        "t.orb.",
        "cast.",
        "orb."
}   ;

    public static bool IsAbilityStartSequence(List<string> tokens, int index)
    {
        string token = tokens[index];
        if (AbilityStartTokens.Contains(token)) return true;

        // Check if the sequence matches [s|t]<HeroType>.[abilitydata|triggerhpdata|onhitdata]
        if (index + 1 < tokens.Count)
        {
            string nextToken = tokens[index + 1].ToLower();
            if (nextToken == "abilitydata" || nextToken == "triggerhpdata" || nextToken == "onhitdata")
            {
                if (token.Length > 1 && (token[0] == 's' || token[0] == 'S' || token[0] == 't' || token[0] == 'T'))
                {
                    string candidateHero = token.Substring(1);
                    // Strictly verify against the actual game registry enum
                    if (Enum.TryParse(candidateHero, true, out HeroType _))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    public static int GetAbilityBlockLength(List<string> tokens, int startIndex)
    {
        int endIndex = startIndex;

        // Check if startIndex is a strictly validated carrier
        if (endIndex + 1 < tokens.Count)
        {
            string token = tokens[endIndex];
            string nextToken = tokens[endIndex + 1].ToLower();
            if (nextToken == "abilitydata" || nextToken == "triggerhpdata" || nextToken == "onhitdata")
            {
                if (token.Length > 1 && (token[0] == 's' || token[0] == 'S' || token[0] == 't' || token[0] == 'T'))
                {
                    string candidateHero = token.Substring(1);
                    if (Enum.TryParse(candidateHero, true, out HeroType _))
                    {
                        endIndex++; // Consume the carrier token safely
                    }
                }
            }
        }

        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            endIndex++;

            if (peek.StartsWith("(") && peek.EndsWith(")")) break;

            if (endIndex - startIndex >= 2)
            {
                if (endIndex < tokens.Count && tokens[endIndex].ToLower() == "abilitydata") continue;
                break;
            }
        }
        return endIndex - startIndex;
    }
}

[System.Serializable]
public abstract class AbilityData : HeroData
{
    public string baseDummyType { get => baseReplica; set => baseReplica = value; }
    public DiceSideData PrimaryEffect { get => diceSides[0]; set => diceSides[0] = value; }
    public DiceSideData SecondaryEffect { get => diceSides[1]; set => diceSides[1] = value; }

    private class ProbeAbilityData : AbilityData
    {
        public ProbeAbilityData()
        {
            if (diceSides == null)
            {
                diceSides = new DiceSideData[6];
                for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData();
            }
        }
        protected override string ExportCore() => string.Empty;
    }

    private void CleanData()
    {
        items = new List<string>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();
        baseAbilityData = new List<string>();
        customPayloads = new List<CustomPayload>();
        _itemPipeline = new List<ItemData>();
    }
    protected override void ParseCore(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        CleanData(); // not sure if this is really needed.

        string core = StripPrefix(data);
        core = StaticBranchTracing.StripOuterParens(core);

        // 2. Extract standard string chunks (isolate global tags if present)
        List<string> chunks = StaticBranchTracing.TopLevelSplit(core, '&');
        string mainPayload = StaticBranchTracing.StripOuterParens(chunks[0]);

        // 3. Tokenize by dot notation (without pre-splitting by '#' so items parse correctly)
        List<string> tokens = StaticBranchTracing.TopLevelSplit(mainPayload, '.');

        if (tokens.Count > 0)
        {
            string firstTokenLower = tokens[0].ToLower();
            // 4. Safely extract base template (e.g. Fey, sthief, etc)
            if (!AbilityDomainRules.AbilityKeys.Contains(firstTokenLower) && !ItemDomainRules.MechanicPrefixes.Contains(firstTokenLower))
            {
                baseReplica = ExtractBaseIdentifier(tokens[0]);
                tokens.RemoveAt(0);
            }
            else if (string.IsNullOrEmpty(baseReplica))
            {
                baseReplica = "Fey";
            }
        }

        // 5. Route through unified parsing pipeline (defined in EntityData)
        ExtractKnowledge(tokens, _itemPipeline, processTraitsAndCollections: true);
        ExecuteItemPipeline();

        // 6. Post-process structural constraints
        if (this is SpellData spell)
        {
            if (spell.diceSides != null && spell.diceSides.Length > 4 && spell.diceSides[4] != null)
                spell.manaCost = spell.diceSides[4].pips;
        }
    }
    protected override bool TryProcessSpecificMetadata(TokenStream stream)
    {
        string tokenLower = stream.Peek().ToLower();
        switch (tokenLower)
        {
            case "col": stream.Consume(); colorClass = stream.Consume(); return true;
            case "tier": stream.Consume(); if (int.TryParse(stream.Consume(), out int t)) tier = t; return true;
            case "adj": stream.Consume(); if (int.TryParse(stream.Consume(), out int a)) adj = a; return true;
            case "speech": stream.Consume(); speech = stream.Consume(); return true;
        }
        return base.TryProcessSpecificMetadata(stream);
    }
    protected override string ExportCore()
    {
        string inner = ExportInner();
        if (ExternalGameRegistry.IsValidAbility(inner)) return inner;
        return StaticBranchTracing.SafeBracket(inner);
    }

    // Update ExportInner in Assets/Scripts/Data/Entities/AbilityData.cs

    protected virtual string ExportInner()
    {
        StringBuilder sb = new StringBuilder();
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;
        if (!string.IsNullOrEmpty(baseReplica)) sb.Append(FormatName(baseReplica));
        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey")
            sb.Append($".n.{FormatName(entityName)}");
        if (!string.IsNullOrEmpty(colorClass)) sb.Append($".col.{colorClass}");
        if (hp > 0) sb.Append($".hp.{hp}");
        AppendDiceSides(sb);
        if (items != null) foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".i.{itm}");

        if (baseAbilityData != null && baseAbilityData.Count > 0)
        {
            List<string> formattedAbilities = new List<string>();
            foreach (var ab in baseAbilityData)
            {
                if (string.IsNullOrEmpty(ab)) continue;
                formattedAbilities.Add(ab.StartsWith("(") && ab.EndsWith(")") ? ab : $"({ab})");
            }
            if (formattedAbilities.Count > 0) sb.Append($".abilitydata.{string.Join("#", formattedAbilities)}");
        }

        // --- NEW: Fix truncation of nested Carrier Abilities (e.g. sthief.abilitydata) ---
        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            foreach (var cab in customAbilityData)
            {
                if (cab == null) continue;
                if (cab is SpellData || cab is TacticData)
                {
                    sb.Append($".{AbilityData.GetFormattedExportString(cab)}");
                }
                else if (cab is OrbData orb)
                {
                    sb.Append($".{orb.ExportAsTrait(useITPrefix: false)}");
                }
                else
                {
                    string pfx = cab is TriggerHPData ? "triggerhpdata" :
                                 cab is OnHitData ? "onhitdata" : "abilitydata";
                    sb.Append($".{pfx}.{cab.Export()}");
                }
            }
        }
        // -------------------------------------------------------------------------------

        if (customPayloads != null)
        {
            foreach (var cp in customPayloads)
            {
                string e = cp.Export();
                if (!string.IsNullOrEmpty(e)) sb.Append($".{e}");
            }
        }
        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);

        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(sb);
        }
        else
        {
            AppendColorModifier(sb);
        }

        if (thue != null && thue.colorOffset != 0) sb.Append($".{PackTHue(thue)}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(doc2)) sb.Append($".doc.{doc2}");
        return sb.ToString();
    }
    public static string StripPrefix(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return string.Empty;
        string clean = data.Trim();

        foreach (string prefix in AbilityDomainRules.AbilityPrefixes)
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return clean.Substring(prefix.Length).Trim();
            }
        }
        return clean;
    }

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
    protected static string GetOrdinalSuffix(int num)
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
    public static string GetFormattedExportString(AbilityData ability)
    {
        if (ability == null) return string.Empty;
        if (ability is OrbData orb) return orb.ExportAsTrait(useITPrefix: true);
        if (ability is TriggerHPData) return $"i.triggerhpdata.{ability.Export()}";
        if (ability is OnHitData) return $"i.onhitdata.{ability.Export()}";
        return $"abilitydata.{ability.Export()}";
    }
    private static bool IsFaceModified(DiceSideData face)
    {
        if (face == null) return false;
        if (face.effectID != 0 || face.pips != 0) return true;
        if (!string.IsNullOrEmpty(face.facadeID)) return true;
        if (face.keywords != null && face.keywords.Count > 0) return true;
        if (!string.IsNullOrEmpty(face.payload)) return true;
        if (face.faceType != DiceSideData.DiceFaceType.Base) return true;
        if (!string.IsNullOrEmpty(face.sidesc)) return true;
        if (face.sideItems != null && face.sideItems.Count > 0) return true; // <-- ADD THIS
        return false;
    }

    public static AbilityData CreateAbility(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        string trimmed = data.Trim();

        // 1. Explicitly check for AbilityData/Cast to guarantee CreateSpellOrTactic mapping before heuristics
        if (trimmed.StartsWith("abilitydata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.abilitydata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("cast.", StringComparison.OrdinalIgnoreCase))
        {
            string cleanStr = StripPrefix(trimmed);
            return CreateSpellOrTactic(cleanStr);
        }

        // 2. Explicitly check for OnHit
        if (trimmed.StartsWith("onhitdata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.onhitdata.", StringComparison.OrdinalIgnoreCase))
        {
            OnHitData onHit = new OnHitData();
            onHit.Parse(trimmed);
            return onHit;
        }

        // 3. Explicitly check for TriggerHP
        if (trimmed.StartsWith("triggerhpdata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.triggerhpdata.", StringComparison.OrdinalIgnoreCase))
        {
            TriggerHPData triggerHP = new TriggerHPData();
            triggerHP.Parse(trimmed);
            return triggerHP;
        }

        // 4. Fallback heuristic for un-prefixed ability strings
        string clean = StripPrefix(data);
        ProbeAbilityData probe = new ProbeAbilityData();
        probe.Parse(clean);
        if (trimmed.StartsWith("orb.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.t.orb.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("t.orb.", StringComparison.OrdinalIgnoreCase))
        {
            OrbData orb = new OrbData();
            orb.Parse(clean);
            return orb;
        }
        if (probe.hp != 0)
        {
            TriggerHPData triggerHP = new TriggerHPData();
            triggerHP.Parse(clean);
            return triggerHP;
        }

        bool isSpell = false;
        if (probe.diceSides != null && probe.diceSides.Length > 4)
        {
            var face5 = probe.diceSides[4];
            if (face5 != null && face5.effectID == 76 && face5.pips > 0) isSpell = true;
        }
        if (isSpell)
        {
            SpellData spell = new SpellData();
            spell.Parse(clean);
            return spell;
        }

        bool onlyLeftFace = false;
        if (probe.diceSides != null && probe.diceSides.Length > 0)
        {
            if (IsFaceModified(probe.diceSides[0]))
            {
                bool otherFacesDefined = false;
                for (int i = 1; i < probe.diceSides.Length; i++)
                {
                    if (IsFaceModified(probe.diceSides[i])) { otherFacesDefined = true; break; }
                }
                if (!otherFacesDefined)
                {
                    bool hasExtraData = (probe.items != null && probe.items.Count > 0) ||
                                        (probe.traits != null && probe.traits.Count > 0) ||
                                        (probe.blessings != null && probe.blessings.Count > 0) ||
                                        (probe.baseAbilityData != null && probe.baseAbilityData.Count > 0) ||
                                        (probe.customPayloads != null && probe.customPayloads.Count > 0);
                    if (!hasExtraData) onlyLeftFace = true;
                }
            }
        }

        if (onlyLeftFace)
        {
            OnHitData onHit = new OnHitData();
            onHit.Parse(clean);
            return onHit;
        }

        TacticData tactic = new TacticData();
        tactic.Parse(clean);
        return tactic;
    }
    public static AbilityData CreateSpellOrTactic(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        ProbeAbilityData probe = new ProbeAbilityData();
        probe.Parse(data);

        bool isSpell = false;
        if (probe.diceSides != null && probe.diceSides.Length > 4)
        {
            var face5 = probe.diceSides[4];
            if (face5 != null && face5.effectID == 76 && face5.pips > 0) isSpell = true;
        }

        AbilityData result = isSpell ? (AbilityData)new SpellData() : new TacticData();
        result.Parse(data);
        return result;
    }

    public void DebugAbilityCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string typeName = this is SpellData ? "SPELL" : this is TacticData ? "TACTIC" : this is OrbData ? "ORB" : this.GetType().Name.ToUpper();

        sb.AppendLine($"{indent}--- {typeName} DATA DEBUG ---");
        if (this is OrbData orb)
        {
            if (orb.isHardcoded)
                sb.AppendLine($"{indent}Hardcoded Orb: {orb.hardcodedAbilityName}");
            else
                sb.AppendLine($"{indent}Carrier Prefix: {orb.carrierPrefix}");
        }

        sb.AppendLine($"{indent}--- {typeName} DATA DEBUG ---");
        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (!string.IsNullOrEmpty(baseReplica)) sb.AppendLine($"{indent}Replica: {baseReplica}");

        if (this is SpellData spell) sb.AppendLine($"{indent}Mana Cost: {spell.manaCost}");

        if (diceSides != null)
        {
            bool headerPrinted = false;
            for (int i = 0; i < diceSides.Length; i++)
            {
                if (this is SpellData && i == 4) continue;
                DiceSideData side = diceSides[i];
                if (side != null && (side.effectID != 0 || side.pips != 0))
                {
                    if (!headerPrinted) { sb.AppendLine($"{indent}Dice Sides:"); headerPrinted = true; }
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0) sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (items != null && items.Count > 0) sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");

        if (customPayloads != null && customPayloads.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Payloads ({customPayloads.Count}):");
            for (int i = 0; i < customPayloads.Count; i++)
            {
                var cp = customPayloads[i];
                sb.AppendLine($"{indent}  [{i}] Prefix: '{cp.Prefix}' | [✓ Unpacked {cp.Data?.GetType().Name}]");

                if (cp.Data is ItemData id) id.DebugContentsToConsole(indent + "        ");
                else if (cp.Data is HeroData hd) hd.DebugContentsToConsoleCompact(indent + "        ");
                else if (cp.Data is AbilityData ad) ad.DebugAbilityCompact(indent + "        ");
                else if (cp.Data is ModifierData md) md.DebugContentsToConsole(indent + "        ");
                else if (cp.Data is MonsterData mnd) mnd.DebugContentsToConsoleCompact(indent + "        ");
            }
        }

        UnityEngine.Debug.Log(sb.ToString());
    }
}

[System.Serializable]
public class SpellData : AbilityData
{
    [Header("Spell Properties")]
    public int manaCost = 1;

    protected override string ExportInner()
    {
        bool isBaseAbility = ExternalGameRegistry.IsValidAbility(baseReplica);
        bool hasCustomMana = diceSides[4] != null && (diceSides[4].effectID != 0 || diceSides[4].pips != 0);

        // Prevents base spells from having `.sd.0:0:0:0:76-1` forcibly appended, 
        // which would ruin the string matching check
        if (!isBaseAbility || hasCustomMana)
        {
            if (diceSides[4] == null) diceSides[4] = new DiceSideData();
            if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
            diceSides[4].pips = manaCost;
        }
        return base.ExportInner();
    }

    public DiceSideData ManaCostSide
    {
        get => diceSides[4];
        set => diceSides[4] = value;
    }

    public SpellData() : base()
    {
        InitializeDiceFaces();
        diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;
    }
}

[System.Serializable]
public class TacticData : AbilityData
{
    public DiceSideData TacticCostTop { get => diceSides[2]; set => diceSides[2] = value; }
    public DiceSideData TacticCostBottom { get => diceSides[3]; set => diceSides[3] = value; }
    public DiceSideData TacticCostRightmost { get => diceSides[5]; set => diceSides[5] = value; }

    public TacticData() : base()
    {
        InitializeDiceFaces();
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
    }

    protected override string ExportInner()
    {
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
        EnsureKeywordCostItems(); // <--- ADDED
        return base.ExportInner();
    }

    // <--- ADDED METHODS BELOW --->
    public void EnsureKeywordCostItems()
    {
        EnsureCostSideKeywordItem(2, "top");
        EnsureCostSideKeywordItem(3, "bot");
        EnsureCostSideKeywordItem(5, "right");
    }

    private void EnsureCostSideKeywordItem(int faceIndex, string sidePrefix)
    {
        if (diceSides == null || faceIndex >= diceSides.Length) return;
        var face = diceSides[faceIndex];

        if (items == null) items = new List<string>();

        // Strip previous DSVarhest items for this side to prevent duplicates
        items.RemoveAll(it =>
            it.Equals($"({sidePrefix}.cast.DSVarhest)", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"({sidePrefix}.cast.DSVarhest#Fly)", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"{sidePrefix}.cast.DSVarhest", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"{sidePrefix}.cast.DSVarhest#Fly", StringComparison.OrdinalIgnoreCase));

        // Inject appropriate keyword modifier item
        if (face != null && face.effectID == 13)
        {
            if (face.pips == 2)
            {
                items.Add($"({sidePrefix}.cast.DSVarhest)");
            }
            else if (face.pips == 4)
            {
                items.Add($"({sidePrefix}.cast.DSVarhest#Fly)");
            }
        }
    }
}

[System.Serializable]
public class OnHitData : AbilityData
{
    public OnHitData() : base()
    {
        InitializeDiceFaces();
        // OnHit only uses the left side (0), zero out the rest by default
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
    }

    protected override string ExportInner()
    {
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
        return base.ExportInner();
    }
}

[System.Serializable]
public class TriggerHPData : AbilityData
{
    public TriggerHPData() : base()
    {
        InitializeDiceFaces();
        // TriggerHP only uses the left side (0), zero out the rest
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
    }

    protected override string ExportInner()
    {
        // Ensure unused faces are cleared
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
        // ExportInner() handles base properties, Color, AND HP now!
        // No manual HP. appending needed.

        // TODO: LATER: sanity double check, make sure HP IS there for an TriggerHPData
        return base.ExportInner();
    }
}

[System.Serializable]
public class OrbData : AbilityData
{
    // List of valid base-game targetless abilities defined in the request
    public static readonly HashSet<string> ValidBaseOrbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Restore", "Gaze", "Slice", "Balance", "Circle", "Glow", "Infuse", "Pray", "Scald", "Burn",
        "Foretell", "Drop", "Clink", "Operate", "Soothe", "Blades", "Crush", "Aid", "Invoke", "Mana",
        "Waste", "Wings", "Heat", "Hack", "Invest", "Luck", "Devoid", "Formation"
    };

    public bool isHardcoded = false;
    public string hardcodedAbilityName = "";
    public string carrierPrefix = "sthief.abilitydata";

    public OrbData() : base()
    {
        InitializeDiceFaces();
    }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        string clean = data.Trim();

        // Strip prefix elements to process nested custom ability structure cleanly
        if (clean.StartsWith("i.t.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(4);
        else if (clean.StartsWith("t.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(2);

        if (clean.StartsWith("orb.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(4);

        int openParen = clean.IndexOf('(');
        int closeParen = clean.LastIndexOf(')');

        if (openParen >= 0 && closeParen > openParen)
        {
            isHardcoded = false;
            string prefix = clean.Substring(0, openParen).TrimEnd('.');
            if (!string.IsNullOrEmpty(prefix))
            {
                carrierPrefix = prefix;
            }
            string innerPayload = clean.Substring(openParen + 1, closeParen - openParen - 1);
            base.Parse(innerPayload);
        }
        else
        {
            isHardcoded = true;
            hardcodedAbilityName = clean;
            entityName = clean;
            baseReplica = clean;
        }
    }

    protected override string ExportCore()
    {
        if (isHardcoded) return hardcodedAbilityName.ToLower();
        return base.ExportCore(); // Base class will safely handle the bracketing check
    }


    /*
    public string ExportAsTrait(bool useITPrefix = true)
    {
        string prefix = useITPrefix ? "i.t.orb." : "t.orb.";
        if (isHardcoded)
        {
            string name = !string.IsNullOrEmpty(hardcodedAbilityName) ? hardcodedAbilityName.ToLower() : (entityName?.ToLower() ?? "slice");
            return $"{prefix}{name}";
        }
        string carrier = !string.IsNullOrEmpty(carrierPrefix) ? carrierPrefix : "sthief.abilitydata";
        return $"{prefix}{carrier}.({ExportInner()})";
    }
    */

    public string ExportAsTrait(bool useITPrefix = true)
    {
        string prefix = useITPrefix ? "i.t.orb." : "t.orb.";
        if (isHardcoded)
        {
            string name = !string.IsNullOrEmpty(hardcodedAbilityName) ? hardcodedAbilityName.ToLower() : (entityName?.ToLower() ?? "slice");
            return $"{prefix}{name}";
        }
        string carrier = !string.IsNullOrEmpty(carrierPrefix) ? carrierPrefix : "sthief.abilitydata";
        // Export() now guarantees (...)
        return $"{prefix}{carrier}.{Export()}";
    }
}