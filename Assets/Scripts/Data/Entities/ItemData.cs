using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Central authority for custom item domain parsing rules, syntax token definitions, and structural grammar constraints.
/// 
/// ============================================================================================
/// SYNTAX GRAMMAR & OPERATOR RULES (THE MASTER SPECIFICATION)
/// ============================================================================================
/// 
/// 1. THE LEFT-TO-RIGHT STATE RULE
///    The custom item syntax is evaluated left-to-right as a stateful token stream. Contextual state 
///    (such as target directions) propagates forward through the chain until explicitly overridden or reset.
/// 
/// 2. THE '#' (AND / CONTEXT PROPAGATION) OPERATOR
///    - PURPOSE: Joins multiple distinct mechanics or keywords under a shared target context.
///    - BEHAVIOR: Represents a semantic "AND" branch. The mechanic directly to the right of '#' inherits the 
///      target context (and scaling properties like pertier) of the mechanic to its left, unless the right 
///      mechanic explicitly defines a new target prefix.
///    - EXAMPLE: "left.k.armoured#k.bloodlust" evaluates both "armoured" and "bloodlust" targeting the left side.
/// 
/// 3. THE '.i.' (INHERENT / BOUNDARY) OPERATOR
///    - PURPOSE: Syntactically acts as a hard boundary and context-reset token in flat chains.
///    - BEHAVIOR: Delimits separate functional items or distinct mechanical blocks. When encountered, 
///      it halts any active payload accumulation (such as reading trailing keywords), resets target 
///      context back to the default (all/none), and starts a fresh mechanic evaluation.
///    - EXAMPLE: "k.bloodlust.i.mid.k.antipair" parses "bloodlust" with default targets, terminates the payload 
///      at ".i.", and parses "antipair" targeting the mid face.
/// 
/// 4. HAT ENCAPSULATION RULE
///    - FORMAT: target.hat.( [EntityData] .i. [Nested Base Items] )
///    - BEHAVIOR: The first ".i." token encountered inside a Hat's outer parentheses serves as the strict 
///      architectural boundary separating the entity's native parameters (e.g., base replica, dice faces, 
///      inline keywords, facades) from the nested Base Item payloads intended for the Hat card's visual Payload Port.
/// 
/// ============================================================================================
/// </summary>

// DO NOT FORGET: CRITICAL
// YOU CANNOT WORK IN THIS CLASS WITHOUT SEEING ItemSyntaxCompiler.CS
// AND ITEMDATA.CS
// AND STRATEGYPATTERNNODES.CS WHICH CONTAINS AuthoringNodeDef!!

