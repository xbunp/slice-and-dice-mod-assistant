using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    protected void ProcessTraitToken(List<string> tokens, ref int i, HashSet<string> domainPropertyKeys)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            if (domainPropertyKeys.Contains(peek)) break;
            endIndex++;
        }

        int count = endIndex - startIndex;
        if (count > 0)
        {
            string tPayload = string.Join(".", tokens.GetRange(startIndex, count));
            i = endIndex - 1;
            ProcessTraitPayload(tPayload); // Routed
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
                customPayloads.Add(new CustomPayload { Prefix = "triggerhpdata", Data = thp });
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
                customPayloads.Add(new CustomPayload { Prefix = "onhitdata", Data = ohd });
            }
            return true;
        }
        return false;
    }
    protected void AppendColorModifier(StringBuilder sb)
    {
        if (phue != null && phue.colorRange != 0) // Prevents adding empty payloads if unassigned
        {
            sb.Append($".{PackPHue(phue)}");
        }
        if (thue != null && (thue.colorRange != 0 || thue.colorOffset != 0))
        {
            sb.Append($".{PackTHue(thue)}");
        }

        if (h != 0 || s != 0 || v != 0)
        {
            sb.Append($".hsv.{h}:{s}:{v}");
        }
        else if (hue != 0)
        {
            sb.Append($".hue.{hue}");
        }
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
    protected abstract int GetEndOfBlockIndex(List<string> tokens, int startIndex);
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

    /*
    protected void ApplyMechanicToDiceSides(List<int> targetFaces, ItemMechanic mech)
    {
        foreach (int faceIdx in targetFaces)
        {
            if (faceIdx < 0 || faceIdx >= 6) continue;
            if (diceSides == null) InitializeDiceFaces();
            if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

            string lowerPrefix = mech.Prefix?.ToLower() ?? "";
            string payload = mech.PayloadString?.Trim() ?? "";

            if (lowerPrefix == "k" || lowerPrefix == "")
            {
                string keyword = payload.ToLower();
                if (keyword == "ritemx.dae9" || keyword == "unpack.ritemx.644f") keyword = "future";

                if (!string.IsNullOrEmpty(keyword) && !diceSides[faceIdx].keywords.Contains(keyword))
                    diceSides[faceIdx].keywords.Add(keyword);

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
                diceSides[faceIdx].faceType = DiceSideData.DiceFaceType.Sticker;
                diceSides[faceIdx].payload = payload;
            }
        }
    }
    */

    private void HydrateEntityFromItem(ItemData item)
    {
        bool canMapNatively = true;
        bool isLeftMidException = false;

        foreach (var mech in item.Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";

            // NEW RULE: If the item's payload is a Modifier, it is an entity-wrapper item. 
            // It cannot map natively to dice faces, so force it into CustomPayloads!
            if (mech.PayloadData is ModifierData)
            {
                canMapNatively = false;
                break;
            }

            // Check if this 'hat' is secretly a structured Sticker Target rule
            if (pfx == "hat" && mech.PayloadData is HeroData hatHero)
            {
                if (IsStickerTargetOverrideHat(hatHero, out _, out _))
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
        }

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
            string pfx = mech.Prefix?.ToLower() ?? "";

            if (pfx == "t")
            {
                if (mech.PayloadString != null && mech.PayloadString.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
                    curses.Add(mech.PayloadString.Substring(5));
                else
                    traits.Add(mech.PayloadString);
            }
            else if (pfx == "gift")
            {
                blessings.Add(mech.PayloadString);
            }
            else if (pfx == "learn" || pfx == "abilitydata")
            {
                baseAbilityData.Add(mech.PayloadString);
            }
            else if (pfx == "hat" && mech.PayloadData is HeroData hatHero)
            {
                if (IsStickerTargetOverrideHat(hatHero, out DiceSideData.PayloadTarget? parsedTarget, out ItemMechanic innerSticker))
                {
                    if (mech.Targets != null && mech.Targets.Count > 0)
                    {
                        List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();

                        // Reverse the Left Face Exception: Isolate target to 0 (Left)
                        if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) &&
                            mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                            mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                        {
                            targetFaces.Remove(1);
                        }

                        ApplyMechanicToDiceSides(targetFaces, innerSticker, parsedTarget);
                    }
                }
            }
            else if (pfx == "k" || pfx == "facade" || pfx == "sticker" || pfx == "")
            {
                if (mech.Targets != null && mech.Targets.Count > 0)
                {
                    List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();

                    // Ensure chained Facades in a Left Face Exception don't pollute the Mid face
                    if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) &&
                        mech.Targets.Contains("left", StringComparer.OrdinalIgnoreCase) &&
                        mech.Targets.Contains("mid", StringComparer.OrdinalIgnoreCase))
                    {
                        targetFaces.Remove(1);
                    }

                    ApplyMechanicToDiceSides(targetFaces, mech);
                }
            }
        }

        if (item.Mechanics.Count == 0 && !string.IsNullOrEmpty(item.entityName))
        {
            items.Add(item.entityName);
        }
    }
    public string BuildFaceModifiers(bool includeInlineFacades)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            List<string> chunks = new List<string>();

            // 1. MUST BE FIRST: Dynamic Payloads & Targets (Overwrites the base face)
            string hatWrapperFmt = null;
            if (face.faceType != DiceSideData.DiceFaceType.Base && !string.IsNullOrWhiteSpace(face.payload))
            {
                string payloadStr = face.payload.Trim();
                string prefix = face.faceType.ToString().ToLower();

                if (!payloadStr.StartsWith("(") && (payloadStr.Contains(".") || payloadStr.Contains("#") || payloadStr.Contains(":")))
                    payloadStr = $"({payloadStr})";

                string innerPayloadStr = $"{prefix}.{payloadStr}";

                // Clean Enum-based Target Routing
                if (face.payloadTarget.HasValue && face.faceType == DiceSideData.DiceFaceType.Sticker)
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

            // If wrapping in a hat, it becomes the core payload for this face
            if (hatWrapperFmt != null)
            {
                chunks.Add($"hat.({hatWrapperFmt})");
            }

            // 2. Permissive (Must be first among keywords)
            if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase)))
            {
                chunks.Add("k.permissive");
            }

            // 3. Add all regular keywords
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

            // 4. Facades (Must go after keywords so extra UI doesn't break)
            if (includeInlineFacades && !string.IsNullOrWhiteSpace(face.facadeID))
            {
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

            // 5. MUST BE LAST: stasis
            if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("stasis", StringComparison.OrdinalIgnoreCase)))
            {
                chunks.Add("k.stasis");
            }

            // Final string generation mapping
            if (chunks.Count > 0)
            {
                string templateString = string.Join("#", chunks);

                // LEFT FACE EXCEPTION: Must route internal side to `mid` when wrapped in hat
                if (i == 0 && hatWrapperFmt != null)
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
}

