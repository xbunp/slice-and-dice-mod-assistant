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

    //protected DiceSideData diceClipboard = null;
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

    // Virtualized to allow HeroUI to call .InitializeAsDefault()
    protected virtual T CreateDefaultEntity() => new T();

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

    protected virtual void Update()
    {
        if (_needsRebuild && IsTabVisible())
        {
            _needsRebuild = false;
            RebuildStatsUI();
            RebuildDiceScrollView();
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
    protected virtual void UpdateIcon(int index)
    {
        if (portraitPreview == null) return;

        var face = CurrentEntity.diceSides[index];

        portraitPreview.SetSlotIcon(
            index,
            AllowFacades() ? face.facadeID : null,
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

    protected virtual void UpdateUIFromData()
    {
        if (statsUI == null || diceUI == null) return;
        isDrawingUI = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(CurrentEntity.entityName);
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn))
            hpIn.SetTextWithoutNotify(CurrentEntity.hp > 0 ? CurrentEntity.hp.ToString() : "");
        if (statsUI.Inputs.TryGetValue("Doc", out var docIn)) docIn.SetTextWithoutNotify(CurrentEntity.doc);

        if (statsUI.Dropdowns.TryGetValue("PoolDropdown", out var poolDrop)) poolDrop.SetValueWithoutNotify(_currentPoolIndex);

        if (statsUI.Sliders.TryGetValue("EntitySliH", out var shH)) shH.SetValueWithoutNotify(CurrentEntity.h);
        if (statsUI.Sliders.TryGetValue("EntitySliS", out var shS)) shS.SetValueWithoutNotify(CurrentEntity.s);
        if (statsUI.Sliders.TryGetValue("EntitySliV", out var shV)) shV.SetValueWithoutNotify(CurrentEntity.v);

        if (statsUI.Inputs.TryGetValue("EntityFacH", out var hH)) hH.SetTextWithoutNotify(CurrentEntity.h.ToString());
        if (statsUI.Inputs.TryGetValue("EntityFacS", out var hS)) hS.SetTextWithoutNotify(CurrentEntity.s.ToString());
        if (statsUI.Inputs.TryGetValue("EntityFacV", out var hV)) hV.SetTextWithoutNotify(CurrentEntity.v.ToString());

        if (statsUI.Sliders.TryGetValue("PhueRangeSlider", out var phueRangeSlider))
            phueRangeSlider.SetValueWithoutNotify(CurrentEntity.phue.colorRange);

        if (statsUI.Sliders.TryGetValue("ThueRangeSlider", out var thueRangeSlider))
            thueRangeSlider.SetValueWithoutNotify(CurrentEntity.thue.colorRange);

        if (statsUI.Sliders.TryGetValue("ThueOffsetSlider", out var thueOffsetSlider))
            thueOffsetSlider.SetValueWithoutNotify(CurrentEntity.thue.colorOffset);

        UpdateSpecificUIFromData();
        diceBuilderWidget?.UpdateUIFromData(currentDiceTab);

        /*
        int startIndex = (currentDiceTab == 0) ? 0 : currentDiceTab - 1;
        int endIndex = (currentDiceTab == 0) ? 6 : currentDiceTab;

        for (int i = startIndex; i < endIndex; i++)
        {
            var face = CurrentEntity.diceSides[i];
            if (diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
            if (diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());

            if (AllowFacades())
            {
                if (diceUI.Inputs.TryGetValue($"Facade_{i}", out var dFac)) dFac.SetTextWithoutNotify(face.facadeID);

                int h = 0, s = 0, v = 0;
                string[] hsv = (face.facadeColor ?? "").Split(':');
                if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
                if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS;
                if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

                if (diceUI.Sliders.TryGetValue($"SliH_{i}", out var sliH)) sliH.SetValueWithoutNotify(h);
                if (diceUI.Sliders.TryGetValue($"SliS_{i}", out var sliS)) sliS.SetValueWithoutNotify(s);
                if (diceUI.Sliders.TryGetValue($"SliV_{i}", out var sliV)) sliV.SetValueWithoutNotify(v);

                if (diceUI.Inputs.TryGetValue($"FacH_{i}", out var dH)) dH.SetTextWithoutNotify(h != 0 ? h.ToString() : "");
                if (diceUI.Inputs.TryGetValue($"FacS_{i}", out var dS)) dS.SetTextWithoutNotify(s != 0 ? s.ToString() : "");
                if (diceUI.Inputs.TryGetValue($"FacV_{i}", out var dV)) dV.SetTextWithoutNotify(v != 0 ? v.ToString() : "");
            }
        }
        */

        isDrawingUI = false;
        UpdateVisualsOnly();
    }

    protected virtual void UpdateVisualsOnly()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetNameText(CurrentEntity.entityName);
            portraitPreview.SetHPText(CurrentEntity.hp > 0 ? CurrentEntity.hp.ToString() : "");

            UpdateSpecificVisuals();

            portraitPreview.SetPortraitHSV(CurrentEntity.h, CurrentEntity.s, CurrentEntity.v);
            portraitPreview.SetPortraitTHue(CurrentEntity.thue);
            portraitPreview.SetPortraitPHue(CurrentEntity.phue);
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("PhueStartBtn", out var phueStartBtn))
            {
                Color c = CurrentEntity.phue != null ? CurrentEntity.phue.colorStart : Color.white;
                SetButtonColorPreview(phueStartBtn, c);
            }
            if (statsUI.Buttons.TryGetValue("PhueDestBtn", out var phueDestBtn))
            {
                Color c = CurrentEntity.phue != null ? CurrentEntity.phue.colorDestination : Color.white;
                SetButtonColorPreview(phueDestBtn, c);
            }
            if (statsUI.Buttons.TryGetValue("ThueColorBtn", out var thueColorBtn))
            {
                Color c = CurrentEntity.thue != null ? CurrentEntity.thue.colorHex : Color.white;
                SetButtonColorPreview(thueColorBtn, c);
            }
        }

        // CHANGED: Update the portrait preview icons for all 6 faces...
        for (int i = 0; i < 6; i++)
        {
            UpdateIcon(i);
        }
        // ...and delegate updating the dice scroll view's buttons to the widget
        diceBuilderWidget?.UpdateVisuals(currentDiceTab);

        if (rawTextOutput != null)
        {
            string exportedString = ExportEntity(CurrentEntity);
            rawTextOutput.SetTextWithoutNotify(exportedString);

            if (syntaxHighlighterText != null)
            {
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(exportedString);
            }
        }
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

    /*
    protected void SetButtonIcon(Button btn, Sprite sprite)
    {
        if (btn == null) return;
        ImageButton imgBtn = btn.GetComponent<ImageButton>();
        if (imgBtn != null && imgBtn.image != null)
        {
            if (sprite != null)
            {
                imgBtn.image.sprite = sprite;
                imgBtn.image.gameObject.SetActive(true);
            }
            else
            {
                imgBtn.image.sprite = null;
                imgBtn.image.gameObject.SetActive(false);
            }
            return;
        }

        Transform iconTransform = btn.transform.Find("Icon");
        Image targetImg = iconTransform != null ? iconTransform.GetComponent<Image>() : btn.image;

        if (targetImg != null)
        {
            if (sprite != null)
            {
                targetImg.sprite = sprite;
                targetImg.color = Color.white;
            }
            else
            {
                targetImg.sprite = null;
                targetImg.color = new Color(1, 1, 1, 0.2f);
            }
        }
    }
    */

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

    protected void UpdateEntityHsvData(int componentIndex, int value)
    {
        if (isDrawingUI) return;

        if (componentIndex == 0) CurrentEntity.h = value;
        else if (componentIndex == 1) CurrentEntity.s = value;
        else if (componentIndex == 2) CurrentEntity.v = value;

        string inputKey = componentIndex == 0 ? "EntityFacH" : (componentIndex == 1 ? "EntityFacS" : "EntityFacV");
        if (statsUI != null && statsUI.Inputs.TryGetValue(inputKey, out var input))
            input.SetTextWithoutNotify(value.ToString());

        string sliderKey = componentIndex == 0 ? "EntitySliH" : (componentIndex == 1 ? "EntitySliS" : "EntitySliV");
        if (statsUI != null && statsUI.Sliders.TryGetValue(sliderKey, out var slider))
            slider.SetValueWithoutNotify(value);

        NotifyStateChanged();
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
                string[] parts = baseSprite.name.Split('_');
                if (parts.Length >= 2)
                {
                    face.facadeID = $"{parts[0]}{parts[1]}";
                    facadeAutoAssigned = true;
                }
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

    protected void RebuildStatsUI()
    {
        if (statsScrollRect == null) return;
        bool wasDrawing = isDrawingUI;
        isDrawingUI = true;

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
                        CurrentEntity.GetType().GetProperty("imageOverride")?.SetValue(CurrentEntity, encodedStr);

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
                // FIXED: Convert 1-based preview clicks (1..6) to 0-based tab indices (0..5)
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
            if (val == ExportEntity(CurrentEntity)) return;
            try
            {
                OnPasteEntityString(val);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not parse manual edits to string: {ex.Message}");
            }
        });

        FullScreenUIGenerator.SetAnchors(inputObj.GetComponent<RectTransform>(), 0.0f, 0.08f, 1.0f, 0.58f);

        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Copy String";
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => GUIUtility.systemCopyBuffer = ExportEntity(CurrentEntity));
        FullScreenUIGenerator.SetAnchors(copyBtnObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Paste String";
        pasteBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => OnPasteEntityString(GUIUtility.systemCopyBuffer));
        FullScreenUIGenerator.SetAnchors(pasteBtnObj.GetComponent<RectTransform>(), 0.52f, 0.0f, 1.0f, 0.06f);
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
}