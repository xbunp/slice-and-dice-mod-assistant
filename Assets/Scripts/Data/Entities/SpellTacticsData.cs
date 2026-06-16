using UnityEngine;

[System.Serializable]
public class SpellData : AbilityData
{
    [Header("Spell Properties")]
    public int manaCost = 1;

    // Override the Export method directly
    public override string Export()
    {
        if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;

        // Return ExportInner without the $"( )" to fix the double parenthesis bug
        return ExportInner();
    }

    // Keep ExportWrapped just in case you use it elsewhere
    public override string ExportWrapped()
    {
        return $"({Export()})";
    }

    // Right side (Index 4) controls Mana on spells natively in the Slice & Dice engine
    public DiceSideData ManaCostSide
    {
        get => diceSides[4];
        set => diceSides[4] = value;
    }

    public SpellData() : base()
    {
        InitializeDiceFaces();
        // Initialize as a 1-cost spell using standard mana effect ID 76
        diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;
    }

    /*
    public override string ExportWrapped()
    {
        // Enforce the mana cost right before export to guarantee the game engine evaluates it as a spell
        if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;

        return $"({ExportInner()})";
    }
    */
}

[System.Serializable]
public class TacticData : AbilityData
{
    public DiceSideData TacticCostTop { get => diceSides[2]; set => diceSides[2] = value; }
    public DiceSideData TacticCostBottom { get => diceSides[3]; set => diceSides[3] = value; }
    public DiceSideData TacticCostRightmost { get => diceSides[5]; set => diceSides[5] = value; }

    public TacticData() : base()
    {
        InitializeDiceFaces();
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
    }

    // Override the Export method directly
    public override string Export()
    {
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;

        // Return ExportInner without the $"( )" to fix the double parenthesis bug
        return ExportInner();
    }

    // Keep ExportWrapped just in case you use it elsewhere
    public override string ExportWrapped()
    {
        return $"({Export()})";
    }
}