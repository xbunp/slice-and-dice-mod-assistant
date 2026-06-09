using System.Collections.Generic;
using System.Linq;

// 1. ONE UNIFIED DATA CLASS. No more sprawling interfaces and factories.
public class ModNode
{
    public enum NodeType
    {
        Chain_AND, Chain_COMMA,
        Command_AddEntity, Command_Fight, Command_Party, Command_Pool,
        Phase_Message, Phase_Choice, Phase_Trade,
        Wrapper_Floor, Wrapper_Multiplier,
        Reward_Standard, Reward_Random,
        Raw_String // Fallback for things we haven't built UI for yet
    }

    public NodeType Type;

    // The specific data for this node (e.g., "rat", "Message Text", "1-5")
    public List<string> DataFields = new List<string>();

    // The Textmod Properties (.n., .mn., .modtier.)
    public string CustomEntityName = "";
    public string CustomEncounterName = "";
    public string ModTier = "";
    public bool IsHidden = false;

    // Nested blocks! (The Two-Way Street equivalent of DropZones)
    public List<ModNode> Children = new List<ModNode>();

    // ====================================================================
    // EXPORTING: Turn this node (and its children) back into Textmod
    // ====================================================================
    public string Compile()
    {
        string core = "";

        switch (Type)
        {
            case NodeType.Chain_AND:
                core = string.Join("&", Children.Select(c => SafeWrap(c.Compile())));
                break;

            case NodeType.Chain_COMMA:
                core = string.Join(",", Children.Select(c => SafeWrap(c.Compile())));
                break;

            case NodeType.Command_AddEntity:
                core = $"add.{DataFields[0]}";
                break;

            case NodeType.Command_Fight:
                core = $"fight.{string.Join("+", DataFields)}";
                break;

            case NodeType.Wrapper_Floor:
                // DataFields: [0] = Type (Range, Single), [1] = Start, [2] = End
                string prefix = DataFields[0] == "Range" ? $"{DataFields[1]}-{DataFields[2]}." : $"{DataFields[1]}.";
                core = prefix + (Children.Count > 0 ? Children[0].Compile() : "");
                break;

            case NodeType.Raw_String:
                core = DataFields[0];
                break;
        }

        // Apply metadata seamlessly
        if (IsHidden) core += ".h.";
        if (!string.IsNullOrEmpty(CustomEntityName)) core += $".n.{CustomEntityName}";
        if (!string.IsNullOrEmpty(CustomEncounterName)) core += $".mn.{CustomEncounterName}";
        if (!string.IsNullOrEmpty(ModTier)) core += $".modtier.{ModTier}";

        return core;
    }

    private string SafeWrap(string compiledNode)
    {
        char[] delimiters = { '&', '@', ';', ',', '+', '~', '#' };
        if (compiledNode.IndexOfAny(delimiters) >= 0 && !(compiledNode.StartsWith("(") && compiledNode.EndsWith(")")))
            return $"({compiledNode})";
        return compiledNode;
    }
}