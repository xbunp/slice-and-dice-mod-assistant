using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Abstract Base Class for all dynamic mod authoring panels.
/// </summary>
public abstract class DirectiveUI
{
    public string Id { get; private set; }
    public string TypeName { get; private set; }
    public SliceDiceTextMod.ModDirectiveData DataModel { get; protected set; }
    public System.Action<int> onMoveRequested;

    protected bool isExpanded = true;
    protected FullScreenUIGenerator uiGenerator;
    protected System.Action onRebuildNeeded;
    protected System.Action onRemoveRequested;

    protected float headerHeight => uiGenerator.rowHeight;
    protected float spacing => uiGenerator.rowSpacing;

    protected DirectiveUI(SliceDiceTextMod.ModDirectiveData dataModel, string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
    {
        Id = System.Guid.NewGuid().ToString();
        DataModel = dataModel;
        TypeName = typeName;
        uiGenerator = generator;
        onRebuildNeeded = onRebuild;
        onRemoveRequested = onRemove;
    }

    public static GridCellSpec CreateInput(string key, string label, float ratio, Action<string> onChanged, InputAlignment inputAlignment = InputAlignment.Top)
    {
        return new GridCellSpec
        {
            type = CellType.Input,
            key = key,
            labelText = label,
            widthRatio = ratio,
            onStringChanged = onChanged,
            inputAlignment = inputAlignment
        };
    }

    public virtual List<GridRowSpec> GetRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        List<GridRowSpec> innerRows = new List<GridRowSpec>();


        string foldoutSymbol = isExpanded ? "▼" : "▶";

        // --- UNIVERSAL HEADER (Always present for all directives) ---
        innerRows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateButton($"Foldout_{Id}", foldoutSymbol, 0.08f, ToggleCollapse),
            GridCellSpec.CreateLabel($"Title_{Id}", TypeName, 0.47f),
            GridCellSpec.CreateToggle($"Hidden_{Id}", "Hidden", 0.15f, (val) => DataModel.IsHidden = val),
            GridCellSpec.CreateButton($"MoveUp_{Id}", "▲", 0.10f, () => onMoveRequested?.Invoke(-1)),
            GridCellSpec.CreateButton($"MoveDown_{Id}", "▼", 0.10f, () => onMoveRequested?.Invoke(1)),
            GridCellSpec.CreateButton($"RemoveBtn_{Id}", "X", 0.10f, () => onRemoveRequested?.Invoke())
        ));

        if (isExpanded)
        {
            innerRows.Add(CreateSeparator());
            innerRows.AddRange(GetContentRowSpecs());
        }

        // Background wrapper logic
        float innerHeight = 0f;
        for (int i = 0; i < innerRows.Count; i++) innerHeight += innerRows[i].customHeight;
        if (innerRows.Count > 1) innerHeight += (innerRows.Count - 1) * spacing;

        var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BG_{Id}", 1.0f));
        bgRow.isBackground = true;
        bgRow.customHeight = innerHeight;
        bgRow.rowSpan = innerRows.Count;

        rows.Add(bgRow);
        rows.AddRange(innerRows);

        return rows;
    }

    protected abstract List<GridRowSpec> GetContentRowSpecs();

    /// <summary>
    /// Restores base-level state ("Hidden"). Inheriting classes override this, 
    /// call base.RestoreState(refs), and then restore their custom fields using the helper functions.
    /// </summary>
    public virtual void RestoreState(GridReferences refs)
    {
        RestoreToggle(refs, $"Hidden_{Id}", DataModel.IsHidden);
    }

    /// <summary>
    /// Fired right before the Copy/Export button generates the final string.
    /// Allows the directive to clean itself up (e.g. remove empty list elements).
    /// </summary>
    public virtual void PrepareForExport() { }

    protected void ToggleCollapse()
    {
        isExpanded = !isExpanded;
        onRebuildNeeded?.Invoke();
    }

    // =========================================================================
    // --- OPTIONAL UI ELEMENT HELPERS (For Subclasses) ---
    // =========================================================================

    protected GridRowSpec CreateSeparator() => new GridRowSpec(2f, GridCellSpec.CreateImagePanel($"Separator_{Id}", 1.0f));

    protected GridRowSpec CreateModNameFloorSelectorRow()
    {
        return new GridRowSpec(headerHeight,
            CreateInput($"ModName_{Id}", "Mod Name", 0.5f, (val) => DataModel.ModName = val),
            CreateInput($"FloorSelector_{Id}", "Floor Selector", 0.5f, (val) => DataModel.FloorSelectorRaw = val)
        );
    }

    protected GridRowSpec CreatePartModtierRow()
    {
        return new GridRowSpec(headerHeight,
            CreateInput($"Part_{Id}", "Part", 0.5f, (val) => { if (int.TryParse(val, out int parsed)) DataModel.Part = parsed; }),
            CreateInput($"Modtier_{Id}", "Modtier", 0.5f, (val) => { if (int.TryParse(val, out int parsed)) DataModel.Modtier = parsed; })
        );
    }

    protected GridRowSpec CreateDocRow()
    {
        return new GridRowSpec(headerHeight, CreateInput($"Doc_{Id}", "Doc Text", 1.0f, (val) => DataModel.Doc = val));
    }

    // =========================================================================
    // --- RESTORATION HELPERS (To reduce dictionary boilerplate) ---
    // =========================================================================

    protected void RestoreInput(GridReferences refs, string key, string value)
    {
        if (refs.Inputs.TryGetValue(key, out var input)) input.SetTextWithoutNotify(value);
    }

    protected void RestoreToggle(GridReferences refs, string key, bool value)
    {
        if (refs.Toggles.TryGetValue(key, out var toggle)) toggle.SetIsOnWithoutNotify(value);
    }
}


