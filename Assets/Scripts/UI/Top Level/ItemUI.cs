using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemUI : RootUI
{
    public enum ItemNodeType
    {
        BaseItem,
        Equippable,
        Hat,
        Chain,
        Splice,
        Merge,
        Unpack,
        ItemPart,
        RawString
    }

    private class WorkspaceItem : ReorderableItem
    {
        public ItemNodeType Type;
        public string NodeLabel = "Generic Logic Node";
        public ItemMechanic Mechanic = new ItemMechanic(); // Used for mechanic-based nodes
    }
    public static ItemUI Instance { get; private set; }

    // Icon Picker & Custom Image State
    private IconPickerModal iconPicker;
    private bool showCustomImagePanel = false;
    private string _customImageString;
    private Texture2D _customImageTexture;
    private ImageReceiver _persistentCustomImageReceiver;
    private Sprite _customImageCachedSprite;

    // SINGLE SOURCE OF TRUTH
    private ItemData currentItem;

    [Header("Layout & Containers")]
    private ReorderableZone _rootWorkspaceZone;
    private WorkspaceItem _selectedWorkspaceItem;

    private ScrollRect _workspaceScroll;
    private Transform _workspaceContent;

    // Top Bar UI
    private TMP_InputField _finalStringInput;
    private TextMeshProUGUI _englishTranslationText;

    // Merged Main Properties Editor
    private ScrollRect _mainScroll;
    private GridReferences _mainRefs;

    private bool _isLoading = false;

    private readonly string[] _nodeDropdownOptions = new string[]
    {
        "-- Add Logic Node --",
        "Base Item",
        "Item Appearance",
        "(Hat) Set Dice Face(s)",
        "(#) Chain Items",
        "Splice Item",
        "Merge Item",
        "Unpack Item",
        "Item Part",
        "Raw String"
    };

    public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        Instance = this;
        currentItem = new ItemData();

        if (iconPicker == null)
            iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);

        base.Initialize(uiGeneratorRef);
    }

    protected override void BuildUIAndBind()
    {
        float totalHeight = 600f;
        if (uiGenerator != null && uiGenerator.canvas != null)
        {
            RectTransform canvasRt = uiGenerator.canvas.GetComponent<RectTransform>();
            if (canvasRt != null) totalHeight = canvasRt.rect.height;
        }

        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;
        float spacing = uiGenerator != null ? uiGenerator.rowSpacing : 8f;
        float topBarHeight = 75f;

        float dynamicScrollHeight = Mathf.Max(300f, totalHeight - topBarHeight - (rowHeight * 2f) - (spacing * 3f));
        float mainScrollHeight = Mathf.Max(300f, totalHeight - topBarHeight - spacing);

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            BuildWorkspaceColumn(rowHeight, dynamicScrollHeight, topBarHeight),
            new ColumnSpec("Main_Column", 0.3f, 1.0f, new List<GridRowSpec>
            {
                new GridRowSpec(topBarHeight, GridCellSpec.CreateLabel("Main_Spacer", "", 1.0f)),
                new GridRowSpec(mainScrollHeight, GridCellSpec.CreateScrollView("MainScrollArea", 1.0f))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

        if (generatedScreen.RootWrapper != null)
        {
            generatedScreen.RootWrapper.offsetMin = Vector2.zero;
            generatedScreen.RootWrapper.offsetMax = Vector2.zero;
        }

        BuildTopCompilerBar(topBarHeight);

        if (generatedScreen.ColumnRefs.TryGetValue("Main_Column", out GridReferences mainCol) &&
            mainCol.ScrollViews.TryGetValue("MainScrollArea", out _mainScroll))
        {
            RebuildMainEditorPanel();
        }

        ConfigureWorkspace();
        UpdateCompilerOutput();
        ApplyDynamicLayoutConstraints();

        Canvas.ForceUpdateCanvases();
    }

    #region CORE DATA FLOW & UI BINDING

    private void SetInputValue(GridReferences refs, string key, string value)
    {
        if (refs != null && refs.Inputs.TryGetValue(key, out var input))
            input.SetTextWithoutNotify(value ?? "");
    }

    private void SetToggleValue(GridReferences refs, string key, bool value)
    {
        if (refs != null && refs.Toggles.TryGetValue(key, out var toggle))
            toggle.SetIsOnWithoutNotify(value);
    }

    private void PopulateMainPanelFromData()
    {
        if (_mainRefs == null || currentItem == null || _selectedWorkspaceItem == null) return;

        bool prev = _isLoading;
        _isLoading = true;

        if (_selectedWorkspaceItem.Type == ItemNodeType.Equippable)
        {
            SetInputValue(_mainRefs, "In_Name", currentItem.entityName);
            SetInputValue(_mainRefs, "In_Tier", currentItem.Tier?.ToString() ?? "");
            SetInputValue(_mainRefs, "In_Doc", currentItem.DocumentedDescription);

            SetInputValue(_mainRefs, "In_Img", currentItem.imageOverride);

            int h = currentItem.HsvShift?.Hue ?? 0;
            int s = currentItem.HsvShift?.Saturation ?? 0;
            int v = currentItem.HsvShift?.Value ?? 0;
            SetInputValue(_mainRefs, "In_H", h.ToString());
            SetInputValue(_mainRefs, "In_S", s.ToString());
            SetInputValue(_mainRefs, "In_V", v.ToString());

            SetInputValue(_mainRefs, "In_Hue", currentItem.SimpleHue?.ToString() ?? "");
            //SetInputValue(_mainRefs, "In_THue", currentItem.TargetedHue);
            SetInputValue(_mainRefs, "In_P", currentItem.PaletteOverride);
            SetInputValue(_mainRefs, "In_B", currentItem.BorderColorCode);

            SetInputValue(_mainRefs, "In_Rect", currentItem.UiRectInstructions);
            SetInputValue(_mainRefs, "In_Draw", currentItem.UiDrawInstructions);

            SetToggleValue(_mainRefs, "Tgl_ClearIcon", currentItem.ClearIcon);
            SetToggleValue(_mainRefs, "Tgl_ClearDesc", currentItem.ClearDescription);
        }

        _isLoading = prev;
    }
    /*
    private void RebuildMainEditorPanel()
    {
        if (_mainScroll == null || _mainScroll.content == null) return;
        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;

        _mainRefs = uiGenerator.RebuildGrid(_mainScroll.content, GetMainEditorRows(rowHeight), useMargins: true);

        float extraH = CalculateScrollExtraHeight(_mainScroll.content);
        _mainScroll.content.sizeDelta = new Vector2(0, _mainRefs.TotalHeight + extraH);

        PopulateMainPanelFromData();
    }
    */
    private void UpdateCompilerOutput()
    {
        if (_isLoading) return;

        currentItem.Mechanics.Clear();

        if (_rootWorkspaceZone != null)
        {
            foreach (WorkspaceItem instance in _rootWorkspaceZone.Entrants)
            {
                // Only nodes that map to an ItemMechanic get added to the mechanics list
                if (instance.Mechanic != null && IsMechanicNode(instance.Type))
                {
                    currentItem.Mechanics.Add(instance.Mechanic);
                }
            }
        }

        string rawExport = currentItem.Export();
        string englishOutput = $"Item '{currentItem.entityName ?? "New Item"}' placeholder translation.";

        if (_finalStringInput != null) _finalStringInput.text = rawExport;
        if (_englishTranslationText != null) _englishTranslationText.text = englishOutput;
    }

    private bool IsMechanicNode(ItemNodeType type)
    {
        // Meta-nodes don't export to currentItem.Mechanics directly, they edit fields on currentItem
        return type != ItemNodeType.BaseItem && type != ItemNodeType.Equippable;
    }

    #endregion

    #region LAYOUT DEFINITIONS

    private ColumnSpec BuildWorkspaceColumn(float rowHeight, float dynamicScrollHeight, float topBarHeight)
    {
        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(topBarHeight, GridCellSpec.CreateLabel("Left_Spacer", "", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_AddNode", "", 1.0f, _nodeDropdownOptions, OnNodeDropdownSelected)),
            new GridRowSpec(dynamicScrollHeight, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("BtnClearWorkspace", "Clear Nodes", 1f, ClearWorkspace))
        };

        return new ColumnSpec("Workspace_Column", 0.0f, 0.3f, rows);
    }

    private List<GridRowSpec> GetMainEditorRows(float rowHeight)
    {
        if (_selectedWorkspaceItem == null)
        {
            return new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("LBL_Empty", "Select a node from the workspace to edit its properties.", 1.0f))
            };
        }

        switch (_selectedWorkspaceItem.Type)
        {
            case ItemNodeType.BaseItem: return GetBaseItemRows(rowHeight);
            case ItemNodeType.Equippable: return GetAppearanceRows(rowHeight);
            case ItemNodeType.Hat: return GetPlaceholderRows(rowHeight, "Hat / Set Dice Face(s)");
            case ItemNodeType.Chain: return GetPlaceholderRows(rowHeight, "Chain Items (#)");
            case ItemNodeType.Splice: return GetPlaceholderRows(rowHeight, "Splice Item");
            case ItemNodeType.Merge: return GetPlaceholderRows(rowHeight, "Merge Item");
            case ItemNodeType.Unpack: return GetPlaceholderRows(rowHeight, "Unpack Item");
            case ItemNodeType.ItemPart: return GetPlaceholderRows(rowHeight, "Item Part");
            case ItemNodeType.RawString: return GetPlaceholderRows(rowHeight, "Raw String / Direct Inject");
            default: return new List<GridRowSpec>();
        }
    }

    private List<GridRowSpec> GetBaseItemRows(float rowHeight)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_Meta", "--- Core Metadata ---", 1.0f)),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Name", "Item Name (n)", 0.7f, (val) => { currentItem.entityName = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_Tier", "Tier (tier)", 0.3f, OnTierChanged)
            ),
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_Doc", "Documented Description (doc)", 1.0f, (val) => { currentItem.DocumentedDescription = val; UpdateCompilerOutput(); }))
        };
    }

    /*
    private List<GridRowSpec> GetAppearanceRows(float rowHeight)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_Colors", "--- Aesthetics & Colors ---", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_Img", "Icon Override Ref (img)", 1.0f, (val) => { currentItem.imageOverride = val; UpdateCompilerOutput(); })),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("LBL_HSV", "HSV Shift:", 0.25f),
                GridCellSpec.CreateInput("In_H", "Hue", 0.25f, (val) => UpdateHsvShift()),
                GridCellSpec.CreateInput("In_S", "Sat", 0.25f, (val) => UpdateHsvShift()),
                GridCellSpec.CreateInput("In_V", "Val", 0.25f, (val) => UpdateHsvShift())
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Hue", "Simple Hue (hue)", 0.33f, OnHueChanged),
                //GridCellSpec.CreateInput("In_THue", "Target Hue (thue)", 0.33f, (val) => { currentItem.TargetedHue = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_P", "Palette Override (p)", 0.34f, (val) => { currentItem.PaletteOverride = val; UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_B", "Border Color Code (b)", 1.0f, (val) => { currentItem.BorderColorCode = val; UpdateCompilerOutput(); })),

            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_UI", "--- UI & Rendering Overrides ---", 1.0f)),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Rect", "Rect Layout (rect)", 0.5f, (val) => { currentItem.UiRectInstructions = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_Draw", "Draw Mode (draw)", 0.5f, (val) => { currentItem.UiDrawInstructions = val; UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_ClearIcon", "Clear Base Icon", 0.5f, (val) => { currentItem.ClearIcon = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_ClearDesc", "Clear Base Desc", 0.5f, (val) => { currentItem.ClearDescription = val; UpdateCompilerOutput(); })
            )
        };
    }
    */
    private List<GridRowSpec> GetPlaceholderRows(float rowHeight, string title)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_PH", $"--- {title} ---", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("PH_Desc", "UI elements for this node have not been mapped yet.", 1.0f))
        };
    }

    #endregion

    #region WORKSPACE HIERARCHY LOGIC

    private void ConfigureWorkspace()
    {
        if (generatedScreen == null || !generatedScreen.ColumnRefs.TryGetValue("Workspace_Column", out GridReferences refs)) return;

        if (refs.ScrollViews.TryGetValue("WorkspaceScrollArea", out _workspaceScroll))
        {
            _workspaceContent = _workspaceScroll.content;

            VerticalLayoutGroup layout = _workspaceContent.gameObject.GetComponent<VerticalLayoutGroup>() ?? _workspaceContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = _workspaceContent.gameObject.GetComponent<ContentSizeFitter>() ?? _workspaceContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _rootWorkspaceZone = _workspaceContent.gameObject.GetComponent<ReorderableZone>() ?? _workspaceContent.gameObject.AddComponent<ReorderableZone>();
        }
    }

    private void OnNodeDropdownSelected(int index)
    {
        if (index <= 0) return;

        ItemNodeType selectedType = (ItemNodeType)(index - 1);
        string nodeLabel = _nodeDropdownOptions[index];

        AddNodeToWorkspace(selectedType, nodeLabel);

        // Reset the dropdown visually
        if (generatedScreen.ColumnRefs.TryGetValue("Workspace_Column", out GridReferences refs) && refs.Dropdowns.TryGetValue("Drop_AddNode", out TMP_Dropdown dropdown))
        {
            dropdown.SetValueWithoutNotify(0);
        }
    }

    private void AddNodeToWorkspace(ItemNodeType type, string label)
    {
        if (_workspaceContent == null || _rootWorkspaceZone == null) return;

        GameObject btnGo = Instantiate(uiGenerator.buttonPrefab);
        btnGo.name = $"WorkspaceBtn_{type}";

        WorkspaceItem workspaceItem = btnGo.AddComponent<WorkspaceItem>();
        workspaceItem.Type = type;
        workspaceItem.NodeLabel = label;
        workspaceItem.Mechanic = IsMechanicNode(type) ? new ItemMechanic() : null;

        TextMeshProUGUI btnText = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = label;
            btnText.margin = new Vector4(8f, 0f, 32f, 0f);
        }

        Button selectBtn = btnGo.GetComponent<Button>();
        if (selectBtn != null) selectBtn.onClick.AddListener(() => SelectWorkspaceBlock(workspaceItem));

        _rootWorkspaceZone.AddEntrant(workspaceItem);

        // Delete Button setup
        GameObject deleteBtnGo = Instantiate(uiGenerator.buttonPrefab, btnGo.transform);
        deleteBtnGo.name = "DeleteBtn";

        LayoutElement lay = deleteBtnGo.GetComponent<LayoutElement>() ?? deleteBtnGo.AddComponent<LayoutElement>();
        lay.ignoreLayout = true;

        RectTransform delRt = deleteBtnGo.GetComponent<RectTransform>();
        delRt.anchorMin = new Vector2(1f, 0.5f);
        delRt.anchorMax = new Vector2(1f, 0.5f);
        delRt.pivot = new Vector2(1f, 0.5f);
        delRt.anchoredPosition = new Vector2(-6f, 0f);
        delRt.sizeDelta = new Vector2(24f, 24f);

        TextMeshProUGUI delText = deleteBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (delText != null) { delText.text = "X"; delText.fontSize = 12; delText.color = Color.red; }

        Button delBtn = deleteBtnGo.GetComponent<Button>();
        if (delBtn != null)
        {
            delBtn.onClick.RemoveAllListeners();
            delBtn.onClick.AddListener(() => RemoveBlockFromWorkspace(workspaceItem));
        }

        Canvas.ForceUpdateCanvases();
        if (_workspaceScroll != null) _workspaceScroll.verticalNormalizedPosition = 0f;

        SelectWorkspaceBlock(workspaceItem); // Auto-select when adding
        UpdateCompilerOutput();
    }

    private void SelectWorkspaceBlock(WorkspaceItem instance)
    {
        _selectedWorkspaceItem = instance;
        RebuildMainEditorPanel();
    }

    private void RemoveBlockFromWorkspace(WorkspaceItem item)
    {
        if (item == null) return;

        if (_rootWorkspaceZone != null && _rootWorkspaceZone.Entrants != null)
            _rootWorkspaceZone.Entrants.Remove(item);

        if (_selectedWorkspaceItem == item)
        {
            _selectedWorkspaceItem = null;
            RebuildMainEditorPanel();
        }

        // Clean up currentItem if a meta-node is deleted
        if (item.Type == ItemNodeType.BaseItem)
        {
            currentItem.entityName = string.Empty;
            currentItem.Tier = null;
            currentItem.DocumentedDescription = string.Empty;
        }
        else if (item.Type == ItemNodeType.Equippable)
        {
            currentItem.imageOverride = string.Empty;
            currentItem.HsvShift = null;
            currentItem.SimpleHue = null;
            currentItem.thue = new Thue();
            currentItem.PaletteOverride = string.Empty;
            currentItem.BorderColorCode = string.Empty;
            currentItem.UiDrawInstructions = string.Empty;
            currentItem.UiRectInstructions = string.Empty;
            currentItem.ClearDescription = false;
            currentItem.ClearIcon = false;
        }

        Destroy(item.gameObject);
        StartCoroutine(ExecuteRecompileNextFrame());
    }

    private void ClearWorkspace()
    {
        _selectedWorkspaceItem = null;
        if (_workspaceContent != null)
        {
            foreach (Transform child in _workspaceContent) Destroy(child.gameObject);
        }
        if (_rootWorkspaceZone != null && _rootWorkspaceZone.Entrants != null)
            _rootWorkspaceZone.Entrants.Clear();

        currentItem = new ItemData();
        RebuildMainEditorPanel();
        UpdateCompilerOutput();
    }

    private System.Collections.IEnumerator ExecuteRecompileNextFrame()
    {
        yield return null;
        UpdateCompilerOutput();
    }

    #endregion

    #region TOP BAR & CONTROLS

    private void BuildTopCompilerBar(float height)
    {
        GameObject topBar = new GameObject("TopCompilerBar", typeof(RectTransform), typeof(Image));
        RectTransform rt = topBar.GetComponent<RectTransform>();

        if (generatedScreen.RootWrapper != null) rt.SetParent(generatedScreen.RootWrapper, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
        topBar.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(25f, GridCellSpec.CreateLabel("Lbl_English", "[English Translation Engine Placeholder]", 1.0f)),
            new GridRowSpec(40f,
                GridCellSpec.CreateDropdown("Drop_Load", "Load...", 0.12f, new string[] { "Load Saved Item" }, OnSelectLoadItem),
                GridCellSpec.CreateButton("Btn_Save", "Save Item To Mod", 0.1f, OnClickSaveItem),
                GridCellSpec.CreateButton("Btn_New", "New Item +", 0.08f, OnClickNewItem),

                GridCellSpec.CreateInput("Input_RawString", "Compiled text output...", 0.6f, null, InputAlignment.Center),
                GridCellSpec.CreateButton("Btn_Copy", "COPY", 0.1f, CopyToClipboard)
            )
        };

        GridReferences topRefs = uiGenerator.RebuildGrid(rt, rows, useMargins: true);
        _englishTranslationText = topBar.GetComponentInChildren<TextMeshProUGUI>();
        topRefs.Inputs.TryGetValue("Input_RawString", out _finalStringInput);
    }

    private void OnClickNewItem() { ClearWorkspace(); }
    private void OnClickSaveItem() { Debug.Log("Save Item Logic Pipeline Needed"); }
    private void OnSelectLoadItem(int index) { if (index > 0) Debug.Log("Load Item Logic Pipeline Needed"); }

    private void CopyToClipboard()
    {
        if (_finalStringInput != null && !string.IsNullOrEmpty(_finalStringInput.text))
        {
            GUIUtility.systemCopyBuffer = _finalStringInput.text;
            uiGenerator.CreatePopup("Successfully compiled item string and copied to system clipboard!");
        }
    }

    #endregion

    #region PARSING & VALUE CHANGERS

    private void OnTierChanged(string text)
    {
        if (_isLoading) return;
        if (int.TryParse(text, out int val)) currentItem.Tier = val;
        else currentItem.Tier = null;
        UpdateCompilerOutput();
    }

    private void OnHueChanged(string text)
    {
        if (_isLoading) return;
        if (int.TryParse(text, out int val)) currentItem.SimpleHue = val;
        else currentItem.SimpleHue = null;
        UpdateCompilerOutput();
    }

    private void UpdateHsvShift()
    {
        if (_isLoading || _mainRefs == null) return;

        int.TryParse(_mainRefs.Inputs["In_H"].text, out int h);
        int.TryParse(_mainRefs.Inputs["In_S"].text, out int s);
        int.TryParse(_mainRefs.Inputs["In_V"].text, out int v);

        if (h == 0 && s == 0 && v == 0) currentItem.HsvShift = null;
        else currentItem.HsvShift = new ItemHsvShift(h, s, v);

        UpdateCompilerOutput();
    }

    #endregion

    #region BOILERPLATE LAYOUT UTILS

    private float CalculateScrollExtraHeight(RectTransform content)
    {
        float extraHeight = 0f;
        var layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }
        return extraHeight;
    }

    private void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;
        var layoutElement = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
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

    private void ApplyDynamicLayoutConstraints()
    {
        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;
        float spacing = uiGenerator != null ? uiGenerator.rowSpacing : 8f;
        float topBarHeight = 75f;

        if (generatedScreen.ColumnRefs.TryGetValue("Workspace_Column", out GridReferences leftRefs))
        {
            if (leftRefs.ScrollViews.TryGetValue("WorkspaceScrollArea", out ScrollRect scroll))
            {
                RectTransform scrollRt = scroll.GetComponent<RectTransform>();
                RectTransform rowContainerRt = scrollRt.parent as RectTransform;

                float topOffset = topBarHeight + rowHeight + (spacing * 2f);
                float bottomOffset = rowHeight + (spacing * 2f);

                ConfigureFlexibleLayout(rowContainerRt);
                ConfigureFlexibleLayout(scrollRt);
                StretchToParent(rowContainerRt, topOffset, bottomOffset);
                StretchToParent(scrollRt, 0f, 0f);
            }
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Main_Column", out GridReferences mainRefs) &&
            mainRefs.ScrollViews.TryGetValue("MainScrollArea", out ScrollRect mainScroll))
        {
            RectTransform scrollRt = mainScroll.GetComponent<RectTransform>();
            RectTransform rowContainerRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowContainerRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowContainerRt, topBarHeight + spacing, 0f);
            StretchToParent(scrollRt, 0f, 0f);
        }
    }
    #endregion

    private void OpenFacadeModal()
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites, // Valid item facades
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) => sprite.name,
            GetTooltip = (index, sprite) => sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');

                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        string prefix = parts[0].ToLower();
                        string facadeStr;

                        // Legacy translation layer alignment
                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31)
                        {
                            facadeStr = $"bas{188 + parsedId}";
                        }
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27)
                        {
                            facadeStr = $"bas{220 + parsedId}";
                        }
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17)
                        {
                            facadeStr = $"bas{248 + parsedId}";
                        }
                        else
                        {
                            facadeStr = $"{parts[0]}{parts[1]}";
                        }

                        currentItem.imageOverride = facadeStr;
                    }
                    else
                    {
                        currentItem.imageOverride = filename;
                    }

                    UpdateCompilerOutput();
                    RebuildMainEditorPanel();
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    private void ToggleCustomImagePanel()
    {
        showCustomImagePanel = !showCustomImagePanel;
        RebuildMainEditorPanel();
    }

    private void UpdateItemHsvData(int componentIndex, int value)
    {
        int h = currentItem.HsvShift?.Hue ?? 0;
        int s = currentItem.HsvShift?.Saturation ?? 0;
        int v = currentItem.HsvShift?.Value ?? 0;

        if (componentIndex == 0) h = value;
        else if (componentIndex == 1) s = value;
        else if (componentIndex == 2) v = value;

        if (h == 0 && s == 0 && v == 0)
        {
            currentItem.HsvShift = null;
        }
        else
        {
            currentItem.HsvShift = new ItemHsvShift(h, s, v);
        }

        // Synchronize Input UI component
        string inputKey = componentIndex == 0 ? "In_H" : (componentIndex == 1 ? "In_S" : "In_V");
        if (_mainRefs != null && _mainRefs.Inputs.TryGetValue(inputKey, out var input))
        {
            input.SetTextWithoutNotify(value.ToString());
        }

        // Synchronize Slider UI component
        string sliderKey = componentIndex == 0 ? "Sld_H" : (componentIndex == 1 ? "Sld_S" : "Sld_V");
        if (_mainRefs != null && _mainRefs.Sliders.TryGetValue(sliderKey, out var slider))
        {
            slider.SetValueWithoutNotify(value);
        }

        UpdateCompilerOutput();
    }

    private List<GridRowSpec> GetAppearanceRows(float rowHeight)
    {
        var layout = new List<GridRowSpec>
        {
            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_Colors", "--- Aesthetics & Colors ---", 1.0f)),

            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Icon Override:", 0.30f),
                GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, OpenFacadeModal),
                GridCellSpec.CreateInput("In_Img", "None", 0.35f, (val) => { currentItem.imageOverride = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.20f, ToggleCustomImagePanel)
            )
        };

        if (showCustomImagePanel)
        {
            layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));
        }

        layout.AddRange(new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Hue:", 0.30f),
                GridCellSpec.CreateSlider("Sld_H", -99, 99, true, 0.50f, (val) => UpdateItemHsvData(0, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput("In_H", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateItemHsvData(0, h); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Saturation:", 0.30f),
                GridCellSpec.CreateSlider("Sld_S", -99, 99, true, 0.50f, (val) => UpdateItemHsvData(1, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput("In_S", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateItemHsvData(1, s); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Value:", 0.30f),
                GridCellSpec.CreateSlider("Sld_V", -99, 99, true, 0.50f, (val) => UpdateItemHsvData(2, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput("In_V", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateItemHsvData(2, v); })
            ),
            /*
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Hue", "Simple Hue (hue)", 0.33f, OnHueChanged),
                //GridCellSpec.CreateInput("In_THue", "Target Hue (thue)", 0.33f, (val) => { currentItem.hue = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_P", "Palette Override (p)", 0.34f, (val) => { currentItem.PaletteOverride = val; UpdateCompilerOutput(); })
            ),
            */
            /*
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_B", "Border Color Code (b)", 1.0f, (val) => { currentItem.BorderColorCode = val; UpdateCompilerOutput(); })),
            */
            /*
            new GridRowSpec(30f, GridCellSpec.CreateLabel("Header_UI", "--- UI & Rendering Overrides ---", 1.0f)),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Rect", "Rect Layout (rect)", 0.5f, (val) => { currentItem.UiRectInstructions = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_Draw", "Draw Mode (draw)", 0.5f, (val) => { currentItem.UiDrawInstructions = val; UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_ClearIcon", "Clear Base Icon", 0.5f, (val) => { currentItem.ClearIcon = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_ClearDesc", "Clear Base Desc", 0.5f, (val) => { currentItem.ClearDescription = val; UpdateCompilerOutput(); })
            )
            */
        });

        return layout;
    }

    private void RebuildMainEditorPanel()
    {
        if (_mainScroll == null || _mainScroll.content == null) return;
        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;

        _mainRefs = uiGenerator.RebuildGrid(_mainScroll.content, GetMainEditorRows(rowHeight), useMargins: true);

        float extraH = CalculateScrollExtraHeight(_mainScroll.content);
        _mainScroll.content.sizeDelta = new Vector2(0, _mainRefs.TotalHeight + extraH);

        if (_selectedWorkspaceItem != null && _selectedWorkspaceItem.Type == ItemNodeType.Equippable)
        {
            if (showCustomImagePanel)
            {
                if (_mainRefs.CustomImgImporter.TryGetValue("CustomImgPanel", out ImageReceiver dummyReceiver))
                {
                    if (_persistentCustomImageReceiver == null)
                    {
                        _persistentCustomImageReceiver = dummyReceiver;
                        _persistentCustomImageReceiver.OnImageGenerated = (encodedStr, tex) =>
                        {
                            currentItem.imageOverride = encodedStr;
                            _customImageString = encodedStr;
                            _customImageTexture = tex;

                            if (_customImageCachedSprite != null) Destroy(_customImageCachedSprite);
                            _customImageCachedSprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));

                            UpdateCompilerOutput();
                        };
                    }
                    else
                    {
                        Transform placeholderParent = dummyReceiver.transform.parent;
                        Destroy(dummyReceiver.gameObject);

                        _persistentCustomImageReceiver.transform.SetParent(placeholderParent, false);
                        _persistentCustomImageReceiver.gameObject.SetActive(true);

                        RectTransform rt = _persistentCustomImageReceiver.GetComponent<RectTransform>();
                        FullScreenUIGenerator.SetAnchors(rt, 0, 0, 1, 1);
                    }
                }
            }
            else if (_persistentCustomImageReceiver != null)
            {
                _persistentCustomImageReceiver.gameObject.SetActive(false);
            }
        }

        PopulateMainPanelFromData();
    }
}