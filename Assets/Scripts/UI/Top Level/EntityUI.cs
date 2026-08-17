using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.ParticleSystem;

// =====================================================================
// GENERIC BASE ENTITY UI
// =====================================================================
public abstract class EntityUI<T> : RootUI where T : EntityData, new()
{
    protected IconPickerModal iconPicker;
    protected PortraitPreviewUI portraitPreview;

    protected GridReferences statsUI;
    protected GridReferences diceUI;
    protected TMP_InputField rawTextOutput;
    protected TextMeshProUGUI syntaxHighlighterText;
    protected ScrollRect statsScrollRect;
    protected ScrollRect diceScrollRect;

    protected int currentDiceTab = 0;
    protected int _currentPoolIndex = 0;
    protected bool isDrawingUI = false;
    protected bool _needsRebuild = false;

    protected bool _pendingTextUpdate = false;
    protected float _textUpdateTimer = 0f;
    protected DiceFaceBuilderWidget diceBuilderWidget;

    protected bool showCustomImagePanel = false;
    protected string _customImageString;
    protected Texture2D _customImageTexture;
    protected ImageReceiver _persistentCustomImageReceiver;

    protected T CurrentEntity
    {
        get
        {
            if (ModPackage.Instance == null) return null;

            var entity = ModPackage.Instance.GetActiveEntity<T>();
            if (entity == null)
            {
                ModPackage.Instance.LoadEntityForEditing(CreateDefaultEntity());
                entity = ModPackage.Instance.GetActiveEntity<T>();
            }

            return entity;
        }
    }

    // Virtualized to allow
    // to call .InitializeAsDefault()
    protected virtual T CreateDefaultEntity()
    {
        T entity = new T();
        if (entity.visuals != null) entity.visuals.Clear();
        return entity;
    }
    public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        uiGenerator = uiGeneratorRef;

