using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ModEditor.Compiler;

public static class CodeBlocks
{
    // Exact 1:1 mapping with Textmod states. No floating assumptions.
    public static readonly List<string> _blockSyntaxOptions = new List<string>
    {
        // Logic Chains & Wrappers
        "Chain: Comma (Top Level AND)",
        "Chain: Ampersand (Nested AND)",
        "Wrapper: Floor Condition",
        "Wrapper: Multiplier (xN)",

        // Core Commands
        "Command: Add Entity (add.)",
        "Command: Fight Encounter (fight.)",
        "Command: Set Party (party.)",
        "Command: Replace Entity (replace.)",
        "Command: Add to Pool (item/hero/monster)",
        "Command: Grant All Items (allitem/alliteme)",
        "Command: Set Zone (zone.)",
        "Command: Set Difficulty (diff.)",
        "Command: Level Constraint (lvl.)",

        // Global Action
        "Global Command",

        // Context Modifiers
        "Context: Implied Phase (ph.)",
        "Context: Indexed Game Phase (phi.)",
        "Context: Modifier Pick Phase (phmp.)",
        "Context: Choosable Reward (ch.)",

        // Data-Driven Phases
        "Phase: Simple Choice (!)",
        "Phase: Level End Screen (2)",
        "Phase: Message Popup (4)",
        "Phase: Hero Change Offer (5)",
        "Phase: Item Combine / Smithing (7)",
        "Phase: Position Swap (8)",
        "Phase: Challenge Phase (9)",
        "Phase: Boolean Check 1 (b)",
        "Phase: Choice Screen (c)",
        "Phase: Linked Events (l)",
        "Phase: Random Reveal Popup (r)",
        "Phase: Story Sequence (s)",
        "Phase: Cursed Chest Trade (t)",
        "Phase: Generate Screen (g)",
        "Phase: Boolean Check 2 (z)",

        // Static Phases
        "Phase: Static (0,1,3,d,6,e)",

        // Rewards Tags
        "Reward: Standard (i/m/g/l)",
        "Reward: Random Reward (r/q)",
        "Reward: Random Choice (o)",
        "Reward: Enum Item (e)",
        "Reward: Modify Variable (v)",
        "Reward: Replace Reward (p)",
        "Reward: Skip (s)"
    };
}

