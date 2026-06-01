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
    ///    - Supports stripping a leading '=' expression indicator when parsing raw strings.
    /// 2. Wrappers: '()' group elements. 
    ///    - Supports recursive parenthetical nesting for replicas, triggers, and nested property definitions.
    ///    - '!m()' and variants wrap elements to create hidden modifiers.
    /// 3. Floor Selectors: Prepend directives to restrict them to specific floors.
    ///    - Exact: "4.fight.Rat" (Floor 4)
    ///    - Range: "1-5.add.Rat" (Floors 1 through 5)
    ///    - Every/Offset: "e2.1.ph.1" (Every 2 floors, offset 1 -> 1, 3, 5, 7)
    ///    - Enclosed: Handles optional wrapper parentheses symmetrically: e.g., "(1-3.monsterpool.(...))"
    ///      maintains balanced parenthesis stripping across the expression.
    /// 4. Pools & Injections: Target game lists using prefix + '.' + '+'-delimited items.
    ///    - e.g., "heropool.Thief+Fighter", "fight.Rat+Boar", "add.Goblin"
    /// 5. Phases: Trigger logic on specific game phases. Uses "ph." or "phmp." + Code.
    ///    - e.g., "ph.4Message;Button", "ph.bY;1;!mheropool.(...)", "phmp.0"
    ///    - Handles conditional branching targets (e.g., "@2bChestLvlTwo" or "@2!m") separately from UI menus.
    /// 6. Choosables / Rewards: Starts with "ch." followed by tag and payload.
    ///    - Supports multi-character prefixes like "ch.pm" (Permanent Modifier) and "ch.r" (Random reward).
    ///    - Parsed parameter structures evaluate tilde-delimited formats: e.g., "r1~2~i" (Tier 1, Qty 2, Item).
    /// 7. Interactive UI Choice Menus: Matches option indicators "@1 Label" and binds them structurally
    ///    - Binds labels sequentially to implementation payloads "@2 Modifiers".
    ///    - Excludes non-UI phase branches by verifying preceding active option context, structural boundaries,
    ///      and trailing variable/conditional characters.
    /// 8. Entity Properties: Custom heroes/items chain properties together with dots.
    ///    - e.g., "replica.Statue.n.NewHero.hp.10.tier.3.col.y.sd.1-1:2-2.i.left.k.acidic"
    ///    - Tracks balanced parenthesis depths, square brackets, and curly braces to avoid premature truncation.
    /// 9. Dice Sides: "sd.Eff-Pips:Eff-Pips..." (6 sides). '0' means blank.
    ///    - Supports pipless/flat side effects, e.g., "sd.43:170-3:0:0:0:0" (effect 43 with 0 pips).
    /// 10. Dice Modifiers: ".i.{targets}.{payload}".
    ///    - Targets = left, mid, self, all, topbot, left2, etc.
    ///    - Payloads are '#' separated: "k.acidic#facade.bas1"
    ///    - Context-Aware Filtering: Differentiates side modifications from general item attachments 
    ///      (e.g., .i.pendulum) by verifying presence of side target keywords.
    /// 11. Formatting & Animation Tags: Automatically classifies and handles non-syntactic aesthetic markup
    ///     such as color triggers, keyword containers, and animation tags.
    ///     - Translates "[comma]" sequences to literal commas during sanitization.
    /// 12. Formula Operators: Supports inline division `/` and exponential `^` calculations inside modifiers.
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
            "Add 10 Fights", "Add 100 Fights", "Minus Fight", "Cursemode Loopdiff", "horde"
        };

        // Side positions/targets
        public static readonly string[] SideposDefs = { "all", "self", "right5", "right3", "right2", "row", "mid2", "col", "topbot", "left2", "rightmost", "right", "bot", "top", "mid", "left" };

        // Entities
        public static readonly string[] HeroPropertyKeys = { "replica", "img", "n", "col", "hp", "tier", "hsv", "sd", "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect", "thue" };
        public static readonly string[] MonsterPropertyKeys = { "i", "rmon", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "b", "draw", "hsv", "rect", "thue", "p" };
        public static readonly string[] ItemPropertyKeys = { "k", "learn", "hat", "t", "#", "sidepos", "tier", "n", "ritem", "ritemx", "facade", "mrg", "self", "m", "doc", "pertier", "part", "rditem", "unpack", "sidesc", "splice", "onhitdata", "triggerhpdata", "sticker", "enchant", "cast", "img", "hue", "b", "draw", "hsv", "rect", "thue", "p" };
        public static readonly string[] EntityCommonPropertyKeys = { "img", "n", "doc", "p", "t", "b", "rect", "thue" };

        // Directives
        public static readonly string[] ModifierPropertyKeys = { "hero", "add", "party", "diff", "modifier", "lvl", "t#", "heropool", "itempool", "monsterpool", "monster", "allitem", "alliteme", "fight", "mn", "heropos", "ph", "phi", "phmp", "e", "et#", "inv", "part", "zone", "delivery", "rmod", "pl", "pb", "pt", "peritem", "spirit", "ch", "rdmod", "temporary", "hidden", "missing", "skip", "skip all", "minus fight", "clear party", "wish", "cursemode loopdiff", "mch", "m#", "x#", "dabble", "bal", "ea" };
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
        // 2. MASTER REGEX PATTERNS
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
        private static readonly Regex FormattingTagRegex;

        static SDSyntaxBrain()
        {
            string keysPattern = string.Join("|", AllPropertyKeys.Where(k => k != "&" && k != "#").Select(CleanKeyForRegex));

            MasterSyntaxRegex = new Regex(
                @"(?<=^|[(&@!~\[])(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
                @"|(?<phase>ph\.[!0-9a-zA-Z#]+|phmp\.[!0-9a-zA-Z#]+|\.phi\b)" +
                @"|(?<delimiter>[&,;=:#]|@\d+[a-zA-Z0-9_]*|@\d+|\b(?i:Delevel|Level Up|No Flee|skip(?: all)?|temporary|Wish|Clear Party|Missing|Hidden|Add(?: 10| 100)? Fights?|Minus Fight|Cursemode Loopdiff|horde)\b|![mv][a-zA-Z0-9_]*)" +
                @"|(?<sq_bracket>\[[^\]]*\])" +
                @"|(?<bracket>[{}()])" +
                @"|(?<itempool_block>itempool\.[^\.]*)" +
                @"|(?<sd_block>sd\.[^.)]*)" +
                @"|(?<ritemx>(?i)(?:i\.)?ritemx\.[^.)]*)" +
                @"|(?<hsv_block>(?i)hsv\.[^\.]*)" +
                @"|(?<k_block>k\.[^.#\s,)]*)" +
                @"|(?<tog>\btog[a-zA-Z0-9_]*\b)" +
                @"|(?<method>\b(?:" + keysPattern + @")\.|\.(?:modtier|add|doc|all|egg|hsv|speech|unpack)\b)" +
                @"|(?<reward>\(?[miglrqovs]\.[a-zA-Z0-9_\-~\^\/ ]*\)?|(?<=[\!&@+=])\(?[miglrqovs]\b\)?|\(?[miglrqovs][a-zA-Z0-9_\-~\^\/]*[\~^\/][a-zA-Z0-9_\-~\^\/]*\)?|\b[ov][A-Z][a-zA-Z0-9_]*\b)" +
                @"|(?<number>\b\d+\b)" +
                @"|(?<text>[a-zA-Z_][a-zA-Z0-9_]*(?:\^(?:\d+\/\d+|\d+)|/\d+)?)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            // 2. Build the formatting tag removal regex using the definitive color list
            var tags = new List<string>
            {
            // Full color names
            "orange", "yellow", "grey", "gray", "red", "blue", "green", "purple", "cyan",
            "sea", "euish", "white", "kuish", "uuish", "violet", "huish", "mahogany",
            "lime", "tuish", "zuish", "amber", "iuish", "quish", "xuish", "fuish", "juish",
            "blurple", "brown", "dark", "light",

            // Single-character and secondary color short codes
            "o", "y", "g", "r", "b", "n", "p", "c", "s", "e", "w", "k", "u", "v", "h", "m", "l", "t", "z", "a", "i", "q", "x", "f", "j", "nh",

            // Layout, formatting, and effect tags
            "sin", "wiggle", "cu", "nokeyword", "hp-plus", "pips", "pipsk"
            };

            // Sort descending by length to prevent shorter prefix strings from matching before longer tags
            tags.Sort((a, b) => b.Length.CompareTo(a.Length));

            string pattern = @"\[(/?(?:" + string.Join("|", tags.Select(Regex.Escape)) + @"))\]";
            FormattingTagRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
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

        public static (string core, bool isHidden) UnwrapHiddenModifier(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return (chunk, false);
            string core = chunk.Trim();
            if (core.StartsWith("=")) core = core.Substring(1).Trim();

            bool isHidden = false;

            var match = Regex.Match(core, @"^(!?[mv][a-zA-Z0-9_]*)\.?\(");
            if (match.Success && core.EndsWith(")"))
            {
                int parenCount = 0;
                int firstParenIdx = match.Length - 1;
                bool isBalanced = true;

                for (int i = firstParenIdx; i < core.Length; i++)
                {
                    if (core[i] == '(') parenCount++;
                    else if (core[i] == ')')
                    {
                        parenCount--;
                        if (parenCount == 0 && i < core.Length - 1)
                        {
                            isBalanced = false;
                            break;
                        }
                    }
                }

                if (isBalanced && parenCount == 0)
                {
                    int prefixLength = match.Length;
                    core = core.Substring(prefixLength, core.Length - prefixLength - 1).Trim();
                    isHidden = true;
                }
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

            int startIdx = -1;

            if (syntax.StartsWith(searchKey1, StringComparison.OrdinalIgnoreCase))
            {
                startIdx = searchKey1.Length;
            }
            else
            {
                int idx = syntax.IndexOf(searchKey2, StringComparison.OrdinalIgnoreCase);
                if (idx != -1)
                {
                    startIdx = idx + searchKey2.Length;
                }
            }

            if (startIdx == -1) return null;

            int depth = 0;
            int sqDepth = 0;
            int curlyDepth = 0;
            int length = 0;

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
                    if (c == '&' || c == ',' || c == ';')
                    {
                        break;
                    }
                    if (c == '.')
                    {
                        string remaining = syntax.Substring(startIdx + length + 1);
                        bool isNextKey = false;
                        foreach (var key in AllPropertyKeys)
                        {
                            string keyPattern = CleanKeyForRegex(key);
                            if (Regex.IsMatch(remaining, $"^({keyPattern})(?:\\.|\\(|$)"))
                            {
                                isNextKey = true;
                                break;
                            }
                        }
                        if (isNextKey) break;
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
                                    if (!SideposDefs.Contains(matchedKey) && matchedKey != "self" && matchedKey != "t")
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

                            if (SideposDefs.Contains(firstToken) || firstToken == "self" || firstToken == "t")
                            {
                                targets.Add(firstToken);
                                remaining = remaining.Substring(dotIdx + 1).Trim();
                            }
                            else
                            {
                                break;
                            }
                        }

                        // Context-aware filter: Differentiate side modification from item properties
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
        /// Structurally parses interactive custom menus based on sequential @1 and @2 options.
        /// Ignores non-UI phase branches by tracking depth, delimiter context, and variable suffix bounds.
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
                        // Check if the digit is followed by a phase branch variable (e.g. @2bChestLvlTwo)
                        bool isPhaseBranch = false;
                        if (i < syntax.Length && char.IsLetter(syntax[i]))
                        {
                            string suffix = syntax.Substring(i);
                            if (!suffix.StartsWith("m(") && !suffix.StartsWith("v(") &&
                                !suffix.StartsWith("m.") && !suffix.StartsWith("v."))
                            {
                                isPhaseBranch = true;
                            }
                        }

                        if (!isPhaseBranch)
                        {
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

                            if (marker == 1)
                            {
                                currentOption = new ChoiceOption { Label = content, Marker = 1 };
                                choiceParenDepth = parenDepth;
                                options.Add(currentOption);
                            }
                            else if (marker == 2 && currentOption != null)
                            {
                                currentOption.Payloads.Add(content);
                            }
                            continue;
                        }
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
            public string RewardType { get; set; } // "l" (level up), "i" (item), "g" (green tier), etc.
        }

        public static RandomReward ParseRandomReward(string rewardSyntax)
        {
            var match = Regex.Match(rewardSyntax, @"^r(\d+)~(\d+)~([a-zA-Z]+)", RegexOptions.IgnoreCase);
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

            // Translate the [comma] escape sequence back into literal commas
            text = Regex.Replace(text, @"\[comma\]", ",", RegexOptions.IgnoreCase);

            // Cleans color, animation, and layout decorators safely while preserving functional gameplay bracket keys.
            return FormattingTagRegex.Replace(text, "");
        }

        public static string CleanEntityName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName;

            string[] parts = rawName.Split('.');
            if (parts.Length == 2)
            {
                string prefix = parts[0].ToLower();
                string suffix = parts[1];

                //reminder: "jinx.uhh, orb.Slice, egg.Bee, vase.uhh" are all safe valid ways to use these keywords when the second word is not known. 
                if (prefix == "jinx")
                {
                    if (suffix.Equals("uhh", StringComparison.OrdinalIgnoreCase)) return "jinx";
                    return ToTitleCase(suffix) + " Jinx";
                }
                if (prefix == "orb")
                {
                    if (suffix.Equals("Slice", StringComparison.OrdinalIgnoreCase)) return "orb";
                    return ToTitleCase(suffix) + " Orb";
                }
                if (prefix == "egg")
                {
                    if (suffix.Equals("Bee", StringComparison.OrdinalIgnoreCase)) return "egg";
                    return ToTitleCase(suffix) + " Egg";
                }
                if (prefix == "vase")
                {
                    if (suffix.Equals("uhh", StringComparison.OrdinalIgnoreCase)) return "vase";
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

        /// <summary>
        /// Helper to convert raw, delimited identifiers (like "reduced_defense" or "sThief") 
        /// into clean, capitalized display names.
        /// </summary>
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
            string pattern = @"\.(mn|doc|modtier|bal|speech|img|hsv)\.(?:\[.*?\]|[a-zA-Z0-9 _\-!?^/]+)+";
            return Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
        }
    }
}