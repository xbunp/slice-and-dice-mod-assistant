using ModEditor.Compiler;
using ModEditor.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModEditor
{
    public class ItemUI : RootUI
    {
        public static ItemUI Instance { get; private set; }

        private ItemData currentItem;

        // Tracks the connection between spawned UI block containers and their logical compiler/UI models
        private readonly List<BlockInstance> _activeUIBlocks = new List<BlockInstance>();
        private BlockInstance? _selectedBlockInstance; // Contextually track which block is currently inspected

        [Header("Left Column: Drag & Drop Tree")]
        private ScrollRect _workspaceScroll;
        private Transform _workspaceContent;
        private TMP_Dropdown _leftDirectiveDropdown;
        private BlockDropZone _rootWorkspaceZone;

        [Header("Bottom Bar: Compiler Panel")]
        private TMP_InputField _finalStringInput;
        private TextMeshProUGUI _englishTranslationText;

        [Header("Middle Column: Workspace Inspector Inputs")]
        private Toggle _tglFaceTop;
        private Toggle _tglFaceBot;
        private Toggle _tglFaceMid;
        private Toggle _tglFaceLeft;
        private Toggle _tglFaceRight;
        private Toggle _tglFaceRightmost;
        private TMP_Dropdown _dropFacePresets;
        private TMP_Dropdown _dropCondition;
        private Toggle _tglMultiplier;
        private TMP_Dropdown _dropActionType;
        private FilteredDropdown _dropKeywordSelect;
        private FilteredDropdown _dropItemSelect;
        private TMP_InputField _inputSummonEntity;
        private Toggle _tglTogTime;
        private Toggle _tglTogFri;
        private Toggle _tglIsWrapped; // Added IsWrapped representation

        [Header("Right Column: Metadata & Aesthetics")]
        private TMP_InputField _inputName;
        private Slider _sldTier;
        private TMP_InputField _inputTierNum;
        private TMP_InputField _inputDoc;
        private TMP_InputField _inputSideDesc; // Added sidesc field
        private TMP_InputField _inputImg;

        // Color mode selectors
        private TMP_Dropdown _dropColorMode;
        private GameObject _panelHSV;
        private GameObject _panelHue;
        private GameObject _panelHSL;
        private Slider _sldH;
        private Slider _sldS;
        private Slider _sldV;
        private Slider _sldSingleHue;
        private TMP_InputField _inputHSL;

        // Aesthetics layout extra properties
        private TMP_InputField _inputP;
        private TMP_InputField _inputB;
        private TMP_InputField _inputRect;
        private TMP_InputField _inputDraw;
        private TMP_InputField _inputTHue;

        private Toggle _tglClearIcon;
        private Toggle _tglClearDesc;

        // Configuration Arrays
        private readonly string[] macroOptions = {
            "Quick Add Logic Node...",
            "Add Keyword",
            "Create Buff/Debuff (Sticker)",
            "Summon Entity",
            "Apply Passive (Custom Side)",
            "Boss Phase (TriggerHP)",
            "Raw Engine Injection"
        };
        private readonly string[] conditionOptions = { "None (Always)", "Target has Full HP", "Target is Damaged", "Target is Ally", "Target is Enemy", "I have Full HP" };
        private readonly string[] targetFaceOptions = { "Custom Selection", "All", "Top", "Bottom", "Middle", "Left", "Right", "Rightmost", "Top & Bottom", "Column", "Row" };
        private readonly string[] actionTypeOptions = { "Keyword", "Sticker (Buff/Debuff)", "Summon (Egg)", "Passive Effect", "TriggerHP", "Raw Inject" };
        private readonly string[] colorModeOptions = { "HSV Shift", "Single Hue Integer", "HSL Text String" };
        private readonly string[] keywordDatabase = { "cantrip", "poison", "decay", "engage", "heavy", "shieldself", "heal", "undying", "first", "exert", "sticky" };
        private readonly string[] itemDatabase = { "Shortsword", "Leather Vest", "Foil", "Kilt", "Chainmail", "Eye of Horus", "Origami", "Wrench" };

        public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
        {
            Instance = this;
            currentItem = new ItemData();
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
            float dynamicScrollHeight = Mathf.Max(300f, totalHeight - (rowHeight * 3f) - (spacing * 4f) - 120f);

            List<ColumnSpec> columns = new List<ColumnSpec>
            {
                BuildHierarchyColumn(rowHeight, dynamicScrollHeight), // Left Column (0.0 to 0.32)
                BuildWorkspaceColumn(rowHeight),                     // Middle Column (0.33 to 0.69)
                BuildMetadataColumn(rowHeight)                      // Right Column (0.70 to 1.0)
            };

            generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

            float bottomBarHeight = 110f;
            foreach (var colPanel in generatedScreen.ColumnPanels.Values)
            {
                colPanel.offsetMin = new Vector2(colPanel.offsetMin.x, bottomBarHeight + 12f);
            }

            BuildBottomCompilerBar(bottomBarHeight);
            BindUIReferences();
            ConfigureWorkspace();
            UpdateColorPanelVisibility(0);
            UpdateCompilerOutput();
        }

        private ColumnSpec BuildHierarchyColumn(float rowHeight, float dynamicScrollHeight)
        {
            List<GridRowSpec> rows = new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("HIERARCHY_TITLE", "CONFIGURATION & LOGIC TREE", 1.0f)),
                new GridRowSpec(rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Item from Clipboard", 1.0f, LoadModFromClipboard)),
                new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, macroOptions, OnDropdownSelected)),
                new GridRowSpec(dynamicScrollHeight, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f)),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateButton("BtnCompileWorkspace", "Compile Layout", 0.5f, UpdateCompilerOutput),
                    GridCellSpec.CreateButton("BtnClearWorkspace", "Clear Logic", 0.5f, ClearWorkspace)
                )
            };

            return new ColumnSpec("Left_Column", 0.0f, 0.32f, rows);
        }

        private void ConfigureWorkspace()
        {
            if (generatedScreen == null) return;

            if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences refs))
            {
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

                    _rootWorkspaceZone = _workspaceContent.gameObject.GetComponent<BlockDropZone>();
                    if (_rootWorkspaceZone == null) _rootWorkspaceZone = _workspaceContent.gameObject.AddComponent<BlockDropZone>();

                    RectTransform viewportRt = _workspaceScroll.viewport;
                    if (viewportRt != null)
                    {
                        ContextMenuTrigger trigger = viewportRt.gameObject.GetComponent<ContextMenuTrigger>();
                        if (trigger == null) trigger = viewportRt.gameObject.AddComponent<ContextMenuTrigger>();

                        FilteredDropdown dropdownPrefab = uiGenerator.filteredDropdown != null
                            ? uiGenerator.filteredDropdown.GetComponent<FilteredDropdown>() : null;

                        trigger.Initialize(
                            generatedScreen.RootWrapper,
                            dropdownPrefab,
                            new List<string>(macroOptions),
                            AddBlockToWorkspace
                        );
                    }
                }
            }
        }

        private ColumnSpec BuildWorkspaceColumn(float rowHeight)
        {
            List<GridRowSpec> rows = new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", "NODE SPECIFICATION INSPECTOR", 1.0f)),
                
                // 1. Target Face Positioning Toggles
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

                // 2. Conditional Constraints
                new GridRowSpec(22f, GridCellSpec.CreateLabel("Conditions_Header_Lbl", "2. Execution Constraints (TogRes Mapping)", 1.0f)),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateDropdown("Drop_Condition", "Requires:", 0.65f, conditionOptions, (val) => UpdateCompilerOutput()),
                    GridCellSpec.CreateToggle("Tgl_Multiplier", "x2 Conditional", 0.35f, (val) => UpdateCompilerOutput())
                ),

                new GridRowSpec(6f, GridCellSpec.CreateLabel("S_1", "", 1.0f)),

                // 3. Action Payload Settings
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

                // Aesthetic color matrix setup
                new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("Drop_ColorMode", "Color Format:", 1.0f, colorModeOptions, OnColorModeDropdownChanged)),
                
                // Color subfields (handled dynamically in Bind / Visibility logic)
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateLabel("H_Lbl", "H", 0.10f),
                    GridCellSpec.CreateSlider("Sld_H", -180f, 180f, true, 0.90f, (val) => { currentItem.h = (int)val; UpdateCompilerOutput(); })
                ),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateLabel("S_Lbl", "S", 0.10f),
                    GridCellSpec.CreateSlider("Sld_S", -100f, 100f, true, 0.90f, (val) => { currentItem.s = (int)val; UpdateCompilerOutput(); })
                ),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateLabel("V_Lbl", "V", 0.10f),
                    GridCellSpec.CreateSlider("Sld_V", -100f, 100f, true, 0.90f, (val) => { currentItem.v = (int)val; UpdateCompilerOutput(); })
                ),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateLabel("Hue_Lbl", "Hue", 0.15f),
                    GridCellSpec.CreateSlider("Sld_SingleHue", 0f, 360f, true, 0.85f, (val) => { currentItem.hue = (int)val; UpdateCompilerOutput(); })
                ),
                new GridRowSpec(rowHeight, GridCellSpec.CreateInput("In_HSL", "HSL String Configuration", 1.0f, (val) => { currentItem.hsl = val; UpdateCompilerOutput(); })),

                // Engine aesthetic overrides
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

        private void BindUIReferences()
        {
            if (generatedScreen == null) return;

            if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
            {
                leftRefs.Dropdowns.TryGetValue("ModDropdown", out _leftDirectiveDropdown);
            }

            if (generatedScreen.ColumnRefs.TryGetValue("Middle_Column", out GridReferences midRefs))
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

            if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rightRefs))
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

        private void OnDropdownSelected(int index)
        {
            if (index <= 0 || _leftDirectiveDropdown == null) return;

            string selectedOption = _leftDirectiveDropdown.options[index].text;
            _leftDirectiveDropdown.value = 0;

            AddBlockToWorkspace(selectedOption);
        }

        private void AddBlockToWorkspace(string optionName)
        {
            if (_workspaceContent == null) return;

            ITextmodNode compilerNode = TextmodBlockFactory.CreateBlock(optionName);
            if (compilerNode == null) return;

            GameObject containerGo = new GameObject($"Block_{optionName}", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasGroup));
            containerGo.transform.SetParent(_workspaceContent, false);

            DragReorderItem dragItem = containerGo.AddComponent<DragReorderItem>();
            dragItem.OnDragEnded = () => RefreshWorkspaceLayout();

            VisualBlockComponent vb = containerGo.AddComponent<VisualBlockComponent>();

            Action onRebuild = () => RefreshWorkspaceLayout();
            Action onRemove = () =>
            {
                _activeUIBlocks.RemoveAll(b => b.ContainerGO == containerGo);
                if (_selectedBlockInstance?.ContainerGO == containerGo) _selectedBlockInstance = null;
                Destroy(containerGo);
                RefreshWorkspaceLayout();
            };

            UIBlockNode uiNode = UIBlockFactory.CreateUIBlock(optionName, compilerNode, uiGenerator, onRebuild, onRemove);
            vb.UI_Node = uiNode;

            BlockInstance instance = new BlockInstance(containerGo, compilerNode, uiNode);
            _activeUIBlocks.Add(instance);

            // Hook up selection listener on the newly spawned visual component
            Button selectBtn = containerGo.GetComponent<Button>();
            if (selectBtn == null) selectBtn = containerGo.AddComponent<Button>();
            selectBtn.onClick.AddListener(() => SelectWorkspaceBlock(instance));

            RebuildSingleBlockUI(containerGo, uiNode);

            Canvas.ForceUpdateCanvases();
            if (_workspaceScroll != null) _workspaceScroll.verticalNormalizedPosition = 0f;

            UpdateCompilerOutput();
        }

        private void SelectWorkspaceBlock(BlockInstance instance)
        {
            _selectedBlockInstance = instance;

            // Populate Inspector fields from the parsed content of this specific block
            if (instance.CompilerNode != null)
            {
                ItemMechanic parsedMech = ItemMechanic.Parse(instance.CompilerNode.Compile());

                // Update workspace toggles to reflect the selected mechanic
                if (_tglFaceTop) _tglFaceTop.isOn = parsedMech.Positions.Contains("top");
                if (_tglFaceBot) _tglFaceBot.isOn = parsedMech.Positions.Contains("bot");
                if (_tglFaceMid) _tglFaceMid.isOn = parsedMech.Positions.Contains("mid");
                if (_tglFaceLeft) _tglFaceLeft.isOn = parsedMech.Positions.Contains("left");
                if (_tglFaceRight) _tglFaceRight.isOn = parsedMech.Positions.Contains("right");
                if (_tglFaceRightmost) _tglFaceRightmost.isOn = parsedMech.Positions.Contains("rightmost");

                if (_tglIsWrapped) _tglIsWrapped.isOn = parsedMech.IsWrapped;

                // Sync action dropdown matching the operation string
                if (_dropActionType != null)
                {
                    if (parsedMech.Operation == "k") _dropActionType.value = 0;
                    else if (parsedMech.Operation == "sticker") _dropActionType.value = 1;
                    else if (parsedMech.Operation == "cast") _dropActionType.value = 2;
                    // Fall back to first index or default
                }
            }
        }

        private void RefreshWorkspaceLayout()
        {
            if (_activeUIBlocks.Count == 0) return;

            var sortedBlocks = _activeUIBlocks
                .Where(b => b.ContainerGO != null)
                .OrderByDescending(b => GetTransformDepth(b.ContainerGO.transform))
                .ToList();

            foreach (var block in sortedBlocks)
            {
                RebuildSingleBlockUI(block.ContainerGO, block.UINode);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_workspaceContent.GetComponent<RectTransform>());
            UpdateCompilerOutput();
        }

        private int GetTransformDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null) { depth++; t = t.parent; }
            return depth;
        }

        private void RebuildSingleBlockUI(GameObject container, UIBlockNode uiNode)
        {
            RectTransform rt = container.GetComponent<RectTransform>();

            var oldDropZones = rt.GetComponentsInChildren<BlockDropZone>(true);
            Dictionary<string, List<Transform>> rescuedBlocks = new Dictionary<string, List<Transform>>();

            foreach (var zone in oldDropZones)
            {
                string zoneKey = zone.gameObject.name;
                List<Transform> blocksToSave = new List<Transform>();

                foreach (Transform child in zone.transform)
                {
                    if (child.GetComponent<VisualBlockComponent>() != null) blocksToSave.Add(child);
                }

                foreach (var b in blocksToSave) b.SetParent(_workspaceContent, false);

                if (blocksToSave.Count > 0) rescuedBlocks[zoneKey] = blocksToSave;
            }

            GridReferences refs = uiGenerator.RebuildGrid(rt, uiNode.GetRowSpecs(), useMargins: false);

            LayoutElement layoutEl = container.GetComponent<LayoutElement>();
            if (layoutEl != null)
            {
                layoutEl.minHeight = refs.TotalHeight;
                layoutEl.preferredHeight = refs.TotalHeight;
            }

            foreach (var kvp in rescuedBlocks)
            {
                if (refs.DropZones.TryGetValue(kvp.Key, out BlockDropZone newZone))
                {
                    foreach (var b in kvp.Value) b.SetParent(newZone.transform, false);
                }
                else
                {
                    foreach (var b in kvp.Value) b.SetParent(_workspaceContent, false);
                }
            }

            uiNode.BindUI(rt, refs);
            uiNode.RestoreState(rt, refs);
        }

        private void OnPresetFaceSelected(int index)
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

            if (_tglFaceTop) _tglFaceTop.isOn = t;
            if (_tglFaceBot) _tglFaceBot.isOn = b;
            if (_tglFaceMid) _tglFaceMid.isOn = m;
            if (_tglFaceLeft) _tglFaceLeft.isOn = l;
            if (_tglFaceRight) _tglFaceRight.isOn = r;
            if (_tglFaceRightmost) _tglFaceRightmost.isOn = rm;

            UpdateCompilerOutput();
        }

        private void OnActionTypeChanged(int index)
        {
            bool showKeyword = (index == 0);
            bool showItem = (index == 1);
            bool showSummon = (index == 2);

            if (_dropKeywordSelect) _dropKeywordSelect.gameObject.SetActive(showKeyword);
            if (_dropItemSelect) _dropItemSelect.gameObject.SetActive(showItem);
            if (_inputSummonEntity) _inputSummonEntity.gameObject.SetActive(showSummon);

            UpdateCompilerOutput();
        }

        private void OnColorModeDropdownChanged(int index)
        {
            UpdateColorPanelVisibility(index);
            UpdateCompilerOutput();
        }

        private void UpdateColorPanelVisibility(int selectedMode)
        {
            // Update UI elements based on selected mode
            if (_sldH) _sldH.gameObject.SetActive(selectedMode == 0);
            if (_sldS) _sldS.gameObject.SetActive(selectedMode == 0);
            if (_sldV) _sldV.gameObject.SetActive(selectedMode == 0);
            if (_sldSingleHue) _sldSingleHue.gameObject.SetActive(selectedMode == 1);
            if (_inputHSL) _inputHSL.gameObject.SetActive(selectedMode == 2);
        }

        private void OnTierSliderChanged(float val)
        {
            if (_inputTierNum != null) _inputTierNum.text = val.ToString("0");
            currentItem.tier = (int)val;
            UpdateCompilerOutput();
        }

        private void OnTierInputChanged(string text)
        {
            if (int.TryParse(text, out int parsedVal))
            {
                float clamped = Mathf.Clamp(parsedVal, -5f, 20f);
                if (_sldTier != null) _sldTier.value = clamped;
                currentItem.tier = (int)clamped;
                UpdateCompilerOutput();
            }
        }

        private void UpdateCompilerOutput()
        {
            // Sync tree mechanics to modern model list
            currentItem.Mechanics.Clear();
            currentItem.GrantedAbilities.Clear();

            foreach (var instance in _activeUIBlocks)
            {
                if (instance.CompilerNode != null)
                {
                    string compiledString = instance.CompilerNode.Compile();
                    if (compiledString.Contains("abilitydata."))
                    {
                        // Safely parse inside the schema wrapper
                        int targetIdx = compiledString.IndexOf("abilitydata.", StringComparison.OrdinalIgnoreCase);
                        string prefixStr = targetIdx > 0 ? compiledString.Substring(0, targetIdx - 1) : "";
                        string payload = compiledString.Substring(targetIdx + 12);

                        currentItem.GrantedAbilities.Add(new ItemAbility
                        {
                            Prefix = prefixStr,
                            Ability = AbilityData.Parse(payload)
                        });
                    }
                    else
                    {
                        currentItem.Mechanics.Add(ItemMechanic.Parse(compiledString));
                    }
                }
            }

            // Fallback generation for current workspace settings if tree is empty
            if (currentItem.Mechanics.Count == 0)
            {
                ItemMechanic fallbackMechanic = new ItemMechanic();
                if (_tglFaceTop && _tglFaceTop.isOn) fallbackMechanic.Positions.Add("top");
                if (_tglFaceBot && _tglFaceBot.isOn) fallbackMechanic.Positions.Add("bot");
                if (_tglFaceMid && _tglFaceMid.isOn) fallbackMechanic.Positions.Add("mid");
                if (_tglFaceLeft && _tglFaceLeft.isOn) fallbackMechanic.Positions.Add("left");
                if (_tglFaceRight && _tglFaceRight.isOn) fallbackMechanic.Positions.Add("right");
                if (_tglFaceRightmost && _tglFaceRightmost.isOn) fallbackMechanic.Positions.Add("rightmost");

                if (_tglIsWrapped) fallbackMechanic.IsWrapped = _tglIsWrapped.isOn;

                if (_dropActionType != null)
                {
                    int actionIdx = _dropActionType.value;
                    if (actionIdx == 0 && _dropKeywordSelect != null)
                    {
                        fallbackMechanic.Operation = "k";
                        fallbackMechanic.Payload = keywordDatabase[_dropKeywordSelect.value];
                    }
                    else if (actionIdx == 1 && _dropItemSelect != null)
                    {
                        fallbackMechanic.Operation = "sticker";
                        fallbackMechanic.Payload = itemDatabase[_dropItemSelect.value];
                    }
                    else if (actionIdx == 2 && _inputSummonEntity != null)
                    {
                        fallbackMechanic.Operation = "cast";
                        fallbackMechanic.Payload = _inputSummonEntity.text;
                    }
                }
                currentItem.Mechanics.Add(fallbackMechanic);
            }

            // Sync structural parameters based on active Color Mode choice
            if (_dropColorMode != null)
            {
                int mode = _dropColorMode.value;
                if (mode != 0) { currentItem.h = currentItem.s = currentItem.v = 0; }
                if (mode != 1) { currentItem.hue = null; }
                if (mode != 2) { currentItem.hsl = null; }
            }

            string rawExport = ItemData.Export(currentItem);

            // Construct contextual preview translation
            string englishOutput = $"Item '{currentItem.entityName ?? "New Item"}' ";
            if (currentItem.tier.HasValue) englishOutput += $"[Tier {currentItem.tier.Value}] ";

            int sideCount = currentItem.Mechanics.Sum(m => m.Positions.Count);
            englishOutput += $"modifies {sideCount} side target(s). ";

            if (!string.IsNullOrEmpty(currentItem.sidesc))
            {
                englishOutput += $"Replaces visual side description with \"{currentItem.sidesc}\".";
            }

            if (_finalStringInput != null) _finalStringInput.text = rawExport;
            if (_englishTranslationText != null) _englishTranslationText.text = englishOutput;
        }

        private void CopyToClipboard()
        {
            if (_finalStringInput != null && !string.IsNullOrEmpty(_finalStringInput.text))
            {
                GUIUtility.systemCopyBuffer = _finalStringInput.text;
                uiGenerator.CreatePopup("Successfully compiled item string and copied to system clipboard!");
            }
        }

        private void LoadModFromClipboard()
        {
            string clipboardContent = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboardContent)) return;

            currentItem = ItemData.Parse(clipboardContent);

            if (_inputName != null) _inputName.text = currentItem.entityName;
            if (_sldTier != null && currentItem.tier.HasValue) _sldTier.value = currentItem.tier.Value;
            if (_inputDoc != null) _inputDoc.text = currentItem.doc;
            if (_inputSideDesc != null) _inputSideDesc.text = currentItem.sidesc;
            if (_inputImg != null) _inputImg.text = currentItem.imageOverride;

            // Load color mode back references
            if (currentItem.h != 0 || currentItem.s != 0 || currentItem.v != 0)
            {
                if (_dropColorMode) _dropColorMode.value = 0;
                if (_sldH) _sldH.value = currentItem.h;
                if (_sldS) _sldS.value = currentItem.s;
                if (_sldV) _sldV.value = currentItem.v;
            }
            else if (currentItem.hue.HasValue)
            {
                if (_dropColorMode) _dropColorMode.value = 1;
                if (_sldSingleHue) _sldSingleHue.value = currentItem.hue.Value;
            }
            else if (!string.IsNullOrEmpty(currentItem.hsl))
            {
                if (_dropColorMode) _dropColorMode.value = 2;
                if (_inputHSL) _inputHSL.text = currentItem.hsl;
            }

            // Sync visual subfield groups
            if (_inputP) _inputP.text = currentItem.p;
            if (_inputB) _inputB.text = currentItem.b;
            if (_inputRect) _inputRect.text = currentItem.rect;
            if (_inputDraw) _inputDraw.text = currentItem.draw;
            if (_inputTHue) _inputTHue.text = currentItem.thue;

            // Sync physical workspace representation
            ClearWorkspace();
            foreach (var mechanic in currentItem.Mechanics)
            {
                AddBlockToWorkspace(mechanic.Operation ?? "Add Keyword");
            }

            UpdateColorPanelVisibility(_dropColorMode ? _dropColorMode.value : 0);
            UpdateCompilerOutput();
        }

        private void ClearWorkspace()
        {
            if (_workspaceContent == null) return;
            foreach (var instance in _activeUIBlocks)
            {
                if (instance.ContainerGO != null) Destroy(instance.ContainerGO);
            }
            _activeUIBlocks.Clear();
            _selectedBlockInstance = null;
            currentItem = new ItemData();
            UpdateCompilerOutput();
        }

        private struct BlockInstance
        {
            public GameObject ContainerGO;
            public ITextmodNode CompilerNode;
            public UIBlockNode UINode;

            public BlockInstance(GameObject container, ITextmodNode compilerNode, UIBlockNode uiNode)
            {
                ContainerGO = container;
                CompilerNode = compilerNode;
                UINode = uiNode;
            }
        }
    }
}