using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Abstract Base Class for all dynamic mod authoring panels.
/// </summary>
public abstract class DirectiveUI
{
    public string Id { get; private set; }
    public string TypeName { get; private set; }

    public SliceDiceTextMod.ModDirectiveData DataModel { get; protected set; } // THE BINDING

    protected bool isExpanded = true;
    protected FullScreenUIGenerator uiGenerator;
    protected System.Action onRebuildNeeded;
    protected System.Action onRemoveRequested;

    protected DirectiveUI(SliceDiceTextMod.ModDirectiveData dataModel, string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
    {
        Id = System.Guid.NewGuid().ToString();
        DataModel = dataModel;
        TypeName = typeName;
        uiGenerator = generator;
        onRebuildNeeded = onRebuild;
        onRemoveRequested = onRemove;
    }

    /// <summary>
    /// Constructs structural rows wrapped inside a clean, tightly-bounded background card.
    /// </summary>
    public virtual List<GridRowSpec> GetRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        string foldoutSymbol = isExpanded ? "▼" : "▶";

        float headerHeight = uiGenerator.rowHeight;
        float spacing = uiGenerator.rowSpacing;

        // 1. Build the exact collection of rows that live inside the background card
        List<GridRowSpec> innerRows = new List<GridRowSpec>();

        // Header Row (Inside the panel)
        innerRows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateButton($"Foldout_{Id}", foldoutSymbol, 0.1f, ToggleCollapse),
            GridCellSpec.CreateLabel($"Title_{Id}", TypeName, 0.65f),
            GridCellSpec.CreateToggle($"Hidden_{Id}", "Hidden", 0.15f, (val) => DataModel.IsHidden = val),
            GridCellSpec.CreateButton($"RemoveBtn_{Id}", "X", 0.1f, () => onRemoveRequested?.Invoke())
        ));

        // Separator line and element controls
        if (isExpanded)
        {
            // Horizontal border line
            innerRows.Add(new GridRowSpec(2f,
                GridCellSpec.CreateImagePanel($"Separator_{Id}", 1.0f)
            ));

            innerRows.AddRange(GetContentRowSpecs());
        }

        // 2. Calculate precise height using the actual custom heights of the collected rows
        float innerHeight = 0f;
        for (int i = 0; i < innerRows.Count; i++)
        {
            innerHeight += innerRows[i].customHeight;
        }

        // Add layout spacing between the inner elements
        if (innerRows.Count > 1)
        {
            innerHeight += (innerRows.Count - 1) * spacing;
        }

        // 3. Create the background container row spanning the exact number of rows
        var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BG_{Id}", 1.0f));
        bgRow.isBackground = true;
        bgRow.customHeight = innerHeight;
        bgRow.rowSpan = innerRows.Count;
        rows.Add(bgRow);

        // 4. Add the built rows on top of the background
        rows.AddRange(innerRows);

        return rows;
    }

    protected abstract List<GridRowSpec> GetContentRowSpecs();
    public abstract void RestoreState(GridReferences refs);

    protected void ToggleCollapse()
    {
        isExpanded = !isExpanded;
        onRebuildNeeded?.Invoke();
    }
}

/// <summary>
/// Abstract wrapper mapping lists of input field strings with standard sizing actions (+ / -).
/// </summary>
public abstract class DirectivePool : DirectiveUI
{
    protected const float CompactItemHeight = 22f; // Slim row height for elements

    protected DirectivePool(SliceDiceTextMod.PoolDirectiveData dataModel, string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, typeName, generator, onRebuild, onRemove) { }

    protected SliceDiceTextMod.PoolDirectiveData PoolData => (SliceDiceTextMod.PoolDirectiveData)DataModel;

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        // Content rows
        for (int i = 0; i < PoolData.Elements.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", PoolData.Elements[index], 0.75f, (val) => PoolData.Elements[index] = val, InputAlignment.Center),
                GridCellSpec.CreateButton($"RemoveElem_{Id}_{index}", "-", 0.1f, () => RemoveElementAt(index))
            ));
        }

        // Clean, full-width footer button
        rows.Add(new GridRowSpec(CompactItemHeight,
            GridCellSpec.CreateButton($"AddElem_{Id}", "+ Add Element", 1.0f, AddElement)
        ));

        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        // Restore the "hidden" toggle state
        if (refs.Toggles != null && refs.Toggles.TryGetValue($"Hidden_{Id}", out var toggle))
        {
            toggle.isOn = DataModel.IsHidden;
        }

        if (!isExpanded) return;

        // Restore input states
        for (int i = 0; i < PoolData.Elements.Count; i++)
        {
            if (refs.Inputs.TryGetValue($"Input_{Id}_{i}", out var inputField))
            {
                inputField.text = PoolData.Elements[i];
            }
        }
    }

    private void AddElement()
    {
        PoolData.Elements.Add(string.Empty);
        onRebuildNeeded?.Invoke();
    }

    private void RemoveElementAt(int index)
    {
        if (index >= 0 && index < PoolData.Elements.Count)
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

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        var heroData = (SliceDiceTextMod.HeroPoolData)DataModel;
        List<GridRowSpec> rows = new List<GridRowSpec>();

        // Prepend the left-aligned toggle row
        rows.Add(new GridRowSpec(CompactItemHeight,
            GridCellSpec.CreateToggle($"HeroToggle_{Id}", "Replace Base Heroes", 0.3f, (val) => heroData.ReplaceBaseHeroes = val),
            GridCellSpec.CreateLabel($"HeroToggleSpacer_{Id}", "", 0.7f)
        ));

        // Append the base elements (dynamic inputs list + Add button)
        rows.AddRange(base.GetContentRowSpecs());

        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        // Restore parent inputs/toggles first
        base.RestoreState(refs);

        if (!isExpanded) return;

        // Restore custom toggle state
        if (refs.Toggles != null && refs.Toggles.TryGetValue($"HeroToggle_{Id}", out var toggle))
        {
            var heroData = (SliceDiceTextMod.HeroPoolData)DataModel;
            toggle.isOn = heroData.ReplaceBaseHeroes;
        }
    }
}

public class MonsterPool : DirectivePool
{
    public MonsterPool(SliceDiceTextMod.MonsterPoolData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "MonsterPool", generator, onRebuild, onRemove) { }
}

public class ItemPool : DirectivePool
{
    public ItemPool(SliceDiceTextMod.ItemPoolData dataModel, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(dataModel, "ItemPool", generator, onRebuild, onRemove) { }
}