public static class ItemDomainRules
{
    public static readonly HashSet<string> ValidItemProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "k", "learn", "hat", "t", "sidepos", "tier", "n", "ritem", "ritemx", "facade",
        "mrg", "self", "m", "doc", "pertier", "part", "rditem", "unpack", "sidesc",
        "splice", "onhitdata", "triggerhpdata", "sticker", "enchant", "cast", "img",
        "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "summon", "cleardesc",
        "clearicon", "oi", "t1", "t2"
    };

    public static readonly HashSet<string> ValidTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "all", "self", "right5", "right3", "right2", "row", "mid2", "col", "topbot",
        "left2", "rightmost", "right", "bot", "top", "mid", "left", "k", "t"
    };

    public static readonly HashSet<string> ContainerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "triggerhpdata", "onhitdata", "learn", "unpack", "splice", "abilitydata",
        "peritem", "allitem", "alliteme", "sticker", "enchant", "cast", "mrg", "hat"
    };

    /// <summary>
    /// Specialized hardcoded structural items that mutate dice faces based on the left face.
    /// FUNCTIONALITY DICTIONARY:
    /// - togtime: Buff duration toggles between 1 turn and indefinite (entire fight) for all sides.
    /// - togtarg: Copies the targeting type (e.g., all, self, specific) from the left side to all sides.
    /// - togfri: Toggles (inverts) friendliness (friend vs foe targeting) for all sides.
    /// - togvis: Copies the visual animation and sometimes sound from the left side to all sides.
    /// - togeft: Copies the base effect from the left side to all sides (does not copy keywords/targeting).
    /// - togpip: Copies the pip count from the left side to all sides. Excellent for adding pips to pipless sides.
    /// - togkey: Copies keywords from the left side to all sides. Can duplicate existing keywords to stack effects.
    /// - togorf: Adds the left side's friendly effect as an optional choice (OR) to other sides targeting enemies.
    /// - togunt: Adds an untargeted effect (mana, revives, ALL targeting) from the left side as a bonus to all sides.
    /// - togres: Copies targeting restrictions (e.g., pristine, engage, cruel) from the left side to all sides.
    /// - togresm: Multiplier variant. Turns a restriction into a "x2 if condition met" bonus multiplier.
    /// - togresa: AND variant. Combines restrictions requiring BOTH to be met.
    /// - togreso: OR variant. Combines restrictions requiring EITHER to be met.
    /// - togresx: XOR variant. Combines restrictions requiring EXACTLY ONE to be met.
    /// - togress: SWAP variant. Swaps "I" and "Target" in the conditional restriction (e.g., swapcruel).
    /// - togresn: NOT variant. Inverts the restriction, requiring the condition to NOT be met.
    /// </summary>
    public static readonly HashSet<string> TogItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "togtime", "togtarg", "togfri", "togvis", "togeft",
        "togpip", "togkey", "togorf", "togunt",
        "togres", "togresm", "togresa", "togreso", "togresx", "togress", "togresn"
    };

    public static bool IsItemIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (token.StartsWith("ritem", StringComparison.OrdinalIgnoreCase)) return true;
        if (TogItems.Contains(token)) return true;
        return ExternalGameRegistry.IsValidItemName(token);
    }

    /// <summary>
    /// Core prefixes that signify a mechanical operation requiring a payload. 
    /// PAYLOAD TYPES & FUNCTIONALITY:
    /// - i: Inherent modifier. Applies following parameters/items directly to the defined targets.
    /// - sd: Dice face definition. Assigns hardcoded effect/pip values to faces.
    /// - k: Keyword applicator. Applies a keyword to the defined dice faces.
    /// - t: Trait applicator. Grants passive entity traits (e.g., t.jinx) to the holder.
    /// - sticker: Swaps a dice face for an item-applying effect. Payload can be a full nested ItemData string.
    /// - enchant: Swaps a dice face for a modifier-applying effect. Payload is a ModifierData string.
    /// - cast: Swaps a dice face for a spell/tactic. Payload is an AbilityData string.
    /// - hat: Replaces dice sides with an entity's dice. Payload is a full nested EntityData string (Heroes/Monsters).
    /// - onhitdata: Triggers an effect (based on left face) when damaged. Payload is a full EntityData string.
    /// - triggerhpdata: Triggers an untargeted effect per X HP lost. Payload is a full EntityData string.
    /// </summary>
    public static readonly HashSet<string> MechanicPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "sd", "k", "t",
        "sticker", "enchant", "cast",
        "hat", "onhitdata", "triggerhpdata",
        "facade", "sidesc"
    };

    public static readonly HashSet<string> RootMetadataProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "n", "tier", "img", "doc", "hsv", "hue", "thue", "p", "b", "draw", "rect", "cleardesc", "clearicon", "learn"
    };

    /// <summary>
    /// Evaluates if a token represents a strict architectural boundary that should 
    /// terminate payload accumulation (preventing greedy token swallowing).
    /// </summary>
    public static bool IsMechanicBoundary(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        string clean = token.ToLower();
        // 1. Hard Context Shifts (Indicates the start of an entirely new bounded payload context)
        if (clean == "hat" || clean == "sticker" || clean == "enchant" || clean == "cast" ||
            clean == "onhitdata" || clean == "triggerhpdata" || clean == "i" ||
            clean == "facade" || clean == "sidesc")
        {
            return true;
        }
        // 2. Root Metadata Intercept (Prevents internal mechanics from swallowing structural root properties)
        if (RootMetadataProperties.Contains(clean))
        {
            return true;
        }
        return false;
    }
    public static bool IsRepeatPrefix(string token, out int count)
    {
        count = 1;
        if (string.IsNullOrEmpty(token) || char.ToLower(token[0]) != 'x') return false;
        return int.TryParse(token.Substring(1), out count);
    }
    public static int GetItemBlockLength(List<string> tokens, int startIndex)
    {
        int endIndex = startIndex;
        int parenDepth = 0;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            if (peek.StartsWith("(")) parenDepth++;
            if (peek.EndsWith(")")) parenDepth--;

            if (parenDepth > 0)
            {
                endIndex++;
                continue;
            }

            if (AbilityDomainRules.IsAbilityStartSequence(tokens, endIndex))
            {
                endIndex += AbilityDomainRules.GetAbilityBlockLength(tokens, endIndex);
                continue;
            }

            // Lookahead: Entity Modifiers (self, jinx, vase) shouldn't be swallowed by flat items
            if (peek == "self" || peek == "jinx" || peek == "vase")
            {
                if (endIndex + 1 < tokens.Count)
                {
                    string next = tokens[endIndex + 1].ToLower();
                    if (next == "ea" || next == "add" || next == "party" || next == "spirit" || next == "i" || next.StartsWith("("))
                    {
                        break;
                    }
                }
            }

            if (peek == "i" && endIndex > startIndex) break;

            if (!IsTokenClaimedByItem(tokens, endIndex)) break;

            endIndex++;
        }
        return endIndex - startIndex;
    }
    public static bool IsTokenClaimedByItem(List<string> tokens, int index)
    {
        string token = tokens[index].ToLower();
        string originalToken = tokens[index]; // Preserve casing for dictionary lookups

        // Strip parens so we can see what's inside
        if (token.StartsWith("(") && token.EndsWith(")"))
        {
            token = token.Substring(1, token.Length - 2);
            originalToken = originalToken.Substring(1, originalToken.Length - 2);
        }

        // 1. First, check if this token is literally a known modifier effect from the datasets
        if (IsKnownModifierEffect(originalToken)) return true;

        // 2. Evaluate chained tokens safely by inspecting inner '#' chunks and '.' chunks
        string[] dotChunks = token.Split('.');
        foreach (var dotChunk in dotChunks)
        {
            string[] hashChunks = dotChunk.Split('#');
            foreach (var chunk in hashChunks)
            {
                if (ValidItemProperties.Contains(chunk) || ValidTargets.Contains(chunk) ||
                    ContainerKeys.Contains(chunk) || MechanicPrefixes.Contains(chunk) ||
                    TogItems.Contains(chunk) || IsItemIdentifier(chunk) ||
                    IsRepeatPrefix(chunk, out _))
                {
                    return true;
                }
            }
        }

        // 3. Contextually allow values mapped to valid preceding keys
        if (index > 0)
        {
            string prev = tokens[index - 1].ToLower();
            string[] prevChunks = prev.Split('#');
            string truePrev = prevChunks[prevChunks.Length - 1];

            HashSet<string> propertiesExpectingValue = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tier", "n", "img", "doc", "sidesc", "m", "part", "mrg", "splice",
                "hsv", "hue", "thue", "p", "b", "draw", "rect",
                "k", "facade", "sticker", "enchant", "cast", "hat", "t", "gift", "learn", "i", "sd",
                "self", "jinx", "vase", "ea", "add", "party", "b",
                "ritem", "ritemx", "rditem"
            };

            if (propertiesExpectingValue.Contains(truePrev)) return true;
        }
        return false;
    }
    public static bool IsKnownModifierEffect(string originalToken)
    {
        // Direct O(1) checks against your exact modifier datasets
        if (ModifierDataSet.Curses != null && ModifierDataSet.Curses.ContainsKey(originalToken)) return true;
        if (ModifierDataSet.Blessings != null && ModifierDataSet.Blessings.ContainsKey(originalToken)) return true;
        if (ModifierDataSet.Tweaks != null && ModifierDataSet.Tweaks.ContainsKey(originalToken)) return true;

        // Fallback: Case-insensitive check just in case capitalization got mangled
        if (ModifierDataSet.Curses != null && ModifierDataSet.Curses.Keys.Any(k => string.Equals(k, originalToken, StringComparison.OrdinalIgnoreCase))) return true;
        if (ModifierDataSet.Blessings != null && ModifierDataSet.Blessings.Keys.Any(k => string.Equals(k, originalToken, StringComparison.OrdinalIgnoreCase))) return true;
        if (ModifierDataSet.Tweaks != null && ModifierDataSet.Tweaks.Keys.Any(k => string.Equals(k, originalToken, StringComparison.OrdinalIgnoreCase))) return true;

        return false;
    }
}

