using System;
using System.Collections.Generic;
using System.Linq;
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

[System.Serializable]
public class OnHitData : AbilityData
{
    public OnHitData() : base()
    {
        InitializeDiceFaces();
        // OnHit only uses the left side (0), zero out the rest by default
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
    }

    public override string Export()
    {
        // Ensure faces 1 through 5 are cleared so they don't show up in the text string
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }

        return ExportInner();
    }

    public override string ExportWrapped()
    {
        return $"({Export()})";
    }
}

[System.Serializable]
public class TriggerHPData : AbilityData
{
    public TriggerHPData() : base()
    {
        InitializeDiceFaces();
        // TriggerHP only uses the left side (0), zero out the rest
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
    }

    public override string Export()
    {
        // Ensure unused faces are cleared
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }

        // ExportInner() handles base properties and Color (via AppendColorModifier)
        // However, because AbilityData omits HP, we MUST manually append .hp.X to the end
        return $"{ExportInner()}.hp.{hp}";
    }

    public override string ExportWrapped()
    {
        return $"({Export()})";
    }
}

[System.Serializable]
public class OrbData : AbilityData
{
    // List of valid base-game targetless abilities defined in the request
    public static readonly HashSet<string> ValidBaseOrbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Restore", "Gaze", "Slice", "Balance", "Circle", "Glow", "Infuse", "Pray", "Scald", "Burn",
        "Foretell", "Drop", "Clink", "Operate", "Soothe", "Blades", "Crush", "Aid", "Invoke", "Mana",
        "Waste", "Wings", "Heat", "Hack", "Invest", "Luck", "Devoid", "Formation"
    };

    public bool isHardcoded = false;
    public string hardcodedAbilityName = "";
    public string carrierPrefix = "sthief.abilitydata";

    public OrbData() : base()
    {
        InitializeDiceFaces();
    }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        string clean = data.Trim();

        // Strip prefix elements to process nested custom ability structure cleanly
        if (clean.StartsWith("i.t.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(4);
        else if (clean.StartsWith("t.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(2);

        if (clean.StartsWith("orb.", StringComparison.OrdinalIgnoreCase))
            clean = clean.Substring(4);

        int openParen = clean.IndexOf('(');
        int closeParen = clean.LastIndexOf(')');

        if (openParen >= 0 && closeParen > openParen)
        {
            isHardcoded = false;
            string prefix = clean.Substring(0, openParen).TrimEnd('.');
            if (!string.IsNullOrEmpty(prefix))
            {
                carrierPrefix = prefix;
            }
            string innerPayload = clean.Substring(openParen + 1, closeParen - openParen - 1);
            base.Parse(innerPayload);
        }
        else
        {
            isHardcoded = true;
            hardcodedAbilityName = clean;
            entityName = clean;
            baseReplica = clean;
        }
    }

    public override string Export()
    {
        if (isHardcoded) return hardcodedAbilityName.ToLower();
        return ExportInner();
    }

    public override string ExportWrapped()
    {
        if (isHardcoded) return hardcodedAbilityName.ToLower();
        return $"({ExportInner()})";
    }

    public string ExportAsTrait(bool useITPrefix = true)
    {
        string prefix = useITPrefix ? "i.t.orb." : "t.orb.";
        if (isHardcoded)
        {
            string name = !string.IsNullOrEmpty(hardcodedAbilityName) ? hardcodedAbilityName.ToLower() : (entityName?.ToLower() ?? "slice");
            return $"{prefix}{name}";
        }
        string carrier = !string.IsNullOrEmpty(carrierPrefix) ? carrierPrefix : "sthief.abilitydata";
        return $"{prefix}{carrier}.({ExportInner()})";
    }
}