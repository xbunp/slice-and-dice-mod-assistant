using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ==========================================
// INTERNAL AST (Strictly for parsing string structure)
// ==========================================
public abstract class ASTNode { public abstract string Export(); }

[System.Serializable]
public class ASTParser
{
    private enum TokenType { Identifier, Dot, Hash, LParen, RParen, EOF }
    private class Token { public TokenType Type; public string Value; }

    private readonly string input;
    private int pos;

    public ASTParser(string input) { this.input = input ?? ""; this.pos = 0; }

    private Token NextToken()
    {
        if (pos >= input.Length) return new Token { Type = TokenType.EOF };

        char c = input[pos];
        if (c == '.') { pos++; return new Token { Type = TokenType.Dot }; }
        if (c == '#') { pos++; return new Token { Type = TokenType.Hash }; }
        if (c == '(') { pos++; return new Token { Type = TokenType.LParen }; }
        if (c == ')') { pos++; return new Token { Type = TokenType.RParen }; }

        int start = pos;
        int bracketDepth = 0, braceDepth = 0;

        while (pos < input.Length)
        {
            char curr = input[pos];
            if (curr == '[') bracketDepth++;
            else if (curr == ']') bracketDepth--;
            else if (curr == '{') braceDepth++;
            else if (curr == '}') braceDepth--;
            else if (bracketDepth == 0 && braceDepth == 0)
            {
                if (curr == '.' || curr == '#' || curr == '(' || curr == ')') break;
            }
            pos++;
        }
        return new Token { Type = TokenType.Identifier, Value = input.Substring(start, pos - start) };
    }

    private List<Token> Tokenize()
    {
        List<Token> tokens = new List<Token>();
        while (true) { var t = NextToken(); tokens.Add(t); if (t.Type == TokenType.EOF) break; }
        return tokens;
    }

    public ASTNode Parse()
    {
        var tokens = Tokenize();
        int index = 0;
        return ParseSequence(tokens, ref index);
    }

    private ASTNode ParseSequence(List<Token> tokens, ref int index)
    {
        SequenceNode seq = new SequenceNode();
        while (index < tokens.Count && tokens[index].Type != TokenType.EOF && tokens[index].Type != TokenType.RParen)
        {
            seq.Items.Add(ParseChain(tokens, ref index));
            if (index < tokens.Count && tokens[index].Type == TokenType.Hash) index++;
        }
        return seq.Items.Count == 1 ? seq.Items[0] : seq;
    }

    private ASTNode ParseChain(List<Token> tokens, ref int index)
    {
        ChainNode chain = new ChainNode();
        while (index < tokens.Count && tokens[index].Type != TokenType.EOF && tokens[index].Type != TokenType.RParen && tokens[index].Type != TokenType.Hash)
        {
            if (tokens[index].Type == TokenType.LParen)
            {
                index++;
                ASTNode content = ParseSequence(tokens, ref index);
                if (index < tokens.Count && tokens[index].Type == TokenType.RParen) index++;
                ASTNode scopeNode = new ScopeNode(content);
                if (index < tokens.Count && tokens[index].Type == TokenType.Identifier)
                {
                    scopeNode = new CompositeNode(scopeNode, tokens[index].Value);
                    index++;
                }
                chain.Elements.Add(scopeNode);
            }
            else if (tokens[index].Type == TokenType.Identifier) { chain.Elements.Add(new StringNode(tokens[index].Value)); index++; }
            else if (tokens[index].Type == TokenType.Dot) index++;
            else index++;
        }
        return chain.Elements.Count == 1 ? chain.Elements[0] : chain;
    }
}

[System.Serializable]
public class StringNode : ASTNode
{
    public string Value { get; set; }
    public StringNode(string value) => Value = value;
    public override string Export() => Value;
}

[System.Serializable]
public class ScopeNode : ASTNode
{
    public ASTNode Content { get; set; }
    public ScopeNode(ASTNode content) => Content = content;
    public override string Export() => Content != null ? $"({Content.Export()})" : "()";
}

