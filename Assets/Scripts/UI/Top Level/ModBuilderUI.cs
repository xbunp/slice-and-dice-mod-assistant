using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ModBuilderUI : RootUI
{
    private CompiledModData _compiledModData = new CompiledModData();
    private ScrollRect _mainScrollRect;
    private GridReferences _gridUI;

    protected override void BuildUIAndBind()
    {
        var layoutRows = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("LblModConfigHeader", "<b>MOD CONFIGURATION</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblFilename", "Output Filename:", 0.35f),
                GridCellSpec.CreateInput("FilenameInput", "", 0.65f, (val) => _compiledModData.modFileName = val)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer1", "", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblClearFlagsHeader", "<b>POOL CLEAR FLAGS</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateToggle("TglClearMonster", "Clear Monster Pool", 1.0f, (val) => _compiledModData.clearMonsterPool = val)
            ),
            new GridRowSpec(
                GridCellSpec.CreateToggle("TglClearHero", "Clear Hero Pool", 1.0f, (val) => _compiledModData.clearHeroPool = val)
            ),
            new GridRowSpec(
                GridCellSpec.CreateToggle("TglClearItem", "Clear Item Pool", 1.0f, (val) => _compiledModData.clearItemPool = val)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer2", "", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblHeroImportHeader", "<b>HERO IMPORT</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateInput("HeroImportInput", "", 0.70f, null),
                GridCellSpec.CreateButton("BtnImportHeroes", "Import Heroes", 0.30f, OnImportHeroesClicked)
            )
        };

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftColumn", 0.05f, 0.50f, new List<GridRowSpec>
            {
                new GridRowSpec(600f, GridCellSpec.CreateScrollView("MainScrollView", 1.0f))
            }),
            new ColumnSpec("RightColumn", 0.52f, 0.95f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
        _mainScrollRect = generatedScreen.ColumnRefs["LeftColumn"].ScrollViews["MainScrollView"];
        _gridUI = uiGenerator.RebuildGrid(_mainScrollRect.content, layoutRows);

        // Set initial filename input
        if (_gridUI.Inputs.TryGetValue("FilenameInput", out TMP_InputField inpFilename))
            inpFilename.SetTextWithoutNotify(_compiledModData.modFileName);

        // Set initial toggle states based on CompiledModData defaults
        if (_gridUI.Toggles.TryGetValue("TglClearMonster", out Toggle tglMonster))
            tglMonster.SetIsOnWithoutNotify(_compiledModData.clearMonsterPool);

        if (_gridUI.Toggles.TryGetValue("TglClearHero", out Toggle tglHero))
            tglHero.SetIsOnWithoutNotify(_compiledModData.clearHeroPool);

        if (_gridUI.Toggles.TryGetValue("TglClearItem", out Toggle tglItem))
            tglItem.SetIsOnWithoutNotify(_compiledModData.clearItemPool);
    }

    private void OnImportHeroesClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("HeroImportInput", out TMP_InputField input))
        {
            string heroString = input.text;
            _compiledModData.ImportHeroes(heroString);
        }
    }
}