/*

     private void HydrateEntityFromItem(ItemData item)
    {
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
            else if (mech.Prefix == "k" || mech.Prefix == "facade" || mech.Prefix == "sticker" || mech.Prefix == "")
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

    public string BuildFaceModifiers(bool includeInlineFacades)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            List<string> chunks = new List<string>();

            // 1. MUST BE FIRST: Check and add permissive before anything else
            if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase)))
            {
                chunks.Add("k.permissive");
            }

            // 2. Add all regular keywords (excluding permissive and stasis)
            foreach (var kw in face.keywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                string cleanKw = kw.Trim().ToLower();
                if (cleanKw != "permissive" && cleanKw != "stasis")
                {
                    if (cleanKw == "future")
                    {
                        chunks.Add("ritemx.dae9");
                    }
                    else
                    {
                        chunks.Add($"k.{cleanKw}");
                    }
                }
            }

            // 3. Dynamic Payloads (Sticker, Cast, Enchant, Egg)
            if (face.faceType != DiceSideData.DiceFaceType.Base && !string.IsNullOrWhiteSpace(face.payload))
            {
                string payloadStr = face.payload.Trim();
                string prefix = face.faceType.ToString().ToLower();

                // Wrap in brackets if it's a complex item syntax, otherwise keep clean
                if (!payloadStr.StartsWith("(") && (payloadStr.Contains(".") || payloadStr.Contains("#") || payloadStr.Contains(":")))
                {
                    payloadStr = $"({payloadStr})";
                }
                chunks.Add($"{prefix}.{payloadStr}");
            }

            // 4. Facade processing happens after standard keywords and payloads
            if (includeInlineFacades && !string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";

                if (!string.IsNullOrWhiteSpace(face.facadeColor))
                {
                    string[] hsv = face.facadeColor.Split(':');
                    List<string> parts = new List<string>();

                    for (int pIdx = 0; pIdx < hsv.Length; pIdx++)
                    {
                        parts.Add(string.IsNullOrWhiteSpace(hsv[pIdx]) ? "0" : hsv[pIdx].Trim());
                    }
                    while (parts.Count < 3) parts.Add("0");

                    if (parts[0] == "0" && parts[1] == "0" && parts[2] == "0")
                    {
                        facStr += ":0";
                    }
                    else
                    {
                        facStr += $":{parts[0]}:{parts[1]}:{parts[2]}";
                    }
                }
                else
                {
                    facStr += ":0";
                }

                chunks.Add(facStr);
            }

            // 5. MUST BE LAST: Check and add stasis after everything, including facades
            if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("stasis", StringComparison.OrdinalIgnoreCase)))
            {
                chunks.Add("k.stasis");
            }

            if (chunks.Count > 0)
            {
                string modString = string.Join("#", chunks);
                int faceMask = 1 << i;
                if (groupedModifiers.ContainsKey(modString)) groupedModifiers[modString] |= faceMask;
                else groupedModifiers[modString] = faceMask;
            }
        }

        foreach (var kvp in groupedModifiers)
        {
            string modString = kvp.Key;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(kvp.Value);
            foreach (string alias in optimalAliases) modSb.Append($".i.{alias}.{modString}");
        }

        return modSb.ToString();
    }




    private bool IsStickerTargetOverrideHat(HeroData hatHero, out DiceSideData.PayloadTarget? target, out ItemMechanic innerSticker)
    {
        target = null;
        innerSticker = null;

        if (!string.Equals(hatHero.baseReplica, "Fey", StringComparison.OrdinalIgnoreCase)) return false;

        int leftSd = hatHero.diceSides[0]?.effectID ?? 0;
        if (leftSd != 179 && leftSd != 185 && leftSd != 186) return false;

        // Hunt down the nested sticker in the hat's parsed dice
        DiceSideData stickerFace = null;
        for (int i = 0; i < 6; i++)
        {
            if (hatHero.diceSides[i] != null && hatHero.diceSides[i].faceType == DiceSideData.DiceFaceType.Sticker)
            {
                stickerFace = hatHero.diceSides[i];
                break;
            }
        }

        if (stickerFace == null || !stickerFace.keywords.Contains("togtarg")) return false;

        bool hasTogfri = stickerFace.keywords.Contains("togfri");

        // Dynamically resolve target mappings
        if (leftSd == 179) target = hasTogfri ? GetPayloadTargetEnum("AllEnemies") : GetPayloadTargetEnum("AllAllies");
        else if (leftSd == 185) target = GetPayloadTargetEnum("Everyone");
        else if (leftSd == 186) target = GetPayloadTargetEnum("Self");

        innerSticker = new ItemMechanic
        {
            Prefix = "sticker",
            PayloadString = stickerFace.payload
        };

        // Retain any actual organic keywords passed on the sticker by the modder
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

            // Safely propagate `#` delimited chained keywords across ALL mechanics (including stickers)
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
                diceSides[faceIdx].faceType = DiceSideData.DiceFaceType.Sticker;
                diceSides[faceIdx].payload = payload;

                // Final payload target application
                if (overrideTarget.HasValue)
                {
                    diceSides[faceIdx].payloadTarget = overrideTarget;
                }
                else if (diceSides[faceIdx].keywords.Contains("togfri"))
                {
                    // Detect "Enemy" base target
                    diceSides[faceIdx].payloadTarget = GetPayloadTargetEnum("Enemy");
                    diceSides[faceIdx].keywords.Remove("togfri"); // Strip raw visual to avoid duplicates
                }
            }
        }
    }




*/