        if (iconPicker == null)
            iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);

        EntityUIHelpers.Initialize();
        InitializeSpecifics();

        // ADDED: Initialize the reusable dice builder widget
        if (diceBuilderWidget == null)
        {
            diceBuilderWidget = new DiceFaceBuilderWidget(
                getDiceSides: () => CurrentEntity?.diceSides,
                allowFacades: AllowFacades,
                openBaseModal: OpenBaseModal,
                openFacadeModal: OpenFacadeModal,
                getBaseSprite: GetBaseDiceSprite,
                getFacadeSprite: GetFacadeDiceSprite,
                onStateChanged: NotifyStateChanged,
                onRebuildRequested: RebuildDiceScrollView
            );
        }

        BuildUIAndBind();

        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged += OnStateChanged;
            OnStateChanged(null);
        }
    }

    private void OnDestroy()
    {
        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged -= OnStateChanged;
        }
    }
    protected virtual void OnEnable()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            RebuildStatsUI();
            RebuildDiceScrollView();
        }
    }
    protected virtual bool IsTabVisible()
    {
        RectTransform rootWrapper = GetRootWrapper();
        return rootWrapper != null && rootWrapper.gameObject.activeInHierarchy;
    }

    // =====================================================================
    // ABSTRACT & VIRTUAL SPECIFICS
    // =====================================================================
    protected virtual void InitializeSpecifics() { }
    protected abstract bool AllowFacades();
    protected abstract List<GridRowSpec> GenerateStatsLayout();
    protected abstract void UpdateSpecificUIFromData();
    protected abstract void UpdateSpecificVisuals();
    protected abstract string ExportEntity(T entity);
    protected abstract T ParseEntity(string data);
    protected abstract void OpenBaseModal(int faceIndex);
    protected abstract void OpenFacadeModal(int faceIndex);
    protected abstract Sprite GetBaseDiceSprite(int effectID);
    protected abstract Sprite GetFacadeDiceSprite(string facadeID);

    // =====================================================================
    // PORTRAIT / ICON MODAL SELECTION
    // =====================================================================
    protected virtual string ResolveFacadeName(string facadeID) => SpriteCacheHelper.ResolveFacadeName(facadeID);
    protected DiceSideData GetEffectivePreviewFace(int index)
    {
        var face = CurrentEntity.diceSides[index];
        if (face == null) return null;

        foreach (var m in face.sideMechanics)
        {
            if (m.LegacyItemPayload != null)
            {
                foreach (var legMech in m.LegacyItemPayload.Mechanics)
                {
                    if (legMech.Prefix == "hat" && legMech.PayloadData is EntityData ed)
                    {
                        if (!(ed is MonsterData md && md.baseMonster != null && md.baseMonster.StartsWith("egg.", StringComparison.OrdinalIgnoreCase)))
                        {
                            int hatSourceIndex = index;
                            if (legMech.Targets.Count > 1)
                            {
                                var sourceTargets = DiceTargetHelper.GetIndicesForTarget(legMech.Targets[1]);
                                if (sourceTargets.Count > 0) hatSourceIndex = sourceTargets[0];
                            }

                            if (ed.diceSides != null && ed.diceSides[hatSourceIndex] != null)
                            {
                                return ed.diceSides[hatSourceIndex];
                            }
                        }
                    }
                }
            }
            else if (m.Prefix == "hat")
            {
                string rawPayload = m.RawPayloadString;
                if (!string.IsNullOrEmpty(rawPayload) && !rawPayload.StartsWith("egg.", StringComparison.OrdinalIgnoreCase))
                {
                    EntityData tempHat = StaticBranchTracing.IsMonsterEntity(rawPayload) ? (EntityData)new MonsterData() : new HeroData();
                    tempHat.SuppressAutoRegister = true;
                    tempHat.Parse(rawPayload);

                    int hatSourceIndex = index;
                    if (m.TargetStrings != null && m.TargetStrings.Count > 1)
                    {
                        var sourceTargets = DiceTargetHelper.GetIndicesForTarget(m.TargetStrings[1]);
                        if (sourceTargets.Count > 0) hatSourceIndex = sourceTargets[0];
                    }
                    else if (m.TargetEnums != null && m.TargetEnums.Count > 1)
                    {
                        var sourceTargets = DiceTargetHelper.GetIndicesForTarget(m.TargetEnums[1].ToString().ToLower());
                        if (sourceTargets.Count > 0) hatSourceIndex = sourceTargets[0];
                    }

                    if (tempHat.diceSides != null && tempHat.diceSides[hatSourceIndex] != null)
                    {
                        return tempHat.diceSides[hatSourceIndex];
                    }
                }
            }
        }

        return face;
    }
    protected virtual void UpdateIcon(int index)
    {
        if (portraitPreview == null) return;
        var face = GetEffectivePreviewFace(index);
        portraitPreview.SetSlotIcon(
            index,
            AllowFacades() ? ResolveFacadeName(face.facadeID) : null,
            face.effectID,
            AllowFacades() ? face.facadeColor : null,
            face.pips
        );
    }
    protected void OnPasteEntityString(string pastedString)
    {
        if (string.IsNullOrWhiteSpace(pastedString)) return;
        T importedEntity = ParseEntity(pastedString);

        ModPackage.Instance.UpdateActiveEntityClone<T>(importedEntity);

        RebuildStatsUI();
        RebuildDiceScrollView();

        ModPackage.Instance.NotifyActiveEntityChanged<T>(this);
    }

    // =====================================================================
    // STATE TO VIEW
    // =====================================================================
    protected virtual void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this))
        {
            UpdateVisualsOnly();
            return;
        }

        if (sender != null) return;

        if (!IsTabVisible())
        {
            _needsRebuild = true;
            return;
        }

        RebuildStatsUI();
        RebuildDiceScrollView();
    }
    protected void OpenColorPicker(Color initialColor, Action<Color> onColorChanged)
    {
        if (uiGenerator.colorPicker == null) return;

        uiGenerator.colorPicker.onColorChange.RemoveAllListeners();

        uiGenerator.colorPicker.gameObject.SetActive(true);

        uiGenerator.colorPicker.SetColor(initialColor);

        uiGenerator.colorPicker.onColorChange.AddListener(new UnityEngine.Events.UnityAction<Color>(onColorChanged));
    }
    protected void CloseColorPicker()
    {
        if (uiGenerator.colorPicker != null)
        {
            uiGenerator.colorPicker.gameObject.SetActive(false);
        }
    }
    protected void SetButtonColorPreview(Button btn, Color color)
    {
        if (btn == null) return;

        if (btn.image != null) btn.image.color = Color.white;

        Transform preview = btn.transform.Find("ColorPreview");
        Image previewImg;
        if (preview == null)
        {
            GameObject go = new GameObject("ColorPreview", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(btn.transform, false);
            previewImg = go.GetComponent<Image>();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.80f, 0.20f);
            rt.anchorMax = new Vector2(0.95f, 0.80f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            previewImg = preview.GetComponent<Image>();
        }

        color.a = 1f;
        previewImg.color = color;
    }
    protected void SetButtonIcon(Button btn, Sprite sprite) => StaticUI.SetButtonIcon(btn, sprite);

    // =====================================================================
    // VIEW TO STATE
    // =====================================================================
    protected void NotifyStateChanged()
    {
        if (isDrawingUI) return;
        ModPackage.Instance.NotifyActiveEntityChanged<T>(this);
    }
    protected void ResetToDefault()
    {
        ModPackage.Instance.UpdateActiveEntityClone<T>(CreateDefaultEntity());
        showCustomImagePanel = false;
        _currentPoolIndex = 0;

        ModPackage.Instance.NotifyActiveEntityChanged<T>(this);
        RebuildStatsUI();
        RebuildDiceScrollView();
    }

    // =====================================================================
    // VIEW TO STATE
    // =====================================================================
    protected void CopyDiceFace(int index) => diceBuilderWidget?.CopyDiceFace(index);
    protected void PasteDiceFace(int index) => diceBuilderWidget?.PasteDiceFace(index);
    protected void ClearDiceFace(int index) => diceBuilderWidget?.ClearDiceFace(index);

    protected void AddKeywordToFace(int faceIndex, int dropdownValue)
    {
        if (dropdownValue <= 0) return;
        string[] rawOptions = Enum.GetNames(typeof(EffectKeyword));
        string targetKeyword = rawOptions[dropdownValue - 1];

        var face = CurrentEntity.diceSides[faceIndex];
        if (!face.keywords.Contains(targetKeyword))
        {
            face.keywords.Add(targetKeyword);
            NotifyStateChanged();
            RebuildDiceScrollView();
        }
    }
    protected void RemoveKeywordFromFace(int faceIndex, string keyword)
    {
        if (CurrentEntity.diceSides[faceIndex].keywords.Remove(keyword))
        {
            NotifyStateChanged();
            RebuildDiceScrollView();
        }
    }
    protected void UpdateFaceHsv(int faceIndex, int componentIndex, int value)
    {
        if (!AllowFacades()) return;

        var face = CurrentEntity.diceSides[faceIndex];
        bool facadeAutoAssigned = false;

        if (string.IsNullOrEmpty(face.facadeID))
        {
            Sprite baseSprite = GetBaseDiceSprite(face.effectID);
            if (baseSprite != null)
            {
                face.facadeID = EntityUIHelpers.FormatFacadeID(baseSprite.name);
                facadeAutoAssigned = true;
            }
        }

        // FIX: Remove empty entries so empty strings don't occupy slot 0
        string[] partsColor = (face.facadeColor ?? "").Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> hsv = new List<string>(partsColor);
        while (hsv.Count < 3) hsv.Add("0");

        hsv[componentIndex] = value.ToString();

        if (hsv[0] == "0" && hsv[1] == "0" && hsv[2] == "0")
        {
            face.facadeColor = null;
        }
        else
        {
            face.facadeColor = $"{hsv[0]}:{hsv[1]}:{hsv[2]}";
        }

        string inputKey = componentIndex == 0 ? $"FacH_{faceIndex}" : (componentIndex == 1 ? $"FacS_{faceIndex}" : $"FacV_{faceIndex}");
        if (diceUI != null && diceUI.Inputs.TryGetValue(inputKey, out var input))
            input.SetTextWithoutNotify(value != 0 ? value.ToString() : "");

        string sliderKey = componentIndex == 0 ? $"SliH_{faceIndex}" : (componentIndex == 1 ? $"SliS_{faceIndex}" : $"SliV_{faceIndex}");
        if (diceUI != null && diceUI.Sliders.TryGetValue(sliderKey, out var slider))
            slider.SetValueWithoutNotify(value);

        NotifyStateChanged();

        if (facadeAutoAssigned) UpdateUIFromData();
    }
    protected void ToggleCustomImagePanel()
    {
        showCustomImagePanel = !showCustomImagePanel;
        RebuildStatsUI();
    }
    protected void OnPoolDropdownChanged(int index)
    {
        if (isDrawingUI) return;
        _currentPoolIndex = index;

        var entities = ModPackage.Instance.loadedMod.GetAll<T>();
        if (index > 0 && (index - 1) < entities.Count)
            ModPackage.Instance.LoadEntityForEditing(entities[index - 1]);
        else
            ModPackage.Instance.LoadEntityForEditing(CreateDefaultEntity());

        ModPackage.Instance.NotifyActiveEntityChanged<T>(this);

        // FIX: Force the UI to fully rebuild and pull values from the newly loaded entity
        RebuildStatsUI();
        RebuildDiceScrollView();
    }
    protected void SaveToModPool()
    {
        ModPackage.Instance.SaveActiveEntity<T>();
        T savedEntity = ModPackage.Instance.GetActiveEntity<T>();
        IReadOnlyList<T> entities = ModPackage.Instance.loadedMod.GetAll<T>();

        int newIndex = (entities as List<T>)?.IndexOf(savedEntity) ?? -1;
        if (newIndex >= 0) _currentPoolIndex = newIndex + 1;

        ModPackage.Instance.NotifyActiveEntityChanged<T>(this);
        RebuildStatsUI();
    }

    // =====================================================================
    // UI GENERATION & LAYOUTS
    // =====================================================================
    protected virtual List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        string[] keywordOptions = EntityUIHelpers.GetKeywordOptions();

        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        CurrentEntity.InitializeDiceFaces();

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            var face = CurrentEntity.diceSides[index];
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            int totalFaceRows = (AllowFacades() ? 8 : 5) + face.keywords.Count;

            var diceBgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f));
            diceBgRow.isBackground = true;
            diceBgRow.rowSpan = totalFaceRows;
            layout.Add(diceBgRow);

            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)));

            if (AllowFacades())
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Base:", 0.15f),
                    GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index)),
                    GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => {
                        if (string.IsNullOrWhiteSpace(val)) { face.effectID = 0; NotifyStateChanged(); }
                        else if (int.TryParse(val, out int id)) { face.effectID = id; NotifyStateChanged(); }
                    }),
                    GridCellSpec.CreateLabel("Facade:", 0.15f),
                    GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
                    GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; NotifyStateChanged(); })
                ));
            }
            else
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Base:", 0.25f),
                    GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.20f, () => OpenBaseModal(index)),
                    GridCellSpec.CreateLabel("ID:", 0.15f),
                    GridCellSpec.CreateInput($"ID_{index}", "ID", 0.40f, (val) => {
                        if (string.IsNullOrWhiteSpace(val)) { face.effectID = 0; NotifyStateChanged(); }
                        else if (int.TryParse(val, out int id)) { face.effectID = id; NotifyStateChanged(); }
                    })
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Pips:", 0.25f),
                GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => {
                    if (string.IsNullOrWhiteSpace(val)) { face.pips = 0; NotifyStateChanged(); }
                    else if (int.TryParse(val, out int p)) { face.pips = p; NotifyStateChanged(); }
                }),
                GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => {
                    face.pips--;
                    if (diceUI != null && diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                        input.SetTextWithoutNotify(face.pips.ToString());
                    NotifyStateChanged();
                }),
                GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => {
                    face.pips++;
                    if (diceUI != null && diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                        input.SetTextWithoutNotify(face.pips.ToString());
                    NotifyStateChanged();
                })
            ));

            if (AllowFacades())
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.30f),
                    GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 0, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateFaceHsv(index, 0, h); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sat:", 0.30f),
                    GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 1, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateFaceHsv(index, 1, s); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Val:", 0.30f),
                    GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 2, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateFaceHsv(index, 2, v); })
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, keywordOptions, (val) => AddKeywordToFace(index, val))
            ));

            foreach (var kw in face.keywords)
            {
                string keywordString = kw;
                string coloredLabel = EntityUIHelpers.GetColoredKeywordLabel(keywordString);
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"KwTag_{index}_{keywordString}", coloredLabel, 0.80f),
                    GridCellSpec.CreateButton($"KwDel_{index}_{keywordString}", "[X]", 0.20f, () => RemoveKeywordFromFace(index, keywordString))
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.33f, () => CopyDiceFace(index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.33f, () => PasteDiceFace(index)),
                GridCellSpec.CreateButton($"BtnClear_{index}", "Clear Dice", 0.33f, () => ClearDiceFace(index))
            ));

            if (tabIndex == 0 && index < 5) layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
        }

        return layout;
    }

    private System.Collections.IEnumerator RestoreFocusRoutine(TMPro.TMP_InputField input, int caretPos)
    {
        // Yield 1 frame to guarantee TMP has processed the text mesh generation
        // and layout group sizes before forcing a caret selection.
        yield return null;

        if (input != null)
        {
            input.ActivateInputField();
            // Safety clamp the caret so it can't index out of bounds on pasted text
            input.caretPosition = Mathf.Min(caretPos, input.text.Length);
        }
    }
    protected void RebuildDiceScrollView()
    {
        if (diceScrollRect == null) return;

        bool wasDrawing = isDrawingUI;
        isDrawingUI = true;

        if (CurrentEntity != null) CurrentEntity.InitializeDiceFaces();

        // CHANGED: Delegate layout generation to the widget, then cache references
        diceUI = uiGenerator.RebuildGrid(diceScrollRect.content, diceBuilderWidget.GenerateLayout(currentDiceTab));
        diceBuilderWidget.SetGridReferences(diceUI);

        float extraHeight = 0f;
        var layoutGroup = diceScrollRect.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = diceScrollRect.content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        diceScrollRect.content.sizeDelta = new Vector2(0, diceUI.TotalHeight + extraHeight);

        isDrawingUI = wasDrawing;
        Canvas.ForceUpdateCanvases();
        UpdateUIFromData();
    }
    protected override void BuildUIAndBind()
    {
        float canvasHeight = 900f;
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null) canvasHeight = canvasRt.rect.height;
        }

        float calculatedStatsHeight = Mathf.Max(canvasHeight - 60f, 400f);
        float calculatedDiceHeight = Mathf.Max(canvasHeight - uiGenerator.rowHeight - 80f, 300f);

        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftStats", 0.01f, 0.35f, new List<GridRowSpec>
            {
                new GridRowSpec(calculatedStatsHeight, GridCellSpec.CreateScrollView("StatsScrollView", 1.0f))
            }),
            new ColumnSpec("MiddleDiceBase", 0.365f, 0.685f, new List<GridRowSpec>
            {
                // CHANGED: Use TabNames from the widget (removes the "All" tab)
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateNavigationTabs("DiceTabs", DiceFaceBuilderWidget.TabNames, new List<GameObject>(), 1.0f, (idx) => {
                    currentDiceTab = idx;
                    RebuildDiceScrollView();
                })),
                new GridRowSpec(calculatedDiceHeight, GridCellSpec.CreateScrollView("DiceScrollView", 1.0f))
            }),
            new ColumnSpec("RightOutput", 0.70f, 0.99f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        statsScrollRect = generatedScreen.ColumnRefs["LeftStats"].ScrollViews["StatsScrollView"];
        diceScrollRect = generatedScreen.ColumnRefs["MiddleDiceBase"].ScrollViews["DiceScrollView"];

        ApplyDynamicLayoutConstraints();

        if (generatedScreen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }

        RebuildStatsUI();
        RebuildDiceScrollView();
    }

    protected void BuildRightPanelContent(RectTransform parent)
    {
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
        previewContainer.transform.SetParent(parent, false);
        FullScreenUIGenerator.SetAnchors(previewContainer.GetComponent<RectTransform>(), 0.05f, 0.7f, 0.95f, 0.95f);
        if (uiGenerator.PortraitPanel != null)
        {
            GameObject portraitObj = Instantiate(uiGenerator.PortraitPanel, previewContainer.transform, false);
            portraitPreview = portraitObj.GetComponentInChildren<PortraitPreviewUI>();
            portraitPreview.OnFaceSelected += (idx) => {
                currentDiceTab = Mathf.Clamp(idx - 1, 0, 5);
                RebuildDiceScrollView();
            };
        }

        GameObject inputObj = Instantiate(uiGenerator.inputFieldPrefab, parent);
        var innerLabel = inputObj.GetComponentInChildren<TextMeshProUGUI>();
        if (innerLabel != null) Destroy(innerLabel.gameObject);
        rawTextOutput = inputObj.GetComponentInChildren<TMP_InputField>();
        rawTextOutput.lineType = TMP_InputField.LineType.MultiLineNewline;
        rawTextOutput.interactable = true;
        rawTextOutput.textComponent.color = Color.clear;
        rawTextOutput.customCaretColor = true;
        rawTextOutput.caretColor = Color.white;
        rawTextOutput.richText = false;
        rawTextOutput.textComponent.enableAutoSizing = false;
        rawTextOutput.pointSize = 16;
        rawTextOutput.textComponent.autoSizeTextContainer = false;

        GameObject highlighterObj = Instantiate(uiGenerator.labelPrefab, rawTextOutput.textComponent.transform.parent);
        highlighterObj.name = "SyntaxHighlighter";
        syntaxHighlighterText = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();
        var canvasGroup = highlighterObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        foreach (var script in highlighterObj.GetComponents<MonoBehaviour>())
        {
            if (script != null && !(script is TextMeshProUGUI)) DestroyImmediate(script);
        }

        RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
        RectTransform textCompRt = rawTextOutput.textComponent.GetComponent<RectTransform>();
        highlightRt.anchorMin = textCompRt.anchorMin;
        highlightRt.anchorMax = textCompRt.anchorMax;
        highlightRt.offsetMin = textCompRt.offsetMin;
        highlightRt.offsetMax = textCompRt.offsetMax;
        highlightRt.pivot = textCompRt.pivot;

        syntaxHighlighterText.enableAutoSizing = false;
        syntaxHighlighterText.fontSize = 16;
        syntaxHighlighterText.alignment = rawTextOutput.textComponent.alignment;
        syntaxHighlighterText.margin = rawTextOutput.textComponent.margin;
        syntaxHighlighterText.enableWordWrapping = rawTextOutput.textComponent.enableWordWrapping;
        syntaxHighlighterText.autoSizeTextContainer = false;
        syntaxHighlighterText.richText = true;

        rawTextOutput.onValueChanged.AddListener((val) =>
        {
            if (syntaxHighlighterText != null)
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(val);
        });

        rawTextOutput.onEndEdit.AddListener((val) =>
        {
            if (string.IsNullOrWhiteSpace(val)) return;

            string actualExport = ExportEntity(CurrentEntity);
            string displayExport = actualExport;

            if (!string.IsNullOrEmpty(CurrentEntity.imageOverride) && CurrentEntity.imageOverride.Length > 50)
            {
                displayExport = displayExport.Replace(CurrentEntity.imageOverride, "CUSTOM_IMG_DATA");
            }

            if (val == actualExport || val == displayExport) return;

            string parsedVal = val;
            if (parsedVal.Contains("CUSTOM_IMG_DATA") && !string.IsNullOrEmpty(CurrentEntity.imageOverride))
            {
                parsedVal = parsedVal.Replace("CUSTOM_IMG_DATA", CurrentEntity.imageOverride);
            }

            try { OnPasteEntityString(parsedVal); }
            catch (Exception ex) { Debug.LogWarning($"Could not parse manual edits to string: {ex.Message}"); }
        });

        FullScreenUIGenerator.SetAnchors(inputObj.GetComponent<RectTransform>(), 0.0f, 0.08f, 1.0f, 0.58f);

        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Copy String";
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => ClipboardManager.CopyToClipboard(ExportEntity(CurrentEntity)));
        FullScreenUIGenerator.SetAnchors(copyBtnObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Paste String";
        pasteBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => OnPasteEntityString(GUIUtility.systemCopyBuffer));
        FullScreenUIGenerator.SetAnchors(pasteBtnObj.GetComponent<RectTransform>(), 0.52f, 0.0f, 1.0f, 0.06f);
    }
    protected void UpdateExportText()
    {
        if (rawTextOutput != null && CurrentEntity != null)
        {
            string exportedString = ExportEntity(CurrentEntity);
            string displayString = exportedString;

            // Mask the massive image data in the UI text box so TMP doesn't truncate/lag and trigger a corrupt onEndEdit
            if (!string.IsNullOrEmpty(CurrentEntity.imageOverride) && CurrentEntity.imageOverride.Length > 50)
            {
                displayString = displayString.Replace(CurrentEntity.imageOverride, "CUSTOM_IMG_DATA");
            }

            rawTextOutput.SetTextWithoutNotify(displayString);
            if (syntaxHighlighterText != null)
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(displayString);
        }
    }
    protected void RebuildStatsUI()
    {
        if (statsScrollRect == null) return;
        bool wasDrawing = isDrawingUI;
        isDrawingUI = true;

        string focusedInputKey = null;
        int savedCaretPosition = 0;
        if (statsUI != null && statsUI.Inputs != null)
        {
            foreach (var kvp in statsUI.Inputs)
            {
                if (kvp.Value != null && kvp.Value.isFocused)
                {
                    focusedInputKey = kvp.Key;
                    savedCaretPosition = kvp.Value.caretPosition;
                    break;
                }
            }
        }

        statsUI = uiGenerator.RebuildGrid(statsScrollRect.content, GenerateStatsLayout());

        float extraHeight = 0f;
        var layoutGroup = statsScrollRect.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = statsScrollRect.content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }
        statsScrollRect.content.sizeDelta = new Vector2(0, statsUI.TotalHeight + extraHeight);
        Canvas.ForceUpdateCanvases();

        if (showCustomImagePanel)
        {
            if (statsUI.CustomImgImporter.TryGetValue("CustomImgPanel", out ImageReceiver dummyReceiver))
            {
                if (_persistentCustomImageReceiver == null)
                {
                    _persistentCustomImageReceiver = dummyReceiver;
                    _persistentCustomImageReceiver.OnImageGenerated = (encodedStr, tex) =>
                    {
                        CurrentEntity.imageOverride = encodedStr; // Fixed property injection
                        _customImageString = encodedStr;
                        _customImageTexture = tex;
                        NotifyStateChanged();
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

        isDrawingUI = wasDrawing;
        UpdateUIFromData();

        if (focusedInputKey != null && statsUI.Inputs.TryGetValue(focusedInputKey, out var inputToFocus))
        {
            StartCoroutine(RestoreFocusRoutine(inputToFocus, savedCaretPosition));
        }
    }

    protected void ApplyDynamicLayoutConstraints()
    {
        if (statsScrollRect != null)
        {
            RectTransform scrollRt = statsScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowRt, 10f, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }

        if (diceScrollRect != null)
        {
            RectTransform scrollRt = diceScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);

            float topOffset = uiGenerator.rowHeight + 15f;
            StretchToParent(rowRt, topOffset, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }
    }
    protected void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;
        var layoutElement = target.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null) layoutElement = target.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1f;
    }
    protected void StretchToParent(RectTransform rt, float topOffset, float bottomOffset)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, bottomOffset);
        rt.offsetMax = new Vector2(0f, -topOffset);
    }
    protected void AppendCollectionSelector<U>(
            List<GridRowSpec> layout, string label, string uniqueKey,
            IReadOnlyList<U> availableChoices, List<string> currentActiveItems,
            Func<U, string> getKey, Func<U, string> getDisplay,
            Action<U> onAdd, Action<string> onRemove)
    {
        List<string> dropdownOptions = new List<string> { "" };
        dropdownOptions.AddRange(availableChoices.Select(getDisplay));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel(label, 0.30f),
            GridCellSpec.CreateFilteredDropdown($"Selector_{uniqueKey}", "", 0.70f, dropdownOptions.ToArray(), (idx) =>
            {
                if (idx > 0 && (idx - 1) < availableChoices.Count)
                {
                    onAdd?.Invoke(availableChoices[idx - 1]);
                }
            })
        ));

        if (currentActiveItems != null)
        {
            for (int i = 0; i < currentActiveItems.Count; i++)
            {
                string activeItemName = currentActiveItems[i];
                string rowKey = $"Active_{uniqueKey}_{i}_{activeItemName}";
                string delKey = $"Del_{uniqueKey}_{i}_{activeItemName}";

                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel(rowKey, activeItemName, 0.80f),
                    GridCellSpec.CreateButton(delKey, "[X]", 0.20f, () => onRemove?.Invoke(activeItemName))
                ));
            }
        }
    }


    #region Shared Layout Helpers
    protected void AppendHeaderButtons(List<GridRowSpec> layout, string entityTypeName, Action onOpenPoolModal)
    {
        layout.Add(new GridRowSpec(GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)));

        string poolBtnText = _currentPoolIndex == 0 ? $"Mod Pool: New {entityTypeName}" : $"Mod Pool: {CurrentEntity.entityName}";
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnOpenPool", poolBtnText, 0.70f, onOpenPoolModal),
            GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
        ));
    }
    protected void AppendIconOverrideLayout(List<GridRowSpec> layout)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Icon Override:", 0.30f),
            GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, () => OpenAllPortraitsModal((isHero, enumValue, selectedSprite) =>
            {
                CurrentEntity.imageOverride = isHero ? ((HeroType)enumValue).ToString() : ((MonsterType)enumValue).ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("OverrideName", "None", 0.35f, (val) => {
                if (val == "[CUSTOM_IMAGE_DATA]") return;
                CurrentEntity.imageOverride = val;
                NotifyStateChanged();
            }),
            GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.20f, ToggleCustomImagePanel)
        ));

        if (showCustomImagePanel)
        {
            layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));
        }
    }

    protected void AppendDocLayout(List<GridRowSpec> layout)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => {
                if (isDrawingUI) return;

                bool wasEmpty = string.IsNullOrEmpty(CurrentEntity.doc);

                if (string.IsNullOrEmpty(val) && !string.IsNullOrEmpty(CurrentEntity.doc2))
                {
                    // Shift doc2 up to doc if doc is cleared
                    CurrentEntity.doc = CurrentEntity.doc2;
                    CurrentEntity.doc2 = null;
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
                else
                {
                    // REMOVED SanitizeRichInput() to prevent Regex lag/stripping on valid game data
                    CurrentEntity.doc = val;
                    bool isEmpty = string.IsNullOrEmpty(CurrentEntity.doc);
                    NotifyStateChanged();

                    if (wasEmpty != isEmpty)
                    {
                        RebuildStatsUI();
                    }
                }
            })
        ));

        // Only render Doc 2 if Doc 1 has content
        if (!string.IsNullOrEmpty(CurrentEntity.doc))
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Doc 2:", 0.20f),
                GridCellSpec.CreateInput("Doc2", "", 0.80f, (val) => {
                    if (isDrawingUI) return;
                    CurrentEntity.doc2 = val; // No sanitizer
                    NotifyStateChanged();
                })
            ));
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Appended Doc:", 0.20f),
            GridCellSpec.CreateInput("AppendedDoc", "", 0.80f, (val) => {
                if (isDrawingUI) return;
                CurrentEntity.appendedDoc = val; // No sanitizer
                NotifyStateChanged();
            })
        ));
    }
    protected void AppendStandardItemsSelector(List<GridRowSpec> layout)
    {
        string[] rawNames = Enum.GetNames(typeof(BaseItems));
        string[] formattedItemNames = rawNames.Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2")).ToArray();

        AppendCollectionSelector<string>(
            layout: layout, label: "Add Item:", uniqueKey: "Item",
            availableChoices: formattedItemNames,
            currentActiveItems: CurrentEntity.items ?? new List<string>(),
            getKey: (itemName) => itemName, getDisplay: (itemName) => itemName,
            onAdd: (itemName) => {
                if (CurrentEntity.items == null) CurrentEntity.items = new List<string>();
                if (!CurrentEntity.items.Contains(itemName)) { CurrentEntity.items.Add(itemName); NotifyStateChanged(); RebuildStatsUI(); }
            },
            onRemove: (itemName) => {
                if (CurrentEntity.items != null && CurrentEntity.items.Remove(itemName)) { NotifyStateChanged(); RebuildStatsUI(); }
            }
        );
    }
    protected void AppendCustomItemsSelector(List<GridRowSpec> layout)
    {
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Item:", uniqueKey: "CustomItem",
            availableChoices: ModPackage.Instance?.CustomItems?
                .Select(i => !string.IsNullOrEmpty(i.unityName) ? i.unityName : (!string.IsNullOrEmpty(i.entityName) ? i.entityName : "Unnamed Item"))
                .Distinct().ToList() ?? new List<string>(),
            currentActiveItems: CurrentEntity.customPayloads?
                .Where(p => p.Type == PayloadType.Item)
                .Select(p => p.Data as ItemData)
                .Where(item => item != null)
                .Select(item => !string.IsNullOrEmpty(item.unityName) ? item.unityName : item.entityName)
                .ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (itemName) => {
                if (CurrentEntity.customPayloads == null) CurrentEntity.customPayloads = new List<CustomPayload>();
                var templateItem = ModPackage.Instance?.CustomItems?.FirstOrDefault(i => i.unityName == itemName || i.entityName == itemName);
                if (templateItem != null)
                {
                    bool alreadyExists = CurrentEntity.customPayloads.Any(p =>
                        p.Type == PayloadType.Item &&
                        ((p.Data as ItemData)?.unityName == itemName || (p.Data as ItemData)?.entityName == itemName));

                    if (!alreadyExists)
                    {
                        ItemData clonedItem = new ItemData();
                        clonedItem.Parse(templateItem.Export());
                        clonedItem.entityName = templateItem.entityName;
                        clonedItem.unityName = templateItem.unityName;
                        CurrentEntity.customPayloads.Add(new CustomPayload { Type = PayloadType.Item, Data = clonedItem });
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (itemName) => {
                if (CurrentEntity.customPayloads != null)
                {
                    var targetPayload = CurrentEntity.customPayloads.FirstOrDefault(p =>
                        p.Type == PayloadType.Item &&
                        ((p.Data as ItemData)?.unityName == itemName || (p.Data as ItemData)?.entityName == itemName));

                    if (targetPayload != null)
                    {
                        CurrentEntity.customPayloads.Remove(targetPayload);
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            }
        );
    }
    protected void AppendCustomAbilitiesSelector(List<GridRowSpec> layout, List<string> customAbilityNames)
    {
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Ability:", uniqueKey: "CustomAbility",
            availableChoices: customAbilityNames,
            currentActiveItems: CurrentEntity.customAbilityData?.Select(a => a.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name, getDisplay: (name) => name,
            onAdd: (abilityName) => {
                bool alreadyExists = CurrentEntity.customAbilityData?.Any(a => a.entityName == abilityName) ?? false;
                if (!alreadyExists)
                {
                    var template = ModPackage.Instance.CustomAbilities.FirstOrDefault(a => a.entityName == abilityName);
                    if (template != null)
                    {
                        AbilityData clonedAbility = AbilityData.CreateAbility(template.Export());
                        if (clonedAbility != null)
                        {
                            clonedAbility.entityName = template.entityName;
                            CurrentEntity.AddCustomAbility(clonedAbility);
                            NotifyStateChanged();
                            RebuildStatsUI();
                        }
                    }
                }
            },
            onRemove: (abilityName) => {
                CurrentEntity.RemoveCustomAbility(abilityName);
                NotifyStateChanged();
                RebuildStatsUI();
            }
        );
    }
    protected void AppendTraitsBlessingsCursesSelectors(List<GridRowSpec> layout)
    {
        // Traits
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Traits:", uniqueKey: "Trait",
            availableChoices: SDColors.TraitNiceNames.Keys.ToList(),
            currentActiveItems: CurrentEntity.traits ?? new List<string>(),
            getKey: (traitName) => traitName,
            getDisplay: (traitName) => SDColors.TraitNiceNames.TryGetValue(traitName, out string desc) ? $"{traitName}: {desc}" : traitName,
            onAdd: (traitName) => {
                if (CurrentEntity.traits == null) CurrentEntity.traits = new List<string>();
                if (!CurrentEntity.traits.Contains(traitName)) { CurrentEntity.traits.Add(traitName); NotifyStateChanged(); RebuildStatsUI(); }
            },
            onRemove: (traitName) => {
                if (CurrentEntity.traits != null && CurrentEntity.traits.Remove(traitName)) { NotifyStateChanged(); RebuildStatsUI(); }
            }
        );

        // Blessings
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Blessing:", uniqueKey: "Blessing",
            availableChoices: ModifierDataSet.Blessings.Keys.ToList(),
            currentActiveItems: CurrentEntity.blessings ?? new List<string>(),
            getKey: (blessingName) => blessingName,
            getDisplay: (blessingName) => ModifierDataSet.Blessings.TryGetValue(blessingName, out string desc) ? $"{blessingName}: {desc}" : blessingName,
            onAdd: (blessingName) => {
                if (CurrentEntity.blessings == null) CurrentEntity.blessings = new List<string>();
                if (!CurrentEntity.blessings.Contains(blessingName)) { CurrentEntity.blessings.Add(blessingName); NotifyStateChanged(); RebuildStatsUI(); }
            },
            onRemove: (blessingName) => {
                if (CurrentEntity.blessings != null && CurrentEntity.blessings.Remove(blessingName)) { NotifyStateChanged(); RebuildStatsUI(); }
            }
        );

        // Curses
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Curse:", uniqueKey: "Curse",
            availableChoices: ModifierDataSet.Curses.Keys.ToList(),
            currentActiveItems: CurrentEntity.curses ?? new List<string>(),
            getKey: (curseName) => curseName,
            getDisplay: (curseName) => ModifierDataSet.Curses.TryGetValue(curseName, out string desc) ? $"{curseName}: {desc}" : curseName,
            onAdd: (curseName) => {
                if (CurrentEntity.curses == null) CurrentEntity.curses = new List<string>();
                if (!CurrentEntity.curses.Contains(curseName)) { CurrentEntity.curses.Add(curseName); NotifyStateChanged(); RebuildStatsUI(); }
            },
            onRemove: (curseName) => {
                if (CurrentEntity.curses != null && CurrentEntity.curses.Remove(curseName)) { NotifyStateChanged(); RebuildStatsUI(); }
            }
        );
    }
    protected void AppendOrbSelectors(List<GridRowSpec> layout, List<string> customAbilityNames)
    {
        // Base Orbs
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Base Orb:", uniqueKey: "BaseOrb",
            availableChoices: OrbData.ValidBaseOrbs.ToList(),
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && o.isHardcoded).Select(o => o.hardcodedAbilityName).ToList() ?? new List<string>(),
            getKey: (name) => name, getDisplay: (name) => name,
            onAdd: (orbName) => {
                if (CurrentEntity.customOrbs == null) CurrentEntity.customOrbs = new List<OrbData>();
                bool alreadyExists = CurrentEntity.customOrbs.Any(o => o != null && o.isHardcoded && string.Equals(o.hardcodedAbilityName, orbName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyExists)
                {
                    OrbData newOrb = new OrbData();
                    newOrb.Parse($"orb.{orbName}");
                    CurrentEntity.AddCustomAbility(newOrb);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (orbName) => {
                if (CurrentEntity.customOrbs == null) return;
                var target = CurrentEntity.customOrbs.FirstOrDefault(o => o != null && o.isHardcoded && string.Equals(o.hardcodedAbilityName, orbName, StringComparison.OrdinalIgnoreCase));
                if (target != null && CurrentEntity.customOrbs.Remove(target))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // Custom Orbs
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Orb:", uniqueKey: "CustomOrb",
            availableChoices: customAbilityNames,
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && !o.isHardcoded).Select(o => o.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name, getDisplay: (name) => name,
            onAdd: (abilityName) => {
                if (CurrentEntity.customOrbs == null) CurrentEntity.customOrbs = new List<OrbData>();
                bool alreadyExists = CurrentEntity.customOrbs.Any(o => o != null && !o.isHardcoded && string.Equals(o.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyExists)
                {
                    var template = ModPackage.Instance.CustomAbilities?.FirstOrDefault(a => string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                    if (template != null)
                    {
                        OrbData clonedOrb = new OrbData();
                        clonedOrb.Parse(template.Export());
                        clonedOrb.entityName = template.entityName;
                        if (template is OrbData templateOrb)
                        {
                            clonedOrb.isHardcoded = templateOrb.isHardcoded;
                            clonedOrb.carrierPrefix = templateOrb.carrierPrefix;
                        }
                        else
                        {
                            clonedOrb.isHardcoded = false;
                            clonedOrb.carrierPrefix = "sthief.abilitydata";
                        }
                        CurrentEntity.AddCustomAbility(clonedOrb);
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (abilityName) => {
                if (CurrentEntity.customOrbs == null) return;
                var target = CurrentEntity.customOrbs.FirstOrDefault(o => o != null && !o.isHardcoded && string.Equals(o.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                if (target != null && CurrentEntity.customOrbs.Remove(target))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );
    }
    private void OpenAllPortraitsModal(Action<bool, int, Sprite> onPortraitSelected)
    {
        if (iconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && (HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name) || HeroSpriteDatabase.SpriteToMonsterMap.ContainsKey(sprite.name)),
            GetSearchName = (index, sprite) => EntityUIHelpers.GetPortraitDisplayName(sprite),
            GetTooltip = (index, sprite) => EntityUIHelpers.GetPortraitDisplayName(sprite),
            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                    onPortraitSelected?.Invoke(true, (int)hero, sprite);
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                    onPortraitSelected?.Invoke(false, (int)monster, sprite);
            }
        };
        iconPicker.OpenModal(config);
    }
    #endregion

    // =====================================================================
    // DYNAMIC COLOR MODIFIER STATE MANAGEMENT
    // =====================================================================
    protected void RemoveVisualModifier(int index)
    {
        if (CurrentEntity.visuals != null && index >= 0 && index < CurrentEntity.visuals.Count)
        {
            CurrentEntity.visuals.RemoveAt(index);
            NotifyStateChanged();
            RebuildStatsUI();
        }
    }
    protected void MoveVisualModifier(int index, int direction)
    {
        if (CurrentEntity.visuals == null) return;
        int newIndex = index + direction;
        if (newIndex >= 0 && newIndex < CurrentEntity.visuals.Count)
        {
            var item = CurrentEntity.visuals[index];
            CurrentEntity.visuals.RemoveAt(index);
            CurrentEntity.visuals.Insert(newIndex, item);
            NotifyStateChanged();
            RebuildStatsUI();
        }
    }
    private void AppendVisualHeader(List<GridRowSpec> layout, string title, int index)
    {
        string upText = index == 0 ? "-" : "▲";
        string downText = index == CurrentEntity.visuals.Count - 1 ? "-" : "▼";

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"<color=#aaaaaa>-- {title.ToUpper()} --</color>", 0.50f),
            GridCellSpec.CreateButton($"VisUp_{index}", upText, 0.15f, () => MoveVisualModifier(index, -1)),
            GridCellSpec.CreateButton($"VisDown_{index}", downText, 0.15f, () => MoveVisualModifier(index, 1)),
            GridCellSpec.CreateButton($"VisDel_{index}", "<color=red>[X]</color>", 0.20f, () => RemoveVisualModifier(index))
        ));
    }

    protected void AddVisualModifier(VisualType type)
    {
        if (CurrentEntity.visuals == null) CurrentEntity.visuals = new List<VisualModifier>();

        if (CurrentEntity.visuals.Count >= 16) return;

        var newVis = new VisualModifier { Type = type };
        if (type == VisualType.P) newVis.p = new Phue { colorRange = 1 };
        else if (type == VisualType.THue) newVis.thue = new Thue { colorRange = 1 };
        else if (type == VisualType.HSV) { newVis.v = 1; }

        CurrentEntity.visuals.Add(newVis);
        NotifyStateChanged();
        RebuildStatsUI();
    }
    protected void PasteVisualsFromClipboard()
    {
        string cb = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(cb)) return;
        try
        {
            T tempEntity = ParseEntity(cb);
            if (tempEntity != null)
            {
                CurrentEntity.imageOverride = tempEntity.imageOverride;

                // Deep copy visuals to prevent reference sharing bugs
                CurrentEntity.visuals = new List<VisualModifier>();
                if (tempEntity.visuals != null)
                {
                    foreach (var v in tempEntity.visuals)
                    {
                        var clonedVis = new VisualModifier
                        {
                            Type = v.Type,
                            RawValue = v.RawValue,
                            x = v.x,
                            y = v.y,
                            h = v.h,
                            s = v.s,
                            v = v.v,
                            hue = v.hue
                        };
                        if (v.p != null) clonedVis.p = new Phue { colorStart = v.p.colorStart, colorDestination = v.p.colorDestination, colorRange = v.p.colorRange };
                        if (v.thue != null) clonedVis.thue = new Thue { colorHex = v.thue.colorHex, colorRange = v.thue.colorRange, colorOffset = v.thue.colorOffset };
                        CurrentEntity.visuals.Add(clonedVis);
                    }
                }
                NotifyStateChanged();
                RebuildStatsUI();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Could not parse pasted visuals string: {ex.Message}");
        }
    }
    protected void OpenDrawIconModal(Action<string> onIconSelected)
    {
        if (iconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null,
            GetSearchName = (index, sprite) => IconPickerModal.GetCleanLeafName(sprite.name),
            GetTooltip = (index, sprite) => sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    onIconSelected?.Invoke(EntityUIHelpers.FormatFacadeID(sprite.name));
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    private string GetNiceVisualName(VisualType type)
    {
        switch (type)
        {
            case VisualType.P: return "P-Hue Swap";
            case VisualType.THue: return "T-Hue Range Shift";
            case VisualType.HSV: return "Global HSV Adjustment";
            case VisualType.Hue: return "Global Hue Shift";
            case VisualType.Draw: return "Draw Icon Overlay";
            default: return type.ToString();
        }
    }
    protected void AppendColorModifiersLayout(List<GridRowSpec> layout)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Manage Visuals:", 0.40f),
            GridCellSpec.CreateButton("BtnPasteVisuals", "Paste Visuals from Clipboard", 0.60f, PasteVisualsFromClipboard)
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Visual Modifier:", 0.40f),
            GridCellSpec.CreateFilteredDropdown("AddVisualDrop", "Select...", 0.60f,
                new string[] { "Select...", "P-Hue Swap", "T-Hue Color", "Global HSV", "Global Hue", "Draw Icon" },
                (idx) => {
                    if (idx == 1) AddVisualModifier(VisualType.P);
                    else if (idx == 2) AddVisualModifier(VisualType.THue);
                    else if (idx == 3) AddVisualModifier(VisualType.HSV);
                    else if (idx == 4) AddVisualModifier(VisualType.Hue);
                    else if (idx == 5) AddVisualModifier(VisualType.Draw);
                })
        ));

        if (CurrentEntity.visuals == null || CurrentEntity.visuals.Count == 0) return;

        for (int i = 0; i < CurrentEntity.visuals.Count; i++)
        {
            int index = i;
            var vis = CurrentEntity.visuals[index];

            if (vis.Type == VisualType.B || vis.Type == VisualType.Rect)
                continue;

            AppendVisualHeader(layout, GetNiceVisualName(vis.Type), index);

            if (vis.Type == VisualType.P)
            {
                if (vis.p == null) vis.p = new Phue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Colors:", 0.30f),
                    GridCellSpec.CreateButton($"VisPhueStartBtn_{index}", "Target", 0.35f, () => {
                        if (uiGenerator.colorPicker == null) return;
                        OpenColorPicker(vis.p.colorStart, (color) => {
                            vis.p.colorStart = color;
                            NotifyStateChanged();
                        });
                    }),
                    GridCellSpec.CreateButton($"VisPhueDestBtn_{index}", "Replace", 0.35f, () => {
                        if (uiGenerator.colorPicker == null) return;
                        OpenColorPicker(vis.p.colorDestination, (color) => {
                            vis.p.colorDestination = color;
                            NotifyStateChanged();
                        });
                    })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Range:", 0.30f),
                    GridCellSpec.CreateSlider($"VisPhueRange_{index}", 0, 99, true, 0.70f, (val) => {
                        vis.p.colorRange = Mathf.RoundToInt(val);
                        NotifyStateChanged();
                    })
                ));
            }
            else if (vis.Type == VisualType.THue)
            {
                if (vis.thue == null) vis.thue = new Thue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Target Color:", 0.35f),
                    GridCellSpec.CreateButton($"VisThueColorBtn_{index}", "Pick Color", 0.65f, () => {
                        if (uiGenerator.colorPicker == null) return;
                        OpenColorPicker(vis.thue.colorHex, (color) => {
                            vis.thue.colorHex = color;
                            NotifyStateChanged();
                        });
                    })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Range:", 0.20f),
                    GridCellSpec.CreateSlider($"VisThueRange_{index}", 0, 99, true, 0.30f, (val) => {
                        vis.thue.colorRange = Mathf.RoundToInt(val);
                        NotifyStateChanged();
                    }),
                    GridCellSpec.CreateLabel("Shift:", 0.20f),
                    GridCellSpec.CreateSlider($"VisThueOffset_{index}", -99, 99, true, 0.30f, (val) => {
                        vis.thue.colorOffset = Mathf.RoundToInt(val);
                        NotifyStateChanged();
                    })
                ));
            }
            else if (vis.Type == VisualType.HSV)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.30f),
                    GridCellSpec.CreateSlider($"VisHsvH_{index}", -99, 99, true, 0.50f, (val) => { vis.h = Mathf.RoundToInt(val); NotifyStateChanged(); }),
                    GridCellSpec.CreateInput($"VisHsvHIn_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) { vis.h = h; NotifyStateChanged(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sat:", 0.30f),
                    GridCellSpec.CreateSlider($"VisHsvS_{index}", -99, 99, true, 0.50f, (val) => { vis.s = Mathf.RoundToInt(val); NotifyStateChanged(); }),
                    GridCellSpec.CreateInput($"VisHsvSIn_{index}", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) { vis.s = s; NotifyStateChanged(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Val:", 0.30f),
                    GridCellSpec.CreateSlider($"VisHsvV_{index}", -99, 99, true, 0.50f, (val) => { vis.v = Mathf.RoundToInt(val); NotifyStateChanged(); }),
                    GridCellSpec.CreateInput($"VisHsvVIn_{index}", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) { vis.v = v; NotifyStateChanged(); } })
                ));
            }
            else if (vis.Type == VisualType.Hue)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.30f),
                    GridCellSpec.CreateSlider($"VisHue_{index}", -99, 99, true, 0.50f, (val) => { vis.hue = Mathf.RoundToInt(val); NotifyStateChanged(); }),
                    GridCellSpec.CreateInput($"VisHueIn_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int hue)) { vis.hue = hue; NotifyStateChanged(); } })
                ));
            }
            else if (vis.Type == VisualType.Draw)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Icon Name:", 0.20f),
                    GridCellSpec.CreateDiceButton($"VisDrawBtn_{index}", "P", 0.10f, () => OpenDrawIconModal((val) => {
                        vis.RawValue = val;
                        NotifyStateChanged();
                        UpdateUIFromData();
                    })),
                    GridCellSpec.CreateInput($"VisDrawName_{index}", "Name", 0.30f, (val) => { vis.RawValue = val; NotifyStateChanged(); }),
                    GridCellSpec.CreateLabel("X:", 0.10f),
                    GridCellSpec.CreateInput($"VisDrawX_{index}", "0", 0.10f, (val) => { if (int.TryParse(val, out int x)) { vis.x = x; NotifyStateChanged(); } }),
                    GridCellSpec.CreateLabel("Y:", 0.10f),
                    GridCellSpec.CreateInput($"VisDrawY_{index}", "0", 0.10f, (val) => { if (int.TryParse(val, out int y)) { vis.y = y; NotifyStateChanged(); } })
                ));
            }
        }
    }
    protected virtual void UpdateUIFromData()
    {
        if (statsUI == null || diceUI == null) return;
        isDrawingUI = true;
        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(CurrentEntity.entityName);
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn))
            hpIn.SetTextWithoutNotify(CurrentEntity.hp > 0 ? CurrentEntity.hp.ToString() : "");
        // Apply richText=false and toggle .enabled to safely inject raw bracketed text without TMP lag/truncation
        if (statsUI.Inputs.TryGetValue("Doc", out var docIn))
        {
            docIn.richText = false;
            docIn.enabled = false;
            docIn.SetTextWithoutNotify(CurrentEntity.doc);
            docIn.enabled = true;
        }
        if (statsUI.Inputs.TryGetValue("Doc2", out var doc2In))
        {
            doc2In.richText = false;
            doc2In.enabled = false;
            doc2In.SetTextWithoutNotify(CurrentEntity.doc2);
            doc2In.enabled = true;
        }
        if (statsUI.Inputs.TryGetValue("AppendedDoc", out var appendedDocIn))
        {
            appendedDocIn.richText = false;
            appendedDocIn.enabled = false;
            appendedDocIn.SetTextWithoutNotify(CurrentEntity.appendedDoc);
            appendedDocIn.enabled = true;
        }
        if (statsUI.Dropdowns.TryGetValue("PoolDropdown", out var poolDrop)) poolDrop.SetValueWithoutNotify(_currentPoolIndex);
        UpdateSpecificUIFromData();
        diceBuilderWidget?.UpdateUIFromData(currentDiceTab);
        isDrawingUI = false;
        UpdateVisualsOnly();

        _pendingTextUpdate = false;
        UpdateExportText();
    }
    protected void TriggerTextUpdate()
    {
        _pendingTextUpdate = true;
        _textUpdateTimer = 0f; // Resets timer so updates only run after slider/input manipulation stops
    }
    protected virtual void Update()
    {
        if (_needsRebuild && IsTabVisible())
        {
            _needsRebuild = false;
            RebuildStatsUI();
            RebuildDiceScrollView();
        }
        if (_pendingTextUpdate)
        {
            _textUpdateTimer += Time.deltaTime;
            if (_textUpdateTimer >= 0.15f)
            {
                _pendingTextUpdate = false;
                _textUpdateTimer = 0f;
                UpdateExportText();
            }
        }
    }
    protected virtual void UpdateVisualsOnly()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetNameText(CurrentEntity.entityName);
            portraitPreview.SetHPText(CurrentEntity.hp > 0 ? CurrentEntity.hp.ToString() : "");
            UpdateSpecificVisuals();
            portraitPreview.SetPortraitVisualModifiers(CurrentEntity.visuals);
        }

        if (statsUI != null && CurrentEntity.visuals != null)
        {
            for (int i = 0; i < CurrentEntity.visuals.Count; i++)
            {
                var vis = CurrentEntity.visuals[i];
                if (vis.Type == VisualType.P)
                {
                    if (statsUI.Buttons != null)
                    {
                        if (statsUI.Buttons.TryGetValue($"VisPhueStartBtn_{i}", out var startBtn))
                            SetButtonColorPreview(startBtn, vis.p != null ? vis.p.colorStart : Color.white);
                        if (statsUI.Buttons.TryGetValue($"VisPhueDestBtn_{i}", out var destBtn))
                            SetButtonColorPreview(destBtn, vis.p != null ? vis.p.colorDestination : Color.white);
                    }
                    if (statsUI.Sliders != null && statsUI.Sliders.TryGetValue($"VisPhueRange_{i}", out var sRange))
                        sRange.SetValueWithoutNotify(vis.p?.colorRange ?? 0);
                }
                else if (vis.Type == VisualType.THue)
                {
                    if (statsUI.Buttons != null && statsUI.Buttons.TryGetValue($"VisThueColorBtn_{i}", out var thueColorBtn))
                        SetButtonColorPreview(thueColorBtn, vis.thue != null ? vis.thue.colorHex : Color.white);
                    if (statsUI.Sliders != null)
                    {
                        if (statsUI.Sliders.TryGetValue($"VisThueRange_{i}", out var tRange))
                            tRange.SetValueWithoutNotify(vis.thue?.colorRange ?? 0);
                        if (statsUI.Sliders.TryGetValue($"VisThueOffset_{i}", out var tOff))
                            tOff.SetValueWithoutNotify(vis.thue?.colorOffset ?? 0);
                    }
                }
                else if (vis.Type == VisualType.HSV)
                {
                    if (statsUI.Sliders != null)
                    {
                        if (statsUI.Sliders.TryGetValue($"VisHsvH_{i}", out var sH)) sH.SetValueWithoutNotify(vis.h);
                        if (statsUI.Sliders.TryGetValue($"VisHsvS_{i}", out var sS)) sS.SetValueWithoutNotify(vis.s);
                        if (statsUI.Sliders.TryGetValue($"VisHsvV_{i}", out var sV)) sV.SetValueWithoutNotify(vis.v);
                    }
                    if (statsUI.Inputs != null)
                    {
                        if (statsUI.Inputs.TryGetValue($"VisHsvHIn_{i}", out var iH)) iH.SetTextWithoutNotify(vis.h.ToString());
                        if (statsUI.Inputs.TryGetValue($"VisHsvSIn_{i}", out var iS)) iS.SetTextWithoutNotify(vis.s.ToString());
                        if (statsUI.Inputs.TryGetValue($"VisHsvVIn_{i}", out var iV)) iV.SetTextWithoutNotify(vis.v.ToString());
                    }
                }
                else if (vis.Type == VisualType.Hue)
                {
                    if (statsUI.Sliders != null && statsUI.Sliders.TryGetValue($"VisHue_{i}", out var sHue)) sHue.SetValueWithoutNotify(vis.hue);
                    if (statsUI.Inputs != null && statsUI.Inputs.TryGetValue($"VisHueIn_{i}", out var iHue)) iHue.SetTextWithoutNotify(vis.hue.ToString());
                }
                else if (vis.Type == VisualType.Draw)
                {
                    if (statsUI.Buttons != null && statsUI.Buttons.TryGetValue($"VisDrawBtn_{i}", out var drawBtn))
                    {
                        Sprite s = SpriteCacheHelper.GetFacadeSprite(vis.RawValue);
                        if (s == null) s = SpriteCacheHelper.GetSpriteForPortrait(vis.RawValue);
                        SetButtonIcon(drawBtn, s);
                    }
                    if (statsUI.Inputs != null)
                    {
                        if (statsUI.Inputs.TryGetValue($"VisDrawName_{i}", out var iName)) iName.SetTextWithoutNotify(vis.RawValue);
                        if (statsUI.Inputs.TryGetValue($"VisDrawX_{i}", out var iX)) iX.SetTextWithoutNotify(vis.x.ToString());
                        if (statsUI.Inputs.TryGetValue($"VisDrawY_{i}", out var iY)) iY.SetTextWithoutNotify(vis.y.ToString());
                    }
                }
            }
        }
        for (int i = 0; i < 6; i++) UpdateIcon(i);
        diceBuilderWidget?.UpdateVisuals(currentDiceTab);

        TriggerTextUpdate();
    }
}