[System.Serializable]
public class ItemProperty { public string Key { get; set; } public string Value { get; set; } public ItemProperty(string k, string v) { Key = k; Value = v; } }

[System.Serializable]
public class ItemMechanic
{
    public List<string> Targets = new List<string>();  // e.g., left, topbot, mid
    public string Prefix = "";  // e.g., i, sd, k, t
    public string PayloadString = ""; // Raw nested string (e.g., facade.bas1)
    public object PayloadData { get; set; } = null;

    /// <summary> (.m#) Numerical effect multiplier. Multiplies the item's numerical output by this value (can be negative). Default is 1. </summary>
    public int Multiplier { get; set; } = 1;
    /// <summary> 
    /// (.mrg.) Merged Item combinations. Combines the effect of two items. 
    /// Example: If Item A modifies Top/Bot, and Item B modifies Mid, MRG applies B's Mid effect to A's Top/Bot. 
    /// Note: Results are highly engine-dependent and difficult to predict outside the game environment.
    /// </summary>
    public string MergedItem { get; set; } = string.Empty;
    /// <summary> 
    /// (.splice.) Spliced Item combinations. Similar to MRG, but uses alternative combination logic. 
    /// Creates distinct combined results. Highly engine-dependent.
    /// </summary>
    public string SplicedItem { get; set; } = string.Empty;
    /// <summary> Supports '#' delimited sub-keywords (e.g., "topbot.k.growth#k.cleave" adds both to top and bottom faces). </summary>
    public List<string> ChainedKeywords { get; set; } = new List<string>();
    /// <summary> 
    /// (xN.) Pre-multiplier. Applies the item's effect N separate times. 
    /// Example: 'x5.+1 pip' applies +1 pip five distinct times (different from .m.5 which is a flat +5).
    /// </summary>
    public int RepeatTimes { get; set; } = 1;
    /// <summary> (pertier.) Multiplies the item's effect by the equipping hero's Tier level (on average 1-3, can range from -5 to 20). </summary>
    public bool PerTier { get; set; }
    /// <summary> 
    /// (unpack.) Strips conditional activation requirements from a base item. 
    /// Example: Changes "On 1st turn, can't die" to simply "Can't die".
    /// </summary>
    public bool Unpack { get; set; }
    /// <summary> 
    /// (.part.#) Isolates a specific substring/sub-effect of a base item's payload. 
    /// Example: If an item grants "+2hp" (part.0) and "all sides blank" (part.1), targeting part.0 only gives the HP.
    /// </summary>
    public int? PartIndex { get; set; }
    public ItemMechanic AddTarget(string target) { Targets.Add(target); return this; }
    public string Export()
    {
        List<string> parts = new List<string>();
        if (Targets.Count > 0) parts.AddRange(Targets);
        bool payloadHandlesMultiplier = PayloadData is SDData sd && sd.xMultiplier >= 2 && sd.xMultiplier <= 9;
        if (RepeatTimes >= 2 && RepeatTimes <= 9 && !payloadHandlesMultiplier)
        {
            parts.Add($"x{RepeatTimes}");
        }
        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);

        string corePayload = PayloadString;
        if (PayloadData != null)
        {
            // Both Heroes and Monsters (like Eggs) use ExportAsHat when attached as a Hat mechanic.
            if (Prefix == "hat" && PayloadData is EntityData ed)
            {
                corePayload = ed.ExportAsHat();
            }
            // EVERYTHING ELSE (Abilities, Traits, Triggers, Curses, Modifiers) exports natively
            else if (PayloadData is SDData sdData)
            {
                corePayload = sdData.Export();
            }
            else
            {
                UnityEngine.Debug.LogError($"Tried to export unknown PayloadData type: {PayloadData.GetType()}");
            }
        }

        // LAW 1 & 2: Chained Keywords append to the mechanic itself, not inside the payload's brackets!
        if (ChainedKeywords.Count > 0)
        {
            corePayload += "#" + string.Join("#", ChainedKeywords);
        }

        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) { parts.Add("m"); parts.Add(Multiplier.ToString()); }
        if (!string.IsNullOrEmpty(MergedItem)) parts.Add($"mrg.{MergedItem}");
        if (!string.IsNullOrEmpty(SplicedItem)) parts.Add($"splice.{SplicedItem}");

        return string.Join(".", parts);
    }
}

