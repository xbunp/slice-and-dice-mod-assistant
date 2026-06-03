using SliceDiceTextMod;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FullModUI : RootUI
{
    private TMP_Dropdown directiveDropdown;
    private ScrollRect directiveScrollView;
    private List<DirectiveUI> directiveUIs = new List<DirectiveUI>();

    private ModDataContainer SSOT => ModPackage.Instance.loadedMod;

    protected override void BuildUIAndBind()
    {
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.0f, 0.49f, new List<GridRowSpec>
            {
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, OnLoadModClicked)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, GetDropdownOptions(), OnDropdownSelected))
            }),
            new ColumnSpec("Right_Column", 0.51f, 1.0f, new List<GridRowSpec>
            {
                new GridRowSpec(800f, GridCellSpec.CreateScrollView("ModScrollView", 1.0f)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("CopyModBtn", "Copy Mod to Clipboard", 1.0f, OnCopyModClicked))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        directiveDropdown = generatedScreen.ColumnRefs["Left_Column"].Dropdowns["ModDropdown"];
        directiveScrollView = generatedScreen.ColumnRefs["Right_Column"].ScrollViews["ModScrollView"];

        // Connect to Single Source of Truth
        SSOT.OnDataChanged += OnStateChanged;
        OnStateChanged(null);
    }

    private void OnDestroy()
    {
        if (ModPackage.Instance != null && SSOT != null)
        {
            SSOT.OnDataChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        directiveUIs.Clear();
        foreach (var directive in SSOT.Directives)
        {
            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());
            if (entry != null)
            {
                System.Action removeAction = () =>
                {
                    SSOT.Directives.Remove(directive);
                    SSOT.NotifyDataChanged(this);
                    OnStateChanged(null);
                };

                directiveUIs.Add(entry.CreateUI(directive, uiGenerator, () => RebuildScrollView(), removeAction));
            }
        }

        RebuildScrollView();
    }

    private void RebuildScrollView()
    {
        if (directiveScrollView == null || directiveScrollView.content == null) return;

        List<GridRowSpec> masterRows = new List<GridRowSpec>();
        foreach (var ui in directiveUIs)
        {
            masterRows.AddRange(ui.GetRowSpecs());
        }

        var refs = uiGenerator.RebuildGrid(directiveScrollView.content, masterRows, true);
        directiveScrollView.content.sizeDelta = new Vector2(0f, refs.TotalHeight);

        foreach (var ui in directiveUIs)
        {
            ui.RestoreState(refs);
        }
    }

    private void OnLoadModClicked()
    {
        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard)) return;

        // Parse via the structured parsing engine directly into the SSOT
        ModParser.ParseIntoContainer(clipboard, SSOT);

        SSOT.NotifyDataChanged(this);
        OnStateChanged(null);
    }

    private void OnCopyModClicked()
    {
        // Serialize clean C# objects to string
        string output = ModSerializer.Export(SSOT);

        // Restore any compressed custom textures safely
        string rawText = ImageUtility.RestoreImages(output);

        GUIUtility.systemCopyBuffer = rawText;
        uiGenerator.CreatePopup("Mod copied to clipboard!", true, null);
    }

    private void OnDropdownSelected(int index)
    {
        if (index <= 0) return;
        string option = directiveDropdown.options[index].text;
        directiveDropdown.value = 0;

        var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => e.DropdownName == option);
        if (entry != null)
        {
            SSOT.Directives.Add(entry.CreateData());
            SSOT.NotifyDataChanged(this);
            OnStateChanged(null);
        }
    }

    private string[] GetDropdownOptions()
    {
        var opts = new List<string> { "Add Directive..." };
        opts.AddRange(SliceDiceTextMod.DirectiveRegistry.Entries.Select(e => e.DropdownName));
        return opts.ToArray();
    }
}