[System.Serializable]
public class CompositeNode : ASTNode
{
    public ASTNode Left { get; set; }
    public string Suffix { get; set; }
    public CompositeNode(ASTNode left, string suffix) { Left = left; Suffix = suffix; }
    public override string Export() => $"{Left.Export()}{Suffix}";
}

[System.Serializable]
public class ChainNode : ASTNode
{
    public List<ASTNode> Elements { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join(".", Elements.Select(e => e.Export()));
}

[System.Serializable]
public class SequenceNode : ASTNode
{
    public List<ASTNode> Items { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join("#", Items.Select(e => e.Export()));
}

// ==========================================
// ITEM DOMAIN DICTIONARY (Factual Rules)
// ==========================================
public static class ItemDomainRules
{
    // Extracted strictly from SDSyntaxBrain ItemPropertyKeys + EntityCommonPropertyKeys
    public static readonly HashSet<string> ValidItemProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "k", "learn", "hat", "t", "sidepos", "tier", "n", "ritem", "ritemx", "facade",
        "mrg", "self", "m", "doc", "pertier", "part", "rditem", "unpack", "sidesc",
        "splice", "onhitdata", "triggerhpdata", "sticker", "enchant", "cast", "img",
        "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "summon", "cleardesc",
        "clearicon", "oi", "t1", "t2"
    };

    // Targets explicitly allowed after an inherent "i." modifier
    public static readonly HashSet<string> ValidTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "all", "self", "right5", "right3", "right2", "row", "mid2", "col", "topbot",
        "left2", "rightmost", "right", "bot", "top", "mid", "left", "k", "t"
    };

    // Keys that hold complex, nested strings/entities and should NOT immediately terminate parsing
    public static readonly HashSet<string> ContainerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "triggerhpdata", "onhitdata", "learn", "unpack", "splice", "abilitydata",
        "peritem", "allitem", "alliteme", "sticker", "enchant", "cast", "mrg", "hat"
    };

    // Common nested properties that do not break a flat dot-chain
    public static readonly HashSet<string> NonBreakingSubKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "hp", "i", "k", "hsv", "hsl", "draw", "rect", "p", "b", "t", "sticker", "cast", "sidesc", "splice", "m"
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

    /// <summary>
    /// Checks if a token matches a base item name, a procedural item, or a structural Tog item.
    /// </summary>
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
        "hat", "onhitdata", "triggerhpdata"
    };

    public static bool IsRepeatPrefix(string token, out int count)
    {
        count = 1;
        if (string.IsNullOrEmpty(token) || char.ToLower(token[0]) != 'x') return false;
        return int.TryParse(token.Substring(1), out count);
    }
}

// ==========================================
// UNIVERSAL DATA WRAPPER
// ==========================================
[System.Serializable]
public class ItemProperty
{
    public string Key { get; set; }
    public string Value { get; set; }

    public ItemProperty(string k, string v)
    {
        Key = k;
        Value = v;
    }
}

// ==========================================
// COMPOSITE MECHANIC ENGINE
// ==========================================
[System.Serializable]
public class ItemMechanic
{
    public List<string> Targets = new List<string>(); // e.g., left, topbot, mid
    public string Prefix = ""; // e.g., i, sd, k, t
    public string PayloadString = ""; // Raw nested string (e.g., facade.bas1)

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

    // UI BUILDERS
    public ItemMechanic AddTarget(string target) { Targets.Add(target); return this; }
    public ItemMechanic SetPrefix(string pre) { Prefix = pre; return this; }
    public ItemMechanic SetPayload(string payload) { PayloadString = payload; return this; }

    public string Export()
    {
        List<string> parts = new List<string>();

        // 1. Targets
        if (Targets.Count > 0) parts.AddRange(Targets);

        // 2. Pre-Modifiers
        if (RepeatTimes != 1) parts.Add($"x{RepeatTimes}");
        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");

        // 3. Prefix & Core Payload
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);

        string corePayload = PayloadString;
        if (ChainedKeywords.Count > 0) corePayload += "#" + string.Join("#", ChainedKeywords);
        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);

        // 4. Suffixes
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) parts.Add($"m{Multiplier}");
        if (!string.IsNullOrEmpty(MergedItem)) parts.Add($"mrg.{MergedItem}");
        if (!string.IsNullOrEmpty(SplicedItem)) parts.Add($"splice.{SplicedItem}");

        return string.Join(".", parts);
    }
}

