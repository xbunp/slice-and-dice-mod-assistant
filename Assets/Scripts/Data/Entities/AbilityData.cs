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
        public override string ExportWrapped() => string.Empty;
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

    public override void Parse(string data)
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
    protected override bool TryProcessSpecificMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "col" && i + 1 < tokens.Count) { colorClass = tokens[++i]; return true; }
        if (tokenLower == "tier" && i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) { tier = t; return true; }
        if (tokenLower == "adj" && i + 1 < tokens.Count && int.TryParse(tokens[++i], out int a)) { adj = a; return true; }
        if (tokenLower == "speech" && i + 1 < tokens.Count) { speech = tokens[++i]; return true; }

        return base.TryProcessSpecificMetadata(tokens, ref i, tokenLower);
    }

    public override string Export() { return ExportWrapped(); }
    public abstract string ExportWrapped();
    protected string ExportInner()
    {
        StringBuilder sb = new StringBuilder();
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica)) sb.Append(FormatName(baseReplica));
        if (!hasImageOverride) AppendColorModifier(sb);

        // FIX: Removed `if (hp > 0)` to prevent duplicating the `.hp.5` threshold 
        if (!string.IsNullOrEmpty(colorClass)) sb.Append($".col.{colorClass}");

        AppendDiceSides(sb);

        // Standard items and the newly string-preserved unmappable items export here natively
        if (items != null) foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".i.{itm}");

        // Export legacy custom payloads if any survived
        if (customPayloads != null)
        {
            foreach (var cp in customPayloads)
            {
                string e = cp.Export();
                if (!string.IsNullOrEmpty(e)) sb.Append($".{e}");
            }
        }

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

        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);

        if (hasImageOverride) { sb.Append($".img.{FormatName(imageOverride)}"); AppendColorModifier(sb); }
        if (thue != null && thue.colorOffset != 0) sb.Append($".{PackTHue(thue)}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey") sb.Append($".n.{FormatName(entityName)}");

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
        if (ability is TriggerHPData) return $"i.triggerhpdata.{ability.ExportWrapped()}";
        if (ability is OnHitData) return $"i.onhitdata.{ability.ExportWrapped()}";

        return $"abilitydata.{ability.ExportWrapped()}";
    }
    public static AbilityData CreateAbility(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        string trimmed = data.Trim();

        if (trimmed.StartsWith("onhitdata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.onhitdata.", StringComparison.OrdinalIgnoreCase))
        {
            OnHitData onHit = new OnHitData();
            onHit.Parse(trimmed);
            return onHit;
        }

        if (trimmed.StartsWith("triggerhpdata.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.triggerhpdata.", StringComparison.OrdinalIgnoreCase))
        {
            TriggerHPData triggerHP = new TriggerHPData();
            triggerHP.Parse(trimmed);
            return triggerHP;
        }

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
            var face1 = probe.diceSides[0];
            bool face1Defined = face1 != null && (face1.effectID != 0 || face1.pips != 0);

            if (face1Defined)
            {
                bool otherFacesDefined = false;
                for (int i = 1; i < probe.diceSides.Length; i++)
                {
                    var face = probe.diceSides[i];
                    if (face != null && (face.effectID != 0 || face.pips != 0)) { otherFacesDefined = true; break; }
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