using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SliceDiceTextMod;

public class ModFactory : RootUI
{
    private Button loadModButton;
    private TMP_Dropdown directiveDropdown;
    private ScrollRect directiveScrollView;
    private Button copyModButton;
    private List<DirectiveUI> directiveUIs = new List<DirectiveUI>();
    private List<GridRowSpec> leftRows;

    private void Awake()
    {
        InitializeBlankMod();
    }
    private void OnDestroy()
    {
        if (ModPackage.Instance != null && ModPackage.Instance.loadedMod != null)
        {
            ModPackage.Instance.loadedMod.OnDataChanged -= HandleExternalModChanged;
        }
    }

    private void InitializeBlankMod()
    {
        if (ModPackage.Instance != null)
        {
            if (ModPackage.Instance.loadedMod == null)
            {
                ModPackage.Instance.loadedMod = new ModDataContainer();
            }
            else
            {
                ModPackage.Instance.loadedMod.Directives.Clear();
            }

            ModPackage.Instance.loadedMod.OnDataChanged -= HandleExternalModChanged;
            ModPackage.Instance.loadedMod.OnDataChanged += HandleExternalModChanged;

            if (ModPackage.Instance.loadedMod.Directives == null)
            {
                ModPackage.Instance.loadedMod.Directives = new List<SliceDiceTextMod.ModDirectiveData>();
            }
        }

        directiveUIs.Clear();
    }
    private void BuildDropdownOptions()
    {
        List<string> dropdownOptions = new List<string> { "Add Directive..." };
        dropdownOptions.AddRange(SliceDiceTextMod.DirectiveRegistry.Entries.Select(e => e.DropdownName));
    }
    protected override void BuildUIAndBind()
    {
        bool useMargins = false;
        generatedScreen = uiGenerator.CreateScreenWrapper(useMargins);

        if (generatedScreen == null || generatedScreen.RootWrapper == null) return;

        Canvas.ForceUpdateCanvases();

        float totalHeight = generatedScreen.RootWrapper.rect.height;
        float spacing = uiGenerator.rowSpacing;
        float buttonRowHeight = uiGenerator.rowHeight;

        float rightScrollViewHeight = totalHeight - buttonRowHeight - spacing;

        List<string> dropdownOptions = new List<string> { "Add Directive..." };
        dropdownOptions.AddRange(SliceDiceTextMod.DirectiveRegistry.Entries.Select(e => e.DropdownName));

        List<ColumnSpec> columns = new List<ColumnSpec>();

        leftRows = new List<GridRowSpec>
        {
            new GridRowSpec(buttonRowHeight, GridCellSpec.CreateButton(
                "LoadModBtn",
                "Load Mod from Clipboard",
                1.0f,
                OnLoadModClicked
            )),
            new GridRowSpec(buttonRowHeight, GridCellSpec.CreateDropdown(
                "ModDropdown",
                "",
                1.0f,
                dropdownOptions.ToArray(),
                OnDirectiveDropdownChanged
            ))
        };
        columns.Add(new ColumnSpec("Left_Column", 0.0f, 0.49f, leftRows));

        List<GridRowSpec> rightRows = new List<GridRowSpec>
        {
            new GridRowSpec(rightScrollViewHeight, GridCellSpec.CreateScrollView("ModScrollView", 1.0f)),
            new GridRowSpec(buttonRowHeight, GridCellSpec.CreateButton(
                "CopyModBtn",
                "Copy Mod to Clipboard",
                1.0f,
                OnCopyModClicked
            ))
        };
        columns.Add(new ColumnSpec("Right_Column", 0.51f, 1.0f, rightRows));

        uiGenerator.PopulateScreen(generatedScreen, columns, useMargins);

        ExtractReferences();
        RebuildDirectiveUIsFromModel();
    }
    private void ExtractReferences()
    {
        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out var leftRefs))
        {
            leftRefs.Buttons.TryGetValue("LoadModBtn", out loadModButton);
            leftRefs.Dropdowns.TryGetValue("ModDropdown", out directiveDropdown);
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out var rightRefs))
        {
            rightRefs.ScrollViews.TryGetValue("ModScrollView", out directiveScrollView);
            rightRefs.Buttons.TryGetValue("CopyModBtn", out copyModButton);
        }
    }
    private void RebuildDirectivesList()
    {
        if (directiveScrollView == null || directiveScrollView.content == null) return;

        // Suppress notifications during the rebuild/restore phase
        var mod = ModPackage.Instance?.loadedMod;
        if (mod != null) mod.SuppressNotifications = true;

        try
        {
            List<GridRowSpec> masterRows = new List<GridRowSpec>();

            foreach (var directive in directiveUIs)
            {
                masterRows.AddRange(directive.GetRowSpecs());
            }

            GridReferences refs = uiGenerator.RebuildGrid(directiveScrollView.content, masterRows, true);
            directiveScrollView.content.sizeDelta = new Vector2(0f, refs.TotalHeight);

            foreach (var directive in directiveUIs)
            {
                directive.RestoreState(refs);
            }
        }
        finally
        {
            // Re-enable notifications safely
            if (mod != null) mod.SuppressNotifications = false;
        }
    }
    private void OnLoadModClicked()
    {
        string clipboardText = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboardText)) return;

        ModPackage.Instance.loadedMod.LoadFromText(clipboardText);
        directiveUIs.Clear();

        foreach (var directive in ModPackage.Instance.loadedMod.Directives)
        {
            System.Action removeAction = () => {
                ModPackage.Instance.loadedMod.Directives.Remove(directive);
                directiveUIs.RemoveAll(ui => ui.DataModel == directive);
                RebuildDirectivesList();
                ModPackage.Instance.loadedMod.NotifyDataChanged(this);
            };

            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());

            DirectiveUI newUI = null;
            if (entry != null) newUI = entry.CreateUI(directive, uiGenerator, RebuildDirectivesList, removeAction);

            if (newUI != null) directiveUIs.Add(newUI);
        }

        RebuildDirectivesList();
        ModPackage.Instance.loadedMod.NotifyDataChanged(this);
    }
    private void OnDirectiveDropdownChanged(int selectedIndex)
    {
        if (selectedIndex <= 0 || directiveDropdown == null) return;
        string selectedOption = directiveDropdown.options[selectedIndex].text;
        directiveDropdown.value = 0;

        var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => e.DropdownName == selectedOption);
        if (entry == null) return;

        var newData = entry.CreateData();
        ModPackage.Instance.loadedMod.Directives.Add(newData);

        DirectiveUI newDirective = null;
        System.Action removeAction = () =>
        {
            ModPackage.Instance.loadedMod.Directives.Remove(newData);
            directiveUIs.Remove(newDirective);
            RebuildDirectivesList();
            ModPackage.Instance.loadedMod.NotifyDataChanged(this);
        };

        newDirective = entry.CreateUI(newData, uiGenerator, RebuildDirectivesList, removeAction);

        if (newDirective != null)
        {
            directiveUIs.Add(newDirective);
            RebuildDirectivesList();
            ModPackage.Instance.loadedMod.NotifyDataChanged(this);
        }
    }

    private void HandleExternalModChanged(object sender)
    {
        // If the sender is this view or one of our internal Directives UI, skip
        // so we don't destroy and recreate the UI layout recursively.
        if (object.ReferenceEquals(sender, this) || sender is DirectiveUI) return;

        directiveUIs.Clear();
        foreach (var directive in ModPackage.Instance.loadedMod.Directives)
        {
            System.Action removeAction = () => {
                ModPackage.Instance.loadedMod.Directives.Remove(directive);
                directiveUIs.RemoveAll(ui => ui.DataModel == directive);
                RebuildDirectivesList();
                ModPackage.Instance.loadedMod.NotifyDataChanged(this);
            };

            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());

            DirectiveUI newUI = null;
            if (entry != null) newUI = entry.CreateUI(directive, uiGenerator, RebuildDirectivesList, removeAction);

            if (newUI != null) directiveUIs.Add(newUI);
        }

        RebuildDirectivesList();
    }
    private void RebuildDirectiveUIsFromModel()
    {
        directiveUIs.Clear();
        if (ModPackage.Instance?.loadedMod?.Directives == null) return;

        foreach (var directive in ModPackage.Instance.loadedMod.Directives)
        {
            System.Action removeAction = () => {
                ModPackage.Instance.loadedMod.Directives.Remove(directive);
                directiveUIs.RemoveAll(ui => ui.DataModel == directive);
                RebuildDirectivesList();
                ModPackage.Instance.loadedMod.NotifyDataChanged(this);
            };

            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(
                e => directive.GetType() == e.CreateData().GetType()
            );

            DirectiveUI newUI = null;
            if (entry != null)
            {
                newUI = entry.CreateUI(directive, uiGenerator, RebuildDirectivesList, removeAction);
            }

            if (newUI != null)
            {
                directiveUIs.Add(newUI);
            }
        }

        RebuildDirectivesList();
    }
    private void OnCopyModClicked()
    {
        if (ModPackage.Instance?.loadedMod == null) return;

        string outputMod = ModPackage.Instance.loadedMod.ExportToText();
        GUIUtility.systemCopyBuffer = outputMod;
        Debug.Log("Mod copied to clipboard:\n" + outputMod);
    }
}