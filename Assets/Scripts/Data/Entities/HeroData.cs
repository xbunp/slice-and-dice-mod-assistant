
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
        p = null; b = null; rect = null; draw = null; thue = null; doc = null; speech = null; adj = null; phue = null;
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
        thue = new Thue();
        phue = new Phue();

        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
    }
    public override void Parse(string data)
    {
        InitializeAsBlank();
        if (string.IsNullOrWhiteSpace(data)) return;
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
            }
        }

        ExtractKnowledge(tokens, _itemPipeline, false);
        ExecuteItemPipeline();
    }

    protected override bool TryProcessSpecificMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        string nextVal = tokens[i + 1];

        switch (tokenLower)
        {
            case "replica": baseReplica = nextVal; break;
            case "col": colorClass = nextVal; break;
            case "tier": if (int.TryParse(nextVal, out int t)) tier = t; break;
            case "x": if (int.TryParse(nextVal, out int a)) adj = a; break;
            case "speech": speech = nextVal; break;
            default: return false;
        }
        i++;
        return true;
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

    /*
    public override string Export()
    {
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
        {
            string formattedReplica = FormatSpecialImageName(baseReplica);
            heroSb.Append($"replica.{FormatName(formattedReplica)}");
            if (!hasImageOverride) AppendColorModifier(heroSb);
        }
        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");

        bool skipColor = false;
        string activeVisual = hasImageOverride ? imageOverride : baseReplica;
        if (!string.IsNullOrEmpty(activeVisual) && Enum.TryParse(activeVisual, true, out HeroType parsedHero))
        {
            if (SDColors.HeroColorMap.TryGetValue(parsedHero, out HeroColorOption defaultColor))
            {
                if (EntityUIHelpers.ReverseLookupColor(colorClass) == defaultColor)
                {
                    skipColor = true;
                }
            }
        }

        if (!skipColor && !string.IsNullOrEmpty(colorClass))
        {
            heroSb.Append($".col.{colorClass}");
        }

        if (hp > 0) heroSb.Append($".hp.{hp}");
        if (tier >= 0) heroSb.Append($".tier.{tier}");
        if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");

        AppendDiceSides(heroSb);
        if (!string.IsNullOrEmpty(speech)) heroSb.Append($".speech.{speech}");

        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        // 1. Sort all custom payloads based on Entity-level rules
        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out var wrapperPayloads);

        // 2. Append Inner items (Items, Traits, Curses, inner payloads) BEFORE the image override
        StringBuilder innerSb = new StringBuilder();
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) innerSb.Append($".i.t.{FormatName(t)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) innerSb.Append($".i.{FormatName(i)}");
        if (blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) innerSb.Append($".gift.{FormatName(bl)}");
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) innerSb.Append($".i.t.jinx.{FormatName(c)}");

        foreach (var inner in innerPayloads)
        {
            innerSb.Append($".{inner}");
        }

        heroSb.Append(innerSb.ToString());

        // 3. Append the Image Override (resets visual rendering to the override payload)
        if (hasImageOverride)
        {
            string formattedImg = FormatSpecialImageName(imageOverride);
            heroSb.Append($".img.{FormatName(formattedImg)}");
            AppendColorModifier(heroSb);
        }

        heroSb.Append(")");

        string baseHeroString = heroSb.ToString();
        string fullContentString = baseHeroString;

        // 4. Append Outer Abilities and Outer Items OUTSIDE the entity string
        StringBuilder outerSb = new StringBuilder();

        // Base ability data (spells/tactics) relocated to outer shell
        if (baseAbilityData != null)
            foreach (var ab in baseAbilityData)
                if (!string.IsNullOrEmpty(ab))
                    outerSb.Append($".i.learn.{FormatName(ab)}");

        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            foreach (var cab in customAbilityData)
            {
                if (cab != null)
                {
                    if (cab is TriggerHPData) outerSb.Append($".triggerhpdata.({cab.Export()})");
                    else if (cab is OnHitData) outerSb.Append($".onhitdata.({cab.Export()})");
                    else if (cab is OrbData orb) outerSb.Append($".{orb.ExportAsTrait(useITPrefix: true)}");
                    else outerSb.Append($".abilitydata.({cab.Export()})");
                }
            }
        }

        foreach (var outer in outerPayloads)
        {
            outerSb.Append($".{outer}");
        }

        if (outerSb.Length > 0)
        {
            // Wrap the base hero and the abilities up: ((hero).abilities)
            fullContentString = $"({baseHeroString}{outerSb.ToString()})";
        }

        // 5. Apply Wrappers (if an item explicitly demands wrapping the entire hero)
        foreach (var wrapper in wrapperPayloads)
        {
            if (wrapper.Contains("{0}"))
                fullContentString = string.Format(wrapper, fullContentString);
            else
                fullContentString = $"({fullContentString}.{wrapper})"; // Failsafe
        }

        bool hasDoc = !string.IsNullOrEmpty(doc);
        bool hasAppendedDoc = !string.IsNullOrEmpty(appendedDoc);
        if (hasDoc || hasAppendedDoc)
        {
            StringBuilder tailSb = new StringBuilder();

            if (hasDoc) tailSb.Append($".doc.{doc}");
            if (hasAppendedDoc) tailSb.Append($".i.self.Wolf.doc.{appendedDoc}.spirit");

            return $"({fullContentString}{tailSb.ToString()})";
        }

        return fullContentString;
    }
    */

    public string ExportAsHat()
    {
        StringBuilder heroSb = new StringBuilder();

        // Hats do not use the "replica." prefix, they just state the name directly.
        if (!string.IsNullOrEmpty(baseReplica))
        {
            heroSb.Append($"{FormatName(FormatSpecialImageName(baseReplica))}");
            AppendColorModifier(heroSb);
        }

        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");

        // Hats shouldn't force default tiers/hp to 0. We only export them if they are explicitly > 0.
        //if (hp > 0) heroSb.Append($".hp.{hp}");
        //if (tier > 0) heroSb.Append($".tier.{tier}");

        //if (!string.IsNullOrEmpty(colorClass)) heroSb.Append($".col.{colorClass}");
        //if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        //if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        //if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        //if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        //if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");

        AppendDiceSides(heroSb);

        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        // Append internal items/traits
        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out var wrapperPayloads);

        StringBuilder innerSb = new StringBuilder();
        //if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) innerSb.Append($".i.t.{FormatName(t)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) innerSb.Append($".i.{FormatName(i)}");
        //if (blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) innerSb.Append($".gift.{FormatName(bl)}");
        //if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) innerSb.Append($".i.t.jinx.{FormatName(c)}");

        foreach (var inner in innerPayloads)
        {
            innerSb.Append($".{inner}");
        }

        heroSb.Append(innerSb.ToString());

        /*
        if (!string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica)
        {
            heroSb.Append($".img.{FormatName(FormatSpecialImageName(imageOverride))}");
            AppendColorModifier(heroSb);
        }
        */

        // Return without outer parentheses, as ItemMechanic.Export() handles the wrapping
        return heroSb.ToString();
    }
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


