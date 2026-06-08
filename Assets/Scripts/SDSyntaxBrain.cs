using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    /// <summary>
    /// THE SLICE & DICE SYNTAX BRAIN.
    /// -----------------------------------------------------------------------------------------
    /// GRAMMAR RULES:
    /// 1. Top-Level Delimiters: 
    ///    - ',' (comma) separates different independent list elements or objects (such as exported pools).
    ///    - '&' (ampersand) chains modifiers/attributes sequentially within a single definition or line.
    ///    - '#' (hash) acts as a structural delimiter to combine multiple item modifiers globally, 
    ///      and serves as a terminal boundary during property extraction.
    ///    - Supports stripping a leading '=' expression indicator when parsing raw strings.
    /// 2. Wrappers: '()' group elements. 
    ///    - Supports recursive parenthetical nesting for replicas, triggers, and nested property definitions.
    ///    - Supports both parenthesized hidden modifier wrappers like '!m()' and inline, parentheseless 
    ///      hidden modifier prefixes like '!mmodifier' or '!mparty'. Supports generalized alphanumeric 
    ///      hidden prefixes (e.g., '!go0.271' or '!mh.top').
    /// 3. Floor Selectors: Prepend directives to restrict them to specific floors.
    ///    - Exact: "4.fight.Rat" (Floor 4)
    ///    - Range: "1-5.add.Rat" (Floors 1 through 5)
    ///    - Every/Offset: "e2.1.ph.1" (Every 2 floors, offset 1 -> 1, 3, 5, 7)
    ///    - Enclosed: Handles optional wrapper parentheses symmetrically: e.g., "(1-3.monsterpool.(...))"
    ///      maintains balanced parenthesis stripping across the expression.
    /// 4. Pools & Injections: Target game lists using prefix + '.' + '+'-delimited items.
    ///    - e.g., "heropool.Thief+Fighter", "fight.Rat+Boar", "add.Goblin"
    ///    - Supports pool expression splitting using the balance-aware '+' operator.
    /// 5. Phases: Trigger logic on specific game phases. Uses "ph." or "phmp." + Code.
    ///    - e.g., "ph.4Message;Button", "ph.bY;1;!mheropool.(...)", "phmp.0", "phmp.-1"
    ///    - Avoids greedy consumption of run-on message text by matching short alphanumeric phase bounds.
    ///    - Correctly recognizes negative sub-phase configurations containing negative signs.
    ///    - Handles conditional branching targets (e.g., "@2bChestLvlTwo" or "@2!m") separately from UI menus.
    /// 6. Choosables / Rewards: Starts with "ch." followed by tag and payload.
    ///    - Supports multi-character prefixes like "ch.pm" (Permanent Modifier), "ch.oi" (Choice Item), and "ch.r" (Random reward).
    ///    - Supports random colored reward prefixes: rgreen, rblue, rred, ryellow, rorange, rgrey, rgray, rpurple, rcyan.
    ///    - Parsed parameter structures evaluate tilde-delimited formats: e.g., "r1~2~i" (Tier 1, Qty 2, Item).
    ///    - Handles rewards defined either standalone or nested within "ch." prefixes, supporting expanded
    ///      class/color codes (Yellow, Blue, Green, and Grey/None).
    /// 7. Interactive UI Choice Menus: Matches option indicators "@1 Label" and binds them structurally.
    ///    - Distinguishes options from actions using an odd/even marker distribution rule. Odd markers (e.g., @1, @3) 
    ///      initialize new standard or sibling choice selections, while even markers (e.g., @2, @4) map to actions.
    ///    - Strips whitelisted option layout suffixes (l, s, b, r, p, lb) to ensure standard text labels and 
    ///      implicit payloads (e.g., colored item/hero coordinates) are preserved.
    /// 8. Entity Properties: Custom heroes/items chain properties together with dots or open parentheses.
    ///    - e.g., "replica.Statue.n.NewHero.hp.10.tier.3" or "draw.(Sapphire Skull...)"
    ///    - Flat-nested sub-properties (e.g., inside 'triggerhpdata' or 'onhitdata') evaluate structural
    ///      non-breaking whitelists to prevent premature dot-chain breaks on nested sub-entity values.
    ///    - Tracks balanced parenthesis depths, square brackets, and curly braces to avoid premature truncation.
    ///    - Terminates extraction properly on choice indicator markers (`@`), structural equal signs (`=`), and hashes (`#`).
    /// 9. Dice Sides: "sd.Eff-Pips:Eff-Pips..." (6 sides). '0' means blank.
    ///    - Supports pipless/flat side effects, e.g., "sd.43:170-3:0:0:0:0" (effect 43 with 0 pips).
    /// 10. Dice and Inherent Modifiers: ".i.{targets}.{payload}".
    ///     - Targets = left, mid, self, all, topbot, left2, rightmost, right, bot, top, mid, left.
    ///     - Supports inherent item/hero modifications and tags using target codes "k" (keywords) and "t" (tags).
    ///     - Payloads are '#' separated: "k.acidic#facade.bas1"
    ///     - Payloads are '#' separated: "k.acidic#facade.bas1"
    /// 11. Formatting, Color, & Animation Tags: Automatically classifies and handles non-syntactic aesthetic markup.
    ///     - Supports structural color rules (including pink, ultragrey, and hsl/hsv settings), bold formatting tags, 
    ///       icons, keyword containers, and animation/style tags.
    ///     - Strips custom bracketed drawing and sprite strings containing periods and colons (e.g. `[Void.rect.03021111:d61]`).
    ///     - Translates "[comma]" sequences to literal commas during sanitization.
    ///     - Translates "[n]" markup into structural literal newlines to preserve formatted text layouts.
    ///     - Translates "[dot]" markup into literal periods.
    /// 12. Modifier Levels: Supports modifier intensity levels and stacks using the `^` indicator (e.g., `hurried^2` for 
    ///     Hurried level 2) as well as inline division `/`.
    /// -----------------------------------------------------------------------------------------
    /// </summary>
    public static class SDSyntaxBrain
    {
        // ======================================================================================
        // 1. CONSTANTS & DELIMITERS
        // ======================================================================================
        public const char TopLevelDelimiter = '&';
        public const char ListDelimiter = ',';
        public const char PoolDelimiter = '+';
        public const char DiceSideDelimiter = ':';
        public const char DiceModDelimiter = '#';

        public static readonly char[] AllDelimiters = { TopLevelDelimiter, ListDelimiter };

        public static readonly string[] GlobalCommands = {
            "Delevel", "Level Up", "No Flee", "skip all", "skip", "temporary",
            "Wish", "Clear Party", "Missing", "Hidden", "Add Fight",
            "Add 10 Fights", "Add 100 Fights", "Minus Fight", "Cursemode Loopdiff", "horde",
            "double monsters", "skip rewards"
        };

        // Side positions/targets
        public static readonly string[] SideposDefs = { "all", "self", "right5", "right3", "right2", "row", "mid2", "col", "topbot", "left2", "rightmost", "right", "bot", "top", "mid", "left", "k", "t" };

        // Entities
        public static readonly string[] HeroPropertyKeys =
        {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata"
        };

        public static readonly string[] MonsterPropertyKeys = { "i", "rmon", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "triggerhpdata" };
        public static readonly string[] ItemPropertyKeys = { "k", "learn", "hat", "t", "#", "sidepos", "tier", "n", "ritem", "ritemx", "facade", "mrg", "self", "m", "doc", "pertier", "part", "rditem", "unpack", "sidesc", "splice", "onhitdata", "triggerhpdata", "sticker", "enchant", "cast", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "summon", "cleardesc", "clearicon", "oi", "t1", "t2" };
        public static readonly string[] EntityCommonPropertyKeys = { "img", "n", "doc", "p", "t", "b", "rect", "thue" };

        // Directives
        public static readonly string[] ModifierPropertyKeys = { "hero", "add", "party", "diff", "modifier", "lvl", "t#", "heropool", "itempool", "monsterpool", "monster", "allitem", "alliteme", "fight", "mn", "heropos", "ph", "phi", "phmp", "e", "et#", "inv", "part", "zone", "delivery", "rmod", "pl", "pb", "pt", "peritem", "spirit", "ch", "rdmod", "temporary", "hidden", "missing", "skip", "skip all", "minus fight", "clear party", "wish", "cursemode loopdiff", "mch", "m#", "x#", "dabble", "bal", "ea", "summon", "rdhero", "modtier", "tm", "tmi", "tmrdmod", "sthief", "statue", "pockets", "dancer", "t", "rgreen", "rblue", "rred", "ryellow", "rorange", "rgrey", "rgray", "rpurple", "rcyan", "t1", "t2" };
        public static readonly string[] Togitems = { "togtime", "togtarg", "togfri", "togvis", "togeft", "togpip", "togkey", "togorf", "togunt", "togres" };

        public static readonly string[] AllPropertyKeys = HeroPropertyKeys
            .Union(MonsterPropertyKeys)
            .Union(ItemPropertyKeys)
            .Union(EntityCommonPropertyKeys)
            .Union(ModifierPropertyKeys)
            .Union(SideposDefs)
            .Distinct()
            .ToArray();

        // ======================================================================================
        // 2. MASTER REGEX PATTERNS & REGISTRATION
        // ======================================================================================

        private static string CleanKeyForRegex(string key)
        {
            string escaped = Regex.Escape(key);
            if (escaped.Contains("#"))
            {
                return escaped.Replace("#", @"\d+");
            }
            return escaped;
        }

        public static readonly Regex MasterSyntaxRegex;
        private static Regex FormattingTagRegex;

        private static readonly object RegexLock = new object();
        private static readonly List<string> BaseFormattingTags = new List<string>
        {
            // Full color names
            "orange", "yellow", "grey", "gray", "red", "blue", "green", "purple", "cyan",
            "sea", "euish", "white", "kuish", "uuish", "violet", "huish", "mahogany",
            "lime", "tuish", "zuish", "amber", "iuish", "quish", "xuish", "fuish", "juish",
            "blurple", "brown", "dark", "light", "pink", "ultragrey",

            // Single-character and secondary color short codes
            "o", "y", "g", "r", "b", "n", "p", "c", "s", "e", "w", "k", "u", "v", "h", "m", "l", "t", "z", "a", "i", "q", "x", "f", "j", "nh",

            // Layout, formatting, and effect tags
            "sin", "wiggle", "cu", "nokeyword", "hp-plus", "pips", "pipsk", "minus", "weird", "fullheart", "secret", "text", "dot",

            // Tooltip and inline custom icons
            "cog", "confirmSkull", "mana", "info", "checkbox", "checkboxTicked", "hash", "plusfive", "equals", "tick", "tinyDice", "plus",
            "roso", "zablocki", "hp", "hp-hole", "hp-arrow_up", "hp-arrow_left", "hp-girder", "hp-cross", "hp-square", "hp-diamond",
            "hp-bar", "hp-bracket", "hp-glider", "hp-reverse", "mysteryVoice"
        };

        private static readonly List<string> RegisteredItemNames = new List<string>();

        static SDSyntaxBrain()
        {
            string keysPattern = string.Join("|", AllPropertyKeys.Where(k => k != "&" && k != "#").Select(CleanKeyForRegex));

            MasterSyntaxRegex = new Regex(
                @"(?<=^|[(&@!~\[])(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
                @"|(?<phase>ph\.[a-z0-9#_]{1,2}(?=[A-Z\s\[?])|ph\.[a-zA-Z0-9_#]{1,3}(?=\b|[^a-zA-Z0-9_#])|ph\.[!\-0-9a-zA-Z#]+|phmp\.[!\-0-9a-zA-Z#]+|\.phi\b)" +
                @"|(?<delimiter>[&,;=:#+]|@\d+(?:lb|b|r|l|p|s)?\b|@\d+|\b(?i:Delevel|Level Up|No Flee|skip(?: all| rewards)?|temporary|Wish|Clear Party|Missing|Hidden|Add(?: 10| 100)? Fights?|Minus Fight|Cursemode Loopdiff|horde|double monsters)\b|![mv][a-zA-Z0-9_]*)" +
                @"|(?<sq_bracket>\[[^\]]*\])" +
                @"|(?<bracket>[{}()])" +
                @"|(?<itempool_block>itempool\.[^\.&,;]*)" +
                @"|(?<sd_block>sd\.[^.)&,;]*)" +
                @"|(?<ritemx>(?i)(?:i\.)?ritemx\.[^.)&,;]*)" +
                @"|(?<hsv_block>(?i)hsv\.[^\.&,;]*)" +
                @"|(?<k_block>k\.[^.#\s,)&,;]*)" +
                @"|(?<tog>\btog[a-zA-Z0-9_]*\b)" +
                @"|(?<method>\b(?:" + keysPattern + @")(?:\.|\()|\.(?:modtier|add|doc|all|egg|hsv|speech|unpack)\b)" +
                @"|(?<reward>\(?(?:rgreen|rblue|rred|ryellow|rorange|rgrey|rgray|rpurple|rcyan|[miglrqovsybn])\.[a-zA-Z0-9_\-~\^\/ ]*\)?|(?<=[\!&@+=])\(?[miglrqovsybn]\b\)?|\(?[miglrqovsybn][a-zA-Z0-9_\-~\^\/]*[\~^\/][a-zA-Z0-9_\-~\^\/]*\)?|\b[ov][A-Z][a-zA-Z0-9_]*\b)" +
                @"|(?<number>(?<!\w)\-?\d+\b)" +
                @"|(?<text>\b(?:[a-zA-Z_][a-zA-Z0-9_']*|\d+[a-zA-Z_][a-zA-Z0-9_']*)(?:\s+(?:[a-zA-Z_][a-zA-Z0-9_']*|\d+[a-zA-Z_][a-zA-Z0-9_']*))*\b[!?]?(?:\^(?:\d+\/\d+|\d+)|/\d+)?)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            RebuildFormattingTagRegex();
        }

        public static void RegisterItemNames(IEnumerable<string> names)
        {
            if (names == null) return;

            lock (RegexLock)
            {
                RegisteredItemNames.Clear();
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    RegisteredItemNames.Add(name);

                    string spaced = SplitPascalCase(name);
                    if (spaced != name)
                    {
                        RegisteredItemNames.Add(spaced);
                    }
                }
                RebuildFormattingTagRegex();
            }
        }

        public static void RegisterItemNamesFromEnum<TEnum>() where TEnum : struct, Enum
        {
            string[] names = Enum.GetNames(typeof(TEnum));
            RegisterItemNames(names);
        }

        private static void RebuildFormattingTagRegex()
        {
            var allTags = new List<string>(BaseFormattingTags);
            allTags.AddRange(RegisteredItemNames);

            allTags = allTags.Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(t => t.Length)
                             .ToList();

            string pattern = @"\[(/?(?:" + string.Join("|", allTags.Select(Regex.Escape)) + @"|alp\d+)|[a-zA-Z0-9%=\-+_/.:]{12,})\]";
            FormattingTagRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        }

        public static readonly Regex FloorSelectorRegex = new Regex(@"^(\(?)(e?\d+(?:\.\d+)?|\-?\d+\-\-?\d+|\-?\d+)\.", RegexOptions.Compiled);
        public static readonly Regex PoolDirectiveRegex = new Regex(@"^(replace\.)?(heropool|monsterpool|itempool|fight|add|party)\.(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex ConfigDirectiveRegex = new Regex(@"^(diff|zone|sd|i\.ritemx|ritemx)\.(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex PhaseRegex = new Regex(@"^(?:b?ph\.|phi\.|phmp\.)|^\!", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex ChoosableRegex = new Regex(@"^ch\.([a-z]+)(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex ToggleRegex = new Regex(@"^tog([a-zA-Z0-9_]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ======================================================================================
        // 3. PARENTHESIS BALANCING & STRUCTURAL HELPERS
        // ======================================================================================

        public static int FindMatchingClosingParenthesis(string s, int openIdx)
        {
            int depth = 1;
            for (int i = openIdx + 1; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        public static List<string> SplitRespectingParens(string input, char[] delimiters)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(input)) return result;

            input = input.Trim();
            if (input.StartsWith("=")) input = input.Substring(1).Trim();

            int depth = 0, start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(') depth++;
                else if (input[i] == ')') depth--;
                else if (depth == 0 && delimiters.Contains(input[i]))
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(input.Substring(start));
            return result;
        }

        public static List<string> SplitRespectingParens(string input, char delimiter)
        {
            return SplitRespectingParens(input, new[] { delimiter });
        }

        public static List<string> SplitPool(string poolContent)
        {
            return SplitRespectingParens(poolContent, PoolDelimiter);
        }

        public static (string core, bool isHidden) UnwrapHiddenModifier(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return (chunk, false);
            string core = chunk.Trim();
            if (core.StartsWith("=")) core = core.Substring(1).Trim();

            bool isHidden = false;

            // General support for numeric weights, category codes, and decimals (e.g., '!go0.271' or '!m')
            var match = Regex.Match(core, @"^(![a-zA-Z0-9_]+(?:\.[0-9]+)?)\.?");
            if (match.Success)
            {
                int prefixLength = match.Length;
                string remaining = core.Substring(prefixLength).Trim();

                if (remaining.StartsWith("(") && remaining.EndsWith(")"))
                {
                    int parenCount = 0;
                    bool isBalanced = true;
                    for (int i = 0; i < remaining.Length; i++)
                    {
                        if (remaining[i] == '(') parenCount++;
                        else if (remaining[i] == ')')
                        {
                            parenCount--;
                            if (parenCount == 0 && i < remaining.Length - 1)
                            {
                                isBalanced = false;
                                break;
                            }
                        }
                    }
                    if (isBalanced && parenCount == 0)
                    {
                        core = remaining.Substring(1, remaining.Length - 2).Trim();
                        isHidden = true;
                        return (core, isHidden);
                    }
                }

                core = remaining;
                isHidden = true;
            }

            return (core, isHidden);
        }

        public static (string core, string floorRaw) StripFloorSelector(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return (raw, null);

            raw = raw.Trim();
            if (raw.StartsWith("=")) raw = raw.Substring(1).Trim();

            bool hasOuterParen = raw.StartsWith("(");

            var match = FloorSelectorRegex.Match(raw);
            if (!match.Success) return (raw, null);

            string floorRaw = match.Groups[2].Value;
            string core = raw.Substring(match.Length).Trim();

            if (hasOuterParen)
            {
                int closeParenIdx = FindMatchingClosingParenthesis(raw, 0);
                if (closeParenIdx != -1)
                {
                    int relativeIdx = closeParenIdx - match.Length;
                    if (relativeIdx >= 0 && relativeIdx < core.Length && core[relativeIdx] == ')')
                    {
                        core = core.Remove(relativeIdx, 1).Trim();
                    }
                }
            }

            return (core, floorRaw);
        }

        // ======================================================================================
        // 4. ENTITY PROPERTY PARSING (PARENTHESIS & NESTED BRACKET AWARE)
        // ======================================================================================

        public static string ExtractProperty(string syntax, string propertyKey)
        {
            if (string.IsNullOrEmpty(syntax)) return null;

            string searchKey1 = propertyKey + ".";
            string searchKey2 = "." + propertyKey + ".";
            string searchKey3 = propertyKey + "(";
            string searchKey4 = "." + propertyKey + "(";

            int startIdx = -1;

            if (syntax.StartsWith(searchKey1, StringComparison.OrdinalIgnoreCase))
            {
                startIdx = searchKey1.Length;
            }
            else if (syntax.StartsWith(searchKey3, StringComparison.OrdinalIgnoreCase))
            {
                startIdx = searchKey3.Length - 1;
            }
            else
            {
                int idx = syntax.IndexOf(searchKey2, StringComparison.OrdinalIgnoreCase);
                if (idx != -1)
                {
                    startIdx = idx + searchKey2.Length;
                }
                else
                {
                    idx = syntax.IndexOf(searchKey4, StringComparison.OrdinalIgnoreCase);
                    if (idx != -1)
                    {
                        startIdx = idx + searchKey4.Length - 1;
                    }
                }
            }

            if (startIdx == -1) return null;

            int depth = 0;
            int sqDepth = 0;
            int curlyDepth = 0;
            int length = 0;

            var containerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "triggerhpdata", "onhitdata", "learn", "unpack", "splice", "replica", "abilitydata", "peritem", "allitem", "alliteme"
            };
            var nonBreakingSubKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "sd", "hp", "i", "k", "hsv", "hsl", "draw", "rect", "p", "b", "t", "sticker", "cast", "sidesc", "splice", "m"
            };

            bool isContainer = containerKeys.Contains(propertyKey);

            while (startIdx + length < syntax.Length)
            {
                char c = syntax[startIdx + length];

                if (c == '(') depth++;
                else if (c == ')')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == '[') sqDepth++;
                else if (c == ']')
                {
                    if (sqDepth > 0) sqDepth--;
                }
                else if (c == '{') curlyDepth++;
                else if (c == '}')
                {
                    if (curlyDepth > 0) curlyDepth--;
                }

                if (depth == 0 && sqDepth == 0 && curlyDepth == 0)
                {
                    if (c == '&' || c == ',' || c == ';' || c == '=' || c == '#')
                    {
                        break;
                    }
                    if (c == '@' && startIdx + length + 1 < syntax.Length && char.IsDigit(syntax[startIdx + length + 1]))
                    {
                        break;
                    }
                    if (c == '.')
                    {
                        string remaining = syntax.Substring(startIdx + length + 1);
                        bool isNextKey = false;
                        string matchedKey = null;

                        foreach (var key in AllPropertyKeys)
                        {
                            string keyPattern = CleanKeyForRegex(key);
                            var match = Regex.Match(remaining, $"^({keyPattern})(?:\\.|\\(|$)");
                            if (match.Success)
                            {
                                isNextKey = true;
                                matchedKey = match.Groups[1].Value;
                                break;
                            }
                        }

                        if (isNextKey)
                        {
                            if (isContainer && matchedKey != null && nonBreakingSubKeys.Contains(matchedKey))
                            {
                                // Do not terminate flat dot-chaining on common nested entity properties
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                length++;
            }

            return syntax.Substring(startIdx, length).Trim();
        }

        public static string ExtractHeroProperty(string heroSyntax, string propertyKey)
        {
            return ExtractProperty(heroSyntax, propertyKey);
        }

        public static (int effectId, int pips)[] ParseDiceSides(string sdPayload)
        {
            var results = new (int, int)[6];
            if (string.IsNullOrEmpty(sdPayload)) return results;

            string[] sides = sdPayload.Split(DiceSideDelimiter);
            for (int i = 0; i < Math.Min(6, sides.Length); i++)
            {
                string side = sides[i].Trim();
                if (side == "0" || side == "0-0" || string.IsNullOrEmpty(side))
                {
                    results[i] = (0, 0);
                    continue;
                }

                int hyphenIndex = -1;
                for (int k = side.Length - 1; k >= 0; k--)
                {
                    if (side[k] == '-')
                    {
                        hyphenIndex = (k > 0 && side[k - 1] == '-') ? k - 1 : k;
                        break;
                    }
                }

                if (hyphenIndex > 0)
                {
                    string effPart = side.Substring(0, hyphenIndex);
                    string pipPart = side.Substring(hyphenIndex + 1);

                    if (int.TryParse(effPart, out int eff) && int.TryParse(pipPart, out int pips))
                    {
                        results[i] = (eff, pips);
                    }
                }
                else
                {
                    if (int.TryParse(side, out int eff))
                    {
                        results[i] = (eff, 0);
                    }
                }
            }
            return results;
        }

        public static List<(string target, string payload)> ParseDiceModifiers(string entitySyntax)
        {
            var results = new List<(string, string)>();
            if (string.IsNullOrEmpty(entitySyntax)) return results;

            int i = 0;
            while (i < entitySyntax.Length)
            {
                if (i <= entitySyntax.Length - 3 && entitySyntax.Substring(i, 3).Equals(".i.", StringComparison.OrdinalIgnoreCase))
                {
                    int start = i + 3;
                    int len = 0;
                    int parenDepth = 0;
                    int bracketDepth = 0;
                    int sqBracketDepth = 0;

                    while (start + len < entitySyntax.Length)
                    {
                        char c = entitySyntax[start + len];

                        if (c == '(') parenDepth++;
                        else if (c == ')') parenDepth--;
                        else if (c == '{') bracketDepth++;
                        else if (c == '}') bracketDepth--;
                        else if (c == '[') sqBracketDepth++;
                        else if (c == ']') sqBracketDepth--;

                        if (parenDepth == 0 && bracketDepth == 0 && sqBracketDepth == 0)
                        {
                            if (c == '&' || c == ',' || c == ';')
                            {
                                break;
                            }

                            if (c == '.')
                            {
                                string remainingText = entitySyntax.Substring(start + len + 1);
                                bool isKey = false;
                                foreach (var key in AllPropertyKeys)
                                {
                                    string keyPattern = CleanKeyForRegex(key);
                                    var m = Regex.Match(remainingText, $"^({keyPattern})(?:\\.|\\(|$)");
                                    if (m.Success)
                                    {
                                        isKey = true;
                                        break;
                                    }
                                }

                                if (isKey)
                                {
                                    string matchedKey = Regex.Match(remainingText, @"^[a-zA-Z0-9#]+").Value.ToLower();
                                    if (!SideposDefs.Contains(matchedKey) && matchedKey != "self")
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        len++;
                    }

                    string modifierString = entitySyntax.Substring(start, len).Trim();

                    if (!string.IsNullOrEmpty(modifierString))
                    {
                        if (modifierString.StartsWith("(") && modifierString.EndsWith(")"))
                        {
                            int depth = 0;
                            bool isOuter = true;
                            for (int k = 0; k < modifierString.Length; k++)
                            {
                                if (modifierString[k] == '(') depth++;
                                else if (modifierString[k] == ')')
                                {
                                    depth--;
                                    if (depth == 0 && k < modifierString.Length - 1)
                                    {
                                        isOuter = false;
                                        break;
                                    }
                                }
                            }
                            if (isOuter)
                            {
                                modifierString = modifierString.Substring(1, modifierString.Length - 2).Trim();
                            }
                        }

                        var targets = new List<string>();
                        string remaining = modifierString;
                        while (true)
                        {
                            int dotIdx = -1;
                            int depth = 0;
                            for (int k = 0; k < remaining.Length; k++)
                            {
                                if (remaining[k] == '(') depth++;
                                else if (remaining[k] == ')') depth--;
                                else if (remaining[k] == '.' && depth == 0)
                                {
                                    dotIdx = k;
                                    break;
                                }
                            }

                            if (dotIdx <= 0) break;

                            string firstToken = remaining.Substring(0, dotIdx).Trim().ToLower();
                            if (firstToken.StartsWith("(") && firstToken.EndsWith(")"))
                            {
                                firstToken = firstToken.Substring(1, firstToken.Length - 2).Trim();
                            }

                            if (SideposDefs.Contains(firstToken) || firstToken == "self")
                            {
                                targets.Add(firstToken);
                                remaining = remaining.Substring(dotIdx + 1).Trim();
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (targets.Count > 0)
                        {
                            string targetStr = string.Join(".", targets);
                            results.Add((targetStr, remaining));
                        }
                    }

                    i = start + len;
                }
                else
                {
                    i++;
                }
            }

            return results;
        }

        // ======================================================================================
        // 5. INTERACTIVE MENU & STRUCTURAL OPTIONS PARSING
        // ======================================================================================

        public class ChoiceOption
        {
            public string Label { get; set; }
            public List<string> Payloads { get; set; } = new List<string>();
            public int Marker { get; set; } = 1;
        }

        /// <summary>
        /// Structurally parses interactive custom menus based on sequential choice indicators (e.g., @1, @2, @3).
        /// Standard label and action assignments are preserved using an odd/even marker distribution rule.
        /// </summary>
        public static List<ChoiceOption> ParseChoiceOptions(string syntax)
        {
            var options = new List<ChoiceOption>();
            if (string.IsNullOrEmpty(syntax)) return options;

            int i = 0;
            ChoiceOption currentOption = null;
            int parenDepth = 0;
            int choiceParenDepth = 0;

            while (i < syntax.Length)
            {
                char c = syntax[i];
                if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')')
                {
                    parenDepth--;
                    if (currentOption != null && parenDepth < choiceParenDepth)
                    {
                        currentOption = null;
                    }
                }
                else if (c == ',' || c == '&')
                {
                    if (parenDepth == 0)
                    {
                        currentOption = null;
                    }
                }
                else if (c == '@')
                {
                    int start = i;
                    i++;

                    while (i < syntax.Length && char.IsDigit(syntax[i])) i++;

                    string numStr = syntax.Substring(start + 1, i - start - 1);
                    if (int.TryParse(numStr, out int marker))
                    {
                        var whitelist = new[] { "lb", "b", "r", "l", "p", "s" };
                        foreach (var sfx in whitelist)
                        {
                            if (i + sfx.Length <= syntax.Length &&
                                syntax.Substring(i, sfx.Length).Equals(sfx, StringComparison.OrdinalIgnoreCase))
                            {
                                int boundaryIdx = i + sfx.Length;
                                if (boundaryIdx == syntax.Length || !char.IsLetterOrDigit(syntax[boundaryIdx]))
                                {
                                    i += sfx.Length;
                                    break;
                                }
                            }
                        }

                        int contentStart = i;
                        int depth = 0;
                        int length = 0;
                        while (contentStart + length < syntax.Length)
                        {
                            char cc = syntax[contentStart + length];
                            if (cc == '(') depth++;
                            else if (cc == ')') depth--;

                            if (depth == 0 && cc == '@' && (contentStart + length + 1 < syntax.Length && char.IsDigit(syntax[contentStart + length + 1])))
                            {
                                break;
                            }
                            length++;
                        }

                        string content = syntax.Substring(contentStart, length).Trim();
                        i = contentStart + length;

                        if (marker % 2 == 1)
                        {
                            currentOption = new ChoiceOption { Label = content, Marker = marker };
                            choiceParenDepth = parenDepth;
                            options.Add(currentOption);
                        }
                        else if (marker % 2 == 0 && currentOption != null)
                        {
                            currentOption.Payloads.Add(content);
                        }
                        continue;
                    }
                    i = start;
                }
                i++;
            }
            return options;
        }

        // ======================================================================================
        // 6. RANDOM REWARD PARSING
        // ======================================================================================

        public class RandomReward
        {
            public int Tier { get; set; }
            public int Quantity { get; set; }
            public string RewardType { get; set; }
        }

        public static RandomReward ParseRandomReward(string rewardSyntax)
        {
            var match = Regex.Match(rewardSyntax, @"^(?:ch\.)?r(\d+)~(\d+)~([a-zA-Z]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return new RandomReward
                {
                    Tier = int.Parse(match.Groups[1].Value),
                    Quantity = int.Parse(match.Groups[2].Value),
                    RewardType = match.Groups[3].Value
                };
            }
            return null;
        }

        // ======================================================================================
        // 7. CLEANUP & SANITIZATION HELPERS
        // ======================================================================================

        public static string StripFormattingTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = Regex.Replace(text, @"\[comma\]", ",", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\[n\]", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\[dot\]", ".", RegexOptions.IgnoreCase);

            lock (RegexLock)
            {
                return FormattingTagRegex.Replace(text, "");
            }
        }

        public static string CleanEntityName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;

            string[] parts = rawName.Split('.');
            if (parts.Length == 2)
            {
                string prefix = parts[0].ToLower();
                string suffix = parts[1];

                if (prefix == "jinx")
                {
                    return ToTitleCase(suffix) + " Jinx";
                }
                if (prefix == "orb")
                {
                    return ToTitleCase(suffix) + " Orb";
                }
                if (prefix == "egg")
                {
                    return ToTitleCase(suffix) + " Egg";
                }
                if (prefix == "vase")
                {
                    return ToTitleCase(suffix) + " Vase";
                }
            }

            if (rawName.EndsWith(".75", StringComparison.OrdinalIgnoreCase))
            {
                string baseName = rawName.Substring(0, rawName.Length - 3);
                if (baseName.Length == 2 && char.IsLetterOrDigit(baseName[0]) && char.IsDigit(baseName[1]))
                {
                    return baseName;
                }
            }
            return rawName;
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string clean = input.Replace('_', ' ').Replace('-', ' ').Trim();
            return string.Join(" ", clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpper(w[0]) + w.Substring(1)));
        }

        public static string StripTrailingMetadata(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string pattern = @"\.(mn|doc|modtier|bal|speech|img|hsv|part)\.(?:\[.*?\]|[a-zA-Z0-9 _\-!?^/]+)+";
            return Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
        }
    }
}