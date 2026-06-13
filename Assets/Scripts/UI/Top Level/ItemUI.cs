using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ModEditor;

public class ItemUI : RootUI
{
    private class WorkspaceItem : ReorderableItem
    {
        public string OptionName;
        public ItemMechanic Mechanic = new ItemMechanic();
    }

    public static ItemUI Instance { get; private set; }

    // SINGLE SOURCE OF TRUTH
    private ItemData currentItem;

    private ReorderableZone _rootWorkspaceZone;
    private WorkspaceItem _selectedWorkspaceItem;

    [Header("Layout & Containers")]
    private ScrollRect _workspaceScroll;
    private Transform _workspaceContent;

    private TMP_InputField _finalStringInput;
    private TextMeshProUGUI _englishTranslationText;

    private ScrollRect _middleScroll;
    private ScrollRect _rightScroll;
    private GridReferences _midRefs;
    private GridReferences _rightRefs;

    [Header("Icon Selection & Custom Upload")]
    private IconPickerModal iconPicker;
    private bool showCustomImagePanel = false;
    private Texture2D _customImageTexture;
    private Sprite _customImageCachedSprite;
    private ImageReceiver _persistentCustomImageReceiver;
    private string _customImageString;

    private bool _isLoading = false;

    // Configuration Arrays
    private readonly string[] macroOptions = {
        "Quick Add Logic Node...", "Add Keyword", "Create Buff/Debuff (Sticker)",
        "Summon Entity", "Apply Passive (Custom Side)", "Boss Phase (TriggerHP)", "Raw Engine Injection"
    };
    private readonly string[] conditionOptions = { "None (Always)", "Target has Full HP", "Target is Damaged", "Target is Ally", "Target is Enemy", "I have Full HP" };
    private readonly string[] targetFaceOptions = { "Custom Selection", "All", "Top", "Bottom", "Middle", "Left", "Right", "Rightmost", "Top & Bottom", "Column", "Row" };
    private readonly string[] actionTypeOptions = { "Keyword", "Sticker (Buff/Debuff)", "Summon (Egg)", "Passive Effect", "TriggerHP", "Raw Inject" };
    private readonly string[] keywordDatabase = { "cantrip", "poison", "decay", "engage", "heavy", "shieldself", "heal", "undying", "first", "exert", "sticky" };
    private readonly string[] itemDatabase = { "Shortsword", "Leather Vest", "Foil", "Kilt", "Chainmail", "Eye of Horus", "Origami", "Wrench" };

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