namespace ModEditor
{
    public static class TextmodBlockFactory
    {
        public static ITextmodNode CreateBlock(string optionName)
        {
            switch (optionName)
            {
                case "Chain: Comma (Top Level AND)": return new CommaChainBlock();
                case "Chain: Ampersand (Nested AND)": return new AndChainBlock();
                case "Wrapper: Floor Condition": return new FloorConditionBlock();
                case "Wrapper: Multiplier (xN)": return new MultiplierBlock();

                case "Command: Add Entity (add.)": return new AddEntityBlock();
                case "Command: Fight Encounter (fight.)": return new FightBlock();
                case "Command: Set Party (party.)": return new PartyBlock();
                case "Command: Replace Entity (replace.)": return new ReplaceCommandBlock();
                case "Command: Add to Pool (item/hero/monster)": return new PoolBlock();
                case "Command: Grant All Items (allitem/alliteme)": return new AllItemBlock();
                case "Command: Set Zone (zone.)": return new SimpleValueBlock("zone");
                case "Command: Set Difficulty (diff.)": return new SimpleValueBlock("diff");
                case "Command: Level Constraint (lvl.)": return new LevelConstraintBlock();

                case "Global Command": return new GlobalCommandBlock();

                case "Context: Implied Phase (ph.)": return new ContextWrapperBlock("ph");
                case "Context: Indexed Game Phase (phi.)": return new ContextWrapperBlock("phi");
                case "Context: Modifier Pick Phase (phmp.)": return new ContextWrapperBlock("phmp");
                case "Context: Choosable Reward (ch.)": return new ContextWrapperBlock("ch");

                case "Phase: Simple Choice (!)": return new Phase_SimpleChoiceBlock();
                case "Phase: Level End Screen (2)": return new Phase_LevelEndBlock();
                case "Phase: Message Popup (4)": return new Phase_MessageBlock();
                case "Phase: Hero Change Offer (5)": return new Phase_HeroChangeBlock();
                case "Phase: Item Combine / Smithing (7)": return new Phase_ItemCombineBlock();
                case "Phase: Position Swap (8)": return new Phase_PositionSwapBlock();
                case "Phase: Challenge Phase (9)": return new Phase_ChallengeBlock();
                case "Phase: Boolean Check 1 (b)": return new Phase_Boolean1Block();
                case "Phase: Choice Screen (c)": return new Phase_ChoiceBlock();
                case "Phase: Linked Events (l)": return new Phase_LinkedBlock();
                case "Phase: Random Reveal Popup (r)": return new Phase_RandomRevealBlock();
                case "Phase: Story Sequence (s)": return new Phase_SequenceBlock();
                case "Phase: Cursed Chest Trade (t)": return new Phase_TradeBlock();
                case "Phase: Generate Screen (g)": return new Phase_GenerateScreenBlock();
                case "Phase: Boolean Check 2 (z)": return new Phase_Boolean2Block();
                case "Phase: Static (0,1,3,d,6,e)": return new Phase_StaticBlock();

                case "Reward: Standard (i/m/g/l)": return new Reward_StandardBlock();
                case "Reward: Random Reward (r/q)": return new Reward_RandomBlock();
                case "Reward: Random Choice (o)": return new Reward_ChoiceBlock();
                case "Reward: Enum Item (e)": return new Reward_EnumItemBlock();
                case "Reward: Modify Variable (v)": return new Reward_ValueModifyBlock();
                case "Reward: Replace Reward (p)": return new Reward_ReplaceBlock();
                case "Reward: Skip (s)": return new Reward_SkipBlock();

                default:
                    Debug.LogWarning($"Unknown block option selected: {optionName}");
                    return null;
            }
        }
    }
}

namespace ModEditor.Compiler
{
    // --- CORE INTERFACES & BASE CLASSES ---

    public interface ITextmodNode
    {
        string Compile();
    }

    public abstract class TextmodBlock : ITextmodNode
    {
        // Engine extracts these properties universally across all blocks if appended at depth-0
        public string CustomEncounterName = ""; // .mn.
        public string CustomEntityName = "";    // .n.
        public string ModTier = "";             // .modtier.

        public abstract string CompileCore();

        public string Compile()
        {
            string core = CompileCore();

            if (!string.IsNullOrEmpty(CustomEntityName))
                core += $".n.{CustomEntityName}";
            if (!string.IsNullOrEmpty(CustomEncounterName))
                core += $".mn.{CustomEncounterName}";
            if (!string.IsNullOrEmpty(ModTier))
                core += $".modtier.{ModTier}";

            return core;
        }

        protected string SafeCompile(ITextmodNode node)
        {
            if (node == null) return "";
            string compiled = node.Compile();

            // Textmod Engine un-nests parenthese aggressively. They are 100% safe bounds-protectors.
            char[] delimiters = { '&', '@', ';', ',', '+', '~', '#' };
            if (compiled.IndexOfAny(delimiters) >= 0 && !(compiled.StartsWith("(") && compiled.EndsWith(")")))
            {
                return $"({compiled})";
            }
            return compiled;
        }
    }

    // --- WRAPPERS & CHAINS ---

    public class CommaChainBlock : ITextmodNode
    {
        public List<ITextmodNode> Nodes = new List<ITextmodNode>();
        public string Compile() => string.Join(",", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
    }

    public class AndChainBlock : ITextmodNode
    {
        public List<ITextmodNode> Nodes = new List<ITextmodNode>();
        public string Compile() => string.Join("&", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
    }

