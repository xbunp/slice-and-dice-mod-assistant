using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    private GridReferences _gridUI;

    protected override void BuildUIAndBind()
    {
        // 1. Calculate height dynamically from Canvas (identical to EntityUI)
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
            new ColumnSpec("RightColumn", 0.50f, 0.98f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
        _mainScrollRect = generatedScreen.ColumnRefs["LeftColumn"].ScrollViews["MainScrollView"];

        // 2. Apply dynamic layout constraints to stretch the ScrollView cleanly
        ApplyDynamicLayoutConstraints();

        // 3. Rebuild the grid inside the scroll view
        RebuildModBuilderUI();
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
            new GridRowSpec(
                GridCellSpec.CreateToggle("TglHumanReadable", "Human-Readable Format (Linebreaks)", 1.0f, (val) => _compiledModData.humanReadable = val)
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

        // 4. Calculate exact height and padding identically to EntityUI
        float extraHeight = 0f;
        var layoutGroup = _mainScrollRect.content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = _mainScrollRect.content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        // Explicitly set the sizeDelta based on the grid's TotalHeight
        _mainScrollRect.content.sizeDelta = new Vector2(0, _gridUI.TotalHeight + extraHeight);
        Canvas.ForceUpdateCanvases();

        // 5. Populate initial values
        if (_gridUI.Inputs.TryGetValue("FilenameInput", out TMP_InputField inpFilename))
            inpFilename.SetTextWithoutNotify(_compiledModData.modFileName);

        if (_gridUI.Toggles.TryGetValue("TglHumanReadable", out Toggle tglReadable))
            tglReadable.SetIsOnWithoutNotify(_compiledModData.humanReadable);

        if (_gridUI.Toggles.TryGetValue("TglClearMonster", out Toggle tglMonster))
            tglMonster.SetIsOnWithoutNotify(_compiledModData.clearMonsterPool);

        if (_gridUI.Toggles.TryGetValue("TglClearHero", out Toggle tglHero))
            tglHero.SetIsOnWithoutNotify(_compiledModData.clearHeroPool);

        if (_gridUI.Toggles.TryGetValue("TglClearItem", out Toggle tglItem))
            tglItem.SetIsOnWithoutNotify(_compiledModData.clearItemPool);
    }

    private void ApplyDynamicLayoutConstraints()
    {
        if (_mainScrollRect != null)
        {
            RectTransform scrollRt = _mainScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowRt, 10f, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }
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

    private void OnImportItemsClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("ItemImportInput", out TMP_InputField input))
        {
            _compiledModData.ImportItems(input.text);
        }
    }

    private void OnImportMonstersClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("MonsterImportInput", out TMP_InputField input))
        {
            _compiledModData.ImportMonsters(input.text);
        }
    }

    private void OnImportHeroesClicked()
    {
        if (_gridUI != null && _gridUI.Inputs.TryGetValue("HeroImportInput", out TMP_InputField input))
        {
            _compiledModData.ImportHeroes(input.text);
        }
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
        Debug.Log("Copied textmod to clipboard!");
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
            Debug.Log($"[Save] File saved to: {path}");
        }
#elif UNITY_WEBGL
        DownloadTextFileWebGL(fileName, _compiledModData.compiledMod);
#else
        _compiledModData.OutputMod(); 
#endif
    }
}