        float leftScrollHeight = Mathf.Max(300f, totalHeight - topBarHeight - (rowHeight * 3f) - (spacing * 4f));
        float midRightScrollHeight = Mathf.Max(300f, totalHeight - topBarHeight - spacing);

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            BuildHierarchyColumn(rowHeight, leftScrollHeight, topBarHeight),
            new ColumnSpec("Middle_Column", 0.33f, 0.69f, new List<GridRowSpec>
            {
                new GridRowSpec(topBarHeight, GridCellSpec.CreateLabel("Middle_Spacer", "", 1.0f)),
                new GridRowSpec(midRightScrollHeight, GridCellSpec.CreateScrollView("MiddleScrollArea", 1.0f))
            }),
            new ColumnSpec("Right_Column", 0.70f, 1.0f, new List<GridRowSpec>
            {
                new GridRowSpec(topBarHeight, GridCellSpec.CreateLabel("Right_Spacer", "", 1.0f)),
                new GridRowSpec(midRightScrollHeight, GridCellSpec.CreateScrollView("RightScrollArea", 1.0f))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

        if (generatedScreen.RootWrapper != null)
        {
            generatedScreen.RootWrapper.offsetMin = Vector2.zero;
            generatedScreen.RootWrapper.offsetMax = Vector2.zero;
        }

        BuildTopCompilerBar(topBarHeight);

        if (generatedScreen.ColumnRefs.TryGetValue("Middle_Column", out GridReferences mCol) &&
            mCol.ScrollViews.TryGetValue("MiddleScrollArea", out _middleScroll))
        {
            RebuildMiddlePanel();
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rCol) &&
            rCol.ScrollViews.TryGetValue("RightScrollArea", out _rightScroll))
        {
            RebuildRightColumn();
        }

        ConfigureWorkspace();
        PopulateRightPanelFromData();
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

    private void SetSliderValue(GridReferences refs, string key, float value)
    {
        if (refs != null && refs.Sliders.TryGetValue(key, out var slider))
            slider.SetValueWithoutNotify(value);
    }

    private void SetToggleValue(GridReferences refs, string key, bool value)
    {
        if (refs != null && refs.Toggles.TryGetValue(key, out var toggle))
            toggle.SetIsOnWithoutNotify(value);
    }

    private void SetDropdownValue(GridReferences refs, string key, int value)
    {
        if (refs != null && refs.Dropdowns.TryGetValue(key, out var dropdown))
            dropdown.SetValueWithoutNotify(value);
    }

    private void SetFilteredDropdownValue(GridReferences refs, string key, int value)
    {
        if (refs != null && refs.FilteredDropdowns.TryGetValue(key, out var dropdown))
        {
            bool prev = _isLoading;
            _isLoading = true;
            dropdown.value = value;
            _isLoading = prev;
        }
    }

    private void PopulateRightPanelFromData()
    {
        if (_rightRefs == null) return;

        SetInputValue(_rightRefs, "In_Name", currentItem.entityName);
        SetSliderValue(_rightRefs, "Sld_Tier", currentItem.tier ?? 0);
        SetInputValue(_rightRefs, "In_TierNum", (currentItem.tier ?? 0).ToString());
        SetInputValue(_rightRefs, "In_Doc", currentItem.doc);
        SetInputValue(_rightRefs, "In_SideDesc", currentItem.sidesc);
        SetInputValue(_rightRefs, "In_Img", currentItem.imageOverride);

        SetSliderValue(_rightRefs, "Sld_H", currentItem.h);
        SetInputValue(_rightRefs, "In_H", currentItem.h.ToString());
        SetSliderValue(_rightRefs, "Sld_S", currentItem.s);
        SetInputValue(_rightRefs, "In_S", currentItem.s.ToString());
        SetSliderValue(_rightRefs, "Sld_V", currentItem.v);
        SetInputValue(_rightRefs, "In_V", currentItem.v.ToString());

        SetInputValue(_rightRefs, "In_P", currentItem.p);
        SetInputValue(_rightRefs, "In_B", currentItem.b);
        SetInputValue(_rightRefs, "In_Rect", currentItem.rect);
        SetInputValue(_rightRefs, "In_Draw", currentItem.draw);
        SetInputValue(_rightRefs, "In_THue", currentItem.thue);

        UpdateVisuals();
    }

    private void PopulateMiddlePanelFromSelected()
    {
        if (_selectedWorkspaceItem == null || _midRefs == null) return;

        ItemMechanic mechanic = _selectedWorkspaceItem.Mechanic;

        SetToggleValue(_midRefs, "Tgl_Face_Top", mechanic.Positions.Contains("top"));
        SetToggleValue(_midRefs, "Tgl_Face_Bot", mechanic.Positions.Contains("bot"));
        SetToggleValue(_midRefs, "Tgl_Face_Mid", mechanic.Positions.Contains("mid"));
        SetToggleValue(_midRefs, "Tgl_Face_Left", mechanic.Positions.Contains("left"));
        SetToggleValue(_midRefs, "Tgl_Face_Right", mechanic.Positions.Contains("right"));
        SetToggleValue(_midRefs, "Tgl_Face_Rightmost", mechanic.Positions.Contains("rightmost"));

        SetToggleValue(_midRefs, "Tgl_IsWrapped", mechanic.IsWrapped);

        if (HasOp(mechanic, "k"))
        {
            int idx = Array.IndexOf(keywordDatabase, mechanic.Payload);
            if (idx >= 0) SetFilteredDropdownValue(_midRefs, "Drop_KeywordSelect", idx);
        }
        else if (HasOp(mechanic, "sticker"))
        {
            int idx = Array.IndexOf(itemDatabase, mechanic.Payload);
            if (idx >= 0) SetFilteredDropdownValue(_midRefs, "Drop_ItemSelect", idx);
        }
        else if (HasOp(mechanic, "cast"))
        {
            SetInputValue(_midRefs, "In_SummonEntity", mechanic.Payload);
        }

        if (_selectedWorkspaceItem.OptionName == "Raw Engine Injection" || _selectedWorkspaceItem.OptionName == "Quick Add Logic Node...")
        {
            SetDropdownValue(_midRefs, "Drop_ActionType", GetActionTypeIndex(mechanic));
        }
    }

    private void RebuildMiddlePanel()
    {
        if (_middleScroll == null || _middleScroll.content == null) return;
        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;

        _midRefs = uiGenerator.RebuildGrid(_middleScroll.content, GetModularWorkspaceRows(rowHeight), useMargins: true);

        float extraH = CalculateScrollExtraHeight(_middleScroll.content);
        _middleScroll.content.sizeDelta = new Vector2(0, _midRefs.TotalHeight + extraH);

        PopulateMiddlePanelFromSelected();
    }

    private void RebuildRightColumn()
    {
        if (_rightScroll == null || _rightScroll.content == null) return;

        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;
        _rightRefs = uiGenerator.RebuildGrid(_rightScroll.content, GetMetadataColumnRows(rowHeight), useMargins: true);

        float extraH = CalculateScrollExtraHeight(_rightScroll.content);
        _rightScroll.content.sizeDelta = new Vector2(0, _rightRefs.TotalHeight + extraH);

        if (showCustomImagePanel)
        {
            if (_rightRefs.CustomImgImporter.TryGetValue("CustomImgPanel", out ImageReceiver dummyReceiver))
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

                        UpdateVisuals();
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

        PopulateRightPanelFromData();
    }

    private void UpdateCompilerOutput()
    {
        if (_isLoading) return;

        currentItem.Mechanics.Clear();
        currentItem.GrantedAbilities.Clear();

        if (_rootWorkspaceZone != null)
        {
            foreach (WorkspaceItem instance in _rootWorkspaceZone.Entrants)
            {
                if (instance.Mechanic != null)
                {
                    currentItem.Mechanics.Add(instance.Mechanic);
                }
            }
        }

        string rawExport = ItemData.Export(currentItem);
        string englishOutput = $"Item '{currentItem.entityName ?? "New Item"}' ";

        if (currentItem.tier.HasValue) englishOutput += $"[Tier {currentItem.tier.Value}] ";

        int sideCount = currentItem.Mechanics.Sum(m => m.Positions.Count);
        englishOutput += $"modifies {sideCount} side target(s). ";

        if (!string.IsNullOrEmpty(currentItem.sidesc))
            englishOutput += $"Replaces visual side description with \"{currentItem.sidesc}\".";

        if (_finalStringInput != null) _finalStringInput.text = rawExport;
        if (_englishTranslationText != null) _englishTranslationText.text = englishOutput;
    }

    #endregion

    #region LAYOUT DEFINITIONS (LEFT, MIDDLE, RIGHT)

    private ColumnSpec BuildHierarchyColumn(float rowHeight, float dynamicScrollHeight, float topBarHeight)
    {
        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(topBarHeight, GridCellSpec.CreateLabel("Left_Spacer", "", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Item from Clipboard", 1.0f, LoadItemFromClipboard)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, macroOptions, OnDropdownSelected)),
            new GridRowSpec(dynamicScrollHeight, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("BtnClearWorkspace", "Clear Logic", 1f, ClearWorkspace))
        };

        return new ColumnSpec("Left_Column", 0.0f, 0.32f, rows);
    }

    private List<GridRowSpec> GetMetadataColumnRows(float rowHeight)
    {
        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_Name", "Name", 1.0f, (val) => { currentItem.entityName = val; UpdateCompilerOutput(); })),

            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Tier_Lbl", "Tier:", 0.20f),
                GridCellSpec.CreateSlider("Sld_Tier", -5f, 20f, true, 0.55f, OnTierSliderChanged),
                GridCellSpec.CreateInput("In_TierNum", "0", 0.25f, OnTierInputChanged)
            ),

            new GridRowSpec(45f, GridCellSpec.CreateInput("In_Doc", "Description Override (doc)", 1.0f, (val) => { currentItem.doc = val; UpdateCompilerOutput(); })),
            new GridRowSpec(45f, GridCellSpec.CreateInput("In_SideDesc", "Side Desc Override (sidesc)", 1.0f, (val) => { currentItem.sidesc = val; UpdateCompilerOutput(); })),

            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("Item Img:", 0.20f),
                GridCellSpec.CreateDiceButton("FacBtn_Item", "F", 0.15f, () => OpenFacadeModal()),
                GridCellSpec.CreateInput("In_Img", "Icon Ref", 0.40f, (val) => { currentItem.imageOverride = val; UpdateVisuals(); UpdateCompilerOutput(); }),
                GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.25f, ToggleCustomImagePanel)
            ),

            new GridRowSpec(5f, GridCellSpec.CreateLabel("S_2", "", 1.0f))
        };

