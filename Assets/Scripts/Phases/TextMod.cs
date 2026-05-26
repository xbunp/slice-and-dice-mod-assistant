/*

// =============================================================================
// SliceDiceTextMod — Parse & Object Model
// =============================================================================
// Covers: Choosables (ch.*), SimpleChoicePhase (ph.!), and all ph.X phases.
// GUI code is NOT included; this file exposes the data model and parse hooks
// that a GUI layer consumes.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    // =========================================================================
    // Delimiters — gathered in one place so they're easy to audit / extend
    // =========================================================================
    internal static class Delimiters
    {
        /// <summary>Top-level entry separator inside a full textmod string.</summary>
        public const string TopLevel = "&";

        /// <summary>Separator between SCPhase / ChoicePhase / TradePhase reward options.</summary>
        public const string AtThree = "@3";

        /// <summary>Or-tag entry separator.</summary>
        public const string AtFour = "@4";

        /// <summary>LinkedPhase / SeqPhase button separator.</summary>
        public const string AtOne = "@1";

        /// <summary>SeqPhase "phase-sequence" separator (what happens after each button).</summary>
        public const string AtTwo = "@2";

        /// <summary>BooleanPhase2 field separator (replaces ';' from BooleanPhase).</summary>
        public const string AtSix = "@6";

        /// <summary>BooleanPhase2 A/B branch separator (replaces '@2' from BooleanPhase).</summary>
        public const string AtSeven = "@7";

        /// <summary>General sub-field separator used by BooleanPhase and some phases.</summary>
        public const string Semicolon = ";";
    }

    // =========================================================================
    // Enumerations
    // =========================================================================

    /// <summary>Reward tag types shared between Choosables and SimpleChoicePhase.</summary>
    public enum TagType
    {
        Modifier,     // ch.m  / ph.!m
        Item,         // ch.i  / ph.!i
        Levelup,      // ch.l  / ph.!l
        Hero,         // ch.g  / ph.!g
        Random,       // ch.r  / ph.!r
        RandomRange,  // ch.q  / ph.!q
        Or,           // ch.o  / ph.!o
        Enu,          // ch.e  / ph.!e
        Value,        // ch.v  / ph.!v
        Replace,      // ch.p  / ph.!p
        Skip          // ch.s  / ph.!s
    }

    /// <summary>The three valid inputs for the Enu tag.</summary>
    public enum EnuVariant
    {
        RandoKeywordT1Item,
        RandoKeywordT5Item,
        RandoKeywordT7Item
    }

    /// <summary>Hard-coded modes for ItemCombinePhase (ph.7).</summary>
    public enum ItemCombineMode
    {
        SecondHighestToTierThrees,
        ZeroToThreeToSingle
    }

    /// <summary>Hero reroll type for HeroChangePhase (ph.5).</summary>
    public enum HeroChangeType
    {
        RandomClass    = 0,
        GeneratedHero  = 1
    }

    /// <summary>What PhaseGeneratorTransformPhase generates (ph.g).</summary>
    public enum PhaseGeneratorMode
    {
        Hero,  // ph.gh
        Item   // ph.gi
    }

    /// <summary>Selector type used by ChoicePhase (ph.c).</summary>
    public enum ChoicePhaseType
    {
        PointBuy,
        Number,
        UpToNumber,
        Optional
    }

    /// <summary>
    /// Built-in phi.# phase indices.
    /// phi. uses a 0-9 integer that maps to one of these standard phase templates.
    /// </summary>
    public enum PhiIndex
    {
        LevelupPhase          = 0,
        StandardLootPhase     = 1,
        RerollPhaseA          = 2,
        RerollPhaseB          = 3,
        OptionalTweakPhase    = 4,
        HeroPositionSwapA     = 5,
        StandardChallengePhase= 6,
        EasyChallengePhase    = 7,
        HeroPositionSwapB     = 8,
        TradePhase            = 9
    }

    // =========================================================================
    // Floor / Level Selector
    // =========================================================================

    /// <summary>
    /// Represents the optional floor-range prefix that can appear before a phase
    /// or choosable, e.g. "1.", "2-5.", "e2.", "e2.1.", "1-20.".
    /// 
    /// Grammar (approximate):
    ///   selector ::= exact | range | every | every-with-offset
    ///   exact    ::= INT '.'
    ///   range    ::= INT '-' INT '.'
    ///   every    ::= 'e' INT '.'
    ///   every-with-offset ::= 'e' INT '.' INT '.'
    /// </summary>
    public class FloorSelector
    {
        // --- Properties -------------------------------------------------------

        /// <summary>Set when the selector targets exactly one floor.</summary>
        public int? ExactFloor { get; set; }

        /// <summary>Inclusive start of a floor range (e.g. 2 in "2-5").</summary>
        public int? RangeStart { get; set; }

        /// <summary>Inclusive end of a floor range (e.g. 5 in "2-5").</summary>
        public int? RangeEnd { get; set; }

        /// <summary>True when the prefix starts with 'e' (every-Nth-floor pattern).</summary>
        public bool IsEvery { get; set; }

        /// <summary>N in "eN" — activate every N floors.</summary>
        public int? EveryN { get; set; }

        /// <summary>Optional offset floor in "eN.offset." patterns.</summary>
        public int? EveryOffset { get; set; }

        // --- Factory / Parse --------------------------------------------------

        private static readonly Regex _everyWithOffset =
            new Regex(@"^e(\d+)\.(\d+)$", RegexOptions.Compiled);
        private static readonly Regex _everyOnly =
            new Regex(@"^e(\d+)$", RegexOptions.Compiled);
        private static readonly Regex _range =
            new Regex(@"^(-?\d+)-(-?\d+)$", RegexOptions.Compiled);
        private static readonly Regex _exact =
            new Regex(@"^(-?\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Attempt to parse a raw floor prefix token (trailing '.' already stripped).
        /// Returns null if the token is not a recognised selector.
        /// </summary>
        public static FloorSelector? TryParse(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            Match m;

            m = _everyWithOffset.Match(token);
            if (m.Success)
                return new FloorSelector
                {
                    IsEvery      = true,
                    EveryN       = int.Parse(m.Groups[1].Value),
                    EveryOffset  = int.Parse(m.Groups[2].Value)
                };

            m = _everyOnly.Match(token);
            if (m.Success)
                return new FloorSelector
                {
                    IsEvery = true,
                    EveryN  = int.Parse(m.Groups[1].Value)
                };

            m = _range.Match(token);
            if (m.Success)
                return new FloorSelector
                {
                    RangeStart = int.Parse(m.Groups[1].Value),
                    RangeEnd   = int.Parse(m.Groups[2].Value)
                };

            m = _exact.Match(token);
            if (m.Success)
                return new FloorSelector { ExactFloor = int.Parse(m.Groups[1].Value) };

            return null;
        }

        /// <summary>Serialise back to raw textmod prefix form (without trailing '.').</summary>
        public string ToRaw()
        {
            if (IsEvery)
                return EveryOffset.HasValue
                    ? $"e{EveryN}.{EveryOffset}"
                    : $"e{EveryN}";

            if (RangeStart.HasValue && RangeEnd.HasValue)
                return $"{RangeStart}-{RangeEnd}";

            if (ExactFloor.HasValue)
                return ExactFloor.Value.ToString();

            return string.Empty;
        }

        public override string ToString() => ToRaw();
    }

    // =========================================================================
    // Reward Tags  (shared between Choosables and SCPhase reward lists)
    // =========================================================================

    /// <summary>
    /// Abstract base for every reward tag.  Concrete subclasses map 1-to-1 to
    /// the tag letters documented in the Choosable/SCPhase table.
    /// </summary>
    public abstract class RewardTag
    {
        public abstract TagType TagType { get; }

        /// <summary>Serialize this tag back to its raw textmod string.</summary>
        public abstract string ToRaw();

        // ---- Static Parse Entry Point ---------------------------------------

        /// <summary>
        /// Parse a single reward-tag string (the part after "ch." or "ph.!" and
        /// the tag letter).  <paramref name="tagChar"/> is the single character
        /// that identifies the tag type; <paramref name="payload"/> is everything
        /// that follows it.
        /// </summary>
        public static RewardTag Parse(char tagChar, string payload)
        {
            return tagChar switch
            {
                'm' => ModifierTag.Parse(payload),
                'i' => ItemTag.Parse(payload),
                'l' => LevelupTag.Parse(payload),
                'g' => HeroTag.Parse(payload),
                'r' => RandomTag.Parse(payload),
                'q' => RandomRangeTag.Parse(payload),
                'o' => OrTag.Parse(payload),
                'e' => EnuTag.Parse(payload),
                'v' => ValueTag.Parse(payload),
                'p' => ReplaceTag.Parse(payload),
                's' => new SkipTag(),
                _   => throw new ArgumentException($"Unknown tag character: '{tagChar}'")
            };
        }
    }

    // -------------------------------------------------------------------------
    // Standard Tags (Modifier, Item, Levelup, Hero)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Grants or designates a modifier.
    /// Raw form: m&lt;modifierNameOrTextmod&gt;
    /// </summary>
    public class ModifierTag : RewardTag
    {
        public override TagType TagType => TagType.Modifier;

        /// <summary>
        /// The modifier specification — may be a plain name ("Wurst"), a
        /// scoped name ("4.fight.Bramble+Rat"), or a full textmod expression.
        /// </summary>
        public string ModifierSpec { get; set; } = string.Empty;

        public static ModifierTag Parse(string payload) =>
            new ModifierTag { ModifierSpec = payload };

        public override string ToRaw() => $"m{ModifierSpec}";
    }

    /// <summary>
    /// Grants an item.
    /// Raw form: i&lt;itemName&gt;
    /// </summary>
    public class ItemTag : RewardTag
    {
        public override TagType TagType => TagType.Item;
        public string ItemName { get; set; } = string.Empty;

        public static ItemTag Parse(string payload) =>
            new ItemTag { ItemName = payload };

        public override string ToRaw() => $"i{ItemName}";
    }

    /// <summary>
    /// Levels up an eligible hero (or the topmost hero if none is eligible).
    /// Raw form: l&lt;levelupName&gt;
    /// </summary>
    public class LevelupTag : RewardTag
    {
        public override TagType TagType => TagType.Levelup;
        public string LevelupName { get; set; } = string.Empty;

        public static LevelupTag Parse(string payload) =>
            new LevelupTag { LevelupName = payload };

        public override string ToRaw() => $"l{LevelupName}";
    }

    /// <summary>
    /// Adds a hero to the party.
    /// Raw form: g&lt;heroNameOrSpec&gt;
    /// Functionally identical to ph.!madd.&lt;hero&gt; when used in a phase.
    /// </summary>
    public class HeroTag : RewardTag
    {
        public override TagType TagType => TagType.Hero;
        public string HeroSpec { get; set; } = string.Empty;

        public static HeroTag Parse(string payload) =>
            new HeroTag { HeroSpec = payload };

        public override string ToRaw() => $"g{HeroSpec}";
    }

    // -------------------------------------------------------------------------
    // Input Tags (Random, RandomRange, Or)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Grants random rewards from Designed entities of a specific tier and type.
    /// Raw form: r&lt;tier&gt;~&lt;count&gt;~&lt;tagChar&gt;
    /// Note: count and tier cannot be changed for l (Levelup) or g (Hero) tags.
    /// </summary>
    public class RandomTag : RewardTag
    {
        public override TagType TagType => TagType.Random;

        public int Tier { get; set; }
        public int Count { get; set; }

        /// <summary>Single character identifying the reward type (m/i/l/g).</summary>
        public char RewardTagChar { get; set; }

        public static RandomTag Parse(string payload)
        {
            // payload: "<tier>~<count>~<tagChar>"
            var parts = payload.Split('~');
            if (parts.Length < 3)
                throw new FormatException($"RandomTag payload malformed: '{payload}'");

            return new RandomTag
            {
                Tier          = int.Parse(parts[0]),
                Count         = int.Parse(parts[1]),
                RewardTagChar = parts[2][0]
            };
        }

        public override string ToRaw() => $"r{Tier}~{Count}~{RewardTagChar}";
    }

    /// <summary>
    /// Grants random rewards from a tier range.
    /// Raw form: q&lt;tierMin&gt;~&lt;tierMax&gt;~&lt;count&gt;~&lt;tagChar&gt;
    /// All rewards in a single call share one randomly-selected tier within the range.
    /// </summary>
    public class RandomRangeTag : RewardTag
    {
        public override TagType TagType => TagType.RandomRange;

        public int TierMin { get; set; }
        public int TierMax { get; set; }
        public int Count { get; set; }
        public char RewardTagChar { get; set; }

        public static RandomRangeTag Parse(string payload)
        {
            var parts = payload.Split('~');
            if (parts.Length < 4)
                throw new FormatException($"RandomRangeTag payload malformed: '{payload}'");

            return new RandomRangeTag
            {
                TierMin       = int.Parse(parts[0]),
                TierMax       = int.Parse(parts[1]),
                Count         = int.Parse(parts[2]),
                RewardTagChar = parts[3][0]
            };
        }

        public override string ToRaw() => $"q{TierMin}~{TierMax}~{Count}~{RewardTagChar}";
    }

    /// <summary>
    /// Picks a random reward from an explicit list.
    /// Raw form: o&lt;reward1&gt;@4&lt;reward2&gt;@4...
    /// Entries may use different tag types and may contain parenthesised sub-expressions.
    /// </summary>
    public class OrTag : RewardTag
    {
        public override TagType TagType => TagType.Or;

        /// <summary>
        /// Each element is itself a raw reward string (tag char + payload), or a
        /// parenthesised modifier block.  The GUI layer can further parse each entry
        /// via <see cref="RewardTag.Parse"/> if needed.
        /// </summary>
        public List<string> Options { get; set; } = new();

        public static OrTag Parse(string payload)
        {
            // Split on @4, respecting nested parentheses
            var options = SplitRespectingParens(payload, Delimiters.AtFour);
            return new OrTag { Options = options };
        }

        public override string ToRaw() => $"o{string.Join(Delimiters.AtFour, Options)}";

        // Shared utility also used by SeqPhase and BooleanPhase parsers
        internal static List<string> SplitRespectingParens(string input, string delimiter)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i <= input.Length - delimiter.Length; i++)
            {
                char c = input[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && input.Substring(i, delimiter.Length) == delimiter)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + delimiter.Length;
                    i += delimiter.Length - 1;
                }
            }
            result.Add(input.Substring(start));
            return result;
        }
    }

    // -------------------------------------------------------------------------
    // Enu Tag
    // -------------------------------------------------------------------------

    /// <summary>
    /// Grants a random keyword item scoped to a specific side set.
    /// Raw form: e&lt;EnuVariant&gt;
    /// Only three valid inputs: RandoKeywordT1Item, RandoKeywordT5Item, RandoKeywordT7Item.
    /// </summary>
    public class EnuTag : RewardTag
    {
        public override TagType TagType => TagType.Enu;
        public EnuVariant Variant { get; set; }

        public static EnuTag Parse(string payload)
        {
            if (!Enum.TryParse<EnuVariant>(payload.Trim(), out var variant))
                throw new ArgumentException($"Unknown EnuVariant: '{payload}'");

            return new EnuTag { Variant = variant };
        }

        public override string ToRaw() => $"e{Variant}";
    }

    // -------------------------------------------------------------------------
    // Value Tag
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds or subtracts from a named hidden variable.
    /// Raw form: v&lt;variableName&gt;V&lt;amount&gt;
    /// The capital 'V' acts as a separator between the name and the numeric delta.
    /// </summary>
    public class ValueTag : RewardTag
    {
        public override TagType TagType => TagType.Value;

        public string VariableName { get; set; } = string.Empty;

        /// <summary>Amount added (may be negative to subtract).</summary>
        public int Amount { get; set; }

        public static ValueTag Parse(string payload)
        {
            // payload: "<variableName>V<amount>"
            int sepIdx = payload.LastIndexOf('V');
            if (sepIdx < 1)
                throw new FormatException($"ValueTag payload malformed: '{payload}'");

            return new ValueTag
            {
                VariableName = payload.Substring(0, sepIdx),
                Amount       = int.Parse(payload.Substring(sepIdx + 1))
            };
        }

        public override string ToRaw() => $"v{VariableName}V{Amount}";
    }

    // -------------------------------------------------------------------------
    // Replace Tag
    // -------------------------------------------------------------------------

    /// <summary>
    /// Removes a modifier from the player's collection and grants a different reward.
    /// Raw form: p m&lt;modifierToRemove&gt;~&lt;rewardTag&gt;
    /// Only modifiers can be replaced; the granted reward can be any Standard tag type.
    /// </summary>
    public class ReplaceTag : RewardTag
    {
        public override TagType TagType => TagType.Replace;

        /// <summary>Name/spec of the modifier to remove.</summary>
        public string ModifierToRemove { get; set; } = string.Empty;

        /// <summary>
        /// The raw reward string to grant (e.g. "gPaladin", "iCharged Skull").
        /// Parse with <see cref="RewardTag.Parse"/> for a typed object.
        /// </summary>
        public string GrantedRewardRaw { get; set; } = string.Empty;

        public static ReplaceTag Parse(string payload)
        {
            // payload starts with 'm', then modifier name, then '~', then reward
            // e.g.  "mWurst~gPaladin"
            if (payload.Length == 0 || payload[0] != 'm')
                throw new FormatException($"ReplaceTag must start with 'm': '{payload}'");

            string afterM = payload.Substring(1);
            int tildeIdx = afterM.IndexOf('~');
            if (tildeIdx < 0)
                throw new FormatException($"ReplaceTag missing '~' separator: '{payload}'");

            return new ReplaceTag
            {
                ModifierToRemove  = afterM.Substring(0, tildeIdx),
                GrantedRewardRaw  = afterM.Substring(tildeIdx + 1)
            };
        }

        public override string ToRaw() => $"pm{ModifierToRemove}~{GrantedRewardRaw}";
    }

    // -------------------------------------------------------------------------
    // Skip Tag
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents an empty "skip" option — grants nothing.
    /// Raw form: s  (no payload)
    /// </summary>
    public class SkipTag : RewardTag
    {
        public override TagType TagType => TagType.Skip;
        public override string ToRaw() => "s";
    }

    // =========================================================================
    // Choosable  (ch.X …)
    // =========================================================================

    /// <summary>
    /// A Choosable grants a reward directly (no pop-up screen).
    /// Raw form: [floorSelector.]ch.&lt;tagChar&gt;&lt;payload&gt;
    /// 
    /// Unlike SCPhase, Choosables cannot be conditionally activated by floor
    /// (lvl.ch does not work); use SCPhase's lvl.ph for floor-based activation.
    /// </summary>
    public class Choosable
    {
        public FloorSelector? FloorSelector { get; set; }
        public RewardTag Reward { get; set; } = new SkipTag();

        // ---- Parse -----------------------------------------------------------

        /// <summary>
        /// Parse a single textmod entry that is a Choosable.
        /// <paramref name="raw"/> should be the full entry string after outer
        /// '&amp;' splitting, e.g. "2.ch.r1~2~m" or "ch.mWurst".
        /// </summary>
        public static Choosable Parse(string raw)
        {
            var choosable = new Choosable();
            string remainder = ParserHelpers.StripFloorSelector(raw, out FloorSelector? selector);

            // Expect "ch.<tagChar><payload>"
            if (!remainder.StartsWith("ch.", StringComparison.Ordinal))
                throw new FormatException($"Not a valid Choosable: '{raw}'");

            if (remainder.Length < 4)
                throw new FormatException($"Choosable tag char missing: '{raw}'");

            char tagChar = remainder[3];
            string payload = remainder.Substring(4);
            choosable.Reward = RewardTag.Parse(tagChar, payload);
            return choosable;
        }

        // ---- Serialize -------------------------------------------------------

        public string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ch.{Reward.ToRaw()}";
        }

        public static bool IsChoosable(string entry) =>
            Regex.IsMatch(entry, @"(^|[0-9e\-]+\.)ch\.");
    }

    // =========================================================================
    // Phases  (ph.X …)
    // =========================================================================

    /// <summary>
    /// Abstract base for all ph.X phases.
    /// Every concrete phase knows its own type code and how to parse/serialize itself.
    /// </summary>
    public abstract class Phase
    {
        public FloorSelector? FloorSelector { get; set; }

        /// <summary>
        /// The single character (or '!' for SCPhase) that follows "ph." in the
        /// raw string.
        /// </summary>
        public abstract string PhaseCode { get; }

        /// <summary>Serialize back to a raw textmod string.</summary>
        public abstract string ToRaw();

        // ---- Static Dispatch -------------------------------------------------

        /// <summary>
        /// Parse any phase entry.  <paramref name="raw"/> is the full entry string
        /// (after '&amp;' splitting), e.g. "1.ph.4Hello World".
        /// </summary>
        public static Phase Parse(string raw)
        {
            string remainder = ParserHelpers.StripFloorSelector(raw, out FloorSelector? fs);

            if (!remainder.StartsWith("ph.", StringComparison.Ordinal))
                throw new FormatException($"Not a valid phase: '{raw}'");

            string afterPh = remainder.Substring(3); // everything after "ph."
            if (afterPh.Length == 0)
                throw new FormatException($"Phase code missing: '{raw}'");

            // The phase code is either '!' (two-char: "!") or a single character.
            string code    = afterPh[0] == '!' ? "!" : afterPh[0].ToString();
            string payload = afterPh.Substring(code.Length);

            Phase phase = code switch
            {
                "!" => SimpleChoicePhase.Parse(payload),
                "0" => new PlayerRollingPhase   { RawArgs = payload },
                "1" => new TargetingPhase        { RawArgs = payload },
                "2" => LevelEndPhase.Parse(payload),
                "3" => new EnemyRollingPhase     { RawArgs = payload },
                "4" => MessagePhase.Parse(payload),
                "5" => HeroChangePhase.Parse(payload),
                "6" => new ResetPhase(),
                "7" => ItemCombinePhase.Parse(payload),
                "8" => PositionSwapPhase.Parse(payload),
                "9" => ChallengePhase.Parse(payload),
                "b" => BooleanPhase.Parse(payload),
                "c" => ChoicePhase.Parse(payload),
                "d" => new DamagePhase           { RawArgs = payload },
                "e" => new RunEndPhase(),
                "g" => PhaseGeneratorPhase.Parse(payload),
                "l" => LinkedPhase.Parse(payload),
                "r" => RandomRevealPhase.Parse(payload),
                "s" => SeqPhase.Parse(payload),
                "t" => TradePhase.Parse(payload),
                "z" => BooleanPhase2.Parse(payload),
                _   => throw new ArgumentException($"Unknown phase code: '{code}' in '{raw}'")
            };

            phase.FloorSelector = fs;
            return phase;
        }

        public static bool IsPhase(string entry) =>
            Regex.IsMatch(entry, @"(^|[0-9e\-]+\.)ph\.");
    }

    // =========================================================================
    // Concrete Phase Implementations
    // =========================================================================

    // ---- ph.!  SimpleChoicePhase --------------------------------------------

    /// <summary>
    /// Presents the player with a reward selection screen.
    /// Raw form: ph.!&lt;optionalTitle&gt;;&lt;reward1&gt;@3&lt;reward2&gt;@3...
    /// The icon and format of the choice screen are determined by the first reward tag.
    /// </summary>
    public class SimpleChoicePhase : Phase
    {
        public override string PhaseCode => "!";

        /// <summary>Optional title shown at the top of the choice screen.</summary>
        public string? Title { get; set; }

        /// <summary>
        /// The ordered list of reward options the player may choose from.
        /// Each entry is a raw reward string; parse with <see cref="RewardTag.Parse"/>
        /// for a typed representation.
        /// </summary>
        public List<string> Options { get; set; } = new();

        public static SimpleChoicePhase Parse(string payload)
        {
            var phase = new SimpleChoicePhase();

            // Check for an optional "Title;" prefix
            int semiIdx = payload.IndexOf(';');
            // Only treat as a title if the semicolon appears before any @3
            int at3Idx = payload.IndexOf(Delimiters.AtThree, StringComparison.Ordinal);
            if (semiIdx >= 0 && (at3Idx < 0 || semiIdx < at3Idx))
            {
                phase.Title = payload.Substring(0, semiIdx);
                payload = payload.Substring(semiIdx + 1);
            }

            phase.Options = OrTag.SplitRespectingParens(payload, Delimiters.AtThree);
            return phase;
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string titlePart = Title != null ? Title + ";" : "";
            return $"{prefix}ph.!{titlePart}{string.Join(Delimiters.AtThree, Options)}";
        }
    }

    // ---- ph.0/1/3/d  Combat Phases ------------------------------------------

    /// <summary>
    /// ph.0 — PlayerRollingPhase.  Rarely used as a modifier; stored as raw args.
    /// </summary>
    public class PlayerRollingPhase : Phase
    {
        public override string PhaseCode => "0";
        public string RawArgs { get; set; } = string.Empty;
        public override string ToRaw() =>
            $"{(FloorSelector != null ? FloorSelector.ToRaw() + "." : "")}ph.0{RawArgs}";
    }

    /// <summary>ph.1 — TargetingPhase.</summary>
    public class TargetingPhase : Phase
    {
        public override string PhaseCode => "1";
        public string RawArgs { get; set; } = string.Empty;
        public override string ToRaw() =>
            $"{(FloorSelector != null ? FloorSelector.ToRaw() + "." : "")}ph.1{RawArgs}";
    }

    /// <summary>ph.3 — EnemyRollingPhase.</summary>
    public class EnemyRollingPhase : Phase
    {
        public override string PhaseCode => "3";
        public string RawArgs { get; set; } = string.Empty;
        public override string ToRaw() =>
            $"{(FloorSelector != null ? FloorSelector.ToRaw() + "." : "")}ph.3{RawArgs}";
    }

    /// <summary>ph.d — DamagePhase.</summary>
    public class DamagePhase : Phase
    {
        public override string PhaseCode => "d";
        public string RawArgs { get; set; } = string.Empty;
        public override string ToRaw() =>
            $"{(FloorSelector != null ? FloorSelector.ToRaw() + "." : "")}ph.d{RawArgs}";
    }

    // ---- ph.2  LevelEndPhase ------------------------------------------------

    /// <summary>
    /// ph.2 — LevelEndPhase.  Container for other phases displayed between levels.
    /// Raw form: ph.2{ps:[&lt;phase1&gt;,&lt;phase2&gt;,...]}
    /// Note: In Custom mode, only 1 phase is supported.
    /// </summary>
    public class LevelEndPhase : Phase
    {
        public override string PhaseCode => "2";

        /// <summary>Phases nested inside this LevelEndPhase, as raw strings.</summary>
        public List<string> InnerPhasesRaw { get; set; } = new();

        public static LevelEndPhase Parse(string payload)
        {
            // payload: "{ps:[phase1,phase2]}" — extract between '[' and ']'
            var phase = new LevelEndPhase();
            var match = Regex.Match(payload, @"\{ps:\[(.+?)\]\}");
            if (match.Success)
                phase.InnerPhasesRaw = new List<string>(match.Groups[1].Value.Split(','));
            return phase;
        }

        public override string ToRaw()
        {
            string prefix  = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string inner   = string.Join(",", InnerPhasesRaw);
            return $"{prefix}ph.2{{ps:[{inner}]}}";
        }
    }

    // ---- ph.4  MessagePhase -------------------------------------------------

    /// <summary>
    /// ph.4 — Displays a message with an optional custom button label.
    /// Raw form: ph.4&lt;message&gt;;&lt;buttonText&gt;
    /// Supports color codes ([orange], [yellow], etc.), entity images ([Thief]),
    /// newlines ([n]), and value interpolation ([val&lt;varName&gt;]).
    /// </summary>
    public class MessagePhase : Phase
    {
        public override string PhaseCode => "4";

        public string Message { get; set; } = string.Empty;

        /// <summary>Defaults to "ok" if null.</summary>
        public string? ButtonText { get; set; }

        public static MessagePhase Parse(string payload)
        {
            int semiIdx = payload.IndexOf(';');
            return semiIdx >= 0
                ? new MessagePhase
                  {
                      Message    = payload.Substring(0, semiIdx),
                      ButtonText = payload.Substring(semiIdx + 1)
                  }
                : new MessagePhase { Message = payload };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string btn    = ButtonText != null ? $";{ButtonText}" : "";
            return $"{prefix}ph.4{Message}{btn}";
        }
    }

    // ---- ph.5  HeroChangePhase ----------------------------------------------

    /// <summary>
    /// ph.5 — Offers the player the option to reroll or replace a hero.
    /// Raw form: ph.5&lt;heroIndex&gt;&lt;changeType&gt;
    /// Hero index is 0-based from the top; changeType is 0 (random class) or 1 (generated).
    /// </summary>
    public class HeroChangePhase : Phase
    {
        public override string PhaseCode => "5";

        /// <summary>0-based index of the hero to change (0 = top hero).</summary>
        public int HeroIndex { get; set; }

        public HeroChangeType ChangeType { get; set; }

        public static HeroChangePhase Parse(string payload)
        {
            if (payload.Length < 2)
                throw new FormatException($"HeroChangePhase payload too short: '{payload}'");

            return new HeroChangePhase
            {
                HeroIndex  = int.Parse(payload[0].ToString()),
                ChangeType = (HeroChangeType)int.Parse(payload[1].ToString())
            };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.5{HeroIndex}{(int)ChangeType}";
        }
    }

    // ---- ph.6  ResetPhase ---------------------------------------------------

    /// <summary>
    /// ph.6 — Triggers the Cursed-mode reset screen: de-levels all heroes to 1
    /// and removes non-modifier items.  No payload.
    /// </summary>
    public class ResetPhase : Phase
    {
        public override string PhaseCode => "6";

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.6";
        }
    }

    // ---- ph.7  ItemCombinePhase ---------------------------------------------

    /// <summary>
    /// ph.7 — Smiths or smashes items.
    /// Raw form: ph.7&lt;ItemCombineMode&gt;
    /// </summary>
    public class ItemCombinePhase : Phase
    {
        public override string PhaseCode => "7";
        public ItemCombineMode Mode { get; set; }

        public static ItemCombinePhase Parse(string payload)
        {
            if (!Enum.TryParse<ItemCombineMode>(payload.Trim(), out var mode))
                throw new ArgumentException($"Unknown ItemCombineMode: '{payload}'");

            return new ItemCombinePhase { Mode = mode };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.7{Mode}";
        }
    }

    // ---- ph.8  PositionSwapPhase --------------------------------------------

    /// <summary>
    /// ph.8 — Offers the player the option to swap two heroes' positions.
    /// Raw form: ph.8&lt;heroA&gt;&lt;heroB&gt;
    /// Hero indices are 0-based from the top.
    /// </summary>
    public class PositionSwapPhase : Phase
    {
        public override string PhaseCode => "8";
        public int HeroIndexA { get; set; }
        public int HeroIndexB { get; set; }

        public static PositionSwapPhase Parse(string payload)
        {
            if (payload.Length < 2)
                throw new FormatException($"PositionSwapPhase payload too short: '{payload}'");

            return new PositionSwapPhase
            {
                HeroIndexA = int.Parse(payload[0].ToString()),
                HeroIndexB = int.Parse(payload[1].ToString())
            };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.8{HeroIndexA}{HeroIndexB}";
        }
    }

    // ---- ph.9  ChallengePhase -----------------------------------------------

    /// <summary>
    /// ph.9 — Offers additional enemies in exchange for pre-combat rewards.
    /// Raw form: ph.9{"reward":{"data":"…"},"type":{"extraMonsters":["…",…]}}
    /// Reward entries are @3-delimited; enemy names are JSON array elements.
    /// </summary>
    public class ChallengePhase : Phase
    {
        public override string PhaseCode => "9";

        /// <summary>Raw @3-delimited reward data string (before JSON encoding).</summary>
        public string RewardDataRaw { get; set; } = string.Empty;

        /// <summary>Enemy names offered in the challenge.</summary>
        public List<string> ExtraMonsters { get; set; } = new();

        public static ChallengePhase Parse(string payload)
        {
            // Extract reward data
            var rewardMatch   = Regex.Match(payload, "\"reward\":\\{\"data\":\"(.+?)\"\\}");
            var monstersMatch = Regex.Match(payload, "\"extraMonsters\":\\[(.+?)\\]");

            var phase = new ChallengePhase();

            if (rewardMatch.Success)
                phase.RewardDataRaw = rewardMatch.Groups[1].Value;

            if (monstersMatch.Success)
            {
                string monsterList = monstersMatch.Groups[1].Value;
                // Strip quotes and split on comma
                phase.ExtraMonsters = new List<string>(
                    monsterList.Replace("\\\"", "\"")
                               .Trim('"')
                               .Split(new[] { "\",\"" }, StringSplitOptions.None));
            }

            return phase;
        }

        public override string ToRaw()
        {
            string prefix  = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string monsters = string.Join("\",\"", ExtraMonsters);
            return $"{prefix}ph.9{{\"reward\":{{\"data\":\"{RewardDataRaw}\"}},\"type\":{{\"extraMonsters\":[\"{monsters}\"]}}}}";
        }
    }

    // ---- ph.b  BooleanPhase -------------------------------------------------

    /// <summary>
    /// ph.b — Compares a Value variable against a threshold and branches.
    /// Raw form: ph.b&lt;varName&gt;;&lt;threshold&gt;;&lt;phaseA&gt;@2&lt;phaseB&gt;
    /// If value ≥ threshold → phaseA; otherwise → phaseB.
    /// Chained BooleanPhases must be ordered from highest threshold to lowest.
    /// </summary>
    public class BooleanPhase : Phase
    {
        public override string PhaseCode => "b";

        public string VariableName { get; set; } = string.Empty;
        public int Threshold { get; set; }

        /// <summary>Raw phase string executed when value ≥ threshold.</summary>
        public string PhaseIfTrue { get; set; } = string.Empty;

        /// <summary>Raw phase string executed when value &lt; threshold.</summary>
        public string PhaseIfFalse { get; set; } = string.Empty;

        public static BooleanPhase Parse(string payload)
        {
            // payload: "<varName>;<threshold>;<phaseA>@2<phaseB>"
            var parts = payload.Split(new[] { Delimiters.Semicolon }, 3,
                                      StringSplitOptions.None);
            if (parts.Length < 3)
                throw new FormatException($"BooleanPhase payload malformed: '{payload}'");

            var branches = parts[2].Split(new[] { Delimiters.AtTwo }, 2,
                                          StringSplitOptions.None);
            return new BooleanPhase
            {
                VariableName = parts[0],
                Threshold    = int.Parse(parts[1]),
                PhaseIfTrue  = branches[0],
                PhaseIfFalse = branches.Length > 1 ? branches[1] : string.Empty
            };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.b{VariableName};{Threshold};{PhaseIfTrue}{Delimiters.AtTwo}{PhaseIfFalse}";
        }
    }

    // ---- ph.z  BooleanPhase2 ------------------------------------------------

    /// <summary>
    /// ph.z — Identical to BooleanPhase but uses @6 and @7 as delimiters,
    /// allowing it to be nested alongside or inside a BooleanPhase.
    /// Raw form: ph.z&lt;varName&gt;@6&lt;threshold&gt;@6&lt;phaseA&gt;@7&lt;phaseB&gt;
    /// </summary>
    public class BooleanPhase2 : Phase
    {
        public override string PhaseCode => "z";

        public string VariableName { get; set; } = string.Empty;
        public int Threshold { get; set; }
        public string PhaseIfTrue  { get; set; } = string.Empty;
        public string PhaseIfFalse { get; set; } = string.Empty;

        public static BooleanPhase2 Parse(string payload)
        {
            // payload: "<varName>@6<threshold>@6<phaseA>@7<phaseB>"
            var fields = payload.Split(new[] { Delimiters.AtSix }, 3,
                                       StringSplitOptions.None);
            if (fields.Length < 3)
                throw new FormatException($"BooleanPhase2 payload malformed: '{payload}'");

            var branches = fields[2].Split(new[] { Delimiters.AtSeven }, 2,
                                           StringSplitOptions.None);
            return new BooleanPhase2
            {
                VariableName = fields[0],
                Threshold    = int.Parse(fields[1]),
                PhaseIfTrue  = branches[0],
                PhaseIfFalse = branches.Length > 1 ? branches[1] : string.Empty
            };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.z{VariableName}{Delimiters.AtSix}{Threshold}{Delimiters.AtSix}{PhaseIfTrue}{Delimiters.AtSeven}{PhaseIfFalse}";
        }
    }

    // ---- ph.c  ChoicePhase --------------------------------------------------

    /// <summary>
    /// ph.c — Like SimpleChoicePhase but applies a selection-count rule.
    /// Raw form: ph.c&lt;ChoiceType&gt;#&lt;number&gt;;&lt;reward1&gt;@3&lt;reward2&gt;@3...
    /// </summary>
    public class ChoicePhase : Phase
    {
        public override string PhaseCode => "c";

        public ChoicePhaseType SelectionType { get; set; }

        /// <summary>
        /// The numeric argument to the selection type (e.g. point budget, pick count).
        /// Ignored for Optional.
        /// </summary>
        public int SelectionNumber { get; set; }

        /// <summary>Raw @3-delimited reward option strings.</summary>
        public List<string> Options { get; set; } = new();

        public static ChoicePhase Parse(string payload)
        {
            // payload: "PointBuy#-20;mSandstorm^3@3mPoison Tendrils…"
            int hashIdx = payload.IndexOf('#');
            int semiIdx = payload.IndexOf(';');
            if (hashIdx < 0 || semiIdx < 0 || semiIdx <= hashIdx)
                throw new FormatException($"ChoicePhase payload malformed: '{payload}'");

            string typeName = payload.Substring(0, hashIdx);
            string numStr   = payload.Substring(hashIdx + 1, semiIdx - hashIdx - 1);
            string options  = payload.Substring(semiIdx + 1);

            if (!Enum.TryParse<ChoicePhaseType>(typeName, out var selType))
                throw new ArgumentException($"Unknown ChoicePhaseType: '{typeName}'");

            return new ChoicePhase
            {
                SelectionType   = selType,
                SelectionNumber = int.Parse(numStr),
                Options         = OrTag.SplitRespectingParens(options, Delimiters.AtThree)
            };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string opts   = string.Join(Delimiters.AtThree, Options);
            return $"{prefix}ph.c{SelectionType}#{SelectionNumber};{opts}";
        }
    }

    // ---- ph.e  RunEndPhase --------------------------------------------------

    /// <summary>ph.e — Ends the run immediately. No payload.</summary>
    public class RunEndPhase : Phase
    {
        public override string PhaseCode => "e";

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.e";
        }
    }

    // ---- ph.g  PhaseGeneratorPhase ------------------------------------------

    /// <summary>
    /// ph.g — Generates a standard hero levelup (ph.gh) or item reward (ph.gi) phase.
    /// Useful for embedding generated reward screens inside other phases.
    /// </summary>
    public class PhaseGeneratorPhase : Phase
    {
        public override string PhaseCode => "g";
        public PhaseGeneratorMode Mode { get; set; }

        public static new PhaseGeneratorPhase Parse(string payload)
        {
            string trimmed = payload.TrimStart();

            return trimmed switch
            {
                string s when s.StartsWith("h") => new PhaseGeneratorPhase { Mode = PhaseGeneratorMode.Hero },
                string s when s.StartsWith("i") => new PhaseGeneratorPhase { Mode = PhaseGeneratorMode.Item },
                _ => throw new ArgumentException($"Unknown PhaseGeneratorMode payload: '{payload}'")
            };
        }

        public override string ToRaw()
        {
            string prefix    = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            string modeChar  = Mode == PhaseGeneratorMode.Hero ? "h" : "i";
            return $"{prefix}ph.g{modeChar}";
        }
    }

    // ---- ph.l  LinkedPhase --------------------------------------------------

    /// <summary>
    /// ph.l — Chains two or more phases to execute sequentially.
    /// Raw form: ph.l&lt;phaseA&gt;@1&lt;phaseB&gt;[@1&lt;phaseC&gt;...]
    /// The last phase in the chain omits the leading "l" prefix.
    /// </summary>
    public class LinkedPhase : Phase
    {
        public override string PhaseCode => "l";

        /// <summary>Raw phase strings, in order.  Parse each with <see cref="Phase.Parse"/>.</summary>
        public List<string> LinkedPhasesRaw { get; set; } = new();

        public static LinkedPhase Parse(string payload)
        {
            var parts = OrTag.SplitRespectingParens(payload, Delimiters.AtOne);
            return new LinkedPhase { LinkedPhasesRaw = parts };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.l{string.Join(Delimiters.AtOne, LinkedPhasesRaw)}";
        }
    }

    // ---- ph.r  RandomRevealPhase --------------------------------------------

    /// <summary>
    /// ph.r — Shows a popup revealing a reward (does NOT actually grant it).
    /// Raw form: ph.r&lt;rewardTag&gt;
    /// </summary>
    public class RandomRevealPhase : Phase
    {
        public override string PhaseCode => "r";

        /// <summary>
        /// The raw reward tag string shown in the popup.
        /// Parse with <see cref="RewardTag.Parse"/> for a typed object.
        /// </summary>
        public string RewardRaw { get; set; } = string.Empty;

        public static RandomRevealPhase Parse(string payload) =>
            new RandomRevealPhase { RewardRaw = payload };

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.r{RewardRaw}";
        }
    }

    // ---- ph.s  SeqPhase -----------------------------------------------------

    /// <summary>
    /// ph.s — Presents an initial message then a set of buttons, each leading to
    /// a distinct phase sequence.
    /// Raw form: ph.s&lt;message&gt;@1&lt;button1&gt;@2&lt;phase1a&gt;@2&lt;phase1b&gt;@1&lt;button2&gt;@2...
    /// </summary>
    public class SeqPhase : Phase
    {
        public override string PhaseCode => "s";

        public string Message { get; set; } = string.Empty;

        public List<SeqOption> Options { get; set; } = new();

        public static SeqPhase Parse(string payload)
        {
            // Split on @1 first (buttons + their phase sequences)
            var segments = OrTag.SplitRespectingParens(payload, Delimiters.AtOne);
            if (segments.Count < 1)
                throw new FormatException($"SeqPhase payload malformed: '{payload}'");

            var phase = new SeqPhase { Message = segments[0] };

            for (int i = 1; i < segments.Count; i++)
            {
                // Each segment: "<buttonText>@2<phase1>@2<phase2>..."
                var parts = OrTag.SplitRespectingParens(segments[i], Delimiters.AtTwo);
                var option = new SeqOption { ButtonText = parts[0] };
                for (int j = 1; j < parts.Count; j++)
                    option.PhaseSequenceRaw.Add(parts[j]);
                phase.Options.Add(option);
            }

            return phase;
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            var sb = new System.Text.StringBuilder($"{prefix}ph.s{Message}");
            foreach (var opt in Options)
            {
                sb.Append(Delimiters.AtOne);
                sb.Append(opt.ButtonText);
                foreach (var p in opt.PhaseSequenceRaw)
                {
                    sb.Append(Delimiters.AtTwo);
                    sb.Append(p);
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>One button + its resulting phase sequence inside a <see cref="SeqPhase"/>.</summary>
    public class SeqOption
    {
        public string ButtonText { get; set; } = string.Empty;

        /// <summary>Ordered raw phase strings that execute when this button is chosen.</summary>
        public List<string> PhaseSequenceRaw { get; set; } = new();
    }

    // ---- ph.t  TradePhase ---------------------------------------------------

    /// <summary>
    /// ph.t — Cursed chest: player either accepts or declines all listed rewards together.
    /// Raw form: ph.t&lt;reward1&gt;@3&lt;reward2&gt;@3...
    /// Functionally identical to ChoicePhase Optional.
    /// </summary>
    public class TradePhase : Phase
    {
        public override string PhaseCode => "t";

        /// <summary>Raw @3-delimited reward strings (may include curses as negative-tier rewards).</summary>
        public List<string> RewardsRaw { get; set; } = new();

        public static TradePhase Parse(string payload)
        {
            var rewards = OrTag.SplitRespectingParens(payload, Delimiters.AtThree);
            return new TradePhase { RewardsRaw = rewards };
        }

        public override string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}ph.t{string.Join(Delimiters.AtThree, RewardsRaw)}";
        }
    }

    // =========================================================================
    // phi.# and phmp.  — Convenience Phase Generators
    // =========================================================================

    /// <summary>
    /// phi.# — Generates a standard phase template every floor (or on specific floors
    /// when combined with a FloorSelector / lvl.phi.# prefix).
    /// Raw form: [floorSelector.]phi.&lt;index&gt;
    /// </summary>
    public class PhiPhase
    {
        public FloorSelector? FloorSelector { get; set; }
        public PhiIndex Index { get; set; }

        public static PhiPhase Parse(string raw)
        {
            var phi = new PhiPhase();
            string remainder = ParserHelpers.StripFloorSelector(raw, out FloorSelector? selector);

            if (!remainder.StartsWith("phi.", StringComparison.Ordinal))
                throw new FormatException($"Not a valid phi phase: '{raw}'");

            phi.Index = (PhiIndex)int.Parse(remainder.Substring(4));
            return phi;
        }

        public string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}phi.{(int)Index}";
        }

        public static bool IsPhiPhase(string entry) =>
            Regex.IsMatch(entry, @"(^|[0-9e\-]+\.)phi\.");
    }

    /// <summary>
    /// phmp.+- — Creates a modifier selection screen where the total tiers must
    /// reach the specified point value (mirrors the difficulty-selection screen).
    /// Raw form: phmp.&lt;pointValue&gt;
    /// A negative value (e.g. phmp.-10) creates an unfair modifier pick screen.
    /// </summary>
    public class PhmpPhase
    {
        public FloorSelector? FloorSelector { get; set; }
        public int PointValue { get; set; }

        public static PhmpPhase Parse(string raw)
        {
            var phmp = new PhmpPhase();
            string remainder = ParserHelpers.StripFloorSelector(raw, out FloorSelector? selector);


            if (!remainder.StartsWith("phmp.", StringComparison.Ordinal))
                throw new FormatException($"Not a valid phmp phase: '{raw}'");

            phmp.PointValue = int.Parse(remainder.Substring(5));
            return phmp;
        }

        public string ToRaw()
        {
            string prefix = FloorSelector != null ? FloorSelector.ToRaw() + "." : "";
            return $"{prefix}phmp.{PointValue}";
        }

        public static bool IsPhmpPhase(string entry) =>
            Regex.IsMatch(entry, @"(^|[0-9e\-]+\.)phmp\.");
    }

    // =========================================================================
    // TextModEntry  — one '&'-separated element of a full textmod string
    // =========================================================================

    /// <summary>
    /// Discriminated union representing a single entry in a textmod string.
    /// Exactly one of <see cref="Phase"/>, <see cref="Choosable"/>,
    /// <see cref="PhiPhase"/>, <see cref="PhmpPhase"/>, or <see cref="RawText"/>
    /// will be non-null after parsing.
    /// </summary>
    public class TextModEntry
    {
        // Mutually exclusive content slots
        public Phase?      Phase     { get; set; }
        public Choosable?  Choosable { get; set; }
        public PhiPhase?   PhiPhase  { get; set; }
        public PhmpPhase?  PhmpPhase { get; set; }

        /// <summary>
        /// Fallback for entries that are plain modifier text, hero pools, party
        /// declarations, or anything not yet parsed by this library.
        /// </summary>
        public string? RawText { get; set; }

        /// <summary>Convenience: which slot is populated?</summary>
        public TextModEntryKind Kind =>
            Phase     != null ? TextModEntryKind.Phase     :
            Choosable != null ? TextModEntryKind.Choosable :
            PhiPhase  != null ? TextModEntryKind.PhiPhase  :
            PhmpPhase != null ? TextModEntryKind.PhmpPhase :
                                TextModEntryKind.Raw;

        // ---- Parse -----------------------------------------------------------

        /// <summary>
        /// Parse one '&amp;'-delimited textmod entry.
        /// Throws <see cref="FormatException"/> only for clearly malformed phase/choosable
        /// syntax; everything unrecognised falls into <see cref="RawText"/>.
        /// </summary>
        public static TextModEntry Parse(string raw)
        {
            raw = raw.Trim();

            try
            {
                if (PhiPhase.IsPhiPhase(raw))
                    return new TextModEntry { PhiPhase = SliceDiceTextMod.PhiPhase.Parse(raw) };

                if (PhmpPhase.IsPhmpPhase(raw))
                    return new TextModEntry { PhmpPhase = SliceDiceTextMod.PhmpPhase.Parse(raw) };

                if (Phase.IsPhase(raw))
                    return new TextModEntry { Phase = SliceDiceTextMod.Phase.Parse(raw) };

                if (Choosable.IsChoosable(raw))
                    return new TextModEntry { Choosable = SliceDiceTextMod.Choosable.Parse(raw) };
            }
            catch (Exception ex)
            {
                // Degrade gracefully: unknown syntax stays as raw text
                // The GUI layer can surface the parse error via ValidationErrors.
                return new TextModEntry
                {
                    RawText = raw,
                    ParseError = ex.Message
                };
            }

            return new TextModEntry { RawText = raw };
        }

        // ---- Serialize -------------------------------------------------------

        public string ToRaw() => Kind switch
        {
            TextModEntryKind.Phase     => Phase!.ToRaw(),
            TextModEntryKind.Choosable => Choosable!.ToRaw(),
            TextModEntryKind.PhiPhase  => PhiPhase!.ToRaw(),
            TextModEntryKind.PhmpPhase => PhmpPhase!.ToRaw(),
            _                          => RawText ?? string.Empty
        };

        /// <summary>Non-null when the entry was stored as RawText due to a parse failure.</summary>
        public string? ParseError { get; private set; }
    }

    public enum TextModEntryKind { Phase, Choosable, PhiPhase, PhmpPhase, Raw }

    // =========================================================================
    // TextMod  — top-level object
    // =========================================================================

    /// <summary>
    /// A fully-parsed Slice &amp; Dice textmod string.
    /// The top-level document is a '&amp;'-separated list of <see cref="TextModEntry"/> objects.
    /// </summary>
    public class TextMod
    {
        public List<TextModEntry> Entries { get; set; } = new();

        /// <summary>
        /// Any entries that could not be parsed are kept in their raw form;
        /// this list surfaces their error messages for the GUI to display.
        /// </summary>
        public IEnumerable<(int Index, string Error)> ValidationErrors
        {
            get
            {
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i].ParseError != null)
                        yield return (i, Entries[i].ParseError!);
            }
        }

        // ---- Parse -----------------------------------------------------------

        /// <summary>
        /// Main entry point.  Parse a complete textmod string (as pasted into the
        /// game's Custom Mode or Paste field) into a structured object graph.
        /// </summary>
        public static TextMod ParseRawText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return new TextMod();

            // Strip a leading '=' if present (Paste mode prefix)
            rawText = rawText.TrimStart();
            if (rawText.StartsWith("="))
                rawText = rawText.Substring(1);

            var tm = new TextMod();
            var rawEntries = SplitTopLevel(rawText);

            foreach (var entry in rawEntries)
                if (!string.IsNullOrWhiteSpace(entry))
                    tm.Entries.Add(TextModEntry.Parse(entry));

            return tm;
        }

        // ---- Serialize -------------------------------------------------------

        /// <summary>Serialize the entire textmod back to a raw string.</summary>
        public string ToRaw() =>
            string.Join(Delimiters.TopLevel, Entries.ConvertAll(e => e.ToRaw()));

        // ---- Helpers ---------------------------------------------------------

        /// <summary>
        /// Split a textmod string on '&amp;' while ignoring '&amp;' inside parentheses.
        /// This prevents splitting e.g. "(ph.4msg)&amp;(ph.4msg2)" incorrectly.
        /// </summary>
        private static List<string> SplitTopLevel(string input)
        {
            var result = new List<string>();
            int depth = 0, start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == '&' && depth == 0)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(input.Substring(start));
            return result;
        }
    }

    // =========================================================================
    // Shared static helpers (used by Phase, Choosable, PhiPhase, PhmpPhase)
    // =========================================================================

    // These are file-scoped static helpers referenced from multiple classes above.
    // Placed at file scope via a static extension class for clarity.

    public static class ParserHelpers
    {
        /// <summary>
        /// If <paramref name="raw"/> begins with a floor-selector prefix
        /// (e.g. "2-5." or "e3."), strips it and returns the remainder.
        /// <paramref name="selector"/> is set to the parsed selector, or null.
        /// </summary>
        public static string StripFloorSelector(string raw, out FloorSelector? selector)
        {
            selector = null;

            // Greedily match a floor selector prefix: digits / 'e' / '-' followed by '.'
            // Accept patterns like:  "1.", "2-5.", "e2.", "e2.1."
            var match = Regex.Match(raw, @"^(e\d+\.\d+|e\d+|\-?\d+\-\-?\d+|\-?\d+)\.");
            if (!match.Success) return raw;

            selector = FloorSelector.TryParse(match.Groups[1].Value);
            return raw.Substring(match.Length);
        }
    }

    // Make the helpers accessible from Phase, Choosable, etc. via a using alias
    // or by calling ParserHelpers.StripFloorSelector directly.
    // For convenience, expose a file-level shim:
    internal static class TopLevelShim
    {
        internal static string StripFloorSelector(string raw, out FloorSelector? fs)
            => ParserHelpers.StripFloorSelector(raw, out fs);
    }
}
// End of file — GUI hooks are the properties/methods on each class above.
// The GUI layer should consume:
//   TextMod.ParseRawText(string)    → entry point
//   TextMod.ToRaw()                 → round-trip serialization
//   TextMod.Entries                 → list of typed entries to display/edit
//   TextMod.ValidationErrors        → highlight parse failures
//   Each concrete Phase / RewardTag → data for form fields

/*

*/