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
}

[System.Serializable]
public abstract class AbilityData : HeroData
{
    public string baseDummyType { get => baseReplica; set => baseReplica = value; }
    public DiceSideData PrimaryEffect { get => diceSides[0]; set => diceSides[0] = value; }
    public DiceSideData SecondaryEffect { get => diceSides[1]; set => diceSides[1] = value; }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string core = StaticBranchTracing.StripOuterParens(chunks[0]);

        List<string> chains = StaticBranchTracing.TopLevelSplit(core, '#');
        bool isFirstChain = true;
        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');

            bool isFirstToken = isFirstChain;
            isFirstChain = false;

            ExtractKnowledge(tokens, ref isFirstToken);
        }

        if (this is SpellData spell)
        {
            if (spell.diceSides != null && spell.diceSides.Length > 4) spell.manaCost = spell.diceSides[4].pips;
        }
    }

    private void ExtractKnowledge(List<string> tokens, ref bool isFirstToken)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string originalToken = tokens[i];
            string tokenLower = originalToken.ToLower();

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerChains = StaticBranchTracing.TopLevelSplit(inner, '#');
                foreach (var chain in innerChains)
                {
                    if (string.IsNullOrWhiteSpace(chain)) continue;
                    List<string> innerTokens = StaticBranchTracing.TopLevelSplit(chain, '.');
                    ExtractKnowledge(innerTokens, ref isFirstToken);
                }
                continue;
            }

            if (isFirstToken)
            {
                isFirstToken = false;
                if (!AbilityDomainRules.AbilityKeys.Contains(tokenLower))
                {
                    baseReplica = originalToken;
                    continue;
                }
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) entityName = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) imageOverride = tokens[++i]; break;
                case "doc": if (i + 1 < tokens.Count) doc = tokens[++i]; break;
                case "col": if (i + 1 < tokens.Count) colorClass = tokens[++i]; break;
                case "hp": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hVal)) hp = hVal; break;
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int tVal)) tier = tVal; break;
                case "adj": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int aVal)) adj = aVal; break;
                case "speech": if (i + 1 < tokens.Count) speech = tokens[++i]; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsv = tokens[++i].Split(':');
                        if (hsv.Length == 3 && int.TryParse(hsv[0], out h) && int.TryParse(hsv[1], out s) && int.TryParse(hsv[2], out v)) { }
                    }
                    break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) hue = hueVal; break;
                case "p": if (i + 1 < tokens.Count) p = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) b = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) rect = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) draw = tokens[++i]; break;

                case "thue": if (i + 1 < tokens.Count) thue = UnpackTHue(tokens[++i]); break;

                case "sd":
                    InitializeDiceFaces();
                    if (i + 1 < tokens.Count)
                    {
                        string[] faces = tokens[++i].Split(':');
                        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                        {
                            if (faces[f] == "0" || faces[f] == "0-0") continue;
                            string[] faceParts = faces[f].Split('-');
                            int.TryParse(faceParts[0], out diceSides[f].effectID);
                            if (faceParts.Length > 1) int.TryParse(faceParts[1], out diceSides[f].pips);
                        }
                    }
                    break;

                case "i":
                    StaticBranchTracing.ProcessAndRouteProperty(tokens, ref i, AbilityDomainRules.AbilityKeys, this);
                    break;

                case "t":
                case "gift":
                case "abilitydata":
                    int startIndex = i + 1;
                    if (startIndex >= tokens.Count) break;

                    int endIndex = startIndex;
                    while (endIndex < tokens.Count)
                    {
                        string peek = tokens[endIndex].ToLower();
                        if (AbilityDomainRules.AbilityKeys.Contains(peek)) break;
                        endIndex++;
                    }

                    int count = endIndex - startIndex;
                    if (count == 0) break;

                    List<string> payloadTokens = tokens.GetRange(startIndex, count);
                    string joinedPayload = string.Join(".", payloadTokens);
                    i = endIndex - 1;

                    if (tokenLower == "t") traits.Add(joinedPayload);
                    else if (tokenLower == "gift") blessings.Add(joinedPayload);
                    else if (tokenLower == "abilitydata") baseAbilityData.Add(joinedPayload);
                    break;
            }
        }
    }

    public override string Export() { return ExportWrapped(); }
    public abstract string ExportWrapped();

    protected string ExportInner()
    {
        StringBuilder sb = new StringBuilder();
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica)) sb.Append(FormatName(baseReplica));
        if (!hasImageOverride) AppendColorModifier(sb);

        AppendDiceSides(sb);

        if (items != null) foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".i.{itm}");
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

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);

        if (hasImageOverride) { sb.Append($".img.{FormatName(imageOverride)}"); AppendColorModifier(sb); }
        if (thue != null && thue.colorOffset != 0) sb.Append($".{PackTHue(thue)}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey") sb.Append($".n.{FormatName(entityName)}");

        return sb.ToString();
    }

    public static AbilityData FigureItOut(string data) => CreateAbility(data);
    public static AbilityData WhatAmI(string data) => CreateAbility(data);

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

    public static AbilityData CreateAbility(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        ProbeAbilityData probe = new ProbeAbilityData();
        probe.Parse(data);

        string trimmed = data.Trim();
        if (trimmed.StartsWith("orb.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("i.t.orb.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("t.orb.", StringComparison.OrdinalIgnoreCase))
        {
            OrbData orb = new OrbData();
            orb.Parse(data);
            return orb;
        }

        if (probe.hp != 0)
        {
            TriggerHPData triggerHP = new TriggerHPData();
            triggerHP.Parse(data);
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
            spell.Parse(data);
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
            onHit.Parse(data);
            return onHit;
        }

        TacticData tactic = new TacticData();
        tactic.Parse(data);
        return tactic;
    }

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

    // FOR ON HIT DATA / TARGETTED DATA.

    /// <summary>
    /// Translates the parsed 'hp' value into a human-readable description of 
    /// which health pips on the target entity are affected according to the Textmod API rules.
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
}
