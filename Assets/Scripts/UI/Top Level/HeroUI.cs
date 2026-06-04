using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroUI : RootUI
{
    //[Header("UI Modal References")]
    private IconPickerModal diceFaceIconPicker;
    private PortraitPreviewUI portraitPreview;

    //[Header("Dynamically Generated Components")]
    private GridReferences statsUI;
    private GridReferences diceUI;
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;
    private ScrollRect statsScrollRect; 
    private ScrollRect diceScrollRect;

    // Navigation and Mod Pool State
    private int currentDiceTab = 0;
    private int _currentPoolIndex = 0;
    private bool isDrawingUI = false;

    // Dice Side Copy/Paste Clipboard State
    private DiceSideData diceClipboard = null;

    // Custom Image State
    private bool showCustomImagePanel = false;
    private string _customImageString;
    private Texture2D _customImageTexture;
    private ImageReceiver _persistentCustomImageReceiver;

    private HeroData CurrentHero
    {
        get
        {
            if (ModPackage.Instance == null) return null;

            var hero = ModPackage.Instance.GetActiveEntity<HeroData>();

            // If no active editing session exists yet, auto-provision a clean template
            if (hero == null)
            {
                ModPackage.Instance.LoadEntityForEditing(new HeroData());
                hero = ModPackage.Instance.GetActiveEntity<HeroData>();
            }

            return hero;
        }
    }
    private IReadOnlyList<HeroData> AllHeroes => ModPackage.Instance.loadedMod.GetAll<HeroData>();

    public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        uiGenerator = uiGeneratorRef;

        if (diceFaceIconPicker == null)
            diceFaceIconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);

        EntityUIHelpers.Initialize();
        BuildUIAndBind();

        if (ModPackage.Instance != null)
        {
            // This now compiles perfectly because both expect an 'object' parameter
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

    // =====================================================================
    // PORTRAIT / ICON MODAL SELECTION
    // =====================================================================

    private void OpenBaseModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.BaseActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) => sprite.name,
            GetTooltip = (index, sprite) => EntityUIHelpers.GetBaseTooltip(sprite),

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        CurrentHero.diceSides[faceIndex].effectID = parsedId;
                        NotifyStateChanged();
                        RebuildDiceScrollView();
                    }
                }
            }
        };

        diceFaceIconPicker.OpenModal(config);
    }
    private void OpenFacadeModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) => sprite.name,
            GetTooltip = (index, sprite) => sprite.name,

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');
                    if (parts.Length >= 2)
                    {
                        string facadeStr = $"{parts[0]}{parts[1]}";
                        CurrentHero.diceSides[faceIndex].facadeID = facadeStr;
                        NotifyStateChanged();
                        RebuildDiceScrollView();
                    }
                }
            }
        };

        diceFaceIconPicker.OpenModal(config);
    }
    private void OpenHeroPortraitsModal(Action<HeroType, Sprite> onHeroSelected)
    {
        if (diceFaceIconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name),
            GetSearchName = (index, sprite) => HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero) ? hero.ToString() : sprite.name,
            GetTooltip = (index, sprite) => HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero) ? hero.ToString() : sprite.name,

            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                {
                    onHeroSelected?.Invoke(hero, sprite);
                }
            }
        };

        diceFaceIconPicker.OpenModal(config);
    }   
    private void OpenAllPortraitsModal(Action<bool, int, Sprite> onPortraitSelected)
    {
        if (diceFaceIconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && (HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name) || HeroSpriteDatabase.SpriteToMonsterMap.ContainsKey(sprite.name)),
            GetSearchName = (index, sprite) => EntityUIHelpers.GetPortraitDisplayName(sprite),
            GetTooltip = (index, sprite) => EntityUIHelpers.GetPortraitDisplayName(sprite),

            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                {
                    onPortraitSelected?.Invoke(true, (int)hero, sprite);
                }
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                {
                    onPortraitSelected?.Invoke(false, (int)monster, sprite);
                }
            }
        };

        diceFaceIconPicker.OpenModal(config);
    }
    private void UpdateIcon(int index)
    {
        if (portraitPreview == null)
        {
            Debug.LogError("Portrait Preview is null, missing!", this);
            return;
        }

        var face = CurrentHero.diceSides[index];

        portraitPreview.SetSlotIcon(
            index,
            face.facadeID,
            face.effectID,
            face.facadeColor,
            face.pips
        );
    }
    private void OnPasteHeroString(string pastedString)
    {
        if (string.IsNullOrWhiteSpace(pastedString)) return;
        HeroData importedHero = TextModLexerParser.ParseHero(pastedString);

        // Safely replace the active working clone with the imported hero
        ModPackage.Instance.UpdateActiveEntityClone<HeroData>(importedHero);
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
        UpdateUIFromData();
    }

    // =====================================================================
    // STATE TO VIEW (PRESENTATION UPDATES)
    // =====================================================================

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this))
        {
            UpdateVisualsOnly();
            return;
        }

        UpdateUIFromData();
    }
    private void UpdateUIFromData()
    {
        if (statsUI == null || diceUI == null) return;

        isDrawingUI = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(CurrentHero.entityName);
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn)) hpIn.SetTextWithoutNotify(CurrentHero.hp.ToString());
        if (statsUI.Inputs.TryGetValue("Tier", out var tierIn)) tierIn.SetTextWithoutNotify(CurrentHero.tier.ToString());
        if (statsUI.Inputs.TryGetValue("ReplicaName", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentHero.baseReplica);
        if (statsUI.Inputs.TryGetValue("OverrideName", out var overNameIn)) overNameIn.SetTextWithoutNotify(CurrentHero.imageOverride);
        if (statsUI.Inputs.TryGetValue("Speech", out var speechIn)) speechIn.SetTextWithoutNotify(CurrentHero.speech);
        if (statsUI.Inputs.TryGetValue("Doc", out var docIn)) docIn.SetTextWithoutNotify(CurrentHero.doc);

        if (statsUI.Dropdowns.TryGetValue("PoolDropdown", out var poolDrop)) poolDrop.SetValueWithoutNotify(_currentPoolIndex);
        if (statsUI.Dropdowns.TryGetValue("Color", out var colDrop))
        {
            HeroColorOption colOpt = EntityUIHelpers.ReverseLookupColor(CurrentHero.colorClass);
            colDrop.SetValueWithoutNotify((int)colOpt);
        }

        if (statsUI.Sliders.TryGetValue("HeroSliH", out var shH)) shH.SetValueWithoutNotify(CurrentHero.h);
        if (statsUI.Sliders.TryGetValue("HeroSliS", out var shS)) shS.SetValueWithoutNotify(CurrentHero.s);
        if (statsUI.Sliders.TryGetValue("HeroSliV", out var shV)) shV.SetValueWithoutNotify(CurrentHero.v);

        if (statsUI.Inputs.TryGetValue("HeroFacH", out var hH)) hH.SetTextWithoutNotify(CurrentHero.h.ToString());
        if (statsUI.Inputs.TryGetValue("HeroFacS", out var hS)) hS.SetTextWithoutNotify(CurrentHero.s.ToString());
        if (statsUI.Inputs.TryGetValue("HeroFacV", out var hV)) hV.SetTextWithoutNotify(CurrentHero.v.ToString());

        int startIndex = (currentDiceTab == 0) ? 0 : currentDiceTab - 1;
        int endIndex = (currentDiceTab == 0) ? 6 : currentDiceTab;

        for (int i = startIndex; i < endIndex; i++)
        {
            var face = CurrentHero.diceSides[i];
            if (diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
            if (diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());
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

        isDrawingUI = false;
        UpdateVisualsOnly();
    }
    private void UpdateVisualsOnly()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetNameText(CurrentHero.entityName);
            portraitPreview.SetHPText(CurrentHero.hp.ToString());
            portraitPreview.SetTierText(CurrentHero.tier.ToString());

            HeroColorOption colOpt = EntityUIHelpers.ReverseLookupColor(CurrentHero.colorClass);
            portraitPreview.SetHeroColor(SDColors.GetColor(colOpt));

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentHero.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                portraitPreview.portrait.sprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Sprite targetSprite = EntityUIHelpers.GetSpriteForPortrait(string.IsNullOrEmpty(CurrentHero.imageOverride) || CurrentHero.imageOverride == "None" ? CurrentHero.baseReplica : CurrentHero.imageOverride);
                if (targetSprite != null && portraitPreview.portrait != null)
                {
                    portraitPreview.portrait.sprite = targetSprite;
                }
            }

            portraitPreview.SetPortraitHSV(CurrentHero.h, CurrentHero.s, CurrentHero.v);
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("ReplicaBtn", out var replicaBtn))
            {
                Sprite s = EntityUIHelpers.GetSpriteForPortrait(CurrentHero.baseReplica);
                SetButtonIcon(replicaBtn, s);
            }
            if (statsUI.Buttons.TryGetValue("OverrideBtn", out var overrideBtn))
            {
                Sprite s = EntityUIHelpers.GetSpriteForPortrait(CurrentHero.imageOverride);
                SetButtonIcon(overrideBtn, s);
            }
        }

        int startIndex = (currentDiceTab == 0) ? 0 : currentDiceTab - 1;
        int endIndex = (currentDiceTab == 0) ? 6 : currentDiceTab;

        for (int i = 0; i < 6; i++)
        {
            UpdateIcon(i);

            if (diceUI != null && diceUI.Buttons != null && i >= startIndex && i < endIndex)
            {
                var face = CurrentHero.diceSides[i];

                if (diceUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
                {
                    Sprite s = EntityUIHelpers.GetBaseSprite(face.effectID);
                    SetButtonIcon(baseBtn, s);
                }
                if (diceUI.Buttons.TryGetValue($"FacBtn_{i}", out var facBtn))
                {
                    Sprite s = EntityUIHelpers.GetFacadeSprite(face.facadeID);
                    SetButtonIcon(facBtn, s);
                }
            }
        }

        if (rawTextOutput != null)
        {
            string exportedString = HeroData.Export(CurrentHero);
            rawTextOutput.SetTextWithoutNotify(exportedString);

            if (syntaxHighlighterText != null)
            {
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(exportedString);
            }
        }
    }
    private void SetButtonIcon(Button btn, Sprite sprite)
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

    // =====================================================================
    // VIEW TO STATE DISPATCHERS
    // =====================================================================

    private void NotifyStateChanged()
    {
        if (isDrawingUI) return;
        // Signal updates on the active clone using the Singleton facade
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
    }
    private void ResetToDefault()
    {
        // Replace active working clone with a clean template
        ModPackage.Instance.UpdateActiveEntityClone<HeroData>(new HeroData());
        showCustomImagePanel = false;
        _currentPoolIndex = 0;

        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
        RebuildStatsUI();
        RebuildDiceScrollView();
    }
    private void CopyDiceFace(int index)
    {
        diceClipboard = CurrentHero.diceSides[index].Clone();
    }
    private void PasteDiceFace(int index)
    {
        if (diceClipboard == null) return;
        CurrentHero.diceSides[index] = diceClipboard.Clone();
        NotifyStateChanged();
        RebuildDiceScrollView();
    }
    private void AddKeywordToFace(int faceIndex, int dropdownValue)
    {
        if (dropdownValue <= 0) return;
        string[] rawOptions = Enum.GetNames(typeof(EffectKeyword));
        string targetKeyword = rawOptions[dropdownValue - 1];

        var face = CurrentHero.diceSides[faceIndex];
        if (!face.keywords.Contains(targetKeyword))
        {
            face.keywords.Add(targetKeyword);
            NotifyStateChanged();
            RebuildDiceScrollView();
        }
    }
    private void RemoveKeywordFromFace(int faceIndex, string keyword)
    {
        var face = CurrentHero.diceSides[faceIndex];
        if (face.keywords.Remove(keyword))
        {
            NotifyStateChanged();
            RebuildDiceScrollView();
        }
    }
    private void UpdateHeroHsvData(int componentIndex, int value)
    {
        if (componentIndex == 0) CurrentHero.h = value;
        else if (componentIndex == 1) CurrentHero.s = value;
        else if (componentIndex == 2) CurrentHero.v = value;

        // Directly update the associated text input field
        string inputKey = componentIndex == 0 ? "HeroFacH" : (componentIndex == 1 ? "HeroFacS" : "HeroFacV");
        if (statsUI != null && statsUI.Inputs.TryGetValue(inputKey, out var input))
        {
            input.SetTextWithoutNotify(value.ToString());
        }

        // Directly update the associated slider to maintain complete alignment
        string sliderKey = componentIndex == 0 ? "HeroSliH" : (componentIndex == 1 ? "HeroSliS" : "HeroSliV");
        if (statsUI != null && statsUI.Sliders.TryGetValue(sliderKey, out var slider))
        {
            slider.SetValueWithoutNotify(value);
        }

        NotifyStateChanged();
    }
    private void UpdateFaceHsv(int faceIndex, int componentIndex, int value)
    {
        var face = CurrentHero.diceSides[faceIndex];
        bool facadeAutoAssigned = false;

        // HSV requires a facade to function.
        // Auto-assign the facade corresponding to the active base ID if empty.
        if (string.IsNullOrEmpty(face.facadeID))
        {
            Sprite baseSprite = EntityUIHelpers.GetBaseSprite(face.effectID);
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

        string[] partsColor = (face.facadeColor ?? "").Split(':');
        List<string> hsv = new List<string>(partsColor);
        while (hsv.Count < 3) hsv.Add("0");

        hsv[componentIndex] = value.ToString();
        face.facadeColor = string.Join(":", hsv);

        // Directly update the associated text input field
        string inputKey = componentIndex == 0 ? $"FacH_{faceIndex}" : (componentIndex == 1 ? $"FacS_{faceIndex}" : $"FacV_{faceIndex}");
        if (diceUI != null && diceUI.Inputs.TryGetValue(inputKey, out var input))
        {
            input.SetTextWithoutNotify(value != 0 ? value.ToString() : ""); // Changed from > 0 to != 0
        }

        // Directly update the associated slider to maintain complete alignment
        string sliderKey = componentIndex == 0 ? $"SliH_{faceIndex}" : (componentIndex == 1 ? $"SliS_{faceIndex}" : $"SliV_{faceIndex}");
        if (diceUI != null && diceUI.Sliders.TryGetValue(sliderKey, out var slider))
        {
            slider.SetValueWithoutNotify(value);
        }

        NotifyStateChanged();

        if (facadeAutoAssigned)
        {
            UpdateUIFromData();
        }
    }
    private void ToggleCustomImagePanel()
    {
        showCustomImagePanel = !showCustomImagePanel;
        RebuildStatsUI();
    }
    private void OnPoolDropdownChanged(int index)
    {
        if (isDrawingUI) return;
        _currentPoolIndex = index;

        var heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();

        if (index > 0 && (index - 1) < heroes.Count)
        {
            // Load the existing structured hero into our editing session
            var originalHero = heroes[index - 1];
            ModPackage.Instance.LoadEntityForEditing(originalHero);
        }
        else
        {
            // Load a fresh, clean hero template into the editing session
            ModPackage.Instance.LoadEntityForEditing(new HeroData());
        }

        // Notify visual components to redraw
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
    }
    private void SaveToModPool()
    {
        // 1. Commit active clone back to the database (updates original or appends a new one)
        ModPackage.Instance.SaveActiveEntity<HeroData>();

        // 2. Fetch the newly updated list to align our selection index
        HeroData savedHero = ModPackage.Instance.GetActiveEntity<HeroData>();
        IReadOnlyList<HeroData> heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();

        // Safe cast to List<T> to use IndexOf
        int newIndex = (heroes as List<HeroData>)?.IndexOf(savedHero) ?? -1;
        if (newIndex >= 0)
        {
            _currentPoolIndex = newIndex + 1; // Update dropdown tracking index
        }

        // 3. Notify the UI to trigger dropdown updates and stats redraws
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
        RebuildStatsUI();
    }

    // =====================================================================
    // UI LAYOUT GENERATION
    // =====================================================================

    private List<GridRowSpec> GenerateStatsLayout()
    {
        var layout = new List<GridRowSpec>();

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)
        ));

        var heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();
        if (heroes != null)
        {
            List<string> poolOptions = new List<string> { "Stand Alone Hero (New)" };

            foreach (var hero in heroes)
            {
                // Access the name directly from the structured C# object.
                string heroName = string.IsNullOrEmpty(hero.entityName) ? "New Hero" : hero.entityName;
                poolOptions.Add(heroName);
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Mod Pool:", 0.20f),
                GridCellSpec.CreateDropdown("PoolDropdown", "", 0.50f, poolOptions.ToArray(), OnPoolDropdownChanged),
                GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
            ));
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentHero.entityName = val; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Replica Base:", 0.35f),
            GridCellSpec.CreateDiceButton("ReplicaBtn", "P", 0.15f, () => OpenHeroPortraitsModal((selectedHero, selectedSprite) => {
                CurrentHero.baseReplica = selectedHero.ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("ReplicaName", "Statue", 0.50f, (val) => { CurrentHero.baseReplica = val; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Icon Override:", 0.30f),
            GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, () => OpenAllPortraitsModal((isHero, enumValue, selectedSprite) => {
                CurrentHero.imageOverride = isHero ? ((HeroType)enumValue).ToString() : ((MonsterType)enumValue).ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("OverrideName", "None", 0.35f, (val) => { CurrentHero.imageOverride = val; NotifyStateChanged(); }),
            GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.20f, ToggleCustomImagePanel)
        ));

        if (showCustomImagePanel)
        {
            layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP:", 0.2f),
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => { if (int.TryParse(val, out int hp)) CurrentHero.hp = hp; NotifyStateChanged(); }),
            GridCellSpec.CreateLabel("Tier:", 0.2f),
            GridCellSpec.CreateInput("Tier", "", 0.3f, (val) => { if (int.TryParse(val, out int t)) CurrentHero.tier = t; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hue:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliH", -99, 99, true, 0.50f, (val) => UpdateHeroHsvData(0, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacH", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateHeroHsvData(0, h); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Saturation:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliS", -99, 99, true, 0.50f, (val) => UpdateHeroHsvData(1, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacS", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateHeroHsvData(1, s); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Value:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliV", -99, 99, true, 0.50f, (val) => UpdateHeroHsvData(2, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacV", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateHeroHsvData(2, v); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Color Class:", 0.35f),
            GridCellSpec.CreateDropdown("Color", "", 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                HeroColorOption selectedColor = (HeroColorOption)val;
                CurrentHero.colorClass = SDColors.GetColorCode(selectedColor);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Speech:", 0.20f),
            GridCellSpec.CreateInput("Speech", "", 0.80f, (val) => { CurrentHero.speech = val; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentHero.doc = val; NotifyStateChanged(); })
        ));

        // 1. Base Abilities
        AppendCollectionSelector<BaseAbility>(
            layout: layout,
            label: "Add Ability:",
            uniqueKey: "BaseAbility",
            availableChoices: BaseAbilityDatabase.Abilities,
            currentActiveItems: CurrentHero.baseAbilityData,
            getKey: (ability) => ability.name,
            getDisplay: (ability) =>
            {
                string cleanEffect = (ability.effect ?? "").Replace("\n", " | ");
                return $"{ability.name} ({ability.cost}): {cleanEffect}";
            },
            onAdd: (abilityName) =>
            {
                if (CurrentHero.baseAbilityData == null)
                    CurrentHero.baseAbilityData = new List<string>();

                if (!CurrentHero.baseAbilityData.Contains(abilityName))
                {
                    CurrentHero.baseAbilityData.Add(abilityName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (abilityName) =>
            {
                if (CurrentHero.baseAbilityData != null && CurrentHero.baseAbilityData.Remove(abilityName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // 2. Custom Abilities (Instantiated directly from selected name string)
        // Available choices hook: pass your list of raw custom ability name strings here
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Custom Ability:",
            uniqueKey: "CustomAbility",
            availableChoices: new List<string>(), // Hook: Put your string choices here
            currentActiveItems: CurrentHero.customAbilityData?.Select(a => a.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (abilityName) =>
            {
                if (CurrentHero.customAbilityData == null)
                    CurrentHero.customAbilityData = new List<AbilityData>();

                if (!CurrentHero.customAbilityData.Any(a => a.entityName == abilityName))
                {
                    /*
                    CurrentHero.customAbilityData.Add(new AbilityData { entityName = abilityName });
                    NotifyStateChanged();
                    RebuildStatsUI();
                    */
                }
            },
            onRemove: (abilityName) =>
            {
                if (CurrentHero.customAbilityData != null)
                {
                    var target = CurrentHero.customAbilityData.FirstOrDefault(a => a.entityName == abilityName);
                    if (target != null && CurrentHero.customAbilityData.Remove(target))
                    {
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            }
        );

        string[] rawNames = Enum.GetNames(typeof(BaseItems));
        string[] formattedItemNames = rawNames.Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2")).ToArray();

        // 3. Items (Strings)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Item:",
            uniqueKey: "Item",
            availableChoices: formattedItemNames,
            currentActiveItems: CurrentHero.items ?? new List<string>(),
            getKey: (itemName) => itemName,
            getDisplay: (itemName) => itemName,
            onAdd: (itemName) =>
            {
                if (CurrentHero.items == null)
                    CurrentHero.items = new List<string>();

                if (!CurrentHero.items.Contains(itemName))
                {
                    CurrentHero.items.Add(itemName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (itemName) =>
            {
                if (CurrentHero.items != null && CurrentHero.items.Remove(itemName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // 4. Custom Items (Instantiated directly from selected name string)
        // Available choices hook: pass your list of raw custom item name strings here
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Custom Item:",
            uniqueKey: "CustomItem",
            availableChoices: new List<string>(), // Hook: Put your string choices here
            currentActiveItems: CurrentHero.customItems?.Select(i => i.entityName).ToList() ?? new List<string>(), // Note: change to 'name' if ItemData uses 'name'
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (itemName) =>
            {
                if (CurrentHero.customItems == null)
                    CurrentHero.customItems = new List<ItemData>();

                if (!CurrentHero.customItems.Any(i => i.entityName == itemName)) // Note: change to 'name' if needed
                {
                    CurrentHero.customItems.Add(new ItemData { entityName = itemName }); // Note: change to 'name' if needed
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (itemName) =>
            {
                if (CurrentHero.customItems != null)
                {
                    var target = CurrentHero.customItems.FirstOrDefault(i => i.entityName == itemName); // Note: change to 'name' if needed
                    if (target != null && CurrentHero.customItems.Remove(target))
                    {
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            }
        );

        // 5. Traits (Strings)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Traits:",
            uniqueKey: "Trait",
            // Use the keys of the TraitNiceNames dictionary
            availableChoices: SDColors.TraitNiceNames.Keys.ToList(),
            currentActiveItems: CurrentHero.traits ?? new List<string>(),
            getKey: (traitName) => traitName,
            // Display both the key (name) and its dictionary value (description)
            getDisplay: (traitName) =>
            {
                if (SDColors.TraitNiceNames.TryGetValue(traitName, out string desc))
                {
                    return $"{traitName}: {desc}";
                }
                return traitName;
            },
            onAdd: (traitName) =>
            {
                if (CurrentHero.traits == null)
                    CurrentHero.traits = new List<string>();

                if (!CurrentHero.traits.Contains(traitName))
                {
                    CurrentHero.traits.Add(traitName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (traitName) =>
            {
                if (CurrentHero.traits != null && CurrentHero.traits.Remove(traitName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // 6. Blessings (Strings)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Blessing:",
            uniqueKey: "Blessing",
            // Use the keys from BlessingDataset.Blessings
            availableChoices: BlessingDataset.Blessings.Keys.ToList(),
            currentActiveItems: CurrentHero.blessings ?? new List<string>(),
            getKey: (blessingName) => blessingName,
            // Display both the key (name) and its description
            getDisplay: (blessingName) =>
            {
                if (BlessingDataset.Blessings.TryGetValue(blessingName, out string desc))
                {
                    return $"{blessingName}: {desc}";
                }
                return blessingName;
            },
            onAdd: (blessingName) =>
            {
                if (CurrentHero.blessings == null)
                    CurrentHero.blessings = new List<string>();

                if (!CurrentHero.blessings.Contains(blessingName))
                {
                    CurrentHero.blessings.Add(blessingName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (blessingName) =>
            {
                if (CurrentHero.blessings != null && CurrentHero.blessings.Remove(blessingName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // 7. Curses (Strings)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Curse:",
            uniqueKey: "Curse",
            // Use the keys from CurseDataset.Curses
            availableChoices: CurseDataset.Curses.Keys.ToList(),
            currentActiveItems: CurrentHero.curses ?? new List<string>(),
            getKey: (curseName) => curseName,
            // Display both the key (name) and its description
            getDisplay: (curseName) =>
            {
                if (CurseDataset.Curses.TryGetValue(curseName, out string desc))
                {
                    return $"{curseName}: {desc}";
                }
                return curseName;
            },
            onAdd: (curseName) =>
            {
                if (CurrentHero.curses == null)
                    CurrentHero.curses = new List<string>();

                if (!CurrentHero.curses.Contains(curseName))
                {
                    CurrentHero.curses.Add(curseName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (curseName) =>
            {
                if (CurrentHero.curses != null && CurrentHero.curses.Remove(curseName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );
        return layout;
    }
    private List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        string[] keywordOptions = EntityUIHelpers.GetKeywordOptions();

        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            var face = CurrentHero.diceSides[index];
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            // Calculate total rows for this face dynamically:
            // 1 (header) + 1 (base) + 1 (pips) + 3 (HSV) + 1 (add dropdown) + N (keywords) + 1 (copy/paste) = 8 + N
            int totalFaceRows = 8 + face.keywords.Count;

            // Encompass the entire layout cleanly inside one unified background frame
            var diceBgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f));
            diceBgRow.isBackground = true;
            diceBgRow.rowSpan = totalFaceRows;
            layout.Add(diceBgRow);

            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Base:", 0.15f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index)),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { face.effectID = id; NotifyStateChanged(); } }),
                GridCellSpec.CreateLabel("Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; NotifyStateChanged(); })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Pips:", 0.25f),
                GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => { if (int.TryParse(val, out int p)) { face.pips = p; NotifyStateChanged(); } }),
                GridCellSpec.CreateButton($"BtnPipDown_{index}", "V", 0.20f, () => { face.pips = Mathf.Max(0, face.pips - 1); NotifyStateChanged(); }),
                GridCellSpec.CreateButton($"BtnPipUp_{index}", "^", 0.20f, () => { face.pips++; NotifyStateChanged(); })
            ));

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

            // NOTE: Inner kwBgRow background spec removed from here to prevent container fragmentation.

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
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.50f, () => CopyDiceFace(index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.50f, () => PasteDiceFace(index))
            ));

            if (tabIndex == 0 && index < 5)
            {
                layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
            }
        }

        return layout;
    }
    private void RebuildStatsUI()
    {
        // Safety check to ensure the scroll container exists before generating
        if (statsScrollRect == null) return;

        bool wasDrawing = isDrawingUI;
        isDrawingUI = true;

        // Target the content panel of the ScrollView instead of the column panel
        statsUI = uiGenerator.RebuildGrid(statsScrollRect.content, GenerateStatsLayout());

        // Account for VerticalLayoutGroup spacing and padding dynamically
        float extraHeight = 0f;
        var layoutGroup = statsScrollRect.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = statsScrollRect.content.childCount;
            if (childCount > 1)
            {
                extraHeight += layoutGroup.spacing * (childCount - 1);
            }
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        // Dynamically adjust the height of the ScrollRect content so scrolling functions properly
        statsScrollRect.content.sizeDelta = new Vector2(0, statsUI.TotalHeight + extraHeight);

        // Force Unity UI to update immediately so scrollbars adjust correctly
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
                        CurrentHero.imageOverride = encodedStr;
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
    protected override void BuildUIAndBind()
    {
        // 1. Calculate dynamic baseline heights based on current parent Canvas dimensions
        float canvasHeight = 900f; // Baseline fallback
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null)
            {
                canvasHeight = canvasRt.rect.height;
            }
        }

        // Derive initial generation heights relative to the screen size
        float calculatedStatsHeight = canvasHeight - 60f;
        float calculatedDiceHeight = canvasHeight - uiGenerator.rowHeight - 80f;

        // Apply a safe minimum bound to prevent collapse on exceptionally small screens
        calculatedStatsHeight = Mathf.Max(calculatedStatsHeight, 400f);
        calculatedDiceHeight = Mathf.Max(calculatedDiceHeight, 300f);

        var columns = new List<ColumnSpec>
        {
            // Halved left margin (starts at 0.01f), expanded width
            new ColumnSpec("LeftStats", 0.01f, 0.35f, new List<GridRowSpec>
            {
                new GridRowSpec(calculatedStatsHeight, GridCellSpec.CreateScrollView("StatsScrollView", 1.0f))
            }),
            // Halved gap (starts at 0.365f)
            new ColumnSpec("MiddleDiceBase", 0.365f, 0.685f, new List<GridRowSpec>
            {
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateNavigationTabs("DiceTabs", new List<string> { "All", "Left", "Middle", "Top", "Bottom", "Right", "Rightmost" }, new List<GameObject>(), 1.0f, (idx) => {
                    currentDiceTab = idx;
                    RebuildDiceScrollView();
                })),
                new GridRowSpec(calculatedDiceHeight, GridCellSpec.CreateScrollView("DiceScrollView", 1.0f))
            }),
            // Halved gap (starts at 0.70f), halved right margin (ends at 0.99f)
            new ColumnSpec("RightOutput", 0.70f, 0.99f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        // Grab scroll rect references
        statsScrollRect = generatedScreen.ColumnRefs["LeftStats"].ScrollViews["StatsScrollView"];
        diceScrollRect = generatedScreen.ColumnRefs["MiddleDiceBase"].ScrollViews["DiceScrollView"];

        // Apply dynamic stretch and layouts to override the generated fixed heights
        ApplyDynamicLayoutConstraints();

        if (generatedScreen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }

        // Must explicitly build the scroll view contents immediately to prevent initialization crashes
        RebuildStatsUI();
        RebuildDiceScrollView();
    }
    private void RebuildDiceScrollView()
    {
        if (diceScrollRect == null) return;
        diceUI = uiGenerator.RebuildGrid(diceScrollRect.content, GenerateDiceLayout(currentDiceTab));

        // Account for VerticalLayoutGroup spacing and padding dynamically
        float extraHeight = 0f;
        var layoutGroup = diceScrollRect.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = diceScrollRect.content.childCount;
            if (childCount > 1)
            {
                extraHeight += layoutGroup.spacing * (childCount - 1);
            }
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        diceScrollRect.content.sizeDelta = new Vector2(0, diceUI.TotalHeight + extraHeight);

        Canvas.ForceUpdateCanvases();
        UpdateUIFromData();
    }
    private void BuildRightPanelContent(RectTransform parent)
    {
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
        previewContainer.transform.SetParent(parent, false);
        FullScreenUIGenerator.SetAnchors(previewContainer.GetComponent<RectTransform>(), 0.05f, 0.7f, 0.95f, 0.95f);

        if (uiGenerator.PortraitPanel != null)
        {
            GameObject portraitObj = Instantiate(uiGenerator.PortraitPanel, previewContainer.transform, false);
            portraitPreview = portraitObj.GetComponentInChildren<PortraitPreviewUI>();
            if (portraitPreview == null)
            {
                Debug.LogError("Portrait Preview Cannot be found!", this);
            }

            portraitPreview.OnFaceSelected += (idx) => {
                currentDiceTab = idx;
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
            if (script != null && !(script is TextMeshProUGUI))
            {
                DestroyImmediate(script);
            }
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

        FullScreenUIGenerator.SetAnchors(inputObj.GetComponent<RectTransform>(), 0.0f, 0.08f, 1.0f, 0.58f);

        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Copy Hero String";
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => GUIUtility.systemCopyBuffer = HeroData.Export(CurrentHero));
        FullScreenUIGenerator.SetAnchors(copyBtnObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Paste Hero String";
        pasteBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => OnPasteHeroString(GUIUtility.systemCopyBuffer));
        FullScreenUIGenerator.SetAnchors(pasteBtnObj.GetComponent<RectTransform>(), 0.52f, 0.0f, 1.0f, 0.06f);
    }
    private void AddAbilityToHero(int dropdownValue)
    {
        if (dropdownValue <= 0) return;

        var selectedAbility = BaseAbilityDatabase.Abilities[dropdownValue - 1];

        if (CurrentHero.AddAbility(selectedAbility.name))
        {
            NotifyStateChanged();
            RebuildStatsUI();
        }
    }
    private void RemoveAbilityFromHero(string abilityName)
    {
        if (CurrentHero.RemoveAbility(abilityName))
        {
            NotifyStateChanged();
            RebuildStatsUI();
        }
    }

    private void ApplyDynamicLayoutConstraints()
    {
        // 1. Stretch the Left Panel Stats ScrollView to fill 100% of its column container
        if (statsScrollRect != null)
        {
            RectTransform scrollRt = statsScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            // Make the container layout-flexible in case the column uses a VerticalLayoutGroup
            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);

            // Force dynamic anchors to stretch to 100% of the parent panel height
            StretchToParent(rowRt, 10f, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }

        // 2. Stretch the Middle Panel Dice ScrollView to fill everything below the navigation tabs
        if (diceScrollRect != null)
        {
            RectTransform scrollRt = diceScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;

            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);

            // Offset the top constraint down by the height of the navigation tabs (plus 10px spacing)
            float topOffset = uiGenerator.rowHeight + 15f;

            StretchToParent(rowRt, topOffset, 10f);
            StretchToParent(scrollRt, 0f, 0f);
        }
    }

    private void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;

        var layoutElement = target.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        }

        // Setting flexibleHeight to 1 instructs layout groups to stretch this element to fill unused space
        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1f;
    }

    private void StretchToParent(RectTransform rt, float topOffset, float bottomOffset)
    {
        if (rt == null) return;

        // Anchors min(0,0) and max(1,1) bounds the RectTransform corners to stretch with its parent
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, bottomOffset);
        rt.offsetMax = new Vector2(0f, -topOffset);
    }
}

/*

// =====================================================================
// HERO SPECIFIC UI
// =====================================================================
public class HeroUI : EntityUI<HeroData>
{
    protected override void InitializeSpecifics()
    {
        HeroUIHelpers.Initialize();
    }

    protected override bool AllowFacades() => true;

    protected override string ExportEntity(HeroData entity) => HeroData.Export(entity);
    protected override HeroData ParseEntity(string data) => HeroData.Parse(data);
    protected override Sprite GetBaseDiceSprite(int effectID) => HeroUIHelpers.GetBaseSprite(effectID);
    protected override Sprite GetFacadeDiceSprite(string facadeID) => HeroUIHelpers.GetFacadeSprite(facadeID);

    protected override void OpenBaseModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = HeroUIHelpers.BaseActionSprites,
            IsValid = (index, sprite) => HeroUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) => sprite.name,
            GetTooltip = (index, sprite) => HeroUIHelpers.GetBaseTooltip(sprite),

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        CurrentEntity.diceSides[faceIndex].effectID = parsedId;
                        NotifyStateChanged();
                        RebuildDiceScrollView();
                    }
                }
            }
        };
        diceFaceIconPicker.OpenModal(config);
    }

    protected override void OpenFacadeModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = HeroUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => HeroUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) => sprite.name,
            GetTooltip = (index, sprite) => sprite.name,

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length >= 2)
                    {
                        string facadeStr = $"{parts[0]}{parts[1]}";
                        CurrentEntity.diceSides[faceIndex].facadeID = facadeStr;
                        NotifyStateChanged();
                        RebuildDiceScrollView();
                    }
                }
            }
        };
        diceFaceIconPicker.OpenModal(config);
    }

    private void OpenHeroPortraitsModal(Action<HeroType, Sprite> onHeroSelected)
    {
        if (diceFaceIconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = HeroUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name),
            GetSearchName = (index, sprite) => HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero) ? hero.ToString() : sprite.name,
            GetTooltip = (index, sprite) => HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero) ? hero.ToString() : sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                    onHeroSelected?.Invoke(hero, sprite);
            }
        };
        diceFaceIconPicker.OpenModal(config);
    }

    private void OpenAllPortraitsModal(Action<bool, int, Sprite> onPortraitSelected)
    {
        if (diceFaceIconPicker == null) return;
        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = HeroUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && (HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name) || HeroSpriteDatabase.SpriteToMonsterMap.ContainsKey(sprite.name)),
            GetSearchName = (index, sprite) => HeroUIHelpers.GetPortraitDisplayName(sprite),
            GetTooltip = (index, sprite) => HeroUIHelpers.GetPortraitDisplayName(sprite),
            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                    onPortraitSelected?.Invoke(true, (int)hero, sprite);
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                    onPortraitSelected?.Invoke(false, (int)monster, sprite);
            }
        };
        diceFaceIconPicker.OpenModal(config);
    }

    protected override void UpdateSpecificUIFromData()
    {
        if (statsUI.Inputs.TryGetValue("Tier", out var tierIn)) tierIn.SetTextWithoutNotify(CurrentEntity.tier.ToString());
        if (statsUI.Inputs.TryGetValue("ReplicaName", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentEntity.baseReplica);
        if (statsUI.Inputs.TryGetValue("OverrideName", out var overNameIn)) overNameIn.SetTextWithoutNotify(CurrentEntity.imageOverride);
        if (statsUI.Inputs.TryGetValue("Speech", out var speechIn)) speechIn.SetTextWithoutNotify(CurrentEntity.speech);

        if (statsUI.Dropdowns.TryGetValue("Color", out var colDrop))
        {
            HeroColorOption colOpt = HeroUIHelpers.ReverseLookupColor(CurrentEntity.colorClass);
            colDrop.SetValueWithoutNotify((int)colOpt);
        }
    }

    protected override void UpdateSpecificVisuals()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetTierText(CurrentEntity.tier.ToString());
            HeroColorOption colOpt = HeroUIHelpers.ReverseLookupColor(CurrentEntity.colorClass);
            portraitPreview.SetHeroColor(SDColors.GetColor(colOpt));

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentEntity.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                portraitPreview.portrait.sprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Sprite targetSprite = HeroUIHelpers.GetSpriteForPortrait(string.IsNullOrEmpty(CurrentEntity.imageOverride) || CurrentEntity.imageOverride == "None" ? CurrentEntity.baseReplica : CurrentEntity.imageOverride);
                if (targetSprite != null && portraitPreview.portrait != null) portraitPreview.portrait.sprite = targetSprite;
            }
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("ReplicaBtn", out var replicaBtn))
            {
                Sprite s = HeroUIHelpers.GetSpriteForPortrait(CurrentEntity.baseReplica);
                SetButtonIcon(replicaBtn, s);
            }
            if (statsUI.Buttons.TryGetValue("OverrideBtn", out var overrideBtn))
            {
                Sprite s = HeroUIHelpers.GetSpriteForPortrait(CurrentEntity.imageOverride);
                SetButtonIcon(overrideBtn, s);
            }
        }
    }

    protected override List<GridRowSpec> GenerateStatsLayout()
    {
        var layout = new List<GridRowSpec>();

        layout.Add(new GridRowSpec(GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)));

        var heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();
        if (heroes != null)
        {
            List<string> poolOptions = new List<string> { "Stand Alone Hero (New)" };
            foreach (var hero in heroes)
                poolOptions.Add(string.IsNullOrEmpty(hero.entityName) ? "New Hero" : hero.entityName);

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Mod Pool:", 0.20f),
                GridCellSpec.CreateDropdown("PoolDropdown", "", 0.50f, poolOptions.ToArray(), OnPoolDropdownChanged),
                GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
            ));
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentEntity.entityName = val; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Replica Base:", 0.35f),
            GridCellSpec.CreateDiceButton("ReplicaBtn", "P", 0.15f, () => OpenHeroPortraitsModal((selectedHero, selectedSprite) => {
                CurrentEntity.baseReplica = selectedHero.ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("ReplicaName", "Statue", 0.50f, (val) => { CurrentEntity.baseReplica = val; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Icon Override:", 0.30f),
            GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, () => OpenAllPortraitsModal((isHero, enumValue, selectedSprite) => {
                CurrentEntity.imageOverride = isHero ? ((HeroType)enumValue).ToString() : ((MonsterType)enumValue).ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("OverrideName", "None", 0.35f, (val) => { CurrentEntity.imageOverride = val; NotifyStateChanged(); }),
            GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.20f, ToggleCustomImagePanel)
        ));

        if (showCustomImagePanel) layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP:", 0.2f),
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => { if (int.TryParse(val, out int hp)) CurrentEntity.hp = hp; NotifyStateChanged(); }),
            GridCellSpec.CreateLabel("Tier:", 0.2f),
            GridCellSpec.CreateInput("Tier", "", 0.3f, (val) => { if (int.TryParse(val, out int t)) CurrentEntity.tier = t; NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hue:", 0.30f),
            GridCellSpec.CreateSlider("EntitySliH", -99, 99, true, 0.50f, (val) => UpdateEntityHsvData(0, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("EntityFacH", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateEntityHsvData(0, h); })
        ));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Sat:", 0.30f),
            GridCellSpec.CreateSlider("EntitySliS", -99, 99, true, 0.50f, (val) => UpdateEntityHsvData(1, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("EntityFacS", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateEntityHsvData(1, s); })
        ));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Val:", 0.30f),
            GridCellSpec.CreateSlider("EntitySliV", -99, 99, true, 0.50f, (val) => UpdateEntityHsvData(2, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("EntityFacV", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateEntityHsvData(2, v); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Color Class:", 0.35f),
            GridCellSpec.CreateDropdown("Color", "", 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                CurrentEntity.colorClass = SDColors.GetColorCode((HeroColorOption)val);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Speech:", 0.20f),
            GridCellSpec.CreateInput("Speech", "", 0.80f, (val) => { CurrentEntity.speech = val; NotifyStateChanged(); })
        ));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentEntity.doc = val; NotifyStateChanged(); })
        ));

        AppendCollectionSelector<BaseAbility>(
            layout: layout, label: "Add Ability:", uniqueKey: "BaseAbility",
            availableChoices: BaseAbilityDatabase.Abilities,
            currentActiveItems: CurrentEntity.baseAbilityData,
            getKey: (ability) => ability.name,
            getDisplay: (ability) => $"{ability.name} ({ability.cost}): {(ability.effect ?? "").Replace("\n", " | ")}",
            onAdd: (abilityName) => {
                if (CurrentEntity.AddAbility(abilityName)) { NotifyStateChanged(); RebuildStatsUI(); }
            },
            onRemove: (abilityName) => {
                if (CurrentEntity.RemoveAbility(abilityName)) { NotifyStateChanged(); RebuildStatsUI(); }
            }
        );

        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Ability:", uniqueKey: "CustomAbility",
            availableChoices: new List<string>(),
            currentActiveItems: CurrentEntity.customAbilityData?.Select(a => a.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name, getDisplay: (name) => name,
            onAdd: (abilityName) => { Custom logic },
            onRemove: (abilityName) => {
                if (CurrentEntity.customAbilityData != null)
                {
                    var target = CurrentEntity.customAbilityData.FirstOrDefault(a => a.entityName == abilityName);
                    if (target != null && CurrentEntity.customAbilityData.Remove(target)) { NotifyStateChanged(); RebuildStatsUI(); }
                }
            }
        );

string[] formattedItemNames = Enum.GetNames(typeof(BaseItems)).Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2")).ToArray();
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

AppendCollectionSelector<string>(
    layout: layout, label: "Add Custom Item:", uniqueKey: "CustomItem",
    availableChoices: new List<string>(),
    currentActiveItems: CurrentEntity.customItems?.Select(i => i.entityName).ToList() ?? new List<string>(),
    getKey: (name) => name, getDisplay: (name) => name,
    onAdd: (itemName) => {
        if (CurrentEntity.customItems == null) CurrentEntity.customItems = new List<ItemData>();
        if (!CurrentEntity.customItems.Any(i => i.entityName == itemName))
        {
            CurrentEntity.customItems.Add(new ItemData { entityName = itemName });
            NotifyStateChanged(); RebuildStatsUI();
        }
    },
    onRemove: (itemName) => {
        if (CurrentEntity.customItems != null)
        {
            var target = CurrentEntity.customItems.FirstOrDefault(i => i.entityName == itemName);
            if (target != null && CurrentEntity.customItems.Remove(target)) { NotifyStateChanged(); RebuildStatsUI(); }
        }
    }
);

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

AppendCollectionSelector<string>(
    layout: layout, label: "Add Blessing:", uniqueKey: "Blessing",
    availableChoices: BlessingDataset.Blessings.Keys.ToList(),
    currentActiveItems: CurrentEntity.blessings ?? new List<string>(),
    getKey: (blessingName) => blessingName,
    getDisplay: (blessingName) => BlessingDataset.Blessings.TryGetValue(blessingName, out string desc) ? $"{blessingName}: {desc}" : blessingName,
    onAdd: (blessingName) => {
        if (CurrentEntity.blessings == null) CurrentEntity.blessings = new List<string>();
        if (!CurrentEntity.blessings.Contains(blessingName)) { CurrentEntity.blessings.Add(blessingName); NotifyStateChanged(); RebuildStatsUI(); }
    },
    onRemove: (blessingName) => {
        if (CurrentEntity.blessings != null && CurrentEntity.blessings.Remove(blessingName)) { NotifyStateChanged(); RebuildStatsUI(); }
    }
);

AppendCollectionSelector<string>(
    layout: layout, label: "Add Curse:", uniqueKey: "Curse",
    availableChoices: CurseDataset.Curses.Keys.ToList(),
    currentActiveItems: CurrentEntity.curses ?? new List<string>(),
    getKey: (curseName) => curseName,
    getDisplay: (curseName) => CurseDataset.Curses.TryGetValue(curseName, out string desc) ? $"{curseName}: {desc}" : curseName,
    onAdd: (curseName) => {
        if (CurrentEntity.curses == null) CurrentEntity.curses = new List<string>();
        if (!CurrentEntity.curses.Contains(curseName)) { CurrentEntity.curses.Add(curseName); NotifyStateChanged(); RebuildStatsUI(); }
    },
    onRemove: (curseName) => {
        if (CurrentEntity.curses != null && CurrentEntity.curses.Remove(curseName)) { NotifyStateChanged(); RebuildStatsUI(); }
    }
);

return layout;
    }
}
*/