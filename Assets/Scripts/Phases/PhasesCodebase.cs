using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    // =========================================================================
    // 1. Core Utilities & Delimiters
    // =========================================================================
    public static class Delimiters
    {
        public const string TopLevel = "&";
        public const string AtOne = "@1";
        public const string AtTwo = "@2";
        public const string AtThree = "@3";
        public const string AtFour = "@4";
        public const string AtSix = "@6";
        public const string AtSeven = "@7";
        public const string Semicolon = ";";
    }

    public static class ParserHelpers
    {
        // Splits a string while ignoring delimiters trapped inside parentheses
        public static List<string> SplitRespectingParens(string input, string delimiter)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(input)) return result;

            int depth = 0, start = 0;

            for (int i = 0; i <= input.Length - delimiter.Length; i++)
            {
                char c = input[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && input.Substring(i, delimiter.Length) == delimiter)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + delimiter.Length;
                    i += delimiter.Length - 1; // Skip the rest of the delimiter
                }
            }
            result.Add(input.Substring(start));
            return result;
        }

        // Strips "e2.1.", "4.", "1-5." and returns the remainder
        public static string StripFloorSelector(string raw, out FloorSelector selector)
        {
            selector = null;
            if (string.IsNullOrEmpty(raw)) return raw;

            var match = Regex.Match(raw, @"^(e\d+\.\d+|e\d+|\-?\d+\-\-?\d+|\-?\d+)\.");
            if (!match.Success) return raw;

            selector = FloorSelector.TryParse(match.Groups[1].Value);
            return raw.Substring(match.Length); // Return the remainder
        }
    }

    // =========================================================================
    // 2. Floor Selector
    // =========================================================================
    public class FloorSelector
    {
        public int? ExactFloor { get; set; }
        public int? RangeStart { get; set; }
        public int? RangeEnd { get; set; }
        public bool IsEvery { get; set; }
        public int? EveryN { get; set; }
        public int? EveryOffset { get; set; }

        public static FloorSelector TryParse(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var match = Regex.Match(token, @"^e(\d+)\.(\d+)$");
            if (match.Success) return new FloorSelector { IsEvery = true, EveryN = int.Parse(match.Groups[1].Value), EveryOffset = int.Parse(match.Groups[2].Value) };

            match = Regex.Match(token, @"^e(\d+)$");
            if (match.Success) return new FloorSelector { IsEvery = true, EveryN = int.Parse(match.Groups[1].Value) };

            match = Regex.Match(token, @"^(-?\d+)-(-?\d+)$");
            if (match.Success) return new FloorSelector { RangeStart = int.Parse(match.Groups[1].Value), RangeEnd = int.Parse(match.Groups[2].Value) };

            match = Regex.Match(token, @"^(-?\d+)$");
            if (match.Success) return new FloorSelector { ExactFloor = int.Parse(match.Groups[1].Value) };

            return null;
        }

        public string ToSyntax()
        {
            if (IsEvery) return EveryOffset.HasValue ? $"e{EveryN}.{EveryOffset}" : $"e{EveryN}";
            if (RangeStart.HasValue && RangeEnd.HasValue) return $"{RangeStart}-{RangeEnd}";
            if (ExactFloor.HasValue) return ExactFloor.Value.ToString();
            return string.Empty;
        }
    }

    // =========================================================================
    // 3. Reward Tags (Used by Choosables & SimpleChoicePhase)
    // =========================================================================
    public abstract class RewardTag
    {
        public bool IsWrappedInParens { get; set; } = false;
        public abstract string ToSyntax();
        protected string WrapIfNeeded(string inner) => IsWrappedInParens ? $"({inner})" : inner;

        public static RewardTag Parse(string fullPayload)
        {
            if (string.IsNullOrEmpty(fullPayload)) return new SkipTag();

            bool hasParens = fullPayload.StartsWith("(") && fullPayload.EndsWith(")");
            string innerPayload = hasParens ? fullPayload.Substring(1, fullPayload.Length - 2) : fullPayload;

            if (string.IsNullOrEmpty(innerPayload)) return new SkipTag();

            char tagChar = innerPayload[0];
            string payload = innerPayload.Substring(1);

            RewardTag tag = tagChar switch
            {
                'm' => new ModifierTag { ModifierSpec = payload },
                'i' => new ItemTag { ItemName = payload },
                'l' => new LevelupTag { LevelupName = payload },
                'g' => new HeroTag { HeroSpec = payload },
                'r' => RandomTag.Parse(payload),
                'q' => RandomRangeTag.Parse(payload),
                'o' => OrTag.Parse(payload),
                'e' => Enum.TryParse<EnuVariant>(payload, true, out var enu) ? new EnuTag { Variant = enu } : new RawStringTag { RawData = fullPayload },
                'v' => ValueTag.Parse(payload),
                'p' => ReplaceTag.Parse(payload),
                's' => new SkipTag(),
                _ => new RawStringTag { RawData = fullPayload }
            };

            tag.IsWrappedInParens = hasParens;
            return tag;
        }
    }

    public enum EnuVariant { RandoKeywordT1Item, RandoKeywordT5Item, RandoKeywordT7Item }

    public class ModifierTag : RewardTag { public string ModifierSpec; public override string ToSyntax() => WrapIfNeeded($"m{ModifierSpec}"); }
    public class ItemTag : RewardTag { public string ItemName; public override string ToSyntax() => WrapIfNeeded($"i{ItemName}"); }
    public class LevelupTag : RewardTag { public string LevelupName; public override string ToSyntax() => WrapIfNeeded($"l{LevelupName}"); }
    public class HeroTag : RewardTag { public string HeroSpec; public override string ToSyntax() => WrapIfNeeded($"g{HeroSpec}"); }
    public class SkipTag : RewardTag { public override string ToSyntax() => "s"; }
    public class RawStringTag : RewardTag { public string RawData; public override string ToSyntax() => RawData; }
    public class EnuTag : RewardTag { public EnuVariant Variant { get; set; } public override string ToSyntax() => WrapIfNeeded($"e{Variant}"); }

    public class RandomTag : RewardTag
    {
        public int Tier, Count; public char RewardTagChar;
        public static new RandomTag Parse(string p)
        {
            var sp = p.Split('~');
            return sp.Length >= 3 ? new RandomTag { Tier = int.Parse(sp[0]), Count = int.Parse(sp[1]), RewardTagChar = sp[2][0] } : new RandomTag();
        }
        public override string ToSyntax() => WrapIfNeeded($"r{Tier}~{Count}~{RewardTagChar}");
    }

    public class RandomRangeTag : RewardTag
    {
        public int TierMin, TierMax, Count; public char RewardTagChar;
        public static new RandomRangeTag Parse(string p)
        {
            var sp = p.Split('~');
            return sp.Length >= 4 ? new RandomRangeTag { TierMin = int.Parse(sp[0]), TierMax = int.Parse(sp[1]), Count = int.Parse(sp[2]), RewardTagChar = sp[3][0] } : new RandomRangeTag();
        }
        public override string ToSyntax() => WrapIfNeeded($"q{TierMin}~{TierMax}~{Count}~{RewardTagChar}");
    }

    public class ValueTag : RewardTag
    {
        public string VariableName; public int Amount;
        public static new ValueTag Parse(string p)
        {
            int i = p.LastIndexOf('V');
            if (i == -1) return new ValueTag { VariableName = p };
            return new ValueTag { VariableName = p.Substring(0, i), Amount = int.TryParse(p.Substring(i + 1), out int amt) ? amt : 0 };
        }
        public override string ToSyntax() => WrapIfNeeded($"v{VariableName}V{Amount}");
    }

    public class ReplaceTag : RewardTag
    {
        public string ModifierToRemove; public RewardTag GrantedReward;
        public static new ReplaceTag Parse(string p)
        {
            int i = p.IndexOf('~');
            if (i == -1) return new ReplaceTag { ModifierToRemove = p };
            return new ReplaceTag { ModifierToRemove = p.Substring(0, i), GrantedReward = RewardTag.Parse(p.Substring(i + 1)) };
        }
        public override string ToSyntax() => WrapIfNeeded($"pm{ModifierToRemove}~{GrantedReward?.ToSyntax()}");
    }

    public class OrTag : RewardTag
    {
        public List<RewardTag> Options = new List<RewardTag>();
        public static new OrTag Parse(string p) => new OrTag { Options = ParserHelpers.SplitRespectingParens(p, Delimiters.AtFour).Select(RewardTag.Parse).ToList() };
        public override string ToSyntax() => WrapIfNeeded($"o{string.Join(Delimiters.AtFour, Options.Select(o => o.ToSyntax()))}");
    }

    // =========================================================================
    // 4. Core Phase Architecture
    // =========================================================================
    public abstract class Phase
    {
        public FloorSelector FloorSelector { get; set; }
        public abstract string PhaseCode { get; }
        public abstract string ToSyntax(bool omitPrefix = false);

        public static Phase Parse(string raw, bool isNested = false)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string remainder = ParserHelpers.StripFloorSelector(raw, out FloorSelector fs);

            if (remainder.StartsWith("phi.", StringComparison.Ordinal))
                return new PhaseIndexedPhase { FloorSelector = fs, Index = int.Parse(remainder.Substring(4)) };

            if (remainder.StartsWith("phmp.", StringComparison.Ordinal))
                return new PhaseModPickPhase { FloorSelector = fs, TargetTotal = int.Parse(remainder.Substring(5)) };

            string code, payload;

            if (remainder.StartsWith("ph.", StringComparison.Ordinal))
            {
                remainder = remainder.Substring(3);
                code = remainder.Length > 0 && remainder[0] == '!' ? "!" : remainder[0].ToString();
                payload = remainder.Substring(code.Length);
            }
            else if (isNested)
            {
                // Nested phases sometimes drop the 'ph.'
                code = remainder.Length > 0 && remainder[0] == '!' ? "!" : remainder[0].ToString();
                payload = remainder.Substring(code.Length);
            }
            else
            {
                return null; // Invalid Phase
            }

            Phase phase = code switch
            {
                "!" => SimpleChoicePhase.Parse(payload),
                "0" => new PlayerRollingPhase { RawArgs = payload },
                "1" => new TargetingPhase { RawArgs = payload },
                "2" => LevelEndPhase.Parse(payload),
                "3" => new EnemyRollingPhase { RawArgs = payload },
                "4" => MessagePhase.Parse(payload),
                "5" => HeroChangePhase.Parse(payload),
                "6" => new ResetPhase(),
                "7" => ItemCombinePhase.Parse(payload),
                "8" => PositionSwapPhase.Parse(payload),
                "9" => ChallengePhase.Parse(payload),
                "b" => BooleanPhase.Parse(payload),
                "c" => ChoicePhase.Parse(payload),
                "d" => new DamagePhase { RawArgs = payload },
                "e" => new RunEndPhase(),
                "g" => PhaseGeneratorPhase.Parse(payload),
                "l" => LinkedPhase.Parse(payload),
                "r" => RandomRevealPhase.Parse(payload),
                "s" => SeqPhase.Parse(payload),
                "t" => TradePhase.Parse(payload),
                "z" => BooleanPhase2.Parse(payload),
                _ => throw new ArgumentException($"Unknown phase code '{code}'")
            };

            if (phase != null) phase.FloorSelector = fs;
            return phase;
        }

        protected string GetPrefix(bool omitPrefix)
        {
            string fs = FloorSelector != null ? FloorSelector.ToSyntax() + "." : "";
            return omitPrefix ? $"{fs}{PhaseCode}" : $"{fs}ph.{PhaseCode}";
        }
    }

    // =========================================================================
    // 5. Complex Phases
    // =========================================================================
    public class SimpleChoicePhase : Phase
    {
        public override string PhaseCode => "!";
        public string Title { get; set; }
        public List<RewardTag> Options { get; set; } = new List<RewardTag>();

        public static SimpleChoicePhase Parse(string payload)
        {
            var phase = new SimpleChoicePhase();
            int semiIdx = payload.IndexOf(';');
            int at3Idx = payload.IndexOf(Delimiters.AtThree);

            // If there's a semicolon and it's before the first @3 (or there is no @3), it's a title
            if (semiIdx >= 0 && (at3Idx < 0 || semiIdx < at3Idx))
            {
                phase.Title = payload.Substring(0, semiIdx);
                payload = payload.Substring(semiIdx + 1);
            }
            phase.Options = ParserHelpers.SplitRespectingParens(payload, Delimiters.AtThree).Select(RewardTag.Parse).ToList();
            return phase;
        }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{(Title != null ? Title + ";" : "")}{string.Join(Delimiters.AtThree, Options.Select(o => o.ToSyntax()))}";
    }

    public class LinkedPhase : Phase
    {
        public override string PhaseCode => "l";
        public List<Phase> LinkedPhases { get; set; } = new List<Phase>();

        public static LinkedPhase Parse(string payload)
        {
            var parts = ParserHelpers.SplitRespectingParens(payload, Delimiters.AtOne);
            var phase = new LinkedPhase();
            foreach (var part in parts) phase.LinkedPhases.Add(Phase.Parse(part, isNested: true));
            return phase;
        }

        public override string ToSyntax(bool omitPrefix = false) => GetPrefix(omitPrefix) + string.Join(Delimiters.AtOne, LinkedPhases.Select(p => p.ToSyntax(omitPrefix: true)));
    }

    public class BooleanPhase : Phase
    {
        public override string PhaseCode => "b";
        public string VariableName { get; set; }
        public int Threshold { get; set; }
        public Phase PhaseIfTrue { get; set; }
        public Phase PhaseIfFalse { get; set; }

        public static BooleanPhase Parse(string payload)
        {
            var parts = payload.Split(new[] { Delimiters.Semicolon }, 3, StringSplitOptions.None);
            if (parts.Length < 3) return new BooleanPhase();

            var branches = parts[2].Split(new[] { Delimiters.AtTwo }, 2, StringSplitOptions.None);
            return new BooleanPhase
            {
                VariableName = parts[0],
                Threshold = int.TryParse(parts[1], out int t) ? t : 0,
                PhaseIfTrue = Phase.Parse(branches[0], isNested: true),
                PhaseIfFalse = branches.Length > 1 ? Phase.Parse(branches[1], isNested: true) : null
            };
        }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{VariableName};{Threshold};{PhaseIfTrue?.ToSyntax(true)}{(PhaseIfFalse != null ? Delimiters.AtTwo + PhaseIfFalse.ToSyntax(true) : "")}";
    }

    public class BooleanPhase2 : Phase
    {
        public override string PhaseCode => "z";
        public string VariableName { get; set; }
        public int Threshold { get; set; }
        public Phase PhaseIfTrue { get; set; }
        public Phase PhaseIfFalse { get; set; }

        public static BooleanPhase2 Parse(string payload)
        {
            var fields = payload.Split(new[] { Delimiters.AtSix }, 3, StringSplitOptions.None);
            if (fields.Length < 3) return new BooleanPhase2();

            var branches = fields[2].Split(new[] { Delimiters.AtSeven }, 2, StringSplitOptions.None);
            return new BooleanPhase2
            {
                VariableName = fields[0],
                Threshold = int.TryParse(fields[1], out int t) ? t : 0,
                PhaseIfTrue = Phase.Parse(branches[0], isNested: true),
                PhaseIfFalse = branches.Length > 1 ? Phase.Parse(branches[1], isNested: true) : null
            };
        }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{VariableName}{Delimiters.AtSix}{Threshold}{Delimiters.AtSix}{PhaseIfTrue?.ToSyntax(true)}{(PhaseIfFalse != null ? Delimiters.AtSeven + PhaseIfFalse.ToSyntax(true) : "")}";
    }

    public class SeqPhase : Phase
    {
        public override string PhaseCode => "s";
        public string Message { get; set; }
        public List<SeqOption> Options { get; set; } = new List<SeqOption>();

        public static SeqPhase Parse(string payload)
        {
            var segments = ParserHelpers.SplitRespectingParens(payload, Delimiters.AtOne);
            var phase = new SeqPhase { Message = segments[0] };
            for (int i = 1; i < segments.Count; i++)
            {
                var parts = ParserHelpers.SplitRespectingParens(segments[i], Delimiters.AtTwo);
                var option = new SeqOption { ButtonText = parts[0] };
                for (int j = 1; j < parts.Count; j++) option.PhaseSequence.Add(Phase.Parse(parts[j], isNested: true));
                phase.Options.Add(option);
            }
            return phase;
        }

        public override string ToSyntax(bool omitPrefix = false)
        {
            var sb = new StringBuilder($"{GetPrefix(omitPrefix)}{Message}");
            foreach (var opt in Options)
            {
                sb.Append(Delimiters.AtOne).Append(opt.ButtonText);
                foreach (var p in opt.PhaseSequence) sb.Append(Delimiters.AtTwo).Append(p?.ToSyntax(true));
            }
            return sb.ToString();
        }
    }

    public class SeqOption
    {
        public string ButtonText { get; set; } = string.Empty;
        public List<Phase> PhaseSequence { get; set; } = new List<Phase>();
    }

    public class LevelEndPhase : Phase
    {
        public override string PhaseCode => "2";
        public List<Phase> InnerPhases { get; set; } = new List<Phase>();

        public static LevelEndPhase Parse(string payload)
        {
            var phase = new LevelEndPhase();
            var match = Regex.Match(payload, @"\{ps:\[(.*?)\]\}");
            if (match.Success)
            {
                var splits = ParserHelpers.SplitRespectingParens(match.Groups[1].Value, ",");
                foreach (var split in splits) phase.InnerPhases.Add(Phase.Parse(split, isNested: true));
            }
            return phase;
        }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{{ps:[{string.Join(",", InnerPhases.Select(p => p?.ToSyntax(true)))}]}}";
    }

    public class ChoicePhase : Phase
    {
        public override string PhaseCode => "c";
        public ChoicePhaseType SelectionType { get; set; }
        public int SelectionNumber { get; set; }
        public List<RewardTag> Options { get; set; } = new List<RewardTag>();

        public static ChoicePhase Parse(string payload)
        {
            int h = payload.IndexOf('#'), s = payload.IndexOf(';');
            if (h == -1 || s == -1) return new ChoicePhase(); // Malformed

            return new ChoicePhase
            {
                SelectionType = Enum.TryParse<ChoicePhaseType>(payload.Substring(0, h), true, out var t) ? t : ChoicePhaseType.PointBuy,
                SelectionNumber = int.TryParse(payload.Substring(h + 1, s - h - 1), out int num) ? num : 0,
                Options = ParserHelpers.SplitRespectingParens(payload.Substring(s + 1), Delimiters.AtThree).Select(RewardTag.Parse).ToList()
            };
        }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{SelectionType}#{SelectionNumber};{string.Join(Delimiters.AtThree, Options.Select(o => o.ToSyntax()))}";
    }

    public class ChallengePhase : Phase
    {
        public override string PhaseCode => "9";
        public List<RewardTag> Rewards { get; set; } = new List<RewardTag>();
        public List<string> ExtraMonsters { get; set; } = new List<string>();

        public static ChallengePhase Parse(string payload)
        {
            var phase = new ChallengePhase();
            var rewM = Regex.Match(payload, "\"reward\":\\{\"data\":\"(.*?)\"\\}");
            if (rewM.Success) phase.Rewards = ParserHelpers.SplitRespectingParens(rewM.Groups[1].Value, Delimiters.AtThree).Select(RewardTag.Parse).ToList();

            var monM = Regex.Match(payload, "\"extraMonsters\":\\[(.*?)\\]");
            if (monM.Success && !string.IsNullOrEmpty(monM.Groups[1].Value))
            {
                phase.ExtraMonsters = monM.Groups[1].Value.Replace("\\\"", "\"").Trim('"').Split(new[] { "\",\"" }, StringSplitOptions.None).ToList();
            }
            return phase;
        }

        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{{\"reward\":{{\"data\":\"{string.Join(Delimiters.AtThree, Rewards.Select(r => r.ToSyntax()))}\"}},\"type\":{{\"extraMonsters\":[\"{string.Join("\",\"", ExtraMonsters)}\"]}}}}";
    }

    // =========================================================================
    // 6. Simplistic / Standard Phases
    // =========================================================================
    public class MessagePhase : Phase
    {
        public override string PhaseCode => "4";
        public string Message { get; set; }
        public string ButtonText { get; set; }

        public static MessagePhase Parse(string payload) { int i = payload.IndexOf(';'); return i >= 0 ? new MessagePhase { Message = payload.Substring(0, i), ButtonText = payload.Substring(i + 1) } : new MessagePhase { Message = payload }; }
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{Message}{(ButtonText != null ? ";" + ButtonText : "")}";
    }

    public class RunEndPhase : Phase { public override string PhaseCode => "e"; public override string ToSyntax(bool omitPrefix = false) => GetPrefix(omitPrefix); }
    public class ResetPhase : Phase { public override string PhaseCode => "6"; public override string ToSyntax(bool omitPrefix = false) => GetPrefix(omitPrefix); }

    public class HeroChangePhase : Phase
    {
        public override string PhaseCode => "5";
        public int HeroIndex, ChangeType;
        public static HeroChangePhase Parse(string p) => p.Length >= 2 ? new HeroChangePhase { HeroIndex = int.Parse(p[0].ToString()), ChangeType = int.Parse(p[1].ToString()) } : new HeroChangePhase();
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{HeroIndex}{ChangeType}";
    }

    public class PositionSwapPhase : Phase
    {
        public override string PhaseCode => "8";
        public int A, B;
        public static PositionSwapPhase Parse(string p) => p.Length >= 2 ? new PositionSwapPhase { A = int.Parse(p[0].ToString()), B = int.Parse(p[1].ToString()) } : new PositionSwapPhase();
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{A}{B}";
    }

    public class ItemCombinePhase : Phase
    {
        public override string PhaseCode => "7";
        public ItemCombineMode Mode { get; set; }
        public static ItemCombinePhase Parse(string p) => new ItemCombinePhase { Mode = Enum.TryParse<ItemCombineMode>(p, true, out var m) ? m : ItemCombineMode.SecondHighestToTierThrees };
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{Mode}";
    }

    public class PhaseGeneratorPhase : Phase
    {
        public override string PhaseCode => "g";
        public PhaseGeneratorMode Mode;
        public static PhaseGeneratorPhase Parse(string p) => new PhaseGeneratorPhase { Mode = p.TrimStart().StartsWith("h") ? PhaseGeneratorMode.Hero : PhaseGeneratorMode.Item };
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{(Mode == PhaseGeneratorMode.Hero ? "h" : "i")}";
    }

    public class RandomRevealPhase : Phase
    {
        public override string PhaseCode => "r";
        public RewardTag Reward;
        public static RandomRevealPhase Parse(string p) => new RandomRevealPhase { Reward = RewardTag.Parse(p) };
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{Reward?.ToSyntax()}";
    }

    public class TradePhase : Phase
    {
        public override string PhaseCode => "t";
        public List<RewardTag> Rewards = new List<RewardTag>();
        public static TradePhase Parse(string p) => new TradePhase { Rewards = ParserHelpers.SplitRespectingParens(p, Delimiters.AtThree).Select(RewardTag.Parse).ToList() };
        public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{string.Join(Delimiters.AtThree, Rewards.Select(r => r.ToSyntax()))}";
    }

    // Pseudo-phases logic
    public class PhaseIndexedPhase : Phase
    {
        public override string PhaseCode => "";
        public int Index { get; set; }
        public override string ToSyntax(bool omitPrefix = false) => $"{(FloorSelector != null ? FloorSelector.ToSyntax() + "." : "")}phi.{Index}";
    }

    public class PhaseModPickPhase : Phase
    {
        public override string PhaseCode => "";
        public int TargetTotal { get; set; }
        public override string ToSyntax(bool omitPrefix = false) => $"{(FloorSelector != null ? FloorSelector.ToSyntax() + "." : "")}phmp.{TargetTotal}";
    }

    // Action / Combat Phases
    public class PlayerRollingPhase : Phase { public override string PhaseCode => "0"; public string RawArgs; public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{RawArgs}"; }
    public class TargetingPhase : Phase { public override string PhaseCode => "1"; public string RawArgs; public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{RawArgs}"; }
    public class EnemyRollingPhase : Phase { public override string PhaseCode => "3"; public string RawArgs; public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{RawArgs}"; }
    public class DamagePhase : Phase { public override string PhaseCode => "d"; public string RawArgs; public override string ToSyntax(bool omitPrefix = false) => $"{GetPrefix(omitPrefix)}{RawArgs}"; }

    // =========================================================================
    // 7. Enums & Databases (Added from Implementation A for UI/Editor support)
    // =========================================================================
    public enum ItemCombineMode { SecondHighestToTierThrees, ZeroToThreeToSingle }
    public enum ChoicePhaseType { PointBuy, Number, UpToNumber, Optional }
    public enum PhaseGeneratorMode { Hero, Item }

    public static class PhaseDatabase
    {
        public static readonly Dictionary<string, string> Phases = new Dictionary<string, string>
        {
            { "ph.!", "Simple Choice Phase" }, { "ph.0", "Player Rolling Phase" },
            { "ph.1", "Targeting Phase" }, { "ph.2", "Level End Phase" },
            { "ph.3", "Enemy Rolling Phase" }, { "ph.4", "Message Phase" },
            { "ph.5", "Hero Change Phase" }, { "ph.6", "Reset Phase" },
            { "ph.7", "Item Combine Phase" }, { "ph.8", "Position Swap Phase" },
            { "ph.9", "Challenge Phase" }, { "ph.b", "Boolean Phase" },
            { "ph.c", "Choice Phase" }, { "ph.d", "Damage Phase" },
            { "ph.e", "Run End Phase" }, { "ph.l", "Linked Phase" },
            { "ph.r", "Random Reveal Phase" }, { "ph.s", "Seq Phase" },
            { "ph.t", "Trade Phase" }, { "ph.g", "Phase Generator Transform Phase" },
            { "ph.z", "Boolean Phase 2" }, { "phi.", "Phase Indexed" },
            { "phmp.", "Phase Mod Pick" }
        };
    }

    public class TagData
    {
        public string ChoosableKey { get; set; }
        public string SCPhaseKey { get; set; }
        public string TagType { get; set; }
        public string Syntax { get; set; }
        public string BaseLetter => ChoosableKey.Replace("ch.", "");

        public TagData(string choosableKey, string scPhaseKey, string tagType, string syntax)
        {
            ChoosableKey = choosableKey; SCPhaseKey = scPhaseKey; TagType = tagType; Syntax = syntax;
        }
    }

    public static class ChoosableDatabase
    {
        public static readonly List<TagData> AllTags = new List<TagData>
        {
            new TagData("ch.m", "ph.!m", "Modifier", "Standard"), new TagData("ch.i", "ph.!i", "Item", "Standard"),
            new TagData("ch.l", "ph.!l", "Levelup", "Standard"), new TagData("ch.g", "ph.!g", "Hero", "Standard"),
            new TagData("ch.r", "ph.!r", "Random", "Input"), new TagData("ch.q", "ph.!q", "RandomRange", "Input"),
            new TagData("ch.o", "ph.!o", "Or", "Input"), new TagData("ch.e", "ph.!e", "Enu", "Three"),
            new TagData("ch.v", "ph.!v", "Value", "Unique"), new TagData("ch.p", "ph.!p", "Replace", "Unique"),
            new TagData("ch.s", "ph.!s", "Skip", "None")
        };
        public static readonly Dictionary<string, TagData> ByChoosable = AllTags.ToDictionary(t => t.ChoosableKey);
        public static readonly Dictionary<string, TagData> BySCPhase = AllTags.ToDictionary(t => t.SCPhaseKey);
    }
}