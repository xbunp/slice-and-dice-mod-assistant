using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    public class ModBlockOverview
    {
        public List<string> BlockTypes { get; set; } = new List<string>();
        public string BlockName { get; set; }
        public List<ModDetail> Details { get; set; } = new List<ModDetail>();
    }

    public class ModDetail
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Round { get; set; }
        public int Depth { get; set; }
    }

    public static class ModAnalyzer
    {
        public static List<ModBlockOverview> Analyze(string rawModString)
        {
            var overviews = new List<ModBlockOverview>();
            if (string.IsNullOrWhiteSpace(rawModString)) return overviews;

            string normalizedString = rawModString.Replace("\r", "").Replace("\n", "");
            string[] rawBlocks = normalizedString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string block in rawBlocks)
            {
                string trimmedBlock = block.Trim();
                if (string.IsNullOrEmpty(trimmedBlock)) continue;

                var overview = ParseBlock(trimmedBlock);
                if (overview != null) overviews.Add(overview);
            }

            return overviews;
        }

        private static ModBlockOverview ParseBlock(string blockText)
        {
            var overview = new ModBlockOverview();

            // 1. Unwrap enclosing parens first to allow top-level delimiter splits
            blockText = Unwrap(blockText);

            string customName = ExtractTagValue(blockText, "mn");
            overview.BlockName = !string.IsNullOrEmpty(customName) ? customName : "Unnamed Block";

            string processingText = blockText;
            if (processingText.StartsWith("="))
            {
                overview.BlockTypes.Add("Auto-Execute");
                processingText = processingText.Substring(1).Trim();
            }

            List<string> components = SplitByActionDelimiter(processingText, '&');

            foreach (string component in components)
            {
                ProcessComponent(component, overview, depth: 0);
            }

            if (overview.BlockName == "Unnamed Block" && overview.BlockTypes.Count > 0)
            {
                overview.BlockName = overview.Details.FirstOrDefault()?.Value ?? "Config Block";
            }

            return overview;
        }

        private static void ProcessComponent(string component, ModBlockOverview overview, string inheritedRound = null, int depth = 0)
        {
            string cleanComponent = component.Trim();

            // Convert condition wrappers like `!m(` to standard `(` so Unwrap can evaluate it
            if (Regex.IsMatch(cleanComponent, @"^!?m?\("))
            {
                cleanComponent = Regex.Replace(cleanComponent, @"^!?m?\(", "(");
            }

            cleanComponent = Unwrap(cleanComponent);

            string round = ExtractRoundPrefix(ref cleanComponent) ?? inheritedRound;

            // Re-unwrap post round-strip in case of wrapped phases e.g., 0.(ph.s...)
            cleanComponent = Unwrap(cleanComponent);

            if (string.IsNullOrEmpty(cleanComponent)) return;

            // ... Check Boolean / Sequence Phases (Omitted unmodified match checks for brevity here)
            var boolMatch = Regex.Match(cleanComponent, @"^b?ph\.b([^;]+);([^;]+);(.*)", RegexOptions.IgnoreCase);
            if (boolMatch.Success)
            {
                if (!overview.BlockTypes.Contains("Boolean Phase")) overview.BlockTypes.Add("Boolean Phase");
                overview.Details.Add(new ModDetail { Title = "Condition Check", Value = $"If {boolMatch.Groups[1].Value} >= {boolMatch.Groups[2].Value}", Round = round, Depth = depth });
                var branches = SplitByStringDelimiter(boolMatch.Groups[3].Value, "@2");
                if (branches.Count > 0) ProcessComponent(branches[0], overview, round, depth + 1);
                if (branches.Count > 1) ProcessComponent(branches[1], overview, round, depth + 1);
                return;
            }

            var bool2Match = Regex.Match(cleanComponent, @"^b?ph\.z([^@]+)@6([^@]+)@6(.*)", RegexOptions.IgnoreCase);
            if (bool2Match.Success)
            {
                if (!overview.BlockTypes.Contains("Boolean Phase")) overview.BlockTypes.Add("Boolean Phase");
                overview.Details.Add(new ModDetail { Title = "Condition Check", Value = $"If {bool2Match.Groups[1].Value} >= {bool2Match.Groups[2].Value}", Round = round, Depth = depth });
                var branches = SplitByStringDelimiter(bool2Match.Groups[3].Value, "@7");
                if (branches.Count > 0) ProcessComponent(branches[0], overview, round, depth + 1);
                if (branches.Count > 1) ProcessComponent(branches[1], overview, round, depth + 1);
                return;
            }

            var seqMatch = Regex.Match(cleanComponent, @"^b?ph\.s(.*)", RegexOptions.IgnoreCase);
            if (seqMatch.Success)
            {
                if (!overview.BlockTypes.Contains("Seq Phase")) overview.BlockTypes.Add("Seq Phase");
                var parts = SplitByStringDelimiter(seqMatch.Groups[1].Value, "@1");
                overview.Details.Add(new ModDetail { Title = "Sequence Decision", Value = SanitizeText(parts.Count > 0 ? parts[0] : "Choice Event", 60), Round = round, Depth = depth });
                for (int i = 1; i < parts.Count; i++)
                {
                    var choiceParts = SplitByStringDelimiter(parts[i], "@2");
                    overview.Details.Add(new ModDetail { Title = "Choice Option", Value = $"Button: {SanitizeText(choiceParts.Count > 0 ? choiceParts[0] : "Next", 40)}", Round = round, Depth = depth + 1 });
                    for (int j = 1; j < choiceParts.Count; j++) ProcessComponent(choiceParts[j], overview, round, depth + 2);
                }
                return;
            }

            var linkedMatch = Regex.Match(cleanComponent, @"^b?ph\.l(.*)", RegexOptions.IgnoreCase);
            if (linkedMatch.Success)
            {
                if (!overview.BlockTypes.Contains("Linked Phase")) overview.BlockTypes.Add("Linked Phase");
                overview.Details.Add(new ModDetail { Title = "Sequence Link", Value = "Execute sequentially", Round = round, Depth = depth });
                var phases = SplitByStringDelimiter(linkedMatch.Groups[1].Value, "@1");
                foreach (var phase in phases) ProcessComponent(phase, overview, round, depth + 1);
                return;
            }

            string exactType = IdentifyType(cleanComponent);
            if (!overview.BlockTypes.Contains(exactType)) overview.BlockTypes.Add(exactType);

            if (exactType.Contains("Pool") || exactType.Contains("Fight") || exactType.Contains("Spawn Injection") || exactType.Contains("Party"))
            {
                string payload = StripCommandPrefixes(cleanComponent);
                payload = Unwrap(payload); // Ensure the list itself wasn't wrapped, permitting internal `+` splitting

                List<string> subEntities = SplitByActionDelimiter(payload, '+');

                foreach (var sub in subEntities)
                {
                    if (string.IsNullOrWhiteSpace(sub)) continue;

                    string cleanSub = RemoveTrailingMetadata(sub);
                    string entityName = ExtractEntityName(cleanSub);
                    if (entityName.Equals("ignore me", StringComparison.OrdinalIgnoreCase)) continue;

                    overview.Details.Add(new ModDetail
                    {
                        Title = exactType.Replace("Pool", "Entity"),
                        Value = SanitizeText(entityName, 60),
                        Round = round,
                        Depth = depth
                    });
                }
            }
            else
            {
                string cleanSub = RemoveTrailingMetadata(cleanComponent);
                overview.Details.Add(new ModDetail
                {
                    Title = exactType,
                    Value = SanitizeText(ExtractEntityName(cleanSub), 60),
                    Round = round,
                    Depth = depth
                });
            }
        }

        private static string Unwrap(string text)
        {
            text = text.Trim();
            while (text.StartsWith("(") && text.EndsWith(")"))
            {
                int depth = 0;
                bool matching = true;
                for (int i = 0; i < text.Length - 1; i++) // Check depth ignoring the very last ')'
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;

                    if (depth == 0) // We hit matching baseline prematurely. Don't remove wrapper.
                    {
                        matching = false;
                        break;
                    }
                }
                if (matching && depth == 1) text = text.Substring(1, text.Length - 2).Trim();
                else break;
            }
            return text;
        }

        private static string ExtractRoundPrefix(ref string text)
        {
            var match = Regex.Match(text, @"^(\d+)\.");
            if (match.Success)
            {
                text = text.Substring(match.Length).Trim();
                return match.Groups[1].Value;
            }
            return null;
        }

        private static List<string> SplitByStringDelimiter(string input, string delimiter)
        {
            List<string> result = new List<string>();
            int depth = 0;
            int startIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(') depth++;
                else if (input[i] == ')') { if (depth > 0) depth--; }
                else if (depth == 0 && i <= input.Length - delimiter.Length && input.Substring(i, delimiter.Length) == delimiter)
                {
                    result.Add(input.Substring(startIndex, i - startIndex));
                    i += delimiter.Length - 1;
                    startIndex = i + 1;
                }
            }

            if (startIndex < input.Length) result.Add(input.Substring(startIndex));
            return result;
        }

        private static string IdentifyType(string componentText)
        {
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])itempool\.", RegexOptions.IgnoreCase)) return "Item Pool";
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])heropool\.", RegexOptions.IgnoreCase)) return "Hero Pool";
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])(?:e?\d+(?:-\d+)?\.)?monsterpool\.", RegexOptions.IgnoreCase)) return "Monster Pool";

            if (Regex.IsMatch(componentText, @"(?:^|[&+(])(?:ch\.om)?(?:e?\d+(?:-\d+)?\.)?fight\.", RegexOptions.IgnoreCase)) return "Forced Fight";
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])(?:ch\.om)?(?:e?\d+(?:-\d+)?\.)?add\.", RegexOptions.IgnoreCase)) return "Spawn Injection";

            if (Regex.IsMatch(componentText, @"(?:^|[&+(])party\.", RegexOptions.IgnoreCase)) return "Starting Party Config";
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])diff\.", RegexOptions.IgnoreCase)) return "Difficulty Config";
            if (Regex.IsMatch(componentText, @"(?:^|[&+(])zone\.", RegexOptions.IgnoreCase)) return "Zone Config";

            // (Assuming generic phase logic fallback or PhaseDatabase matches)
            var phaseMatch = Regex.Match(componentText, @"(?:^|[&+(])(?:ch\.om)?(?:e?\d+(?:-\d+)?\.)?(ph\.[a-zA-Z0-9!]|phi\.|phmp\.)", RegexOptions.IgnoreCase);
            if (phaseMatch.Success) return "Phase Event";

            var chMatch = Regex.Match(componentText, @"(?:^|[&+(])(?:ch\.om)?(?:e?\d+(?:-\d+)?\.)?(ch\.[a-z])", RegexOptions.IgnoreCase);
            if (chMatch.Success) return "Reward Option";

            return $"Mod: {SanitizeText(ExtractEntityName(componentText), 25)}";
        }

        private static string StripCommandPrefixes(string text)
        {
            return Regex.Replace(text, @"^(?:ch\.om)?(?:e?\d+(?:-\d+)?\.)?(?:itempool|heropool|monsterpool|fight|add|party)\.", "", RegexOptions.IgnoreCase);
        }

        private static string ExtractEntityName(string data)
        {
            string explicitName = ExtractTagValue(data, "n");
            if (!string.IsNullOrEmpty(explicitName)) return explicitName;

            var repMatch = Regex.Match(data, @"(?:replica\.)?([A-Za-z0-9_]+)");
            if (repMatch.Success && !repMatch.Groups[1].Value.Equals("self", StringComparison.OrdinalIgnoreCase))
                return repMatch.Groups[1].Value;

            if (data.Contains("rmon.") || data.Contains("rditem.") || data.Contains("rdhero.")) return "Procedural Entity";

            return data;
        }

        private static string ExtractTagValue(string text, string tag)
        {
            var matches = Regex.Matches(text, $@"\.{tag}\.((?:\[.*?\]|[a-zA-Z0-9 _\-!?^/]+)+)");
            if (matches.Count == 0) return null;

            int minDepth = int.MaxValue;
            string bestMatch = null;

            foreach (Match match in matches)
            {
                int depth = 0;
                for (int i = 0; i < match.Index; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;
                }

                // If multiple nested tags, we want the most exposed tier 0 item to overwrite inner tiers
                if (depth <= minDepth)
                {
                    minDepth = depth;
                    bestMatch = match.Groups[1].Value.Trim();
                }
            }

            return bestMatch;
        }

        private static string RemoveTrailingMetadata(string text)
        {
            // Now replaces specific keys/payloads with an empty string rather than `.doc.*$` to the end of line
            string pattern = @"\.(mn|doc|modtier|bal|speech|img)\.(?:\[.*?\]|[a-zA-Z0-9 _\-!?^/]+)+";
            return Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
        }

        private static string SanitizeText(string text, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string clean = Regex.Replace(text, @"\[[A-Za-z0-9%/+=]{12,}\]", "[Sprite]");
            clean = Regex.Replace(clean, @"\[/?(sin|wiggle|pink|purple|red|orange|yellow|green|blue|lime|white|grey|ultragrey|cu|nh|com|b|i|dot|nbp|p|eye of horus|pips|hp-plus|fullHeart|bas15|dee12|dee27|bas103)[a-zA-Z0-9 \-]*\]", " ");
            clean = clean.Replace("[comma]", ",").Replace("[n]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();

            if (clean.Length > maxLength) clean = clean.Substring(0, maxLength - 3) + "...";
            return clean;
        }

        private static List<string> SplitByActionDelimiter(string input)
        {
            List<string> result = new List<string>();
            int depth = 0;
            int startIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '(') depth++;
                else if (c == ')') { if (depth > 0) depth--; }
                else if ((c == '&' || c == ';') && depth == 0)
                {
                    result.Add(input.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            if (startIndex < input.Length) result.Add(input.Substring(startIndex));
            return result;
        }

        private static List<string> SplitByActionDelimiter(string input, char delimiter)
        {
            List<string> result = new List<string>();
            int depth = 0;
            int startIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '(') depth++;
                else if (c == ')') { if (depth > 0) depth--; }
                else if (c == delimiter && depth == 0)
                {
                    result.Add(input.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            if (startIndex < input.Length) result.Add(input.Substring(startIndex));
            return result;
        }
    }
}