    public class FloorConditionBlock : ITextmodNode
    {
        public enum ConditionType { Single, Range, EveryX }
        public ConditionType Type = ConditionType.Single;
        public int StartFloor = 1;
        public int EndFloor = 5;
        public int Interval = 2;
        public int Offset = 0; // Starts checking on this floor for EveryX
        public ITextmodNode Payload;

        public string Compile()
        {
            string prefix = "";
            switch (Type)
            {
                case ConditionType.Single: prefix = $"{StartFloor}."; break;
                case ConditionType.Range: prefix = $"{StartFloor}-{EndFloor}."; break;
                case ConditionType.EveryX: prefix = Offset > 0 ? $"e{Interval}.{Offset}." : $"e{Interval}."; break;
            }
            return prefix + Payload?.Compile();
        }
    }

    public class MultiplierBlock : ITextmodNode
    {
        public int Multiplier = 2;
        public ITextmodNode Payload;
        public string Compile() => Multiplier <= 1 ? Payload?.Compile() : $"x{Multiplier}.{Payload?.Compile()}";
    }

    public class ContextWrapperBlock : ITextmodNode
    {
        public string Prefix; // "ph", "phi", "phmp", "ch"
        public string Target = ""; // Often an index for phi, or a value for phmp
        public ITextmodNode Payload;

        public ContextWrapperBlock(string prefix) { Prefix = prefix; }

        public string Compile()
        {
            if (Payload != null) return $"{Prefix}.{Target}{Payload.Compile()}";
            return $"{Prefix}.{Target}";
        }
    }

    // --- CORE COMMANDS ---

    public class AddEntityBlock : TextmodBlock
    {
        public string Entity = "";
        public override string CompileCore() => $"add.{Entity}";
    }

    public class FightBlock : TextmodBlock
    {
        public List<string> Monsters = new List<string>();
        public override string CompileCore() => $"fight.{string.Join("+", Monsters)}";
    }

    public class PartyBlock : TextmodBlock
    {
        public List<string> Heroes = new List<string>();
        public override string CompileCore() => $"party.{string.Join("+", Heroes)}";
    }

    public class ReplaceCommandBlock : TextmodBlock
    {
        public ITextmodNode TargetNode;
        public override string CompileCore() => $"replace.{SafeCompile(TargetNode)}";
    }

    public class PoolBlock : TextmodBlock
    {
        public enum PoolType { Item, Hero, Monster }
        public PoolType Type = PoolType.Item;
        public List<string> Entities = new List<string>();
        public override string CompileCore() => $"{Type}.{string.Join("+", Entities)}";
    }

    public class AllItemBlock : TextmodBlock
    {
        public bool Equipped = false; // "alliteme" vs "allitem"
        public List<string> Pools = new List<string>();
        public override string CompileCore() => (Equipped ? "alliteme." : "allitem.") + string.Join("+", Pools);
    }

    public class SimpleValueBlock : TextmodBlock
    {
        private string Prefix; // "zone" or "diff"
        public string Value = "";
        public SimpleValueBlock(string prefix) { Prefix = prefix; }
        public override string CompileCore() => $"{Prefix}.{Value}";
    }

    public class LevelConstraintBlock : TextmodBlock
    {
        public ITextmodNode Payload;
        public override string CompileCore() => $"lvl.{SafeCompile(Payload)}";
    }

    public class GlobalCommandBlock : TextmodBlock
    {
        public enum GlobalType
        {
            Delevel, LevelUp, NoFlee, SkipAll, Skip, Temporary, Wish, ClearParty,
            Missing, Hidden, AddFight, Add10Fights, Add100Fights, MinusFight,
            CursemodeLoopdiff, Horde, DoubleMonsters, SkipRewards
        }
        public GlobalType Type = GlobalType.SkipAll;

