using System.Collections.Generic;

public static class PhaseDatabase
{
    public static readonly Dictionary<string, string> Phases = new Dictionary<string, string>
    {
        { "ph.!", "Simple Choice Phase" },
        { "ph.0", "Player Rolling Phase" },
        { "ph.1", "Targeting Phase" },
        { "ph.2", "Level End Phase" },
        { "ph.3", "Enemy Rolling Phase" },
        { "ph.4", "Message Phase" },
        { "ph.5", "Hero Change Phase" },
        { "ph.6", "Reset Phase" },
        { "ph.7", "Item Combine Phase" },
        { "ph.8", "Position Swap Phase" },
        { "ph.9", "Challenge Phase" },
        { "ph.b", "Boolean Phase" },
        { "ph.c", "Choice Phase" },
        { "ph.d", "Damage Phase" },
        { "ph.e", "Run End Phase" },
        { "ph.l", "Linked Phase" },
        { "ph.r", "Random Reveal Phase" },
        { "ph.s", "Seq Phase" },
        { "ph.t", "Trade Phase" },
        { "ph.g", "Phase Generator Transform Phase" },
        { "ph.z", "Boolean Phase 2" }
    };
}

public class TagData
{
    public string ChoosableKey { get; set; }  // e.g., "ch.m"
    public string SCPhaseKey { get; set; }    // e.g., "ph.!m"
    public string TagType { get; set; }       // e.g., "Modifier"
    public string Syntax { get; set; }        // e.g., "Standard"

    // Optional helper property to get just the single identifying letter (m, i, l, etc.)
    public string BaseLetter => ChoosableKey.Replace("ch.", "");

    public TagData(string choosableKey, string scPhaseKey, string tagType, string syntax)
    {
        ChoosableKey = choosableKey;
        SCPhaseKey = scPhaseKey;
        TagType = tagType;
        Syntax = syntax;
    }
}

public static class ChoosableDatabase
{
    // A master list of all tags
    public static readonly List<TagData> AllTags = new List<TagData>
    {
        new TagData("ch.m", "ph.!m", "Modifier", "Standard"),
        new TagData("ch.i", "ph.!i", "Item", "Standard"),
        new TagData("ch.l", "ph.!l", "Levelup", "Standard"),
        new TagData("ch.g", "ph.!g", "Hero", "Standard"),
        new TagData("ch.r", "ph.!r", "Random", "Input"),
        new TagData("ch.q", "ph.!q", "RandomRange", "Input"),
        new TagData("ch.o", "ph.!o", "Or", "Input"),
        new TagData("ch.e", "ph.!e", "Enu", "Three"),
        new TagData("ch.v", "ph.!v", "Value", "Unique"),
        new TagData("ch.p", "ph.!p", "Replace", "Unique"),
        new TagData("ch.s", "ph.!s", "Skip", "None")
    };

    // Dictionary to quickly look up a tag by its Choosable syntax (e.g., "ch.m")
    public static readonly Dictionary<string, TagData> ByChoosable = new Dictionary<string, TagData>();

    // Dictionary to quickly look up a tag by its SCPhase syntax (e.g., "ph.!m")
    public static readonly Dictionary<string, TagData> BySCPhase = new Dictionary<string, TagData>();

    // Constructor to automatically populate the dictionaries based on the list
    static ChoosableDatabase()
    {
        foreach (var tag in AllTags)
        {
            ByChoosable[tag.ChoosableKey] = tag;
            BySCPhase[tag.SCPhaseKey] = tag;
        }
    }
}

public enum ItemCombineType
{
    SecondHighestToTierThrees,
    ZeroToThreeToSingle
}

public enum ChoicePhaseType
{
    PointBuy,
    Number,
    UpToNumber,
    Optional
}

public enum GeneratorTransformType
{
    HeroLevelup = 'h',
    ItemReward = 'i'
}

public static class PhaseParser
{
    public static BasePhase ParseString(string syntax)
    {
        if (string.IsNullOrEmpty(syntax)) return null;

        // Clean up brackets or modifiers if they exist in the raw read layer
        // If syntax is formatted like "ph.4Hello" or "4Hello"
        string identifier = syntax.StartsWith("ph.") ? syntax.Substring(3, 1) : syntax.Substring(0, 1);
        string rawData = syntax.StartsWith("ph.") ? syntax.Substring(4) : syntax.Substring(1);

        BasePhase parsedPhase = identifier switch
        {
            "4" => new MessagePhase(),
            "5" => new HeroChangePhase(),
            "6" => new ResetPhase(),
            "b" => new BooleanPhase(),
            "l" => new LinkedPhase(),
            "s" => new SeqPhase(),
            "c" => new ChoicePhase(),
            "e" => new RunEndPhase(),
            // You will add the others here using the exact same pattern:
            // "0" => new PlayerRollingPhase(),
            // "7" => new ItemCombinePhase(),
            // "9" => new ChallengePhase(),
            // "z" => new BooleanPhase2(),
            // "r" => new RandomRevealPhase(),
            // "t" => new TradePhase(),
            // "2" => new LevelEndPhase(),
            // "g" => new PhaseGeneratorTransformPhase(),
            _ => null
        };

        if (parsedPhase != null)
        {
            parsedPhase.Parse(rawData);
        }

        return parsedPhase;
    }
}