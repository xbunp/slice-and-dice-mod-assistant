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
        "i", "t", "gift", "learn", "abilitydata", "triggerhpdata", "onhitdata", "orb"
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
                endIndex++;

                // Fix for dot-separated identifiers to ensure trailing tokens are grouped together natively
                if (peek == "rmon" || peek == "rmod" || peek == "ritem" || peek == "ritemx" || peek == "rditem")
                {
                    if (endIndex < tokens.Count && !tokens[endIndex].StartsWith("("))
                        endIndex++;
                }

                continue;
            }
            break;
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
                customPayloads.Add(new CustomPayload { Prefix = "t", Data = nestedMod, Type = PayloadType.Modifier });
            }
            else
            {
                traits.Add(trimmed);
            }
        }
    }

    // Unifies lookahead trait parsing (supports both strings and nested custom modifiers)
    // Notice we dropped the Hashset parameter entirely!

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
        var indexedPipeline = pipeline.Select((item, index) => new { Item = item, OriginalIndex = index }).ToList();
        var sortedPipeline = indexedPipeline.OrderBy(x => GetItemPriority(x.Item)).ToList();
        int minRupturedOriginalIndex = int.MaxValue;

        foreach (var entry in sortedPipeline)
        {
            var item = entry.Item;
            int origIdx = entry.OriginalIndex;

            bool isDiceAffecting = IsDiceAffectingItem(item);
            bool forceCustomPayload = (origIdx > minRupturedOriginalIndex) && isDiceAffecting;

            HydrateEntityFromItem(item, forceCustomPayload);

            if (isDiceAffecting && customPayloads.Count > 0 && customPayloads.Last().Data == item)
            {
                if (origIdx < minRupturedOriginalIndex)
                {
                    minRupturedOriginalIndex = origIdx;
                }
            }
        }
    }
    private void HydrateEntityFromItem(ItemData item, bool forceCustomPayload = false)
    {
        bool canMapNatively = false;
        if (!forceCustomPayload)
        {
            canMapNatively = item.TryAbsorbIntoEntity(this);
        }
        if (!canMapNatively)
        {
            customPayloads.Add(new CustomPayload { Prefix = "i", Data = item, Type = PayloadType.Item });
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

    protected string ExtractBaseIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return token;
        while (token.StartsWith("(") && token.EndsWith(")"))
        {
            string stripped = StaticBranchTracing.StripOuterParens(token);
            if (stripped == token) break; // PREVENT INFINITE LOOP: string is balanced recursively but not globally enclosed
            token = stripped;
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
            string nextToken = stream.PeekNext();
            if (!string.IsNullOrEmpty(nextToken))
            {
                string stripped = StaticBranchTracing.StripOuterParens(nextToken);

                int wolfDocIdx = stripped.IndexOf("Wolf.doc.", StringComparison.OrdinalIgnoreCase);
                int spiritIdx = stripped.LastIndexOf("spirit", StringComparison.OrdinalIgnoreCase);

                // Dynamically detect the presence of self., Wolf.doc., and spirit anywhere in the token
                if (stripped.StartsWith("self.", StringComparison.OrdinalIgnoreCase) && wolfDocIdx != -1 && spiritIdx > wolfDocIdx)
                {
                    int start = wolfDocIdx + 9; // "Wolf.doc.".Length
                    string rawDoc = stripped.Substring(start, spiritIdx - start);

                    // Cleanly slice off trailing/leading delimiters (, ), ., space
                    appendedDoc = rawDoc.TrimEnd('.', ')', '(', ' ');

                    stream.Consume(); // Consume 'i'
                    stream.Consume(); // Consume the doc token
                    return true;
                }
            }

            // Fallback for legacy flat token sequence support
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
                if (targetToken == "t") ProcessTraitPayload(payload);
                else if (targetToken == "gift") blessings.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                else baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
                if (targetToken == "gift") blessings = blessings.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                else if (targetToken == "learn") baseAbilityData = baseAbilityData.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
                if (payload.StartsWith("("))
                {
                    AddCustomAbility(AbilityData.CreateAbility($"{targetToken}.{payload}"));
                }
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
            string rawKw = kw.Trim();
            string cleanKw = rawKw.ToLower();

            if (cleanKw != "permissive" && cleanKw != "stasis")
            {
                if (cleanKw == "future")
                {
                    chunks.Add("ritemx.dae9");
                }
                else if (ItemDomainRules.TogItems.Contains(cleanKw))
                {
                    chunks.Add(rawKw);
                }
                else if (ExternalGameRegistry.IsValidKeyword(rawKw))
                {
                    // STRICT RULE: Only attach 'k.' if the game engine actually considers it a keyword
                    chunks.Add($"k.{cleanKw}"); // Native keywords are traditionally lowercase
                }
                else if (ExternalGameRegistry.IsValidItemName(rawKw))
                {
                    // STRICT RULE: It is a base item (like Fly). Preserve casing, do not attach 'k.'
                    chunks.Add(rawKw);
                }
                else
                {
                    // Fallback to exactly what the user authored without assuming anything
                    chunks.Add(rawKw);
                }
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
                string rawKw = chainKw.Trim(); // PRESERVE CASING
                if (rawKw.StartsWith("k.", StringComparison.OrdinalIgnoreCase))
                {
                    rawKw = rawKw.Substring(2);
                }

                // Add if it doesn't already exist (case-insensitive check, but preserve original case)
                if (!diceSides[faceIdx].keywords.Any(k => string.Equals(k, rawKw, StringComparison.OrdinalIgnoreCase)))
                {
                    diceSides[faceIdx].keywords.Add(rawKw);
                }
            }

            if (lowerPrefix == "k" || lowerPrefix == "")
            {
                string rawKw = payload; // PRESERVE CASING
                if (string.Equals(rawKw, "ritemx.dae9", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(rawKw, "unpack.ritemx.644f", StringComparison.OrdinalIgnoreCase))
                {
                    rawKw = "future";
                }

                if (!string.IsNullOrEmpty(rawKw) && !diceSides[faceIdx].keywords.Any(k => string.Equals(k, rawKw, StringComparison.OrdinalIgnoreCase)))
                {
                    diceSides[faceIdx].keywords.Add(rawKw);
                }
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
            else if (lowerPrefix == "sidesc")
            {
                diceSides[faceIdx].sidesc = payload;
            }
            else if (lowerPrefix == "sticker" || lowerPrefix == "cast" || lowerPrefix == "enchant" || lowerPrefix == "hat")
            {
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

    // Update BuildFaceModifiers & ProcessIntrinsicFaceKeywords in Assets/Scripts/Data/Entities/EntityData.cs

    public string BuildFaceModifiers(bool includeInlineFacades)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        // TRACKING FOR LEGACY ITEMS: Track the ItemData reference to prevent duplicates across multiple faces
        HashSet<ItemData> processedSideItems = new HashSet<ItemData>();
        List<ItemData> itemsToExport = new List<ItemData>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            if (face == null) continue;
            List<string> chunks = new List<string>();

            bool hasHatWrapper = ProcessFacePayload(face, chunks);

            // Process native properties, decoupled entirely from Legacy Side Items
            ProcessIntrinsicFaceKeywords(face, chunks);
            ProcessFaceFacades(face, chunks, includeInlineFacades);
            ProcessFaceDescription(face, chunks);
            ProcessFaceStasis(face, chunks);
            /*
            if (chunks.Count > 0)
            {
                string templateString = string.Join("#", chunks);
                bool isLeftFaceHat = i == 0 && hasHatWrapper;
                bool isEggOnOuterFace = face.faceType == DiceSideData.DiceFaceType.Egg && (i == 2 || i == 3 || i == 5);

                if (isLeftFaceHat || isEggOnOuterFace)
                {
                    string resolvedMod = templateString.Contains("{0}") ? string.Format(templateString, "mid") : templateString;
                    string faceAlias = i == 0 ? "left" : (i == 2 ? "top" : (i == 3 ? "bot" : "rightmost"));
                    ItemData giveItem = new ItemData();
                    giveItem.Parse($"{faceAlias}.mid.{resolvedMod}");
                    ModifierData modifier = new ModifierData { ActionType = ModifierActionType.GiveItem, ItemPayload = giveItem };
                    modSb.Append($".{modifier.Export()}");
                }
                else
                {
                    int faceMask = 1 << i;
                    if (groupedModifiers.ContainsKey(templateString)) groupedModifiers[templateString] |= faceMask;
                    else groupedModifiers[templateString] = faceMask;
                }
            }
            */

            if (chunks.Count > 0)
            {
                // Unify hat/egg payloads by assigning the 'mid' positional scope directly into the template string.
                // This allows them to group perfectly in the bitmask dictionary.
                if (hasHatWrapper)
                {
                    chunks[0] = "mid." + chunks[0];
                }

                string templateString = string.Join("#", chunks);

                int faceMask = 1 << i;
                if (groupedModifiers.ContainsKey(templateString)) groupedModifiers[templateString] |= faceMask;
                else groupedModifiers[templateString] = faceMask;
            }

            // GATHER STRUCTURAL INTACT SIDE ITEMS FROM THE AST BRIDGE
            if (face.sideMechanics != null)
            {
                foreach (var m in face.sideMechanics)
                {
                    if (m.LegacyItemPayload != null && processedSideItems.Add(m.LegacyItemPayload))
                    {
                        itemsToExport.Add(m.LegacyItemPayload);
                    }
                }
            }
        }

        // 1. Export inherent grouped string properties
        foreach (var kvp in groupedModifiers)
        {
            string templateString = kvp.Key;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(kvp.Value);
            foreach (string alias in optimalAliases)
            {
                string resolvedMod = templateString.Contains("{0}") ? string.Format(templateString, alias) : templateString;
                ItemData giveItem = new ItemData();
                giveItem.Parse($"{alias}.{resolvedMod}");
                ModifierData modifier = new ModifierData { ActionType = ModifierActionType.GiveItem, ItemPayload = giveItem };
                modSb.Append($".{modifier.Export()}");
            }
        }

        // 2. Export bridged structural objects (Legacy Items)
        foreach (var item in itemsToExport)
        {
            ModifierData modifier = new ModifierData { ActionType = ModifierActionType.GiveItem, ItemPayload = item };
            modSb.Append($".{modifier.Export()}");
        }

        return modSb.ToString();
    }
    private void ProcessIntrinsicFaceKeywords(DiceSideData face, List<string> chunks)
    {
        var flatKeywords = new List<string>();

        foreach (var m in face.sideMechanics)
        {
            if (m.LegacyItemPayload != null) continue; // BYPASS LEGACY VEIL

            // Skip primary payloads and handled properties
            if (m.Prefix == "facade" || m.Prefix == "sidesc" || m.Prefix == "sticker" || m.Prefix == "cast" || m.Prefix == "enchant" || m.Prefix == "hat")
                continue;

            // Collect exploded flat nodes
            if (!string.IsNullOrEmpty(m.RawPayloadString) &&
                !string.Equals(m.RawPayloadString, "togtime", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(m.RawPayloadString, "stasis", StringComparison.OrdinalIgnoreCase))
            {
                if (m.Prefix == "k") flatKeywords.Add($"k.{m.RawPayloadString}");
                else flatKeywords.Add(m.RawPayloadString);
            }

            // Just in case any ChainedKeywords remained unexploded
            foreach (var kw in m.ChainedKeywords)
            {
                if (!string.Equals(kw, "togtime", StringComparison.OrdinalIgnoreCase) && !string.Equals(kw, "stasis", StringComparison.OrdinalIgnoreCase))
                {
                    flatKeywords.Add(kw);
                }
            }
        }

        if (flatKeywords.Any(kw => kw.Trim().Equals("permissive", StringComparison.OrdinalIgnoreCase) || kw.Trim().Equals("k.permissive", StringComparison.OrdinalIgnoreCase)))
        {
            chunks.Add("k.permissive");
        }

        foreach (var kw in flatKeywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;

            string rawKw = kw.Trim();
            string cleanKw = rawKw.ToLower();

            if (cleanKw == "permissive" || cleanKw == "k.permissive" || cleanKw == "stasis" || cleanKw == "k.stasis") continue;

            if (cleanKw == "future" || cleanKw == "k.future")
            {
                chunks.Add("ritemx.dae9");
            }
            else if (rawKw.StartsWith("k.", StringComparison.OrdinalIgnoreCase))
            {
                chunks.Add(rawKw.ToLower()); // Already formatted
            }
            else if (ExternalGameRegistry.IsValidKeyword(rawKw))
            {
                chunks.Add($"k.{cleanKw}");
            }
            else
            {
                chunks.Add(rawKw);
            }
        }
    }

    private void ProcessFaceStasis(DiceSideData face, List<string> chunks)
    {
        // Must check only flat mechanics to avoid duplicating stasis from legacy items
        bool hasFlatStasis = face.sideMechanics.Any(m =>
            m.LegacyItemPayload == null &&
            (
                string.Equals(m.RawPayloadString, "stasis", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.RawPayloadString, "k.stasis", StringComparison.OrdinalIgnoreCase) ||
                m.ChainedKeywords.Any(k => string.Equals(k, "stasis", StringComparison.OrdinalIgnoreCase) || string.Equals(k, "k.stasis", StringComparison.OrdinalIgnoreCase))
            )
        );

        // MUST BE LAST: Stasis keyword terminates effects that follow it
        if (hasFlatStasis)
        {
            chunks.Add("k.stasis");
        }
    }
    private bool ProcessFacePayload(DiceSideData face, List<string> chunks)
    {
        // Strictly only check flat AST mechanics, ignoring legacy payloads
        var m = face.sideMechanics.FirstOrDefault(x =>
            x.Prefix == "sticker" || x.Prefix == "cast" || x.Prefix == "enchant" || x.Prefix == "hat"
        );

        if (m == null || string.IsNullOrWhiteSpace(m.RawPayloadString))
            return false;

        string payloadStr = m.RawPayloadString.Trim();
        DiceSideData.DiceFaceType fType = m.Prefix switch
        {
            "hat" => DiceSideData.DiceFaceType.Egg,
            "cast" => DiceSideData.DiceFaceType.Cast,
            "enchant" => DiceSideData.DiceFaceType.Enchant,
            _ => DiceSideData.DiceFaceType.Sticker
        };

        // Branch out for specialized parsing
        if (fType == DiceSideData.DiceFaceType.Egg)
        {
            // Egg format is "egg.Wolf". Strip the prefix for the processor.
            string cleanSummon = payloadStr.StartsWith("egg.", StringComparison.OrdinalIgnoreCase)
                ? payloadStr.Substring(4)
                : payloadStr;
            return ProcessEggPayload(face, cleanSummon, chunks);
        }
        else
        {
            return ProcessStandardPayload(face, payloadStr, fType, m.PayloadTargetOverride, chunks);
        }
    }
    private bool ProcessStandardPayload(DiceSideData face, string payloadStr, DiceSideData.DiceFaceType faceType, DiceSideData.PayloadTarget? payloadTarget, List<string> chunks)
    {
        // If payloadStr is complex, ItemData self-brackets on export.
        string prefix = faceType.ToString().ToLower();
        bool applyStickerRules = faceType == DiceSideData.DiceFaceType.Sticker;

        // If the face is an Enchant with a target override, divert it to sticker format
        if (faceType == DiceSideData.DiceFaceType.Enchant && payloadTarget.HasValue)
        {
            prefix = "sticker";
            payloadStr = $"(self.{payloadStr})";
            applyStickerRules = true;
        }

        string innerPayloadStr = $"{prefix}.{payloadStr}";
        string hatWrapperFmt = null;

        // Use the safe property to check if togtime exists ANYWHERE on this face
        bool hasTogtime = face.togtime;

        if (applyStickerRules)
        {
            if (hasTogtime)
                innerPayloadStr += "#togtime";

            if (payloadTarget.HasValue)
            {
                switch (payloadTarget.Value)
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
            if (hasTogtime && faceType == DiceSideData.DiceFaceType.Enchant)
                innerPayloadStr += "#togtime";

            chunks.Add(innerPayloadStr);
        }

        if (hatWrapperFmt != null)
        {
            chunks.Add($"hat.({hatWrapperFmt})");
            return true;
        }

        return false;
    }
    private void ProcessFaceFacades(DiceSideData face, List<string> chunks, bool includeInlineFacades)
    {
        if (!includeInlineFacades) return;

        var m = face.sideMechanics.FirstOrDefault(x => x.Prefix == "facade");
        if (m == null || string.IsNullOrWhiteSpace(m.RawPayloadString)) return;

        string[] parts = m.RawPayloadString.Split(':');
        string fId = parts[0].Trim();

        string colorStr = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : "";
        bool hasColor = !string.IsNullOrEmpty(colorStr) && colorStr != "0" && colorStr != "0:0" && colorStr != "0:0:0";

        // DO NOT EXPORT EMPTY FACADES WITH NO ID AND NO COLOR!
        if (string.IsNullOrEmpty(fId) && !hasColor) return;

        string facStr = $"facade.{fId}";

        if (parts.Length > 1)
        {
            List<string> cParts = new List<string>();
            for (int pIdx = 1; pIdx < parts.Length; pIdx++)
                cParts.Add(string.IsNullOrWhiteSpace(parts[pIdx]) ? "0" : parts[pIdx].Trim());

            while (cParts.Count < 3) cParts.Add("0");

            if (cParts[0] == "0" && cParts[1] == "0" && cParts[2] == "0")
                facStr += ":0";
            else
                facStr += $":{cParts[0]}:{cParts[1]}:{cParts[2]}";
        }
        else
        {
            facStr += ":0";
        }

        chunks.Add(facStr);
    }
    private void ProcessFaceDescription(DiceSideData face, List<string> chunks)
    {
        var m = face.sideMechanics.FirstOrDefault(x => x.Prefix == "sidesc");
        if (m != null && !string.IsNullOrEmpty(m.RawPayloadString))
        {
            chunks.Add($"sidesc.{m.RawPayloadString}");
        }
    }

    private bool ProcessEggPayload(DiceSideData face, string payloadStr, List<string> chunks)
    {
        bool hasBlindfold = payloadStr.EndsWith("#blindfold", StringComparison.OrdinalIgnoreCase);
        string cleanSummon = hasBlindfold ? payloadStr.Substring(0, payloadStr.Length - 10) : payloadStr;
        string fullSummonExport = cleanSummon;

        if (ModPackage.Instance != null)
        {
            string searchName = cleanSummon;

            // If cleanSummon is a full raw string like "(replica.Statue.n.Smeagol...)", parse out its entity name safely
            if (cleanSummon.StartsWith("("))
            {
                HeroData tempHero = new HeroData();
                tempHero.SuppressAutoRegister = true;
                tempHero.Parse(cleanSummon);
                if (!string.IsNullOrEmpty(tempHero.entityName))
                {
                    searchName = tempHero.entityName;
                }
            }

            // Search ModPackage for an existing Hero or Monster matching that entity name
            var summonHero = ModPackage.Instance.Heroes?.FirstOrDefault(
                h => string.Equals(h.entityName, searchName, StringComparison.OrdinalIgnoreCase));

            if (summonHero != null)
            {
                fullSummonExport = summonHero.Export(); // <-- FIX: Fetch full Export() string instead of just entityName!
            }
            else
            {
                var summonMonster = ModPackage.Instance.Monsters?.FirstOrDefault(
                    m => string.Equals(m.entityName, searchName, StringComparison.OrdinalIgnoreCase));

                if (summonMonster != null)
                {
                    fullSummonExport = summonMonster.Export(); // <-- FIX: Fetch full Export() string instead of just entityName!
                }
            }
        }

        MonsterData eggMonster = new MonsterData();
        eggMonster.baseMonster = $"egg.{fullSummonExport}";

        if (face.pips >= 2 && face.pips <= 9)
        {
            eggMonster.xMultiplier = face.pips;
        }

        chunks.Add($"hat.{eggMonster.Export()}");

        if (hasBlindfold)
        {
            chunks.Add("blindfold");
        }

        return true;
    }
    private bool ProcessStandardPayload(DiceSideData face, string payloadStr, List<string> chunks)
    {
        // If payloadStr is complex, ItemData self-brackets on export.
        string prefix = face.faceType.ToString().ToLower();

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

            // FIX: Check TryProcessModifierData BEFORE generic item processing ("i")
            // so outer modifiers (like i.self, i.jinx, i.vase, t.jinx) are captured as ModifierData
            // instead of being hijacked and fragmented as ItemData!
            if (TryProcessModifierData(stream)) continue;

            if (tokenLower == "i")
            {
                int startIndex = stream.Index + 1;
                if (startIndex >= stream.GetRawList().Count) { stream.Consume(); continue; }
                int length = ItemDomainRules.GetItemBlockLength(stream.GetRawList(), startIndex);
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
                    stream.Consume();
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

            string droppedToken = stream.Consume();
            UnityEngine.Debug.LogError($"[EntityData Parser ERROR] Unrecognized string chunk discarded! Token '{droppedToken}' did not match any valid property, metadata, item, or modifier. Entity: {entityName ?? "Unknown"}");
        }
    }
    protected override string ExportCore()
    {
        HeroData hero = this as HeroData;
        MonsterData monster = this as MonsterData;
        bool isHero = hero != null;
        if (!isHero) SyncMonsterContainerBaseIdentifier(monster);
        string baseId = isHero ? hero.baseReplica : monster.baseMonster;
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                !string.Equals(imageOverride, "None", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(imageOverride, baseId, StringComparison.OrdinalIgnoreCase);
        // 1. Setup injection scopes
        List<string> innerHeroPayloads = new List<string>();
        List<string> lateInnerPayloads = new List<string>();
        List<string> outerPayloads = new List<string>();
        List<string> wrapperPayloads = new List<string>();
        // 2. Process Custom Payloads
        if (customPayloads != null)
        {
            foreach (var payload in customPayloads)
            {
                if (payload.Type == PayloadType.Item && payload.Data is ItemData itemData)
                {
                    RouteItemPayload(itemData, isHero, innerHeroPayloads, outerPayloads);
                }
                else if (payload.Data is OnHitData || payload.Data is TriggerHPData || payload.Data is AbilityData || payload.Type == PayloadType.Modifier)
                {
                    string exported = payload.Export();
                    if (!string.IsNullOrEmpty(exported)) outerPayloads.Add(exported);
                }
                else
                {
                    string exported = payload.Export();
                    if (!string.IsNullOrEmpty(exported)) innerHeroPayloads.Add(exported);
                }
            }
        }
        // 3. Custom Ability Data
        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            foreach (var cab in customAbilityData)
            {
                if (cab == null) continue;
                if (cab is SpellData || cab is TacticData)
                {
                    lateInnerPayloads.Add(AbilityData.GetFormattedExportString(cab));
                }
                else if (cab is OrbData orb)
                {
                    outerPayloads.Add(orb.ExportAsTrait(useITPrefix: true));
                }
                else
                {
                    string prefix = cab is TriggerHPData ? "triggerhpdata" :
                                    cab is OnHitData ? "onhitdata" : "abilitydata";
                    ItemData abilityItem = new ItemData();
                    abilityItem.Mechanics.Add(new ItemMechanic { Prefix = prefix, PayloadData = cab });
                    RouteItemPayload(abilityItem, isHero, innerHeroPayloads, outerPayloads);
                }
            }
        }
        // 4. Stock Items
        if (items != null)
        {
            foreach (var itm in items)
            {
                if (!string.IsNullOrEmpty(itm))
                {
                    ItemData parsedItem = new ItemData();
                    parsedItem.Parse(itm);
                    // Fallback to preserve vanilla item syntax instead of coercing to n.Item
                    if (parsedItem.Mechanics.Count == 0 && !string.IsNullOrEmpty(parsedItem.entityName))
                    {
                        parsedItem = new ItemData();
                        parsedItem.Mechanics.Add(new ItemMechanic { Prefix = "", PayloadString = itm });
                    }
                    RouteItemPayload(parsedItem, isHero, innerHeroPayloads, outerPayloads);
                }
            }
        }
        // 5. Blessings
        if (isHero && blessings != null)
        {
            foreach (var bl in blessings)
            {
                if (!string.IsNullOrEmpty(bl))
                {
                    ModifierData blessMod = new ModifierData(); blessMod.Parse(bl);
                    string exported = blessMod.ExportInternal(false);
                    if (!string.IsNullOrEmpty(exported))
                    {
                        if (!exported.StartsWith("(")) exported = $"({exported})";
                        outerPayloads.Add($"gift.{exported}");
                    }
                }
            }
        }
        // 6. Base Abilities (Learn)
        if (isHero && hero.baseAbilityData != null)
        {
            foreach (var ab in hero.baseAbilityData)
            {
                if (!string.IsNullOrEmpty(ab))
                {
                    AbilityData abilityPayload = AbilityData.CreateAbility(ab);
                    ItemData learnItem = new ItemData();
                    learnItem.Mechanics.Add(new ItemMechanic { Prefix = "learn", PayloadData = abilityPayload });
                    RouteItemPayload(learnItem, isHero, innerHeroPayloads, outerPayloads);
                }
            }
        }
        // 7. Traits
        if (traits != null)
        {
            foreach (var t in traits)
            {
                if (!string.IsNullOrEmpty(t))
                {
                    MonsterData traitMonster = new MonsterData();
                    traitMonster.Parse(t);
                    ItemData traitItem = new ItemData();
                    traitItem.Mechanics.Add(new ItemMechanic { Prefix = "t", PayloadData = traitMonster });
                    RouteItemPayload(traitItem, isHero, innerHeroPayloads, outerPayloads);
                }
            }
        }
        // 9. Curses
        if (curses != null)
        {
            foreach (var c in curses)
            {
                if (!string.IsNullOrEmpty(c))
                {
                    ModifierData curseMod = new ModifierData();
                    curseMod.Parse(c);
                    ModifierData jinxMod = new ModifierData { ActionType = ModifierActionType.Jinx, NestedModifierPayload = curseMod };
                    ItemData traitItem = new ItemData();
                    traitItem.Mechanics.Add(new ItemMechanic { Prefix = "t", PayloadData = jinxMod });
                    RouteItemPayload(traitItem, isHero, innerHeroPayloads, outerPayloads);
                }
            }
        }
        // 10. Appended Doc (Hero)
        if (isHero && !string.IsNullOrEmpty(hero.appendedDoc))
        {
            ModifierData parsedDocMod = new ModifierData(); parsedDocMod.Parse($"self.Wolf.doc.{hero.appendedDoc}.spirit");
            ItemData docItem = new ItemData();
            docItem.Mechanics.Add(new ItemMechanic { Prefix = "", PayloadData = parsedDocMod });
            RouteItemPayload(docItem, isHero, innerHeroPayloads, outerPayloads);
        }
        // --- BUILD CORE BODY ---
        string coreBody = BuildCoreBody(hero, monster, isHero, baseId, hasImageOverride, innerHeroPayloads, lateInnerPayloads);
        // --- BUILD TRAILING ---
        StringBuilder trailingSb = new StringBuilder();
        foreach (var outer in outerPayloads) trailingSb.Append($".{outer}");
        if (!string.IsNullOrEmpty(doc)) trailingSb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(doc2)) trailingSb.Append($".doc.{doc2}");
        string combined = $"{coreBody}{trailingSb.ToString()}";
        bool forceOuterParens = isOuterWrappedInParens || trailingSb.Length > 0;
        return ApplyWrappersAndOuterBracketing(combined, wrapperPayloads, forceOuterParens);
    }
    private string BuildCoreBody(HeroData hero, MonsterData monster, bool isHero, string baseId, bool hasImageOverride, List<string> innerHeroPayloads, List<string> lateInnerPayloads)
    {
        StringBuilder sb = new StringBuilder();
        // 1. Base Identifier
        if (!string.IsNullOrEmpty(baseId))
        {
            if (isHero) sb.Append($"replica.{FormatName(FormatSpecialImageName(baseId))}");
            else sb.Append(FormatName(FormatSpecialImageName(baseId)));
        }
        // 2. Pre-Name Visual Modifiers (Monsters)
        if (!isHero && !hasImageOverride) AppendColorModifier(sb);
        // 3. Name
        if (!string.IsNullOrEmpty(entityName) && (isHero || !string.Equals(entityName, baseId, StringComparison.OrdinalIgnoreCase)))
            sb.Append($".n.{FormatName(entityName)}");
        // 4. Hero Metadata
        if (isHero && !string.IsNullOrEmpty(hero.colorClass) && !IsDefaultHeroColor(hero.baseReplica, hero.colorClass))
            sb.Append($".col.{hero.colorClass}");
        if (hp > 0) sb.Append($".hp.{hp}");
        if (isHero && hero.tier >= 0) sb.Append($".tier.{hero.tier}");
        if (isHero && hero.adj.HasValue) sb.Append($".adj.{hero.adj.Value}");
        // 5. Dice Sides & Speech
        AppendDiceSides(sb);
        if (isHero && !string.IsNullOrEmpty(hero.speech)) sb.Append($".speech.{hero.speech}");
        // 6. Face Modifiers (Inline Arrays)
        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) sb.Append(faceModifiers);
        // 7. --- INJECT INNER PAYLOADS HERE (Before Visual Modifiers) ---
        if (innerHeroPayloads != null)
        {
            foreach (var inner in innerHeroPayloads) sb.Append($".{inner}");
        }
        // 8. Image Override AND Visual Modifiers (MUST BE AT THE VERY END OF CORE BODY)
        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(FormatSpecialImageName(imageOverride))}");
            AppendColorModifier(sb);
        }
        else if (isHero)
        {
            AppendColorModifier(sb);
        }
        // Monster Balance: bal belongs inside the core body
        if (!isHero && monster != null && !string.IsNullOrEmpty(monster.bal))
        {
            sb.Append($".bal.{FormatName(monster.bal)}");
        }
        // 9. Late Inner Payloads (Spells/Tactics placed after visuals to prevent game parser bugs)
        if (lateInnerPayloads != null)
        {
            foreach (var late in lateInnerPayloads) sb.Append($".{late}");
        }
        string coreString = sb.ToString();
        if (coreString.StartsWith(".")) coreString = coreString.Substring(1);
        return $"({coreString})";
    }

    // ====================================================================
    // DOMAIN PAYLOAD ROUTING HELPER
    // ====================================================================
    private void RouteItemPayload(ItemData item, bool isHero, List<string> inner, List<string> outer)
    {
        ModifierData giveItemMod = new ModifierData { ActionType = ModifierActionType.GiveItem, ItemPayload = item };
        string exported = giveItemMod.Export();

        // Hats and dice-affecting items route inside the entity scope for BOTH Heroes and Monsters
        if (IsDiceAffectingItem(item))
        {
            inner.Add(exported);
        }
        else
        {
            outer.Add(exported);
        }
    }
    protected bool IsDiceAffectingItem(ItemData item)
    {
        if (item == null) return false;

        // 1. Check root entity name
        if (item.Mechanics.Count == 0 && !string.IsNullOrEmpty(item.entityName))
        {
            if (BaseItemMetadataRegistry.IsDiceFaceAffecting(item.entityName)) return true;
        }

        // 2. Scan mechanics
        foreach (var mech in item.Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";

            // Explicit face modifier prefixes
            if (pfx == "hat" || pfx == "facade" || pfx == "sticker" || pfx == "k" || pfx == "enchant" || pfx == "cast" || pfx == "sd" || pfx == "sidesc")
                return true;

            // Prefixless items (base items, tog items, etc.)
            if (pfx == "")
            {
                if (!string.IsNullOrEmpty(mech.PayloadString) && BaseItemMetadataRegistry.ExpressionContainsDiceAffectingItem(mech.PayloadString))
                    return true;
            }
        }
        return false;
    }
    private void SyncMonsterContainerBaseIdentifier(MonsterData monster)
    {
        if (monster == null || monster.payloadData == null) return;
        string prefix = monster.baseMonster;

        int parenIdx = prefix.IndexOf('(');
        int dotIdx = prefix.IndexOf('.');

        // Safely extract the container keyword (e.g. "egg", "vase", "jinx", "orb", "rmon") before the payload begins
        if (dotIdx > 0 && (parenIdx == -1 || dotIdx < parenIdx))
        {
            prefix = prefix.Substring(0, dotIdx);
        }
        else if (parenIdx > 0)
        {
            prefix = prefix.Substring(0, parenIdx).TrimEnd('.');
        }

        if (monster.payloadData is AbilityData abPayload)
        {
            monster.baseMonster = $"{prefix}.{abPayload.Export()}";
        }
        else if (monster.payloadData is SDData sdPayload)
        {
            monster.baseMonster = $"{prefix}.{sdPayload.Export()}";
        }
        else if (monster.payloadData is ModifierData modPayload)
        {
            monster.baseMonster = $"{prefix}.{modPayload.ExportInternal(false)}";
        }
    }
    private string ApplyWrappersAndOuterBracketing(string result, List<string> wrapperPayloads, bool forceOuterParens = false)
    {
        if (wrapperPayloads != null)
        {
            foreach (var wrapper in wrapperPayloads)
            {
                result = wrapper.Contains("{0}") ? string.Format(wrapper, result) : $"({result}.{wrapper})";
            }
        }

        if (forceOuterParens || isOuterWrappedInParens)
        {
            result = $"({result})";
        }

        return result;
    }
    public virtual string ExportAsHat()
    {
        HeroData hero = this as HeroData;
        MonsterData monster = this as MonsterData;
        bool isHero = hero != null;
        if (!isHero) SyncMonsterContainerBaseIdentifier(monster);

        string baseId = isHero ? hero.baseReplica : monster.baseMonster;
        StringBuilder heroSb = new StringBuilder();

        // 1. Base Identifier (Hats do not use the "replica." prefix, they state the name directly)
        if (!string.IsNullOrEmpty(baseId))
        {
            heroSb.Append(FormatName(FormatSpecialImageName(baseId)));
        }

        // 2. Dice Sides
        AppendDiceSides(heroSb);

        // 3. Face Modifiers
        string faceModifiers = BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        // 4. Append internal custom item payloads (NO traits or abilities)
        ProcessCustomPayloadsForExport(out var innerPayloads, out var outerPayloads, out var wrapperPayloads);
        StringBuilder innerSb = new StringBuilder();
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) innerSb.Append($".i.{FormatName(i)}");
        foreach (var inner in innerPayloads) if (!string.IsNullOrEmpty(inner)) innerSb.Append($".{inner}");
        foreach (var outer in outerPayloads) if (!string.IsNullOrEmpty(outer)) innerSb.Append($".{outer}");
        heroSb.Append(innerSb.ToString());

        string rawHat = $"({heroSb.ToString()})";

        // Law 3: Safely bracket multipliers into the natively self-bracketed scope
        if (xMultiplier >= 2 && xMultiplier <= 9)
        {
            return $"(x{xMultiplier}.{rawHat.Substring(1)}";
        }

        return rawHat;
    }
    protected bool IsDefaultHeroColor(string baseReplica, string colorClass)
    {
        if (string.IsNullOrEmpty(baseReplica) || string.IsNullOrEmpty(colorClass)) return false;
        string cleanCode = colorClass.Replace("col.", "").Trim();
        if (Enum.TryParse(baseReplica, true, out HeroType heroType))
        {
            if (SDColors.HeroColorMap.TryGetValue(heroType, out var defaultOption))
            {
                string defaultCode = SDColors.GetColorCode(defaultOption);
                return string.Equals(cleanCode, defaultCode, StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }
}