/// <summary>
/// Abstract wrapper mapping lists of input field strings with standard sizing actions (+ / -).
/// </summary>
/// <summary>
/// Abstract wrapper mapping lists of input field strings with standard sizing actions (+ / -).
/// </summary>
public abstract class DirectivePool : DirectiveUI
{
    protected const float CompactItemHeight = 22f;

    protected DirectivePool(SliceDiceTextMod.PoolDirectiveData dataModel, string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, typeName, generator, onRebuild, onRemove) { }

    protected SliceDiceTextMod.PoolDirectiveData PoolData => (SliceDiceTextMod.PoolDirectiveData)DataModel;

    protected virtual string GetNewElementString() => string.Empty;

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        if (PoolData == null) return rows;

        for (int i = 0; i < PoolData.Elements.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", PoolData.Elements[index], 0.75f, (val) => {
                    PoolData.Elements[index] = val;
                }, InputAlignment.Center),
                GridCellSpec.CreateButton($"RemoveElem_{Id}_{index}", "-", 0.1f, () => RemoveElementAt(index))
            ));
        }

        rows.Add(new GridRowSpec(CompactItemHeight, GridCellSpec.CreateButton($"AddElem_{Id}", "+ Add Element", 1.0f, AddElement)));
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        for (int i = 0; i < PoolData.Elements.Count; i++)
        {
            RestoreInput(refs, $"Input_{Id}_{i}", PoolData.Elements[i]);
        }
    }

    public override void PrepareForExport()
    {
        base.PrepareForExport();
        // Control export: Strip out completely empty elements to prevent malformed text mods
        PoolData?.Elements.RemoveAll(e => string.IsNullOrWhiteSpace(e));
    }

    protected void AddElement()
    {
        PoolData?.Elements.Add(GetNewElementString());
        onRebuildNeeded?.Invoke();
    }

    protected void RemoveElementAt(int index)
    {
        if (PoolData != null && index >= 0 && index < PoolData.Elements.Count)
        {
            PoolData.Elements.RemoveAt(index);
            onRebuildNeeded?.Invoke();
        }
    }
}

public class HeroPool : DirectivePool
{
    public HeroPool(SliceDiceTextMod.HeroPoolData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "HeroPool", generator, onRebuild, onRemove) { }

    protected override string GetNewElementString() => "(replica.Statue.n.NewHero.col.y.hp.7.tier.1.sd.0:0:0:0:0:0)";

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.HeroPoolData)DataModel;

        // Custom field unique to HeroPool
        rows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateToggle($"HeroToggle_{Id}", "Replace Base Heroes", 1.0f, (val) => {
                data.ReplaceBaseHeroes = val;
                onRebuildNeeded?.Invoke();
            })
        ));

        // Inject standard fields IF HeroPool uses them (Example, uncomment if needed)
        // rows.Add(CreatePartModtierRow());
        // rows.Add(CreateSeparator());

        // Base pool elements
        //rows.AddRange(base.GetContentRowSpecs());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs); // Restores "Hidden" and handles standard Elements list
        if (!isExpanded) return;

        var heroData = (SliceDiceTextMod.HeroPoolData)DataModel;
        RestoreToggle(refs, $"HeroToggle_{Id}", heroData.ReplaceBaseHeroes);

        // RestoreInput(refs, $"Part_{Id}", heroData.Part.ToString());
    }
}

public class MonsterPool : DirectivePool
{
    // Constructor
    public MonsterPool(SliceDiceTextMod.MonsterPoolData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "MonsterPool", generator, onRebuild, onRemove)
    {
        // constructor body empty unless initializing unique local variables
    }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        return rows;
    }
}

public class ItemPool : DirectivePool
{
    public ItemPool(SliceDiceTextMod.ItemPoolData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "ItemPool", generator, onRebuild, onRemove) { }
}


// =========================================================================
// 1. POOL DIRECTIVE UI PLACEHOLDERS (Inherit from DirectivePool)
// =========================================================================

