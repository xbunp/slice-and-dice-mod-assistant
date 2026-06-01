using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModFactory : RootUI
{
    private Button loadModButton;
    private TMP_Dropdown directiveDropdown;
    private ScrollRect directiveScrollView;
    private Button copyModButton;

    // List of dynamically loaded directives in dropdown
    private readonly Dictionary<string, string> directiveMapping = new Dictionary<string, string>
    {
        { "HeroPool", "Hero Pool" },
        { "MonsterPool", "Monster Pool" },
        { "ItemPool", "Item Pool" }
    };

    private List<DirectiveUI> directiveUIs = new List<DirectiveUI>();

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
        dropdownOptions.AddRange(directiveMapping.Values);

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
        RebuildDirectivesList();
    }

    private List<GridRowSpec> leftRows; // Retained to ensure local scope match

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

    private void OnLoadModClicked()
    {
        string clipboardText = GUIUtility.systemCopyBuffer; // Or your platform's clipboard fetcher
        if (string.IsNullOrWhiteSpace(clipboardText)) return;

        // 1. Load string into the underlying data model
        ModPackage.Instance.loadedMod.LoadFromText(clipboardText);

        // 2. Clear UI
        directiveUIs.Clear();

        // 3. Generate UI blocks based on the parsed data classes
        foreach (var directive in ModPackage.Instance.loadedMod.Directives)
        {
            System.Action removeAction = () => {
                ModPackage.Instance.loadedMod.Directives.Remove(directive);
                directiveUIs.RemoveAll(ui => ui.DataModel == directive);
                RebuildDirectivesList();
            };

            DirectiveUI newUI = directive switch
            {
                SliceDiceTextMod.HeroPoolData hp => new HeroPool(hp, uiGenerator, RebuildDirectivesList, removeAction),
                SliceDiceTextMod.MonsterPoolData mp => new MonsterPool(mp, uiGenerator, RebuildDirectivesList, removeAction),
                SliceDiceTextMod.ItemPoolData ip => new ItemPool(ip, uiGenerator, RebuildDirectivesList, removeAction),
                SliceDiceTextMod.RawDirectiveData rd => null, // TODO: Build a RawDirectiveUI to edit raw strings
                _ => null
            };

            if (newUI != null) directiveUIs.Add(newUI);
        }

        RebuildDirectivesList();
        Debug.Log("Mod Loaded successfully!");
    }
    private void OnCopyModClicked()
    {
        // 1. The data classes (HeroPoolData, etc.) are already updated via the InputField bindings.
        // 2. Export the mod string
        string outputMod = ModPackage.Instance.loadedMod.ExportToText();

        // 3. Copy to clipboard
        GUIUtility.systemCopyBuffer = outputMod;
        Debug.Log("Mod copied to clipboard:\n" + outputMod);
    }

    private void OnDirectiveDropdownChanged(int selectedIndex)
    {
        if (selectedIndex <= 0 || directiveDropdown == null) return;

        string selectedOption = directiveDropdown.options[selectedIndex].text;
        directiveDropdown.value = 0; // Reset dropdown selection visually

        string originalDirective = directiveMapping.FirstOrDefault(x => x.Value == selectedOption).Key;
        if (string.IsNullOrEmpty(originalDirective)) return;

        // 1. Create the new raw Data Model object
        SliceDiceTextMod.ModDirectiveData newData = originalDirective switch
        {
            "HeroPool" => new SliceDiceTextMod.HeroPoolData(),
            "MonsterPool" => new SliceDiceTextMod.MonsterPoolData(),
            "ItemPool" => new SliceDiceTextMod.ItemPoolData(),
            _ => null
        };

        if (newData == null) return;

        // 2. Add the data model directly to the Singleton's source of truth
        ModPackage.Instance.loadedMod.Directives.Add(newData);

        // 3. Define the UI reference and set up the removal routine to sync both layers
        DirectiveUI newDirective = null;
        System.Action removeAction = () =>
        {
            ModPackage.Instance.loadedMod.Directives.Remove(newData);
            directiveUIs.Remove(newDirective);
            RebuildDirectivesList();
        };

        // 4. Instantiate the corresponding UI block, passing the freshly created data model
        switch (originalDirective)
        {
            case "HeroPool":
                newDirective = new HeroPool((SliceDiceTextMod.HeroPoolData)newData, uiGenerator, RebuildDirectivesList, removeAction);
                break;
            case "MonsterPool":
                newDirective = new MonsterPool((SliceDiceTextMod.MonsterPoolData)newData, uiGenerator, RebuildDirectivesList, removeAction);
                break;
            case "ItemPool":
                newDirective = new ItemPool((SliceDiceTextMod.ItemPoolData)newData, uiGenerator, RebuildDirectivesList, removeAction);
                break;
        }

        // 5. Add to the active UI array and rebuild the list view
        if (newDirective != null)
        {
            directiveUIs.Add(newDirective);
            RebuildDirectivesList();
        }
    }
}