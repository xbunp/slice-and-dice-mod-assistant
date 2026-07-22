using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class EntityDomainRules
{
    // The keys shared by almost ALL entities
    public static readonly HashSet<string> CommonMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "n", "img", "doc", "hp", "hsv", "hsl", "hue",
        "p", "b", "rect", "draw", "thue", "sd"
    };

    // Shared collection routing keys
    public static readonly HashSet<string> CommonCollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "learn", "abilitydata", "triggerhpdata", "onhitdata", "orb" // Added "orb"
    };


    public static int GetCollectionBlockLength(List<string> tokens, int startIndex)
    {
        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();

            if (peek.StartsWith("(") && peek.EndsWith(")"))
            {
                endIndex++; continue;
            }
            if (ModifierDomainRules.IsModifierStartToken(peek))
            {
                endIndex += ModifierDomainRules.GetModifierBlockLength(tokens, endIndex); continue;
            }
            if (AbilityDomainRules.IsAbilityStartSequence(tokens, endIndex))
            {
                endIndex += AbilityDomainRules.GetAbilityBlockLength(tokens, endIndex); continue;
            }

            // Standard single-token payloads (like t.Beefy)
            if (endIndex == startIndex)
            {
                endIndex++; continue;
            }

            break; // Stop parsing collection string natively
        }
        return endIndex - startIndex;
    }
}

[System.Serializable]
public abstract class EntityData : SDData, IPayloadContainer
{
    [Header("Core Shared Info")]
    public int hp = 0;
    public string appendedDoc = "";

    [Header("Shared Extended Modifiers")]
    public List<string> items = new List<string>();

    public List<string> traits = new List<string>();
    public List<string> blessings = new List<string>();
    public List<string> curses = new List<string>();
    public List<string> baseAbilityData = new List<string>();

    // ============================================================================================
    // CRITICAL ARCHITECTURAL CONSTRAINT & AI DEVELOPER GUIDELINE - DO NOT REMOVE
    // ============================================================================================
    // DO NOT SILENTLY ASSUME OR ALTER THE DICE FACE INDEX LAYOUT! 
    // THE CODES, ALIASES, AND BITMASKS ARE RIGIDLY TIED TO THE GAME ENGINE AND MUST NEVER BE ASSUMED.
    //
    // THE INDICES OF THE 6 DICE SIDES ARE DEFINED STRICTLY AS:
    //   Index 0: Left
    //   Index 1: Middle (mid)
    //   Index 2: Top (top)
    //   Index 3: Bottom (bot)
    //   Index 4: Right (right)
    //   Index 5: Rightmost (rightmost)
    //
    // ANY TRANSLATION BETWEEN SIDE NAMES/ALIASES AND INDICES *MUST* USE 'DiceTargetHelper' METHODS:
    //   - DiceTargetHelper.GetIndicesForTarget(target)
    //   - DiceTargetHelper.GetBestAliasCombination(mask)
    //
    // DO NOT hardcode direct translations (e.g. assuming index 4 is 'mid' or index 1 is 'right').
    // Doing so violates engine rules and corrupts hero/item properties on export.
    // ============================================================================================

    public DiceSideData[] diceSides = new DiceSideData[6];

    // Change from [SerializeField] public List<SpellData> customSpells;
    [System.NonSerialized] // Tells Unity's serializer to ignore this field
    [JsonProperty]         // Tells Newtonsoft to keep serializing this field
    [SerializeField] public List<OnHitData> customOnHits;

    [System.NonSerialized]
    [JsonProperty]
    [SerializeField] public List<TriggerHPData> customTriggerHPs;

    [System.NonSerialized]
    [JsonProperty]
    [SerializeField] public List<OrbData> customOrbs = new List<OrbData>();

    [System.NonSerialized]
    protected List<ItemData> _itemPipeline = new List<ItemData>();

    //ADD ORB SUPPORT.

