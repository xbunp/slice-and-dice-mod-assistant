using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SpellData : AbilityData
{
    [Header("Spell Properties")]
    public int manaCost = 1;

    protected override string ExportInner()
    {
        bool isBaseAbility = ExternalGameRegistry.IsValidAbility(baseReplica);
        bool hasCustomMana = diceSides[4] != null && (diceSides[4].effectID != 0 || diceSides[4].pips != 0);

        // Prevents base spells from having `.sd.0:0:0:0:76-1` forcibly appended, 
        // which would ruin the string matching check
        if (!isBaseAbility || hasCustomMana)
        {
            if (diceSides[4] == null) diceSides[4] = new DiceSideData();
            if (diceSides[4].effectID == 0) diceSides[4].effectID = 76;
            diceSides[4].pips = manaCost;
        }
        return base.ExportInner();
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

    protected override string ExportInner()
    {
        diceSides[4].effectID = 0;
        diceSides[4].pips = 0;
        EnsureKeywordCostItems(); // <--- ADDED
        return base.ExportInner();
    }

    // <--- ADDED METHODS BELOW --->
    public void EnsureKeywordCostItems()
    {
        EnsureCostSideKeywordItem(2, "top");
        EnsureCostSideKeywordItem(3, "bot");
        EnsureCostSideKeywordItem(5, "right");
    }

    private void EnsureCostSideKeywordItem(int faceIndex, string sidePrefix)
    {
        if (diceSides == null || faceIndex >= diceSides.Length) return;
        var face = diceSides[faceIndex];

        if (items == null) items = new List<string>();

        // Strip previous DSVarhest items for this side to prevent duplicates
        items.RemoveAll(it =>
            it.Equals($"({sidePrefix}.cast.DSVarhest)", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"({sidePrefix}.cast.DSVarhest#Fly)", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"{sidePrefix}.cast.DSVarhest", StringComparison.OrdinalIgnoreCase) ||
            it.Equals($"{sidePrefix}.cast.DSVarhest#Fly", StringComparison.OrdinalIgnoreCase));

        // Inject appropriate keyword modifier item
        if (face != null && face.effectID == 13)
        {
            if (face.pips == 2)
            {
                items.Add($"({sidePrefix}.cast.DSVarhest)");
            }
            else if (face.pips == 4)
            {
                items.Add($"({sidePrefix}.cast.DSVarhest#Fly)");
            }
        }
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

    protected override string ExportInner()
    {
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
        return base.ExportInner();
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

    protected override string ExportInner()
    {
        // Ensure unused faces are cleared
        for (int i = 1; i <= 5; i++)
        {
            diceSides[i].effectID = 0;
            diceSides[i].pips = 0;
        }
        // ExportInner() handles base properties, Color, AND HP now!
        // No manual HP. appending needed.

        // TODO: LATER: sanity double check, make sure HP IS there for an TriggerHPData
        return base.ExportInner();
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

    protected override string ExportCore()
    {
        if (isHardcoded) return hardcodedAbilityName.ToLower();
        return base.ExportCore(); // Base class will safely handle the bracketing check
    }


    /*
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
    */

    public string ExportAsTrait(bool useITPrefix = true)
    {
        string prefix = useITPrefix ? "i.t.orb." : "t.orb.";
        if (isHardcoded)
        {
            string name = !string.IsNullOrEmpty(hardcodedAbilityName) ? hardcodedAbilityName.ToLower() : (entityName?.ToLower() ?? "slice");
            return $"{prefix}{name}";
        }
        string carrier = !string.IsNullOrEmpty(carrierPrefix) ? carrierPrefix : "sthief.abilitydata";
        // Export() now guarantees (...)
        return $"{prefix}{carrier}.{Export()}";
    }
}