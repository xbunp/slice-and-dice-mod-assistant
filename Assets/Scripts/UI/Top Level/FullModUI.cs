using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SliceDiceTextMod;

public class FullModUI : RootUI
{
    private TMP_Dropdown directiveDropdown;
    private ScrollRect directiveScrollView;
    private List<BlockUIPanel> blockUIs = new List<BlockUIPanel>();

    // No registry. Just hardcoded templates that instantiate TextModBlocks.
    private readonly Dictionary<string, TextModBlock> blockTemplates = new Dictionary<string, TextModBlock>
    {
        { "Hero Pool", new TextModBlock { Title = "Hero Pool", Type = BlockType.PoolList, Prefix = "heropool" } },
        { "Monster Pool", new TextModBlock { Title = "Monster Pool", Type = BlockType.PoolList, Prefix = "monsterpool" } },
        { "Item Pool", new TextModBlock { Title = "Item Pool", Type = BlockType.PoolList, Prefix = "itempool" } },
        { "Forced Fight / Add Monster", new TextModBlock { Title = "Forced Fight / Add", Type = BlockType.PoolList, Prefix = "fight" } },
        { "Starting Party", new TextModBlock { Title = "Starting Party", Type = BlockType.PoolList, Prefix = "party" } },
        { "Modifier Choosable (ch.m)", new TextModBlock { Title = "Modifier Reward", Type = BlockType.PrefixPayload, Prefix = "ch.m" } },
        { "Item Choosable (ch.i)", new TextModBlock { Title = "Item Reward", Type = BlockType.PrefixPayload, Prefix = "ch.i" } },
        { "Random Tag (ch.r)", new TextModBlock { Title = "Random Reward", Type = BlockType.PrefixPayload, Prefix = "ch.r", P1 = "1~1~m" } },
        { "Or Tag List (ch.o)", new TextModBlock { Title = "Or List (Random 1 of List)", Type = BlockType.ChoiceList, Prefix = "ch.o", Delimiter = "@4" } },
        { "SCPhase List (ph.!)", new TextModBlock { Title = "Simple Choice Phase", Type = BlockType.ChoiceList, Prefix = "ph.!", Delimiter = "@3" } },
        { "Message Phase (ph.4)", new TextModBlock { Title = "Message Phase", Type = BlockType.MessagePhase, P2 = "ok" } },
        { "Boolean Phase (ph.b)", new TextModBlock { Title = "Boolean Logic Phase", Type = BlockType.BooleanPhase } },
        { "Reset Phase (ph.6)", new TextModBlock { Title = "Reset Phase", Type = BlockType.Raw, P1 = "ph.6" } },
        { "Run End Phase (ph.e)", new TextModBlock { Title = "Run End Phase", Type = BlockType.Raw, P1 = "ph.e" } },
        { "Custom / Raw Code", new TextModBlock { Title = "Raw Textmod Code", Type = BlockType.Raw } }
    };

    protected override void BuildUIAndBind()
    {
        float totalHeight = uiGenerator.canvas.GetComponent<RectTransform>().rect.height;
        float dynamicScrollViewHeight = totalHeight - uiGenerator.rowHeight - uiGenerator.rowSpacing;

        List<string> dropdownOptions = new List<string> { "Add TextMod Block..." };
        dropdownOptions.AddRange(blockTemplates.Keys);

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
        directiveDropdown = generatedScreen.ColumnRefs["Left_Column"].Dropdowns["ModDropdown"];
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
        string option = directiveDropdown.options[index].text;
        directiveDropdown.value = 0;

        if (blockTemplates.TryGetValue(option, out var template))
        {
            // Clone the template parameters to create a fresh block
            var newBlock = new TextModBlock
            {
                Title = template.Title,
                Type = template.Type,
                Prefix = template.Prefix,
                Delimiter = template.Delimiter,
                P1 = template.P1,
                P2 = template.P2
            };

            ModPackage.Instance.loadedMod.SaveDirective(null, newBlock);
            ModPackage.Instance.NotifyDirectiveSessionChanged(this);
        }
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