    // Interface mappings
    public List<string> BaseItems => items;
    public List<string> Traits => traits;
    public List<string> Curses => curses;
    public List<string> Blessings => blessings;
    public List<string> BaseAbilities => baseAbilityData;
    public List<CustomPayload> CustomPayloads => customPayloads;
    public virtual IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>();
            if (customOnHits != null) combined.AddRange(customOnHits);
            if (customTriggerHPs != null) combined.AddRange(customTriggerHPs);
            if (customOrbs != null) combined.AddRange(customOrbs); // Added
            return combined;
        }
    }
    public virtual void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customOnHits == null) customOnHits = new List<OnHitData>();
        if (customTriggerHPs == null) customTriggerHPs = new List<TriggerHPData>();
        if (customOrbs == null) customOrbs = new List<OrbData>();

        if (ability is OrbData orb)
        {
            if (!customOrbs.Any(o => o.entityName == orb.entityName && o.hardcodedAbilityName == orb.hardcodedAbilityName))
                customOrbs.Add(orb);
        }
        else if (ability is OnHitData onHit)
        {
            if (!customOnHits.Any(o => o.entityName == onHit.entityName)) customOnHits.Add(onHit);
        }
        else if (ability is TriggerHPData trig)
        {
            if (!customTriggerHPs.Any(t => t.entityName == trig.entityName)) customTriggerHPs.Add(trig);
        }
    }
    public virtual void RemoveCustomAbility(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return;

        if (customOnHits != null)
            customOnHits.RemoveAll(a => a != null && string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));

        if (customTriggerHPs != null)
            customTriggerHPs.RemoveAll(a => a != null && string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));

        if (customOrbs != null)
            customOrbs.RemoveAll(a => a != null && (string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase) || string.Equals(a.hardcodedAbilityName, abilityName, StringComparison.OrdinalIgnoreCase)));
    }

    // ====================================================================
    // UNIFIED EXPORT PIPELINE (DRY Implementation)
    // ====================================================================

    public override string Export()
    {
        StringBuilder sb = new StringBuilder();
        bool isHero = this is HeroData;
        HeroData hero = this as HeroData;
        MonsterData monster = this as MonsterData;

        string baseId = isHero ? hero.baseReplica : monster.baseMonster;
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                !string.Equals(imageOverride, "None", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(imageOverride, baseId, StringComparison.OrdinalIgnoreCase);

        // --- 1. CORE WRAPPER & BASE ID ---
        sb.Append("("); // CHANGED: ALWAYS wrap the core in parentheses (was isHero-only)

        if (!string.IsNullOrEmpty(baseId))
        {
            if (isHero)
            {
                string formattedReplica = FormatSpecialImageName(baseId);
                sb.Append($"replica.{FormatName(formattedReplica)}");
            }
            else
            {
                // CHANGED: Run the baseId through FormatSpecialImageName to map 
                // shorthand keys (like "rm_n") back to their in-game equivalents (like "rmon.0")
                string formattedBase = FormatSpecialImageName(baseId);
                sb.Append(FormatName(formattedBase));
            }
            if (!hasImageOverride) AppendColorModifier(sb);
        }

        // --- 2. ENTITY NAME ---
        if (!string.IsNullOrEmpty(entityName) && (isHero || !string.Equals(entityName, baseId, StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append($".n.{FormatName(entityName)}");
        }

        // --- 3. METADATA (Hero Color Class) ---
        if (isHero)
        {
            bool skipColor = false;
            string activeVisual = hasImageOverride ? imageOverride : baseId;
            if (!string.IsNullOrEmpty(activeVisual) && Enum.TryParse(activeVisual, true, out HeroType parsedHero))
            {
                if (SDColors.HeroColorMap.TryGetValue(parsedHero, out HeroColorOption defaultColor))
                {
                    if (EntityUIHelpers.ReverseLookupColor(hero.colorClass) == defaultColor) skipColor = true;
                }
            }
            if (!skipColor && !string.IsNullOrEmpty(hero.colorClass)) sb.Append($".col.{hero.colorClass}");
        }

        // --- 4. SHARED METADATA ---
        if (hp > 0) sb.Append($".hp.{hp}");
        if (isHero && hero.tier >= 0) sb.Append($".tier.{hero.tier}");
        if (isHero && hero.adj.HasValue) sb.Append($".adj.{hero.adj.Value}");

        // --- 5. DICE SIDES & FACADES ---
        AppendDiceSides(sb);

        if (isHero && !string.IsNullOrEmpty(hero.speech)) sb.Append($".speech.{hero.speech}");

        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            if (!isHero) faceModifiers = Regex.Replace(faceModifiers, @"(\.facade\.[^.:\s]+)(?=\.|$)", "$1:0");
            sb.Append(faceModifiers);
        }

        // --- 7. INNER PAYLOADS ---
        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out var wrapperPayloads);

        StringBuilder innerSb = new StringBuilder();

        string traitPrefix = isHero ? ".i.t." : ".t.";
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) innerSb.Append($"{traitPrefix}{FormatName(t)}");
        if (!isHero && monster.customOrbs != null) foreach (var orb in monster.customOrbs) if (orb != null) innerSb.Append($".{orb.ExportAsTrait(useITPrefix: false)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) innerSb.Append($".i.{FormatName(i)}");
        if (isHero && blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) innerSb.Append($".gift.{FormatName(bl)}");

        string jinxPrefix = isHero ? ".i.t.jinx." : ".t.jinx.";
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) innerSb.Append($"{jinxPrefix}{FormatName(c)}");

        foreach (var inner in innerPayloads) innerSb.Append($".{inner}");

        sb.Append(innerSb.ToString());

        // --- 6. IMAGE OVERRIDE ---
        if (hasImageOverride)
        {
            string formattedImg = FormatSpecialImageName(imageOverride);
            sb.Append($".img.{FormatName(formattedImg)}");
            AppendColorModifier(sb);
        }

        sb.Append(")"); // CHANGED: ALWAYS close core parenthesis (was isHero-only)

        string fullContentString = sb.ToString();

        // --- 8. OUTER PAYLOADS ---
        StringBuilder outerSb = new StringBuilder();
        if (isHero && hero.baseAbilityData != null)
            foreach (var ab in hero.baseAbilityData)
                if (!string.IsNullOrEmpty(ab))
                    outerSb.Append($".i.learn.{FormatName(ab)}");

        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            foreach (var cab in customAbilityData)
            {
                //TODO: might not want to bracket them here if they already bracket themselves? or just make sure its bracket-safe. 
                if (cab == null) continue;
                if (cab is TriggerHPData) outerSb.Append($".triggerhpdata.({cab.Export()})");
                else if (cab is OnHitData) outerSb.Append($".i.onhitdata.({cab.Export()})");
                else if (cab is OrbData orb) outerSb.Append($".{orb.ExportAsTrait(useITPrefix: true)}");
                else outerSb.Append($".abilitydata.({cab.Export()})");
            }
        }

        foreach (var outer in outerPayloads) outerSb.Append($".{outer}");

        if (outerSb.Length > 0)
        {
            // CHANGED: ALWAYS wrap outer payloads in parenthesis (was isHero-only ternary)
            fullContentString = $"({fullContentString}{outerSb.ToString()})";
        }

        // --- 9. WRAPPERS ---
        foreach (var wrapper in wrapperPayloads)
        {
            if (wrapper.Contains("{0}")) fullContentString = string.Format(wrapper, fullContentString);
            else fullContentString = $"({fullContentString}.{wrapper})";
        }

        // --- 10. TAIL MODIFIERS ---
        StringBuilder tailSb = new StringBuilder();
        if (!string.IsNullOrEmpty(doc)) tailSb.Append($".doc.{doc}");
        if (isHero && !string.IsNullOrEmpty(hero.appendedDoc)) tailSb.Append($".i.self.Wolf.doc.{hero.appendedDoc}.spirit");
        if (!isHero && !string.IsNullOrEmpty(monster.bal)) tailSb.Append($".bal.{FormatName(monster.bal)}");

        // CHANGED: ALWAYS wrap tail modifiers in parenthesis if they exist (was isHero-only ternary)
        if (tailSb.Length > 0) return $"({fullContentString}{tailSb.ToString()})";
        return fullContentString;
    }

    protected void ProcessTraitPayload(string tPayload)
    {
        if (string.IsNullOrWhiteSpace(tPayload)) return;

        List<string> chains = StaticBranchTracing.TopLevelSplit(tPayload, '#');
        foreach (string chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            string trimmed = chain.Trim();

            if (trimmed.StartsWith("orb.", StringComparison.OrdinalIgnoreCase))
            {
                OrbData orb = new OrbData();
                orb.Parse(trimmed);
                AddCustomAbility(orb);
            }
            else if (trimmed.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
            {
                curses.Add(trimmed.Substring(5));
            }
            else if (trimmed.Contains("("))
            {
                ModifierData nestedMod = new ModifierData();
                nestedMod.Parse(trimmed);
                if (customPayloads == null) customPayloads = new List<CustomPayload>();
                customPayloads.Add(new CustomPayload { Prefix = "t", Data = nestedMod });
            }
            else
            {
                traits.Add(trimmed);
            }
        }
    }
    protected bool TryProcessEntityMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "hp")
        {
            if (i + 1 < tokens.Count && int.TryParse(tokens[i + 1], out int hpVal))
            {
                hp = hpVal;
                i++; // Consume the value token
            }
            return true;
        }
        return false;
    }
    protected bool TryProcessDiceSides(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower != "sd" || i + 1 >= tokens.Count) return false;

        // Split faces by colon (e.g., "187:76-0")
        string[] faces = tokens[++i].Split(':');

        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
        {
            if (faces[f] == "0" || faces[f] == "0-0") continue;

            // Ensure the DiceSideData instance exists to prevent NullReferenceException
            if (diceSides[f] == null)
            {
                diceSides[f] = new DiceSideData
                {
                    effectID = 0,
                    pips = 0,
                    facadeID = null,
                    keywords = new List<string>()
                };
            }

            string[] faceParts = faces[f].Split('-');
            if (faceParts.Length > 0)
            {
                // Parse Effect ID (always present if we reached here)
                int.TryParse(faceParts[0], out diceSides[f].effectID);

                // Parse Pips if specified (e.g., "76-2"), otherwise default to 0 (e.g., "187")
                if (faceParts.Length > 1)
                {
                    int.TryParse(faceParts[1], out diceSides[f].pips);
                }
                else
                {
                    diceSides[f].pips = 0;
                }
            }
        }
        return true;
    }

    // Unifies lookahead trait parsing (supports both strings and nested custom modifiers)
    // Notice we dropped the Hashset parameter entirely!
    protected void ProcessTraitToken(List<string> tokens, ref int i)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        int length = EntityDomainRules.GetCollectionBlockLength(tokens, startIndex);
        if (length > 0)
        {
            string tPayload = string.Join(".", tokens.GetRange(startIndex, length));
            i += length - 1; // Evaluates last token, allows standard loop incrementing
            ProcessTraitPayload(tPayload);
        }
    }
    protected bool TryProcessOrbData(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "orb")
        {
            if (i + 1 >= tokens.Count) return true;

            int endIndex = i + 1;
            if (OrbData.ValidBaseOrbs.Contains(tokens[endIndex]))
            {
                OrbData orb = new OrbData();
                orb.Parse($"orb.{tokens[endIndex]}");
                AddCustomAbility(orb);
                i = endIndex;
                return true;
            }

            int j = i + 1;
            while (j < tokens.Count)
            {
                if (tokens[j].StartsWith("("))
                {
                    endIndex = j;
                    break;
                }
                if (EntityDomainRules.CommonMetadataKeys.Contains(tokens[j]) || EntityDomainRules.CommonCollectionKeys.Contains(tokens[j]))
                {
                    break;
                }
                j++;
            }

            string payload = string.Join(".", tokens.GetRange(i, endIndex - i + 1));
            OrbData customOrb = new OrbData();
            customOrb.Parse(payload);
            AddCustomAbility(customOrb);
            i = endIndex;
            return true;
        }
        return false;
    }
    protected bool TryProcessTriggerData(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "triggerhpdata")
        {
            if (i + 1 < tokens.Count)
            {
                string payload = tokens[++i];
                TriggerHPData thp = new TriggerHPData();
                thp.Parse(StaticBranchTracing.StripOuterParens(payload));
                AddCustomAbility(thp);
            }
            return true;
        }
        if (tokenLower == "onhitdata")
        {
            if (i + 1 < tokens.Count)
            {
                string payload = tokens[++i];
                OnHitData ohd = new OnHitData();
                ohd.Parse(StaticBranchTracing.StripOuterParens(payload));
                AddCustomAbility(ohd);
            }
            return true;
        }
        return false;
    }
    public void InitializeDiceFaces()
    {
        // Ensure the array itself exists
        if (diceSides == null || diceSides.Length != 6)
        {
            diceSides = new DiceSideData[6];
        }

        // ONLY instantiate slots that are completely null, preserving existing data
        for (int i = 0; i < diceSides.Length; i++)
        {
            if (diceSides[i] == null)
            {
                diceSides[i] = new DiceSideData();
                // Safety net: ensure keywords list is never null
                if (diceSides[i].keywords == null) diceSides[i].keywords = new List<string>();
            }
        }
    }
    protected void AppendDiceSides(StringBuilder sb)
    {
        // Find the last modified side so we can truncate trailing zeroes
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            // CHANGED: Also check if pips != 0 so we don't drop pip-only modifications
            if (diceSides[i] != null && (diceSides[i].effectID != 0 || diceSides[i].pips != 0))
            {
                lastActiveIndex = i;
            }
        }

        // If no custom sides are defined, omit the .sd block entirely
        if (lastActiveIndex == -1) return;

        sb.Append(".sd.");
        for (int i = 0; i <= lastActiveIndex; i++)
        {
            var side = diceSides[i];
            if (side == null || (side.effectID == 0 && side.pips == 0))
            {
                sb.Append("0");
            }
            else
            {
                // Simplify: if pips are 0, omit the "-0" suffix
                if (side.pips == 0)
                {
                    sb.Append(side.effectID);
                }
                else
                {
                    sb.Append($"{side.effectID}-{side.pips}");
                }
            }

            // Only append separator if there are more customized sides remaining
            if (i < lastActiveIndex) sb.Append(":");
        }
    }
    protected void ProcessCustomPayloadsForExport(
    out List<string> innerPayloads,
    out List<string> outerPayloads,
    out List<string> wrapperPayloads)
    {
        innerPayloads = new List<string>();
        outerPayloads = new List<string>();
        wrapperPayloads = new List<string>();

        if (customPayloads == null) return;

        foreach (var payload in customPayloads)
        {
            if (payload.Type == PayloadType.Item && payload.Data is ItemData itemData)
            {
                var result = CustomItemContextHelper.EvaluateItem(itemData);
                if (!string.IsNullOrEmpty(result.FormattedString))
                {
                    if (result.Zone == PayloadInjectionZone.InnerEntity) innerPayloads.Add(result.FormattedString);
                    else if (result.Zone == PayloadInjectionZone.OuterEntity) outerPayloads.Add(result.FormattedString);
                    else if (result.Zone == PayloadInjectionZone.EntityWrapper) wrapperPayloads.Add(result.FormattedString);
                }
            }
            else
            {
                // Non-item custom payloads default to InnerEntity
                string exported = payload.Export();
                if (!string.IsNullOrEmpty(exported)) innerPayloads.Add(exported);
            }
        }
    }

    // Derived classes MUST define how they identify the end of a block
    //protected abstract int GetEndOfBlockIndex(List<string> tokens, int startIndex);
    protected void ExecuteItemPipeline()
    {
        if (_itemPipeline.Count > 0)
        {
            ResolveItemPipeline(_itemPipeline);
            _itemPipeline.Clear();
        }
    }
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
                currentItem.Mechanics.AddRange(nextItem.Mechanics);
                pipeline.RemoveAt(i + 1);
                i--;
            }
        }

        // 2. Stable Sort by Priority
        var sortedPipeline = pipeline.OrderBy(item => GetItemPriority(item)).ToList();

        // 3. Hydrate Entity State
        foreach (var item in sortedPipeline)
        {
            HydrateEntityFromItem(item);
        }
    }
    private int GetItemPriority(ItemData item)
    {
        int priority = 50;
        foreach (var mech in item.Mechanics)
        {
            string payloadLower = mech.PayloadString?.ToLower() ?? "";
            if (mech.Prefix == "k" && payloadLower == "permissive") return 0;
            if (mech.Prefix == "k" && payloadLower == "stasis") priority = 99;
            else if (mech.Prefix == "facade") priority = 100;
        }
        return priority;
    }
    private void HydrateEntityFromItem(ItemData item)
    {
        bool canMapNatively = true;
        bool isLeftMidException = false;

        // Check if it's purely a named item with no mechanics
        if (item.Mechanics.Count == 0 && !string.IsNullOrEmpty(item.entityName))
        {
            items.Add(item.entityName);
            return;
        }

        foreach (var mech in item.Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";

            if (mech.PayloadData is ModifierData)
            {
                canMapNatively = false;
                break;
            }

            // Check if this 'hat' is secretly a structured Sticker Target rule OR an Egg rule
            if (pfx == "hat")
            {
                if (mech.PayloadData is HeroData hatHero && IsStickerTargetOverrideHat(hatHero, out _, out _))
                {
                    if (mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                        mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                    {
                        isLeftMidException = true;
                    }
                    continue; // Is natively mappable!
                }
                else if (mech.PayloadString != null && mech.PayloadString.StartsWith("egg.(", StringComparison.OrdinalIgnoreCase))
                {
                    if (mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                        mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                    {
                        isLeftMidException = true;
                    }
                    continue; // Is natively mappable!
                }
            }

            if (pfx != "t" && pfx != "gift" && pfx != "learn" && pfx != "abilitydata" && pfx != "k" && pfx != "facade" && pfx != "sticker" && pfx != "")
            {
                canMapNatively = false;
                break;
            }

            if ((pfx == "k" || pfx == "facade" || pfx == "sticker" || pfx == "") && mech.Targets.Count == 0)
            {
                if (pfx == "" && ItemDomainRules.TogItems.Contains(mech.PayloadString)) continue;
                canMapNatively = false;
                break;
            }
        }

        if (!canMapNatively)
        {
            string exportedItem = item.Export();
            if (!string.IsNullOrEmpty(exportedItem)) items.Add(exportedItem);
        }

        if (item.LearnedAbilities != null && item.LearnedAbilities.Count > 0) baseAbilityData.AddRange(item.LearnedAbilities);

        foreach (var mech in item.Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";

            if (pfx == "t")
            {
                if (mech.PayloadString != null && mech.PayloadString.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase)) curses.Add(mech.PayloadString.Substring(5));
                else traits.Add(mech.PayloadString);
            }
            else if (pfx == "gift") blessings.Add(mech.PayloadString);
            else if (pfx == "learn" || pfx == "abilitydata") baseAbilityData.Add(mech.PayloadString);
            else if (pfx == "hat")
            {
                if (mech.PayloadData is HeroData hatHero && IsStickerTargetOverrideHat(hatHero, out DiceSideData.PayloadTarget? parsedTarget, out ItemMechanic innerSticker))
                {
                    if (mech.Targets != null && mech.Targets.Count > 0)
                    {
                        List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();

                        if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) &&
                            mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                            mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                        {
                            targetFaces.Remove(1);
                        }

                        ApplyMechanicToDiceSides(targetFaces, innerSticker, parsedTarget);
                    }
                }
                else if (mech.PayloadString != null && mech.PayloadString.StartsWith("egg.(", StringComparison.OrdinalIgnoreCase))
                {
                    if (mech.Targets != null && mech.Targets.Count > 0)
                    {
                        List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();

                        if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) &&
                            mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                            mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                        {
                            targetFaces.Remove(1);
                        }

                        // Extract inner payload and strip outer parens
                        string innerPayload = mech.PayloadString.Substring(4).Trim();
                        innerPayload = StaticBranchTracing.StripOuterParens(innerPayload);

                        foreach (int faceIdx in targetFaces)
                        {
                            if (faceIdx < 0 || faceIdx >= 6) continue;
                            if (diceSides == null) InitializeDiceFaces();
                            if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

                            diceSides[faceIdx].faceType = DiceSideData.DiceFaceType.Egg;
                            diceSides[faceIdx].payload = innerPayload;
                        }
                    }
                }
            }
            else if (pfx == "k" || pfx == "facade" || pfx == "sticker" || pfx == "")
            {
                if (mech.Targets != null && mech.Targets.Count > 0)
                {
                    List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();

                    if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) &&
                        mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                        mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                    {
                        targetFaces.Remove(1);
                    }

                    // Intercept blindfold when directed at an egg face to append it to the payload (stops .i.left2.k.blindfold)
                    string keyword = mech.PayloadString?.Trim().ToLower() ?? "";
                    if (keyword == "blindfold")
                    {
                        bool appliedToEgg = false;
                        foreach (int faceIdx in targetFaces)
                        {
                            if (diceSides != null && diceSides[faceIdx] != null && diceSides[faceIdx].faceType == DiceSideData.DiceFaceType.Egg)
                            {
                                if (string.IsNullOrEmpty(diceSides[faceIdx].payload)) diceSides[faceIdx].payload = "";
                                if (!diceSides[faceIdx].payload.EndsWith("#blindfold", StringComparison.OrdinalIgnoreCase))
                                {
                                    diceSides[faceIdx].payload += "#blindfold";
                                }
                                appliedToEgg = true;
                            }
                        }
                        if (appliedToEgg) continue;
                    }

                    ApplyMechanicToDiceSides(targetFaces, mech);
                }
            }
        }
    }

    private bool IsStickerTargetOverrideHat(HeroData hatHero, out DiceSideData.PayloadTarget? target, out ItemMechanic innerSticker)
    {
        target = null;
        innerSticker = null;

        if (!string.Equals(hatHero.baseReplica, "Fey", StringComparison.OrdinalIgnoreCase)) return false;

        int leftSd = hatHero.diceSides[0]?.effectID ?? 0;
        if (leftSd != 179 && leftSd != 185 && leftSd != 186) return false;

        DiceSideData stickerFace = hatHero.diceSides.FirstOrDefault(s => s != null && s.faceType == DiceSideData.DiceFaceType.Sticker);

        if (stickerFace == null || !stickerFace.keywords.Contains("togtarg")) return false;

        bool hasTogfri = stickerFace.keywords.Contains("togfri");

        // Strict, direct enum mapping based on the Left Face ID rules
        if (leftSd == 179) target = hasTogfri ? DiceSideData.PayloadTarget.AllEnemies : DiceSideData.PayloadTarget.AllAllies;
        else if (leftSd == 185) target = DiceSideData.PayloadTarget.Everyone;
        else if (leftSd == 186) target = DiceSideData.PayloadTarget.Self;

        innerSticker = new ItemMechanic
        {
            Prefix = "sticker",
            PayloadString = stickerFace.payload
        };

        foreach (var kw in stickerFace.keywords)
        {
            if (kw != "togtarg" && kw != "togfri") innerSticker.ChainedKeywords.Add(kw);
        }

        return true;
    }
    protected void ApplyMechanicToDiceSides(List<int> targetFaces, ItemMechanic mech, DiceSideData.PayloadTarget? overrideTarget = null)
    {
        foreach (int faceIdx in targetFaces)
        {
            if (faceIdx < 0 || faceIdx >= 6) continue;
            if (diceSides == null) InitializeDiceFaces();
            if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

            string lowerPrefix = mech.Prefix?.ToLower() ?? "";
            string payload = mech.PayloadString?.Trim() ?? "";

            foreach (string chainKw in mech.ChainedKeywords)
            {
                string cleanKw = chainKw.Trim().ToLower();
                if (cleanKw.StartsWith("k.")) cleanKw = cleanKw.Substring(2);
                if (!diceSides[faceIdx].keywords.Contains(cleanKw))
                    diceSides[faceIdx].keywords.Add(cleanKw);
            }

            if (lowerPrefix == "k" || lowerPrefix == "")
            {
                string keyword = payload.ToLower();
                if (keyword == "ritemx.dae9" || keyword == "unpack.ritemx.644f") keyword = "future";

                if (!string.IsNullOrEmpty(keyword) && !diceSides[faceIdx].keywords.Contains(keyword))
                    diceSides[faceIdx].keywords.Add(keyword);
            }
            else if (lowerPrefix == "facade")
            {
                string cleanPayload = payload;
                int firstColon = payload.IndexOf(':');
                if (firstColon != -1)
                {
                    int firstDotAfterColon = payload.IndexOf('.', firstColon);
                    if (firstDotAfterColon != -1)
                    {
                        // Truncate the facade's payload so it only contains its coordinates (e.g., "Ber125:0:0:0")
                        cleanPayload = payload.Substring(0, firstDotAfterColon);

                        // Extract the remainder (e.g., ".img.Collector.thue.313172:52:-64")
                        string remainder = payload.Substring(firstDotAfterColon);

                        // Parse remainder metadata locally to prevent infinite pipeline recursion
                        if (!string.IsNullOrEmpty(remainder))
                        {
                            // Strip the leading dot and split by '.' to isolate metadata keys
                            string cleanRemainder = remainder.TrimStart('.');
                            List<string> metaTokens = new List<string>(cleanRemainder.Split('.'));

                            for (int metaIdx = 0; metaIdx < metaTokens.Count; metaIdx++)
                            {
                                string metaTokenLower = metaTokens[metaIdx].ToLower();

                                // Directly parse the keys (img, thue, etc.) on this instance
                                TryProcessCommonMetadata(metaTokens, ref metaIdx, metaTokenLower);
                            }
                        }
                    }
                }

                string[] facadeParts = cleanPayload.Split(':');
                diceSides[faceIdx].facadeID = facadeParts[0];
                if (facadeParts.Length > 1)
                {
                    var colorParts = facadeParts.Skip(1)
                                                .Take(3)
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
            }
            else if (lowerPrefix == "sticker")
            {
                diceSides[faceIdx].faceType = DiceSideData.DiceFaceType.Sticker;
                diceSides[faceIdx].payload = payload;

                // Final payload target application
                if (overrideTarget.HasValue)
                {
                    diceSides[faceIdx].payloadTarget = overrideTarget.Value;
                }
                else if (diceSides[faceIdx].keywords.Contains("togfri"))
                {
                    // Strict assignment to the Enum
                    diceSides[faceIdx].payloadTarget = DiceSideData.PayloadTarget.Enemy;
                    diceSides[faceIdx].keywords.Remove("togfri"); // Strip raw visual to avoid duplicates on export
                }
            }
        }
    }
    protected bool TryProcessAppendedDoc(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "i" && i + 5 < tokens.Count &&
            tokens[i + 1].Equals("self", StringComparison.OrdinalIgnoreCase) &&
            tokens[i + 2].Equals("Wolf", StringComparison.OrdinalIgnoreCase) &&
            tokens[i + 3].Equals("doc", StringComparison.OrdinalIgnoreCase) &&
            tokens[i + 5].Equals("spirit", StringComparison.OrdinalIgnoreCase))
        {
            appendedDoc = tokens[i + 4];
            i += 5; // Move parser to the end of '.spirit'
            return true;
        }
        return false;
    }
    protected void ExtractKnowledge(List<string> tokens, List<ItemData> itemPipeline, bool processTraitsAndCollections = true)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            // 1. Handle recursive parenthesis
            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                ProcessRecursiveParentheses(originalToken, (innerTokens) =>
                    ExtractKnowledge(innerTokens, itemPipeline, processTraitsAndCollections));
                continue;
            }

            // 2. Metadata Processors
            if (TryProcessCommonMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessEntityMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessSpecificMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            if (TryProcessTriggerData(tokens, ref i, tokenLower)) continue;
            if (TryProcessOrbData(tokens, ref i, tokenLower)) continue;
            if (TryProcessAppendedDoc(tokens, ref i, tokenLower)) continue;

            // 3. Optional Trait & Collection Processing
            if (processTraitsAndCollections)
            {
                if (tokenLower == "t")
                {
                    ProcessTraitToken(tokens, ref i);
                    continue;
                }
                if (TryProcessCollections(tokens, ref i, tokenLower)) continue;
            }

            // 4. Unified Item Processing
            if (tokenLower == "i")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) continue;

                int length = ItemDomainRules.GetItemBlockLength(tokens, startIndex);

                // NEW: Intercept and shrink the item block if it swallowed entity-level metadata keys
                for (int k = 0; k < length; k++)
                {
                    int tokenIdx = startIndex + k;
                    if (tokenIdx >= tokens.Count) break;

                    string peek = tokens[tokenIdx].ToLower();
                    if (EntityDomainRules.CommonMetadataKeys.Contains(peek))
                    {
                        length = k; // Truncate the item payload before the entity property
                        break;
                    }
                }

                if (length > 0)
                {
                    List<string> subTokens = tokens.GetRange(startIndex, length);
                    i += length; // Safely advance past the block

                    string itemString = string.Join(".", subTokens);
                    ItemData parsedItem = new ItemData();
                    parsedItem.Parse(StaticBranchTracing.StripOuterParens(itemString));

                    if (string.IsNullOrEmpty(parsedItem.entityName) && parsedItem.Mechanics.Count == 0)
                    {
                        parsedItem.entityName = itemString;
                    }

                    itemPipeline.Add(parsedItem);
                }
                continue;
            }
        }
    }
    protected virtual bool TryProcessSpecificMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        return false;
    }
    protected bool TryProcessCollections(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;

        if (tokenLower == "t" || tokenLower == "gift" || tokenLower == "learn")
        {
            int length = EntityDomainRules.GetCollectionBlockLength(tokens, i + 1);
            if (length > 0)
            {
                string payload = string.Join(".", tokens.GetRange(i + 1, length));
                if (tokenLower == "t") traits.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                else if (tokenLower == "gift") blessings.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                else baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                i += length;
            }
            return true;
        }

        if (tokenLower == "abilitydata" || tokenLower == "triggerhpdata" || tokenLower == "onhitdata")
        {
            int length = AbilityDomainRules.GetAbilityBlockLength(tokens, i);
            if (length > 1)
            {
                string payload = string.Join(".", tokens.GetRange(i + 1, length - 1));
                if (payload.StartsWith("(")) AddCustomAbility(AbilityData.CreateAbility(payload));
                else baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                i += length - 1;
            }
            return true;
        }
        return false;
    }
    protected string ExtractBaseIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return token;

        while (token.StartsWith("(") && token.EndsWith(")"))
        {
            token = StaticBranchTracing.StripOuterParens(token);
            List<string> innerTokens = StaticBranchTracing.TopLevelSplit(token, '.');
            if (innerTokens.Count > 0)
            {
                token = innerTokens[0];
            }
            else
            {
                break;
            }
        }
        return token;
    }

    ////////////////////////////
    /// FACE BUILDING //////////
    ////////////////////////////

    public string BuildFaceModifiers(bool includeInlineFacades)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            if (face == null) continue; // Safety catch

            List<string> chunks = new List<string>();

            // 1. Process Payloads (Handles Casts, Stickers, Hats, and Eggs)
            bool hasHatWrapper = ProcessFacePayload(face, chunks);

            // 2. Process Standard Keywords
            ProcessFaceKeywords(face, chunks);

            // 3. Process Facades
            ProcessFaceFacades(face, chunks, includeInlineFacades);

            // 4. Process Description
            ProcessFaceDescription(face, chunks);

            // 5. Process Stasis (Must always be last keyword)
            ProcessFaceStasis(face, chunks);

            // 6. Final String Grouping & Left Face Logic
            if (chunks.Count > 0)
            {
                string templateString = string.Join("#", chunks);

                // LEFT FACE EXCEPTION: Must route internal side to `mid` when wrapped in hat
                if (i == 0 && hasHatWrapper)
                {
                    string resolvedMod = string.Format(templateString, "mid");
                    modSb.Append($".i.left.mid.{resolvedMod}");
                }
                else
                {
                    int faceMask = 1 << i;
                    if (groupedModifiers.ContainsKey(templateString)) groupedModifiers[templateString] |= faceMask;
                    else groupedModifiers[templateString] = faceMask;
                }
            }
        }

        // Apply best aliases for grouped identical faces
        foreach (var kvp in groupedModifiers)
        {
            string templateString = kvp.Key;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(kvp.Value);
            foreach (string alias in optimalAliases)
            {
                string resolvedMod = templateString.Contains("{0}") ? string.Format(templateString, alias) : templateString;
                modSb.Append($".i.{alias}.{resolvedMod}");
            }
        }

        return modSb.ToString();
    }
    private bool ProcessFacePayload(DiceSideData face, List<string> chunks)
    {
        if (face.faceType == DiceSideData.DiceFaceType.Base || string.IsNullOrWhiteSpace(face.payload))
            return false;

        string payloadStr = face.payload.Trim();

        // Branch out for specialized parsing
        if (face.faceType == DiceSideData.DiceFaceType.Egg)
        {
            return ProcessEggPayload(payloadStr, chunks);
        }
        else
        {
            return ProcessStandardPayload(face, payloadStr, chunks);
        }
    }
    private bool ProcessEggPayload(string payloadStr, List<string> chunks)
    {
        bool hasBlindfold = payloadStr.EndsWith("#blindfold", StringComparison.OrdinalIgnoreCase);
        string cleanSummon = hasBlindfold ? payloadStr.Substring(0, payloadStr.Length - 10) : payloadStr;

        string fullSummonExport = cleanSummon; // Fallback to raw string if entity lookup fails
        if (ModPackage.Instance != null)
        {
            var summonHero = ModPackage.Instance.Heroes?.FirstOrDefault(h => string.Equals(h.entityName, cleanSummon, StringComparison.OrdinalIgnoreCase));
            if (summonHero != null) fullSummonExport = summonHero.Export();
            else
            {
                var summonMonster = ModPackage.Instance.Monsters?.FirstOrDefault(m => string.Equals(m.entityName, cleanSummon, StringComparison.OrdinalIgnoreCase));
                if (summonMonster != null) fullSummonExport = summonMonster.Export();
            }
        }

        // Avoid double parenthesis wrappers since EntityData.Export() now applies them intrinsically 
        if (!fullSummonExport.StartsWith("("))
        {
            fullSummonExport = $"({fullSummonExport})";
        }

        // 1. Hat MUST come first to establish the dice face override
        chunks.Add($"hat.(egg.{fullSummonExport})");

        // 2. Blindfold item MUST come immediately after the Hat
        // Outputting standalone "blindfold" triggers standard chaining resulting in #blindfold
        if (hasBlindfold)
        {
            chunks.Add("blindfold");
        }

        return true; // Indicates a hat wrapper is active for this face
    }
    private bool ProcessStandardPayload(DiceSideData face, string payloadStr, List<string> chunks)
    {
        string prefix = face.faceType.ToString().ToLower();

        if (!payloadStr.StartsWith("(") && (payloadStr.Contains(".") || payloadStr.Contains("#") || payloadStr.Contains(":")))
            payloadStr = $"({payloadStr})";

        bool applyStickerRules = face.faceType == DiceSideData.DiceFaceType.Sticker;

        // If the face is an Enchant with a target override, divert it to sticker format
        if (face.faceType == DiceSideData.DiceFaceType.Enchant && face.payloadTarget.HasValue)
        {
            prefix = "sticker";
            payloadStr = $"(self.{payloadStr})";
            applyStickerRules = true;
        }

        string innerPayloadStr = $"{prefix}.{payloadStr}";
        string hatWrapperFmt = null;

        if (applyStickerRules)
        {
            if (face.togtime)
                innerPayloadStr += "#togtime";

            if (face.payloadTarget.HasValue)
            {
                switch (face.payloadTarget.Value)
                {
                    case DiceSideData.PayloadTarget.Enemy:
                        innerPayloadStr += "#togfri";
                        chunks.Add(innerPayloadStr);
                        break;
                    case DiceSideData.PayloadTarget.AllAllies:
                        hatWrapperFmt = "Fey.sd.179.i.{0}." + innerPayloadStr + "#togtarg";
                        break;
                    case DiceSideData.PayloadTarget.AllEnemies:
                        hatWrapperFmt = "Fey.sd.179.i.{0}." + innerPayloadStr + "#togtarg#togfri";
                        break;
                    case DiceSideData.PayloadTarget.Everyone:
                        hatWrapperFmt = "Fey.sd.185.i.{0}." + innerPayloadStr + "#togtarg";
                        break;
                    case DiceSideData.PayloadTarget.Self:
                        hatWrapperFmt = "Fey.sd.186.i.{0}." + innerPayloadStr + "#togtarg";
                        break;
                    case DiceSideData.PayloadTarget.Ally:
                    case DiceSideData.PayloadTarget.None:
                    default:
                        chunks.Add(innerPayloadStr);
                        break;
                }
            }
            else
            {
                chunks.Add(innerPayloadStr);
            }
        }
        else
        {
            if (face.togtime && face.faceType == DiceSideData.DiceFaceType.Enchant)
                innerPayloadStr += "#togtime";
            chunks.Add(innerPayloadStr);
        }

        if (hatWrapperFmt != null)
        {
            chunks.Add($"hat.({hatWrapperFmt})");
            return true; // Target overrides successfully utilized a hat wrapper
        }

        return false;
    }
    private void ProcessFaceKeywords(DiceSideData face, List<string> chunks)
    {
        // 1. Permissive must always be evaluated first if present
        if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase)))
        {
            chunks.Add("k.permissive");
        }

        // 2. Regular keywords
        foreach (var kw in face.keywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            string cleanKw = kw.Trim().ToLower();
            if (cleanKw != "permissive" && cleanKw != "stasis")
            {
                if (cleanKw == "future") chunks.Add("ritemx.dae9");
                else chunks.Add($"k.{cleanKw}");
            }
        }
    }
    private void ProcessFaceFacades(DiceSideData face, List<string> chunks, bool includeInlineFacades)
    {
        if (!includeInlineFacades || string.IsNullOrWhiteSpace(face.facadeID)) return;

        string facStr = $"facade.{face.facadeID.Trim()}";

        if (!string.IsNullOrWhiteSpace(face.facadeColor))
        {
            string[] hsv = face.facadeColor.Split(':');
            List<string> parts = new List<string>();

            for (int pIdx = 0; pIdx < hsv.Length; pIdx++)
                parts.Add(string.IsNullOrWhiteSpace(hsv[pIdx]) ? "0" : hsv[pIdx].Trim());

            while (parts.Count < 3) parts.Add("0");

            if (parts[0] == "0" && parts[1] == "0" && parts[2] == "0") facStr += ":0";
            else facStr += $":{parts[0]}:{parts[1]}:{parts[2]}";
        }
        else
        {
            facStr += ":0";
        }
        chunks.Add(facStr);
    }
    private void ProcessFaceDescription(DiceSideData face, List<string> chunks)
    {
        if (!string.IsNullOrEmpty(face.sidesc))
        {
            chunks.Add($"sidesc.{face.sidesc}");
        }
    }
    private void ProcessFaceStasis(DiceSideData face, List<string> chunks)
    {
        // MUST BE LAST: Stasis keyword terminates effects that follow it
        if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("stasis", StringComparison.OrdinalIgnoreCase)))
        {
            chunks.Add("k.stasis");
        }
    }
}