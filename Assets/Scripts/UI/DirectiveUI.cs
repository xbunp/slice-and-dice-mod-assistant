using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract Base Class for all dynamic mod authoring panels.
/// </summary>
public abstract class DirectiveUI
{
    public string Id { get; private set; }
    public string TypeName { get; private set; }
    protected bool isExpanded = true;

    protected FullScreenUIGenerator uiGenerator;
    protected System.Action onRebuildNeeded;
    protected System.Action onRemoveRequested;

    protected DirectiveUI(string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
    {
        Id = System.Guid.NewGuid().ToString();
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
        float itemHeight = 22f; // Tight, professional element row height
        float spacing = uiGenerator.rowSpacing;

        // Calculate dynamic height requirements
        int totalRowsToWrap = 1; // Header always wrapped
        if (isExpanded)
        {
            totalRowsToWrap += GetContentRowCount(); // Includes separator, elements, and footer
        }

        if (isExpanded)
        {
            // Simulate exact layout coordinates of BuildGrid to find the precise backdrop height
            float padding = 8f; // Margin padding
            float currentY = -padding;

            // 1. Header height
            currentY -= headerHeight;

            // 2. Separator height
            currentY -= spacing;
            currentY -= 2f;

            // 3. Elements height
            for (int i = 0; i < GetItemCount(); i++)
            {
                currentY -= spacing;
                currentY -= itemHeight;
            }

            // 4. Footer Add button height
            currentY -= spacing;
            currentY -= itemHeight;

            currentY -= padding; // Bottom padding

            float simulatedBackdropHeight = Mathf.Abs(currentY);

            // Container Panel: Instantiates behind the header and elements
            var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel(
                $"BG_{Id}",
                1.0f,
                new Color(0.12f, 0.12f, 0.13f, 1.0f) // Dark-theme charcoal backing
            ));
            bgRow.isBackground = true;
            bgRow.customHeight = simulatedBackdropHeight;
            bgRow.rowSpan = totalRowsToWrap;
            rows.Add(bgRow);
        }
        else
        {
            // Simple closed container covering just the header
            var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel(
                $"BG_{Id}",
                1.0f,
                new Color(0.12f, 0.12f, 0.13f, 1.0f)
            ));
            bgRow.isBackground = true;
            bgRow.rowSpan = 1;
            rows.Add(bgRow);
        }

        // 1. Header Row
        rows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateButton($"Foldout_{Id}", foldoutSymbol, 0.1f, ToggleCollapse),
            GridCellSpec.CreateLabel($"Title_{Id}", TypeName, 0.8f),
            GridCellSpec.CreateButton($"RemoveBtn_{Id}", "X", 0.1f, () => onRemoveRequested?.Invoke())
        ));

        // 2. Header separator line and content rows
        if (isExpanded)
        {
            // Compact 2f horizontal separator line directly beneath the header
            rows.Add(new GridRowSpec(2f,
                GridCellSpec.CreateImagePanel($"Separator_{Id}", 1.0f, new Color(0.24f, 0.24f, 0.26f, 1.0f))
            ));

            rows.AddRange(GetContentRowSpecs());
        }

        // 3. Spacing margin between different directive blocks
        rows.Add(new GridRowSpec(15f,
            GridCellSpec.CreateLabel($"Spacer_{Id}", "", 1.0f)
        ));

        return rows;
    }

    protected abstract List<GridRowSpec> GetContentRowSpecs();
    protected abstract int GetContentRowCount();
    protected abstract int GetItemCount();
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
    protected List<string> items = new List<string>();
    private const float CompactItemHeight = 22f; // Slim row height for elements

    protected DirectivePool(string typeName, FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base(typeName, generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        // Content rows
        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            rows.Add(new GridRowSpec(CompactItemHeight,
                GridCellSpec.CreateLabel($"IndexLabel_{Id}_{index}", $"Element {index}", 0.15f),
                GridCellSpec.CreateInput($"Input_{Id}_{index}", "", 0.75f, (val) => items[index] = val),
                GridCellSpec.CreateButton($"RemoveElem_{Id}_{index}", "-", 0.1f, () => RemoveElementAt(index))
            ));
        }

        // Clean, full-width footer button
        rows.Add(new GridRowSpec(CompactItemHeight,
            GridCellSpec.CreateButton($"AddElem_{Id}", "+ Add Element", 1.0f, AddElement)
        ));

        return rows;
    }

    protected override int GetContentRowCount()
    {
        return items.Count + 2; // elements + 1 Add button + 1 Separator line
    }

    protected override int GetItemCount()
    {
        return items.Count;
    }

    public override void RestoreState(GridReferences refs)
    {
        if (!isExpanded) return;

        for (int i = 0; i < items.Count; i++)
        {
            if (refs.Inputs.TryGetValue($"Input_{Id}_{i}", out var inputField))
            {
                inputField.text = items[i];
            }
        }
    }

    private void AddElement()
    {
        items.Add(string.Empty);
        onRebuildNeeded?.Invoke();
    }

    private void RemoveElementAt(int index)
    {
        if (index >= 0 && index < items.Count)
        {
            items.RemoveAt(index);
            onRebuildNeeded?.Invoke();
        }
    }
}

public class HeroPool : DirectivePool
{
    public HeroPool(FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base("HeroPool", generator, onRebuild, onRemove) { }
}

public class MonsterPool : DirectivePool
{
    public MonsterPool(FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base("MonsterPool", generator, onRebuild, onRemove) { }
}

public class ItemPool : DirectivePool
{
    public ItemPool(FullScreenUIGenerator generator, System.Action onRebuild, System.Action onRemove)
        : base("ItemPool", generator, onRebuild, onRemove) { }
}