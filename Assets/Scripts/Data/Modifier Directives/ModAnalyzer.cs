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
        public string SearchSnippet { get; set; }

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

            // 40 characters is plenty to guarantee a unique match.
            string stripped = Regex.Replace(blockText, @"\s+", "");
            overview.SearchSnippet = stripped.Substring(0, Math.Min(40, stripped.Length));

            // 1. Unwrap enclosing parens first to allow top-level delimiter splits
            blockText = Unwrap(blockText);

            string customName = ExtractTagValue(blockText, "mn") ?? ExtractTagValue(blockText, "n");
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
                var firstDetail = overview.Details.FirstOrDefault();
                if (firstDetail != null)
                {
                    string prefix = !string.IsNullOrEmpty(firstDetail.Round) ? firstDetail.Round + ". " : "";
                    overview.BlockName = prefix + firstDetail.Value;
                }
                else
                {
                    overview.BlockName = "Config Block";
                }
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

            // Use the comprehensive FloorSelector instead of simple regex extraction
            string remainder = ParserHelpers.StripFloorSelector(cleanComponent, out FloorSelector fs);
            string round = inheritedRound;
            if (fs != null)
            {
                cleanComponent = remainder.Trim();
                round = FormatFloorSelector(fs) ?? inheritedRound;
            }

            // Re-unwrap post round-strip in case of wrapped phases e.g., 0.(ph.s...)
            cleanComponent = Unwrap(cleanComponent);

            if (string.IsNullOrEmpty(cleanComponent)) return;

            // =========================================================
            // INTEGRATION: Attempt Object-Oriented Phase Parsing First
            // =========================================================
            if (Regex.IsMatch(cleanComponent, @"^(?:b?ph\.|phi\.|phmp\.)|^\!", RegexOptions.IgnoreCase))
            {
                string pStr = cleanComponent;
                if (pStr.StartsWith("bph.", StringComparison.OrdinalIgnoreCase)) pStr = pStr.Substring(1);

                try
                {
                    Phase phase = Phase.Parse(pStr, isNested: false) ?? Phase.Parse(pStr, isNested: true);
                    if (phase != null)
                    {
                        ProcessParsedPhase(phase, overview, round, depth);
                        return; // Fully handled by domain classes
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ModAnalyzer] Phase parsing failed for component: '{pStr}'. Error: {ex.Message}");
                    throw new FormatException($"Phase parsing failed for component: '{pStr}'", ex);
                }
            }

            // INTEGRATION: Attempt Choosable Tags
            var chMatch = Regex.Match(cleanComponent, @"^ch\.([a-z])(.*)", RegexOptions.IgnoreCase);
            if (chMatch.Success)
            {
                if (!overview.BlockTypes.Contains("Reward Option")) overview.BlockTypes.Add("Reward Option");
                string tagPayload = chMatch.Groups[1].Value + chMatch.Groups[2].Value;
                try
                {
                    RewardTag tag = RewardTag.Parse(tagPayload);
                    overview.Details.Add(new ModDetail
                    {
                        Title = "Direct Reward (Choosable)",
                        Value = DescribeRewardTag(tag),
                        Round = round,
                        Depth = depth
                    });
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ModAnalyzer] Choosable Tag parsing failed for payload: '{tagPayload}'. Error: {ex.Message}");
                    throw new FormatException($"Choosable Tag parsing failed for payload: '{tagPayload}'", ex);
                }
            }

            // =========================================================
            // FALLBACK: Existing Regex Parsing (Guarantees no breakages)
            // =========================================================

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
                overview.Details.Add(new ModDetail { Title = "Condition Check (Alt)", Value = $"If {bool2Match.Groups[1].Value} >= {bool2Match.Groups[2].Value}", Round = round, Depth = depth });
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

        // =====================================================================
        // NEW DELEGATION LOGIC (Ties into the Phase Architecture)
        // =====================================================================
        private static void ProcessParsedPhase(Phase phase, ModBlockOverview overview, string round, int depth)
        {
            if (!overview.BlockTypes.Contains("Phase Event")) overview.BlockTypes.Add("Phase Event");

            string key = "";
            if (phase is PhaseIndexedPhase) key = "phi.";
            else if (phase is PhaseModPickPhase) key = "phmp.";
            else key = "ph." + phase.PhaseCode;

            string phaseName = PhaseDatabase.Phases.ContainsKey(key)
                ? PhaseDatabase.Phases[key]
                : phase.GetType().Name.Replace("Phase", " Phase").Trim();

            if (phase is SimpleChoicePhase scp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = string.IsNullOrEmpty(scp.Title) ? "Reward Screen" : SanitizeText(scp.Title), Round = round, Depth = depth });
                foreach (var opt in scp.Options)
                    overview.Details.Add(new ModDetail { Title = "Option", Value = DescribeRewardTag(opt), Round = round, Depth = depth + 1 });
            }
            else if (phase is SeqPhase sqp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = SanitizeText(sqp.Message), Round = round, Depth = depth });
                foreach (var opt in sqp.Options)
                {
                    overview.Details.Add(new ModDetail { Title = "Button Option", Value = SanitizeText(opt.ButtonText), Round = round, Depth = depth + 1 });
                    foreach (var inner in opt.PhaseSequence) ProcessParsedPhase(inner, overview, round, depth + 2);
                }
            }
            else if (phase is LinkedPhase lp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = "Sequential Steps", Round = round, Depth = depth });
                foreach (var inner in lp.LinkedPhases) ProcessParsedPhase(inner, overview, round, depth + 1);
            }
            else if (phase is BooleanPhase bp)
            {
                overview.Details.Add(new ModDetail { Title = "Condition Check", Value = $"If {bp.VariableName} >= {bp.Threshold}", Round = round, Depth = depth });
                if (bp.PhaseIfTrue != null) ProcessParsedPhase(bp.PhaseIfTrue, overview, round, depth + 1);
                if (bp.PhaseIfFalse != null) ProcessParsedPhase(bp.PhaseIfFalse, overview, round, depth + 1);
            }
            else if (phase is BooleanPhase2 bp2)
            {
                overview.Details.Add(new ModDetail { Title = "Condition Check (Alt)", Value = $"If {bp2.VariableName} >= {bp2.Threshold}", Round = round, Depth = depth });
                if (bp2.PhaseIfTrue != null) ProcessParsedPhase(bp2.PhaseIfTrue, overview, round, depth + 1);
                if (bp2.PhaseIfFalse != null) ProcessParsedPhase(bp2.PhaseIfFalse, overview, round, depth + 1);
            }
            else if (phase is MessagePhase mp)
            {
                string val = SanitizeText(mp.Message);
                if (!string.IsNullOrEmpty(mp.ButtonText)) val += $" [Btn: {SanitizeText(mp.ButtonText)}]";
                overview.Details.Add(new ModDetail { Title = phaseName, Value = val, Round = round, Depth = depth });
            }
            else if (phase is ChoicePhase cp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"{cp.SelectionType} (Qty: {cp.SelectionNumber})", Round = round, Depth = depth });
                foreach (var opt in cp.Options)
                    overview.Details.Add(new ModDetail { Title = "Option", Value = DescribeRewardTag(opt), Round = round, Depth = depth + 1 });
            }
            else if (phase is ChallengePhase chp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Extra Enemies: {string.Join(", ", chp.ExtraMonsters)}", Round = round, Depth = depth });
                foreach (var rew in chp.Rewards)
                    overview.Details.Add(new ModDetail { Title = "Challenge Reward", Value = DescribeRewardTag(rew), Round = round, Depth = depth + 1 });
            }
            else if (phase is LevelEndPhase lep)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = "End of Level Actions", Round = round, Depth = depth });
                foreach (var inner in lep.InnerPhases) ProcessParsedPhase(inner, overview, round, depth + 1);
            }
            else if (phase is TradePhase tp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = "Cursed Chest Trade", Round = round, Depth = depth });
                foreach (var rew in tp.Rewards)
                    overview.Details.Add(new ModDetail { Title = "Trade Included", Value = DescribeRewardTag(rew), Round = round, Depth = depth + 1 });
            }
            else if (phase is RandomRevealPhase rrp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Reveal: {DescribeRewardTag(rrp.Reward)}", Round = round, Depth = depth });
            }
            else if (phase is HeroChangePhase hcp)
            {
                string action = hcp.ChangeType == 0 ? "Random Class" : "Generated Hero";
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Reroll Slot {hcp.HeroIndex} -> {action}", Round = round, Depth = depth });
            }
            else if (phase is ItemCombinePhase icp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Mode: {icp.Mode}", Round = round, Depth = depth });
            }
            else if (phase is PositionSwapPhase psp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Swap Hero Slot {psp.A} and {psp.B}", Round = round, Depth = depth });
            }
            else if (phase is PhaseGeneratorPhase pgp)
            {
                overview.Details.Add(new ModDetail { Title = phaseName, Value = $"Generate: {pgp.Mode} Screen", Round = round, Depth = depth });
            }
            else if (phase is PhaseIndexedPhase pip)
            {
                overview.Details.Add(new ModDetail { Title = "Phase Indexed", Value = $"Type Index: {pip.Index}", Round = round, Depth = depth });
            }
            else if (phase is PhaseModPickPhase pmpp)
            {
                overview.Details.Add(new ModDetail { Title = "Phase Mod Pick", Value = $"Target Total: {pmpp.TargetTotal}", Round = round, Depth = depth });
            }
            else
            {
                // Fallback for simple single-action phases (RunEndPhase, ResetPhase, etc)
                overview.Details.Add(new ModDetail { Title = phaseName, Value = "Execute Action", Round = round, Depth = depth });
            }
        }

        private static string DescribeRewardTag(RewardTag tag)
        {
            if (tag is ModifierTag mt)
            {
                string type = "Modifier";
                if (mt.IsCombatEncounter) type = "Combat Encounter";
                else if (mt.IsZoneChange) type = "Zone Change";
                else if (mt.IsDifficultyChange) type = "Difficulty Change";
                else if (mt.IsPartyChange) type = "Party Change";
                return $"{type}: {SanitizeText(ExtractEntityName(mt.ModifierSpec), 40)}";
            }
            if (tag is ItemTag it) return $"Item: {SanitizeText(ExtractEntityName(it.ItemName), 40)}";
            if (tag is LevelupTag lt) return $"Levelup: {SanitizeText(ExtractEntityName(lt.LevelupName), 40)}";
            if (tag is HeroTag ht) return $"Hero Add: {SanitizeText(ExtractEntityName(ht.HeroSpec), 40)}";
            if (tag is RandomTag rt) return $"{rt.Count}x Tier {rt.Tier} Random {DescribeTagChar(rt.RewardTagChar)}";
            if (tag is RandomRangeTag rrt) return $"{rrt.Count}x Tier {rrt.TierMin}-{rrt.TierMax} Random {DescribeTagChar(rrt.RewardTagChar)}";
            if (tag is EnuTag et) return $"Enu Item: {et.Variant}";
            if (tag is ValueTag vt) return $"Add {vt.Amount} to Value '{vt.VariableName}'";
            if (tag is ReplaceTag pt) return $"Replace '{SanitizeText(ExtractEntityName(pt.ModifierToRemove), 20)}' => [{DescribeRewardTag(pt.GrantedReward)}]";
            if (tag is SkipTag) return "Skip / None";
            if (tag is OrTag ot) return $"Randomly Choose: {string.Join(" OR ", ot.Options.Select(DescribeRewardTag))}";
            if (tag is RawStringTag rst) return $"Raw Data: {SanitizeText(rst.RawData, 40)}";
            return "Unknown Reward";
        }

        private static string DescribeTagChar(char c) => c switch { 'm' => "Modifier", 'i' => "Item", 'l' => "Levelup", 'g' => "Hero", _ => "Reward" };

        private static string FormatFloorSelector(FloorSelector fs)
        {
            return fs?.ToSyntax();
        }

        /*
        private static string FormatFloorSelector(FloorSelector fs)
        {
            if (fs == null) return null;
            if (fs.IsEvery) return fs.EveryOffset.HasValue ? $"Every {fs.EveryN} Flrs (Offset {fs.EveryOffset})" : $"Every {fs.EveryN} Flrs";
            if (fs.RangeStart.HasValue && fs.RangeEnd.HasValue) return $"Flrs {fs.RangeStart}-{fs.RangeEnd}";
            if (fs.ExactFloor.HasValue) return $"Flr {fs.ExactFloor}";
            return null;
        }
        */

        // =====================================================================
        // CORE UTILITIES (Mostly untouched to preserve exact legacy functionality)
        // =====================================================================
        private static string Unwrap(string text)
        {
            text = text.Trim();
            while (text.StartsWith("(") && text.EndsWith(")"))
            {
                int depth = 0;
                bool matching = true;
                for (int i = 0; i < text.Length - 1; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;

                    if (depth == 0)
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

        private static List<string> SplitByStringDelimiter(string input, string delimiter)
        {
            List<string> result = new List<string>();
            int depth = 0, startIndex = 0;

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

            string mnName = ExtractTagValue(data, "mn");
            if (!string.IsNullOrEmpty(mnName)) return mnName;

            // Strip the functional command logic upfront before looking for a replica name to avoid accidentally ripping out floor numbers like `4` from `4.fight.`
            string stripped = StripCommandPrefixes(data);
            var repMatch = Regex.Match(stripped, @"(?:replica\.)?([A-Za-z0-9_]+)");
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

        private static List<string> SplitByActionDelimiter(string input, char delimiter = '&')
        {
            List<string> result = new List<string>();
            int depth = 0, startIndex = 0;

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