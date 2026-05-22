using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HeroData
{
    [Header("Core Info")]
    public string heroName = "NewHero";
    public string baseReplica = "Statue"; // e.g., statue, lost, thief
    public string imageOverride = "None"; // Default fallback
    public string colorClass = "y";       // y, o, r, b, g, etc.
    public int hp = 7;
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
