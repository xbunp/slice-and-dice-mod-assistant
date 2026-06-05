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
        // Retrieve the exact dynamic height of the content canvas container
        float totalHeight = uiGenerator.canvas.GetComponent<RectTransform>().rect.height;

        // Perfect math using the generator's public layout properties.
        // This perfectly accounts for the manual rowSpacing used inside BuildGrid.
        float dynamicScrollViewHeight = totalHeight - uiGenerator.rowHeight - uiGenerator.rowSpacing;

        var columns = new List<ColumnSpec>
    {
        new ColumnSpec("Left_Column", 0.0f, 0.49f, new List<GridRowSpec>
        {
            new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, OnLoadModClicked)),
            new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, GetDropdownOptions(), OnDropdownSelected)),
            
            // Checkboxes situated in the Left Column
            new GridRowSpec(uiGenerator.rowHeight,
                GridCellSpec.CreateToggle("AutoReplaceBase", "Replace Base Heroes (Auto-Pool)", 0.5f, (v) => ModPackage.Instance.loadedMod.AutoExport_ReplaceBaseHeroes = v),
                GridCellSpec.CreateToggle("AutoHidePool", "Hide Auto-Pool", 0.5f, (v) => ModPackage.Instance.loadedMod.AutoExport_HideHeroPool = v))
        }),
        new ColumnSpec("Right_Column", 0.51f, 1.0f, new List<GridRowSpec>
        {
            // Dynamically sized to fill all space down to the bottom button perfectly
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

    /// <summary>
    /// Retrieves the real, scaled height of the active UI canvas or container, 
    /// avoiding DPI/resolution scaling mismatch issues.
    /// </summary>
    private float GetScaleIndependentHeight()
    {
        // Try to get the height of the UI Generator's own RectTransform
        if (uiGenerator != null)
        {
            RectTransform uiGenRect = uiGenerator.GetComponent<RectTransform>();
            if (uiGenRect != null && uiGenRect.rect.height > 100f)
            {
                return uiGenRect.rect.height;
            }
        }

        // Try to locate the main UI Canvas to read its scaled dimensions
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null && uiGenerator != null)
        {
            canvas = uiGenerator.GetComponentInParent<Canvas>();
        }
        if (canvas == null)
        {
            canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        }

        if (canvas != null && canvas.transform is RectTransform canvasRect)
        {
            return canvasRect.rect.height;
        }

        // Fallback if UI is initialized before the canvas scales
        return Screen.height;
    }

    /// <summary>
    /// Helper method to determine the available height. 
    /// Adjust this to match your UI framework's way of retrieving screen or window dimensions.
    /// </summary>
    private float GetAvailableHeight()
    {
        // Example fallback: If uiGenerator exposes a height limit, use it.
        // Otherwise, you might use Unity's Screen.height, or a parent container's height.
        return 900f;
    }

    private void OnDestroy()
    {
        if (ModPackage.Instance != null) ModPackage.Instance.OnModDataChanged -= OnStateChanged;
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        // Restore settings state from the Left_Column
        if (generatedScreen.ColumnRefs["Left_Column"].Toggles.TryGetValue("AutoReplaceBase", out var rToggle))
            rToggle.SetIsOnWithoutNotify(ModPackage.Instance.loadedMod.AutoExport_ReplaceBaseHeroes);
        if (generatedScreen.ColumnRefs["Left_Column"].Toggles.TryGetValue("AutoHidePool", out var hToggle))
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