/*
public override void Parse(string data)
{
    InitializeAsBlank(); // Ensure we start completely clean
    if (string.IsNullOrWhiteSpace(data)) return;

    List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
    string heroCore = StaticBranchTracing.StripOuterParens(chunks[0]);

    // FIX: Do NOT split by '#' at the top level. Process as a single chain.
    List<string> tokens = StaticBranchTracing.TopLevelSplit(heroCore, '.');

    if (tokens.Count > 0)
    {
        string firstLower = tokens[0].ToLower();
        if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
        {
            baseReplica = tokens[0];
        }
    }

    ExtractKnowledge(tokens);
}
*/
/*
public void ResolveModifiers()
{
    var itemPayloads = customPayloads.Where(p => p.Type == PayloadType.Item).ToList();

    // 1. Resolve Coupling (Hat + Facade)
    for (int i = 0; i < itemPayloads.Count - 1; i++)
    {
        var currentItem = itemPayloads[i].Data as ItemData;
        var nextItem = itemPayloads[i + 1].Data as ItemData;

        if (IsHat(currentItem) && IsFacade(nextItem))
        {
            // Bind them together so they cannot be separated during sort/export
            MergeItems(currentItem, nextItem);
            customPayloads.Remove(itemPayloads[i + 1]);
            itemPayloads.RemoveAt(i + 1);
        }
    }

    // 2. Sort Mechanics
    // Assign a priority integer to each ItemData based on its mechanics 
    // (0 for Permissive, 50 for standard, 99 for Stasis, 100 for Facades).
    var sortedPayloads = itemPayloads.OrderBy(p => DeterminePriority(p.Data as ItemData)).ToList();

    // 3. Apply to Hero State
    foreach (var payload in sortedPayloads)
    {
        ApplyItemMechanicsToHero(payload.Data as ItemData);
    }
}
*/
/*
private void ApplyItemMechanicsToHero(ItemData item)
{
    foreach (var mech in item.Mechanics)
    {
        if (mech.Prefix == "t")
            traits.Add(mech.PayloadString);
        else if (mech.Prefix == "learn")
            baseAbilityData.Add(mech.PayloadString);
        else if (mech.Prefix == "k" || mech.Prefix == "facade")
            ApplyMechanicToDiceSides(mech); // Your existing face iteration logic, but object-driven
    }
}
*/
/*
private void ApplyDiceModifiers(List<int> targetFaces, string modPayload)
{
    string[] chunks = modPayload.Split('#');
    foreach (int faceIdx in targetFaces)
    {
        if (faceIdx < 0 || faceIdx >= diceSides.Length) continue;
        if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

        foreach (string chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            // FIX: Detect standalone future keyword IDs (like ritemx.dae9 or unpack.ritemx.644f)
            string trimmedChunk = chunk.Trim();
            string lowerChunk = trimmedChunk.ToLower();
            if (lowerChunk == "ritemx.dae9" || lowerChunk == "unpack.ritemx.644f" || lowerChunk == "k.future")
            {
                if (!diceSides[faceIdx].keywords.Contains("future"))
                {
                    diceSides[faceIdx].keywords.Add("future");
                }
                continue;
            }

            string[] parts = chunk.Split(new char[] { '.' }, 2);
            if (parts.Length < 2) continue;

            string type = parts[0].ToLower();
            string value = parts[1];

            if (type == "k")
            {
                string lowercaseKeyword = value.Trim().ToLower();

                // FIX: Normalization fallback in case of mixed formats
                if (lowercaseKeyword == "ritemx.dae9" || lowercaseKeyword == "unpack.ritemx.644f" || lowercaseKeyword == "future")
                {
                    lowercaseKeyword = "future";
                }

                if (!diceSides[faceIdx].keywords.Contains(lowercaseKeyword))
                {
                    diceSides[faceIdx].keywords.Add(lowercaseKeyword);
                }
            }
            else if (type == "facade")
            {
                string[] facadeParts = value.Split(':');
                diceSides[faceIdx].facadeID = facadeParts[0];

                if (facadeParts.Length > 1)
                {
                    var colorParts = facadeParts.Skip(1)
                                                .Select(p => string.IsNullOrWhiteSpace(p) ? "0" : p.Trim())
                                                .ToList();

                    if (colorParts.Count == 0 || colorParts.All(p => p == "0"))
                    {
                        diceSides[faceIdx].facadeColor = null;
                    }
                    else
                    {
                        while (colorParts.Count < 3) colorParts.Add("0");
                        diceSides[faceIdx].facadeColor = $"{colorParts[0]}:{colorParts[1]}:{colorParts[2]}";
                    }
                }
                else
                {
                    diceSides[faceIdx].facadeColor = null;
                }
            }
        }
    }
}
*/
/*
private void ResolveItemPipeline(List<ItemData> pipeline)
{
    // 1. Resolve Coupling (Hat + Facade)
    for (int i = 0; i < pipeline.Count - 1; i++)
    {
        var currentItem = pipeline[i];
        var nextItem = pipeline[i + 1];

        bool hasHat = currentItem.Mechanics.Any(m => m.Prefix == "hat");
        bool hasFacade = nextItem.Mechanics.Any(m => m.Prefix == "facade");

        if (hasHat && hasFacade)
        {
            // Merge nextItem's mechanics directly into currentItem so they sort and export together
            currentItem.Mechanics.AddRange(nextItem.Mechanics);
            pipeline.RemoveAt(i + 1);
            i--; // Step back to evaluate if the next item is ALSO chained
        }
    }

    // 2. Stable Sort by Priority
    // Priority: Permissive (0) -> Standard (50) -> Stasis (99) -> Facades (100)
    var sortedPipeline = pipeline.OrderBy(item => GetItemPriority(item)).ToList();

    // 3. Hydrate Hero State
    foreach (var item in sortedPipeline)
    {
        HydrateHeroFromItem(item);
    }
}
private int GetItemPriority(ItemData item)
{
    int priority = 50;
    foreach (var mech in item.Mechanics)
    {
        string payloadLower = mech.PayloadString?.ToLower() ?? "";
        if (mech.Prefix == "k" && payloadLower == "permissive") return 0; // Absolute First
        if (mech.Prefix == "k" && payloadLower == "stasis") priority = 99;
        else if (mech.Prefix == "facade") priority = 100;
    }
    return priority;
}
private void HydrateHeroFromItem(ItemData item)
{
    // Check if this item contains ANY mechanics that HeroData cannot natively map
    bool canMapNatively = true;
    foreach (var mech in item.Mechanics)
    {
        string pfx = mech.Prefix?.ToLower() ?? "";
        if (pfx != "t" && pfx != "gift" && pfx != "learn" && pfx != "abilitydata" && pfx != "k" && pfx != "facade" && pfx != "sticker" && pfx != "")
        {
            canMapNatively = false;
            break;
        }
    }

    // If the item possesses complex mechanics (Hats, Modifiers, Enchants) we black-box it 
    // to prevent double-exporting properties the UI can't track.
    if (!canMapNatively)
    {
        customPayloads.Add(new CustomPayload { Prefix = "i", Data = item, Type = PayloadType.Item });
        return;
    }

    if (item.LearnedAbilities != null && item.LearnedAbilities.Count > 0)
    {
        baseAbilityData.AddRange(item.LearnedAbilities);
    }

    foreach (var mech in item.Mechanics)
    {
        if (mech.Prefix == "t")
        {
            if (mech.PayloadString != null && mech.PayloadString.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
                curses.Add(mech.PayloadString.Substring(5));
            else
                traits.Add(mech.PayloadString);
        }
        else if (mech.Prefix == "gift")
        {
            blessings.Add(mech.PayloadString);
        }
        else if (mech.Prefix == "learn" || mech.Prefix == "abilitydata")
        {
            baseAbilityData.Add(mech.PayloadString);
        }
        else if (mech.Prefix == "k" || mech.Prefix == "facade" || mech.Prefix == "sticker")
        {
            if (mech.Targets != null && mech.Targets.Count > 0)
            {
                List<int> targetFaces = new List<int>();
                foreach (string target in mech.Targets)
                {
                    targetFaces.AddRange(DiceTargetHelper.GetIndicesForTarget(target));
                }
                ApplyMechanicToDiceSides(targetFaces.Distinct().ToList(), mech);
            }
        }
    }

    if (item.Mechanics.Count == 0 && !string.IsNullOrEmpty(item.entityName))
    {
        items.Add(item.entityName);
    }
}
private void ApplyMechanicToDiceSides(List<int> targetFaces, ItemMechanic mech)
{
    foreach (int faceIdx in targetFaces)
    {
        if (faceIdx < 0 || faceIdx >= diceSides.Length) continue;
        if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

        string lowerPrefix = mech.Prefix?.ToLower() ?? "";

        if (lowerPrefix == "k")
        {
            string keyword = mech.PayloadString?.Trim().ToLower() ?? "";
            if (keyword == "ritemx.dae9" || keyword == "unpack.ritemx.644f") keyword = "future";
            if (!string.IsNullOrEmpty(keyword) && !diceSides[faceIdx].keywords.Contains(keyword))
            {
                diceSides[faceIdx].keywords.Add(keyword);
            }
        }
        else if (lowerPrefix == "facade")
        {
            string[] facadeParts = (mech.PayloadString ?? "").Split(':');
            diceSides[faceIdx].facadeID = facadeParts[0];
            if (facadeParts.Length > 1)
            {
                var colorParts = facadeParts.Skip(1).Select(p => string.IsNullOrWhiteSpace(p) ? "0" : p.Trim()).ToList();
                if (colorParts.Count == 0 || colorParts.All(p => p == "0")) diceSides[faceIdx].facadeColor = null;
                else
                {
                    while (colorParts.Count < 3) colorParts.Add("0");
                    diceSides[faceIdx].facadeColor = $"{colorParts[0]}:{colorParts[1]}:{colorParts[2]}";
                }
            }
        }
        else if (lowerPrefix == "sticker")
        {
            diceSides[faceIdx].sticker = mech.PayloadString;
        }
    }
}
public override void Parse(string data)
{
    InitializeAsBlank();
    if (string.IsNullOrWhiteSpace(data)) return;
    List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
    string heroCore = StaticBranchTracing.StripOuterParens(chunks[0]);
    List<string> tokens = StaticBranchTracing.TopLevelSplit(heroCore, '.');

    if (tokens.Count > 0)
    {
        string firstLower = tokens[0].ToLower();
        if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
        {
            baseReplica = tokens[0];
        }
    }

    // INTERMEDIATE PIPELINE: Holds parsed ItemData objects before context resolution
    List<ItemData> itemPipeline = new List<ItemData>();

    ExtractKnowledge(tokens, itemPipeline);
    ResolveItemPipeline(itemPipeline);
}
*/