        public override string CompileCore()
        {
            switch (Type)
            {
                case GlobalType.LevelUp: return "Level Up";
                case GlobalType.NoFlee: return "No Flee";
                case GlobalType.SkipAll: return "skip all";
                case GlobalType.ClearParty: return "Clear Party";
                case GlobalType.AddFight: return "Add Fight";
                case GlobalType.Add10Fights: return "Add 10 Fights";
                case GlobalType.Add100Fights: return "Add 100 Fights";
                case GlobalType.MinusFight: return "Minus Fight";
                case GlobalType.CursemodeLoopdiff: return "Cursemode Loopdiff";
                case GlobalType.DoubleMonsters: return "double monsters";
                case GlobalType.SkipRewards: return "skip rewards";
                default: return Type.ToString().ToLower(); // Matches enum closely enough
            }
        }
    }

    // --- PHASES ---

    public class Phase_SimpleChoiceBlock : TextmodBlock
    {
        public string Title = "";
        public List<ITextmodNode> Choices = new List<ITextmodNode>();
        public override string CompileCore()
        {
            string opts = string.Join("@3", Choices.Select(c => SafeCompile(c)));
            return string.IsNullOrEmpty(Title) ? $"!{opts}" : $"!{Title};{opts}";
        }
    }

    public class Phase_LevelEndBlock : TextmodBlock
    {
        public List<ITextmodNode> EndScreenData = new List<ITextmodNode>();
        public override string CompileCore() => $"2ps:[{string.Join(",", EndScreenData.Select(SafeCompile))}]";
    }

    public class Phase_MessageBlock : TextmodBlock
    {
        public string Message = "";
        public string ButtonText = "Ok";
        public override string CompileCore() => $"4{Message};{ButtonText}";
    }

    public class Phase_HeroChangeBlock : TextmodBlock
    {
        public int HeroPositionIndex = 0; // 0-based
        public bool IsRandomClass = false; // '0' is random class, '1' is generated
        public override string CompileCore() => $"5{HeroPositionIndex}{(IsRandomClass ? "0" : "1")}";
    }

    public class Phase_ItemCombineBlock : TextmodBlock
    {
        public enum CombineRule { SecondHighestToTierThrees, ZeroToThreeToSingle }
        public CombineRule Rule = CombineRule.SecondHighestToTierThrees;
        public override string CompileCore() => $"7{Rule}";
    }

    public class Phase_PositionSwapBlock : TextmodBlock
    {
        public int IndexA = 0;
        public int IndexB = 1;
        public override string CompileCore() => $"8{IndexA}{IndexB}";
    }

    public class Phase_ChallengeBlock : TextmodBlock
    {
        public List<string> ExtraMonsters = new List<string>();
        public ITextmodNode RewardPayload;

        public override string CompileCore()
        {
            // S&D uses standard JSON-like blocks for Challenge phase payload
            string monsters = string.Join(",", ExtraMonsters.Select(m => $"\"{m}\""));
            return $"9{{\"extraMonsters\":[{monsters}],\"data\":\"{SafeCompile(RewardPayload)}\"}}";
        }
    }

    public class Phase_Boolean1Block : TextmodBlock
    {
        public string VariableName = "";
        public int Threshold = 1;
        public ITextmodNode TrueBranch;
        public ITextmodNode FalseBranch;

        public override string CompileCore()
            => $"b{VariableName};{Threshold};{SafeCompile(TrueBranch)}@2{SafeCompile(FalseBranch)}";
    }

    public class Phase_ChoiceBlock : TextmodBlock
    {
        public string ChoiceType = "i"; // 'i', 'm', 'g', etc.
        public int NumChoices = 1;
        public string Title = "";
        public List<ITextmodNode> Options = new List<ITextmodNode>();

        public override string CompileCore()
        {
            string opts = string.Join("@3", Options.Select(SafeCompile));
            string core = $"c{ChoiceType}#{NumChoices};{opts}";
            return string.IsNullOrEmpty(Title) ? core : $"{core};{Title}";
        }
    }

    public class Phase_LinkedBlock : TextmodBlock
    {
        public List<ITextmodNode> Phases = new List<ITextmodNode>();
        public override string CompileCore() => $"l{string.Join("@1", Phases.Select(SafeCompile))}";
    }

