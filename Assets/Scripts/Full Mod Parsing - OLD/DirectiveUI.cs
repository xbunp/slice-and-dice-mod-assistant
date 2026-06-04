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

    public virtual List<GridRowSpec> GetRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();
        string foldoutSymbol = isExpanded ? "▼" : "▶";

        float headerHeight = uiGenerator.rowHeight;
        float spacing = uiGenerator.rowSpacing;

        List<GridRowSpec> innerRows = new List<GridRowSpec>();

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
        if (data == null) return rows; // Safe check if no entity is loaded

        for (int i = 0; i < data.Elements.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", data.Elements[index], 0.75f, (val) => {
                    data.Elements[index] = val;

                    // Notify the UI using the Singleton facade instead of touching ModData
                    ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
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
        // FIX: Must use SetIsOnWithoutNotify to prevent cascading logic overrides
        if (refs.Toggles != null && refs.Toggles.TryGetValue($"Hidden_{Id}", out var toggle))
        {
            toggle.SetIsOnWithoutNotify(DataModel.IsHidden);
        }

        if (!isExpanded) return;

        // FIX: Must use SetTextWithoutNotify so that formatting restrictions inside 
        // TMP_InputField don't instantly corrupt the massive data string.
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

        // Notify through the Singleton facade
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
    }

    protected void RemoveElementAt(int index)
    {
        var data = PoolData;
        if (data == null) return;

        if (index >= 0 && index < data.Elements.Count)
        {
            data.Elements.RemoveAt(index);
            onRebuildNeeded?.Invoke();

            // Notify through the Singleton facade
            ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
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

        var data = PoolData;
        if (data == null) return rows; // Safe check if no entity is loaded

        for (int i = 0; i < data.Elements.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", data.Elements[index], 0.75f, (val) => {
                    data.Elements[index] = val;

                    // Notify the UI using the Singleton facade instead of touching ModData
                    ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
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
        base.RestoreState(refs);

        if (!isExpanded) return;

        // FIX: Use WithoutNotify logic here as well
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