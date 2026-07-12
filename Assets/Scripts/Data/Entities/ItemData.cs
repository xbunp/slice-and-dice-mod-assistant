using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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

    public static bool IsRepeatPrefix(string token, out int count)
    {
        count = 1;
        if (string.IsNullOrEmpty(token) || char.ToLower(token[0]) != 'x') return false;
        return int.TryParse(token.Substring(1), out count);
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

    /*
    public string Export()
    {
        List<string> parts = new List<string>();
        if (Targets.Count > 0) parts.AddRange(Targets);
        if (RepeatTimes != 1) parts.Add($"x{RepeatTimes}");
        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);

        string corePayload = PayloadString;
        if (ChainedKeywords.Count > 0)
        {
            string chains = "#" + string.Join("#", ChainedKeywords);

            // Inject chained keywords inside parens if the payload is a wrapped group (e.g. hat data)
            if (corePayload.StartsWith("(") && corePayload.EndsWith(")"))
            {
                corePayload = corePayload.Substring(0, corePayload.Length - 1) + chains + ")";
            }
            else
            {
                corePayload += chains;
            }
        }

        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) parts.Add($"m{Multiplier}");
        if (!string.IsNullOrEmpty(MergedItem)) parts.Add($"mrg.{MergedItem}");
        if (!string.IsNullOrEmpty(SplicedItem)) parts.Add($"splice.{SplicedItem}");
        return string.Join(".", parts);
    }
    */

    public string Export()
    {
        List<string> parts = new List<string>();
        if (Targets.Count > 0) parts.AddRange(Targets);
        if (RepeatTimes != 1) parts.Add($"x{RepeatTimes}");
        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);

        string corePayload = PayloadString;

        // DYNAMIC EXPORT: Pulls fresh data from the nested object if mutated.
        if (PayloadData != null)
        {
            string exportedData = "";

            // Route Hats to the specialized clean export method
            if (Prefix == "hat" && PayloadData is HeroData hd)
            {
                exportedData = hd.ExportAsHat();
            }
            else if (PayloadData is SDData sdData)
            {
                exportedData = sdData.Export();
            }

            if (!string.IsNullOrEmpty(exportedData))
            {
                // Certain nested mechanics require parenthetical wrapping in textmod syntax
                if (Prefix == "hat" || Prefix == "enchant" || Prefix == "self" ||
                    Prefix == "triggerhpdata" || Prefix == "onhitdata" || Prefix == "sticker")
                {
                    if (!exportedData.StartsWith("(")) corePayload = $"({exportedData})";
                    else corePayload = exportedData;
                }
                else
                {
                    corePayload = exportedData;
                }
            }
        }

        if (ChainedKeywords.Count > 0)
        {
            string chains = "#" + string.Join("#", ChainedKeywords);
            // Inject chained keywords inside parens if the payload is a wrapped group
            if (corePayload.StartsWith("(") && corePayload.EndsWith(")"))
            {
                corePayload = corePayload.Substring(0, corePayload.Length - 1) + chains + ")";
            }
            else
            {
                corePayload += chains;
            }
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
    /* 
     * LOUD WARNING FOR LATER AI / COMPILER:
     * DO NOT REDEFINE THESE TYPES. They exist in the primary game engine dictionaries.
     * Replace the placeholder types below with references to your actual project assemblies.
     */
    public static bool IsValidSprite(string atlasId) => true; // TODO: Link to project's Sprite Dictionary
    public static bool IsValidKeyword(string key) => Enum.TryParse<EffectKeyword>(key, true, out _);
    public static bool IsValidAbility(string id) => BaseAbilityDatabase.ValidAbilities.Contains(id);
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
    public string DocumentedDescription { get; set; } = string.Empty;
    /// <summary> (.hsv) Direct Hue, Saturation, and Value shifting values (-99 to 99). </summary>
    public ItemHsvShift? HsvShift { get; set; }
    /// <summary> (.hue) Simple single-axis hue shift value. </summary>
    public int? SimpleHue { get; set; }
    /// <summary> (.p) Palette override configuration. Format: "fff:fff:##" (e.g. "aaa:a0a:60") </summary>
    public string PaletteOverride { get; set; } = string.Empty;
    /// <summary> (.b) Custom border color overlay determined by 3 hex characters (e.g., "f0f"). </summary>
    public string BorderColorCode { get; set; } = string.Empty;
    /// <summary> (.draw) Complex arbitrary UI sprite rendering instructions. </summary>
    public string UiDrawInstructions { get; set; } = string.Empty;
    /// <summary> (.rect) UI single-color layout rectangle drawing parameters. </summary>
    public string UiRectInstructions { get; set; } = string.Empty;
    /// <summary> (learn.) Base game spells or tactics given to the player. </summary>
    public List<string> LearnedAbilities { get; set; } = new List<string>();

    /// <summary> (cleardesc) item Suppresses the game's auto-generated description of an item's effect. </summary>
    public bool ClearDescription { get; set; }
    /// <summary> (clearicon) item Suppresses the game's auto-generated item graphics. </summary>

    public bool ClearIcon { get; set; }
    public List<ItemProperty> Containers = new List<ItemProperty>();
    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();

    // now handled through item mechanics. 
    /*
    /// <summary> 
    /// (abilitydata.) Custom tactical/spell data payloads. 
    /// These are raw strings that should be handed off to SpellData or TacticData parsers.
    /// </summary>
    public List<string> CustomAbilities { get; set; } = new List<string>();

    /// <summary> 
    /// (t.) Passive traits mapped to this item. 
    /// Highly common usage: "t.jinx.[ModifierName]" which invokes a gameplay modifier as a passive trait.
    /// </summary>
    public List<string> PassiveTraits { get; set; } = new List<string>();

    /// <summary> 
    /// (self.) Modifiers applied strictly to the item-carrying entity. 
    /// Unlike standard modifiers which are global, this payload isolates the effect to the single hero/monster.
    /// Payload can be a basic name or a fully nested (ModifierData) scope.
    /// </summary>
    public List<string> SelfModifiers { get; set; } = new List<string>();
    */

    public bool IsEquippable => !string.IsNullOrEmpty(entityName) || Tier.HasValue;

    public override void Parse(string data)
    {
        GlobalTags.Clear(); PropertiesClear(); Containers.Clear(); Mechanics.Clear();
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string itemCore = chunks[0];

        for (int c = 1; c < chunks.Count; c++)
        {
            List<string> hiddenTokens = StaticBranchTracing.TopLevelSplit(chunks[c], '.');
            if (hiddenTokens.Count > 0 && (hiddenTokens[0].ToLower() == "hidden" || hiddenTokens[0].ToLower() == "temporary"))
                GlobalTags.Add(hiddenTokens[0]);
        }

        itemCore = StaticBranchTracing.StripOuterParens(itemCore);

        List<string> chains = StaticBranchTracing.TopLevelSplit(itemCore, '#');

        // FIX: Track and propagate targets across '#' splits. Because '#' is a semantic 'AND',
        // the subsequent split chunk must inherit the target context of the chunk directly to its left
        // if it doesn't declare an explicit target of its own.
        List<string> lastTargets = null;

        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');

            // Pass the targets down to be inherited
            ExtractKnowledge(tokens, this, lastTargets);

            // Update the tracked targets to whatever the last parsed mechanic used
            if (Mechanics.Count > 0)
            {
                lastTargets = Mechanics.Last().Targets.ToList();
            }
        }
    }

    private void PropertiesClear()
    {
        thue = new Thue();
        phue = new Phue();
        entityName = string.Empty; imageOverride = string.Empty; Tier = null; DocumentedDescription = string.Empty;
        HsvShift = null; SimpleHue = null; PaletteOverride = string.Empty;
        BorderColorCode = string.Empty; UiDrawInstructions = string.Empty; UiRectInstructions = string.Empty;
        ClearDescription = false; ClearIcon = false; LearnedAbilities.Clear();
    }

    /*
    private void ExtractKnowledge(List<string> tokens, ItemData item)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerChains = StaticBranchTracing.TopLevelSplit(inner, '#');
                foreach (var chain in innerChains)
                {
                    if (string.IsNullOrWhiteSpace(chain)) continue;
                    List<string> innerTokens = StaticBranchTracing.TopLevelSplit(chain, '.');
                    ExtractKnowledge(innerTokens, item);
                }
                continue;
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) item.entityName = tokens[++i]; break;
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) item.Tier = t; break;
                case "doc":
                case "sidesc": if (i + 1 < tokens.Count) item.DocumentedDescription = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) item.imageOverride = tokens[++i]; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsvParts = tokens[++i].Split(':');
                        if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out int h) && int.TryParse(hsvParts[1], out int s) && int.TryParse(hsvParts[2], out int v))
                            item.HsvShift = new ItemHsvShift(h, s, v);
                    }
                    break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) item.SimpleHue = hueVal; break;
                case "thue": if (i + 1 < tokens.Count) item.TargetedHue = tokens[++i]; break;
                case "p": if (i + 1 < tokens.Count) item.PaletteOverride = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) item.BorderColorCode = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) item.UiDrawInstructions = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) item.UiRectInstructions = tokens[++i]; break;
                case "learn": if (i + 1 < tokens.Count) item.LearnedAbilities.Add(tokens[++i]); break;
                case "cleardesc": item.ClearDescription = true; break;
                case "clearicon": item.ClearIcon = true; break;

                default:
                    if (TryProcessGenericContainer(tokens, ref i, tokenLower, originalToken)) { }
                    else if (IsMechanicTriggerToken(tokenLower)) ProcessMechanicChain(tokens, ref i, originalToken);
                    break;
            }
        }
    }
    */

    // FIX: Add inheritedTargets parameter
    private void ExtractKnowledge(List<string> tokens, ItemData item, List<string> inheritedTargets = null)
    {
        bool isFirstMechanic = true; // Track if we are at the start of a # chain

        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                // We pass null into recursive parentheses to prevent unexpected deep inheritance
                ProcessRecursiveParentheses(originalToken, (innerTokens) => ExtractKnowledge(innerTokens, item, null));
                continue;
            }

            if (TryProcessCommonMetadata(tokens, ref i, tokenLower))
            {
                if (tokenLower == "hsv") item.HsvShift = new ItemHsvShift(h, s, v);
                else if (tokenLower == "hue") item.SimpleHue = hue;
                else if (tokenLower == "thue") item.thue = UnpackTHue(tokens[i]);
                else if (tokenLower == "p") item.PaletteOverride = p;
                else if (tokenLower == "b") item.BorderColorCode = b;
                else if (tokenLower == "draw") item.UiDrawInstructions = draw;
                else if (tokenLower == "rect") item.UiRectInstructions = rect;
                else if (tokenLower == "doc") item.DocumentedDescription = doc;
                continue;
            }

            switch (tokenLower)
            {
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) item.Tier = t; break;
                case "sidesc": if (i + 1 < tokens.Count) item.DocumentedDescription = tokens[++i]; break;
                case "learn": if (i + 1 < tokens.Count) item.LearnedAbilities.Add(tokens[++i]); break;
                case "cleardesc": item.ClearDescription = true; break;
                case "clearicon": item.ClearIcon = true; break;

                default:
                    if (TryProcessGenericContainer(tokens, ref i, tokenLower, originalToken)) { }
                    else if (IsMechanicTriggerToken(tokenLower))
                    {
                        // FIX: Pass inherited targets ONLY to the first mechanic, then turn it off
                        ProcessMechanicChain(tokens, ref i, originalToken, isFirstMechanic ? inheritedTargets : null);
                        isFirstMechanic = false;
                    }
                    break;
            }
        }
    }

    // FIX: Add inheritedTargets parameter
    private void ProcessMechanicChain(List<string> tokens, ref int i, string initialToken, List<string> inheritedTargets = null)
    {
        ItemMechanic mech = new ItemMechanic();
        bool hasExplicitTargets = false; // Track if this specific item defined its own targets

        while (i < tokens.Count)
        {
            string originalToken = tokens[i];
            string tLower = originalToken.ToLower();

            if (tLower == "i")
            {
                i++;
                continue;
            }

            if (ItemDomainRules.MechanicPrefixes.Contains(tLower))
            {
                mech.Prefix = tLower;
                i++; // Move past the prefix
                mech.PayloadString = BuildPayloadString(tokens, ref i);
                break; // Exits the mechanic loop immediately once the payload is assigned
            }
            else if (ItemDomainRules.ValidTargets.Contains(tLower))
            {
                // FIX: If this item explicitly declares a target, clear out anything we were going to inherit
                if (!hasExplicitTargets)
                {
                    mech.Targets.Clear();
                    hasExplicitTargets = true;
                }
                mech.AddTarget(originalToken);
            }
            else if (ItemDomainRules.IsRepeatPrefix(tLower, out int reps))
            {
                mech.RepeatTimes = reps;
            }
            else if (tLower == "pertier") mech.PerTier = true;
            else if (tLower == "unpack") mech.Unpack = true;
            else
            {
                List<string> payloadTokens = new List<string> { originalToken };
                i++;

                string subsequent = BuildPayloadString(tokens, ref i);
                if (!string.IsNullOrEmpty(subsequent)) payloadTokens.Add(subsequent);

                mech.PayloadString = string.Join(".", payloadTokens);
                break;
            }
            i++;
        }

        // FIX: If no explicit targets were found, but we inherited some from a '#' split, apply them!
        if (!hasExplicitTargets && inheritedTargets != null && inheritedTargets.Count > 0)
        {
            mech.Targets.AddRange(inheritedTargets);
        }

        // Process trailing suffixes (part, multiplier, mrg, splice)
        while (i + 1 < tokens.Count)
        {
            string nextTokenLower = tokens[i + 1].ToLower();

            if (nextTokenLower == "part" && i + 2 < tokens.Count) { if (int.TryParse(tokens[i + 2], out int pIdx)) { mech.PartIndex = pIdx; i += 2; } else break; }
            else if (nextTokenLower == "m" && i + 2 < tokens.Count) { if (int.TryParse(tokens[i + 2], out int mult)) { mech.Multiplier = mult; i += 2; } else break; }
            else if (nextTokenLower == "mrg" && i + 2 < tokens.Count) { mech.MergedItem = tokens[i + 2]; i += 2; }
            else if (nextTokenLower == "splice" && i + 2 < tokens.Count) { mech.SplicedItem = tokens[i + 2]; i += 2; }
            else break;
        }

        AssignDomainPayload(mech);
        Mechanics.Add(mech);
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
        else if (mech.Prefix == "onhitdata" || mech.Prefix == "triggerhpdata") { TriggerHPData thp = new TriggerHPData(); thp.Parse(core); mech.PayloadData = thp; }
        else if (mech.Prefix == "enchant" || mech.Prefix == "self") { ModifierData mod = new ModifierData(); mod.Parse(core); mech.PayloadData = mod; }
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
            if (mech.PayloadString.StartsWith("(")) { ItemData item = new ItemData(); item.Parse(core); mech.PayloadData = item; }
        }
    }


    public override string Export()
    {
        List<string> chainParts = new List<string>();
        if (!string.IsNullOrEmpty(entityName)) chainParts.Add($"n.{entityName}");
        if (Tier.HasValue) chainParts.Add($"tier.{Tier.Value}");
        if (!string.IsNullOrEmpty(DocumentedDescription)) chainParts.Add($"doc.{DocumentedDescription}");
        if (!string.IsNullOrEmpty(imageOverride) && imageOverride != "None") chainParts.Add($"img.{imageOverride}");
        if (HsvShift.HasValue) chainParts.Add($"hsv.{HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");
        if (SimpleHue.HasValue) chainParts.Add($"hue.{SimpleHue.Value}");

        if (this.thue != null && this.thue.colorOffset != 0) chainParts.Add($".{PackTHue(this.thue)}");

        if (!string.IsNullOrEmpty(PaletteOverride)) chainParts.Add($"p.{PaletteOverride}");
        if (!string.IsNullOrEmpty(BorderColorCode)) chainParts.Add($"b.{BorderColorCode}");
        if (!string.IsNullOrEmpty(UiDrawInstructions)) chainParts.Add($"draw.{UiDrawInstructions}");
        if (!string.IsNullOrEmpty(UiRectInstructions)) chainParts.Add($"rect.{UiRectInstructions}");
        if (ClearDescription) chainParts.Add("cleardesc");
        if (ClearIcon) chainParts.Add("clearicon");

        foreach (var cont in Containers) chainParts.Add($"{cont.Key}.({cont.Value})");

        // Run optimization and append the mechanics to chainParts
        OptimizeAndExportMechanics(chainParts);

        StringBuilder sb = new StringBuilder(string.Join(".", chainParts));
        foreach (var tag in GlobalTags) sb.Append($"&{tag}");

        return sb.ToString();
    }

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

            optimizedMechanics.Add(clonedMech);
        }

        foreach (var mech in optimizedMechanics)
        {
            chainParts.Add(mech.Export());
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
        if (HsvShift.HasValue) sb.AppendLine($"{indent}HsvShift: {HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");

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

    // Helper method to safely collect forward payload tokens without suffix collisions
    // Inside ItemData.cs class
    /// <summary>
    /// Accumulates tokens for a mechanic's payload.
    /// NOTE: Must explicitly halt if it encounters 'i'. Because 'i' is the universal 
    /// delimiter for a new mechanic context, allowing a payload (like a keyword) to 
    /// swallow it will corrupt the parser's state machine and merge distinct mechanics.
    /// </summary>
    private string BuildPayloadString(List<string> tokens, ref int i)
    {
        List<string> payloadTokens = new List<string>();

        // Define major structural prefixes that should break inline payload accumulation
        HashSet<string> majorPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hat", "sticker", "enchant", "cast", "facade", "sidesc", "onhitdata", "triggerhpdata"
        };

        while (i < tokens.Count)
        {
            string peek = tokens[i].ToLower();
            if (peek == "part" || (peek.StartsWith("m") && int.TryParse(peek.Substring(1), out _)) || peek == "mrg" || peek == "splice")
                break;

            // CRITICAL FIX: Add 'i' to the break condition so keywords don't swallow adjacent item chains
            if (payloadTokens.Count > 0 && (majorPrefixes.Contains(peek) || peek == "i"))
                break;

            payloadTokens.Add(tokens[i]);
            i++;
        }
        i--;
        return string.Join(".", payloadTokens);
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
    /// <summary>
    /// Evaluates a Custom Item to determine its exact syntax and where it belongs 
    /// relative to the Entity's structural parentheses.
    /// </summary>
    public static ItemInjectionResult EvaluateItem(ItemData item)
    {
        if (item == null) return new ItemInjectionResult { FormattedString = "" };

        string rawItem = item.Export();
        if (string.IsNullOrWhiteSpace(rawItem)) return new ItemInjectionResult { FormattedString = "" };

        string contentToEvaluate = rawItem.Trim();

        // ====================================================================
        // RULE 1: EXTERNAL PROPERTIES (OuterEntity Zone)
        // ====================================================================
        if (contentToEvaluate.StartsWith("abilitydata.") ||
            contentToEvaluate.StartsWith("triggerhpdata.") ||
            contentToEvaluate.StartsWith("onhitdata.") ||
            contentToEvaluate.StartsWith("i.abilitydata.") ||
            contentToEvaluate.StartsWith("i.triggerhpdata.") ||
            contentToEvaluate.StartsWith("learn.") ||         // ADDED THIS
            contentToEvaluate.StartsWith("i.learn."))         // ADDED THIS
        {
            return new ItemInjectionResult
            {
                FormattedString = contentToEvaluate.StartsWith("i.") ? contentToEvaluate : $"i.{contentToEvaluate}",
                Zone = PayloadInjectionZone.OuterEntity
            };
        }

        // ====================================================================
        // RULE 2: ENCAPSULATION (EntityWrapper Zone)
        // ====================================================================
        if (contentToEvaluate.StartsWith("custom.wrap.") ||
           (item.Mechanics != null && item.Mechanics.Any(m => m.Prefix == "wrap")))
        {
            return new ItemInjectionResult
            {
                FormattedString = contentToEvaluate,
                Zone = PayloadInjectionZone.EntityWrapper
            };
        }

        // ====================================================================
        // RULE 3: MODIFIERS & EQUIPMENT (InnerEntity Zone)
        // ====================================================================
        string formattedInner;
        if (contentToEvaluate.StartsWith("i.") || contentToEvaluate.StartsWith("i.t."))
        {
            formattedInner = contentToEvaluate;
        }
        else if (contentToEvaluate.StartsWith("t."))
        {
            formattedInner = $"i.{contentToEvaluate}";
        }
        else
        {
            formattedInner = $"i.{contentToEvaluate}";
        }

        return new ItemInjectionResult
        {
            FormattedString = formattedInner,
            Zone = PayloadInjectionZone.InnerEntity
        };
    }
}