// ApplyMechanicToDiceSides
/*
private void ApplyMechanicToDiceSides(List<int> targetFaces, ItemMechanic mech)
{
    foreach (int faceIdx in targetFaces)
    {
        if (faceIdx < 0 || faceIdx >= 6) continue;
        if (diceSides == null) InitializeDiceFaces();
        if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

        string lowerPrefix = mech.Prefix?.ToLower() ?? "";
        string payload = mech.PayloadString?.Trim() ?? "";

        // Handle direct keywords and implicit Tog items
        if (lowerPrefix == "k" || lowerPrefix == "")
        {
            string keyword = payload.ToLower();
            if (keyword == "ritemx.dae9" || keyword == "unpack.ritemx.644f") keyword = "future";

            if (!string.IsNullOrEmpty(keyword) && !diceSides[faceIdx].keywords.Contains(keyword))
            {
                diceSides[faceIdx].keywords.Add(keyword);
            }

            // Add any keywords chained inside the item syntax via '#'
            foreach (string chainKw in mech.ChainedKeywords)
            {
                string cleanKw = chainKw.Trim().ToLower();
                if (cleanKw.StartsWith("k.")) cleanKw = cleanKw.Substring(2);
                if (!diceSides[faceIdx].keywords.Contains(cleanKw))
                    diceSides[faceIdx].keywords.Add(cleanKw);
            }
        }
        else if (lowerPrefix == "facade")
        {
            string[] facadeParts = payload.Split(':');
            diceSides[faceIdx].facadeID = facadeParts[0];
            if (facadeParts.Length > 1)
            {
                var colorParts = facadeParts.Skip(1).Select(p => string.IsNullOrWhiteSpace(p) ? "0" : p.Trim()).ToList();
                if (colorParts.Count == 0 || colorParts.All(p => p == "0")) diceSides[faceIdx].facadeColor = null;
                else
                {
                    while (colorParts.Count < 3) colorParts.Add("0");
                    diceSides[faceIdx].facadeColor = $"{colorParts[0]}:{colorParts[1]}:{colorParts[2]}";
                }
            }
        }
        else if (lowerPrefix == "sticker")
        {
            diceSides[faceIdx].sticker = payload;
        }
    }
}
*/

