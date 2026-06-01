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
    private void OnDirectiveDropdownChanged(int selectedIndex)
    {
        if (selectedIndex <= 0 || directiveDropdown == null) return;

        string selectedOption = directiveDropdown.options[selectedIndex].text;
        directiveDropdown.value = 0;

        string originalDirective = directiveMapping.FirstOrDefault(x => x.Value == selectedOption).Key;

        DirectiveUI newDirective = null;
        System.Action removeAction = () =>
        {
            directiveUIs.Remove(newDirective);
            RebuildDirectivesList();
        };

        // Switch on the 'originalDirective' variable instead of 'selectedOption'
        switch (originalDirective)
        {
            case "HeroPool":
                newDirective = new HeroPool(uiGenerator, RebuildDirectivesList, removeAction);
                break;
            case "MonsterPool":
                newDirective = new MonsterPool(uiGenerator, RebuildDirectivesList, removeAction);
                break;
            case "ItemPool":
                newDirective = new ItemPool(uiGenerator, RebuildDirectivesList, removeAction);
                break;
        }

        if (newDirective != null)
        {
            directiveUIs.Add(newDirective);
            RebuildDirectivesList();
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

    private void OnLoadModClicked() { }
    private void OnCopyModClicked() { }
}