// ======================================================================================
// EXTERNAL REGISTRY PLACEHOLDERS (Loudly flagged for your existing dictionaries/enums)
// ======================================================================================
/* 
 * LOUD WARNING FOR LATER AI / COMPILER:
 * DO NOT REDEFINE THESE TYPES. They exist in the primary game engine dictionaries.
 * Replace the placeholder types below with references to your actual project assemblies.
 */
public static class ExternalGameRegistry
{
    public static bool IsValidSprite(string atlasId) => true; // TODO: Link to project's Sprite Dictionary

    public static bool IsValidKeyword(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return Enum.TryParse<EffectKeyword>(key, true, out _);
    }

    public static bool IsValidAbility(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return BaseAbilityDatabase.ValidAbilities.Contains(id);
    }

    public static bool IsValidItemName(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // "Dead Crow" -> "DeadCrow"
        string normalizedToken = token.Replace(" ", "");

        return Enum.TryParse<BaseItems>(normalizedToken, true, out _);
    }
}

// ==========================================
// DETAILED METADATA COMPONENT STRUCTURES
// ==========================================
[System.Serializable]
public struct ItemHsvShift
{
    public int Hue;        // Range: -99 to 99
    public int Saturation; // Range: -99 to 99
    public int Value;      // Range: -99 to 99

    public ItemHsvShift(int h, int s, int v)
    {
        Hue = Math.Clamp(h, -99, 99);
        Saturation = Math.Clamp(s, -99, 99);
        Value = Math.Clamp(v, -99, 99);
    }
}

// ==========================================
// CORE ITEM DATA KNOWLEDGE BASE
// ==========================================
[System.Serializable]
public class ItemData : SDData
{
    public List<string> GlobalTags = new List<string>();

    public ItemData() : base()
    {
    }

    // Constructor
    public ItemData(string data) : this()
    {
        ((SDData)this).Parse(data);
    }

    // Static Factory
    public static ItemData Create(string data)
    {
        var item = new ItemData();
        ((SDData)item).Parse(data);
        return item;
    }

    // --- EQUIPPABLE ITEM METADATA ---
    // (Only populated if the item functions as an inventory/equippable object)

    /// <summary> (.n) Plaintext name for the item. </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> (.tier) Rarity reward pool index. Valid range: -5 to 20. </summary>
    public int? Tier { get; set; }

    /// <summary> (.doc) Rich text description of the item's custom mechanics or use. </summary>
    public string DocumentedDescription { get; set; } = string.Empty;

    /// <summary> (.img) Reference to a game atlas sprite, or a base64.png string. </summary>
    public string ImageReference { get; set; } = string.Empty;

    /// <summary> (.hsv) Direct Hue, Saturation, and Value shifting values (-99 to 99). </summary>
    public ItemHsvShift? HsvShift { get; set; }

    /// <summary> (.hue) Simple single-axis hue shift value. </summary>
    public int? SimpleHue { get; set; }

    /// <summary> (.thue) Targeted hue shift applied only to a specific range of colors. Format: "fff:##:##" </summary>
    public string TargetedHue { get; set; } = string.Empty;

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

    /// <summary> (cleardesc) item Suppresses the game's auto-generated description of an item's effect. </summary>
    public bool ClearDescription { get; set; }

    /// <summary> (clearicon) item Suppresses the game's auto-generated item graphics. </summary>
    public bool ClearIcon { get; set; }

    // --- MECHANICAL CONTAINERS ---
    public List<ItemProperty> Containers = new List<ItemProperty>();
    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();

    /// <summary>
    /// Evaluates whether this item is a true equippable inventory item or simply a mechanical payload (e.g., 'i.k.cleave').
    /// </summary>
    public bool IsEquippable => !string.IsNullOrEmpty(Name) || Tier.HasValue;

