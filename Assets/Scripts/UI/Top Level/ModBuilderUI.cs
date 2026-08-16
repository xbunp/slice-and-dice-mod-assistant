using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

public class ModBuilderUI : RootUI
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DownloadTextFileWebGL(string filename, string content);

    [DllImport("__Internal")]
    private static extern void CopyToClipboardWebGL(string text);
#endif

    private CompiledModData _compiledModData = new CompiledModData();
    private ScrollRect _mainScrollRect;
    private ScrollRect _rightScrollRect;
    private GridReferences _gridUI;
    private GridReferences _rightGridUI;

    private string _tempMonsterStr = "";
    private int _tempMultiplier = 1;

    protected override void BuildUIAndBind()
    {
        float canvasHeight = 900f;
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null) canvasHeight = canvasRt.rect.height;
        }

        float calculatedScrollHeight = Mathf.Max(canvasHeight - 60f, 400f);

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftColumn", 0.02f, 0.48f, new List<GridRowSpec>
            {
                new GridRowSpec(calculatedScrollHeight, GridCellSpec.CreateScrollView("MainScrollView", 1.0f))
            }),
            new ColumnSpec("RightColumn", 0.50f, 0.98f, new List<GridRowSpec>
            {
                new GridRowSpec(calculatedScrollHeight, GridCellSpec.CreateScrollView("RightScrollView", 1.0f))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
        _mainScrollRect = generatedScreen.ColumnRefs["LeftColumn"].ScrollViews["MainScrollView"];
        _rightScrollRect = generatedScreen.ColumnRefs["RightColumn"].ScrollViews["RightScrollView"];

        ApplyDynamicLayoutConstraints(_mainScrollRect);
        ApplyDynamicLayoutConstraints(_rightScrollRect);

        RebuildModBuilderUI();
        RebuildRightColumnUI();
    }

    private void RebuildModBuilderUI()
    {
        if (_mainScrollRect == null) return;

        var layoutRows = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("LblModConfigHeader", "<b>MOD CONFIGURATION</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblFilename", "Output Filename:", 0.35f),
                GridCellSpec.CreateInput("FilenameInput", "", 0.65f, (val) => _compiledModData.modFileName = val)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer1", "", 1.0f)),

            new GridRowSpec(GridCellSpec.CreateLabel("LblClearFlagsHeader", "<b>POOL CLEAR FLAGS</b>", 1.0f)),
            new GridRowSpec(GridCellSpec.CreateToggle("TglClearMonster", "Clear Monster Pool", 1.0f, (val) => _compiledModData.clearMonsterPool = val)),
            new GridRowSpec(GridCellSpec.CreateToggle("TglClearHero", "Clear Hero Pool", 1.0f, (val) => _compiledModData.clearHeroPool = val)),
            new GridRowSpec(GridCellSpec.CreateToggle("TglClearItem", "Clear Item Pool", 1.0f, (val) => _compiledModData.clearItemPool = val)),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer2", "", 1.0f)),

            new GridRowSpec(GridCellSpec.CreateLabel("LblItemImportHeader", "<b>ITEM IMPORT</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateInput("ItemImportInput", "", 0.68f, null),
                GridCellSpec.CreateButton("BtnImportItems", "Import Items", 0.32f, OnImportItemsClicked)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer3", "", 1.0f)),

            new GridRowSpec(GridCellSpec.CreateLabel("LblMonsterImportHeader", "<b>MONSTER IMPORT</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateInput("MonsterImportInput", "", 0.68f, null),
                GridCellSpec.CreateButton("BtnImportMonsters", "Import Monsters", 0.32f, OnImportMonstersClicked)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer4", "", 1.0f)),

            new GridRowSpec(GridCellSpec.CreateLabel("LblHeroImportHeader", "<b>HERO IMPORT</b>", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateInput("HeroImportInput", "", 0.68f, null),
                GridCellSpec.CreateButton("BtnImportHeroes", "Import Heroes", 0.32f, OnImportHeroesClicked)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("Spacer5", "", 1.0f)),

            new GridRowSpec(
                GridCellSpec.CreateButton("BtnCopyMod", "Copy to Clipboard", 0.50f, OnCopyClicked),
                GridCellSpec.CreateButton("BtnSaveMod", "Save File...", 0.50f, OnSaveClicked)
            )
        };

        _gridUI = uiGenerator.RebuildGrid(_mainScrollRect.content, layoutRows);
        FitContentSize(_mainScrollRect, _gridUI);

        if (_gridUI.Inputs.TryGetValue("FilenameInput", out TMP_InputField inpFilename))
            inpFilename.SetTextWithoutNotify(_compiledModData.modFileName);
        if (_gridUI.Toggles.TryGetValue("TglClearMonster", out Toggle tglMonster))
            tglMonster.SetIsOnWithoutNotify(_compiledModData.clearMonsterPool);
        if (_gridUI.Toggles.TryGetValue("TglClearHero", out Toggle tglHero))
            tglHero.SetIsOnWithoutNotify(_compiledModData.clearHeroPool);
        if (_gridUI.Toggles.TryGetValue("TglClearItem", out Toggle tglItem))
            tglItem.SetIsOnWithoutNotify(_compiledModData.clearItemPool);
    }

    private void RebuildRightColumnUI()
    {
        if (_rightScrollRect == null) return;

        var activeFight = _compiledModData.GetActiveFight();
        var layoutRows = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("LblFightHeader", "<b>DEFINED FIGHTS POOL</b>", 1.0f))
        };

        // 1. Fight Tabs
        var tabCells = new List<GridCellSpec>();
        float tabWidth = 1.0f / Mathf.Max(1, _compiledModData.fights.Count + 1);

        for (int f = 0; f < _compiledModData.fights.Count; f++)
        {
            int fIndex = f;
            string tabName = string.IsNullOrEmpty(_compiledModData.fights[f].bossLabel) ? $"Fight {f + 1}" : _compiledModData.fights[f].bossLabel;
            if (fIndex == _compiledModData.selectedFightIndex) tabName = $"<b>[{tabName}]</b>";

            tabCells.Add(GridCellSpec.CreateButton($"TabFight_{fIndex}", tabName, tabWidth, () => {
                _compiledModData.selectedFightIndex = fIndex;
                RebuildRightColumnUI();
            }));
        }

        tabCells.Add(GridCellSpec.CreateButton("BtnNewFight", "+ New", tabWidth, () => {
            _compiledModData.AddNewFight();
            RebuildRightColumnUI();
        }));
        layoutRows.Add(new GridRowSpec(tabCells.ToArray()));

        // 2. Action Buttons (IMPORT RESTORED)
        layoutRows.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnImportFight", "Import Fight from Clipboard", 0.65f, OnImportFightFromClipboard),
            GridCellSpec.CreateButton("BtnDelFight", "Delete Fight", 0.35f, OnDeleteActiveFight)
        ));
        layoutRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spc0", "", 1.0f)));

        // 3. Global Pool Info
        layoutRows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("LblFloor", "Fight/Floor #:", 0.25f),
            GridCellSpec.CreateInput("InpFloor", _compiledModData.floorNumber, 0.25f, (v) => { _compiledModData.floorNumber = v; _compiledModData.Compile(); }),
            GridCellSpec.CreateLabel("LblPool", "Pool #:", 0.25f),
            GridCellSpec.CreateInput("InpPool", _compiledModData.bossPoolNumber, 0.25f, (v) => { _compiledModData.bossPoolNumber = v; _compiledModData.Compile(); })
        ));

        layoutRows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("LblBossLabel", "Boss Label:", 0.35f),
            GridCellSpec.CreateInput("InpBossLabel", activeFight.bossLabel, 0.65f, (v) => { activeFight.bossLabel = v; _compiledModData.Compile(); })
        ));
        layoutRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spc1", "", 1.0f)));

        // 4. Add Entity Inputs
        layoutRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblAdd", "<b>ADD MONSTER TO FIGHT</b>", 1.0f)));
        layoutRows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("LblMon", "Code:", 0.2f),
            GridCellSpec.CreateInput("InpMonStr", _tempMonsterStr, 0.8f, (v) => _tempMonsterStr = v)
        ));
        layoutRows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("LblMult", "Amount (x):", 0.35f),
            GridCellSpec.CreateInput("InpMult", _tempMultiplier.ToString(), 0.25f, (v) => { if (int.TryParse(v, out int m)) _tempMultiplier = m; }),
            GridCellSpec.CreateButton("BtnAddMon", "Add Monster", 0.40f, OnAddMonsterToFight)
        ));
        layoutRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spc2", "", 1.0f)));

        // 5. Encounter Order
        layoutRows.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblList", $"<b>ENCOUNTER SEQUENCE ({activeFight.entities.Count})</b> [Top = 1st, Bot = Last]", 1.0f)));

        for (int i = 0; i < activeFight.entities.Count; i++)
        {
            int idx = i;
            string preview = activeFight.entities[i].MonsterString.Trim();
            if (preview.Length > 24) preview = preview.Substring(0, 21) + "...";

            layoutRows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"Ent_{idx}", $"{idx + 1}. {preview} <b>(x{activeFight.entities[i].Multiplier})</b>", 0.55f),
                GridCellSpec.CreateButton($"BtnUp_{idx}", "↑", 0.15f, () => MoveEntity(idx, -1)),
                GridCellSpec.CreateButton($"BtnDn_{idx}", "↓", 0.15f, () => MoveEntity(idx, 1)),
                GridCellSpec.CreateButton($"BtnDel_{idx}", "X", 0.15f, () => DeleteEntity(idx))
            ));
        }

        _rightGridUI = uiGenerator.RebuildGrid(_rightScrollRect.content, layoutRows);
        FitContentSize(_rightScrollRect, _rightGridUI);

        if (_rightGridUI.Inputs.TryGetValue("InpFloor", out var fl)) fl.SetTextWithoutNotify(_compiledModData.floorNumber);
        if (_rightGridUI.Inputs.TryGetValue("InpPool", out var pl)) pl.SetTextWithoutNotify(_compiledModData.bossPoolNumber);
        if (_rightGridUI.Inputs.TryGetValue("InpBossLabel", out var bl)) bl.SetTextWithoutNotify(activeFight.bossLabel);
        if (_rightGridUI.Inputs.TryGetValue("InpMonStr", out var mon)) mon.SetTextWithoutNotify(_tempMonsterStr);
        if (_rightGridUI.Inputs.TryGetValue("InpMult", out var mult)) mult.SetTextWithoutNotify(_tempMultiplier.ToString());
    }

    private void ApplyDynamicLayoutConstraints(ScrollRect scrollRect)
    {
        if (scrollRect != null)
        {
            RectTransform scrollRt = scrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowRt, 10f, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }
    }

    private void FitContentSize(ScrollRect targetRect, GridReferences gridData)
    {
        float extraHeight = 0f;
        var layoutGroup = targetRect.content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = targetRect.content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        targetRect.content.sizeDelta = new Vector2(0, gridData.TotalHeight + extraHeight);
        Canvas.ForceUpdateCanvases();
    }

    private void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;
        var layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = target.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1f;
    }

    private void StretchToParent(RectTransform rt, float topOffset, float bottomOffset)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, bottomOffset);
        rt.offsetMax = new Vector2(0f, -topOffset);
    }

    private void OnAddMonsterToFight()
    {
        if (string.IsNullOrWhiteSpace(_tempMonsterStr)) return;
        var activeFight = _compiledModData.GetActiveFight();
        activeFight.entities.Add(new FightEntity
        {
            MonsterString = _tempMonsterStr.Trim(),
            Multiplier = Mathf.Max(1, _tempMultiplier)
        });

        _tempMonsterStr = "";
        _tempMultiplier = 1;
        _compiledModData.Compile();
        RebuildRightColumnUI();
    }

    private void MoveEntity(int index, int direction)
    {
        var activeFight = _compiledModData.GetActiveFight();
        int newIdx = index + direction;
        if (newIdx < 0 || newIdx >= activeFight.entities.Count) return;

        var temp = activeFight.entities[index];
        activeFight.entities[index] = activeFight.entities[newIdx];
        activeFight.entities[newIdx] = temp;

        _compiledModData.Compile();
        RebuildRightColumnUI();
    }

    private void DeleteEntity(int index)
    {
        var activeFight = _compiledModData.GetActiveFight();
        if (index >= 0 && index < activeFight.entities.Count)
        {
            activeFight.entities.RemoveAt(index);
            _compiledModData.Compile();
            RebuildRightColumnUI();
        }
    }

    private void OnDeleteActiveFight()
    {
        if (_compiledModData.fights.Count <= 1)
        {
            _compiledModData.fights[0] = new FightData { bossLabel = "Boss 1" };
        }
        else
        {
            _compiledModData.fights.RemoveAt(_compiledModData.selectedFightIndex);
            if (_compiledModData.selectedFightIndex >= _compiledModData.fights.Count)
            {
                _compiledModData.selectedFightIndex = _compiledModData.fights.Count - 1;
            }
        }
        _compiledModData.Compile();
        RebuildRightColumnUI();
    }

    private void OnImportFightFromClipboard()
    {
        string clipText = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipText)) return;

        if (_compiledModData.UnpackAndAddFight(clipText))
        {
            _compiledModData.selectedFightIndex = _compiledModData.fights.Count - 1;
            _compiledModData.Compile();
            RebuildRightColumnUI();
        }
    }

    private void OnImportItemsClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("ItemImportInput", out TMP_InputField input))
            _compiledModData.ImportItems(input.text);
    }

    private void OnImportMonstersClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("MonsterImportInput", out TMP_InputField input))
            _compiledModData.ImportMonsters(input.text);
    }

    private void OnImportHeroesClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("HeroImportInput", out TMP_InputField input))
            _compiledModData.ImportHeroes(input.text);
    }

    private void OnCopyClicked()
    {
        _compiledModData.Compile();
        string textToCopy = _compiledModData.compiledMod;

#if UNITY_WEBGL && !UNITY_EDITOR
        CopyToClipboardWebGL(textToCopy);
#else
        GUIUtility.systemCopyBuffer = textToCopy;
#endif
        Debug.Log("Copied full TextMod with defined fights to clipboard!");
    }

    private void OnSaveClicked()
    {
        _compiledModData.Compile();
        string fileName = _compiledModData.modFileName;
        if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) fileName += ".txt";

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.SaveFilePanel("Save TextMod", "", fileName, "txt");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, _compiledModData.compiledMod);
            UnityEditor.AssetDatabase.Refresh();
        }
#elif UNITY_WEBGL
        DownloadTextFileWebGL(fileName, _compiledModData.compiledMod);
#else
        _compiledModData.OutputMod(); 
#endif
    }
}