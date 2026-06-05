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
                new GridRowSpec(750f, GridCellSpec.CreateScrollView("ModScrollView", 1.0f)),
                
                // Expose settings for automated Hero Pool exports
                new GridRowSpec(uiGenerator.rowHeight,
                    GridCellSpec.CreateToggle("AutoReplaceBase", "Replace Base Heroes (Auto-Pool)", 0.5f, (v) => ModPackage.Instance.loadedMod.AutoExport_ReplaceBaseHeroes = v),
                    GridCellSpec.CreateToggle("AutoHidePool", "Hide Auto-Pool", 0.5f, (v) => ModPackage.Instance.loadedMod.AutoExport_HideHeroPool = v)),

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

    private void OnDestroy()
    {
        if (ModPackage.Instance != null) ModPackage.Instance.OnModDataChanged -= OnStateChanged;
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        // Restore settings state
        if (generatedScreen.ColumnRefs["Right_Column"].Toggles.TryGetValue("AutoReplaceBase", out var rToggle))
            rToggle.SetIsOnWithoutNotify(ModPackage.Instance.loadedMod.AutoExport_ReplaceBaseHeroes);
        if (generatedScreen.ColumnRefs["Right_Column"].Toggles.TryGetValue("AutoHidePool", out var hToggle))
            hToggle.SetIsOnWithoutNotify(ModPackage.Instance.loadedMod.AutoExport_HideHeroPool);

        directiveUIs.Clear();
        var directives = ModPackage.Instance.loadedMod.GetDirectives();

        foreach (var directive in directives)
        {
            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());
            if (entry != null)
            {
                var workingClone = ModPackage.Instance.GetOrCreateDirectiveSession(directive);

                directiveUIs.Add(entry.CreateUI(workingClone, uiGenerator, () =>
                {
                    ModPackage.Instance.SaveDirective(directive);
                    RebuildScrollView();
                },
                () => ModPackage.Instance.DeleteDirective(directive)));
            }
        }
        RebuildScrollView();
    }

    private void RebuildScrollView()
    {
        if (directiveScrollView == null || directiveScrollView.content == null) return;

        List<GridRowSpec> masterRows = new List<GridRowSpec>();
        foreach (var ui in directiveUIs) masterRows.AddRange(ui.GetRowSpecs());

        var refs = uiGenerator.RebuildGrid(directiveScrollView.content, masterRows, true);
        directiveScrollView.content.sizeDelta = new Vector2(0f, refs.TotalHeight);

        foreach (var ui in directiveUIs) ui.RestoreState(refs);
    }

    private void OnLoadModClicked()
    {
        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard)) return;

        ModParser.ParseIntoContainer(clipboard, ModPackage.Instance.loadedMod);
        ModPackage.Instance.NotifyDirectiveSessionChanged(this);
    }

    private void OnCopyModClicked()
    {
        // Force commit any remaining active directive clones before export
        foreach (var d in directiveUIs) ModPackage.Instance.SaveDirective(d.DataModel);

        string output = ModPackage.Instance.ExportModToTextModString();
        if (string.IsNullOrEmpty(output))
        {
            Debug.LogError("No TextMod loaded to export!");
            return;
        }

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
            ModPackage.Instance.loadedMod.SaveDirective(null, entry.CreateData());
            ModPackage.Instance.NotifyDirectiveSessionChanged(this);
        }
    }

    private string[] GetDropdownOptions()
    {
        var opts = new List<string> { "Add Directive..." };
        opts.AddRange(SliceDiceTextMod.DirectiveRegistry.Entries.Select(e => e.DropdownName));
        return opts.ToArray();
    }
}