    public override void Parse(string data)
    {
        UnityEngine.Debug.Log($"[ItemData] Starting Parse on data length: {data?.Length}");

        GlobalTags.Clear();
        PropertiesClear();
        Containers.Clear();
        Mechanics.Clear();

        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = TopLevelSplit(data.Trim(), '&');
        string itemCore = chunks[0];

        // Process hidden/temporary global tags
        for (int c = 1; c < chunks.Count; c++)
        {
            List<string> hiddenTokens = TopLevelSplit(chunks[c], '.');
            if (hiddenTokens.Count > 0)
            {
                string tag = hiddenTokens[0].ToLower();
                if (tag == "hidden" || tag == "temporary")
                {
                    GlobalTags.Add(hiddenTokens[0]);
                }
            }
        }

        ASTNode root = new ASTParser(itemCore).Parse();
        ExtractKnowledge(ref root, this);

        UnityEngine.Debug.Log($"[ItemData] Parse Complete! Name: '{Name}', Tier: '{Tier}', Mechanics: {Mechanics.Count}");
    }

    private void PropertiesClear()
    {
        Name = string.Empty;
        Tier = null;
        DocumentedDescription = string.Empty;
        ImageReference = string.Empty;
        HsvShift = null;
        SimpleHue = null;
        TargetedHue = string.Empty;
        PaletteOverride = string.Empty;
        BorderColorCode = string.Empty;
        UiDrawInstructions = string.Empty;
        UiRectInstructions = string.Empty;
        ClearDescription = false;
        ClearIcon = false;
    }

