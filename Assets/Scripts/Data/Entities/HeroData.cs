
// ==========================================
// 3. HERO DATA
// ==========================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;

public static class HeroDomainRules
{
    // Change from string[] to HashSet<string>
    public static readonly HashSet<string> HeroPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata"
    };

    public static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "n", "img", "col", "hp", "tier", "hsv", "hsl", "hue",
        "p", "b", "rect", "draw", "thue", "adj", "speech", "doc"
    };
}

[System.Serializable]
public class HeroData : EntityData
{
    public string baseReplica;
    public string colorClass;
    public int tier;
    public int? adj;
    public string speech;

    [SerializeField] public List<SpellData> customSpells;
    [SerializeField] public List<TacticData> customTactics;

    public IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>();
            if (customSpells != null) combined.AddRange(customSpells);
            if (customTactics != null) combined.AddRange(customTactics);
            return combined;
        }
    }

    public void InitializeAsDefault()
    {
        entityName = "NewEntity"; baseReplica = "Statue"; colorClass = "y"; imageOverride = "None"; hp = 7; tier = 1;
    }

    public void InitializeAsBlank()
    {
        entityName = null; imageOverride = null; baseReplica = null; colorClass = null;
        hp = 0; h = 0; s = 0; v = 0; tier = 0; hue = 0;
        hsl = null; p = null; b = null; rect = null; draw = null; thue = null; doc = null; speech = null; adj = null;
        items = new List<string>(); traits = new List<string>(); blessings = new List<string>(); curses = new List<string>();
        baseAbilityData = new List<string>(); customSpells = new List<SpellData>(); customTactics = new List<TacticData>();
        customPayloads = new List<CustomPayload>();
        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
    }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string heroCore = StaticBranchTracing.StripOuterParens(chunks[0]);

        List<string> chains = StaticBranchTracing.TopLevelSplit(heroCore, '#');
        bool isFirstChain = true;
        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');

            if (isFirstChain && tokens.Count > 0)
            {
                isFirstChain = false;
                string firstLower = tokens[0].ToLower();
                if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
                {
                    baseReplica = tokens[0];
                }
            }

            ExtractKnowledge(tokens);
        }
    }

    private void ExtractKnowledge(List<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = StaticBranchTracing.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens);
                continue;
            }

            if (TryProcessMetadata(tokens, ref i, tokenLower)) continue;
            else if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            else if (TryProcessCollections(tokens, ref i, tokenLower)) continue;
            else if (tokenLower == "i")
            {
                StaticBranchTracing.ProcessAndRouteProperty(
                    tokens, ref i,
                    HeroDomainRules.HeroPropertyKeys, // FIX: Pass the full property list so it breaks on 'i', 't', and 'sd'
                    this
                );
            }
        }
    }

    private bool TryProcessMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (!HeroDomainRules.MetadataKeys.Contains(tokenLower)) return false;
        if (i + 1 >= tokens.Count) return false;

        string nextVal = tokens[++i];
        switch (tokenLower)
        {
            case "replica": baseReplica = nextVal; break;
            case "n": entityName = nextVal; break;
            case "img": imageOverride = nextVal; break;
            case "col": colorClass = nextVal; break;
            case "hp": if (int.TryParse(nextVal, out int hpVal)) hp = hpVal; break;
            case "tier": if (int.TryParse(nextVal, out int t)) tier = t; break;
            case "hsv":
                string[] hsvParts = nextVal.Split(':');
                if (hsvParts.Length == 3) { int.TryParse(hsvParts[0], out h); int.TryParse(hsvParts[1], out s); int.TryParse(hsvParts[2], out v); }
                break;
            case "hsl": hsl = nextVal; break;
            case "hue": if (int.TryParse(nextVal, out int hVal)) hue = hVal; break;
            case "p": p = nextVal; break;
            case "b": b = nextVal; break;
            case "rect": rect = nextVal; break;
            case "draw": draw = nextVal; break;
            case "thue": thue = nextVal; break;
            case "adj": if (int.TryParse(nextVal, out int a)) adj = a; break;
            case "speech": speech = nextVal; break;
            case "doc": doc = nextVal; break;
        }
        return true;
    }

    private bool TryProcessDiceSides(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower != "sd" || i + 1 >= tokens.Count) return false;
        InitializeDiceFaces();
        string[] faces = tokens[++i].Split(':');
        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
        {
            if (faces[f] == "0") continue;
            string[] faceParts = faces[f].Split('-');
            if (faceParts.Length == 2) { int.TryParse(faceParts[0], out diceSides[f].effectID); int.TryParse(faceParts[1], out diceSides[f].pips); }
        }
        return true;
    }

    private bool TryProcessCollections(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        if (tokenLower == "t") { traits.AddRange(StaticBranchTracing.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "gift") { blessings.AddRange(StaticBranchTracing.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "abilitydata")
        {
            string payload = tokens[++i];
            if (payload.StartsWith("(")) { AddCustomAbility(AbilityData.CreateAbility(payload)); }
            else { baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#')); }
            return true;
        }
        return false;
    }

    public override string Export()
    {
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica)) { heroSb.Append($"replica.{FormatName(baseReplica)}"); if (!hasImageOverride) AppendColorModifier(heroSb); }
        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");
        if (!string.IsNullOrEmpty(colorClass)) heroSb.Append($".col.{colorClass}");
        if (hp > 0) heroSb.Append($".hp.{hp}");
        if (tier > 0) heroSb.Append($".tier.{tier}");
        if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");
        if (!string.IsNullOrEmpty(thue)) heroSb.Append($".thue.{thue}");

        AppendDiceSides(heroSb);
        if (!string.IsNullOrEmpty(speech)) heroSb.Append($".speech.{speech}");
        if (!string.IsNullOrEmpty(doc)) heroSb.Append($".doc.{doc}");

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        if (hasImageOverride) { heroSb.Append($".img.{FormatName(imageOverride)}"); AppendColorModifier(heroSb); }
        heroSb.Append(")");

        StringBuilder thoseSb = new StringBuilder();
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
        if (blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) thoseSb.Append($".gift.{FormatName(bl)}");
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
        if (baseAbilityData != null) foreach (var ab in baseAbilityData) if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");

        if (customPayloads != null)
        {
            foreach (var payload in customPayloads)
            {
                string exported = payload.Export();
                if (!string.IsNullOrEmpty(exported)) thoseSb.Append($".{exported}");
            }
        }

        // Generate the base hero string (with modifiers, traits, and items if any)
        string baseHeroString = thoseSb.Length == 0 ? heroSb.ToString() : $"({heroSb.ToString()}{thoseSb.ToString()})";

        // Append custom abilities on the outside of the base hero structure
        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            StringBuilder abilitiesSb = new StringBuilder();
            foreach (var cab in customAbilityData)
            {
                if (cab != null)
                {
                    abilitiesSb.Append($".abilitydata.({cab.Export()})");
                }
            }

            if (abilitiesSb.Length > 0)
            {
                return $"({baseHeroString}){abilitiesSb.ToString()}";
            }
        }

        return baseHeroString;
    }

    public void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();
        if (ability is SpellData spell) { if (!customSpells.Any(s => s.entityName == spell.entityName)) customSpells.Add(spell); }
        else if (ability is TacticData tactic) { if (!customTactics.Any(t => t.entityName == tactic.entityName)) customTactics.Add(tactic); }
    }

    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (baseReplica != null && !string.IsNullOrEmpty(baseReplica.ToString())) sb.AppendLine($"{indent}Base Replica: {baseReplica}");
        if (!string.IsNullOrEmpty(colorClass)) sb.AppendLine($"{indent}Color Class: {colorClass}");
        if (tier != 0) sb.AppendLine($"{indent}Tier: {tier}");
        if (hp != 0) sb.AppendLine($"{indent}HP: {hp}");
        if (!string.IsNullOrEmpty(imageOverride))
        {
            string displayValue = imageOverride.Length > 32 ? "<base64 string img>" : imageOverride;
            sb.AppendLine($"{indent}Image Override: {displayValue}");
        }

        if (diceSides != null && diceSides.Length > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                if (side != null && (side.effectID != 0 || side.pips != 0))
                {
                    if (!headerPrinted) { sb.AppendLine($"{indent}Dice Sides:"); headerPrinted = true; }
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0) sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (blessings != null && blessings.Count > 0) sb.AppendLine($"{indent}Blessings: {string.Join(", ", blessings)}");
        if (curses != null && curses.Count > 0) sb.AppendLine($"{indent}Curses: {string.Join(", ", curses)}");
        if (baseAbilityData != null && baseAbilityData.Count > 0) sb.AppendLine($"{indent}Base Abilities: {string.Join(", ", baseAbilityData)}");
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
        if (sb.Length > 0) UnityEngine.Debug.Log($"{indent}--- HERO DATA DEBUG (COMPACT) ---\n" + sb.ToString());
    }
}