public class ForcedFight : DirectivePool
{
    public ForcedFight(SliceDiceTextMod.ForcedFightData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Forced Fight", generator, onRebuild, onRemove) { }

    protected override string GetNewElementString() => "Fight_Placeholder";
}

public class SpawnInjection : DirectivePool
{
    public SpawnInjection(SliceDiceTextMod.SpawnInjectionData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Spawn Injection (Add)", generator, onRebuild, onRemove) { }

    protected override string GetNewElementString() => "Spawn_Placeholder";
}

public class PartyConfig : DirectivePool
{
    public PartyConfig(SliceDiceTextMod.PartyConfigData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Party Config", generator, onRebuild, onRemove) { }

    protected override string GetNewElementString() => "Hero_Placeholder";
}


// =========================================================================
// 2. STANDARD DIRECTIVE UI PLACEHOLDERS (Inherit from DirectiveUI)
// =========================================================================

public class ConfigDirective : DirectiveUI
{
    public ConfigDirective(SliceDiceTextMod.ConfigDirectiveData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Config Directive", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.ConfigDirectiveData)DataModel;

        // Configuration Inputs
        rows.Add(new GridRowSpec(headerHeight,
            CreateInput($"Prefix_{Id}", "Prefix", 0.3f, (val) => data.Prefix = val),
            CreateInput($"Payload_{Id}", "Payload", 0.7f, (val) => data.Payload = val)
        ));

        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        var data = (SliceDiceTextMod.ConfigDirectiveData)DataModel;
        RestoreInput(refs, $"Prefix_{Id}", data.Prefix);
        RestoreInput(refs, $"Payload_{Id}", data.Payload);
        RestoreInput(refs, $"ModName_{Id}", data.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", data.FloorSelectorRaw);
    }
}

public class ToggleDirective : DirectiveUI
{
    public ToggleDirective(SliceDiceTextMod.ToggleDirectiveData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Toggle", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.ToggleDirectiveData)DataModel;

        rows.Add(new GridRowSpec(headerHeight,
            CreateInput($"ToggleName_{Id}", "Toggle Name", 1.0f, (val) => data.ToggleName = val)
        ));

        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        var data = (SliceDiceTextMod.ToggleDirectiveData)DataModel;
        RestoreInput(refs, $"ToggleName_{Id}", data.ToggleName);
        RestoreInput(refs, $"ModName_{Id}", data.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", data.FloorSelectorRaw);
    }
}

public class CommandDirective : DirectiveUI
{
    public CommandDirective(SliceDiceTextMod.CommandDirectiveData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Command", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.CommandDirectiveData)DataModel;

        rows.Add(new GridRowSpec(headerHeight,
            CreateInput($"Command_{Id}", "Command syntax", 1.0f, (val) => data.Command = val)
        ));

        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        var data = (SliceDiceTextMod.CommandDirectiveData)DataModel;
        RestoreInput(refs, $"Command_{Id}", data.Command);
        RestoreInput(refs, $"ModName_{Id}", data.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", data.FloorSelectorRaw);
    }
}

public class CustomEntityUI : DirectiveUI
{
    public CustomEntityUI(SliceDiceTextMod.CustomEntityData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Custom Entity", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.CustomEntityData)DataModel;

        rows.Add(new GridRowSpec(headerHeight,
            CreateInput($"EntitySyntax_{Id}", "Raw Entity Syntax", 1.0f, (val) => data.EntitySyntax = val)
        ));

        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        var data = (SliceDiceTextMod.CustomEntityData)DataModel;
        RestoreInput(refs, $"EntitySyntax_{Id}", data.EntitySyntax);
        RestoreInput(refs, $"ModName_{Id}", data.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", data.FloorSelectorRaw);
    }
}

public class PhaseEvent : DirectiveUI
{
    public PhaseEvent(SliceDiceTextMod.PhaseEventData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Phase Event", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        // Custom phase configurations can go here. For now, we display the selector.
        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        RestoreInput(refs, $"ModName_{Id}", DataModel.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", DataModel.FloorSelectorRaw);
    }
}

public class RewardOption : DirectiveUI
{
    public RewardOption(SliceDiceTextMod.RewardOptionData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Reward Option", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        rows.Add(CreateModNameFloorSelectorRow());
        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        RestoreInput(refs, $"ModName_{Id}", DataModel.ModName);
        RestoreInput(refs, $"FloorSelector_{Id}", DataModel.FloorSelectorRaw);
    }
}

public class RawDirective : DirectiveUI
{
    public RawDirective(SliceDiceTextMod.RawDirectiveData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "Raw Text Fallback", generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        var data = (SliceDiceTextMod.RawDirectiveData)DataModel;

        rows.Add(new GridRowSpec(headerHeight,
            CreateInput($"RawContent_{Id}", "Raw Mod Text String", 1.0f, (val) => data.RawContent = val)
        ));

        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);
        if (!isExpanded) return;

        var data = (SliceDiceTextMod.RawDirectiveData)DataModel;
        RestoreInput(refs, $"RawContent_{Id}", data.RawContent);
    }
}