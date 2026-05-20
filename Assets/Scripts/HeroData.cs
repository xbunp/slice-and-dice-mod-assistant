using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroData
{
    [Header("Core Info")]
    public string heroName = "NewHero";
    public string baseReplica = "statue"; // e.g., statue, lost, thief
    public string imageOverride = "Statue"; // Default fallback
    public string colorClass = "y";       // y, o, r, b, g, etc.
    public int hp = 10;
    public int tier = 1;

    [Header("Dice Definition (6 Sides)")]
    public DiceSideData[] diceSides = new DiceSideData[6];

    [Header("Modifiers & Keywords")]
    public List<SideModifier> modifiers = new List<SideModifier>();

    [Header("Spells & Abilities")]
    public AbilityData ability;
    public bool hasAbility = false;

    [Header("Cosmetics & Meta")]
    public string imageString = "";
    [TextArea(2, 4)]
    public string passiveDoc = "";
    public List<string> speechLines = new List<string>();

    public HeroData()
    {
        // Initialize the 6 sides
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData();
    }
}


[System.Serializable]
public class SideModifier
{
    public TargetType target = TargetType.all;
    public List<string> keywords = new List<string>(); // e.g., "pain", "exert"

    public bool overrideFacade = false;
    public string facadeID = "bas170";
    public Vector3 facadeOffset = Vector3.zero;

    public string customDescription = "";
}

[System.Serializable]
public class AbilityData
{
    public string abilityName = "New Spell";
    public string baseType = "Fey"; // Usually Fey or Statue
    public DiceSideData[] abilitySides = new DiceSideData[6];
    public string imageIcon = "Blaze";
    public Vector3 hsvShift = Vector3.zero; // Hue, Saturation, Value shift
}

public enum TargetType
{
    left,       // index 0
    left2,      // index 1
    top,        // index 2
    bot,        // index 3
    topbot,     // indices 2 & 3
    right2,     // index 4
    right,      // index 5
    rightmost,  // Also index 5? Sometimes used differently in UI
    mid,        // Middle column?
    mid2,
    row,        // Middle row
    all,        // All sides
    col,        // Color wide
    self        // Passive/Hero wide
}

public enum AllNames
{
    None,
    Acolyte,
    Ace,
    Alien,
    Alloy,
    Armorer,
    Artificer,
    Assassin,
    Barbarian,
    Bard,
    Bash,
    Berserker,
    Brawler,
    Brigand,
    Brute,
    Buckle,
    Caldera,
    Chronos,
    Cleric,
    Clumsy,
    Coffin,
    Collector,
    Cultist,
    Curator,
    Dabble,
    Dabbler,
    Dabblest,
    Dancer,
    Defender,
    Dice,
    Disciple,
    Doctor,
    Druid,
    Eccentric,
    Enchanter,
    Evoker,
    Fate,
    Fencer,
    Fey,
    Fiend,
    Fighter,
    Forsaken,
    Gambler,
    Gardener,
    Ghast,
    Glacia,
    Gladiator,
    Granite,
    Guardian,
    Healer,
    Herbalist,
    Hoarder,
    Housecat,
    Initiate,
    Jester,
    Juggler,
    Jumble,
    Keeper,
    Knight,
    Lazy,
    Leader,
    Lost,
    Ludus,
    Luggage,
    Mage,
    Meddler,
    Medic,
    Mimic,
    Monk,
    Myco,
    Mystic,
    Ninja,
    Paladin,
    Pilgrim,
    Pockets,
    Poet,
    Presense,
    Priestess,
    Primrose,
    Prodigy,
    Prophet,
    Ranger,
    Reflection,
    Robot,
    Roulette,
    Ruffian,
    Scrapper,
    Scoundrel,
    Seer,
    Shaman,
    Sharpshot,
    Sinew,
    Soldier,
    Sorcerer,
    Spade,
    Sparky,
    Spellbalde,
    Sphere,
    Spine,
    Splint,
    Squire,
    Stalwart,
    Statue,
    Student,
    Surgeon,
    Tainted,
    Thief,
    Tinder,
    Trapper,
    Twin,
    Valkyrie,
    Vampire,
    Venom,
    Vessel,
    Veteran,
    Wallop,
    Wanderer,
    Warden,
    Warlock,
    Weaver,
    Whirl,
    Witch,
    Wizard,
    Wraith
}