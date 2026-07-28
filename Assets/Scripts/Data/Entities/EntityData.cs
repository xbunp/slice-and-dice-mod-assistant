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
    [System.NonSerialized]
    public bool isCoreWrappedInParens = true;
    [System.NonSerialized]
    public bool isOuterWrappedInParens = false;

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
            if (!customOrbs.Any(o => string.Equals(o.entityName, orb.entityName, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(o.hardcodedAbilityName, orb.hardcodedAbilityName, StringComparison.OrdinalIgnoreCase)))
                customOrbs.Add(orb);
        }
        else if (ability is OnHitData onHit)
        {
            if (!customOnHits.Any(o => string.Equals(o.entityName, onHit.entityName, StringComparison.OrdinalIgnoreCase))) customOnHits.Add(onHit);
        }
        else if (ability is TriggerHPData trig)
        {
            if (!customTriggerHPs.Any(t => string.Equals(t.entityName, trig.entityName, StringComparison.OrdinalIgnoreCase))) customTriggerHPs.Add(trig);
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

    // Unifies lookahead trait parsing (supports both strings and nested custom modifiers)
    // Notice we dropped the Hashset parameter entirely!

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
            else if (payload.Data is OnHitData || payload.Data is TriggerHPData || payload.Data is AbilityData || payload.Type == PayloadType.Modifier)
            {
                string exported = payload.Export();
                if (!string.IsNullOrEmpty(exported)) outerPayloads.Add(exported);
            }
            else
            {
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
    protected virtual bool TryProcessSpecificMetadata(TokenStream stream) { return false; }
    protected bool TryProcessEntityMetadata(TokenStream stream)
    {
        if (stream.Peek().ToLower() == "hp")
        {
            stream.Consume(); // 'hp'
            if (!stream.IsEOF && int.TryParse(stream.Consume(), out int hpVal)) hp = hpVal;
            return true;
        }
        return false;
    }
    protected bool TryProcessDiceSides(TokenStream stream)
    {
        if (stream.Peek().ToLower() != "sd") return false;
        stream.Consume(); // 'sd'
        if (stream.IsEOF) return true;

        string[] faces = stream.Consume().Split(':');
        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
        {
            if (faces[f] == "0" || faces[f] == "0-0") continue;
            if (diceSides[f] == null) diceSides[f] = new DiceSideData { effectID = 0, pips = 0, keywords = new List<string>() };
            string[] faceParts = faces[f].Split('-');
            int.TryParse(faceParts[0], out diceSides[f].effectID);
            if (faceParts.Length > 1) int.TryParse(faceParts[1], out diceSides[f].pips);
            else diceSides[f].pips = 0;
        }
        return true;
    }
    protected bool TryProcessTriggerData(TokenStream stream)
    {
        string tokenLower = stream.Peek().ToLower();
        bool isI = tokenLower == "i";
        string targetToken = isI ? stream.PeekNext().ToLower() : tokenLower;

        if (targetToken == "triggerhpdata" || targetToken == "onhitdata")
        {
            if (isI) stream.Consume(); // 'i'
            stream.Consume(); // key
            if (!stream.IsEOF)
            {
                string payload = StaticBranchTracing.StripOuterParens(stream.Consume());
                if (targetToken == "triggerhpdata") { TriggerHPData thp = new TriggerHPData(); thp.Parse(payload); AddCustomAbility(thp); }
                else { OnHitData ohd = new OnHitData(); ohd.Parse(payload); AddCustomAbility(ohd); }
            }
            return true;
        }
        return false;
    }
    protected bool TryProcessOrbData(TokenStream stream)
    {
        string tokenLower = stream.Peek().ToLower();
        bool isI = tokenLower == "i";
        string targetToken = isI ? stream.PeekNext().ToLower() : tokenLower;

        if (targetToken == "orb")
        {
            if (isI) stream.Consume();
            stream.Consume(); // 'orb'
            if (stream.IsEOF) return true;

            string peekPayload = stream.Peek();
            if (OrbData.ValidBaseOrbs.Contains(peekPayload))
            {
                OrbData orb = new OrbData();
                orb.Parse($"orb.{stream.Consume()}");
                AddCustomAbility(orb);
                return true;
            }

            int endIndex = stream.Index;
            var raw = stream.GetRawList();
            while (endIndex < raw.Count)
            {
                if (raw[endIndex].StartsWith("(")) { endIndex++; break; } // Skip parens entirely
                if (EntityDomainRules.CommonMetadataKeys.Contains(raw[endIndex]) || EntityDomainRules.CommonCollectionKeys.Contains(raw[endIndex])) break;
                endIndex++;
            }

            string payload = string.Join(".", stream.ConsumeRange(endIndex - stream.Index));
            OrbData customOrb = new OrbData();
            customOrb.Parse(payload);
            AddCustomAbility(customOrb);
            return true;
        }
        return false;
    }
    protected bool TryProcessAppendedDoc(TokenStream stream)
    {
        if (stream.Peek().ToLower() == "i")
        {
            var raw = stream.GetRawList();
            int i = stream.Index;
            if (i + 5 < raw.Count &&
                raw[i + 1].Equals("self", StringComparison.OrdinalIgnoreCase) &&
                raw[i + 2].Equals("Wolf", StringComparison.OrdinalIgnoreCase) &&
                raw[i + 3].Equals("doc", StringComparison.OrdinalIgnoreCase) &&
                raw[i + 5].Equals("spirit", StringComparison.OrdinalIgnoreCase))
            {
                appendedDoc = raw[i + 4];
                stream.Advance(6);
                return true;
            }
        }
        return false;
    }
    protected void ProcessTraitToken(TokenStream stream)
    {
        stream.Consume(); // consume 't'
        if (stream.IsEOF) return;
        int length = EntityDomainRules.GetCollectionBlockLength(stream.GetRawList(), stream.Index);
        if (length > 0) ProcessTraitPayload(string.Join(".", stream.ConsumeRange(length)));
    }
    protected bool TryProcessCollections(TokenStream stream)
    {
        string tokenLower = stream.Peek().ToLower();
        bool isI = tokenLower == "i";
        string targetToken = isI ? stream.PeekNext().ToLower() : tokenLower;

        if (targetToken == "t" || targetToken == "gift" || targetToken == "learn")
        {
            if (isI) stream.Consume();
            stream.Consume();
            if (stream.IsEOF) return true;

            int length = EntityDomainRules.GetCollectionBlockLength(stream.GetRawList(), stream.Index);
            if (length > 0)
            {
                string payload = string.Join(".", stream.ConsumeRange(length));
                if (targetToken == "t") traits.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                else if (targetToken == "gift") blessings.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                else baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));

                if (targetToken == "t") traits = traits.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                else if (targetToken == "gift") blessings = blessings.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                else baseAbilityData = baseAbilityData.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            return true;
        }

        if (targetToken == "abilitydata" || targetToken == "triggerhpdata" || targetToken == "onhitdata")
        {
            if (isI) stream.Consume();
            stream.Consume();
            if (stream.IsEOF) return true;

            int length = AbilityDomainRules.GetAbilityBlockLength(stream.GetRawList(), stream.Index - 1) - 1;
            if (length > 0)
            {
                string payload = string.Join(".", stream.ConsumeRange(length));
                if (payload.StartsWith("(")) AddCustomAbility(AbilityData.CreateAbility(payload));
                else
                {
                    baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                    baseAbilityData = baseAbilityData.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
            return true;
        }
        return false;
    }
    protected bool TryProcessModifierData(TokenStream stream)
    {
        string peek = stream.Peek().ToLower();
        string prefix = "";

        // Handle "t" or "i" prefixes explicitly so outer modifiers (like t.jinx or i.self) retain their context
        if (peek == "t" || peek == "i")
        {
            string next = stream.PeekNext().ToLower();
            if (ModifierDomainRules.IsModifierStartToken(next))
            {
                prefix = peek;
                stream.Consume(); // Consume the 't' or 'i' prefix
                peek = stream.Peek().ToLower();
            }
        }

        if (ModifierDomainRules.IsModifierStartToken(peek))
        {
            int length = ModifierDomainRules.GetModifierBlockLength(stream.GetRawList(), stream.Index);
            if (length > 0)
            {
                string payload = string.Join(".", stream.ConsumeRange(length));
                ModifierData mod = new ModifierData();
                mod.Parse(payload);
                if (customPayloads == null) customPayloads = new List<CustomPayload>();
                customPayloads.Add(new CustomPayload { Prefix = prefix, Data = mod, Type = PayloadType.Modifier });
                return true;
            }
        }
        return false;
    }
    private void HydrateEntityFromItem(ItemData item)
    {
        bool canMapNatively = item.TryAbsorbIntoEntity(this);

        if (!canMapNatively)
        {
            // FIX: Route non-native items (Hats, Enchants) to CustomPayloads so CustomItemContextHelper can correctly bracket them to the outside!
            customPayloads.Add(new CustomPayload { Prefix = "i", Data = item, Type = PayloadType.Item });
        }
    }
    private void ProcessFaceKeywords(DiceSideData face, List<string> chunks)
    {
        if (face.keywords.Any(kw => kw != null && kw.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase)))
        {
            chunks.Add("k.permissive");
        }

        foreach (var kw in face.keywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            string cleanKw = kw.Trim().ToLower();
            if (cleanKw != "permissive" && cleanKw != "stasis")
            {
                if (cleanKw == "future") chunks.Add("ritemx.dae9");
                // FIX: Do NOT add 'k.' prefix to Tog items
                else if (ItemDomainRules.TogItems.Contains(cleanKw)) chunks.Add(cleanKw);
                else chunks.Add($"k.{cleanKw}");
            }
        }

        if (face.sideItems != null)
        {
            foreach (var item in face.sideItems)
            {
                chunks.Add(item.Export());
            }
        }
    }
    public void ApplyMechanicToDiceSides(List<int> targetFaces, ItemMechanic mech, DiceSideData.PayloadTarget? overrideTarget = null)
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
                if (!diceSides[faceIdx].keywords.Contains(cleanKw)) diceSides[faceIdx].keywords.Add(cleanKw);
            }

            if (lowerPrefix == "k" || lowerPrefix == "")
            {
                string keyword = payload.ToLower();
                if (keyword == "ritemx.dae9" || keyword == "unpack.ritemx.644f") keyword = "future";
                if (!string.IsNullOrEmpty(keyword) && !diceSides[faceIdx].keywords.Contains(keyword)) diceSides[faceIdx].keywords.Add(keyword);
            }
            else if (lowerPrefix == "facade")
            {
                string cleanPayload = payload;
                int firstColon = payload.IndexOf(':');
                if (firstColon != -1 && payload.IndexOf('.', firstColon) != -1) cleanPayload = payload.Substring(0, payload.IndexOf('.', firstColon));

                string[] facadeParts = cleanPayload.Split(':');
                diceSides[faceIdx].facadeID = facadeParts[0];
                if (facadeParts.Length > 1)
                {
                    var colorParts = facadeParts.Skip(1).Take(3).Select(p => string.IsNullOrWhiteSpace(p) ? "0" : p.Trim()).ToList();
                    while (colorParts.Count < 3) colorParts.Add("0");
                    diceSides[faceIdx].facadeColor = $"{colorParts[0]}:{colorParts[1]}:{colorParts[2]}";
                }
            }
            else if (lowerPrefix == "sticker" || lowerPrefix == "cast" || lowerPrefix == "enchant" || lowerPrefix == "hat")
            {
                // Assign payload directly. The enum mappings are handled strictly by Export() prefixing
                if (lowerPrefix == "hat" && mech.PayloadData is HeroData hatHero)
                {
                    diceSides[faceIdx].faceType = DiceSideData.DiceFaceType.Sticker;
                    diceSides[faceIdx].payload = hatHero.ExportAsHat();
                }
                else
                {
                    diceSides[faceIdx].faceType = lowerPrefix == "cast" ? DiceSideData.DiceFaceType.Cast :
                                                  lowerPrefix == "enchant" ? DiceSideData.DiceFaceType.Enchant :
                                                  DiceSideData.DiceFaceType.Sticker;
                    diceSides[faceIdx].payload = payload;
                }
            }
        }
    }
    protected void DetectBracketingState(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData)) return;
        string trimmed = rawData.Trim();
        isOuterWrappedInParens = trimmed.StartsWith("((") && trimmed.EndsWith(")");
        isCoreWrappedInParens = trimmed.StartsWith("(");
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

                bool isLeftFaceHat = i == 0 && hasHatWrapper;
                bool isEggOnOuterFace = face.faceType == DiceSideData.DiceFaceType.Egg && (i == 2 || i == 3 || i == 5);

                // EXCEPTION: Route via `mid` for general Left face hats OR when mapping Eggs to Top, Bot, or Rightmost
                if (isLeftFaceHat || isEggOnOuterFace)
                {
                    // Only string.Format if "{0}" actually exists to prevent arbitrary curly brace format exceptions
                    string resolvedMod = templateString.Contains("{0}") ? string.Format(templateString, "mid") : templateString;
                    string faceAlias = i == 0 ? "left" : (i == 2 ? "top" : (i == 3 ? "bot" : "rightmost"));

                    modSb.Append($".i.{faceAlias}.mid.{resolvedMod}");
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
            return ProcessEggPayload(face, payloadStr, chunks);
        }
        else
        {
            return ProcessStandardPayload(face, payloadStr, chunks);
        }
    }
    private bool ProcessEggPayload(DiceSideData face, string payloadStr, List<string> chunks)
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
        if (!fullSummonExport.StartsWith("("))
        {
            fullSummonExport = $"({fullSummonExport})";
        }

        string repeatPrefix = "";
        if (face.pips > 1)
        {
            repeatPrefix = $"x{face.pips}.";
        }

        // Place repeat multiplier outside the hat so ItemData can absorb it
        chunks.Add($"{repeatPrefix}hat.(egg.{fullSummonExport})");

        // Blindfold item MUST come immediately after the Hat
        if (hasBlindfold)
        {
            chunks.Add("blindfold");
        }
        return true;
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
    // IN: Assets/Scripts/Data/Entities/EntityData.cs -> ProcessFaceKeywords()

    // IN: ProcessFaceKeywords()

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

    // ====================================================================
    // EXPORT PIPELINE DEDICATED HELPER METHODS
    // ====================================================================
    protected void ExtractKnowledge(List<string> tokens, List<ItemData> itemPipeline, bool processTraitsAndCollections = true)
    {
        var stream = new TokenStream(tokens);
        while (!stream.IsEOF)
        {
            string originalToken = stream.Peek();
            string tokenLower = originalToken.ToLower();
            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                stream.Consume();
                ProcessRecursiveParentheses(originalToken, (innerTokens) => ExtractKnowledge(innerTokens, itemPipeline, processTraitsAndCollections));
                continue;
            }

            if (TryProcessCommonMetadata(stream)) continue;
            if (TryProcessEntityMetadata(stream)) continue;
            if (TryProcessSpecificMetadata(stream)) continue;
            if (TryProcessDiceSides(stream)) continue;
            if (TryProcessTriggerData(stream)) continue;
            if (TryProcessOrbData(stream)) continue;
            if (TryProcessAppendedDoc(stream)) continue;

            if (processTraitsAndCollections)
            {
                if (tokenLower == "t") { ProcessTraitToken(stream); continue; }
                if (TryProcessCollections(stream)) continue;
            }

            if (tokenLower == "i")
            {
                int startIndex = stream.Index + 1;
                if (startIndex >= stream.GetRawList().Count) { stream.Consume(); continue; }
                int length = ItemDomainRules.GetItemBlockLength(stream.GetRawList(), startIndex);

                // FIX: Track depth accurately by counting parentheses 
                int depth = 0;
                for (int k = 0; k < length; k++)
                {
                    string peek = stream.GetRawList()[startIndex + k].ToLower();
                    depth += peek.Count(c => c == '(') - peek.Count(c => c == ')');

                    if (depth <= 0 && EntityDomainRules.CommonMetadataKeys.Contains(peek))
                    {
                        length = k;
                        break;
                    }
                }

                if (length > 0)
                {
                    stream.Consume(); // 'i'
                    List<string> subTokens = stream.ConsumeRange(length);
                    string itemString = string.Join(".", subTokens);
                    ItemData parsedItem = new ItemData();
                    parsedItem.Parse(StaticBranchTracing.StripOuterParens(itemString));

                    bool isItemParsedDataEmpty = string.IsNullOrEmpty(parsedItem.entityName) && parsedItem.Mechanics.Count == 0 && parsedItem.LearnedAbilities.Count == 0 && parsedItem.Containers.Count == 0 && !parsedItem.Tier.HasValue && string.IsNullOrEmpty(parsedItem.doc) && string.IsNullOrEmpty(parsedItem.imageOverride);
                    if (isItemParsedDataEmpty) parsedItem.entityName = itemString;

                    itemPipeline.Add(parsedItem);
                    continue;
                }
            }
            if (TryProcessModifierData(stream)) continue;

            string droppedToken = stream.Consume();
            UnityEngine.Debug.LogError($"[EntityData Parser ERROR] Unrecognized string chunk discarded! Token '{droppedToken}' did not match any valid property, metadata, item, or modifier. Entity: {entityName ?? "Unknown"}");
        }
    }




    public override string Export()
    {
        HeroData hero = this as HeroData;
        MonsterData monster = this as MonsterData;
        bool isHero = hero != null;

        if (!isHero) SyncMonsterContainerBaseIdentifier(monster);

        string baseId = isHero ? hero.baseReplica : monster.baseMonster;
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                !string.Equals(imageOverride, "None", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(imageOverride, baseId, StringComparison.OrdinalIgnoreCase);

        string coreBody = BuildCoreBody(hero, monster, isHero, baseId, hasImageOverride);
        string trailingPayloads = BuildTrailingPayloads(hero, monster, isHero, out var wrapperPayloads);

        string combined = $"{coreBody}{trailingPayloads}";

        return ApplyWrappersAndOuterBracketing(combined, wrapperPayloads);
    }
    private string BuildCoreBody(HeroData hero, MonsterData monster, bool isHero, string baseId, bool hasImageOverride)
    {
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(baseId))
        {
            if (isHero) sb.Append($"replica.{FormatName(FormatSpecialImageName(baseId))}");
            else sb.Append(FormatName(FormatSpecialImageName(baseId)));
            if (!hasImageOverride) AppendColorModifier(sb);
        }

        if (!string.IsNullOrEmpty(entityName) && (isHero || !string.Equals(entityName, baseId, StringComparison.OrdinalIgnoreCase)))
            sb.Append($".n.{FormatName(entityName)}");

        if (isHero && !string.IsNullOrEmpty(hero.colorClass)) sb.Append($".col.{hero.colorClass}");
        if (hp > 0) sb.Append($".hp.{hp}");
        if (isHero && hero.tier >= 0) sb.Append($".tier.{hero.tier}");
        if (isHero && hero.adj.HasValue) sb.Append($".adj.{hero.adj.Value}");

        AppendDiceSides(sb);

        if (isHero && !string.IsNullOrEmpty(hero.speech)) sb.Append($".speech.{hero.speech}");

        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);

        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(FormatSpecialImageName(imageOverride))}");
            AppendColorModifier(sb);
        }

        string coreString = sb.ToString();
        if (coreString.StartsWith(".")) coreString = coreString.Substring(1);

        return $"({coreString})";
    }
    private string BuildTrailingPayloads(HeroData hero, MonsterData monster, bool isHero, out List<string> wrapperPayloads)
    {
        StringBuilder outerSb = new StringBuilder();

        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out wrapperPayloads);

        string traitPrefix = isHero ? ".i.t." : ".t.";
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) outerSb.Append($"{traitPrefix}{FormatName(t)}");
        if (!isHero && monster.customOrbs != null) foreach (var orb in monster.customOrbs) if (orb != null) outerSb.Append($".{orb.ExportAsTrait(useITPrefix: false)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) outerSb.Append($".i.{FormatName(i)}");
        if (isHero && blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) outerSb.Append($".gift.{FormatName(bl)}");

        foreach (var inner in innerPayloads) outerSb.Append($".{inner}");

        if (isHero && hero.baseAbilityData != null)
            foreach (var ab in hero.baseAbilityData) if (!string.IsNullOrEmpty(ab)) outerSb.Append($".i.learn.{FormatName(ab)}");

        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            foreach (var cab in customAbilityData)
            {
                if (cab == null) continue;
                if (cab is TriggerHPData) outerSb.Append($".triggerhpdata.{cab.Export()}");
                else if (cab is OnHitData) outerSb.Append($".i.onhitdata.{cab.Export()}");
                else if (cab is OrbData orb) outerSb.Append($".{orb.ExportAsTrait(useITPrefix: true)}");
                else outerSb.Append($".abilitydata.{cab.Export()}");
            }
        }

        foreach (var outer in outerPayloads) outerSb.Append($".{outer}");

        string jinxPrefix = isHero ? ".i.t.jinx." : ".t.jinx.";
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) outerSb.Append($"{jinxPrefix}{FormatName(c)}");

        if (!string.IsNullOrEmpty(doc)) outerSb.Append($".doc.{doc}");
        if (isHero && !string.IsNullOrEmpty(hero.appendedDoc)) outerSb.Append($".i.self.Wolf.doc.{hero.appendedDoc}.spirit");
        if (!isHero && !string.IsNullOrEmpty(monster.bal)) outerSb.Append($".bal.{FormatName(monster.bal)}");

        return outerSb.ToString();
    }
    private string ApplyWrappersAndOuterBracketing(string result, List<string> wrapperPayloads, bool hasOuterExtensions)
    {
        if (wrapperPayloads != null)
        {
            foreach (var wrapper in wrapperPayloads)
            {
                result = wrapper.Contains("{0}") ? string.Format(wrapper, result) : $"({result}.{wrapper})";
            }
        }

        if (hasOuterExtensions || isOuterWrappedInParens)
        {
            result = $"({result})";
        }

        return result;
    }
    private void SyncMonsterContainerBaseIdentifier(MonsterData monster)
    {
        if (monster == null || monster.payloadData == null) return;

        string prefix = monster.baseMonster;
        int parenIdx = prefix.IndexOf('(');
        if (parenIdx > 0)
        {
            prefix = prefix.Substring(0, parenIdx).TrimEnd('.');
        }
        else
        {
            int dotIdx = prefix.IndexOf('.');
            if (dotIdx > 0) prefix = prefix.Substring(0, dotIdx);
        }

        if (monster.payloadData is AbilityData abPayload)
        {
            monster.baseMonster = $"{prefix}.({abPayload.Export()})";
        }
        else if (monster.payloadData is SDData sdPayload)
        {
            monster.baseMonster = $"{prefix}.({sdPayload.Export()})";
        }
        else if (monster.payloadData is ModifierData modPayload)
        {
            monster.baseMonster = $"{prefix}.({modPayload.Export()})";
        }
    }

    private string ApplyWrappersAndOuterBracketing(string result, List<string> wrapperPayloads)
    {
        if (wrapperPayloads != null)
        {
            foreach (var wrapper in wrapperPayloads)
            {
                result = wrapper.Contains("{0}") ? string.Format(wrapper, result) : $"({result}.{wrapper})";
            }
        }

        if (isOuterWrappedInParens)
        {
            result = $"({result})";
        }

        return result;
    }
    private bool IsImageOverridePresent(string baseId)
    {
        return !string.IsNullOrEmpty(imageOverride) &&
               !string.Equals(imageOverride, "None", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(imageOverride, baseId, StringComparison.OrdinalIgnoreCase);
    }
    private string BuildVisualOverrides(bool hasImageOverride)
    {
        if (!hasImageOverride) return string.Empty;

        StringBuilder visSb = new StringBuilder();
        visSb.Append($".img.{FormatName(FormatSpecialImageName(imageOverride))}");
        AppendColorModifier(visSb);
        return visSb.ToString();
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

    ////////////////////////////////
}