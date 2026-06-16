using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


// ==========================================
// ABILITY DOMAIN DICTIONARY (Factual Rules)
// ==========================================
public static class AbilityDomainRules
{
    // Explicit keys recognized strictly by the Ability parser.
    // Anything preceding these in a chain is assumed to be an implicit BaseReplica.
    public static readonly HashSet<string> AbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "i", "t", "gift", "abilitydata", "n", "img", "hp", "col", "tier",
        "hsv", "hsl", "hue", "p", "b", "rect", "draw", "thue", "doc", "adj", "speech"
    };

    // Keys that signal a collection of modifiers that greedy-consume subsequent properties
    public static readonly HashSet<string> CollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "abilitydata"
    };
}

[System.Serializable]
public abstract class AbilityData : HeroData
{
    public string baseDummyType
    {
        get => baseReplica;
        set => baseReplica = value;
    }

    public DiceSideData PrimaryEffect
    {
        get => diceSides[0];
        set => diceSides[0] = value;
    }

    public DiceSideData SecondaryEffect
    {
        get => diceSides[1];
        set => diceSides[1] = value;
    }

    // ==========================================
    // KNOWLEDGE EXTRACTION (The Perfect Linear Parser)
    // ==========================================

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        UnityEngine.Debug.Log($"[AbilityData] Starting Parse on data length: {data.Length}");

        List<string> chunks = ItemData.TopLevelSplit(data.Trim(), '&');
        string core = chunks[0];

        // Strip structural wrapper scopes immediately since abilities often wrap `(Burst.sd...)`
        core = StripOuterParens(core);

        List<string> tokens = ItemData.TopLevelSplit(core, '.');

        bool isFirstToken = true;
        ExtractKnowledge(tokens, this, ref isFirstToken);

        // Spell specific resolution
        if (this is SpellData spell)
        {
            if (spell.diceSides != null && spell.diceSides.Length > 4)
            {
                spell.manaCost = spell.diceSides[4].pips;
            }
        }

