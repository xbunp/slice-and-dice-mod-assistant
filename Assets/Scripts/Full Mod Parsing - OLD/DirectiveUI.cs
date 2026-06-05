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

    // Helper method provided for input generation
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
        string foldoutSymbol = isExpanded ? "▼" : "▶";

        float headerHeight = uiGenerator.rowHeight;
        float spacing = uiGenerator.rowSpacing;

        List<GridRowSpec> innerRows = new List<GridRowSpec>();

        // Header Row
        innerRows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateButton($"Foldout_{Id}", foldoutSymbol, 0.1f, ToggleCollapse),
            GridCellSpec.CreateLabel($"Title_{Id}", TypeName, 0.65f),
            GridCellSpec.CreateToggle($"Hidden_{Id}", "Hidden", 0.15f, (val) => DataModel.IsHidden = val),
            GridCellSpec.CreateButton($"RemoveBtn_{Id}", "X", 0.1f, () => onRemoveRequested?.Invoke())
        ));

        if (isExpanded)
        {
            innerRows.Add(new GridRowSpec(2f,
                GridCellSpec.CreateImagePanel($"Separator_{Id}", 1.0f)
            ));

            // Row 1: Mod Name & Floor Selector
            innerRows.Add(new GridRowSpec(headerHeight,
                CreateInput($"ModName_{Id}", "Mod Name", 0.5f, (val) => DataModel.ModName = val),
                CreateInput($"FloorSelector_{Id}", "Floor Selector", 0.5f, (val) => DataModel.FloorSelectorRaw = val)
            ));

            // Row 2: Doc text
            innerRows.Add(new GridRowSpec(headerHeight,
                CreateInput($"Doc_{Id}", "Doc Text", 1.0f, (val) => DataModel.Doc = val)
            ));

            // Row 3: Part & Modtier (with string-to-int parsing)
            innerRows.Add(new GridRowSpec(headerHeight,
                CreateInput($"Part_{Id}", "Part", 0.5f, (val) => {
                    if (int.TryParse(val, out int parsed)) DataModel.Part = parsed;
                }),
                CreateInput($"Modtier_{Id}", "Modtier", 0.5f, (val) => {
                    if (int.TryParse(val, out int parsed)) DataModel.Modtier = parsed;
                })
            ));

            // Separator before subclass specific elements
            innerRows.Add(new GridRowSpec(2f,
                GridCellSpec.CreateImagePanel($"InnerSeparator_{Id}", 1.0f)
            ));

            // Append specific child elements
            innerRows.AddRange(GetContentRowSpecs());
        }

        float innerHeight = 0f;
        for (int i = 0; i < innerRows.Count; i++)
        {
            innerHeight += innerRows[i].customHeight;
        }

        if (innerRows.Count > 1)
        {
            innerHeight += (innerRows.Count - 1) * spacing;
        }

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
    /// Restores the base level fields. 
    /// Inheriting classes should override this, call base.RestoreState(refs), and then restore their custom fields.
    /// </summary>
    public virtual void RestoreState(GridReferences refs)
    {
        // Example implementation pattern (update to match your exact GridReferences API):
        // refs.SetInputValue($"ModName_{Id}", DataModel.ModName);
        // refs.SetInputValue($"FloorSelector_{Id}", DataModel.FloorSelectorRaw);
        // refs.SetInputValue($"Doc_{Id}", DataModel.Doc);
        // refs.SetInputValue($"Part_{Id}", DataModel.Part.ToString());
        // refs.SetInputValue($"Modtier_{Id}", DataModel.Modtier.ToString());
        // refs.SetToggleValue($"Hidden_{Id}", DataModel.IsHidden);
    }

    protected void ToggleCollapse()
    {
        isExpanded = !isExpanded;
        onRebuildNeeded?.Invoke();
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
        var data = PoolData;
        if (data == null) return rows;

        for (int i = 0; i < data.Elements.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", data.Elements[index], 0.75f, (val) => {
                    data.Elements[index] = val;
                }, InputAlignment.Center),
                GridCellSpec.CreateButton($"RemoveElem_{Id}_{index}", "-", 0.1f, () => RemoveElementAt(index))
            ));
        }

        rows.Add(new GridRowSpec(CompactItemHeight,
            GridCellSpec.CreateButton($"AddElem_{Id}", "+ Add Element", 1.0f, AddElement)
        ));

        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        if (refs.Toggles != null && refs.Toggles.TryGetValue($"Hidden_{Id}", out var toggle))
        {
            toggle.SetIsOnWithoutNotify(DataModel.IsHidden);
        }

        if (!isExpanded) return;

        for (int i = 0; i < PoolData.Elements.Count; i++)
        {
            if (refs.Inputs.TryGetValue($"Input_{Id}_{i}", out var inputField))
            {
                inputField.SetTextWithoutNotify(PoolData.Elements[i]);
            }
        }
    }

    protected void AddElement()
    {
        var data = PoolData;
        if (data == null) return;

        data.Elements.Add(GetNewElementString());
        onRebuildNeeded?.Invoke();
    }

    protected void RemoveElementAt(int index)
    {
        var data = PoolData;
        if (data == null) return;

        if (index >= 0 && index < data.Elements.Count)
        {
            data.Elements.RemoveAt(index);
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
        // Leverage base pooling generation but prepend the Hero toggles specifically
        List<GridRowSpec> rows = base.GetContentRowSpecs();

        var data = (SliceDiceTextMod.HeroPoolData)DataModel;
        if (data == null) return rows;

        rows.Insert(0, new GridRowSpec(CompactItemHeight,
            GridCellSpec.CreateToggle($"HeroToggle_{Id}", "Replace Base Heroes", 1.0f, (val) => {
                data.ReplaceBaseHeroes = val;
                onRebuildNeeded?.Invoke();
            })
        ));

        return rows;
    }

    public override void RestoreState(GridReferences refs)
    {
        base.RestoreState(refs);

        if (!isExpanded) return;

        if (refs.Toggles != null && refs.Toggles.TryGetValue($"HeroToggle_{Id}", out var toggle))
        {
            var heroData = (SliceDiceTextMod.HeroPoolData)DataModel;
            toggle.SetIsOnWithoutNotify(heroData.ReplaceBaseHeroes);
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