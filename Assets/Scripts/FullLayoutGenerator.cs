using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FullScreenUIGenerator : MonoBehaviour
{
    [Header("UI Prefabs")]
    public GameObject inputFieldPrefab;
    public GameObject dropdownPrefab;
    public GameObject labelPrefab;
    public GameObject diceButtonPrefab;
    public GameObject buttonPrefab;
    public GameObject sliderPrefab;

    [Header("Layout Settings")]
    public float rowHeight = 35f;
    public float rowSpacing = 8f;

    /// <summary>
    /// Generates a complete Canvas containing three distinct columns from scratch.
    /// </summary>
    public GeneratedScreen SetupScreen(List<ColumnSpec> columns)
    {
        GeneratedScreen screen = new GeneratedScreen();

        // 1. Get or Create Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        // 2. Screen Wrapper (removes edge bleeding)
        GameObject wrapper = CreateUIObject("UI_Wrapper", canvas.transform);
        RectTransform wrapperRt = wrapper.GetComponent<RectTransform>();
        SetAnchors(wrapperRt, 0.05f, 0.05f, 0.95f, 0.95f);

        // 3. Process Columns
        foreach (var colSpec in columns)
        {
            RectTransform colPanel = CreateUIObject(colSpec.name, wrapper.transform).GetComponent<RectTransform>();
            SetAnchors(colPanel, colSpec.anchorMinX, 0.0f, colSpec.anchorMaxX, 1.0f);

            screen.ColumnPanels[colSpec.name] = colPanel;

            if (colSpec.isCustomLayout)
            {
                // Register custom layout panel targets
                screen.CustomPanels[colSpec.name] = colPanel;
            }
            else
            {
                // Process mathematical grids
                GridReferences refs = BuildGrid(colPanel, colSpec.rows);
                screen.ColumnRefs[colSpec.name] = refs;
            }
        }

        return screen;
    }

    private GridReferences BuildGrid(RectTransform panel, List<GridRowSpec> rows)
    {
        GridReferences refs = new GridReferences();
        float currentY = 0;

        foreach (var rowSpec in rows)
        {
            GameObject rowObj = CreateUIObject("Row", panel);
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            SetAnchors(rowRt, 0.0f, 1.0f, 1.0f, 1.0f); // Top stretch
            rowRt.anchoredPosition = new Vector2(0, currentY);
            rowRt.sizeDelta = new Vector2(0, rowHeight);

            float currentX = 0;

            foreach (var cell in rowSpec.cells)
            {
                GameObject cellObj = null;

                switch (cell.type)
                {
                    case CellType.Input:
                        cellObj = Instantiate(inputFieldPrefab, rowRt);
                        TMP_InputField input = cellObj.GetComponentInChildren<TMP_InputField>();
                        refs.Inputs[cell.key] = input;

                        if (cell.onStringChanged != null)
                            input.onValueChanged.AddListener((val) => cell.onStringChanged(val));
                        break;

                    case CellType.Dropdown:
                        cellObj = Instantiate(dropdownPrefab, rowRt);
                        TMP_Dropdown drop = cellObj.GetComponentInChildren<TMP_Dropdown>();
                        refs.Dropdowns[cell.key] = drop;
                        drop.ClearOptions();
                        drop.AddOptions(new List<string>(cell.dropdownOptions));

                        if (cell.onIntChanged != null)
                            drop.onValueChanged.AddListener((val) => cell.onIntChanged(val));
                        break;

                    case CellType.Label:
                        cellObj = Instantiate(labelPrefab, rowRt);
                        break;

                    case CellType.Button:
                        cellObj = Instantiate(buttonPrefab, rowRt);
                        Button btn = cellObj.GetComponentInChildren<Button>();
                        refs.Buttons[cell.key] = btn;

                        if (cell.onClicked != null)
                            btn.onClick.AddListener(() => cell.onClicked());
                        break;

                    case CellType.DiceButton:
                        cellObj = Instantiate(diceButtonPrefab, rowRt);
                        Button diceBtn = cellObj.GetComponentInChildren<Button>();
                        refs.Buttons[cell.key] = diceBtn;

                        if (cell.onClicked != null)
                            diceBtn.onClick.AddListener(() => cell.onClicked());
                        break;

                    case CellType.Slider:
                        cellObj = Instantiate(sliderPrefab, rowRt);
                        Slider slider = cellObj.GetComponentInChildren<Slider>();
                        refs.Sliders[cell.key] = slider;

                        slider.minValue = cell.sliderMin;
                        slider.maxValue = cell.sliderMax;
                        slider.wholeNumbers = cell.sliderWholeNumbers;

                        if (cell.onFloatChanged != null)
                            slider.onValueChanged.AddListener((val) => cell.onFloatChanged(val));
                        break;
                }

                if (cellObj != null)
                {
                    var labelText = cellObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (labelText != null) labelText.text = cell.labelText;

                    RectTransform cellRt = cellObj.GetComponent<RectTransform>();
                    cellRt.localScale = Vector3.one;

                    cellRt.anchorMin = new Vector2(currentX, 0);
                    cellRt.anchorMax = new Vector2(currentX + cell.widthRatio, 1);
                    cellRt.pivot = new Vector2(0.5f, 0.5f);
                    cellRt.offsetMin = new Vector2(4, 2);
                    cellRt.offsetMax = new Vector2(-4, -2);

                    currentX += cell.widthRatio;
                }
            }

            currentY -= (rowHeight + rowSpacing);
        }

        return refs;
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

    public GridReferences RebuildGrid(RectTransform panel, List<GridRowSpec> rows)
    {
        // Destroy old UI elements
        foreach (Transform child in panel)
        {
            Destroy(child.gameObject);
        }
        // Build new ones
        return BuildGrid(panel, rows);
    }
}