        if (showCustomImagePanel)
        {
            rows.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));
        }

        rows.AddRange(new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("H_Lbl", "H", 0.10f),
                GridCellSpec.CreateSlider("Sld_H", -99f, 99f, true, 0.65f, OnHSliderChanged),
                GridCellSpec.CreateInput("In_H", "0", 0.25f, OnHInputChanged)
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("S_Lbl", "S", 0.10f),
                GridCellSpec.CreateSlider("Sld_S", -99f, 99f, true, 0.65f, OnSSliderChanged),
                GridCellSpec.CreateInput("In_S", "0", 0.25f, OnSInputChanged)
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateLabel("V_Lbl", "V", 0.10f),
                GridCellSpec.CreateSlider("Sld_V", -99f, 99f, true, 0.65f, OnVSliderChanged),
                GridCellSpec.CreateInput("In_V", "0", 0.25f, OnVInputChanged)
            ),

            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_P", "Particle (p)", 0.50f, (val) => { currentItem.p = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_B", "Border (b)", 0.50f, (val) => { currentItem.b = val; UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateInput("In_Rect", "Rect (rect)", 0.50f, (val) => { currentItem.rect = val; UpdateCompilerOutput(); }),
                GridCellSpec.CreateInput("In_Draw", "Draw Mode (draw)", 0.50f, (val) => { currentItem.draw = val; UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_THue", "Target Hue (thue)", 1.0f, (val) => { currentItem.thue = val; UpdateCompilerOutput(); })),

            // TODO: Bind to structural logic when mapping is built
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_ClearIcon", "Clear Base Icon", 0.50f, (val) => UpdateCompilerOutput()),
                GridCellSpec.CreateToggle("Tgl_ClearDesc", "Clear Base Desc", 0.50f, (val) => UpdateCompilerOutput())
            )
        });

        return rows;
    }

    private List<GridRowSpec> GetModularWorkspaceRows(float rowHeight)
    {
        if (_selectedWorkspaceItem == null)
        {
            return new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", "NODE SPECIFICATION INSPECTOR", 1.0f)),
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("Empty_LBL", "Select a logic node from the hierarchy to edit properties.", 1.0f))
            };
        }

        string opName = _selectedWorkspaceItem.OptionName;
        ItemMechanic mechanic = _selectedWorkspaceItem.Mechanic;

        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", $"INSPECTING: {opName.ToUpper()}", 1.0f))
        };

        rows.AddRange(GetSharedTargetRows(rowHeight, mechanic));
        rows.AddRange(GetSharedConstraintRows(rowHeight, mechanic));

        rows.Add(new GridRowSpec(22f, GridCellSpec.CreateLabel("Action_Header_Lbl", "3. Action Payload", 1.0f)));

        if (opName == "Add Keyword")
        {
            rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_KeywordSelect", "Select Keyword:", 1.0f, keywordDatabase, (val) => { mechanic.Operations = new List<string> { "k" }; mechanic.Payload = keywordDatabase[val]; UpdateCompilerOutput(); })));
        }
        else if (opName.Contains("Sticker") || opName.Contains("Buff"))
        {
            rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_ItemSelect", "Select Item/Buff:", 1.0f, itemDatabase, (val) => { mechanic.Operations = new List<string> { "sticker" }; mechanic.Payload = itemDatabase[val]; UpdateCompilerOutput(); })));
        }
        else if (opName.Contains("Summon"))
        {
            rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_SummonEntity", "Summon Entity Tag", 1.0f, (val) => { mechanic.Operations = new List<string> { "cast" }; mechanic.Payload = val; UpdateCompilerOutput(); })));
        }
        else
        {
            int actionIdx = GetActionTypeIndex(mechanic);
            rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_ActionType", "Operation:", 1.0f, actionTypeOptions, (val) => OnActionTypeChanged(val, mechanic))));

            if (actionIdx == 0)
                rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_KeywordSelect", "Select Keyword:", 1.0f, keywordDatabase, (val) => { mechanic.Payload = keywordDatabase[val]; UpdateCompilerOutput(); })));
            else if (actionIdx == 1)
                rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_ItemSelect", "Select Item/Buff:", 1.0f, itemDatabase, (val) => { mechanic.Payload = itemDatabase[val]; UpdateCompilerOutput(); })));
            else if (actionIdx == 2)
                rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_SummonEntity", "Summon Entity Tag", 1.0f, (val) => { mechanic.Payload = val; UpdateCompilerOutput(); })));
        }

        rows.Add(new GridRowSpec(rowHeight, GridCellSpec.CreateToggle("Tgl_IsWrapped", "Enclose Node in Parents ( )", 1.0f, (val) => { mechanic.IsWrapped = val; UpdateCompilerOutput(); })));

        return rows;
    }

    private List<GridRowSpec> GetSharedTargetRows(float rowHeight, ItemMechanic mechanic)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(22f, GridCellSpec.CreateLabel("Faces_Header_Lbl", "1. Target Faces (Where does this go?)", 1.0f)),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_Face_Top", "Top", 0.16f, (val) => { SetPosition(mechanic, "top", val); UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_Face_Bot", "Bot", 0.16f, (val) => { SetPosition(mechanic, "bot", val); UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_Face_Mid", "Mid", 0.16f, (val) => { SetPosition(mechanic, "mid", val); UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_Face_Left", "Left", 0.16f, (val) => { SetPosition(mechanic, "left", val); UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_Face_Right", "Right", 0.16f, (val) => { SetPosition(mechanic, "right", val); UpdateCompilerOutput(); }),
                GridCellSpec.CreateToggle("Tgl_Face_Rightmost", "R-most", 0.20f, (val) => { SetPosition(mechanic, "rightmost", val); UpdateCompilerOutput(); })
            ),
            new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_FacePresets", "Target Presets:", 1.0f, targetFaceOptions, (val) => OnPresetFaceSelected(val, mechanic))),
            new GridRowSpec(6f, GridCellSpec.CreateLabel("S_0", "", 1.0f))
        };
    }

    private List<GridRowSpec> GetSharedConstraintRows(float rowHeight, ItemMechanic mechanic)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(22f, GridCellSpec.CreateLabel("Conditions_Header_Lbl", "2. Execution Constraints (TogRes Mapping)", 1.0f)),
            new GridRowSpec(rowHeight,
                // TODO: Map to mechanic logic when implementation is ready
                GridCellSpec.CreateDropdown("Drop_Condition", "Requires:", 0.65f, conditionOptions, (val) => UpdateCompilerOutput()),
                GridCellSpec.CreateToggle("Tgl_Multiplier", "x2 Conditional", 0.35f, (val) => UpdateCompilerOutput())
            ),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateToggle("Tgl_TogTime", "Continuous (togtime)", 0.50f, (val) => UpdateCompilerOutput()),
                GridCellSpec.CreateToggle("Tgl_TogFri", "Invert Target (togfri)", 0.50f, (val) => UpdateCompilerOutput())
            ),
            new GridRowSpec(6f, GridCellSpec.CreateLabel("S_1", "", 1.0f))
        };
    }

    #endregion

    #region WORKSPACE HIERARCHY LOGIC

    private void SelectWorkspaceBlock(WorkspaceItem instance)
    {
        _selectedWorkspaceItem = instance;
        RebuildMiddlePanel();
    }

    private void ClearWorkspaceLogic()
    {
        _selectedWorkspaceItem = null;
        if (_workspaceContent != null)
        {
            foreach (Transform child in _workspaceContent) Destroy(child.gameObject);
        }
        if (_rootWorkspaceZone != null && _rootWorkspaceZone.Entrants != null)
            _rootWorkspaceZone.Entrants.Clear();
    }

    private void ClearWorkspace()
    {
        ClearWorkspaceLogic();
        currentItem = new ItemData();

        PopulateRightPanelFromData();
        RebuildMiddlePanel();
        UpdateCompilerOutput();
    }

    private void ConfigureWorkspace()
    {
        if (generatedScreen == null || !generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences refs)) return;

        if (refs.ScrollViews.TryGetValue("WorkspaceScrollArea", out _workspaceScroll))
        {
            _workspaceContent = _workspaceScroll.content;

            VerticalLayoutGroup layout = _workspaceContent.gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = _workspaceContent.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = _workspaceContent.gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = _workspaceContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _rootWorkspaceZone = _workspaceContent.gameObject.GetComponent<ReorderableZone>();
            if (_rootWorkspaceZone == null) _rootWorkspaceZone = _workspaceContent.gameObject.AddComponent<ReorderableZone>();

            RectTransform viewportRt = _workspaceScroll.viewport;
            if (viewportRt != null)
            {
                ContextMenuTrigger trigger = viewportRt.gameObject.GetComponent<ContextMenuTrigger>();
                if (trigger == null) trigger = viewportRt.gameObject.AddComponent<ContextMenuTrigger>();

                FilteredDropdown dropdownPrefab = uiGenerator.filteredDropdown != null ? uiGenerator.filteredDropdown.GetComponent<FilteredDropdown>() : null;
                trigger.Initialize(generatedScreen.RootWrapper, dropdownPrefab, new List<string>(macroOptions), AddBlockToWorkspace);
            }
        }
    }

    private void AddBlockToWorkspace(string optionName)
    {
        if (_workspaceContent == null || _rootWorkspaceZone == null) return;

        GameObject btnGo = Instantiate(uiGenerator.buttonPrefab);
        btnGo.name = $"WorkspaceBtn_{optionName}";

        WorkspaceItem workspaceItem = btnGo.AddComponent<WorkspaceItem>();
        workspaceItem.OptionName = optionName;
        workspaceItem.Mechanic = new ItemMechanic();
        AssignDefaultMechanicPayload(workspaceItem.Mechanic, optionName);

        TextMeshProUGUI btnText = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
        {
            btnText.text = optionName;
            btnText.margin = new Vector4(8f, 0f, 32f, 0f);
        }

        Button selectBtn = btnGo.GetComponent<Button>();
        if (selectBtn != null) selectBtn.onClick.AddListener(() => SelectWorkspaceBlock(workspaceItem));

        _rootWorkspaceZone.AddEntrant(workspaceItem);

        GameObject deleteBtnGo = Instantiate(uiGenerator.buttonPrefab, btnGo.transform);
        deleteBtnGo.name = "DeleteBtn";

        LayoutElement lay = deleteBtnGo.GetComponent<LayoutElement>();
        if (lay == null) lay = deleteBtnGo.AddComponent<LayoutElement>();
        lay.ignoreLayout = true;

        RectTransform delRt = deleteBtnGo.GetComponent<RectTransform>();
        delRt.anchorMin = new Vector2(1f, 0.5f);
        delRt.anchorMax = new Vector2(1f, 0.5f);
        delRt.pivot = new Vector2(1f, 0.5f);
        delRt.anchoredPosition = new Vector2(-6f, 0f);
        delRt.sizeDelta = new Vector2(24f, 24f);

        TextMeshProUGUI delText = deleteBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (delText != null)
        {
            delText.text = "X";
            delText.fontSize = 12;
            delText.color = Color.red;
        }

        Button delBtn = deleteBtnGo.GetComponent<Button>();
        if (delBtn != null)
        {
            delBtn.onClick.RemoveAllListeners();
            delBtn.onClick.AddListener(() => RemoveBlockFromWorkspace(workspaceItem));
        }

        Canvas.ForceUpdateCanvases();
        if (_workspaceScroll != null) _workspaceScroll.verticalNormalizedPosition = 0f;

        UpdateCompilerOutput();
    }

    private void RemoveBlockFromWorkspace(WorkspaceItem item)
    {
        if (item == null) return;

        if (_rootWorkspaceZone != null && _rootWorkspaceZone.Entrants != null)
            _rootWorkspaceZone.Entrants.Remove(item);

        if (_selectedWorkspaceItem == item)
        {
            _selectedWorkspaceItem = null;
            RebuildMiddlePanel();
        }

        Destroy(item.gameObject);
        StartCoroutine(ExecuteRecompileNextFrame());
    }

    private System.Collections.IEnumerator ExecuteRecompileNextFrame()
    {
        yield return null;
        UpdateCompilerOutput();
    }

    private void LoadItemFromClipboard()
    {
        string clipboardContent = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrEmpty(clipboardContent)) return;

        ItemData parsedItem = null;
        try { parsedItem = ItemData.Parse(clipboardContent); }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ItemUI] Paste rejected: {ex.Message}");
            return;
        }

        if (parsedItem == null) return;

        _isLoading = true;
        ClearWorkspaceLogic();

        currentItem = parsedItem;
        PopulateRightPanelFromData();

        var mechanicsToLoad = new List<ItemMechanic>(currentItem.Mechanics);
        foreach (var mechanic in mechanicsToLoad)
        {
            if (mechanic.Operations.Count == 0 && mechanic.Positions.Count == 0) continue;

            string op = mechanic.Operations.FirstOrDefault() ?? "Add Keyword";
            AddBlockToWorkspace(MapOperatorToMacroName(op));

            if (_rootWorkspaceZone.Entrants.Count > 0)
            {
                var loadedItem = _rootWorkspaceZone.Entrants[_rootWorkspaceZone.Entrants.Count - 1] as WorkspaceItem;
                if (loadedItem != null) loadedItem.Mechanic = mechanic;
            }
        }

        RebuildMiddlePanel();

        _isLoading = false;
        UpdateVisuals();
        UpdateCompilerOutput();
    }

    #endregion

    #region UTILITY & VALUE EVENT CALLBACKS

    private void SetPosition(ItemMechanic mechanic, string pos, bool state)
    {
        if (state && !mechanic.Positions.Contains(pos)) mechanic.Positions.Add(pos);
        else if (!state) mechanic.Positions.Remove(pos);
    }

    private bool HasOp(ItemMechanic mech, string opName)
    {
        if (mech == null || mech.Operations == null) return false;
        return mech.Operations.Any(op => op.Equals(opName, StringComparison.OrdinalIgnoreCase));
    }

    private int GetActionTypeIndex(ItemMechanic mechanic)
    {
        if (HasOp(mechanic, "k")) return 0;
        if (HasOp(mechanic, "sticker")) return 1;
        if (HasOp(mechanic, "cast")) return 2;
        return 0;
    }

    private string MapOperatorToMacroName(string op)
    {
        if (string.IsNullOrEmpty(op)) return "Add Keyword";
        string lower = op.ToLower();
        if (lower == "k") return "Add Keyword";
        if (lower == "sticker") return "Create Buff/Debuff (Sticker)";
        if (lower == "cast") return "Summon Entity";
        return "Add Keyword";
    }

    private void AssignDefaultMechanicPayload(ItemMechanic mechanic, string optionName)
    {
        mechanic.Operations.Clear();
        if (optionName.Contains("Keyword")) mechanic.Operations.Add("k");
        else if (optionName.Contains("Buff") || optionName.Contains("Sticker")) mechanic.Operations.Add("sticker");
        else if (optionName.Contains("Summon")) mechanic.Operations.Add("cast");
        else mechanic.Operations.Add("k");
    }

    private void OnDropdownSelected(int index)
    {
        if (index <= 0) return;

        if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("Left_Column", out var leftRefs))
            SetDropdownValue(leftRefs, "ModDropdown", 0); // reset it for next use

        AddBlockToWorkspace(macroOptions[index]);
    }

    private void OnPresetFaceSelected(int index, ItemMechanic mechanic)
    {
        if (index == 0) return;

        bool t = false, b = false, m = false, l = false, r = false, rm = false;

        switch (index)
        {
            case 1: t = b = m = l = r = rm = true; break;
            case 2: t = true; break;
            case 3: b = true; break;
            case 4: m = true; break;
            case 5: l = true; break;
            case 6: r = true; break;
            case 7: rm = true; break;
            case 8: t = b = true; break;
            case 9: t = b = true; break;
            case 10: l = m = r = true; break;
        }

        SetPosition(mechanic, "top", t);
        SetPosition(mechanic, "bot", b);
        SetPosition(mechanic, "mid", m);
        SetPosition(mechanic, "left", l);
        SetPosition(mechanic, "right", r);
        SetPosition(mechanic, "rightmost", rm);

        SetToggleValue(_midRefs, "Tgl_Face_Top", t);
        SetToggleValue(_midRefs, "Tgl_Face_Bot", b);
        SetToggleValue(_midRefs, "Tgl_Face_Mid", m);
        SetToggleValue(_midRefs, "Tgl_Face_Left", l);
        SetToggleValue(_midRefs, "Tgl_Face_Right", r);
        SetToggleValue(_midRefs, "Tgl_Face_Rightmost", rm);

        UpdateCompilerOutput();
    }

    private void OnActionTypeChanged(int index, ItemMechanic mechanic)
    {
        mechanic.Operations.Clear();
        if (index == 0) mechanic.Operations.Add("k");
        else if (index == 1) mechanic.Operations.Add("sticker");
        else if (index == 2) mechanic.Operations.Add("cast");

        RebuildMiddlePanel();
        UpdateCompilerOutput();
    }

    private void OnTierSliderChanged(float val)
    {
        if (_isLoading) return;
        currentItem.tier = (int)val;
        SetInputValue(_rightRefs, "In_TierNum", val.ToString("0"));
        UpdateCompilerOutput();
    }

    private void OnTierInputChanged(string text)
    {
        if (_isLoading) return;
        if (int.TryParse(text, out int parsedVal))
        {
            int clamped = Mathf.Clamp(parsedVal, -5, 20);
            currentItem.tier = clamped;
            SetSliderValue(_rightRefs, "Sld_Tier", clamped);
            UpdateCompilerOutput();
        }
    }

    private void OnHSliderChanged(float val) { if (_isLoading) return; currentItem.h = (int)val; SetInputValue(_rightRefs, "In_H", val.ToString("0")); UpdateCompilerOutput(); }
    private void OnSSliderChanged(float val) { if (_isLoading) return; currentItem.s = (int)val; SetInputValue(_rightRefs, "In_S", val.ToString("0")); UpdateCompilerOutput(); }
    private void OnVSliderChanged(float val) { if (_isLoading) return; currentItem.v = (int)val; SetInputValue(_rightRefs, "In_V", val.ToString("0")); UpdateCompilerOutput(); }

    private void OnHInputChanged(string text) { if (_isLoading) return; if (int.TryParse(text, out int val)) { currentItem.h = Mathf.Clamp(val, -99, 99); SetSliderValue(_rightRefs, "Sld_H", currentItem.h); UpdateCompilerOutput(); } }
    private void OnSInputChanged(string text) { if (_isLoading) return; if (int.TryParse(text, out int val)) { currentItem.s = Mathf.Clamp(val, -99, 99); SetSliderValue(_rightRefs, "Sld_S", currentItem.s); UpdateCompilerOutput(); } }
    private void OnVInputChanged(string text) { if (_isLoading) return; if (int.TryParse(text, out int val)) { currentItem.v = Mathf.Clamp(val, -99, 99); SetSliderValue(_rightRefs, "Sld_V", currentItem.v); UpdateCompilerOutput(); } }

    private void CopyToClipboard()
    {
        if (_finalStringInput != null && !string.IsNullOrEmpty(_finalStringInput.text))
        {
            GUIUtility.systemCopyBuffer = _finalStringInput.text;
            uiGenerator.CreatePopup("Successfully compiled item string and copied to system clipboard!");
        }
    }

    private void ToggleCustomImagePanel()
    {
        showCustomImagePanel = !showCustomImagePanel;
        RebuildRightColumn();
    }

    private void UpdateVisuals()
    {
        SetInputValue(_rightRefs, "In_Img", currentItem.imageOverride);

        if (_rightRefs != null && _rightRefs.Buttons.TryGetValue("FacBtn_Item", out var facBtn))
        {
            Sprite s = EntityUIHelpers.GetFacadeSprite(currentItem.imageOverride);
            SetButtonIcon(facBtn, s);
        }
    }

    #endregion

    #region BOILERPLATE UI/LAYOUT HELPER METHODS

    private float CalculateScrollExtraHeight(RectTransform content)
    {
        float extraHeight = 0f;
        var layoutGroup = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
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
        var layoutElement = target.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null) layoutElement = target.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
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

    private void SetButtonIcon(Button btn, Sprite sprite)
    {
        if (btn == null) return;
        ImageButton imgBtn = btn.GetComponent<ImageButton>();
        if (imgBtn != null && imgBtn.image != null)
        {
            if (sprite != null) { imgBtn.image.sprite = sprite; imgBtn.image.gameObject.SetActive(true); }
            else { imgBtn.image.sprite = null; imgBtn.image.gameObject.SetActive(false); }
            return;
        }

        Transform iconTransform = btn.transform.Find("Icon");
        Image targetImg = iconTransform != null ? iconTransform.GetComponent<Image>() : btn.image;
        if (targetImg != null)
        {
            if (sprite != null) { targetImg.sprite = sprite; targetImg.color = Color.white; }
            else { targetImg.sprite = null; targetImg.color = new Color(1, 1, 1, 0.2f); }
        }
    }

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
            new GridRowSpec(25f, GridCellSpec.CreateLabel("Lbl_English", "[English Translation Engine]", 1.0f)),
            new GridRowSpec(40f, GridCellSpec.CreateInput("Input_RawString", "Compiled text output...", 0.85f, null, InputAlignment.Center),
                                 GridCellSpec.CreateButton("Btn_Copy", "COPY STRING", 0.15f, CopyToClipboard))
        };

        GridReferences bottomRefs = uiGenerator.RebuildGrid(rt, rows, useMargins: true);
        _englishTranslationText = topBar.GetComponentInChildren<TextMeshProUGUI>();
        bottomRefs.Inputs.TryGetValue("Input_RawString", out _finalStringInput);
    }

    private void ApplyDynamicLayoutConstraints()
    {
        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;
        float spacing = uiGenerator != null ? uiGenerator.rowSpacing : 8f;
        float topBarHeight = 75f;

        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            if (leftRefs.Buttons.TryGetValue("BtnClearWorkspace", out Button compileBtn))
            {
                RectTransform btnRt = compileBtn.GetComponent<RectTransform>();
                RectTransform rowContainerRt = btnRt.parent as RectTransform;
                if (rowContainerRt != null)
                {
                    ConfigureFlexibleLayout(rowContainerRt);
                    rowContainerRt.anchorMin = new Vector2(0f, 0f);
                    rowContainerRt.anchorMax = new Vector2(1f, 0f);
                    rowContainerRt.pivot = new Vector2(0.5f, 0f);
                    rowContainerRt.anchoredPosition = new Vector2(0f, spacing);
                    rowContainerRt.sizeDelta = new Vector2(0f, rowHeight);
                }
            }

            if (leftRefs.ScrollViews.TryGetValue("WorkspaceScrollArea", out ScrollRect scroll))
            {
                RectTransform scrollRt = scroll.GetComponent<RectTransform>();
                RectTransform rowContainerRt = scrollRt.parent as RectTransform;

                float topOffset = topBarHeight + (rowHeight * 2f) + (spacing * 3f);
                float bottomOffset = rowHeight + (spacing * 2f);

                ConfigureFlexibleLayout(rowContainerRt);
                ConfigureFlexibleLayout(scrollRt);
                StretchToParent(rowContainerRt, topOffset, bottomOffset);
                StretchToParent(scrollRt, 0f, 0f);
            }
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Middle_Column", out GridReferences midRefs) &&
            midRefs.ScrollViews.TryGetValue("MiddleScrollArea", out ScrollRect midScroll))
        {
            RectTransform scrollRt = midScroll.GetComponent<RectTransform>();
            RectTransform rowContainerRt = scrollRt.parent as RectTransform;
            ConfigureFlexibleLayout(rowContainerRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowContainerRt, topBarHeight + spacing, 0f);
            StretchToParent(scrollRt, 0f, 0f);
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rightRefs) &&
            rightRefs.ScrollViews.TryGetValue("RightScrollArea", out ScrollRect rightScroll))
        {
            RectTransform scrollRt = rightScroll.GetComponent<RectTransform>();
            RectTransform rowContainerRt = scrollRt.parent as RectTransform;
            ConfigureFlexibleLayout(rowContainerRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowContainerRt, topBarHeight + spacing, 0f);
            StretchToParent(scrollRt, 0f, 0f);
        }
    }

    private void OpenFacadeModal()
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId) && parsedId > 187)
                        return IconPickerModal.GetCleanLeafName(sprite.name);
                }
                return sprite.name;
            },
            GetTooltip = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId) && parsedId > 187)
                        return $"Community Facade [{IconPickerModal.GetCleanLeafName(sprite.name)}]";
                }
                return sprite.name;
            },
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        string prefix = parts[0].ToLower();
                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31) currentItem.imageOverride = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27) currentItem.imageOverride = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17) currentItem.imageOverride = $"bas{248 + parsedId}";
                        else currentItem.imageOverride = $"{parts[0]}{parts[1]}";
                    }
                    else currentItem.imageOverride = sprite.name;

                    UpdateVisuals();
                    UpdateCompilerOutput();
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    #endregion
}