        UnityEngine.Debug.Log($"[AbilityData] Parse Complete! Name: '{entityName}', Replica: '{baseReplica}'");
    }

    private string StripOuterParens(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string t = text.Trim();
        while (t.StartsWith("(") && t.EndsWith(")"))
        {
            int depth = 0; bool matching = true;
            for (int k = 0; k < t.Length - 1; k++)
            {
                if (t[k] == '(') depth++; else if (t[k] == ')') depth--;
                if (depth == 0) { matching = false; break; }
            }
            if (matching) t = t.Substring(1, t.Length - 2).Trim();
            else break;
        }
        return t;
    }

    private void ExtractKnowledge(List<string> tokens, AbilityData ability, ref bool isFirstToken)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string originalToken = tokens[i];
            string tokenLower = originalToken.ToLower();

            // If the chunk is entirely wrapped in parentheses, unwrap it to bypass the trap, preserving exact casing!
            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = ItemData.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens, ability, ref isFirstToken);
                continue;
            }

            // INTELLIGENT REPLICA DETECTION
            // If the very first parsed token is not a known property key, it acts as the implied replica.
            if (isFirstToken)
            {
                isFirstToken = false;
                if (!AbilityDomainRules.AbilityKeys.Contains(tokenLower))
                {
                    ability.baseReplica = originalToken; // Preserves exact casing
                    continue; // Consumed, move to next token in chain
                }
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) ability.entityName = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) ability.imageOverride = tokens[++i]; break;
                case "doc": if (i + 1 < tokens.Count) ability.doc = tokens[++i]; break;
                case "col": if (i + 1 < tokens.Count) ability.colorClass = tokens[++i]; break;
                case "hp": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hVal)) ability.hp = hVal; break;
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int tVal)) ability.tier = tVal; break;
                case "adj": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int aVal)) ability.adj = aVal; break;
                case "speech": if (i + 1 < tokens.Count) ability.speech = tokens[++i]; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsv = tokens[++i].Split(':');
                        if (hsv.Length == 3 && int.TryParse(hsv[0], out ability.h) && int.TryParse(hsv[1], out ability.s) && int.TryParse(hsv[2], out ability.v)) { }
                    }
                    break;
                case "hsl": if (i + 1 < tokens.Count) ability.hsl = tokens[++i]; break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) ability.hue = hueVal; break;
                case "p": if (i + 1 < tokens.Count) ability.p = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) ability.b = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) ability.rect = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) ability.draw = tokens[++i]; break;
                case "thue": if (i + 1 < tokens.Count) ability.thue = tokens[++i]; break;
                case "sd":
                    if (i + 1 < tokens.Count)
                    {
                        string[] faces = tokens[++i].Split(':');
                        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                        {
                            if (faces[f] == "0" || faces[f] == "0-0") continue;
                            string[] faceParts = faces[f].Split('-');
                            int.TryParse(faceParts[0], out ability.diceSides[f].effectID);
                            if (faceParts.Length > 1) int.TryParse(faceParts[1], out ability.diceSides[f].pips);
                        }
                    }
                    break;

                // GREEDY COLLECTION MODIFIERS
                case "i":
                case "t":
                case "gift":
                case "abilitydata":
                    int startIndex = i + 1;
                    if (startIndex >= tokens.Count) break;

                    int endIndex = startIndex;
                    // Greedily consume everything until the next explicit AbilityKey is encountered
                    while (endIndex < tokens.Count)
                    {
                        string peek = tokens[endIndex].ToLower();
                        if (AbilityDomainRules.AbilityKeys.Contains(peek)) break;
                        endIndex++;
                    }

                    int count = endIndex - startIndex;
                    if (count == 0) break;

                    List<string> payloadTokens = tokens.GetRange(startIndex, count);
                    string joinedPayload = string.Join(".", payloadTokens); // Reconstruct pristine string
                    i = endIndex - 1; // Advance loop to immediately before the next top-level key

                    if (tokenLower == "i") ability.items.Add(joinedPayload);
                    else if (tokenLower == "t") ability.traits.Add(joinedPayload);
                    else if (tokenLower == "gift") ability.blessings.Add(joinedPayload);
                    else if (tokenLower == "abilitydata") ability.baseAbilityData.Add(joinedPayload);
                    break;
            }
        }
    }

    // ==========================================
    // EXPORTING (Symmetrical Reconstruction)
    // ==========================================

    public override string Export()
    {
        return ExportWrapped();
    }

    public abstract string ExportWrapped();

    protected string ExportInner()
    {
        StringBuilder sb = new StringBuilder();

        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                imageOverride != "None" &&
                                imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
        {
            sb.Append(FormatName(baseReplica));
        }

        if (!hasImageOverride) AppendColorModifier(sb);

        AppendDiceSides(sb);

        if (items != null)
        {
            foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                sb.Append($".i.{itm}");
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
            if (formattedAbilities.Count > 0)
            {
                sb.Append($".abilitydata.{string.Join("#", formattedAbilities)}");
            }
        }

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            sb.Append(faceModifiers);
        }

        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(sb);
        }

        if (!string.IsNullOrEmpty(thue)) sb.Append($".thue.{thue}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");

        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey")
        {
            sb.Append($".n.{FormatName(entityName)}");
        }

        return sb.ToString();
    }

    public static AbilityData FigureItOut(string data) => CreateAbility(data);
    public static AbilityData WhatAmI(string data) => CreateAbility(data);

    // ==========================================
    // TYPE GUESSER FACTORY
    // ==========================================

    public static AbilityData CreateSpellOrTactic(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        ProbeAbilityData probe = new ProbeAbilityData();
        probe.Parse(data);

        bool isSpell = false;
        if (probe.diceSides != null && probe.diceSides.Length > 4)
        {
            var face5 = probe.diceSides[4];
            if (face5 != null && face5.effectID == 76 && face5.pips > 0)
            {
                isSpell = true;
            }
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
            if (face5 != null && face5.effectID == 76 && face5.pips > 0)
            {
                isSpell = true;
            }
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
                    if (face != null && (face.effectID != 0 || face.pips != 0))
                    {
                        otherFacesDefined = true;
                        break;
                    }
                }

                if (!otherFacesDefined)
                {
                    bool hasExtraData = (probe.items != null && probe.items.Count > 0) ||
                                        (probe.traits != null && probe.traits.Count > 0) ||
                                        (probe.blessings != null && probe.blessings.Count > 0) ||
                                        (probe.baseAbilityData != null && probe.baseAbilityData.Count > 0);

                    if (!hasExtraData)
                    {
                        onlyLeftFace = true;
                    }
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

    protected void InitializeDefaults()
    {
        baseReplica = "Fey";
    }

    public void DebugAbilityCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string typeName = this is SpellData ? "SPELL" : this is TacticData ? "TACTIC" : this.GetType().Name.ToUpper();

        sb.AppendLine($"{indent}--- {typeName} DATA DEBUG ---");
        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (!string.IsNullOrEmpty(baseReplica)) sb.AppendLine($"{indent}Replica: {baseReplica}");

        if (this is SpellData spell)
        {
            sb.AppendLine($"{indent}Mana Cost: {spell.manaCost}");
        }

        if (diceSides != null)
        {
            bool headerPrinted = false;
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                // Skip the mana cost side (index 4) for spells to avoid duplicate logging
                if (this is SpellData && i == 4) continue;

                if (side != null && (side.effectID != 0 || side.pips != 0))
                {
                    if (!headerPrinted)
                    {
                        sb.AppendLine($"{indent}Dice Sides:");
                        headerPrinted = true;
                    }
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0) sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (items != null && items.Count > 0) sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");
        if (baseAbilityData != null && baseAbilityData.Count > 0) sb.AppendLine($"{indent}Base Abilities: {string.Join(", ", baseAbilityData)}");

        UnityEngine.Debug.Log(sb.ToString());
    }
}
