using UnityEngine;

[System.Serializable]
public class SpellData : AbilityData
{
    [Header("Spell Properties")]
    public int manaCost = 1;

    // Right side (Index 4) controls Mana on spells natively in the Slice & Dice engine
    public DiceSideData ManaCostSide
    {
        get => diceSides[4];
        set => diceSides[4] = value;
    }

    public SpellData() : base()
    {
        // Initialize as a 1-cost spell using standard mana effect ID 76
        diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;
    }

    public override string ExportWrapped()
    {
        // Enforce the mana cost right before export to guarantee the game engine evaluates it as a spell
        if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;

        return $"({ExportInner()})";
    }
}

[System.Serializable]
public class TacticData : AbilityData
{
    // Tactic costs map natively to the Top (Index 2), Bottom (Index 3), and Rightmost (Index 5) slots
    public DiceSideData TacticCostTop
    {
        get => diceSides[2];
        set => diceSides[2] = value;
    }

    public DiceSideData TacticCostBottom
    {
        get => diceSides[3];
        set => diceSides[3] = value;
    }

    public DiceSideData TacticCostRightmost
    {
        get => diceSides[5];
        set => diceSides[5] = value;
    }

    public TacticData() : base()
    {
        // Ensure right side (Index 4) is completely blanked out to prevent the engine from misinterpreting a Tactic as a Spell
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
    }

    public override string ExportWrapped()
    {
        // Force side 4 blank immediately before export to prevent engine parsing it as a spell
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;

        return $"({ExportInner()})";
    }
}