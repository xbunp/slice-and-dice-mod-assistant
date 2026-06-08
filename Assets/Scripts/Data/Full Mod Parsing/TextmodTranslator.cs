using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// A utility class to parse and translate Slice & Dice Textmod syntax into readable sentences.
/// </summary>
public static class TextmodTranslator
{
    private static readonly string[] GlobalCommands =
    {
        "Delevel", "Level Up", "No Flee", "skip all", "skip", "temporary",
        "Wish", "Clear Party", "Missing", "Hidden", "Add Fight",
        "Add 10 Fights", "Add 100 Fights", "Minus Fight", "Cursemode Loopdiff", "horde",
        "double monsters", "skip rewards", "level up", "level-up"
    };

    /// <summary>
    /// Parses a raw Textmod string and returns a human-readable explanation.
    /// </summary>
    public static string Translate(string input)
    {
        if (string.IsNullOrEmpty(input)) return "Empty input.";

        input = CleanFormattingTags(input).Trim();

        while ((input.StartsWith("'") && input.EndsWith("'")) ||
               (input.StartsWith("\"") && input.EndsWith("\"")) ||
               (input.StartsWith("`") && input.EndsWith("`")))
        {
            input = input.Substring(1, input.Length - 2).Trim();
        }

        while (input.StartsWith("=")) input = input.Substring(1).Trim();

        List<string> listCommands = SplitRespectingParens(input, ",");
        if (listCommands.Count > 1)
        {
            List<string> translatedCommands = new List<string>();
            foreach (string cmd in listCommands)
            {
                if (!string.IsNullOrEmpty(cmd)) translatedCommands.Add(Translate(cmd));
            }

            if (translatedCommands.Count == 1) return translatedCommands[0].Replace("**COMMA**", ",");
            return string.Join("\n--- AND ---\n", translatedCommands).Replace("**COMMA**", ",");
        }

        while (input.StartsWith("(") && input.EndsWith(")"))
        {
            int matchingClose = FindMatchingClosingParenthesis(input, 0);
            if (matchingClose == input.Length - 1) input = input.Substring(1, input.Length - 2).Trim();
            else break;
        }

        bool skipAndSplit = false;
        if (IsPhaseString(input))
        {
            int depth = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(' || input[i] == '{') depth++;
                else if (input[i] == ')' || input[i] == '}') depth--;
                else if (depth == 0 && input[i] == '@' && i < input.Length - 1)
                {
                    char nextChar = input[i + 1];
                    if (nextChar == '1' || nextChar == '2' || nextChar == '3' || nextChar == '6' || nextChar == '7')
                    {
                        skipAndSplit = true;
                        break;
                    }
                }
            }
        }

        if (!skipAndSplit)
        {
            List<string> rawSplits = SplitRespectingParens(input, "&");
            List<string> concurrentCommands = new List<string>();
            string currentPhase = "";

            foreach (string cmd in rawSplits)
            {
                if (!string.IsNullOrEmpty(currentPhase))
                {
                    currentPhase += "&" + cmd;
                }
                else
                {
                    // Protected from swallowing if the phase is wrapped in parentheses!
                    if (IsPhaseString(cmd) && !cmd.StartsWith("("))
                    {
                        currentPhase = cmd;
                    }
                    else
                    {
                        concurrentCommands.Add(cmd);
                    }
                }
            }
            if (!string.IsNullOrEmpty(currentPhase))
            {
                concurrentCommands.Add(currentPhase);
            }

            if (concurrentCommands.Count > 1)
            {
                List<string> translatedCommands = new List<string>();
                foreach (string cmd in concurrentCommands) translatedCommands.Add(Translate(cmd));
                return string.Join(" AND ", translatedCommands).Replace("**COMMA**", ",");
            }
        }

        string prefixTranslation = "";
        string coreText = input;

        Match floorMatch = Regex.Match(input, @"^((?:e\d+(?:\.\d+)?|\-?\d+(?:-\-?\d+)?)\.)");
        if (floorMatch.Success)
        {
            string rawPrefix = floorMatch.Groups[1].Value.TrimEnd('.');
            prefixTranslation = TranslateFloorPrefix(rawPrefix) + ": ";
            coreText = input.Substring(floorMatch.Length).Trim();
        }

        string translation = prefixTranslation + ParseMainCore(coreText);

        return CapitalizeFirst(translation).Replace("**COMMA**", ",");
    }

    private static string TranslateFloorPrefix(string prefix)
    {
        if (prefix.StartsWith("e"))
        {
            string numStr = prefix.Substring(1);
            string[] parts = numStr.Split('.');
            string floorNum = parts[0];
            string offsetStr = parts.Length == 2 ? $" (starting on {parts[1]})" : "";

            // Formats ordinal numbers perfectly (e.g. 1st, 2nd, 3rd, 4th)
            return $"Before every {floorNum}{GetOrdinalSuffix(floorNum)} floor{offsetStr}";
        }
        if (prefix.Contains("-")) return $"From floors {prefix.Replace("-", " to ")}";
        return $"On floor {prefix}";
    }

    private static string ParseMainCore(string text)
    {
        text = text.Trim();

        // 1. Proactively slice global modifier properties (.mn., .doc., .modtier.) off ANY modifier string at Depth 0.
        // This perfectly mirrors how the S&D engine extracts modifier metadata regardless of where it is appended.
        int propIdx = -1;
        if (!IsPhaseString(text))
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '(' || text[i] == '[' || text[i] == '{') depth++;
                else if (text[i] == ')' || text[i] == ']' || text[i] == '}') depth--;
                else if (depth == 0 && text[i] == '.')
                {
                    string remaining = text.Substring(i);
                    if (remaining.StartsWith(".mn.") || remaining.StartsWith(".doc.") || remaining.StartsWith(".modtier."))
                    {
                        propIdx = i;
                        break;
                    }
                }
            }
        }

        if (propIdx != -1)
        {
            string core = text.Substring(0, propIdx);
            string properties = text.Substring(propIdx);

            string translatedCore = Translate(core);
            string translatedProps = ParseEntity("LogicBlock" + properties);
            string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();

            if (finalProps != "''" && finalProps != "'Entity'")
            {
                return $"{translatedCore} {finalProps}".Trim();
            }
            return translatedCore;
        }

        // 2. Safely unwrap standalone Logic Blocks
        if (text.StartsWith("("))
        {
            int closeParen = FindMatchingClosingParenthesis(text, 0);
            if (closeParen == text.Length - 1)
            {
                return Translate(text.Substring(1, text.Length - 2));
            }
        }

        if (text.StartsWith("ch.")) return ParseRewardTag(text.Substring(3));
        if (text.StartsWith("ph.")) return ParseImpliedPhase(text.Substring(3));
        if (text.StartsWith("phi."))
        {
            string payload = text.Substring(4);
            int dotIdx = payload.IndexOf('.');
            if (dotIdx != -1)
            {
                string index = payload.Substring(0, dotIdx);
                string properties = payload.Substring(dotIdx);

                string translatedPhase = ParseIndexedPhase(index);
                string translatedProps = ParseEntity("LogicBlock" + properties);
                string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();
                return $"{translatedPhase} {finalProps}".Trim();
            }
            return ParseIndexedPhase(payload);
        }

        if (text.StartsWith("phmp."))
        {
            string payload = text.Substring(5);
            int dotIdx = payload.IndexOf('.');
            if (dotIdx != -1)
            {
                string value = payload.Substring(0, dotIdx);
                string properties = payload.Substring(dotIdx);

                string translatedPhase = ParseModPickPhase(value);
                string translatedProps = ParseEntity("LogicBlock" + properties);
                string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();
                return $"{translatedPhase} {finalProps}".Trim();
            }
            return ParseModPickPhase(payload);
        }
        if (text.StartsWith("!")) return ParseImpliedPhase(text);

        // Pure-Read Naked Phase Routing (No string editing!)
        char firstChar = text.Length > 0 ? text[0] : ' ';
        string nakedPhaseChars = "0123456789bcdelrstzg";
        if (nakedPhaseChars.Contains(firstChar.ToString()))
        {
            bool hasPhaseDelimiter = false;
            int depthVar = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '(' || text[i] == '{') depthVar++;
                else if (text[i] == ')' || text[i] == '}') depthVar--;
                else if (depthVar == 0 && (text[i] == '@' || text[i] == ';' || text[i] == '#'))
                {
                    hasPhaseDelimiter = true;
                    break;
                }
            }

            if (hasPhaseDelimiter)
            {
                return ParseImpliedPhase(text);
            }
        }

        // Handles Textmod multipliers (e.g. x3.add.rat)
        if (text.StartsWith("x") && text.Contains("."))
        {
            int dotIdx = text.IndexOf('.');
            string multi = text.Substring(1, dotIdx - 1);
            if (int.TryParse(multi, out _)) return $"[x{multi}] " + Translate(text.Substring(dotIdx + 1));
        }

        if (text.StartsWith("add.")) return "Add Entity: " + ParseEntity(text.Substring(4));

        if (text.StartsWith("fight."))
        {
            List<string> parsedMonsters = new List<string>();
            foreach (string m in SplitRespectingParens(text.Substring(6), "+")) parsedMonsters.Add(ParseEntity(m));
            return "Fight Encounter with: [" + string.Join(", ", parsedMonsters) + "]";
        }

        if (text.StartsWith("party."))
        {
            List<string> parsedHeroes = new List<string>();
            foreach (string h in SplitRespectingParens(text.Substring(6), "+")) parsedHeroes.Add(ParseEntity(h));
            return "Set Party to: [" + string.Join(", ", parsedHeroes) + "]";
        }

        if (text.StartsWith("replace.")) return "Replace " + Translate(text.Substring(8));
        if (text.StartsWith("heropool."))
        {
            List<string> parsed = new List<string>();
            foreach (string h in SplitRespectingParens(text.Substring(9), "+")) parsed.Add(ParseEntity(h));
            return "Add to Hero Pool: " + string.Join(", ", parsed);
        }
        if (text.StartsWith("itempool."))
        {
            List<string> parsed = new List<string>();
            foreach (string i in SplitRespectingParens(text.Substring(9), "+")) parsed.Add(ParseEntity(i));
            return "Add to Item Pool: " + string.Join(", ", parsed);
        }
        if (text.StartsWith("monsterpool."))
        {
            List<string> parsed = new List<string>();
            foreach (string m in SplitRespectingParens(text.Substring(12), "+")) parsed.Add(ParseEntity(m));
            return "Add to Monster Pool: " + string.Join(", ", parsed);
        }
        if (text.StartsWith("allitem."))
        {
            List<string> parsed = new List<string>();
            foreach (string i in SplitRespectingParens(text.Substring(8), "+")) parsed.Add(ParseEntity(i));
            return "Grant all items from pool: " + string.Join(", ", parsed);
        }
        if (text.StartsWith("alliteme."))
        {
            List<string> parsed = new List<string>();
            foreach (string i in SplitRespectingParens(text.Substring(9), "+")) parsed.Add(ParseEntity(i));
            return "Grant all items from pool (equipped): " + string.Join(", ", parsed);
        }

        if (text.StartsWith("zone.")) return "Set Zone to: " + text.Substring(5);
        if (text.StartsWith("diff.")) return "Set Difficulty parameter: " + text.Substring(5);
        if (text.StartsWith("lvl.")) return "Level Constraint: " + Translate(text.Substring(4));

        return ParseRewardTag(text);
    }

    /// <summary>
    /// Parses the single-character identifier for Phases (e.g., '!', 'b', 'l')
    /// </summary>
    private static string ParseImpliedPhase(string text)
    {
        if (string.IsNullOrEmpty(text)) return "Unknown Phase";

        char phaseType = text[0];
        string data = text.Substring(1);

        switch (phaseType)
        {
            case '!': return ParseSimpleChoicePhase(data);
            case '0': return "Player Rolling Phase (reroll dice)";
            case '1': return "Targeting Phase (use dice abilities)";
            case '2': return ParseLevelEndPhase(data);
            case '3': return "Enemy Rolling Phase";
            case 'd': return "Damage Phase (enemies attack)";
            case '4': return ParseMessagePhase(data);
            case '5': return ParseHeroChangePhase(data);
            case '6': return "Reset Phase (curse mode reset, de-level heroes, remove non-mod items)";
            case '7': return ParseItemCombinePhase(data);
            case '8': return ParsePositionSwapPhase(data);
            case '9': return ParseChallengePhase(data);
            case 'b': return ParseBooleanPhase(data);
            case 'c': return ParseChoicePhase(data);
            case 'e': return "Run End Phase (ends the run)";
            case 'l': return ParseLinkedPhase(data);
            case 'r': return "Random Reveal popup showing: " + ParseRewardTag(data);
            case 's': return ParseSeqPhase(data);
            case 't': return ParseTradePhase(data);
            case 'g':
                int gDotIdx = data.IndexOf('.');
                string type = gDotIdx != -1 ? data.Substring(0, gDotIdx) : data;
                string translated = type == "h" ? "Generate random Level-up screen" : "Generate random Item screen";

                if (gDotIdx != -1)
                {
                    string properties = data.Substring(gDotIdx);
                    string translatedProps = ParseEntity("LogicBlock" + properties);
                    string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();
                    return $"{translated} {finalProps}".Trim();
                }
                return translated;
            case 'z': return ParseBooleanPhase2(data);
            default: return $"[Phase '{phaseType}' with data: {data}]";
        }
    }

    private static string ParseRewardTag(string text)
    {
        if (string.IsNullOrEmpty(text)) return "Nothing";

        // Extract the base name (before any dot properties)
        string baseName = text.Split('.')[0].Trim();
        bool isGlobalCommand = false;
        string matchedCommand = "";

        foreach (var cmd in GlobalCommands)
        {
            if (string.Equals(baseName, cmd, StringComparison.OrdinalIgnoreCase))
            {
                isGlobalCommand = true;
                matchedCommand = cmd;
                break;
            }
        }

        // If it's a global command, protect it from being parsed as a single-letter tag!
        if (isGlobalCommand)
        {
            // Fetch the explicit gameplay definition
            string descriptiveCommand = TranslateGlobalCommand(matchedCommand);

            if (text.Contains("."))
            {
                // If it has properties attached, ParseEntity will cleanly wrap the descriptive definition
                string properties = text.Substring(baseName.Length);
                return ParseEntity(descriptiveCommand + properties);
            }
            return descriptiveCommand;
        }

        char tag = text[0];
        string data = text.Substring(1);

        switch (tag)
        {
            case 'm':
                string translatedMod = Translate(data);
                if (!string.Equals(translatedMod, CapitalizeFirst(data), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(translatedMod, data, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Modifier ({translatedMod})";
                }
                return $"Modifier {ParseEntity(data)}";

            case 'i': return $"Item {ParseEntity(data)}";
            case 'l': return string.IsNullOrEmpty(data) ? "Level-up (lowest tier hero)" : $"Level-up to '{data}'";
            case 'g': return $"Hero {ParseEntity(data)}";
            case 'r':
                int rDot = data.IndexOf('.');
                if (rDot != -1)
                {
                    string basePayload = data.Substring(0, rDot);
                    string properties = data.Substring(rDot);
                    List<string> rParts = SplitRespectingParens(basePayload, "~", 3);
                    string rTranslated = rParts.Count >= 3 ? $"Grant {rParts[1]} random Tier {rParts[0]} {GetTagTypeName(rParts[2])}" : $"Random reward ({basePayload})";
                    string translatedProps = ParseEntity("LogicBlock" + properties);
                    string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();
                    return $"{rTranslated} {finalProps}".Trim();
                }
                else
                {
                    List<string> rParts = SplitRespectingParens(data, "~", 3);
                    if (rParts.Count >= 3) return $"Grant {rParts[1]} random Tier {rParts[0]} {GetTagTypeName(rParts[2])}";
                    return $"Random reward ({data})";
                }
            case 'q':
                int qDot = data.IndexOf('.');
                if (qDot != -1)
                {
                    string basePayload = data.Substring(0, qDot);
                    string properties = data.Substring(qDot);
                    List<string> qParts = SplitRespectingParens(basePayload, "~", 4);
                    string qTranslated = qParts.Count >= 4 ? $"Grant {qParts[2]} random {GetTagTypeName(qParts[3])}(s) between Tiers {qParts[0]} and {qParts[1]}" : $"Random Range reward ({basePayload})";
                    string translatedProps = ParseEntity("LogicBlock" + properties);
                    string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();
                    return $"{qTranslated} {finalProps}".Trim();
                }
                else
                {
                    List<string> qParts = SplitRespectingParens(data, "~", 4);
                    if (qParts.Count >= 4) return $"Grant {qParts[2]} random {GetTagTypeName(qParts[3])}(s) between Tiers {qParts[0]} and {qParts[1]}";
                    return $"Random Range reward ({data})";
                }
            case 'o':
                List<string> oChoices = SplitRespectingParens(data, "@4");
                List<string> oParsed = new List<string>();
                foreach (string choice in oChoices) oParsed.Add(Translate(choice));
                return "Randomly chosen from: [" + string.Join(" OR ", oParsed) + "]";
            case 'e':
                if (data == "RandoKeywordT1Item") return "Tier 1 random keyword item (rightmost side)";
                if (data == "RandoKeywordT5Item") return "Tier 5 random keyword item";
                if (data == "RandoKeywordT7Item") return "Tier 7 random keyword item (all sides)";
                return $"Enum Item ({data})";
            case 'v':
                int vDot = data.IndexOf('.');
                if (vDot != -1)
                {
                    string basePayload = data.Substring(0, vDot);
                    string properties = data.Substring(vDot);

                    string translatedVal = ParseValueTag(basePayload);
                    string translatedProps = ParseEntity("LogicBlock" + properties);
                    string finalProps = translatedProps.Replace("'LogicBlock'", "").Trim();

                    return $"{translatedVal} {finalProps}".Trim();
                }
                return ParseValueTag(data);
            case 'p':
                if (data.StartsWith("m"))
                {
                    List<string> pParts = SplitRespectingParens(data.Substring(1), "~", 2);
                    if (pParts.Count >= 2) return $"Replace {Translate(pParts[0])} with {Translate(pParts[1])}";
                }
                return $"Replace reward ({data})";
            case 's':
                return "Skip (Grant nothing)";
            default:
                if (text.Contains(".")) return ParseEntity(text);
                return text;
        }
    }

    private static string ParseValueTag(string data)
    {
        int lastV = data.LastIndexOf('V');
        if (lastV != -1) return $"Add {data.Substring(lastV + 1)} to hidden variable '{data.Substring(0, lastV)}'";
        return $"Change value ({data})";
    }

    // --- Specific Phase Parsers --- //

    private static string ParseEntity(string text)
    {
        text = text.Trim();

        // 1. Fast, deep extraction of explicit custom names
        // This regex looks for .n. or .mn. followed by the name, cleanly stopping at the next dot or closing paren.
        // It natively bypasses ALL nested brackets, parens, and hex-code garbage!
        Match nameMatch = Regex.Match(text, @"\.(?:m?n)\.([^.)]+)");
        if (nameMatch.Success)
        {
            string extractedName = nameMatch.Groups[1].Value.Trim();

            // Strip off sloppy trailing parentheses that might have stuck to the end of the name
            while (extractedName.EndsWith(")")) extractedName = extractedName.Substring(0, extractedName.Length - 1).Trim();

            return $"'{extractedName}'";
        }

        // 2. If no custom name exists, gracefully fall back to the base class/item name
        if (text.StartsWith("(") && text.EndsWith(")"))
        {
            if (FindMatchingClosingParenthesis(text, 0) == text.Length - 1)
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }
        }

        List<string> tokens = SplitRespectingParens(text, ".");
        if (tokens.Count == 0) return "'Entity'";

        string entityName = tokens[0];

        // Handle "replica.BaseClass" by targeting the second token
        if (entityName.Equals("replica", StringComparison.OrdinalIgnoreCase) && tokens.Count > 1)
        {
            entityName = tokens[1];
        }

        // Scan ONLY for explicitly defined Custom Names (.n.Name or .mn.EncounterName)
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];

            // S&D uses '.n.' for Custom Entity names, and '.mn.' for Custom Encounter names
            if ((token == "n" || token == "mn") && i + 1 < tokens.Count)
            {
                entityName = tokens[i + 1];
                break; // We found the custom name! No need to parse anything else.
            }
        }

        // Clean up base names that might have trailing parentheses
        if (entityName.StartsWith("(") && entityName.EndsWith(")"))
        {
            entityName = entityName.Substring(1, entityName.Length - 2).Trim();
        }
        else if (entityName.StartsWith("("))
        {
            entityName = entityName.Substring(1).Trim();
        }

        return $"'{entityName}'";
    }

    private static string ParseBooleanPhase(string data)
    {
        // Split exactly 3 parts: [Variable], [Threshold], [Everything Else]
        List<string> parts = SplitRespectingParens(data, ";", 3);
        if (parts.Count < 3) return $"Boolean Phase ({data})";

        string varName = parts[0];
        string threshold = parts[1];

        // Split branches exactly 2 parts so nested chained booleans pass intact into the FALSE branch
        List<string> branches = SplitRespectingParens(parts[2], "@2", 2);

        string trueBranch = branches.Count > 0 ? Translate(branches[0]) : "Nothing";
        string falseBranch = branches.Count > 1 ? Translate(branches[1]) : "Nothing";

        return $"Check if '{varName}' >= {threshold} -> \n TRUE: \n [{trueBranch}] | \n FALSE: \n [{falseBranch}]";
    }

    private static string ParseBooleanPhase2(string data)
    {
        // Identical fix to BooleanPhase but utilizing BooleanPhase2's delimiters
        List<string> parts = SplitRespectingParens(data, "@6", 3);
        if (parts.Count < 3) return $"Boolean Phase 2 ({data})";

        string varName = parts[0];
        string threshold = parts[1];

        List<string> branches = SplitRespectingParens(parts[2], "@7", 2);

        string trueBranch = branches.Count > 0 ? Translate(branches[0]) : "Nothing";
        string falseBranch = branches.Count > 1 ? Translate(branches[1]) : "Nothing";

        return $"Check if '{varName}' >= {threshold} -> \nTRUE: \n [{trueBranch}] | \nFALSE: \n[{falseBranch}]";
    }

    private static string ParseLevelEndPhase(string data)
    {
        Match m = Regex.Match(data, @"ps:\[(.*?)\]");
        if (m.Success)
        {
            List<string> innerPhases = SplitRespectingParens(m.Groups[1].Value, ",");
            List<string> parsedInner = new List<string>();
            foreach (string ip in innerPhases) parsedInner.Add(Translate(ip));
            return "Level End Screen containing: " + string.Join(" AND ", parsedInner);
        }
        return "Level End Screen";
    }

    private static string ParseLinkedPhase(string data)
    {
        List<string> phases = SplitRespectingParens(data, "@1");
        List<string> parsedPhases = new List<string>();

        for (int i = 0; i < phases.Count; i++)
        {
            string phaseStr = phases[i];

            // Handle the textmod quirk where subsequent LinkedPhases start with 'l' 
            // to continue the chain. Stripping it here flattens them into a readable sequence.
            if (i > 0 && phaseStr.StartsWith("l"))
            {
                phaseStr = phaseStr.Substring(1);
            }

            parsedPhases.Add($"{i + 1}) {Translate(phaseStr)}");
        }
        return "Sequential Event:\n  " + string.Join("\n  ", parsedPhases);
    }

    private static string ParseDiceSides(string sdData)
    {
        string[] sides = sdData.Split(':');
        List<string> parsedSides = new List<string>();
        foreach (string side in sides)
        {
            if (side == "0" || string.IsNullOrEmpty(side)) parsedSides.Add("Blank");
            else parsedSides.Add(side);
        }
        return $"[Dice Sides: {string.Join(", ", parsedSides)}]";
    }

    private static string ParseSimpleChoicePhase(string data)
    {
        if (string.IsNullOrEmpty(data)) return "Gain: (None)";

        List<string> choices = SplitRespectingParens(data, "@3");
        if (choices.Count == 0 || string.IsNullOrEmpty(choices[0])) return "Gain: (None)";

        string title = "Select a reward";

        if (choices[0].Contains(";"))
        {
            char firstChar = choices[0][0];
            if (!"b4c013d".Contains(firstChar.ToString()))
            {
                List<string> titleParts = SplitRespectingParens(choices[0], ";", 2);
                if (titleParts.Count >= 2)
                {
                    title = $"'{titleParts[0]}'";
                    choices[0] = titleParts[1];
                }
            }
        }

        List<string> parsedChoices = new List<string>();
        foreach (string choice in choices)
        {
            // Recursively route choices back to Translate so & commands unfold perfectly
            if (!string.IsNullOrEmpty(choice)) parsedChoices.Add(Translate(choice));
        }

        if (parsedChoices.Count == 1 && title == "Select a reward") return $"Gain: {parsedChoices[0]}";
        return $"{title}: " + string.Join(", ", parsedChoices);
    }

    private static string ParseMessagePhase(string data)
    {
        // Limit to 2 splits in case the message body contains a semicolon
        List<string> parts = SplitRespectingParens(data, ";", 2);
        string msg = parts[0];
        string btn = parts.Count > 1 ? parts[1] : "Ok";
        return $"Show message: \"{msg}\" (Button: '{btn}')";
    }

    private static string ParseHeroChangePhase(string data)
    {
        if (data.Length < 2) return "Hero Change Phase (invalid format)";
        int index = data[0] - '0';
        string type = data[1] == '0' ? "a random class" : "a generated hero";
        return $"Offer to reroll hero at position {index + 1} into {type}";
    }

    private static string ParseItemCombinePhase(string data)
    {
        if (data == "SecondHighestToTierThrees") return "Smithing Phase (Smash second-highest tier item into multiple Tier 3s)";
        if (data == "ZeroToThreeToSingle") return "Smithing Phase (Combine Tier 0-3 items into one high-tier item)";
        return $"Item Combine Phase: {data}";
    }

    private static string ParsePositionSwapPhase(string data)
    {
        if (data.Length < 2) return "Position Swap Phase (invalid format)";
        return $"Offer to swap heroes at position {(data[0] - '0') + 1} and {(data[1] - '0') + 1}";
    }

    private static string ParseChallengePhase(string data)
    {
        string rewards = "Unknown rewards";
        string monsters = "Unknown monsters";

        // 1. Extract the Reward Payload safely without relying on JSON quote formats
        int dataIdx = data.IndexOf("\"data\"");
        if (dataIdx == -1) dataIdx = data.IndexOf("data"); // Fallback for alternative quote layouts

        if (dataIdx != -1)
        {
            int colonIdx = data.IndexOf(':', dataIdx);
            if (colonIdx != -1)
            {
                // Find the end of the reward bracket '}'
                int braceIdx = data.IndexOf('}', colonIdx);
                if (braceIdx != -1)
                {
                    string rawVal = data.Substring(colonIdx + 1, braceIdx - colonIdx - 1).Trim();

                    // Clean off JSON outer-bracket quote layers
                    rawVal = StripJsonQuotes(rawVal);

                    // S&D JSON separates multiple challenge rewards using inline quotes and commas (e.g. `",\"` or `",\”`)
                    // We normalize these delimiters into standard '@3' dividers so ParseSimpleChoicePhase can read them
                    string normalizedRewards = Regex.Replace(rawVal, @"(?:\\""|""|\\”|”|“)\s*,\s*(?:\\""|""|\\”|”|“)", "@3");

                    // Clean up any remaining raw commas
                    normalizedRewards = Regex.Replace(normalizedRewards, @"\s*,\s*", "@3");

                    rewards = ParseSimpleChoicePhase(normalizedRewards);
                }
            }
        }

        // 2. Extract the Monster Payload safely via brackets
        int monsterIdx = data.IndexOf("extraMonsters");
        if (monsterIdx != -1)
        {
            int openBracket = data.IndexOf('[', monsterIdx);
            int closeBracket = data.IndexOf(']', monsterIdx);
            if (openBracket != -1 && closeBracket != -1)
            {
                string rawMonsters = data.Substring(openBracket + 1, closeBracket - openBracket - 1);
                string[] monsterArray = rawMonsters.Split(',');

                List<string> parsedMonsters = new List<string>();
                foreach (string m in monsterArray)
                {
                    string cleanM = StripJsonQuotes(m).Trim();
                    if (!string.IsNullOrEmpty(cleanM))
                    {
                        parsedMonsters.Add(ParseEntity(cleanM));
                    }
                }
                monsters = string.Join(", ", parsedMonsters);
            }
        }

        return $"Challenge Phase! Add enemies [{monsters}] to gain: {rewards}";
    }


    private static string ParseChoicePhase(string data)
    {
        int semiIndex = data.IndexOf(';');
        if (semiIndex == -1) return $"Choice Phase ({data})";

        string config = data.Substring(0, semiIndex);
        string rest = data.Substring(semiIndex + 1);
        string[] configParts = config.Split('#');
        string type = configParts[0];
        string num = configParts.Length > 1 ? configParts[1] : "?";

        List<string> choices = SplitRespectingParens(rest, "@3");
        string title = "";

        // Cleanly isolate trailing semicolon titles from the last choice
        if (choices.Count > 0)
        {
            List<string> lastChoiceParts = SplitRespectingParens(choices[choices.Count - 1], ";");
            if (lastChoiceParts.Count > 1)
            {
                title = $"'{lastChoiceParts[lastChoiceParts.Count - 1]}'";
                lastChoiceParts.RemoveAt(lastChoiceParts.Count - 1);
                choices[choices.Count - 1] = string.Join(";", lastChoiceParts);
            }
        }

        List<string> parsedChoices = new List<string>();
        foreach (string choice in choices) parsedChoices.Add(Translate(choice));

        string titleDisplay = string.IsNullOrEmpty(title) ? "" : $" ({title})";
        return $"Choice Screen{titleDisplay} ({type} up to {num} rewards): " + string.Join(", ", parsedChoices);
    }

    private static string ParseSeqPhase(string data)
    {
        // Splits cleanly on @1, @2, @3, etc. while respecting parens!
        List<string> parts = SplitRespectingParens(data, @"@\d+", isRegex: true);

        if (parts.Count == 0) return "Sequence Phase";

        string firstPart = parts[0];
        string message = "Message";

        // Safety check: ensure the string is not empty before accessing characters
        if (!string.IsNullOrEmpty(firstPart))
        {
            // Check if the message is actually a nested Phase identifier (like '4' or '!')
            if ("0123d456789bcelrstgz!".Contains(firstPart[0].ToString()))
            {
                message = Translate(firstPart);
            }
            else
            {
                // Otherwise, it's just literal text
                message = $"'{firstPart}'";
            }
        }

        List<string> options = new List<string>();
        string currentButton = "Unknown Button";
        List<string> currentConsequences = new List<string>();

        // We manually extract the delimiters using Regex to see if they are buttons (@1) or actions (@2)
        // We check depth by pulling ONLY @ markers that aren't trapped in parentheses
        int parenDepth = 0;
        List<int> validMarkers = new List<int>();

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == '(') parenDepth++;
            else if (data[i] == ')') parenDepth--;
            else if (parenDepth == 0 && data[i] == '@' && i < data.Length - 1)
            {
                // Find the full number after the @
                int numStart = i + 1;
                int numLen = 0;
                while (numStart + numLen < data.Length && char.IsDigit(data[numStart + numLen])) numLen++;

                if (numLen > 0 && int.TryParse(data.Substring(numStart, numLen), out int marker))
                {
                    validMarkers.Add(marker);
                }
            }
        }

        for (int i = 1; i < parts.Count; i++)
        {
            string part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            // Default to action
            int marker = 2;
            if (i - 1 < validMarkers.Count)
            {
                marker = validMarkers[i - 1];
            }

            // Odd numbers (@1, @3) are buttons. Even numbers (@2, @4) are consequences.
            if (marker % 2 != 0)
            {
                // If we already had a button, save it before starting a new one
                if (currentConsequences.Count > 0 || currentButton != "Unknown Button")
                {
                    options.Add($"Button [{currentButton}] -> {string.Join(" THEN ", currentConsequences)}");
                }

                currentButton = part;
                currentConsequences.Clear();
            }
            else
            {
                currentConsequences.Add(Translate(part));
            }
        }

        // Save the final button
        if (currentConsequences.Count > 0 || currentButton != "Unknown Button")
        {
            options.Add($"Button [{currentButton}] -> {string.Join(" THEN ", currentConsequences)}");
        }

        return $"Story Choice: {message}\n  Options:\n    " + string.Join("\n    ", options);
    }

    private static string ParseTradePhase(string data)
    {
        List<string> items = SplitRespectingParens(data, "@3", 2);
        if (items.Count >= 2)
            return $"Cursed Chest Trade: Accept BOTH [{ParseRewardTag(items[0])}] AND [{ParseRewardTag(items[1])}] or decline.";
        return $"Trade Phase ({data})";
    }

    // --- Hardcoded Pre-defined Modifiers --- //

    private static string ParseIndexedPhase(string data)
    {
        // Clean data once more before evaluation
        data = data.Trim();
        switch (data)
        {
            case "0": return "Level-up Phase";
            case "1": return "Standard Loot Phase";
            case "2":
            case "3": return "Reroll Phase";
            case "4": return "Optional Tweak Phase";
            case "5":
            case "8": return "Hero Position Swap Phase";
            case "6": return "Standard Challenge Phase";
            case "7": return "Easy Challenge Phase";
            case "9": return "Trade Phase (Cursed Chest)";
            default: return $"Unknown Phase, Possible Read Error: {data}";
        }
    }

    private static string ParseModPickPhase(string data)
    {
        return $"Modifier Selection Screen (Total value objective: {data})";
    }

    // --- Utility Methods --- //

    /// <summary>
    /// Safely splits a string by a delimiter, ignoring delimiters that appear inside parentheses `()`.
    /// Adding maxSplits guarantees nested command chains lacking parentheses stay bound together.
    /// </summary>
    /// <summary>
    /// Safely splits a string by a delimiter, ignoring delimiters that appear inside parentheses `()`.
    /// Adding maxSplits guarantees nested command chains lacking parentheses stay bound together.
    /// </summary>
    /// <summary>
    /// Safely splits a string by a delimiter, ignoring delimiters that appear inside 
    /// parentheses (), square brackets [], or curly braces {}.
    /// </summary>
    private static List<string> SplitRespectingParens(string text, string delimiter, int maxSplits = -1, bool isRegex = false)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(text)) { result.Add(""); return result; }

        int parenDepth = 0, braceDepth = 0;
        int lastIndex = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') parenDepth++;
            else if (text[i] == ')') parenDepth--;
            //else if (text[i] == '[') bracketDepth++;
            //else if (text[i] == ']') bracketDepth--;
            else if (text[i] == '{') braceDepth++; else if (text[i] == '}') braceDepth--;

            if (parenDepth == 0 && braceDepth == 0 && i <= text.Length - delimiter.Length)
            {
                int matchLength = 0;

                if (isRegex)
                {
                    Match m = Regex.Match(text.Substring(i), "^" + delimiter);
                    if (m.Success) matchLength = m.Length;
                }
                else if (text.Substring(i, delimiter.Length) == delimiter)
                {
                    matchLength = delimiter.Length;
                }

                if (matchLength > 0)
                {
                    result.Add(text.Substring(lastIndex, i - lastIndex).Trim());
                    i += matchLength - 1;
                    lastIndex = i + 1;
                    if (maxSplits > 0 && result.Count == maxSplits - 1) break;
                }
            }
        }
        result.Add(text.Substring(lastIndex).Trim());
        return result;
    }

    private static string GetTagTypeName(string tagLetter)
    {
        if (tagLetter == "m") return "Modifier";
        if (tagLetter == "i") return "Item";
        if (tagLetter == "l") return "Level-up";
        if (tagLetter == "g") return "Hero";
        return "Reward";
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    /// <summary>
    /// Converts layout brackets ([n] -> newline) and safely strips coloring/icon noise 
    /// without destroying JSON arrays.
    /// </summary>
    /// <summary>
    /// Converts layout brackets ([n] -> newline) and safely strips coloring/icon noise.
    /// Supports S&D's sloppy bracket closures where '[' is used instead of ']'.
    /// </summary>
    /// <summary>
    /// Converts layout brackets ([n] -> newline) and safely strips coloring/icon noise.
    /// Supports S&D's sloppy bracket closures where '[' is used instead of ']'.
    /// </summary>
    private static string CleanFormattingTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Use a safe placeholder for commas so they don't break depth-0 pool splitting!
        text = Regex.Replace(text, @"\[comma[\]\[]", "**COMMA**", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[n[\]\[]", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\[dot[\]\[]", ".", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"\[val([\w-]+)[\]\[]", "(Value: $1)", RegexOptions.IgnoreCase);

        // Strip purely cosmetic formatting tags (like [orange[, [/cu[, [hp-plus[, [q[, or hex image strings).
        // Using \w\-\=\:\+\#\% strictly avoids stripping JSON arrays like [\"Militia\"] because it ignores quotes/commas.
        text = Regex.Replace(text, @"\[/?[\w\-\=\:\+\#\%]+[\]\[]", "");

        text = Regex.Replace(text, @"\s{2,}", " ");

        return text;
    }

    private static int FindMatchingClosingParenthesis(string s, int openIdx)
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

    /// <summary>
    /// Safely strips JSON noise including escaped, stylized, or curly smart-quotes from S&D string payloads.
    /// </summary>
    private static string StripJsonQuotes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        input = input.Trim();

        // Strips straight, escaped, and opening/closing curly smart-quotes from bounds
        input = Regex.Replace(input, @"^(?:\\""|""|\\”|”|“)", "");
        input = Regex.Replace(input, @"(?:\\""|""|\\”|”|“)$", "");
        return input;
    }

    /// <summary>
    /// Calculates standard English ordinal suffixes (st, nd, rd, th) mathematically.
    /// </summary>
    private static string GetOrdinalSuffix(string numStr)
    {
        if (int.TryParse(numStr, out int num))
        {
            if (num % 100 >= 11 && num % 100 <= 13) return "th";
            switch (num % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
            }
        }
        return "th";
    }

    /// <summary>
    /// Translates raw S&D global commands and hidden modifiers into their exact gameplay definitions.
    /// </summary>
    /// 

    private static string TranslateGlobalCommand(string cmd)
    {
        switch (cmd.ToLower())
        {
            case "skip":
                return "Skip (Modifier with no effect)";
            case "wish":
                return "Wish (Enable Wish Mode: customize items, heroes, modifiers, and skip fights)";
            case "clear party":
            case "missing":
                return "Clear Party (Remove entire party of heroes)";
            case "temporary":
                return "Temporary (Modifier is removed after a single combat)";
            case "hidden":
                return "Hidden (Modifier is invisible on the modifier menu)";
            case "skip all":
                return "Skip All (Skip all reward choices and event phases)";
            case "add fight":
                return "Add Fight (Increases custom run length by 1 fight)";
            case "add 10 fights":
                return "Add 10 Fights (Increases custom run length by 10 fights)";
            case "add 100 fights":
                return "Add 100 Fights (Increases custom run length by 100 fights)";
            case "minus fight":
                return "Minus Fight (Decreases custom run length by 1 fight)";
            case "cursemode loopdiff":
                return "Cursemode Loopdiff (Matches Enemy Balance on Level 21 and Level 1 for Cursed runs)";
            case "double monsters":
                return "Double Monsters (Spawns double the normal enemies)";
            case "skip rewards":
                return "Skip Rewards (Skip normal item/upgrade choices)";
            case "no flee":
                return "No Flee (Enemies cannot escape)";
            case "delevel":
                return "Delevel (Demote all heroes by 1 level)";
            case "level up":
            case "level-up":
                return "Level Up (Promote all heroes by 1 level)";
            default:
                return CapitalizeFirst(cmd);
        }
    }

    /// <summary>
    /// Proactively checks if a string is structured as an S&D Phase command (rather than a modifier list).
    /// </summary>

    private static bool IsPhaseString(string text)
    {
        text = text.Trim();

        // Strip floor prefix if present to check the core command
        Match floorMatch = Regex.Match(text, @"^((?:e\d+(?:\.\d+)?|\-?\d+(?:-\-?\d+)?)\.)");
        if (floorMatch.Success)
        {
            text = text.Substring(floorMatch.Length).Trim();
        }

        // Strip outer parens
        if (text.StartsWith("(") && text.EndsWith(")"))
        {
            if (FindMatchingClosingParenthesis(text, 0) == text.Length - 1)
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }
        }

        if (text.StartsWith("ch.")) return false; // Choosable modifier (Not a phase)

        // 1. Direct prefix match
        if (text.StartsWith("ph.") || text.StartsWith("phi.") || text.StartsWith("phmp.") || text.StartsWith("!"))
            return true;

        // 2. Pure-Read Naked Phase check (No string editing!)
        char firstChar = text.Length > 0 ? text[0] : ' ';
        string nakedPhaseChars = "0123456789bcdelrstzg";
        if (nakedPhaseChars.Contains(firstChar.ToString()))
        {
            // A naked character is only a phase if it is immediately followed by phase-level delimiters
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '(' || text[i] == '{') depth++;
                else if (text[i] == ')' || text[i] == '}') depth--;
                else if (depth == 0)
                {
                    if (text[i] == '@' || text[i] == ';' || text[i] == '#')
                        return true;
                }
            }
        }

        return false;
    }
}