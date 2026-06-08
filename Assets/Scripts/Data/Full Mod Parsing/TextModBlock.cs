using System;
using System.Collections.Generic;
using System.Linq;

namespace SliceDiceTextMod
{
    public enum BlockType
    {
        Raw,            // Single text box (Toggles, Custom Entities, Complex Challenge phases)
        PrefixPayload,  // Prefix + Payload (Difficulty, Choosables like ch.mModifier)
        PoolList,       // Prefix + '+' delimited list (HeroPool, Party, Fights)
        ChoiceList,     // Prefix + Custom Delimiter list (Or Tags [@4], SCPhase [@3])
        MessagePhase,   // Message + Button Text (Outputs ph.4Msg;Btn)
        BooleanPhase    // Var + Threshold + TruePhase + FalsePhase
    }

    [Serializable]
    public class TextModBlock
    {
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        // --- IDENTITY ---
        public string Title = "New Block";
        public BlockType Type = BlockType.Raw;

        // --- CONFIGURATION ---
        public string Prefix = "";
        public string Delimiter = "+"; // Used for ChoiceLists (@3, @4, etc)
        public bool ReplaceBaseHeroes = false;

        // --- WRAPPERS ---
        public bool IsHidden = false;
        public string FloorSelector = "";
        public string ModName = "";
        public string Part = "";
        public string ModTier = "";

        // --- CONTENT PAYLOADS ---
        public string P1 = ""; // Payload 1 (Command, Config Value, Message Text, Boolean Var)
        public string P2 = ""; // Payload 2 (Message Button, Boolean Threshold)
        public string P3 = ""; // Payload 3 (Boolean True Phase)
        public string P4 = ""; // Payload 4 (Boolean False Phase)
        public List<string> Elements = new List<string>();

        public string ToModString()
        {
            string core = "";

            // 1. Build the core string based on the "Shape" of the block
            switch (Type)
            {
                case BlockType.Raw:
                    core = P1;
                    break;
                case BlockType.PrefixPayload:
                    core = $"{Prefix}{P1}"; // Intentionally no forced dot, put the dot in the prefix if needed (e.g. "diff.")
                    break;
                case BlockType.MessagePhase:
                    core = $"ph.4{P1};{P2}";
                    break;
                case BlockType.BooleanPhase:
                    core = $"ph.b{P1};{P2};{P3}@2{P4}";
                    break;
                case BlockType.PoolList:
                    var pElms = Elements.Where(e => !string.IsNullOrWhiteSpace(e));
                    core = string.Join("+", pElms);
                    if (!string.IsNullOrEmpty(Prefix)) core = $"{Prefix}.{core}";
                    if (Prefix == "heropool" && ReplaceBaseHeroes) core = $"replace.{core}";
                    break;
                case BlockType.ChoiceList:
                    var cElms = Elements.Where(e => !string.IsNullOrWhiteSpace(e));
                    core = $"{Prefix}{string.Join(Delimiter, cElms)}";
                    break;
            }

            if (string.IsNullOrWhiteSpace(core)) return "";

            // 2. Apply sequential wrappers
            if (!string.IsNullOrEmpty(FloorSelector)) core = $"{FloorSelector}.{core}";
            if (!string.IsNullOrEmpty(Part)) core += $".part.{Part}";
            if (!string.IsNullOrEmpty(ModTier)) core += $".modtier.{ModTier}";
            if (!string.IsNullOrEmpty(ModName)) core += $".mn.{ModName}";

            // 3. Apply hiding wrapper last
            if (IsHidden) core = $"!m({core})";

            return core;
        }
    }
}