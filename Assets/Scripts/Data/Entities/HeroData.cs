
// ==========================================
// 3. HERO DATA
// ==========================================

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class HeroDomainRules
{
    // Change from string[] to HashSet<string>
    public static readonly HashSet<string> HeroPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata", "orb"
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

    // Change from [SerializeField] public List<SpellData> customSpells;
    [System.NonSerialized] // Tells Unity's serializer to ignore this field
    [JsonProperty]         // Tells Newtonsoft to keep serializing this field
    public List<SpellData> customSpells;

    [System.NonSerialized]
    [JsonProperty]
    public List<TacticData> customTactics;

    protected override void ParseCore(string data)
    {
        InitializeAsBlank();
        if (string.IsNullOrWhiteSpace(data)) return;
        DetectBracketingState(data);

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string heroCore = StaticBranchTracing.StripOuterParens(chunks[0]);
        List<string> tokens = StaticBranchTracing.TopLevelSplit(heroCore, '.');
        if (tokens.Count > 0)
        {
            string baseTokenClean = ExtractBaseIdentifier(tokens[0]);
            string firstLower = baseTokenClean.ToLower();
            if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
            {
                baseReplica = baseTokenClean;
                tokens.RemoveAt(0); // Ensures ExtractKnowledge doesn't discard it
            }
        }
        ExtractKnowledge(tokens, _itemPipeline, true); // was false before
        ExecuteItemPipeline();
    }
    protected override bool TryProcessSpecificMetadata(TokenStream stream)
    {
        string tokenLower = stream.Peek().ToLower();
        switch (tokenLower)
        {
            case "replica": stream.Consume(); baseReplica = stream.Consume(); return true;
            case "col": stream.Consume(); colorClass = stream.Consume(); return true;
            case "tier": stream.Consume(); if (int.TryParse(stream.Consume(), out int t)) tier = t; return true;
            case "adj":
            case "x": stream.Consume(); if (int.TryParse(stream.Consume(), out int a)) adj = a; return true;
            case "speech": stream.Consume(); speech = stream.Consume(); return true;
        }
        return false;
    }

    public override IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>(base.customAbilityData); // Safely grabs base orbs
            if (customSpells != null) combined.AddRange(customSpells);
            if (customTactics != null) combined.AddRange(customTactics);
            return combined;
        }
    }
    public void InitializeAsDefault()
    {
        InitializeAsBlank();
        entityName = "NewEntity"; baseReplica = "Statue"; colorClass = "y"; imageOverride = "None"; hp = 7; tier = 1;
    }
    public void InitializeAsBlank()
    {
        entityName = null; imageOverride = null; baseReplica = null; colorClass = null;
        hp = 0; h = 0; s = 0; v = 0; tier = 0; hue = 0;
        doc = null; doc2 = null;  speech = null; adj = null;
        appendedDoc = null;

        items = new List<string>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();
        baseAbilityData = new List<string>();
        customSpells = new List<SpellData>();
        customTactics = new List<TacticData>();
        customOnHits = new List<OnHitData>();
        customTriggerHPs = new List<TriggerHPData>();
        customPayloads = new List<CustomPayload>();
        customOrbs = new List<OrbData>();
        diceSides = new DiceSideData[6];

        visuals.Clear();

        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
    }
    private bool TryProcessHeroSpecificMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        string nextVal = tokens[i + 1];

        switch (tokenLower)
        {
            case "replica": baseReplica = nextVal; break;
            case "col": colorClass = nextVal; break;
            case "tier": if (int.TryParse(nextVal, out int t)) tier = t; break;
            case "adj": if (int.TryParse(nextVal, out int a)) adj = a; break;
            case "speech": speech = nextVal; break;
            default: return false;
        }
        i++;
        return true;
    }

    /* // temporarily removed while EntityData tries to take over.
    public override string ExportAsHat()
    {
        StringBuilder heroSb = new StringBuilder();
        // Hats do not use the "replica." prefix, they just state the name directly.
        if (!string.IsNullOrEmpty(baseReplica))
        {
            heroSb.Append($"{FormatName(FormatSpecialImageName(baseReplica))}");
        }
        AppendDiceSides(heroSb);
        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);
        // Append internal items/traits
        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out var wrapperPayloads);
        StringBuilder innerSb = new StringBuilder();
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) innerSb.Append($".i.{FormatName(i)}");
        foreach (var inner in innerPayloads)
        {
            innerSb.Append($".{inner}");
        }
        foreach (var outer in outerPayloads)
        {
            innerSb.Append($".{outer}");
        }
        heroSb.Append(innerSb.ToString());

        // Self-bracket safely rather than forcing the caller to bracket
        //return StaticBranchTracing.SafeBracket(heroSb.ToString());
        // Temp: Always Bracket.
        return $"({heroSb.ToString()})";

    }
    */

    public override void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();
        if (customOrbs == null) customOrbs = new List<OrbData>(); // Safety check

        if (ability is SpellData spell) { if (!customSpells.Any(s => s.entityName == spell.entityName)) customSpells.Add(spell); }
        else if (ability is TacticData tactic) { if (!customTactics.Any(t => t.entityName == tactic.entityName)) customTactics.Add(tactic); }
        else if (ability is OrbData orb) { if (!customOrbs.Any(o => o.entityName == orb.entityName && o.hardcodedAbilityName == orb.hardcodedAbilityName)) customOrbs.Add(orb); }
        else base.AddCustomAbility(ability);
    }
    public override void RemoveCustomAbility(string abilityName)
    {
        base.RemoveCustomAbility(abilityName);
        if (string.IsNullOrEmpty(abilityName)) return;

        if (customSpells != null)
            customSpells.RemoveAll(a => a != null && string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));

        if (customTactics != null)
            customTactics.RemoveAll(a => a != null && string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
    }
    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        string displayName = !string.IsNullOrEmpty(entityName) ? entityName : baseReplica;
        if (!string.IsNullOrEmpty(displayName)) sb.AppendLine($"{indent}Name: {displayName}");
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

        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Abilities ({customAbilityData.Count}):");
            foreach (var cab in customAbilityData)
            {
                string abilityType = cab is SpellData ? "Spell" :
                                     cab is TacticData ? "Tactic" :
                                     cab is OnHitData ? "OnHit" :
                                     cab is TriggerHPData ? "TriggerHP" : "Ability";

                sb.AppendLine($"{indent}  [✓ Unpacked {abilityType}: {cab.entityName ?? "Unnamed"}]");
                cab.DebugAbilityCompact();
            }
        }

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
    public int GetEffectiveTier()
    {
        if (tier >= 0) return tier;

        if (!string.IsNullOrEmpty(baseReplica) &&
            SDColors.heroTiers.TryGetValue(baseReplica, out int inherentTier))
        {
            return inherentTier;
        }

        return 1;
    }
}


