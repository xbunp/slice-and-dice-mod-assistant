using System;
using System.Collections.Generic;

public class BlockUIPanel
{
    public SliceDiceTextMod.TextModBlock Block { get; private set; }

    private bool isExpanded = true;
    private FullScreenUIGenerator uiGenerator;
    private Action onRebuildNeeded;
    public Action<int> onMoveRequested;
    private Action onRemoveRequested;

    private float headerHeight => uiGenerator.rowHeight;
    private float compactHeight = 22f;

    public BlockUIPanel(SliceDiceTextMod.TextModBlock block, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
    {
        Block = block;
        uiGenerator = generator;
        onRebuildNeeded = onRebuild;
        onRemoveRequested = onRemove;
    }

    public List<GridRowSpec> GetRowSpecs()
    {
        List<GridRowSpec> innerRows = new List<GridRowSpec>();

        // 1. UNIVERSAL HEADER
        innerRows.Add(new GridRowSpec(headerHeight,
            GridCellSpec.CreateButton($"Fold_{Block.Id}", isExpanded ? "▼" : "▶", 0.08f, () => { isExpanded = !isExpanded; onRebuildNeeded?.Invoke(); }),
            GridCellSpec.CreateLabel($"Title_{Block.Id}", Block.Title, 0.47f),
            GridCellSpec.CreateToggle($"Hidden_{Block.Id}", "Hidden", 0.15f, (val) => Block.IsHidden = val),
            GridCellSpec.CreateButton($"Up_{Block.Id}", "▲", 0.10f, () => onMoveRequested?.Invoke(-1)),
            GridCellSpec.CreateButton($"Down_{Block.Id}", "▼", 0.10f, () => onMoveRequested?.Invoke(1)),
            GridCellSpec.CreateButton($"Rem_{Block.Id}", "X", 0.10f, () => onRemoveRequested?.Invoke())
        ));

        if (isExpanded)
        {
            innerRows.Add(new GridRowSpec(2f, GridCellSpec.CreateImagePanel($"Sep_{Block.Id}", 1.0f)));

            // 2. WRAPPERS (ModName, Floor, Part, Tier)
            innerRows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateInput($"ModName_{Block.Id}", "Mod Name (.mn)", 0.5f, (v) => Block.ModName = v),
                GridCellSpec.CreateInput($"Floor_{Block.Id}", "Floor (floor.)", 0.5f, (v) => Block.FloorSelector = v)
            ));
            innerRows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateInput($"Part_{Block.Id}", "Part (.part)", 0.5f, (v) => Block.Part = v),
                GridCellSpec.CreateInput($"Tier_{Block.Id}", "Modtier (.modtier)", 0.5f, (v) => Block.ModTier = v)
            ));

            innerRows.Add(new GridRowSpec(2f, GridCellSpec.CreateImagePanel($"Sep2_{Block.Id}", 1.0f)));

            // 3. DYNAMIC CONTENT SHAPES
            switch (Block.Type)
            {
                case SliceDiceTextMod.BlockType.Raw:
                    innerRows.Add(new GridRowSpec(headerHeight, GridCellSpec.CreateInput($"P1_{Block.Id}", "Raw Syntax", 1.0f, (v) => Block.P1 = v)));
                    break;

                case SliceDiceTextMod.BlockType.PrefixPayload:
                    innerRows.Add(new GridRowSpec(headerHeight,
                        GridCellSpec.CreateInput($"Pref_{Block.Id}", "Prefix", 0.3f, (v) => Block.Prefix = v),
                        GridCellSpec.CreateInput($"P1_{Block.Id}", "Payload", 0.7f, (v) => Block.P1 = v)
                    ));
                    break;

                case SliceDiceTextMod.BlockType.MessagePhase:
                    innerRows.Add(new GridRowSpec(headerHeight,
                        GridCellSpec.CreateInput($"P1_{Block.Id}", "Message Text", 0.7f, (v) => Block.P1 = v),
                        GridCellSpec.CreateInput($"P2_{Block.Id}", "Btn Text", 0.3f, (v) => Block.P2 = v)
                    ));
                    break;

                case SliceDiceTextMod.BlockType.BooleanPhase:
                    innerRows.Add(new GridRowSpec(headerHeight,
                        GridCellSpec.CreateInput($"P1_{Block.Id}", "Variable Name", 0.6f, (v) => Block.P1 = v),
                        GridCellSpec.CreateInput($"P2_{Block.Id}", "Threshold #", 0.4f, (v) => Block.P2 = v)
                    ));
                    innerRows.Add(new GridRowSpec(headerHeight,
                        GridCellSpec.CreateInput($"P3_{Block.Id}", "If True (Phase)", 0.5f, (v) => Block.P3 = v),
                        GridCellSpec.CreateInput($"P4_{Block.Id}", "If False (Phase)", 0.5f, (v) => Block.P4 = v)
                    ));
                    break;

                case SliceDiceTextMod.BlockType.PoolList:
                case SliceDiceTextMod.BlockType.ChoiceList:
                    if (Block.Type == SliceDiceTextMod.BlockType.ChoiceList)
                    {
                        innerRows.Add(new GridRowSpec(headerHeight,
                            GridCellSpec.CreateInput($"Pref_{Block.Id}", "Prefix", 0.6f, (v) => Block.Prefix = v),
                            GridCellSpec.CreateInput($"Delim_{Block.Id}", "Delimiter", 0.4f, (v) => Block.Delimiter = v)
                        ));
                    }
                    else if (Block.Prefix == "heropool")
                    {
                        innerRows.Add(new GridRowSpec(compactHeight, GridCellSpec.CreateToggle($"RepBase_{Block.Id}", "Replace Base Heroes", 1.0f, (v) => Block.ReplaceBaseHeroes = v)));
                    }

                    for (int i = 0; i < Block.Elements.Count; i++)
                    {
                        int index = i;
                        innerRows.Add(new GridRowSpec(compactHeight,
                            GridCellSpec.CreateLabel($"Lbl_{Block.Id}_{i}", $"Item {i}", 0.15f),
                            GridCellSpec.CreateInput($"Elm_{Block.Id}_{i}", Block.Elements[i], 0.75f, (v) => Block.Elements[index] = v),
                            GridCellSpec.CreateButton($"Del_{Block.Id}_{i}", "-", 0.1f, () => { Block.Elements.RemoveAt(index); onRebuildNeeded?.Invoke(); })
                        ));
                    }
                    innerRows.Add(new GridRowSpec(compactHeight, GridCellSpec.CreateButton($"Add_{Block.Id}", "+ Add Element", 1.0f, () => { Block.Elements.Add(""); onRebuildNeeded?.Invoke(); })));
                    break;
            }
        }

        float totalHeight = 0f;
        foreach (var row in innerRows) totalHeight += row.customHeight;
        if (innerRows.Count > 1) totalHeight += (innerRows.Count - 1) * uiGenerator.rowSpacing;

        var finalRows = new List<GridRowSpec>();
        var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BG_{Block.Id}", 1.0f)) { isBackground = true, customHeight = totalHeight, rowSpan = innerRows.Count };
        finalRows.Add(bgRow);
        finalRows.AddRange(innerRows);

        return finalRows;
    }

    public void RestoreState(GridReferences refs)
    {
        if (refs.Toggles.TryGetValue($"Hidden_{Block.Id}", out var tHid)) tHid.SetIsOnWithoutNotify(Block.IsHidden);
        if (!isExpanded) return;

        if (refs.Inputs.TryGetValue($"ModName_{Block.Id}", out var iName)) iName.SetTextWithoutNotify(Block.ModName);
        if (refs.Inputs.TryGetValue($"Floor_{Block.Id}", out var iFlr)) iFlr.SetTextWithoutNotify(Block.FloorSelector);
        if (refs.Inputs.TryGetValue($"Part_{Block.Id}", out var iPart)) iPart.SetTextWithoutNotify(Block.Part);
        if (refs.Inputs.TryGetValue($"Tier_{Block.Id}", out var iTier)) iTier.SetTextWithoutNotify(Block.ModTier);

        if (refs.Inputs.TryGetValue($"P1_{Block.Id}", out var iP1)) iP1.SetTextWithoutNotify(Block.P1);
        if (refs.Inputs.TryGetValue($"P2_{Block.Id}", out var iP2)) iP2.SetTextWithoutNotify(Block.P2);
        if (refs.Inputs.TryGetValue($"P3_{Block.Id}", out var iP3)) iP3.SetTextWithoutNotify(Block.P3);
        if (refs.Inputs.TryGetValue($"P4_{Block.Id}", out var iP4)) iP4.SetTextWithoutNotify(Block.P4);

        if (refs.Inputs.TryGetValue($"Pref_{Block.Id}", out var iPre)) iPre.SetTextWithoutNotify(Block.Prefix);
        if (refs.Inputs.TryGetValue($"Delim_{Block.Id}", out var iDel)) iDel.SetTextWithoutNotify(Block.Delimiter);

        if (Block.Prefix == "heropool" && refs.Toggles.TryGetValue($"RepBase_{Block.Id}", out var tRep))
            tRep.SetIsOnWithoutNotify(Block.ReplaceBaseHeroes);

        for (int i = 0; i < Block.Elements.Count; i++)
        {
            if (refs.Inputs.TryGetValue($"Elm_{Block.Id}_{i}", out var iElm))
                iElm.SetTextWithoutNotify(Block.Elements[i]);
        }
    }
}