using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum AbilityPrefixKind
{
    None,
    AbilityData,
    OnHitData,
    TriggerHPData,
    Cast,
    Orb
}

public static class AbilityDomainRules
{
    public const string DefaultCarrier = "sthief";
    public static readonly Dictionary<string, string> AbilityOnlyKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Channel", "ritemx.302ea5e.part.0" },
        { "Cooldown", "k.exert" },
        { "Deplete", "ritemx.539ce9a" },
        { "Future", "ritemx.dae9" },
        { "Spell Rescue", "ritemx.62e8" },
        { "Single Cast", "k.singleuse" }
    };
    private static readonly Dictionary<string, string> NormalizedAbilityOnlyKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "channel", "ritemx.302ea5e.part.0" },
        { "cooldown", "k.exert" },
        { "deplete", "ritemx.539ce9a" },
        { "future", "ritemx.dae9" },
        { "spellrescue", "ritemx.62e8" },
        { "singlecast", "k.singleuse" }
    };
    public static bool TryGetAbilityOnlyKeywordItem(string keyword, out string itemToken)
    {
        itemToken = null;
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        string normalized = keyword.Trim().Replace(" ", "").Replace("_", "");
        return NormalizedAbilityOnlyKeywords.TryGetValue(normalized, out itemToken);
    }
    public static bool IsAbilityOnlyKeywordItem(string itemToken)
    {
        if (string.IsNullOrWhiteSpace(itemToken)) return false;
        string clean = StaticBranchTracing.StripOuterParens(itemToken.Trim());
        return AbilityOnlyKeywords.Values.Any(v => string.Equals(v, clean, StringComparison.OrdinalIgnoreCase));
    }
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
        "i.abilitydata.",
        "abilitydata.",
        "onhitdata.",
        "i.t.orb.",
        "t.orb.",
        "cast.",
        "orb."
    };
    public static bool IsAbilityCarrierToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 2) return false;
        char prefix = char.ToLowerInvariant(token[0]);
        if (prefix != 's' && prefix != 't') return false;
        string candidateHero = token.Substring(1);
        if (char.IsDigit(candidateHero[0]) || candidateHero[0] == '-' || candidateHero[0] == '+' || candidateHero.IndexOf(',') >= 0)
            return false;
        return Enum.TryParse(candidateHero, true, out HeroType parsed) && parsed != HeroType.None;
    }
    public static bool TryParseAbilityPrefix(string data, out AbilityPrefixKind kind, out string carrier, out string remainder)
    {
        kind = AbilityPrefixKind.None;
        carrier = string.Empty;
        remainder = data;
        if (string.IsNullOrWhiteSpace(data)) return false;
        string clean = data.Trim();
        int firstParen = clean.IndexOf('(');
        string prefixRegion = firstParen >= 0 ? clean.Substring(0, firstParen) : clean;
        List<(string token, int endAfterDot)> parts = new List<(string, int)>();
        int start = 0;
        for (int i = 0; i <= prefixRegion.Length; i++)
        {
            if (i == prefixRegion.Length || prefixRegion[i] == '.')
            {
                if (i > start)
                {
                    string tok = prefixRegion.Substring(start, i - start);
                    int endAfter = (i < clean.Length && clean[i] == '.') ? i + 1 : i;
                    parts.Add((tok, endAfter));
                }
                start = i + 1;
            }
        }
        if (parts.Count == 0) return false;
        int idx = 0;
        // Optional leading "i."
        if (parts[idx].token.Equals("i", StringComparison.OrdinalIgnoreCase))
        {
            idx++;
            if (idx >= parts.Count) return false;
        }
        // Optional "t." before "orb." (i.t.orb. / t.orb.)
        if (parts[idx].token.Equals("t", StringComparison.OrdinalIgnoreCase) &&
            idx + 1 < parts.Count &&
            parts[idx + 1].token.Equals("orb", StringComparison.OrdinalIgnoreCase))
        {
            kind = AbilityPrefixKind.Orb;
            int afterOrb = parts[idx + 1].endAfterDot;
            remainder = afterOrb < clean.Length ? clean.Substring(afterOrb).Trim() : string.Empty;
            return true;
        }
        // Direct "orb."
        if (parts[idx].token.Equals("orb", StringComparison.OrdinalIgnoreCase))
        {
            kind = AbilityPrefixKind.Orb;
            int afterOrb = parts[idx].endAfterDot;
            remainder = afterOrb < clean.Length ? clean.Substring(afterOrb).Trim() : string.Empty;
            return true;
        }
        // Direct "cast."
        if (parts[idx].token.Equals("cast", StringComparison.OrdinalIgnoreCase))
        {
            kind = AbilityPrefixKind.Cast;
            int afterCast = parts[idx].endAfterDot;
            remainder = afterCast < clean.Length ? clean.Substring(afterCast).Trim() : string.Empty;
            return true;
        }
        // Optional "learn."
        bool hasLearn = false;
        if (parts[idx].token.Equals("learn", StringComparison.OrdinalIgnoreCase))
        {
            hasLearn = true;
            idx++;
            if (idx >= parts.Count) return false;
        }
        // Optional [s|t]<HeroType> carrier token
        if (IsAbilityCarrierToken(parts[idx].token))
        {
            carrier = parts[idx].token;
            idx++;
            if (idx >= parts.Count) return false;
        }
        else if (hasLearn)
        {
            string nextLower = parts[idx].token.ToLowerInvariant();
            if (nextLower != "abilitydata" && nextLower != "onhitdata" && nextLower != "triggerhpdata")
                return false;
        }
        string kindToken = parts[idx].token.ToLowerInvariant();
        switch (kindToken)
        {
            case "abilitydata":
                kind = AbilityPrefixKind.AbilityData;
                break;
            case "onhitdata":
                kind = AbilityPrefixKind.OnHitData;
                break;
            case "triggerhpdata":
                kind = AbilityPrefixKind.TriggerHPData;
                break;
            default:
                return false;
        }
        int remStart = parts[idx].endAfterDot;
        remainder = remStart < clean.Length ? clean.Substring(remStart).Trim() : string.Empty;
        return true;
    }
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
                if (IsAbilityCarrierToken(token))
                {
                    return true;
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
                if (IsAbilityCarrierToken(token))
                {
                    endIndex++; // Consume the carrier token safely
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
        CleanData();
        string core = StripPrefix(data);
        core = StaticBranchTracing.StripOuterParens(core);
        List<string> chunks = StaticBranchTracing.TopLevelSplit(core, '&');
        string mainPayload = StaticBranchTracing.StripOuterParens(chunks[0]);
        List<string> tokens = StaticBranchTracing.TopLevelSplit(mainPayload, '.');
        if (tokens.Count > 0)
        {
            string firstTokenLower = tokens[0].ToLower();
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
        ExtractKnowledge(tokens, _itemPipeline, processTraitsAndCollections: true);
        ExecuteItemPipeline();

        // STRICT INVARIANT: Only set entityName if an explicit .n. tag was defined in the data
        if (string.IsNullOrEmpty(entityName) || entityName.Contains(".") || entityName.StartsWith("("))
        {
            var match = System.Text.RegularExpressions.Regex.Match(data, @"\.n\.([^\.\#\)]+)");
            if (match.Success)
            {
                entityName = match.Groups[1].Value.Trim();
            }
            else
            {
                entityName = null; // Do NOT synthesize a name. Absence of name must remain absent.
            }
        }

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
        var drawVisuals = visuals?
            .Where(vis => vis.Type == VisualType.Draw && !string.IsNullOrWhiteSpace(vis.RawValue))
            .ToList();
        bool hasDraw = drawVisuals != null && drawVisuals.Count > 0;
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica)) sb.Append(FormatName(baseReplica));
        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey")
            sb.Append($".n.{FormatName(entityName)}");
        if (!string.IsNullOrEmpty(colorClass)) sb.Append($".col.{colorClass}");
        if (hp > 0) sb.Append($".hp.{hp}");
        AppendDiceSides(sb);

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
        // --- Fix truncation of nested Carrier Abilities (e.g. sthief.abilitydata) ---
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
        // ---------------------------------------------------------------------------
        List<string> deferredAbilityKeywordPayloads = new List<string>();
        if (customPayloads != null)
        {
            foreach (var cp in customPayloads)
            {
                if (cp == null) continue;
                string e = cp.Export();
                if (string.IsNullOrEmpty(e)) continue;
                if (cp.Type == PayloadType.Item && !e.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
                {
                    e = $"i.{e}";
                }

                // Strip i. and parens to inspect the true token
                string check = e;
                if (check.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
                    check = check.Substring(2);
                check = StaticBranchTracing.StripOuterParens(check.Trim());

                if (AbilityDomainRules.IsAbilityOnlyKeywordItem(check))
                {
                    deferredAbilityKeywordPayloads.Add(e);
                }
                else
                {
                    sb.Append($".{e}");
                }
            }
        }
        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);

        // Emit non-ability-keyword items first, then ability-only keywords AFTER all face modifiers and payloads
        if (items != null)
        {
            foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x) && !AbilityDomainRules.IsAbilityOnlyKeywordItem(x)))
            {
                sb.Append($".i.{itm}");
            }
            foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x) && AbilityDomainRules.IsAbilityOnlyKeywordItem(x)))
            {
                sb.Append($".i.{itm}");
            }
        }
        foreach (var deferred in deferredAbilityKeywordPayloads)
        {
            sb.Append($".{deferred}");
        }

        // Enforce exact visual order: .img.<img|Void> -> .draw.<sprite>:<x>:<y> -> .hsv.<h>:<s>:<v>
        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(imageOverride)}");
        }
        else if (hasDraw)
        {
            sb.Append(".img.Void");
        }
        if (hasDraw)
        {
            foreach (var dv in drawVisuals)
            {
                sb.Append($".draw.{dv.RawValue}:{dv.x}:{dv.y}");
            }
        }
        AppendColorModifier(sb, skipDraw: true);

        if (thue != null && thue.colorOffset != 0) sb.Append($".{PackTHue(thue)}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(doc2)) sb.Append($".doc.{doc2}");
        return sb.ToString();
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

        if (AbilityDomainRules.TryParseAbilityPrefix(trimmed, out AbilityPrefixKind prefixKind, out _, out string remainder))
        {
            switch (prefixKind)
            {
                case AbilityPrefixKind.AbilityData:
                case AbilityPrefixKind.Cast:
                    return CreateSpellOrTactic(remainder);
                case AbilityPrefixKind.OnHitData:
                    OnHitData onHit = new OnHitData();
                    onHit.Parse(remainder);
                    return onHit;
                case AbilityPrefixKind.TriggerHPData:
                    TriggerHPData triggerHP = new TriggerHPData();
                    triggerHP.Parse(remainder);
                    return triggerHP;
                case AbilityPrefixKind.Orb:
                    OrbData orb = new OrbData();
                    orb.Parse(remainder);
                    return orb;
            }
        }

        // Fallback heuristic for un-prefixed ability strings
        string clean = StripPrefix(data);
        ProbeAbilityData probe = new ProbeAbilityData();
        probe.Parse(clean);
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
    private static bool TryStripLearnAbilityPrefix(string clean, out string stripped)
    {
        stripped = clean;
        if (string.IsNullOrWhiteSpace(clean)) return false;

        if (!clean.StartsWith("i.learn.", StringComparison.OrdinalIgnoreCase) &&
            !clean.StartsWith("learn.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string marker = ".abilitydata.";
        int firstParen = clean.IndexOf('(');
        int markerIdx = clean.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIdx > 0 && (firstParen < 0 || markerIdx < firstParen))
        {
            stripped = clean.Substring(markerIdx + marker.Length).Trim();
            return true;
        }

        return false;
    }
    public static string StripPrefix(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return string.Empty;
        string clean = data.Trim();
        if (AbilityDomainRules.TryParseAbilityPrefix(clean, out _, out _, out string remainder))
        {
            return remainder.Trim();
        }
        return clean;
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

    /// <summary>
    /// Returns the clean display name of the ability, never returning raw syntax chains or export brackets.
    /// </summary>
    public string GetCleanName()
    {
        // 1. If entityName is populated, ensure it is not raw syntax
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            string clean = entityName.Trim();
            if (!clean.StartsWith("(") && !clean.Contains(".sd.") && !clean.Contains("abilitydata.") && !clean.Contains("learn."))
            {
                return clean;
            }
            // If raw syntax leaked into entityName, extract the .n. tag if present
            var match = System.Text.RegularExpressions.Regex.Match(clean, @"\.n\.([^\.\#\)]+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // 2. Hardcoded Orb check
        if (this is OrbData orb && orb.isHardcoded && !string.IsNullOrWhiteSpace(orb.hardcodedAbilityName))
        {
            return orb.hardcodedAbilityName;
        }

        // 3. Stock ability check via base replica
        if (!string.IsNullOrEmpty(baseReplica) && !baseReplica.Equals("Fey", StringComparison.OrdinalIgnoreCase) && !baseReplica.Equals("Statue", StringComparison.OrdinalIgnoreCase))
        {
            if (ExternalGameRegistry.IsValidAbility(baseReplica))
                return baseReplica;
        }

        // 4. Deterministic fallback based on ability kind & effect
        string typeLabel = this is SpellData ? "Spell" :
                           this is TacticData ? "Tactic" :
                           this is OnHitData ? "On Hit" :
                           this is TriggerHPData ? "Trigger HP" :
                           this is OrbData ? "Orb" : "Ability";

        if (diceSides != null && diceSides.Length > 0 && diceSides[0] != null && diceSides[0].effectID != 0)
        {
            return $"{typeLabel} (Effect {diceSides[0].effectID})";
        }

        return typeLabel;
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