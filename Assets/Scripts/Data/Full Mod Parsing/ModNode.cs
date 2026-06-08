using System.Collections.Generic;
using System.Linq;

public enum NodeType
{
    // Wrappers (Hold exactly 1 child)
    Floor, Hidden, ModTier,

    // Actions (Hold 0 children)
    ForcedFight, Message, Reward, Command,

    // Logic/Phases (Hold 2+ children)
    BooleanPhase, SeqPhase, LinkedPhase
}

public class ModNode
{
    public NodeType Type;
    public string Payload1; // Used for "Seed", "Message Text", "Fight Name"
    public string Payload2; // Used for "3" (threshold)

    // The magic: Nodes contain other Nodes.
    public List<ModNode> Children = new List<ModNode>();

    // The Export function recursively builds the nightmare string for you
    public string ToTextModString()
    {
        switch (Type)
        {
            // --- WRAPPERS (Wrap their child) ---
            case NodeType.Floor:
                return $"{Payload1}.{Children[0].ToTextModString()}";
            case NodeType.Hidden:
                return $"!m({Children[0].ToTextModString()})";

            // --- ACTIONS (End of the line) ---
            case NodeType.ForcedFight:
                return $"fight.{Payload1}";
            case NodeType.Message:
                return $"ph.4{Payload1};{Payload2}"; // "ph.4Message;Button"

            // --- LOGIC PHASES (Format their children with delimiters) ---
            case NodeType.BooleanPhase:
                // ph.bVar;Threshold;TrueChild@2FalseChild
                string trueStr = Children.Count > 0 ? Children[0].ToTextModString() : "";
                string falseStr = Children.Count > 1 ? Children[1].ToTextModString() : "";
                return $"ph.b{Payload1};{Payload2};{trueStr}@2{falseStr}";

            case NodeType.LinkedPhase:
                // Joins all children with @1
                var childStrings = Children.Select(c => c.ToTextModString());
                return $"ph.l{string.Join("@1", childStrings)}";

            default:
                return "";
        }
    }
}