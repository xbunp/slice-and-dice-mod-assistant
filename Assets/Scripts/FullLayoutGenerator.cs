using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FullScreenUIGenerator : MonoBehaviour
{
    public Canvas canvas;

    [Header("UI Prefabs")]
    public GameObject inputFieldPrefab;
    public GameObject dropdownPrefab;
    public GameObject labelPrefab;
    public GameObject diceButtonPrefab;
    public GameObject buttonPrefab;
    public GameObject sliderPrefab;
    public GameObject scrollViewPrefab;
    public GameObject navigationTabs;
    public GameObject imagePanelPrefab;
    public GameObject PortraitPanel;
    public GameObject customImgPanel;

    [Header("Layout Settings")]
    public float rowHeight = 35f;
    public float rowSpacing = 8f;

    /// <summary>
    /// Instantiates the UI Wrapper and returns the screen container with the RootWrapper assigned.
    /// </summary>
    public GeneratedScreen CreateScreenWrapper(bool useMargins = true)
    {
        GeneratedScreen screen = new GeneratedScreen();

        if (canvas == null)
        {
            Debug.LogError("NO CANVAS");
            return null;
        }

        GameObject wrapper = CreateUIObject("UI_Wrapper", canvas.transform);
        RectTransform wrapperRt = wrapper.GetComponent<RectTransform>();

        screen.RootWrapper = wrapperRt;

        if (useMargins)
            SetAnchors(wrapperRt, 0.05f, 0.05f, 0.95f, 0.95f);
        else
            SetAnchors(wrapperRt, 0.0f, 0.0f, 1.0f, 1.0f);

        return screen;
    }

    /// <summary>
    /// Populates the already created screen wrapper with columns.
    /// </summary>
    public void PopulateScreen(GeneratedScreen screen, List<ColumnSpec> columns, bool useMargins = true)
    {
        if (screen == null || screen.RootWrapper == null)
        {
            Debug.LogError("Cannot populate screen: RootWrapper is null.");
            return;
        }

        foreach (var colSpec in columns)
        {
            RectTransform colPanel = CreateUIObject(colSpec.name, screen.RootWrapper.transform).GetComponent<RectTransform>();
            SetAnchors(colPanel, colSpec.anchorMinX, 0.0f, colSpec.anchorMaxX, 1.0f);

            screen.ColumnPanels[colSpec.name] = colPanel;

            if (colSpec.isCustomLayout)
            {
                screen.CustomPanels[colSpec.name] = colPanel;
            }
            else
            {
                GridReferences refs = BuildGrid(colPanel, colSpec.rows, useMargins);
                screen.ColumnRefs[colSpec.name] = refs;
            }
        }
    }

    /// <summary>
    /// Generates a complete Canvas containing three distinct columns from scratch.
    /// </summary>
    public GeneratedScreen SetupScreen(List<ColumnSpec> columns, bool useMargins = true)
    {
        GeneratedScreen screen = new GeneratedScreen();

        if (canvas == null)
        {
            Debug.LogError("NO CANVAS");
            return null;
        }

        GameObject wrapper = CreateUIObject("UI_Wrapper", canvas.transform);
        RectTransform wrapperRt = wrapper.GetComponent<RectTransform>();

        // FIX: Assign the wrapper directly here
        screen.RootWrapper = wrapperRt;

        if (useMargins)
            SetAnchors(wrapperRt, 0.05f, 0.05f, 0.95f, 0.95f);
        else
            SetAnchors(wrapperRt, 0.0f, 0.0f, 1.0f, 1.0f);

        foreach (var colSpec in columns)
        {
            RectTransform colPanel = CreateUIObject(colSpec.name, wrapper.transform).GetComponent<RectTransform>();
            SetAnchors(colPanel, colSpec.anchorMinX, 0.0f, colSpec.anchorMaxX, 1.0f);

            screen.ColumnPanels[colSpec.name] = colPanel;

            if (colSpec.isCustomLayout)
            {
                screen.CustomPanels[colSpec.name] = colPanel;
            }
            else
            {
                GridReferences refs = BuildGrid(colPanel, colSpec.rows, useMargins);
                screen.ColumnRefs[colSpec.name] = refs;
            }
        }

        return screen;
    }

    //==========================================================================================

    private GridReferences BuildGrid(RectTransform panel, List<GridRowSpec> rows, bool useMargins = true)
    {
        GridReferences refs = new GridReferences();
        float currentY = 0;
        float padding = useMargins ? 8f : 0f;

        RectTransform activeContainer = panel;
        float containerCurrentY = 0;
        int rowsRemainingInContainer = 0;

        if (rows == null) return refs;

        foreach (var rowSpec in rows)
        {
            if (rowSpec == null) continue;

            // Revert to main panel if we've filled the current container
            if (rowsRemainingInContainer <= 0 && activeContainer != panel)
            {
                activeContainer = panel;
            }

            if (rowSpec.isBackground)
            {
                float innerHeight = CalculateRowHeight(rowSpec);
                float outerHeight = innerHeight + (padding * 2);

                RectTransform rowRt = CreateAndSetupRow(activeContainer, currentY, outerHeight);

                float currentX = 0;
                if (rowSpec.cells != null && rowSpec.cells.Count > 0)
                {
                    var cell = rowSpec.cells[0];
                    if (cell != null)
                    {
                        GameObject cellObj = InstantiateCell(cell, rowRt, refs);
                        if (cellObj != null)
                        {
                            FinalizeCellLayoutAndText(cellObj, cell, ref currentX, useMargins);
                            RectTransform cellRt = cellObj.GetComponent<RectTransform>();
                            if (cellRt != null) activeContainer = cellRt;
                        }
                    }
                }

                containerCurrentY = -padding;
                rowsRemainingInContainer = rowSpec.rowSpan;
                currentY -= (outerHeight + rowSpacing);
                continue;
            }

            // Standard content row
            float activeHeight = CalculateRowHeight(rowSpec);
            float targetY = rowsRemainingInContainer > 0 ? containerCurrentY : currentY;
            RectTransform targetParent = rowsRemainingInContainer > 0 && activeContainer != null ? activeContainer : panel;

            RectTransform contentRowRt = CreateAndSetupRow(targetParent, targetY, activeHeight);

            if (rowsRemainingInContainer > 0 && contentRowRt != null)
            {
                contentRowRt.offsetMin = new Vector2(padding, contentRowRt.offsetMin.y);
                contentRowRt.offsetMax = new Vector2(-padding, contentRowRt.offsetMax.y);
            }

            float currentX2 = 0;
            if (rowSpec.cells != null)
            {
                foreach (var cell in rowSpec.cells)
                {
                    if (cell == null) continue;

                    GameObject cellObj = InstantiateCell(cell, contentRowRt, refs);
                    if (cellObj != null)
                    {
                        FinalizeCellLayoutAndText(cellObj, cell, ref currentX2, useMargins);
                    }
                }
            }

            if (rowsRemainingInContainer > 0)
            {
                containerCurrentY -= (activeHeight + rowSpacing);
                rowsRemainingInContainer--;
            }
            else
            {
                currentY -= (activeHeight + rowSpacing);
            }
        }

        refs.TotalHeight = Mathf.Abs(currentY);
        return refs;
    }

    private GameObject InstantiateCell(GridCellSpec cell, RectTransform rowRt, GridReferences refs)
    {
        if (cell == null) return null;
        GameObject cellObj = null;

        switch (cell.type)
        {
            case CellType.Input:
                if (inputFieldPrefab != null)
                {
                    cellObj = Instantiate(inputFieldPrefab, rowRt);
                    ConfigureInputCell(cell, cellObj, refs);
                }
                break;

            case CellType.ImagePanel:
                if (imagePanelPrefab != null)
                {
                    cellObj = Instantiate(imagePanelPrefab, rowRt);
                    ConfigureImagePanelCell(cell, cellObj, refs);
                }
                break;

            case CellType.Dropdown:
                if (dropdownPrefab != null)
                {
                    cellObj = Instantiate(dropdownPrefab, rowRt);
                    ConfigureDropdownCell(cell, cellObj, refs);
                }
                break;

            case CellType.Label:
                if (labelPrefab != null)
                {
                    cellObj = Instantiate(labelPrefab, rowRt);
                }
                break;

            case CellType.Button:
                if (buttonPrefab != null)
                {
                    cellObj = Instantiate(buttonPrefab, rowRt);
                    ConfigureButtonCell(cell, cellObj, refs);
                }
                break;

            case CellType.DiceButton:
                if (diceButtonPrefab != null)
                {
                    cellObj = Instantiate(diceButtonPrefab, rowRt);
                    ConfigureDiceButtonCell(cell, cellObj, refs);
                }
                break;

            case CellType.Slider:
                if (sliderPrefab != null)
                {
                    cellObj = Instantiate(sliderPrefab, rowRt);
                    ConfigureSliderCell(cell, cellObj, refs);
                }
                break;

            case CellType.ScrollView:
                if (scrollViewPrefab != null)
                {
                    cellObj = Instantiate(scrollViewPrefab, rowRt);
                    ConfigureScrollViewCell(cell, cellObj, refs);
                }
                break;

            case CellType.NavigationTabs:
                if (navigationTabs != null)
                {
                    cellObj = Instantiate(navigationTabs, rowRt);
                    ConfigureNavigationTabsCell(cell, cellObj, refs);
                }
                break;

            case CellType.PortraitPanel:
                if (PortraitPanel != null)
                {
                    cellObj = Instantiate(PortraitPanel, rowRt);
                    ConfigurePortraitPanelCell(cell, cellObj, refs);
                }
                break;
            case CellType.CustomImgImporter:
                if (customImgPanel != null)
                {
                    cellObj = Instantiate(customImgPanel, rowRt);
                    ConfigureCustomImgCell(cell, cellObj, refs);
                }
                break;


        }

        return cellObj;
    }

    private void ConfigurePortraitPanelCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        PortraitPreview preview = cellObj.GetComponent<PortraitPreview>();
        if (preview != null && !string.IsNullOrEmpty(cell.key))
        {
            refs.PortraitPanels[cell.key] = preview;
        }
    }

    private void ConfigureInputCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        TMP_InputField input = cellObj.GetComponentInChildren<TMP_InputField>();
        if (input != null)
        {
            if (!string.IsNullOrEmpty(cell.key)) refs.Inputs[cell.key] = input;
            if (cell.onStringChanged != null)
            {
                input.onValueChanged.AddListener((val) => cell.onStringChanged(val));
            }
        }
    }

    private void ConfigureImagePanelCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        Image img = cellObj.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.color = cell.panelColor;
            if (cell.panelSprite != null)
            {
                img.sprite = cell.panelSprite;
                img.type = Image.Type.Sliced;
            }
            if (!string.IsNullOrEmpty(cell.key)) refs.ImagePanels[cell.key] = img;
        }
    }

    private void ConfigureDropdownCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        TMP_Dropdown drop = cellObj.GetComponentInChildren<TMP_Dropdown>();
        if (drop != null)
        {
            if (!string.IsNullOrEmpty(cell.key)) refs.Dropdowns[cell.key] = drop;
            drop.ClearOptions();
            if (cell.dropdownOptions != null) drop.AddOptions(new List<string>(cell.dropdownOptions));

            if (cell.onIntChanged != null)
            {
                drop.onValueChanged.AddListener((val) => cell.onIntChanged(val));
            }
        }
    }

    private void ConfigureButtonCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        ImageButton imgBtn = cellObj.GetComponent<ImageButton>();
        Button btn = null;

        if (imgBtn != null)
        {
            btn = imgBtn.button;
            if (imgBtn.text != null)
            {
                if (cell.labelText != null)
                {
                    imgBtn.text.text = cell.labelText;
                    imgBtn.text.gameObject.SetActive(true);
                }
                else
                {
                    imgBtn.text.gameObject.SetActive(false);
                }
            }

            if (imgBtn.image != null)
            {
                if (cell.buttonSprite != null)
                {
                    imgBtn.image.sprite = cell.buttonSprite;
                    imgBtn.image.gameObject.SetActive(true);
                }
                else
                {
                    imgBtn.image.gameObject.SetActive(false);
                }
            }
            cell.labelText = null;
        }
        else
        {
            btn = cellObj.GetComponentInChildren<Button>();
        }

        if (btn != null)
        {
            if (!string.IsNullOrEmpty(cell.key)) refs.Buttons[cell.key] = btn;
            if (cell.onClicked != null)
            {
                btn.onClick.AddListener(() => cell.onClicked());
            }
        }
    }

    private void ConfigureDiceButtonCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        Button diceBtn = cellObj.GetComponentInChildren<Button>();
        if (diceBtn != null)
        {
            if (!string.IsNullOrEmpty(cell.key)) refs.Buttons[cell.key] = diceBtn;
            if (cell.onClicked != null)
            {
                diceBtn.onClick.AddListener(() => cell.onClicked());
            }
        }
    }

    private void ConfigureSliderCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        Slider slider = cellObj.GetComponentInChildren<Slider>();
        if (slider != null)
        {
            if (!string.IsNullOrEmpty(cell.key)) refs.Sliders[cell.key] = slider;
            slider.minValue = cell.sliderMin;
            slider.maxValue = cell.sliderMax;
            slider.wholeNumbers = cell.sliderWholeNumbers;

            if (cell.onFloatChanged != null)
            {
                slider.onValueChanged.AddListener((val) => cell.onFloatChanged(val));
            }
        }
    }

    private void ConfigureScrollViewCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        ScrollRect scrollRect = cellObj.GetComponentInChildren<ScrollRect>();
        if (scrollRect != null && !string.IsNullOrEmpty(cell.key))
        {
            refs.ScrollViews[cell.key] = scrollRect;
        }
    }

    private void ConfigureNavigationTabsCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        NavigationTabsController navTabs = cellObj.GetComponent<NavigationTabsController>();
        if (navTabs != null)
        {
            navTabs.Initialize(cell.tabNames, cell.tabTargetPanels, buttonPrefab, cell.onIntChanged);
            if (!string.IsNullOrEmpty(cell.key)) refs.NavigationTabs[cell.key] = navTabs;
        }
    }

    private void ConfigureCustomImgCell(GridCellSpec cell, GameObject cellObj, GridReferences refs)
    {
        ImageReceiver receiver = cellObj.GetComponentInChildren<ImageReceiver>();
        if (receiver != null && !string.IsNullOrEmpty(cell.key))
        {
            refs.CustomImgImporter[cell.key] = receiver;
        }
    }

    //==========================================================================================

    private void FinalizeCellLayoutAndText(GameObject cellObj, GridCellSpec cell, ref float currentX, bool useMargins = true)
    {
        var textComponents = cellObj.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in textComponents)
        {
            txt.enableAutoSizing = false;
            txt.fontSize = 13f;
        }

        var mainLabel = cellObj.GetComponentInChildren<TextMeshProUGUI>();
        if (mainLabel != null && !string.IsNullOrEmpty(cell.labelText))
        {
            mainLabel.text = cell.labelText;
        }

        RectTransform cellRt = cellObj.GetComponent<RectTransform>();
        if (cellRt != null)
        {
            cellRt.localScale = Vector3.one;
            cellRt.anchorMin = new Vector2(currentX, 0);
            cellRt.anchorMax = new Vector2(currentX + cell.widthRatio, 1);
            cellRt.pivot = new Vector2(0.5f, 0.5f);

            if (cell.type == CellType.ImagePanel || !useMargins)
            {
                cellRt.offsetMin = Vector2.zero;
                cellRt.offsetMax = Vector2.zero;
            }
            else
            {
                cellRt.offsetMin = new Vector2(4, 2);
                cellRt.offsetMax = new Vector2(-4, -2);
            }
        }

        currentX += cell.widthRatio;
    }

    private float CalculateRowHeight(GridRowSpec rowSpec)
    {
        return rowSpec.customHeight > 0
            ? rowSpec.customHeight
            : (rowHeight * rowSpec.rowSpan + rowSpacing * (rowSpec.rowSpan - 1));
    }

    private RectTransform CreateAndSetupRow(RectTransform panel, float currentY, float activeHeight)
    {
        GameObject rowObj = CreateUIObject("Row", panel);
        RectTransform rowRt = rowObj.GetComponent<RectTransform>();

        SetAnchors(rowRt, 0.0f, 1.0f, 1.0f, 1.0f); // Top stretch
        rowRt.anchoredPosition = new Vector2(0, currentY);
        rowRt.pivot = new Vector2(0.5f, 1.0f); // Force the row to pivot from its top edge
        rowRt.sizeDelta = new Vector2(0, activeHeight);

        return rowRt;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void SetAnchors(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public GridReferences RebuildGrid(RectTransform panel, List<GridRowSpec> rows, bool useMargins = true)
    {
        foreach (Transform child in panel) Destroy(child.gameObject);
        return BuildGrid(panel, rows, useMargins);
    }
}