public static class ExternalGameRegistry
{
    // ======================================================================================
    // EXTERNAL REGISTRY PLACEHOLDERS (Loudly flagged for your existing dictionaries/enums)
    // ======================================================================================
    public static bool IsValidSprite(string atlasId) => true; // TODO: Link to project's Sprite Dictionary
    public static bool IsValidKeyword(string key) => Enum.TryParse<EffectKeyword>(key, true, out _);
    public static bool IsValidAbility(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (OrbData.ValidBaseOrbs != null && OrbData.ValidBaseOrbs.Contains(id)) return true;
        if (BaseAbilityDatabase.Abilities != null && BaseAbilityDatabase.Abilities.Any(a => a != null && string.Equals(a.name, id, StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }
    public static bool IsValidItemName(string token) => Enum.TryParse<BaseItems>(token.Replace(" ", ""), true, out _);
}

[System.Serializable]
public struct ItemHsvShift
{
    // Range: -99 to 99
    public int Hue, Saturation, Value;
    public ItemHsvShift(int h, int s, int v) { Hue = Math.Clamp(h, -99, 99); Saturation = Math.Clamp(s, -99, 99); Value = Math.Clamp(v, -99, 99); }
}

[System.Serializable]
public class ItemData : SDData
{
    public string unityName = "New Item";

    public List<string> GlobalTags = new List<string>();
    /// <summary> (.tier) Rarity reward pool index. Valid range: -5 to 20. </summary>
    public int? Tier { get; set; }
    /// <summary> (.doc) Rich text description of the item's custom mechanics or use. </summary>
    public List<string> LearnedAbilities { get; set; } = new List<string>();
    /// <summary> (cleardesc) item Suppresses the game's auto-generated description of an item's effect. </summary>
    public bool ClearDescription { get; set; }
    /// <summary> (clearicon) item Suppresses the game's auto-generated item graphics. </summary>
    public bool ClearIcon { get; set; }

    public List<ItemProperty> Containers = new List<ItemProperty>();
    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();

    public bool IsEquippable => !string.IsNullOrEmpty(entityName) || Tier.HasValue;

    protected override void ParseCore(string data)
    {
        GlobalTags.Clear(); PropertiesClear(); Containers.Clear(); Mechanics.Clear();
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string itemCore = StaticBranchTracing.StripOuterParens(chunks[0]);

        for (int c = 1; c < chunks.Count; c++)
        {
            List<string> hiddenTokens = StaticBranchTracing.TopLevelSplit(chunks[c], '.');
            if (hiddenTokens.Count > 0 && (hiddenTokens[0].ToLower() == "hidden" || hiddenTokens[0].ToLower() == "temporary"))
                GlobalTags.Add(hiddenTokens[0]);
        }

        List<string> chains = StaticBranchTracing.TopLevelSplit(itemCore, '#');
        List<string> lastTargets = null;

        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            var stream = new TokenStream(StaticBranchTracing.TopLevelSplit(chain, '.'));
            ExtractKnowledge(stream, this, lastTargets);

            if (Mechanics.Count > 0)
            {
                var lastMechTargets = Mechanics.Last().Targets;
                if (lastMechTargets != null && lastMechTargets.Count > 0)
                    lastTargets = new List<string>(lastMechTargets);
            }
        }
    }
    private void ExtractKnowledge(TokenStream stream, ItemData item, List<string> inheritedTargets)
    {
        bool isFirstMechanic = true;

        while (!stream.IsEOF)
        {
            string originalToken = stream.Peek();
            string tokenLower = originalToken.ToLower();

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                stream.Consume();
                string inner = originalToken.Substring(1, originalToken.Length - 2);

                // Replicate top-level chaining rules for nested scopes
                List<string> chains = StaticBranchTracing.TopLevelSplit(inner, '#');

                // We pass down inherited targets to the first element in the parens if this is the first mechanic
                List<string> currentLastTargets = isFirstMechanic ? inheritedTargets : null;

                foreach (var chain in chains)
                {
                    var innerStream = new TokenStream(StaticBranchTracing.TopLevelSplit(chain, '.'));
                    ExtractKnowledge(innerStream, item, currentLastTargets);

                    if (item.Mechanics.Count > 0)
                    {
                        var lastMechTargets = item.Mechanics.Last().Targets;
                        if (lastMechTargets != null && lastMechTargets.Count > 0)
                            currentLastTargets = new List<string>(lastMechTargets);
                    }
                }

                isFirstMechanic = false; // The evaluated parens safely terminate the prior context
                continue;
            }

            if (item.TryProcessCommonMetadata(stream)) continue;

            // Simplified Fallthrough Rules
            switch (tokenLower)
            {
                case "tier": item.Tier = int.Parse(stream.ConsumeNext()); continue;
                case "sidesc": item.doc = stream.ConsumeNext(); continue;
                case "learn": item.LearnedAbilities.Add(stream.ConsumeNext()); continue;
                case "cleardesc": item.ClearDescription = true; stream.Consume(); continue;
                case "clearicon": item.ClearIcon = true; stream.Consume(); continue;
            }

            if (ItemDomainRules.ContainerKeys.Contains(tokenLower) && !ItemDomainRules.MechanicPrefixes.Contains(tokenLower))
            {
                item.Containers.Add(new ItemProperty(stream.Consume(), stream.Consume()));
                continue;
            }

            if (IsMechanicTriggerToken(tokenLower))
            {
                ProcessMechanicChain(stream, isFirstMechanic ? inheritedTargets : null);
                isFirstMechanic = false;
            }
            else
            {
                string droppedToken = stream.Consume();
                UnityEngine.Debug.LogError($"[ItemData Parser ERROR] Unrecognized string chunk discarded! Token '{droppedToken}' is not a valid target, prefix, or known modifier effect. Item context: {item.entityName ?? "Unknown"}");
            }
        }
    }
    private void ProcessMechanicChain(TokenStream stream, List<string> inheritedTargets)
    {
        ItemMechanic mech = new ItemMechanic();
        bool hasExplicitTargets = false;

        while (!stream.IsEOF)
        {
            string originalToken = stream.Peek();
            string tLower = originalToken.ToLower();

            if (tLower == "i")
            {
                stream.Consume();
                continue;
            }

            if (ItemDomainRules.MechanicPrefixes.Contains(tLower))
            {
                mech.Prefix = stream.Consume();
                mech.PayloadString = BuildPayloadString(stream);
                break;
            }
            else if (ItemDomainRules.ValidTargets.Contains(tLower))
            {
                if (!hasExplicitTargets)
                {
                    mech.Targets.Clear();
                    hasExplicitTargets = true;
                }
                mech.AddTarget(stream.Consume());
            }
            else if (ItemDomainRules.IsRepeatPrefix(tLower, out int reps))
            {
                mech.RepeatTimes = reps;
                stream.Consume();
            }
            else if (tLower == "pertier") { mech.PerTier = true; stream.Consume(); }
            else if (tLower == "unpack") { mech.Unpack = true; stream.Consume(); }
            else
            {
                stream.Consume();
                string subsequent = BuildPayloadString(stream);
                mech.PayloadString = string.IsNullOrEmpty(subsequent) ? originalToken : $"{originalToken}.{subsequent}";
                break;
            }
        }

        if (!hasExplicitTargets && inheritedTargets != null && inheritedTargets.Count > 0)
        {
            mech.Targets.AddRange(inheritedTargets);
        }

        // Process trailing suffixes (part, multiplier, mrg, splice)
        while (!stream.IsEOF)
        {
            string nextTokenLower = stream.Peek().ToLower();
            if (nextTokenLower == "part") { stream.Consume(); mech.PartIndex = int.Parse(stream.Consume()); }
            else if (nextTokenLower == "m") { stream.Consume(); mech.Multiplier = int.Parse(stream.Consume()); }
            else if (nextTokenLower == "mrg") { stream.Consume(); mech.MergedItem = BuildSuffixPayloadString(stream); }
            else if (nextTokenLower == "splice") { stream.Consume(); mech.SplicedItem = BuildSuffixPayloadString(stream); }
            else break;
        }

        AssignDomainPayload(mech);
        Mechanics.Add(mech);
    }
    private string BuildPayloadString(TokenStream stream)
    {
        List<string> payloadTokens = new List<string>();
        while (!stream.IsEOF)
        {
            string peek = stream.Peek().ToLower();
            if (peek == "part" || peek == "m" || (peek.StartsWith("m") && int.TryParse(peek.Substring(1), out _)) || peek == "mrg" || peek == "splice")
                break;
            if (ItemDomainRules.IsMechanicBoundary(peek))
                break;
            payloadTokens.Add(stream.Consume());
        }
        return string.Join(".", payloadTokens);
    }

    private string BuildSuffixPayloadString(TokenStream stream)
    {
        List<string> payloadTokens = new List<string>();
        bool isFirst = true;

        while (!stream.IsEOF)
        {
            string peek = stream.Peek().ToLower();

            // Never swallow root metadata properties (like img, tier, n, etc.)
            if (ItemDomainRules.RootMetadataProperties.Contains(peek))
                break;

            // Unless it's the very first token in the splice (e.g. splice.hat), 
            // break if we hit a hard context shift, or chained mrg/splice suffixes.
            if (!isFirst)
            {
                if (ItemDomainRules.IsMechanicBoundary(peek) || peek == "mrg" || peek == "splice")
                    break;
            }

            payloadTokens.Add(stream.Consume());
            isFirst = false;
        }

        return string.Join(".", payloadTokens);
    }
    public bool TryAbsorbIntoEntity(EntityData entity, bool isLeftMidException = false)
    {
        bool isPureEntityName = Mechanics.Count == 0 &&
                                !string.IsNullOrEmpty(entityName) &&
                                !ItemDomainRules.TogItems.Contains(entityName) && // ADDED
                                string.IsNullOrEmpty(imageOverride) &&
                                visuals.Count == 0 &&
                                !Tier.HasValue &&
                                string.IsNullOrEmpty(doc) &&
                                LearnedAbilities.Count == 0 &&
                                Containers.Count == 0;

        if (isPureEntityName)
        {
            entity.items.Add(entityName);
            return true;
        }

        bool isPureBaseItem = Mechanics.Count == 1 &&
                              string.IsNullOrEmpty(Mechanics[0].Prefix) &&
                              !ItemDomainRules.TogItems.Contains(Mechanics[0].PayloadString ?? "") && // ADDED
                              Mechanics[0].Targets.Count == 0 &&
                              Mechanics[0].ChainedKeywords.Count == 0 &&
                              Mechanics[0].Multiplier == 1 &&
                              Mechanics[0].RepeatTimes == 1 &&
                              !Mechanics[0].PerTier &&
                              !Mechanics[0].Unpack &&
                              string.IsNullOrEmpty(Mechanics[0].MergedItem) &&
                              string.IsNullOrEmpty(Mechanics[0].SplicedItem) &&
                              !Mechanics[0].PartIndex.HasValue &&
                              string.IsNullOrEmpty(imageOverride) &&
                              visuals.Count == 0 &&
                              !Tier.HasValue &&
                              string.IsNullOrEmpty(doc) &&
                              LearnedAbilities.Count == 0 &&
                              Containers.Count == 0;

        if (isPureBaseItem)
        {
            entity.items.Add(Mechanics[0].PayloadString);
            return true;
        }

        bool canMapNatively = true;
        foreach (var mech in Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";

            // Reject raw Modifiers (like 'self' or 'jinx' given as items), but allow targeted 'enchant' payloads
            if (mech.PayloadData is ModifierData && pfx != "enchant") return false;

            // ALLOW facade, sticker, cast, enchant to map natively so the UI can read them!
            // hat remains excluded because it's a massive structure that breaks face-grouping logic.
            if (pfx != "t" && pfx != "gift" && pfx != "learn" && pfx != "abilitydata" &&
                pfx != "k" && pfx != "facade" && pfx != "sticker" && pfx != "cast" && pfx != "enchant" && pfx != "")
            {
                return false;
            }

            if ((pfx == "k" || pfx == "facade" || pfx == "sticker" || pfx == "cast" || pfx == "enchant" || pfx == "") && mech.Targets.Count == 0)
            {
                return false;
            }

            // Protect existing face overrides from being flattened by combined target stickers/casts/enchants
            if (pfx == "sticker" || pfx == "cast" || pfx == "enchant")
            {
                List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();
                foreach (int face in targetFaces)
                {
                    if (entity.diceSides != null && face >= 0 && face < 6 && entity.diceSides[face] != null)
                    {
                        if (!string.IsNullOrEmpty(entity.diceSides[face].payload)) return false;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(imageOverride) && !imageOverride.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            entity.imageOverride = imageOverride;
            imageOverride = null;
        }

        if (visuals != null && visuals.Count > 0)
        {
            entity.visuals.AddRange(visuals);
            visuals.Clear();
        }

        if (!string.IsNullOrEmpty(doc))
        {
            entity.doc = doc;
            doc = null;
        }

        if (Tier.HasValue && entity is HeroData heroData)
        {
            heroData.tier = Tier.Value;
            Tier = null;
        }

        foreach (var ab in LearnedAbilities)
        {
            if (!entity.baseAbilityData.Contains(ab, StringComparer.OrdinalIgnoreCase))
                entity.baseAbilityData.Add(ab);
        }

        foreach (var mech in Mechanics)
        {
            string pfx = mech.Prefix?.ToLower() ?? "";
            if (pfx == "t")
            {
                if (mech.PayloadString != null && mech.PayloadString.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
                {
                    string curse = mech.PayloadString.Substring(5);
                    if (!entity.curses.Contains(curse, StringComparer.OrdinalIgnoreCase)) entity.curses.Add(curse);
                }
                else if (!entity.traits.Contains(mech.PayloadString, StringComparer.OrdinalIgnoreCase))
                    entity.traits.Add(mech.PayloadString);
            }
            else if (pfx == "gift")
            {
                if (!entity.blessings.Contains(mech.PayloadString, StringComparer.OrdinalIgnoreCase)) entity.blessings.Add(mech.PayloadString);
            }
            else if (pfx == "learn" || pfx == "abilitydata")
            {
                if (!entity.baseAbilityData.Contains(mech.PayloadString, StringComparer.OrdinalIgnoreCase)) entity.baseAbilityData.Add(mech.PayloadString);
            }
            else if (pfx == "k" || pfx == "facade" || pfx == "sticker" || pfx == "cast" || pfx == "enchant" || pfx == "") // ADDED MISSING PREFIXES
            {
                List<int> targetFaces = mech.Targets.SelectMany(t => DiceTargetHelper.GetIndicesForTarget(t)).Distinct().ToList();
                if (isLeftMidException && targetFaces.Contains(0) && targetFaces.Contains(1) && mech.Targets.Contains("left") && mech.Targets.Contains("mid")) targetFaces.Remove(1);
                string keyword = mech.PayloadString?.Trim().ToLower() ?? "";

                if (keyword == "blindfold")
                {
                    foreach (int faceIdx in targetFaces)
                    {
                        if (entity.diceSides != null && entity.diceSides[faceIdx] != null && entity.diceSides[faceIdx].faceType == DiceSideData.DiceFaceType.Egg)
                            if (!entity.diceSides[faceIdx].payload.EndsWith("#blindfold", StringComparison.OrdinalIgnoreCase))
                                entity.diceSides[faceIdx].payload += "#blindfold";
                    }
                    continue;
                }
                entity.ApplyMechanicToDiceSides(targetFaces, mech);
            }
        }
        return canMapNatively;
    }
    private void PropertiesClear()
    {
        entityName = null;
        imageOverride = null;
        Tier = null;
        doc = null;
        doc2 = null;
        visuals.Clear();
        ClearDescription = false;
        ClearIcon = false;
        LearnedAbilities.Clear();
    }
    private bool TryProcessItemMetadata(List<string> tokens, ref int i, string tokenLower, ItemData item)
    {
        switch (tokenLower)
        {
            case "tier":
                if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) item.Tier = t;
                return true;
            case "sidesc":
                if (i + 1 < tokens.Count) item.doc = tokens[++i];
                return true;
            case "learn":
                if (i + 1 < tokens.Count) item.LearnedAbilities.Add(tokens[++i]);
                return true;
            case "cleardesc":
                item.ClearDescription = true;
                return true;
            case "clearicon":
                item.ClearIcon = true;
                return true;
        }
        return false;
    }
    private bool TryProcessGenericContainer(List<string> tokens, ref int i, string tokenLower, string originalToken)
    {
        if (ItemDomainRules.ContainerKeys.Contains(tokenLower) && !ItemDomainRules.MechanicPrefixes.Contains(tokenLower))
        {
            if (i + 1 < tokens.Count)
            {
                Containers.Add(new ItemProperty(originalToken, tokens[++i]));
                return true;
            }
        }
        return false;
    }
    private bool IsMechanicTriggerToken(string token)
    {
        return ItemDomainRules.MechanicPrefixes.Contains(token) || token == "pertier" || token == "unpack" ||
               ItemDomainRules.ValidTargets.Contains(token) || ItemDomainRules.IsItemIdentifier(token) ||
               ItemDomainRules.IsRepeatPrefix(token, out _);
    }
    private void AssignDomainPayload(ItemMechanic mech)
    {
        if (string.IsNullOrEmpty(mech.PayloadString)) return;
        string core = StaticBranchTracing.StripOuterParens(mech.PayloadString);
        if (mech.Prefix == "hat")
        {
            if (StaticBranchTracing.IsMonsterEntity(core)) { MonsterData monster = new MonsterData(); monster.Parse(core); mech.PayloadData = monster; }
            else { HeroData hero = new HeroData(); hero.Parse(core); mech.PayloadData = hero; }
        }
        else if (mech.Prefix == "onhitdata") { OnHitData ohd = new OnHitData(); ohd.Parse(core); mech.PayloadData = ohd; }
        else if (mech.Prefix == "triggerhpdata") { TriggerHPData thp = new TriggerHPData(); thp.Parse(core); mech.PayloadData = thp; }
        else if (mech.Prefix == "enchant") { ModifierData mod = new ModifierData(); mod.Parse(core); mech.PayloadData = mod; }
        else if (mech.Prefix == "cast" || mech.Prefix == "abilitydata") { mech.PayloadData = AbilityData.CreateSpellOrTactic(core); }
        else if (mech.Prefix == "sticker") { ItemData item = new ItemData(); item.Parse(core); mech.PayloadData = item; }
        else if (mech.Prefix == "t")
        {
            if (StaticBranchTracing.IsMonsterEntity(core))
            {
                MonsterData monster = new MonsterData(); monster.Parse(core); mech.PayloadData = monster;
            }
            else if (core.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
            {
                string modifierCore = StaticBranchTracing.StripOuterParens(core.Substring(5).Trim());
                ModifierData mod = new ModifierData(); mod.Parse(modifierCore); mech.PayloadData = mod;
            }
            else
            {
                HeroData hero = new HeroData(); hero.Parse(core); mech.PayloadData = hero;
            }
        }
        else if (mech.Prefix == "i" || string.IsNullOrEmpty(mech.Prefix))
        {
            if (mech.PayloadString.StartsWith("("))
            {
                ItemData item = new ItemData(); item.Parse(core); mech.PayloadData = item;
            }
            else if (mech.Targets.Contains("self", StringComparer.OrdinalIgnoreCase))
            {
                ModifierData mod = new ModifierData();
                mod.Parse(core);
                mech.PayloadData = mod;
            }
        }
    }

    protected override string ExportCore()
    {
        List<string> chainParts = new List<string>();
        foreach (var cont in Containers) chainParts.Add($"{cont.Key}.({StaticBranchTracing.StripOuterParens(cont.Value)})");

        /* // Old Smart bracketing, temporarily disabled.
        if (mechanicParts.Count > 0)
        {
            string mechs = mechanicParts[0];

            bool needsBrackets = Mechanics.Any(m =>
                m.PartIndex.HasValue ||
                m.Multiplier != 1 ||
                m.PerTier ||
                m.Unpack ||
                !string.IsNullOrEmpty(m.MergedItem) ||
                !string.IsNullOrEmpty(m.SplicedItem));

            if (needsBrackets && !mechs.StartsWith("("))
            {
                mechs = $"({mechs})";
            }

            if (!mechs.StartsWith("i.", StringComparison.OrdinalIgnoreCase) && !mechs.StartsWith("sd.", StringComparison.OrdinalIgnoreCase))
                chainParts.Add($"i.{mechs}");
            else
                chainParts.Add(mechs);
        }
        */

        List<string> mechanicParts = new List<string>();
        OptimizeAndExportMechanics(mechanicParts);

        if (mechanicParts.Count > 0)
        {
            chainParts.Add(mechanicParts[0]);
        }

        string visualsStr = ItemSyntaxCompiler.BuildVisualsString(this, imageOverride);
        if (!string.IsNullOrEmpty(visualsStr)) chainParts.Add(visualsStr);

        if (ClearDescription) chainParts.Add("cleardesc");
        if (ClearIcon) chainParts.Add("clearicon");
        if (Tier.HasValue) chainParts.Add($"tier.{Tier.Value}");
        if (!string.IsNullOrEmpty(doc)) chainParts.Add($"doc.{doc}");
        if (!string.IsNullOrEmpty(entityName)) chainParts.Add($"n.{entityName}");

        StringBuilder sb = new StringBuilder(string.Join(".", chainParts));
        foreach (var tag in GlobalTags) sb.Append($"&{tag}");

        string payload = sb.ToString();
        if (string.IsNullOrWhiteSpace(payload)) return "";

        // STRICT SELF-BRACKETING DOCTRINE.
        // It brackets its own scope. It never guesses.
        return $"({payload})";
    }

    // Current item export
    private void OptimizeAndExportMechanics(List<string> chainParts)
    {
        List<ItemMechanic> optimizedMechanics = new List<ItemMechanic>();
        foreach (var mech in Mechanics)
        {
            // Clone to prevent mutating original memory references during export operations
            ItemMechanic clonedMech = CloneMechanic(mech);

            // Case 1: Direct loose Tog Items
            if (string.IsNullOrEmpty(clonedMech.Prefix) && ItemDomainRules.TogItems.Contains(clonedMech.PayloadString))
            {
                var prev = optimizedMechanics.LastOrDefault(m => m.Targets.Count == clonedMech.Targets.Count && m.Targets.All(t => clonedMech.Targets.Contains(t)));
                if (prev != null)
                {
                    prev.ChainedKeywords.Add(clonedMech.PayloadString);
                    continue;
                }
            }

            /*
            // Case 2: Tog Items wrapped inside of an inherent (i) Item Pack tuple
            if (clonedMech.Prefix == "i" && clonedMech.PayloadData is ItemData nestedItem)
            {
                bool onlyTog = nestedItem.Mechanics.Count > 0 && nestedItem.Mechanics.All(m => string.IsNullOrEmpty(m.Prefix) && ItemDomainRules.TogItems.Contains(m.PayloadString));
                if (onlyTog)
                {
                    bool allMerged = true;
                    foreach (var innerMech in nestedItem.Mechanics)
                    {
                        var prev = optimizedMechanics.LastOrDefault(m => m.Targets.Count == innerMech.Targets.Count && m.Targets.All(t => innerMech.Targets.Contains(t)));
                        if (prev != null)
                        {
                            prev.ChainedKeywords.Add(innerMech.PayloadString);
                        }
                        else
                        {
                            allMerged = false;
                        }
                    }

                    // Skip appending this '.i' node if we successfully merged all its contents natively
                    if (allMerged) continue;
                }
            }
            */

            // Case 2: Tog Items wrapped inside of an inherent (i) Item Pack tuple
            if (clonedMech.Prefix == "i" && clonedMech.PayloadData is ItemData nestedItem)
            {
                bool onlyTog = nestedItem.Mechanics.Count > 0 && nestedItem.Mechanics.All(m => string.IsNullOrEmpty(m.Prefix) && ItemDomainRules.TogItems.Contains(m.PayloadString));
                if (onlyTog)
                {
                    bool allMerged = true;

                    // PASS 1: Validation. Ensure EVERY item can merge before we mutate anything.
                    foreach (var innerMech in nestedItem.Mechanics)
                    {
                        var prev = optimizedMechanics.LastOrDefault(m => m.Targets.Count == innerMech.Targets.Count && m.Targets.All(t => innerMech.Targets.Contains(t)));
                        if (prev == null)
                        {
                            allMerged = false;
                            break;
                        }
                    }

                    // PASS 2: Mutation. Only apply if the entire block successfully mapped.
                    if (allMerged)
                    {
                        foreach (var innerMech in nestedItem.Mechanics)
                        {
                            var prev = optimizedMechanics.LastOrDefault(m => m.Targets.Count == innerMech.Targets.Count && m.Targets.All(t => innerMech.Targets.Contains(t)));
                            if (prev != null)
                            {
                                prev.ChainedKeywords.Add(innerMech.PayloadString);
                            }
                        }
                        // Skip appending this '.i' node because we successfully merged all its contents natively
                        continue;
                    }
                }
            }

            optimizedMechanics.Add(clonedMech);
        }

        // --- EXPORT & CHAINING LOGIC ---
        if (optimizedMechanics.Count > 0)
        {
            StringBuilder mechsSb = new StringBuilder();
            List<string> lastTargets = null;
            bool lastPerTier = false;
            bool lastUnpack = false;

            for (int i = 0; i < optimizedMechanics.Count; i++)
            {
                var mech = optimizedMechanics[i];
                bool targetsMatch = false;

                if (lastTargets != null && mech.Targets.Count == lastTargets.Count)
                {
                    targetsMatch = true;
                    foreach (var t in mech.Targets)
                    {
                        if (!lastTargets.Contains(t))
                        {
                            targetsMatch = false;
                            break;
                        }
                    }
                }

                bool safeToChain = targetsMatch;
                if (safeToChain)
                {
                    // Do not falsely propagate scaling rules down the chain 
                    if (lastPerTier && !mech.PerTier) safeToChain = false;
                    if (lastUnpack && !mech.Unpack) safeToChain = false;
                }

                List<string> currentTargets = new List<string>(mech.Targets);
                bool currentPerTier = mech.PerTier;
                bool currentUnpack = mech.Unpack;

                string exportedMech = mech.Export();

                if (i == 0)
                {
                    mechsSb.Append(exportedMech);
                }
                else
                {
                    if (safeToChain)
                    {
                        // Inherit targets via '#', clear explicit targets from export to prevent redeclaring side words
                        mech.Targets.Clear();

                        // PREVENT silent Prefix drops if the chain inherits an inherently prefixed mechanic (like facade)
                        string chainedExport = mech.Export();
                        if (!string.IsNullOrEmpty(mech.Prefix) && !chainedExport.StartsWith(mech.Prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            chainedExport = $"{mech.Prefix}.{chainedExport}";
                        }

                        mechsSb.Append("#").Append(chainedExport);
                    }
                    else
                    {
                        // Context shift requires a clean boundary delimiter
                        mechsSb.Append(".i.").Append(exportedMech);
                    }
                }

                lastTargets = currentTargets;
                lastPerTier = currentPerTier;
                lastUnpack = currentUnpack;
            }

            // NEW: Directly append the raw item syntax. 
            // DO NOT inject "i." here. The parent/caller decides the modifier!
            chainParts.Add(mechsSb.ToString());
        }
    }
    private ItemMechanic CloneMechanic(ItemMechanic original)
    {
        return new ItemMechanic
        {
            Targets = new List<string>(original.Targets),
            Prefix = original.Prefix,
            PayloadString = original.PayloadString,
            PayloadData = original.PayloadData, // Shallow reference copy is fine
            Multiplier = original.Multiplier,
            MergedItem = original.MergedItem,
            SplicedItem = original.SplicedItem,
            ChainedKeywords = new List<string>(original.ChainedKeywords),
            RepeatTimes = original.RepeatTimes,
            PerTier = original.PerTier,
            Unpack = original.Unpack,
            PartIndex = original.PartIndex
        };
    }
    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- ITEM DATA DEBUG ---");
        sb.AppendLine($"{indent}Name: {entityName}");
        sb.AppendLine($"{indent}Tier: {Tier}");
        string displayValue = !string.IsNullOrEmpty(imageOverride) && imageOverride.Length > 32 ? "<base64 string img>" : imageOverride;
        sb.AppendLine($"{indent}ImageRef: {displayValue}");
        //if (HsvShift.HasValue) sb.AppendLine($"{indent}HsvShift: {HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");

        sb.AppendLine($"{indent}\n{indent}Mechanics ({Mechanics.Count}):");
        for (int i = 0; i < Mechanics.Count; i++)
        {
            var m = Mechanics[i];
            sb.AppendLine($"{indent}  [{i}] Targets: [{string.Join(", ", m.Targets)}] | Prefix: '{m.Prefix}'");
            sb.AppendLine($"{indent}      Payload: '{m.PayloadString}'");

            if (m.PayloadData is ItemData nestedItem)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked ItemData!]");
                nestedItem.DebugContentsToConsole(indent + "        ");
            }
            else if (m.PayloadData is AbilityData ad)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked AbilityData!]");
                ad.DebugAbilityCompact(indent + "        ");
            }
            else if (m.PayloadData is HeroData hd)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked HeroData!]");
                hd.DebugContentsToConsoleCompact(indent + "        ");
            }
            else if (m.PayloadData is MonsterData md)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked MonsterData!]");
                md.DebugContentsToConsoleCompact(indent + "        ");
            }
            else if (m.PayloadData is ModifierData mod)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked ModifierData!]");
                mod.DebugContentsToConsole(indent + "        ");
            }
            else if (m.PayloadData != null)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked {m.PayloadData.GetType().Name}!]");
            }

            if (m.Multiplier != 1 || !string.IsNullOrEmpty(m.MergedItem) || !string.IsNullOrEmpty(m.SplicedItem) || m.PartIndex.HasValue)
                sb.AppendLine($"{indent}      Suffixes -> m:{m.Multiplier}, mrg:{m.MergedItem}, splice:{m.SplicedItem}, part:{m.PartIndex}");
        }
        UnityEngine.Debug.Log(sb.ToString());
    }
}

public enum PayloadInjectionZone
{
    InnerEntity,   // Appends inside the base replica ( ) alongside standard items/traits
    OuterEntity,   // Appends outside the base ( ) alongside OnHits/TriggerHPs
    EntityWrapper  // Completely wraps the entity string (Uses {0} as the entity placeholder)
}

public struct ItemInjectionResult
{
    public string FormattedString;
    public PayloadInjectionZone Zone;
}

public static class CustomItemContextHelper
{
    public static ItemInjectionResult EvaluateItem(ItemData item)
    {
        if (item == null) return new ItemInjectionResult { FormattedString = "" };

        // Strictly architectural domain wrapper: GiveItem guarantees an 'i.(...)' output payload
        ModifierData giveItemMod = new ModifierData { ActionType = ModifierActionType.GiveItem, ItemPayload = item };
        string exported = giveItemMod.Export();

        if (string.IsNullOrWhiteSpace(exported) || exported == "()")
            return new ItemInjectionResult { FormattedString = "" };

        return new ItemInjectionResult
        {
            FormattedString = exported,
            Zone = PayloadInjectionZone.InnerEntity
        };
    }
}