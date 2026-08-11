// ============================================================================================
// UPDATED SPELL AST NODE
// ============================================================================================

using System;
using System.Collections.Generic;

/// <summary>
/// AST node representing a Spell ability.
/// Controls mana cost injection on side index 4 (effect 76) without corrupting base abilities.
/// </summary>
[System.Serializable]
public class SpellEntityNode : HeroEntityNode
{
    public int ManaCost { get; set; } = 1;

    public SpellEntityNode()
    {
        BaseReplica = "Fey";
    }

    public override string Export()
    {
        bool isBaseAbility = ExternalGameRegistry.IsValidAbility(BaseReplica);
        bool hasCustomMana = DiceSides.Sides[4] != null && (DiceSides.Sides[4].effectID != 0 || DiceSides.Sides[4].pips != 0);

        if (!isBaseAbility || hasCustomMana)
        {
            if (DiceSides.Sides[4] == null) DiceSides.Sides[4] = new DiceSideData();
            if (DiceSides.Sides[4].effectID == 0) DiceSides.Sides[4].effectID = 76;
            DiceSides.Sides[4].pips = ManaCost;
        }

        string coreInner = base.Export();

        if (ExternalGameRegistry.IsValidAbility(coreInner))
            return coreInner;

        return $"abilitydata.{StaticBranchTracing.SafeBracket(coreInner)}";
    }
}

// ============================================================================================
// UPDATED TACTIC AST NODE
// ============================================================================================

/// <summary>
/// AST node representing a Tactic ability.
/// Automatically zeroes side 4 and injects DSVarhest keyword cost items on faces 2 (top), 3 (bot), and 5 (right).
/// </summary>
[System.Serializable]
public class TacticEntityNode : HeroEntityNode
{
    public TacticEntityNode()
    {
        BaseReplica = "Fey";
    }

    public override string Export()
    {
        // Zero out mana cost face (side 4 is reserved strictly for Spells)
        if (DiceSides != null && DiceSides.Sides.Length > 4 && DiceSides.Sides[4] != null)
        {
            DiceSides.Sides[4].effectID = 0;
            DiceSides.Sides[4].pips = 0;
        }

        string coreInner = base.Export();
        if (ExternalGameRegistry.IsValidAbility(coreInner))
            return coreInner;

        return $"abilitydata.{StaticBranchTracing.SafeBracket(coreInner)}";
    }
}

// ============================================================================================
// UPDATED ON-HIT AST NODE
// ============================================================================================

/// <summary>
/// AST node representing an OnHit ability.
/// Enforces face zeroing for all dice sides except the left face (index 0).
/// </summary>
[System.Serializable]
public class OnHitEntityNode : HeroEntityNode
{
    public OnHitEntityNode()
    {
        BaseReplica = "Fey";
    }

    public override string Export()
    {
        // OnHit only utilizes side index 0
        for (int i = 1; i <= 5; i++)
        {
            if (DiceSides.Sides[i] != null)
            {
                DiceSides.Sides[i].effectID = 0;
                DiceSides.Sides[i].pips = 0;
            }
        }

        string coreInner = base.Export();
        return $"i.onhitdata.{StaticBranchTracing.SafeBracket(coreInner)}";
    }
}

// ============================================================================================
// UPDATED TRIGGER-HP AST NODE
// ============================================================================================

/// <summary>
/// AST node representing a TriggerHP ability.
/// Enforces face zeroing for non-left sides while preserving trigger HP threshold/frequency.
/// </summary>
[System.Serializable]
public class TriggerHPEntityNode : HeroEntityNode
{
    public TriggerHPEntityNode(int hpTrigger = 1)
    {
        BaseReplica = "Fey";
        Hp = hpTrigger;
    }

    public override string Export()
    {
        for (int i = 1; i <= 5; i++)
        {
            if (DiceSides.Sides[i] != null)
            {
                DiceSides.Sides[i].effectID = 0;
                DiceSides.Sides[i].pips = 0;
            }
        }

        string coreInner = base.Export();
        return $"i.triggerhpdata.{StaticBranchTracing.SafeBracket(coreInner)}";
    }
}

// ============================================================================================
// UPDATED ORB AST NODE
// ============================================================================================

/// <summary>
/// AST node representing an Orb trait/ability.
/// Supports hardcoded base-game targetless abilities and custom carrier-wrapped abilities.
/// </summary>
[System.Serializable]
public class OrbEntityNode : SDNode
{
    public static readonly HashSet<string> ValidBaseOrbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Restore", "Gaze", "Slice", "Balance", "Circle", "Glow", "Infuse", "Pray", "Scald", "Burn",
        "Foretell", "Drop", "Clink", "Operate", "Soothe", "Blades", "Crush", "Aid", "Invoke", "Mana",
        "Waste", "Wings", "Heat", "Hack", "Invest", "Luck", "Devoid", "Formation"
    };

    public bool IsHardcoded { get; set; } = false;
    public string HardcodedAbilityName { get; set; } = "";
    public string CarrierPrefix { get; set; } = "sthief.abilitydata";
    public SDNode CustomPayload { get; set; }

    public OrbEntityNode(string orbName = "")
    {
        if (!string.IsNullOrWhiteSpace(orbName))
        {
            if (ValidBaseOrbs.Contains(orbName.Trim()))
            {
                IsHardcoded = true;
                HardcodedAbilityName = orbName.Trim();
            }
        }
    }

    public string ExportAsTrait(bool useITPrefix = true)
    {
        string prefix = useITPrefix ? "i.t.orb." : "t.orb.";

        if (IsHardcoded)
        {
            string name = !string.IsNullOrWhiteSpace(HardcodedAbilityName) ? HardcodedAbilityName.ToLower() : "slice";
            return $"{prefix}{name}";
        }

        string carrier = !string.IsNullOrWhiteSpace(CarrierPrefix) ? CarrierPrefix.Trim() : "sthief.abilitydata";
        string payloadExport = CustomPayload?.Export() ?? "";

        if (!payloadExport.StartsWith("("))
            payloadExport = $"({payloadExport})";

        return $"{prefix}{carrier}.{payloadExport}";
    }

    public override string Export() => ExportAsTrait(useITPrefix: true);
}