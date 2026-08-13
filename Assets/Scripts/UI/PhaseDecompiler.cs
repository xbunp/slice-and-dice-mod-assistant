using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class PhaseDecompiler
{
    // Entry Point: Kicks off the entire process
    public static void Decompile(string text, PhasesUI ui)
    {
        ui.ClearWorkspace();
        if (string.IsNullOrWhiteSpace(text)) return;

        text = SanitizeInput(text);

        // This is the main recursive call on the root workspace
        ParseSegments(text, ui.GetRootZone(), "&");
    }

    // Main recursive function: splits by the target delimiter while respecting depth
    private static void ParseSegments(string text, ReorderableZone zone, string delimiter)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var parts = TopLevelStringSplit(text, delimiter);
        for (int i = 0; i < parts.Count; i++)
        {
            CreateNodeFromSegment(parts[i], zone);
        }
    }

    // Factory: Identifies what a string segment is and creates the correct card for it
    public static PhaseCard CreateNodeFromSegment(string segment, ReorderableZone zone)
    {
        segment = segment.Trim();
        if (string.IsNullOrEmpty(segment)) return null;

        PhaseCard card = null;
        PhasesUI ui = zone.gameObject.GetComponentInParent<PhasesUI>();

        // 1. ACTION BLOCKS
        if (segment.StartsWith("!m("))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.ActionBlock);
            string payload = StaticBranchTracing.StripOuterParens(segment.Substring(2)); // Remove !m and strip ()
            ParseSegments(payload, card.PayloadPort, "&");
        }
        // 2. CHOICE OPTIONS (@1, @2)
        else if (segment.StartsWith("@") && segment.Length > 1 && char.IsDigit(segment[1]))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.ChoiceOption);

            // Extract the number
            string idStr = new string(segment.Substring(1).TakeWhile(char.IsDigit).ToArray());
            int.TryParse(idStr, out card.Data.Num1);

            // Extract the text and the child payload
            string remainder = segment.Substring(1 + idStr.Length);

            // Options often contain their own payloads split by @2, @3, etc.
            // We search for the first top-level @ digit delimiter to split text from nested actions
            int nextDelimIdx = FindFirstTopLevelDelimiter(remainder, "@");

            if (nextDelimIdx != -1)
            {
                card.Data.PrimaryText = remainder.Substring(0, nextDelimIdx);
                // recursively parse the rest (e.g., !m(...) blocks attached to this option)
                ParseSegments(remainder.Substring(nextDelimIdx), card.PayloadPort, "&");
            }
            else
            {
                card.Data.PrimaryText = remainder;
            }
        }
        // 3. PHASE ROOTS (ph, ch, phi, phmp)
        else if (Regex.IsMatch(segment, @"^(\d+\.)?(ph|ch|phi|phmp)"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.PhaseRoot);

            // Extract Timing Prefix if present
            var prefixMatch = Regex.Match(segment, @"^(\d+)\.");
            if (prefixMatch.Success)
            {
                card.Data.SecondaryText = prefixMatch.Groups[1].Value;
                segment = segment.Substring(prefixMatch.Length);
            }

            // Route standard Phase structures
            if (segment.StartsWith("ch."))
            {
                card.Data.PrimaryText = "ch";
                ParseSegments(segment.Substring(3), card.PayloadPort, "@4"); // Choosables usually split Or by @4
            }
            else if (segment.StartsWith("ph.!"))
            {
                card.Data.PrimaryText = "ph.!";
                string payload = segment.Substring(4);

                // Extract Title if present (ends with ;)
                int titleSplit = payload.IndexOf(';');
                if (titleSplit != -1 && titleSplit < payload.IndexOf('@'))
                {
                    card.Data.TertiaryText = payload.Substring(0, titleSplit);
                    payload = payload.Substring(titleSplit + 1);
                }

                ParseSegments(payload, card.PayloadPort, "@3"); // SCPhase uses @3
            }
            else if (segment.StartsWith("ph.s"))
            {
                // Sequence Phase
                card.NodeType = PhaseNodeType.PhaseSeq;
                card.Data = new PhaseCardData(PhaseNodeType.PhaseSeq);

                string payload = segment.Substring(4);
                int msgSplit = payload.IndexOf("@1");
                if (msgSplit != -1)
                {
                    card.Data.PrimaryText = payload.Substring(0, msgSplit);
                    ParseSegments(payload.Substring(msgSplit), card.PayloadPort, "@1");
                }
                else card.Data.PrimaryText = payload;
            }
            else if (segment.StartsWith("ph."))
            {
                // Complex Choice Phase (e.g. ph.bbarty;1;!m(...))
                card.Data.PrimaryText = "ph";
                string payload = segment.Substring(3);

                var chunks = TopLevelStringSplit(payload, ";");
                if (chunks.Count > 0)
                {
                    // Everything up to the first ; is the entity/var name
                    // But if there are no semicolons, it's just raw children
                    if (chunks.Count == 1)
                    {
                        ParseSegments(chunks[0], card.PayloadPort, "");
                    }
                    else
                    {
                        card.Data.PrimaryText = chunks[0];
                        string children = string.Join(";", chunks.Skip(1));

                        // Break down options using @ sequence logic
                        ParseSegments(children, card.PayloadPort, "@");
                    }
                }
            }
            else
            {
                // Fallback for phi/phmp 
                card.Data.PrimaryText = segment;
            }
        }
        // 4. REWARDS / TAGS
        else if (segment.StartsWith("r") && segment.Contains("~"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardRandom);
            var parts = segment.Substring(1).Split('~');
            if (parts.Length == 3)
            {
                int.TryParse(parts[0], out card.Data.Num1);
                int.TryParse(parts[1], out card.Data.Num2);
                card.Data.PrimaryText = parts[2];
            }
        }
        else if (segment.StartsWith("q") && segment.Contains("~"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardRandomRange);
            var parts = segment.Substring(1).Split('~');
            if (parts.Length == 4)
            {
                int.TryParse(parts[0], out card.Data.Num1);
                int.TryParse(parts[1], out card.Data.Num2);
                int.TryParse(parts[2], out card.Data.Num3);
                card.Data.PrimaryText = parts[3];
            }
        }
        else if (segment.StartsWith("o"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardOr);
            ParseSegments(segment.Substring(1), card.PayloadPort, "@4");
        }
        else if (segment.StartsWith("v") && segment.Contains("V"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardValue);
            var parts = segment.Substring(1).Split('V');
            if (parts.Length == 2)
            {
                card.Data.PrimaryText = parts[0];
                int.TryParse(parts[1], out card.Data.Num1);
            }
        }
        else if (segment.StartsWith("pm") && segment.Contains("~"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardReplace);
            var parts = segment.Substring(2).Split('~');
            if (parts.Length == 2)
            {
                card.Data.PrimaryText = parts[0];
                card.Data.TertiaryText = parts[1].Substring(0, 1); // Extract Tag (m/i/l/g)
                card.Data.SecondaryText = parts[1].Substring(1);   // Extract Entity Name
            }
        }
        else if (segment.StartsWith("e"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardEnu);
            card.Data.PrimaryText = segment.Substring(1);
        }
        else if (segment == "s")
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardSkip);
        }
        else if (segment.StartsWith("m") || segment.StartsWith("i") || segment.StartsWith("l") || segment.StartsWith("g"))
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RewardStandard);
            card.Data.PrimaryText = segment.Substring(0, 1);
            card.Data.SecondaryText = segment.Substring(1);
        }
        // 5. RAW STRING FALLBACK
        else
        {
            card = ui.CreatePhaseCard(PhaseNodeType.RawString);
            card.Data.PrimaryText = segment;
        }

        if (card != null)
        {
            zone.AddEntrant(card);
        }
        return card;
    }

    private static string SanitizeInput(string text)
    {
        text = text.Trim();
        if (text.StartsWith("=")) text = text.Substring(1);

        // Strip modifier wrappers if they encapsulate the whole string
        text = StaticBranchTracing.StripOuterParens(text);

        return text;
    }

    // --- STRING SPLITTERS WITH PARENTHESIS TRACKING ---

    private static List<string> TopLevelStringSplit(string input, string separator)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(separator))
        {
            if (!string.IsNullOrEmpty(input)) result.Add(input);
            return result;
        }

        int p = 0, b = 0, br = 0, start = 0;
        for (int i = 0; i <= input.Length - separator.Length; i++)
        {
            char c = input[i];
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '[') b++;
            else if (c == ']') b--;
            else if (c == '{') br++;
            else if (c == '}') br--;

            // Check for match
            if (p == 0 && b == 0 && br == 0)
            {
                if (input.Substring(i, separator.Length) == separator)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + separator.Length;
                    i += separator.Length - 1; // Skip the rest of the separator
                }
            }
        }
        result.Add(input.Substring(start));

        // Remove empty artifacts
        result.RemoveAll(string.IsNullOrWhiteSpace);
        return result;
    }

    private static int FindFirstTopLevelDelimiter(string input, string separatorPrefix)
    {
        int p = 0, b = 0, br = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '[') b++;
            else if (c == ']') b--;
            else if (c == '{') br++;
            else if (c == '}') br--;

            if (p == 0 && b == 0 && br == 0 && input.Substring(i).StartsWith(separatorPrefix))
            {
                // Ensure it's actually an @ followed by a number
                if (i + 1 < input.Length && char.IsDigit(input[i + 1]))
                {
                    return i;
                }
            }
        }
        return -1;
    }
}