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
        if (canvas == null)
        {
            Debug.LogError("NO CANVAS");
            return null;
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
            rowRt.pivot = new Vector2(0.5f, 1.0f); // Force the row to pivot from its top edge

            float activeHeight = rowSpec.customHeight > 0 ? rowSpec.customHeight : rowHeight;
            rowRt.sizeDelta = new Vector2(0, activeHeight);

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

                    case CellType.ImagePanel:
                        cellObj = Instantiate(imagePanelPrefab, rowRt);
                        Image img = cellObj.GetComponentInChildren<Image>();
                        if (img != null)
                        {
                            img.color = cell.panelColor;
                            if (cell.panelSprite != null)
                            {
                                img.sprite = cell.panelSprite;
                                img.type = Image.Type.Sliced;
                            }
                            refs.ImagePanels[cell.key] = img;
                        }
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
                        ImageButton imgBtn = cellObj.GetComponent<ImageButton>();
                        Button btn = null;

                        if (imgBtn != null)
                        {
                            btn = imgBtn.button;

                            // Configure Text Component
                            if (imgBtn.text != null)
                            {
                                if (cell.labelText != null) // Passing null means NO text. "" means empty text.
                                {
                                    imgBtn.text.text = cell.labelText;
                                    imgBtn.text.gameObject.SetActive(true);
                                }
                                else
                                {
                                    imgBtn.text.gameObject.SetActive(false);
                                }
                            }

                            // Configure Image Component
                            if (imgBtn.image != null)
                            {
                                if (cell.buttonSprite != null) // Passing null means NO image.
                                {
                                    imgBtn.image.sprite = cell.buttonSprite;
                                    imgBtn.image.gameObject.SetActive(true);
                                }
                                else
                                {
                                    imgBtn.image.gameObject.SetActive(false);
                                }
                            }

                            // CRITICAL: Wipe the labelText so the generic text applicator at the 
                            // bottom of the BuildGrid loop doesn't forcibly turn the text back on.
                            cell.labelText = null;
                        }
                        else
                        {
                            btn = cellObj.GetComponentInChildren<Button>();
                        }

                        refs.Buttons[cell.key] = btn;

                        if (btn != null && cell.onClicked != null)
                        {
                            btn.onClick.AddListener(() => cell.onClicked());
                        }
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

                    case CellType.ScrollView:
                        cellObj = Instantiate(scrollViewPrefab, rowRt);
                        ScrollRect scrollRect = cellObj.GetComponentInChildren<ScrollRect>();
                        if (scrollRect != null)
                        {
                            refs.ScrollViews[cell.key] = scrollRect;
                        }
                        break;

                    case CellType.NavigationTabs:
                        cellObj = Instantiate(navigationTabs, rowRt);
                        NavigationTabsController navTabs = cellObj.GetComponent<NavigationTabsController>();
                        if (navTabs != null)
                        {
                            navTabs.Initialize(cell.tabNames, cell.tabTargetPanels, buttonPrefab, cell.onIntChanged);
                            refs.NavigationTabs[cell.key] = navTabs;
                        }
                        break;
                }

                if (cellObj != null)
                {
                    // 1. Configure all child texts (previously unreachable)
                    var textComponents = cellObj.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var txt in textComponents)
                    {
                        txt.enableAutoSizing = false;
                        txt.fontSize = 13f;
                    }

                    // 2. Set the main label text
                    var mainLabel = cellObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (mainLabel != null && !string.IsNullOrEmpty(cell.labelText))
                    {
                        mainLabel.text = cell.labelText;
                    }

                    // 3. Apply anchoring & layouts (only once per cell)
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

            currentY -= (activeHeight + rowSpacing);
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