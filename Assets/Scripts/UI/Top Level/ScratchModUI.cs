using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SliceDiceTextMod;

public class ScratchModUI : RootUI
{
    private TMP_Dropdown blockDropdown;
    private ScrollRect directiveScrollView;
    private List<BlockUIPanel> blockUIs = new List<BlockUIPanel>();

    protected override void BuildUIAndBind()
    {
        float totalHeight = uiGenerator.canvas.GetComponent<RectTransform>().rect.height;
        float dynamicScrollViewHeight = totalHeight - uiGenerator.rowHeight - uiGenerator.rowSpacing;

        List<string> dropdownOptions = new List<string> { "Add TextMod Block..." };

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.0f, 0.49f, new List<GridRowSpec>
            {
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, null)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, dropdownOptions.ToArray(), OnDropdownSelected))
            }),
            new ColumnSpec("Right_Column", 0.51f, 1.0f, new List<GridRowSpec>
            {
                new GridRowSpec(dynamicScrollViewHeight, GridCellSpec.CreateScrollView("ModScrollView", 1.0f)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("CopyModBtn", "Copy Mod to Clipboard", 1.0f, OnCopyModClicked))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
        blockDropdown = generatedScreen.ColumnRefs["Left_Column"].Dropdowns["ModDropdown"];
        directiveScrollView = generatedScreen.ColumnRefs["Right_Column"].ScrollViews["ModScrollView"];

        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged += OnStateChanged;
            OnStateChanged(null);
        }
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        IReadOnlyList<TextModBlock> blocks = ModPackage.Instance.loadedMod.GetDirectives();
        Dictionary<TextModBlock, BlockUIPanel> existingUIs = blockUIs.ToDictionary(ui => ui.Block);
        blockUIs.Clear();

        foreach (var block in blocks)
        {
            if (existingUIs.TryGetValue(block, out var existingUI))
            {
                blockUIs.Add(existingUI);
            }
            else
            {
                var sessionBlock = ModPackage.Instance.GetOrCreateDirectiveSession(block);
                var newUI = new BlockUIPanel(sessionBlock, uiGenerator,
                    () => RebuildScrollView(),
                    () => ModPackage.Instance.DeleteDirective(block)
                );

                newUI.onMoveRequested = (dir) => ModPackage.Instance.MoveDirective(block, dir);
                blockUIs.Add(newUI);
            }
        }
        RebuildScrollView();
    }

    private void RebuildScrollView()
    {
        if (directiveScrollView == null || directiveScrollView.content == null) return;

        List<GridRowSpec> masterRows = new List<GridRowSpec>();
        foreach (var ui in blockUIs) masterRows.AddRange(ui.GetRowSpecs());

        var refs = uiGenerator.RebuildGrid(directiveScrollView.content, masterRows, true);
        directiveScrollView.content.sizeDelta = new Vector2(0f, refs.TotalHeight);

        foreach (var ui in blockUIs) ui.RestoreState(refs);
    }

    private void OnDropdownSelected(int index)
    {
        if (index <= 0) return;
        string option = blockDropdown.options[index].text;
        blockDropdown.value = 0;
    }

    private void OnCopyModClicked()
    {
        foreach (var ui in blockUIs) ModPackage.Instance.SaveDirective(ui.Block);

        string output = ModPackage.Instance.ExportModToTextModString();
        if (!string.IsNullOrEmpty(output))
        {
            GUIUtility.systemCopyBuffer = output;
            Debug.Log("Copied to clipboard!");
        }
    }
}