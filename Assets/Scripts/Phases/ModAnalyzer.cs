using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SliceDiceTextMod
{
    public static class ModAnalyzer
    {
        // Container for structural blocks discovered during global analysis
        private class ScannedBlock
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public string RawText { get; set; }
            public string DatabaseKey { get; set; } // References keys directly from PhaseDatabase / ChoosableDatabase
            public FloorSelector Floor { get; set; }
            public Phase ParsedPhase { get; set; }
            public RewardTag ParsedTag { get; set; }
            public List<ScannedBlock> NestedBlocks { get; set; } = new List<ScannedBlock>();

            public int SortWeight
            {
                get
                {
                    if (Floor == null) return -1;
                    if (Floor.ExactFloor.HasValue) return Floor.ExactFloor.Value;
                    if (Floor.RangeStart.HasValue) return Floor.RangeStart.Value;
                    return 0; // Fallback for 'Every X floors' (e.g., e2)
                }
            }
        }

        private class ExclusionZone
        {
            public int Start;
            public int End;
        }

        /// <summary>
        /// Scans a massive TextMod string, filters raw asset data, nested-maps elements, 
        /// and outputs a highly polished, database-driven chronological outline.
        /// </summary>
        public static string BuildHierarchy(string rawModString)
        {
            if (string.IsNullOrWhiteSpace(rawModString))
            {
                Debug.LogWarning("[ModAnalyzer] Passed empty or null string to analyzer.");
                return "<i>No data provided.</i>";
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. Map out exclusion zones (image blocks and custom asset pools)
            var exclusionZones = MapExclusionZones(rawModString);

            // 2. Scan for the positions of all phases/choosables globally
            var matches = FindBlockStarts(rawModString);
            var flatBlocks = new List<ScannedBlock>();

            foreach (Match match in matches)
            {
                // Skip if this match falls within any mapped exclusion zone
                if (exclusionZones.Any(z => match.Index >= z.Start && match.Index <= z.End))
                {
                    continue;
                }

                var block = ExtractBlock(rawModString, match.Index, match.Groups["type"].Value, match.Groups["floor"].Value);
                if (block != null)
                {
                    flatBlocks.Add(block);
                }
            }

            // 3. Map structural nesting (ranges inside other ranges)
            var sortedFlatBlocks = flatBlocks
                .OrderBy(b => b.StartIndex)
                .ThenByDescending(b => b.EndIndex - b.StartIndex)
                .ToList();

            var rootBlocks = new List<ScannedBlock>();

            foreach (var block in sortedFlatBlocks)
            {
                ScannedBlock parent = FindParentBlock(rootBlocks, block);
                if (parent != null)
                {
                    // Discard children if the parent itself is identified as a raw asset definition
                    if (!IsRawCode(parent.RawText))
                    {
                        parent.NestedBlocks.Add(block);
                    }
                }
                else
                {
                    if (!IsRawCode(block.RawText))
                    {
                        rootBlocks.Add(block);
                    }
                }
            }

            // 4. Chronologically sort root nodes
            var sortedRoots = rootBlocks
                .OrderBy(b => b.SortWeight)
                .ThenBy(b => b.StartIndex)
                .ToList();

            sw.Stop();

            // Print stats directly to Unity Console
            Debug.Log($"[ModAnalyzer] Parsing Finished in {sw.ElapsedMilliseconds}ms. " +
                      $"Found globally: {flatBlocks.Count} items | Roots: {sortedRoots.Count} | Nested: {flatBlocks.Count - sortedRoots.Count}");

            // 5. Construct high-level outline string
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=================================================================================");
            sb.AppendLine($"                    TEXTMOD CAMPAIGN HIGH-LEVEL OUTLINE");
            sb.AppendLine($"        Parsed: {flatBlocks.Count} elements | Root Events: {sortedRoots.Count} | Time: {sw.ElapsedMilliseconds}ms");
            sb.AppendLine("=================================================================================\n");

            foreach (var root in sortedRoots)
            {
                string floorStr = root.Floor != null ? $"[Floor {root.Floor.ToSyntax()}]" : "[Global/Start]";
                sb.AppendLine($"== {floorStr} ==");
                BuildOutlineString(root, 1, sb);
                sb.AppendLine();
            }

            string finalResult = sb.ToString();

            // --- USER DIRECTIVE: Pipe the result straight to Debug.Log for copy-paste inspection ---
            Debug.Log($"[ModAnalyzer Output Outline]\n{finalResult}");

            return finalResult;
        }

        // =========================================================================
        // High-Level Outline Generator (Database-First Mapping)
        // =========================================================================

        private static void BuildOutlineString(ScannedBlock block, int depth, StringBuilder sb)
        {
            if (block == null) return;

            string indent = new string(' ', depth * 4);
            string outlineSummary = "Unknown Element";
            string color = "#ffffff";

            // --- STRICT DATABASE LOOKUPS ---
            if (block.DatabaseKey == "phi.")
            {
                color = "#55ff55";
                string niceName = PhaseDatabase.Phases.ContainsKey(block.DatabaseKey) ? PhaseDatabase.Phases[block.DatabaseKey] : "Phase Indexed";
                outlineSummary = $"{niceName} (Index: {block.RawText.Replace("phi.", "").Trim()})";
            }
            else if (block.DatabaseKey == "phmp.")
            {
                color = "#55ff55";
                string niceName = PhaseDatabase.Phases.ContainsKey(block.DatabaseKey) ? PhaseDatabase.Phases[block.DatabaseKey] : "Phase Mod Pick";
                outlineSummary = $"{niceName} (Cost Target: {block.RawText.Replace("phmp.", "").Trim()})";
            }
            else if (block.DatabaseKey.StartsWith("ph."))
            {
                color = "#55ff55";
                if (block.ParsedPhase != null)
                {
                    outlineSummary = GetPhaseOutline(block.ParsedPhase);
                }
                else
                {
                    // Fallback Warning
                    string fallbackCode = block.RawText.Substring(0, Math.Min(15, block.RawText.Length));
                    Debug.LogWarning($"[ModAnalyzer] Phase parsing fell back to guesswork for raw block at index {block.StartIndex}: {block.RawText}");
                    outlineSummary = $"Unrecognized Phase Code ({fallbackCode})";
                }
            }
            else if (block.DatabaseKey.StartsWith("ch."))
            {
                color = "#55ffff";
                if (block.ParsedTag != null)
                {
                    outlineSummary = GetTagOutline(block.ParsedTag);
                }
                else
                {
                    string fallbackCode = block.RawText.Substring(0, Math.Min(15, block.RawText.Length));
                    Debug.LogWarning($"[ModAnalyzer] Choosable parsing fell back to guesswork for raw block at index {block.StartIndex}: {block.RawText}");
                    outlineSummary = $"Unrecognized Choosable Tag ({fallbackCode})";
                }
            }

            sb.AppendLine($"{indent}• <color={color}><b>{outlineSummary}</b></color>");

            // Recursively print structural nested elements found inside
            foreach (var nested in block.NestedBlocks)
            {
                BuildOutlineString(nested, depth + 1, sb);
            }
        }

        private static string GetPhaseOutline(Phase phase)
        {
            if (phase == null) return "Null Phase";

            string dbKey = $"ph.{phase.PhaseCode}";
            string niceName = PhaseDatabase.Phases.ContainsKey(dbKey) ? PhaseDatabase.Phases[dbKey] : "Unknown Phase";

            return phase switch
            {
                MessagePhase msg => $"{niceName} : \"{SanitizeText(msg.Message, 45)}\"{(string.IsNullOrEmpty(msg.ButtonText) ? "" : $" [Button: {SanitizeText(msg.ButtonText, 15)}]")}",
                HeroChangePhase hcp => $"{niceName} : Slot {hcp.HeroIndex} -> {(hcp.ChangeType == 0 ? "Random Class" : "Generated Hero")}",
                BooleanPhase bp => $"{niceName} : If [{bp.VariableName}] >= {bp.Threshold}",
                BooleanPhase2 bp2 => $"{niceName} : If [{bp2.VariableName}] >= {bp2.Threshold}",
                SeqPhase seq => $"{niceName} : Dialogue tree \"{SanitizeText(seq.Message, 40)}\" ({seq.Options.Count} Choices)",
                SimpleChoicePhase sc => $"{niceName} : \"{SanitizeText(sc.Title ?? "Select Reward", 30)}\" ({sc.Options.Count} Options)",
                ChoicePhase cp => $"{niceName} : Select {cp.SelectionNumber} ({cp.SelectionType}) of {cp.Options.Count} Options",
                ChallengePhase chal => $"{niceName} : Add {chal.ExtraMonsters.Count} Monsters ({string.Join(", ", chal.ExtraMonsters.Select(GetCleanName))})",
                TradePhase tp => $"{niceName} (Cursed Chest) : Accept Curses/Blessings ({tp.Rewards.Count} Options)",
                PositionSwapPhase psp => $"{niceName} : Swap Party Slot {psp.A} <-> Slot {psp.B}",
                ItemCombinePhase icp => $"{niceName} : Mode {icp.Mode}",
                PhaseGeneratorPhase pgp => $"{niceName} : Mode {pgp.Mode}",
                LinkedPhase lp => $"{niceName} : Sequential Action Group ({lp.LinkedPhases.Count} sub-phases)",
                LevelEndPhase lep => $"{niceName} : Screen overrides ({lep.InnerPhases.Count} sub-phases)",
                ResetPhase _ => $"{niceName} : Reset Party Levels & Clear Inventory",
                RunEndPhase _ => $"{niceName} : Game Over Screen",
                RandomRevealPhase rrp => $"{niceName}",
                PlayerRollingPhase _ => $"{niceName}",
                TargetingPhase _ => $"{niceName}",
                EnemyRollingPhase _ => $"{niceName}",
                DamagePhase _ => $"{niceName}",
                _ => $"Unsupported Phase Code '{phase.PhaseCode}'"
            };
        }

        private static string GetTagOutline(RewardTag tag)
        {
            if (tag == null) return "Null Reward";

            string tagLetter = GetTagLetter(tag);
            string dbKey = $"ch.{tagLetter}";
            string niceName = ChoosableDatabase.ByChoosable.ContainsKey(dbKey) ? ChoosableDatabase.ByChoosable[dbKey].TagType : "Unknown Reward";

            return tag switch
            {
                ModifierTag m => $"{niceName} Reward : {GetCleanName(m.ModifierSpec)}",
                ItemTag i => $"{niceName} Reward : {GetCleanName(i.ItemName)}",
                LevelupTag l => $"{niceName} Reward : Level up to {GetCleanName(l.LevelupName)}",
                HeroTag g => $"{niceName} Reward : Add Hero {GetCleanName(g.HeroSpec)}",
                ValueTag v => $"{niceName} Reward : Adjust Counter [{v.VariableName}] = {v.Amount:+#;-#;0}",
                ReplaceTag p => $"{niceName} Reward : Swap {GetCleanName(p.ModifierToRemove)} with {GetTagOutline(p.GrantedReward)}",
                OrTag o => $"{niceName} Reward : Choose 1 of {o.Options.Count} Random Choices",
                EnuTag e => $"{niceName} Reward : Grant Weapon Side ({e.Variant})",
                RandomTag r => $"{niceName} Reward : {r.Count}x Tier {r.Tier} ({r.RewardTagChar} items)",
                RandomRangeTag q => $"{niceName} Reward : {q.Count}x Tier {q.TierMin}-{q.TierMax} ({q.RewardTagChar} items)",
                SkipTag _ => "Skip Reward Option",
                RawStringTag r => $"Custom reward : {SanitizeText(r.RawData, 40)}",
                _ => "Reward Option"
            };
        }

        // =========================================================================
        // Sanitization and Text Cleaning Helpers
        // =========================================================================

        private static string SanitizeText(string text, int maxCharacters)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Strip massive base64 compressed data sheets
            string clean = Regex.Replace(text, @"\[[A-Za-z0-9%/+=]{12,}\]", "[Sprite Asset]");

            // Strip textmod markup tags
            clean = Regex.Replace(clean, @"\[/?(sin|wiggle|pink|purple|red|orange|yellow|green|blue|lime|white|grey|ultragrey|cu|nh|com|b|i)[a-zA-Z0-9]*\]", "");

            clean = clean.Replace("[comma]", ",").Replace("[dot]", ".").Replace("[pips]", "pips");
            clean = clean.Replace("\n", " ").Replace("\r", " ").Replace("[nh]", " ").Trim();

            // Escape all brackets to guarantee console color tags never bleed
            clean = EscapeRichText(clean);

            if (clean.Length > maxCharacters)
            {
                clean = clean.Substring(0, maxCharacters - 3) + "...";
            }

            return clean;
        }

        private static string GetCleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Empty";
            if (IsRawCode(raw)) return "[Custom Definition Asset]";

            var nameMatch = Regex.Match(raw, @"\.mn\.(?<name>[^.&)]+)");
            if (nameMatch.Success)
            {
                return nameMatch.Groups["name"].Value.Trim();
            }

            string sanitized = SanitizeText(raw, 50);
            int hashIdx = sanitized.IndexOf('#');
            if (hashIdx > 0) sanitized = sanitized.Substring(0, hashIdx);

            int dotIdx = sanitized.IndexOf('.');
            if (dotIdx > 0 && dotIdx < 15) sanitized = sanitized.Substring(0, dotIdx);

            return sanitized.Trim();
        }

        private static bool IsRawCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Contains("replica.") || text.Contains("heropool.") || text.Contains("itempool.") || text.Contains(".img.") || text.Contains(".sd."))
            {
                return true;
            }
            return false;
        }

        private static string EscapeRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // =========================================================================
        // Exclusion Zone Mapper (Prevents parsing false positives inside assets)
        // =========================================================================

        private static List<ExclusionZone> MapExclusionZones(string src)
        {
            var zones = new List<ExclusionZone>();

            // 1. Map all square bracket [...] zones
            int bracketStart = -1;
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] == '[')
                {
                    bracketStart = i;
                }
                else if (src[i] == ']' && bracketStart != -1)
                {
                    zones.Add(new ExclusionZone { Start = bracketStart, End = i });
                    bracketStart = -1;
                }
            }

            // 2. Map all custom asset pools
            var regex = new Regex(@"(heropool\.|itempool\.|replica\.)", RegexOptions.Compiled);
            foreach (Match m in regex.Matches(src))
            {
                int startIdx = m.Index;
                int depth = 0;
                int endIdx = -1;

                for (int i = startIdx; i < src.Length; i++)
                {
                    if (src[i] == '(') depth++;
                    else if (src[i] == ')')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endIdx = i;
                            break;
                        }
                    }
                }

                if (endIdx != -1)
                {
                    zones.Add(new ExclusionZone { Start = startIdx, End = endIdx });
                }
            }

            return zones;
        }

        // =========================================================================
        // Helper Scanner Methods
        // =========================================================================

        private static MatchCollection FindBlockStarts(string src)
        {
            string pattern = @"(?:(?<floor>e\d+(?:\.\d+)?|\d+(?:-\d+)?)\.)?(?<type>ph\.[a-zA-Z0-9!]|phi\.|phmp\.|ch\.[a-z])";
            return Regex.Matches(src, pattern);
        }

        private static ScannedBlock ExtractBlock(string src, int matchIndex, string typeToken, string floorToken)
        {
            int start = matchIndex;

            if (!string.IsNullOrEmpty(floorToken))
            {
                start = src.LastIndexOf(floorToken + ".", matchIndex);
                if (start == -1) start = matchIndex;
            }

            int depth = 0;
            int end = start;

            for (int i = matchIndex; i < src.Length; i++)
            {
                char c = src[i];

                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']')
                {
                    depth--;
                    if (depth < 0)
                    {
                        end = i;
                        break;
                    }
                }
                // --- BLOCK TERMINATION VIA STRONGLY TYPED DELIMITERS ---
                else if (depth == 0 && (c == char.Parse(Delimiters.TopLevel) || c == ',' ||
                        (i <= src.Length - 2 && (src.Substring(i, 2) == Delimiters.AtThree ||
                                                 src.Substring(i, 2) == Delimiters.AtOne ||
                                                 src.Substring(i, 2) == Delimiters.AtTwo ||
                                                 src.Substring(i, 2) == Delimiters.AtSix ||
                                                 src.Substring(i, 2) == Delimiters.AtSeven))))
                {
                    end = i;
                    break;
                }

                end = i + 1;
            }

            if (start < 0 || end <= start || end > src.Length)
            {
                return null;
            }

            string rawBlockText = src.Substring(start, end - start).Trim();
            if (string.IsNullOrEmpty(rawBlockText)) return null;

            string remainder = ParserHelpers.StripFloorSelector(rawBlockText, out FloorSelector fs);

            var block = new ScannedBlock
            {
                StartIndex = start,
                EndIndex = end,
                RawText = rawBlockText,
                Floor = fs
            };

            try
            {
                if (typeToken.StartsWith("phi."))
                {
                    block.DatabaseKey = "phi.";
                }
                else if (typeToken.StartsWith("phmp."))
                {
                    block.DatabaseKey = "phmp.";
                }
                else if (typeToken.StartsWith("ph."))
                {
                    block.DatabaseKey = typeToken;
                    block.ParsedPhase = Phase.Parse(rawBlockText);
                }
                else if (typeToken.StartsWith("ch."))
                {
                    block.DatabaseKey = typeToken;
                    string payload = remainder.Substring(3);
                    block.ParsedTag = RewardTag.Parse(payload);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModAnalyzer] Parsing guesswork skipped exception: {ex.Message} on string '{rawBlockText}'");
                return null;
            }

            return block;
        }

        private static ScannedBlock FindParentBlock(List<ScannedBlock> activeRoots, ScannedBlock target)
        {
            for (int i = activeRoots.Count - 1; i >= 0; i--)
            {
                var root = activeRoots[i];
                if (root.StartIndex <= target.StartIndex && root.EndIndex >= target.EndIndex)
                {
                    var nestedParent = FindParentBlock(root.NestedBlocks, target);
                    return nestedParent ?? root;
                }
            }
            return null;
        }

        private static string GetTagLetter(RewardTag tag)
        {
            return tag switch
            {
                ModifierTag _ => "m",
                ItemTag _ => "i",
                LevelupTag _ => "l",
                HeroTag _ => "g",
                RandomTag _ => "r",
                RandomRangeTag _ => "q",
                OrTag _ => "o",
                EnuTag _ => "e",
                ValueTag _ => "v",
                ReplaceTag _ => "p",
                SkipTag _ => "s",
                _ => "?"
            };
        }
    }
}