/*
protected override int GetEndOfBlockIndex(List<string> tokens, int startIndex)
{
    int endIndex = startIndex;
    while (endIndex < tokens.Count)
    {
        string peek = tokens[endIndex].ToLower();

        if (peek == "i" || peek == "t" || peek == "gift" || peek == "learn" ||
            peek == "abilitydata" || peek == "triggerhpdata" || peek == "onhitdata" ||
            peek == "orb" || peek == "sd")
        {
            break;
        }

        if (HeroDomainRules.MetadataKeys.Contains(peek))
        {
            if (peek == "col" && endIndex + 1 < tokens.Count)
            {
                string nextToken = tokens[endIndex + 1].ToLower();
                if (SDColors.GetOptionFromColorCode(nextToken) != HeroColorOption.White || nextToken == "w")
                    break;
            }
            else if ((peek == "hp" || peek == "tier") && endIndex + 1 < tokens.Count)
            {
                if (int.TryParse(tokens[endIndex + 1], out _)) break;
            }
            else if (peek == "n" || peek == "img" || peek == "doc" || peek == "speech" ||
                     peek == "hsv" || peek == "hue" || peek == "thue" || peek == "p" ||
                     peek == "b" || peek == "draw" || peek == "rect")
            {
                break;
            }
        }
        endIndex++;
    }
    return endIndex;
}
*/