/*
private List<GridRowSpec> GetWorkspaceColumnRows(float rowHeight)
{
    List<GridRowSpec> rows = new List<GridRowSpec>
    {
        new GridRowSpec(22f, GridCellSpec.CreateLabel("Faces_Header_Lbl", "1. Target Faces (Where does this go?)", 1.0f)),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_Face_Top", "Top", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Bot", "Bot", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Mid", "Mid", 0.16f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_Face_Left", "Left", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Right", "Right", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Rightmost", "R-most", 0.20f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_FacePresets", "Target Presets:", 1.0f, targetFaceOptions, OnPresetFaceSelected)),

        new GridRowSpec(6f, GridCellSpec.CreateLabel("S_0", "", 1.0f)),

        new GridRowSpec(22f, GridCellSpec.CreateLabel("Conditions_Header_Lbl", "2. Execution Constraints (TogRes Mapping)", 1.0f)),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateDropdown("Drop_Condition", "Requires:", 0.65f, conditionOptions, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Multiplier", "x2 Conditional", 0.35f, (val) => UpdateCompilerOutput())
        ),

        new GridRowSpec(6f, GridCellSpec.CreateLabel("S_1", "", 1.0f)),

        new GridRowSpec(22f, GridCellSpec.CreateLabel("Action_Header_Lbl", "3. Action Payload", 1.0f)),
        new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_ActionType", "Operation:", 1.0f, actionTypeOptions, OnActionTypeChanged)),

        new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_KeywordSelect", "Select Keyword:", 1.0f, keywordDatabase, (val) => UpdateCompilerOutput())),
        new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_ItemSelect", "Select Item/Buff:", 1.0f, itemDatabase, (val) => UpdateCompilerOutput())),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_SummonEntity", "Summon Entity Tag", 1.0f, (val) => UpdateCompilerOutput())),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_TogTime", "Continuous (togtime)", 0.50f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_TogFri", "Invert Target (togfri)", 0.50f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_IsWrapped", "Enclose Node in Parents ( )", 1.0f, (val) => UpdateCompilerOutput())
        )
    };

    return rows;
}
private void BindUIReferences(GridReferences midRefs, GridReferences rightRefs)
{
    if (generatedScreen == null) return;

    if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
    {
        leftRefs.Dropdowns.TryGetValue("ModDropdown", out _leftDirectiveDropdown);
    }

    if (midRefs != null)
    {
        midRefs.Toggles.TryGetValue("Tgl_Face_Top", out _tglFaceTop);
        midRefs.Toggles.TryGetValue("Tgl_Face_Bot", out _tglFaceBot);
        midRefs.Toggles.TryGetValue("Tgl_Face_Mid", out _tglFaceMid);
        midRefs.Toggles.TryGetValue("Tgl_Face_Left", out _tglFaceLeft);
        midRefs.Toggles.TryGetValue("Tgl_Face_Right", out _tglFaceRight);
        midRefs.Toggles.TryGetValue("Tgl_Face_Rightmost", out _tglFaceRightmost);
        midRefs.Dropdowns.TryGetValue("Drop_FacePresets", out _dropFacePresets);
        midRefs.Dropdowns.TryGetValue("Drop_Condition", out _dropCondition);
        midRefs.Toggles.TryGetValue("Tgl_Multiplier", out _tglMultiplier);
        midRefs.Dropdowns.TryGetValue("Drop_ActionType", out _dropActionType);
        midRefs.FilteredDropdowns.TryGetValue("Drop_KeywordSelect", out _dropKeywordSelect);
        midRefs.FilteredDropdowns.TryGetValue("Drop_ItemSelect", out _dropItemSelect);
        midRefs.Inputs.TryGetValue("In_SummonEntity", out _inputSummonEntity);
        midRefs.Toggles.TryGetValue("Tgl_TogTime", out _tglTogTime);
        midRefs.Toggles.TryGetValue("Tgl_TogFri", out _tglTogFri);
        midRefs.Toggles.TryGetValue("Tgl_IsWrapped", out _tglIsWrapped);
    }

    if (rightRefs != null)
    {
        rightRefs.Inputs.TryGetValue("In_Name", out _inputName);
        rightRefs.Sliders.TryGetValue("Sld_Tier", out _sldTier);
        rightRefs.Inputs.TryGetValue("In_TierNum", out _inputTierNum);
        rightRefs.Inputs.TryGetValue("In_Doc", out _inputDoc);
        rightRefs.Inputs.TryGetValue("In_SideDesc", out _inputSideDesc);
        rightRefs.Inputs.TryGetValue("In_Img", out _inputImg);

        rightRefs.Dropdowns.TryGetValue("Drop_ColorMode", out _dropColorMode);
        rightRefs.Sliders.TryGetValue("Sld_H", out _sldH);
        rightRefs.Sliders.TryGetValue("Sld_S", out _sldS);
        rightRefs.Sliders.TryGetValue("Sld_V", out _sldV);
        rightRefs.Sliders.TryGetValue("Sld_SingleHue", out _sldSingleHue);
        rightRefs.Inputs.TryGetValue("In_HSL", out _inputHSL);

        rightRefs.Inputs.TryGetValue("In_P", out _inputP);
        rightRefs.Inputs.TryGetValue("In_B", out _inputB);
        rightRefs.Inputs.TryGetValue("In_Rect", out _inputRect);
        rightRefs.Inputs.TryGetValue("In_Draw", out _inputDraw);
        rightRefs.Inputs.TryGetValue("In_THue", out _inputTHue);

        rightRefs.Toggles.TryGetValue("Tgl_ClearIcon", out _tglClearIcon);
        rightRefs.Toggles.TryGetValue("Tgl_ClearDesc", out _tglClearDesc);
    }
}
private void UpdateColorPanelVisibility(int selectedMode)
{
    if (_sldH) _sldH.gameObject.SetActive(selectedMode == 0);
    if (_sldS) _sldS.gameObject.SetActive(selectedMode == 0);
    if (_sldV) _sldV.gameObject.SetActive(selectedMode == 0);
    if (_sldSingleHue) _sldSingleHue.gameObject.SetActive(selectedMode == 1);
    if (_inputHSL) _inputHSL.gameObject.SetActive(selectedMode == 2);
}
private ColumnSpec BuildWorkspaceColumn(float rowHeight)
{
    List<GridRowSpec> rows = new List<GridRowSpec>
    {
        new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", "NODE SPECIFICATION INSPECTOR", 1.0f)),

        new GridRowSpec(22f, GridCellSpec.CreateLabel("Faces_Header_Lbl", "1. Target Faces (Where does this go?)", 1.0f)),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_Face_Top", "Top", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Bot", "Bot", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Mid", "Mid", 0.16f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_Face_Left", "Left", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Right", "Right", 0.16f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Face_Rightmost", "R-most", 0.20f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_FacePresets", "Target Presets:", 1.0f, targetFaceOptions, OnPresetFaceSelected)),

        new GridRowSpec(6f, GridCellSpec.CreateLabel("S_0", "", 1.0f)),

        new GridRowSpec(22f, GridCellSpec.CreateLabel("Conditions_Header_Lbl", "2. Execution Constraints (TogRes Mapping)", 1.0f)),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateDropdown("Drop_Condition", "Requires:", 0.65f, conditionOptions, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_Multiplier", "x2 Conditional", 0.35f, (val) => UpdateCompilerOutput())
        ),

        new GridRowSpec(6f, GridCellSpec.CreateLabel("S_1", "", 1.0f)),

        new GridRowSpec(22f, GridCellSpec.CreateLabel("Action_Header_Lbl", "3. Action Payload", 1.0f)),
        new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_ActionType", "Operation:", 1.0f, actionTypeOptions, OnActionTypeChanged)),

        new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_KeywordSelect", "Select Keyword:", 1.0f, keywordDatabase, (val) => UpdateCompilerOutput())),
        new GridRowSpec(rowHeight, GridCellSpec.CreateFilteredDropdown("Drop_ItemSelect", "Select Item/Buff:", 1.0f, itemDatabase, (val) => UpdateCompilerOutput())),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_SummonEntity", "Summon Entity Tag", 1.0f, (val) => UpdateCompilerOutput())),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_TogTime", "Continuous (togtime)", 0.50f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_TogFri", "Invert Target (togfri)", 0.50f, (val) => UpdateCompilerOutput())
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_IsWrapped", "Enclose Node in Parents ( )", 1.0f, (val) => UpdateCompilerOutput())
        )
    };

    return new ColumnSpec("Middle_Column", 0.33f, 0.69f, rows);
}
private ColumnSpec BuildMetadataColumn(float rowHeight)
{
    List<GridRowSpec> rows = new List<GridRowSpec>
    {
        new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("METADATA_TITLE", "GLOBAL METADATA & AESTHETICS", 1.0f)),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_Name", "Name", 1.0f, (val) => { currentItem.entityName = val; UpdateCompilerOutput(); })),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateLabel("Tier_Lbl", "Tier:", 0.20f),
            GridCellSpec.CreateSlider("Sld_Tier", -5f, 20f, true, 0.55f, OnTierSliderChanged),
            GridCellSpec.CreateInput("In_TierNum", "0", 0.25f, OnTierInputChanged)
        ),

        new GridRowSpec(45f, GridCellSpec.CreateInput("In_Doc", "Description Override (doc)", 1.0f, (val) => { currentItem.doc = val; UpdateCompilerOutput(); })),
        new GridRowSpec(45f, GridCellSpec.CreateInput("In_SideDesc", "Side Desc Override (sidesc)", 1.0f, (val) => { currentItem.sidesc = val; UpdateCompilerOutput(); })),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_Img", "Icon Reference (img.any)", 1.0f, (val) => { currentItem.imageOverride = val; UpdateCompilerOutput(); })),

        new GridRowSpec(5f, GridCellSpec.CreateLabel("S_2", "", 1.0f)),

        new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_ColorMode", "Color Format:", 1.0f, colorModeOptions, OnColorModeDropdownChanged)),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateLabel("H_Lbl", "H", 0.10f),
            GridCellSpec.CreateSlider("Sld_H", -99f, 99f, true, 0.65f, OnHSliderChanged),
            GridCellSpec.CreateInput("In_H", "0", 0.25f, OnHInputChanged)
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateLabel("S_Lbl", "S", 0.10f),
            GridCellSpec.CreateSlider("Sld_S", -99f, 99f, true, 0.65f, OnSSliderChanged),
            GridCellSpec.CreateInput("In_S", "0", 0.25f, OnSInputChanged)
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateLabel("V_Lbl", "V", 0.10f),
            GridCellSpec.CreateSlider("Sld_V", -99f, 99f, true, 0.65f, OnVSliderChanged),
            GridCellSpec.CreateInput("In_V", "0", 0.25f, OnVInputChanged)
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateLabel("Hue_Lbl", "Hue", 0.15f),
            GridCellSpec.CreateSlider("Sld_SingleHue", 0f, 360f, true, 0.85f, (val) => { currentItem.hue = (int)val; UpdateCompilerOutput(); })
        ),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_HSL", "HSL String Configuration", 1.0f, (val) => { currentItem.hsl = val; UpdateCompilerOutput(); })),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateInput("In_P", "Particle (p)", 0.50f, (val) => { currentItem.p = val; UpdateCompilerOutput(); }),
            GridCellSpec.CreateInput("In_B", "Border (b)", 0.50f, (val) => { currentItem.b = val; UpdateCompilerOutput(); })
        ),
        new GridRowSpec(rowHeight,
            GridCellSpec.CreateInput("In_Rect", "Rect (rect)", 0.50f, (val) => { currentItem.rect = val; UpdateCompilerOutput(); }),
            GridCellSpec.CreateInput("In_Draw", "Draw Mode (draw)", 0.50f, (val) => { currentItem.draw = val; UpdateCompilerOutput(); })
        ),
        new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_THue", "Target Hue (thue)", 1.0f, (val) => { currentItem.thue = val; UpdateCompilerOutput(); })),

        new GridRowSpec(rowHeight,
            GridCellSpec.CreateToggle("Tgl_ClearIcon", "Clear Base Icon", 0.50f, (val) => UpdateCompilerOutput()),
            GridCellSpec.CreateToggle("Tgl_ClearDesc", "Clear Base Desc", 0.50f, (val) => UpdateCompilerOutput())
        )
    };

    return new ColumnSpec("Right_Column", 0.70f, 1.0f, rows);
}
private void BuildBottomCompilerBar(float height)
{
    GameObject bottomBar = new GameObject("BottomCompilerBar", typeof(RectTransform), typeof(Image));
    RectTransform rt = bottomBar.GetComponent<RectTransform>();

    // Make it a sibling to the main layout to prevent it acting as an overlay
    if (generatedScreen.RootWrapper != null && generatedScreen.RootWrapper.parent != null)
        rt.SetParent(generatedScreen.RootWrapper.parent, false);
    else
        rt.SetParent(generatedScreen.RootWrapper, false);

    rt.anchorMin = new Vector2(0f, 0f);
    rt.anchorMax = new Vector2(1f, 0f);
    rt.pivot = new Vector2(0.5f, 0f);
    rt.sizeDelta = new Vector2(0f, height);

    bottomBar.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

    List<GridRowSpec> rows = new List<GridRowSpec>
    {
        new GridRowSpec(25f, GridCellSpec.CreateLabel("Lbl_English", "[English Translation Engine]", 1.0f)),
        new GridRowSpec(40f, GridCellSpec.CreateInput("Input_RawString", "Compiled text output...", 0.85f, null, InputAlignment.Center),
                                GridCellSpec.CreateButton("Btn_Copy", "COPY STRING", 0.15f, CopyToClipboard))
    };

    GridReferences bottomRefs = uiGenerator.RebuildGrid(rt, rows, useMargins: true);
    _englishTranslationText = bottomBar.GetComponentInChildren<TextMeshProUGUI>();
    bottomRefs.Inputs.TryGetValue("Input_RawString", out _finalStringInput);
}
private void SetupScrollViewContent(RectTransform content)
{
    VerticalLayoutGroup vlg = content.gameObject.GetComponent<VerticalLayoutGroup>();
    if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
    vlg.spacing = 8f;
    vlg.padding = new RectOffset(8, 8, 8, 8);
    vlg.childControlHeight = true;
    vlg.childControlWidth = true;
    vlg.childForceExpandHeight = false;
    vlg.childForceExpandWidth = true;

    ContentSizeFitter csf = content.gameObject.GetComponent<ContentSizeFitter>();
    if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
}
private void AddBlockWithMechanic(ItemMechanic mechanic)
{
    if (_workspaceContent == null || _rootWorkspaceZone == null) return;

    GameObject btnGo = Instantiate(uiGenerator.buttonPrefab);

    string labelName = "Add Keyword";
    if (HasOp(mechanic, "sticker")) labelName = "Create Buff/Debuff (Sticker)";
    else if (HasOp(mechanic, "cast")) labelName = "Summon Entity";

    btnGo.name = $"WorkspaceBtn_{labelName}";

    WorkspaceItem workspaceItem = btnGo.AddComponent<WorkspaceItem>();
    workspaceItem.OptionName = labelName;
    workspaceItem.Mechanic = mechanic;

    TextMeshProUGUI btnText = btnGo.GetComponentInChildren<TextMeshProUGUI>();
    if (btnText != null) btnText.text = labelName;

    Button selectBtn = btnGo.GetComponent<Button>();
    if (selectBtn != null) selectBtn.onClick.AddListener(() => SelectWorkspaceBlock(workspaceItem));

    _rootWorkspaceZone.AddEntrant(workspaceItem);
}
}

*/