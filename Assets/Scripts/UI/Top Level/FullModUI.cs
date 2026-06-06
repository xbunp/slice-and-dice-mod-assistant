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
        float totalHeight = uiGenerator.canvas.GetComponent<RectTransform>().rect.height;
        float dynamicScrollViewHeight = totalHeight - uiGenerator.rowHeight - uiGenerator.rowSpacing;

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.0f, 0.49f, new List<GridRowSpec>
            {
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, OnLoadModClicked)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, GetDropdownOptions(), OnDropdownSelected))
                // Global "Auto-Pool" toggles removed. These are now handled natively inside the HeroPool directive UI.
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

    private void OnDestroy()
    {
        if (ModPackage.Instance != null) ModPackage.Instance.OnModDataChanged -= OnStateChanged;
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        var directives = ModPackage.Instance.loadedMod.GetDirectives();
        var existingUIs = directiveUIs.ToDictionary(ui => ui.DataModel);
        directiveUIs.Clear();

        foreach (var directive in directives)
        {
            if (existingUIs.TryGetValue(directive, out var existingUI))
            {
                directiveUIs.Add(existingUI); // Reuses the existing UI instance
            }
            else
            {
                var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());
                if (entry != null)
                {
                    // --- SAFETY CHECK ---
                    // Detects if the registry entry forgot to populate the UI delegate
                    if (entry.CreateUI == null)
                    {
                        Debug.LogError($"[FullModUI] Registry entry for '{entry.DropdownName}' is missing its 'CreateUI' delegate assignment!");
                        continue;
                    }

                    var workingReference = ModPackage.Instance.GetOrCreateDirectiveSession(directive);

                    var newUI = entry.CreateUI(workingReference, uiGenerator,
                        () => RebuildScrollView(),
                        () => ModPackage.Instance.DeleteDirective(directive)
                    );

                    if (newUI != null)
                    {
                        newUI.onMoveRequested = (dir) => ModPackage.Instance.MoveDirective(directive, dir);
                        directiveUIs.Add(newUI);
                    }
                }
                else
                {
                    Debug.LogWarning($"[FullModUI] No registry entry found for directive type: {directive.GetType().Name}");
                }
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

        // ModParser.ParseIntoContainer(clipboard, ModPackage.Instance.loadedMod);
        ModPackage.Instance.NotifyDirectiveSessionChanged(this);
    }

    private void OnCopyModClicked()
    {
        // 1. Run export prep (e.g. cleaning empty fields) and commit directives
        foreach (var d in directiveUIs)
        {
            d.PrepareForExport();
            ModPackage.Instance.SaveDirective(d.DataModel);
        }

        // 2. Perform the export
        string output = ModPackage.Instance.ExportModToTextModString();
        if (string.IsNullOrEmpty(output))
        {
            Debug.LogError("No TextMod loaded to export!");
            return;
        }

        // string rawText = ImageUtility.RestoreImages(output);
        GUIUtility.systemCopyBuffer = output;
        // uiGenerator.CreatePopup("Mod copied to clipboard!", true, null);
        Debug.Log("Copied to clipboard!");
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