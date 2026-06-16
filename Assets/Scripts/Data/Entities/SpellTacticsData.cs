using UnityEngine;

[System.Serializable]
public class SpellData : AbilityData
{
    [Header("Spell Properties")]
    public int manaCost = 1;

    public override string Export()
    {
        if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;
        return ExportInner();
    }

    public override string ExportWrapped()
    {
        return $"({Export()})";
    }

    public DiceSideData ManaCostSide
    {
        get => diceSides[4];
        set => diceSides[4] = value;
    }

    public SpellData() : base()
    {
        InitializeDiceFaces();
        diceSides[4].effectID = 76;
        diceSides[4].pips = manaCost;
    }
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

    public override string Export()
    {
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
        return ExportInner();
    }

    public override string ExportWrapped()
    {
        return $"({Export()})";
    }
}

public class TriggerHPData : AbilityData
{
    public override string ExportWrapped()
    {
        throw new System.NotImplementedException();
    }

    public override void Parse(string data)
    {
        base.Parse(data);
    }
}

public class OnHitData : AbilityData
{
    public override string ExportWrapped()
    {
        throw new System.NotImplementedException();
    }
    public override void Parse(string data)
    {
        base.Parse(data);
    }
}