    private void ExtractKnowledge(ref ASTNode node, ItemData item)
    {
        if (node is ChainNode chain)
        {
            for (int i = 0; i < chain.Elements.Count; i++)
            {
                string token = chain.Elements[i].Export().ToLower();

                // Match Metadata Keywords
                switch (token)
                {
                    case "n":
                        if (i + 1 < chain.Elements.Count) item.Name = chain.Elements[++i].Export();
                        break;
                    case "tier":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[i + 1].Export(), out int t))
                        {
                            item.Tier = t;
                            i++;
                        }
                        break;
                    case "doc":
                    case "sidesc": // Synonymous description keys
                        if (i + 1 < chain.Elements.Count) item.DocumentedDescription = chain.Elements[++i].Export();
                        break;
                    case "img":
                        if (i + 1 < chain.Elements.Count) item.ImageReference = chain.Elements[++i].Export();
                        break;
                    case "hsv":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string[] hsvParts = chain.Elements[++i].Export().Split(':');
                            if (hsvParts.Length == 3 &&
                                int.TryParse(hsvParts[0], out int h) &&
                                int.TryParse(hsvParts[1], out int s) &&
                                int.TryParse(hsvParts[2], out int v))
                            {
                                item.HsvShift = new ItemHsvShift(h, s, v);
                            }
                        }
                        break;
                    case "hue":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[i + 1].Export(), out int hueVal))
                        {
                            item.SimpleHue = hueVal;
                            i++;
                        }
                        break;
                    case "thue":
                        if (i + 1 < chain.Elements.Count) item.TargetedHue = chain.Elements[++i].Export();
                        break;
                    case "p":
                        if (i + 1 < chain.Elements.Count) item.PaletteOverride = chain.Elements[++i].Export();
                        break;
                    case "b":
                        if (i + 1 < chain.Elements.Count) item.BorderColorCode = chain.Elements[++i].Export();
                        break;
                    case "draw":
                        if (i + 1 < chain.Elements.Count) item.UiDrawInstructions = chain.Elements[++i].Export();
                        break;
                    case "rect":
                        if (i + 1 < chain.Elements.Count) item.UiRectInstructions = chain.Elements[++i].Export();
                        break;
                    case "learn":
                        if (i + 1 < chain.Elements.Count) item.LearnedAbilities.Add(chain.Elements[++i].Export());
                        break;
                    case "abilitydata":
                        if (i + 1 < chain.Elements.Count) item.CustomAbilities.Add(chain.Elements[++i].Export());
                        break;
                    case "t":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string traitPayload = chain.Elements[++i].Export();
                            // Handle standard nested syntax like: t.jinx.modifierName
                            if (traitPayload.Equals("jinx", StringComparison.OrdinalIgnoreCase) && i + 1 < chain.Elements.Count)
                            {
                                traitPayload += "." + chain.Elements[++i].Export();
                            }
                            item.PassiveTraits.Add(traitPayload);
                        }
                        break;
                    case "self":
                        if (i + 1 < chain.Elements.Count) item.SelfModifiers.Add(chain.Elements[++i].Export());
                        break;

                    case "cleardesc":
                        item.ClearDescription = true;
                        break;
                    case "clearicon":
                        item.ClearIcon = true;
                        break;

                    default:
                        if (TryProcessGenericContainer(chain, ref i, token))
                        {
                            // Handled successfully as a standard metadata container
                        }
                        else if (IsMechanicTriggerToken(token))
                        {
                            ProcessMechanicChain(chain, ref i, token);
                        }
                        else if (chain.Elements[i] is ScopeNode isolatedScope)
                        {
                            // FIX: Unpack isolated scopes that might be wrapping the entire item/valid chained data
                            UnityEngine.Debug.Log($"[ItemData] Unpacking isolated ScopeNode to find trapped properties.");
                            ASTNode inner = isolatedScope.Content;
                            ExtractKnowledge(ref inner, item);
                        }
                        else if (chain.Elements[i] is CompositeNode compNode && compNode.Left is ScopeNode compScope)
                        {
                            // FIX: Unpack isolated Composite nodes as well
                            UnityEngine.Debug.Log($"[ItemData] Unpacking isolated CompositeNode to find trapped properties.");
                            ASTNode inner = compScope.Content;
                            ExtractKnowledge(ref inner, item);
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"[ItemData] Unhandled token dropped during extraction: {token}");
                        }
                        break;
                }
            }
        }
        else if (node is ScopeNode scope)
        {
            ASTNode inner = scope.Content;
            ExtractKnowledge(ref inner, item);
        }
        else if (node is SequenceNode seq)
        {
            foreach (var itemNode in seq.Items)
            {
                ASTNode temp = itemNode;
                ExtractKnowledge(ref temp, item);
            }
        }
    }

    /// <summary>
    /// Processes a single mechanical dot-chain, extracting prefixes, targets, payloads, and suffixes.
    /// </summary>
    private void ProcessMechanicChain(ChainNode chain, ref int i, string initialToken)
    {
        ItemMechanic mech = new ItemMechanic();
        string token = initialToken;

        // PHASE 1: Walk the chain gathering targets and pre-modifiers until locking the payload
        while (i < chain.Elements.Count)
        {
            string tLower = chain.Elements[i].Export().ToLower();

            if (ItemDomainRules.ValidTargets.Contains(tLower))
            {
                mech.AddTarget(chain.Elements[i].Export());
            }
            else if (ItemDomainRules.IsRepeatPrefix(tLower, out int reps))
            {
                mech.RepeatTimes = reps;
            }
            else if (tLower == "pertier")
            {
                mech.PerTier = true;
            }
            else if (tLower == "unpack")
            {
                mech.Unpack = true;
            }
            else if (ItemDomainRules.MechanicPrefixes.Contains(tLower))
            {
                mech.Prefix = tLower;
                // Grab the next immediate element as the payload (e.g. the "(Thief.sd.27-2)" entity block)
                if (i + 1 < chain.Elements.Count)
                {
                    mech.PayloadString = chain.Elements[++i].Export();
                }
                break; // Payload acquired, proceed to Phase 2
            }
            else
            {
                // Raw fallback: This is the payload name itself (e.g. "Stoneskin")
                mech.PayloadString = chain.Elements[i].Export();
                break; // Payload acquired, proceed to Phase 2
            }
            i++;
        }

        // PHASE 2: Look-ahead for trailing modification suffixes (part, m, mrg, splice)
        while (i + 1 < chain.Elements.Count)
        {
            string nextToken = chain.Elements[i + 1].Export().ToLower();

            if (nextToken == "part" && i + 2 < chain.Elements.Count)
            {
                if (int.TryParse(chain.Elements[i + 2].Export(), out int pIdx))
                {
                    mech.PartIndex = pIdx;
                    i += 2;
                }
                else break;
            }
            else if (nextToken.StartsWith("m") && int.TryParse(nextToken.Substring(1), out int mult))
            {
                mech.Multiplier = mult;
                i++;
            }
            else if (nextToken == "mrg" && i + 2 < chain.Elements.Count)
            {
                mech.MergedItem = chain.Elements[i + 2].Export();
                i += 2;
            }
            else if (nextToken == "splice" && i + 2 < chain.Elements.Count)
            {
                mech.SplicedItem = chain.Elements[i + 2].Export();
                i += 2;
            }
            else break; // Suffix parsing complete
        }

        Mechanics.Add(mech);
    }

    public override string Export()
    {
        List<string> chainParts = new List<string>();

        // Reconstruct metadata
        if (!string.IsNullOrEmpty(Name)) chainParts.Add($"n.{Name}");
        if (Tier.HasValue) chainParts.Add($"tier.{Tier.Value}");
        if (!string.IsNullOrEmpty(DocumentedDescription)) chainParts.Add($"doc.{DocumentedDescription}");
        if (!string.IsNullOrEmpty(ImageReference)) chainParts.Add($"img.{ImageReference}");
        if (HsvShift.HasValue) chainParts.Add($"hsv.{HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");
        if (SimpleHue.HasValue) chainParts.Add($"hue.{SimpleHue.Value}");
        if (!string.IsNullOrEmpty(TargetedHue)) chainParts.Add($"thue.{TargetedHue}");
        if (!string.IsNullOrEmpty(PaletteOverride)) chainParts.Add($"p.{PaletteOverride}");
        if (!string.IsNullOrEmpty(BorderColorCode)) chainParts.Add($"b.{BorderColorCode}");
        if (!string.IsNullOrEmpty(UiDrawInstructions)) chainParts.Add($"draw.{UiDrawInstructions}");
        if (!string.IsNullOrEmpty(UiRectInstructions)) chainParts.Add($"rect.{UiRectInstructions}");
        if (ClearDescription) chainParts.Add("cleardesc");
        if (ClearIcon) chainParts.Add("clearicon");

        // Reconstruct payloads and mechanics
        foreach (var cont in Containers) chainParts.Add($"{cont.Key}.({cont.Value})");
        foreach (var mech in Mechanics) chainParts.Add(mech.Export());

        string core = string.Join(".", chainParts);

        StringBuilder sb = new StringBuilder(core);
        foreach (var tag in GlobalTags) sb.Append($"&{tag}");

        return sb.ToString();
    }

    public static List<string> TopLevelSplit(string input, char separator)
    {
        List<string> result = new List<string>();
        int p = 0, b = 0, br = 0, start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '[') b++;
            else if (c == ']') b--;
            else if (c == '{') br++;
            else if (c == '}') br--;
            else if (c == separator && p == 0 && b == 0 && br == 0)
            {
                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }
        }
        result.Add(input.Substring(start));
        return result;
    }

    /// <summary>
    /// Checks if a fallback token is an unmapped container key and grabs its payload.
    /// Safely ignores containers that are explicitly handled as Mechanic Prefixes.
    /// </summary>
    private bool TryProcessGenericContainer(ChainNode chain, ref int i, string token)
    {
        if (ItemDomainRules.ContainerKeys.Contains(token) &&
            !ItemDomainRules.MechanicPrefixes.Contains(token))
        {
            if (i + 1 < chain.Elements.Count)
            {
                Containers.Add(new ItemProperty(token, chain.Elements[++i].Export()));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a token should trigger the creation of a mechanical rule.
    /// </summary>
    private bool IsMechanicTriggerToken(string token)
    {
        return ItemDomainRules.MechanicPrefixes.Contains(token) ||
               token == "pertier" || token == "unpack" ||
               ItemDomainRules.ValidTargets.Contains(token) ||
               ItemDomainRules.IsItemIdentifier(token) ||
               ItemDomainRules.IsRepeatPrefix(token, out _);
    }
}