    public class Phase_RandomRevealBlock : TextmodBlock
    {
        public ITextmodNode RewardData;
        public override string CompileCore() => $"r{SafeCompile(RewardData)}";
    }

    public class Phase_SequenceBlock : TextmodBlock
    {
        public string SequenceMessage = "";
        public struct SequenceStep { public string ButtonText; public ITextmodNode Action; }
        public List<SequenceStep> Steps = new List<SequenceStep>();

        public override string CompileCore()
        {
            string res = $"s{SequenceMessage}";
            foreach (var step in Steps) res += $"@1{step.ButtonText}@2{SafeCompile(step.Action)}";
            return res;
        }
    }

    public class Phase_TradeBlock : TextmodBlock
    {
        public ITextmodNode Item1;
        public ITextmodNode Item2;
        public override string CompileCore() => $"t{SafeCompile(Item1)}@3{SafeCompile(Item2)}";
    }

    public class Phase_GenerateScreenBlock : TextmodBlock
    {
        public enum ScreenType { LevelUp = 'h', Item = 'i' }
        public ScreenType Type = ScreenType.Item;
        public override string CompileCore() => $"g{(char)Type}";
    }

    public class Phase_Boolean2Block : TextmodBlock
    {
        public string VariableName = "";
        public int Threshold = 1;
        public ITextmodNode TrueBranch;
        public ITextmodNode FalseBranch;

        public override string CompileCore()
            => $"z{VariableName}@6{Threshold}@7{SafeCompile(TrueBranch)}@7{SafeCompile(FalseBranch)}";
    }

    public class Phase_StaticBlock : TextmodBlock
    {
        public enum StaticPhase { PlayerRolling = '0', Targeting = '1', EnemyRolling = '3', Damage = 'd', Reset = '6', RunEnd = 'e' }
        public StaticPhase Phase = StaticPhase.PlayerRolling;
        public override string CompileCore() => $"{(char)Phase}";
    }

    // --- REWARDS ---

    public class Reward_StandardBlock : TextmodBlock
    {
        public enum RewardType { Item = 'i', Modifier = 'm', Hero = 'g', LevelUp = 'l' }
        public RewardType Type = RewardType.Item;
        public string TargetEntity = "";
        public override string CompileCore() => $"{(char)Type}{TargetEntity}";
    }

    public class Reward_RandomBlock : TextmodBlock
    {
        public int MinTier = 1;
        public int MaxTier = 1;
        public int Amount = 1;
        public string RewardTypeFlag = "i"; // 'i', 'm', 'l', 'g'

        public override string CompileCore()
        {
            if (MinTier == MaxTier) return $"r{MinTier}~{Amount}~{RewardTypeFlag}";
            return $"q{MinTier}~{MaxTier}~{Amount}~{RewardTypeFlag}";
        }
    }

    public class Reward_ChoiceBlock : TextmodBlock
    {
        public List<ITextmodNode> Options = new List<ITextmodNode>();
        public override string CompileCore() => $"o{string.Join("@4", Options.Select(SafeCompile))}";
    }

    public class Reward_EnumItemBlock : TextmodBlock
    {
        public string EnumName = "RandoKeywordT1Item";
        public override string CompileCore() => $"e{EnumName}";
    }

    public class Reward_ValueModifyBlock : TextmodBlock
    {
        public string VariableName = "";
        public int ValueToAdd = 1;
        public override string CompileCore() => $"v{VariableName}V{ValueToAdd}";
    }

    public class Reward_ReplaceBlock : TextmodBlock
    {
        public bool IsModifierReplacement = false;
        public string TargetToReplace = "";
        public string NewValue = ""; // If Modifier

        public override string CompileCore()
        {
            if (IsModifierReplacement) return $"p m{TargetToReplace}~{NewValue}";
            return $"p{TargetToReplace}";
        }
    }

    public class Reward_SkipBlock : TextmodBlock
    {
        public override string CompileCore() => "s";
    }
}