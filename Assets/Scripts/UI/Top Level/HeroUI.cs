using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

public class HeroUI : RootUI
{
    //[Header("UI Modal References")]
    private IconPickerModal iconPicker;
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

    private Sprite _customImageCachedSprite;

    private bool _needsRebuild = false;

    private HeroData CurrentHero
    {
        get
        {
            if (ModPackage.Instance == null) return null;

            var hero = ModPackage.Instance.GetActiveEntity<HeroData>();

            // If no active editing session exists yet, auto-provision a clean template
            if (hero == null)
            {
                HeroData newHero = new HeroData();
                newHero.InitializeAsDefault();
                ModPackage.Instance.LoadEntityForEditing(newHero);
                hero = ModPackage.Instance.GetActiveEntity<HeroData>();
            }

            return hero;
        }
    }
    private IReadOnlyList<HeroData> AllHeroes => ModPackage.Instance.loadedMod.GetAll<HeroData>();

    public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        uiGenerator = uiGeneratorRef;

        if (iconPicker == null)
            iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);

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
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.BaseActionSprites,

            // CHANGED: Filter out any base sprites with an ID greater than the absolute hero limit of 187
            IsValid = (index, sprite) =>
            {
                if (sprite == null || !EntityUIHelpers.IsSpriteValid(sprite)) return false;

                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        return parsedId >= 0 && parsedId <= 187;
                    }
                }
                return true;
            },

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

        iconPicker.OpenModal(config);
    }
    private void OpenFacadeModal(int faceIndex)
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),

            // CHANGED: Do not attempt to query base databases for community sprites with bas_ IDs > 187
            GetSearchName = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        if (parsedId > 187) return IconPickerModal.GetCleanLeafName(sprite.name);
                    }
                }
                return sprite.name;
            },

            // CHANGED: Apply same tooltip fallback logic for community facades using bas_ IDs > 187
            GetTooltip = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        if (parsedId > 187) return $"Community Facade [{IconPickerModal.GetCleanLeafName(sprite.name)}]";
                    }
                }
                return sprite.name;
            },

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

                        // TRANSLATION LAYER: Legacy Engine Quirk
                        // Official base game monster faces are stored sequentially as "bas{ID}" after the 187 hero faces.
                        // We must convert their local IDs into the global monolithic array index.
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
                            // Community sprites (and standard 'bas' sprites) format normally as prefix + ID
                            facadeStr = $"{parts[0]}{parts[1]}";
                        }

                        CurrentHero.diceSides[faceIndex].facadeID = facadeStr;
                    }
                    else
                    {
                        // Fallback case for non-standard community structures
                        CurrentHero.diceSides[faceIndex].facadeID = filename;
                    }

                    NotifyStateChanged();
                    RebuildDiceScrollView();
                }
            }
        };

        iconPicker.OpenModal(config);
    }
    private void OpenHeroPortraitsModal(Action<HeroType, Sprite> onHeroSelected)
    {
        if (iconPicker == null) return;

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

        iconPicker.OpenModal(config);
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
                {
                    onPortraitSelected?.Invoke(true, (int)hero, sprite);
                }
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                {
                    onPortraitSelected?.Invoke(false, (int)monster, sprite);
                }
            }
        };

        iconPicker.OpenModal(config);
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
            ResolveFacadeName(face.facadeID), // <-- Wrap facadeID with the resolver
            face.effectID,
            face.facadeColor,
            face.pips
        );
    }
    private void OnPasteHeroString(string pastedString)
    {
        if (string.IsNullOrWhiteSpace(pastedString)) return;

        // Bypass the deleted Lexer and use the native parser directly
        HeroData importedHero = new HeroData();
        importedHero.Parse(pastedString);

        // Safely replace the active working clone with the imported hero
        ModPackage.Instance.UpdateActiveEntityClone<HeroData>(importedHero);
        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
        UpdateUIFromData();
    }

    // =====================================================================
    // STATE TO VIEW (PRESENTATION UPDATES)
    // =====================================================================

    /*
    private void OnStateChanged(object sender)
    {
        // 1. We are the ones typing -> Visuals only
        if (object.ReferenceEquals(sender, this))
        {
            UpdateVisualsOnly();
            return;
        }

        // 2. The other tab is typing -> Ignore completely
        if (sender != null) return;

        // 3. A true save occurred. If we are hidden, defer the rebuild until we are opened!
        if (!gameObject.activeInHierarchy)
        {
            _needsRebuild = true;
            return;
        }

        RebuildStatsUI();
        RebuildDiceScrollView();
    }
    */

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this))
        {
            UpdateVisualsOnly();
            return;
        }

        if (sender != null) return;

        bool isVisible = IsTabVisible();
        Debug.Log($"[DEBUG HeroUI] OnStateChanged received (Null Sender / Database change). IsTabVisible? {isVisible}");

        if (!isVisible)
        {
            _needsRebuild = true;
            return;
        }

        RebuildStatsUI();
        RebuildDiceScrollView();
    }

    private void Update()
    {
        if (_needsRebuild && IsTabVisible())
        {
            Debug.Log("[DEBUG HeroUI] Deferred rebuild firing! Tab is now visible.");
            _needsRebuild = false;
            RebuildStatsUI();
            RebuildDiceScrollView();
        }
    }

    // Hook into Unity's OnEnable to rebuild safely when the tab is clicked
    private void OnEnable()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            RebuildStatsUI();
            RebuildDiceScrollView();
        }
    }

    private void UpdateUIFromData()
    {
        if (statsUI == null || diceUI == null) return;

        isDrawingUI = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(CurrentHero.entityName);
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn))
            hpIn.SetTextWithoutNotify(CurrentHero.hp > 0 ? CurrentHero.hp.ToString() : "");
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

        if (statsUI.Sliders.TryGetValue("PhueRangeSlider", out var phueRangeSlider))
            phueRangeSlider.SetValueWithoutNotify(CurrentHero.phue.colorRange);

        if (statsUI.Sliders.TryGetValue("ThueRangeSlider", out var thueRangeSlider))
            thueRangeSlider.SetValueWithoutNotify(CurrentHero.thue.colorRange);

        if (statsUI.Sliders.TryGetValue("ThueOffsetSlider", out var thueOffsetSlider))
            thueOffsetSlider.SetValueWithoutNotify(CurrentHero.thue.colorOffset);

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
            portraitPreview.SetHPText(CurrentHero.hp > 0 ? CurrentHero.hp.ToString() : "");

            portraitPreview.SetTierText(CurrentHero.tier.ToString());

            HeroColorOption colOpt = EntityUIHelpers.ReverseLookupColor(CurrentHero.colorClass);
            portraitPreview.SetHeroColor(SDColors.GetColor(colOpt));

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentHero.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                // Fix the lag: Only create the sprite once, don't recreate it every frame
                if (_customImageCachedSprite == null)
                {
                    _customImageCachedSprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
                }
                portraitPreview.portrait.sprite = _customImageCachedSprite;
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
            portraitPreview.SetPortraitTHue(CurrentHero.thue);
            portraitPreview.SetPortraitPHue(CurrentHero.phue);
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

            if (statsUI.Buttons.TryGetValue("PhueStartBtn", out var phueStartBtn))
            {
                Color c = CurrentHero.phue != null ? CurrentHero.phue.colorStart : Color.white;
                SetButtonColorPreview(phueStartBtn, c);
            }
            if (statsUI.Buttons.TryGetValue("PhueDestBtn", out var phueDestBtn))
            {
                Color c = CurrentHero.phue != null ? CurrentHero.phue.colorDestination : Color.white;
                SetButtonColorPreview(phueDestBtn, c);
            }
            if (statsUI.Buttons.TryGetValue("ThueColorBtn", out var thueColorBtn))
            {
                Color c = CurrentHero.thue != null ? CurrentHero.thue.colorHex : Color.white;
                SetButtonColorPreview(thueColorBtn, c);
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
                    // <-- Wrap facadeID with the resolver here too
                    Sprite s = EntityUIHelpers.GetFacadeSprite(ResolveFacadeName(face.facadeID));
                    SetButtonIcon(facBtn, s);
                }
            }
        }

        if (rawTextOutput != null)
        {
            string exportedString = CurrentHero.Export();
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
        HeroData hero = new HeroData();
        hero.InitializeAsDefault();

        ModPackage.Instance.UpdateActiveEntityClone<HeroData>(hero);
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
    private void ClearDiceFace(int index)
    {
        CurrentHero.diceSides[index] = new DiceSideData();
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
        if (isDrawingUI) return;

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
            HeroData hero = new HeroData();
            hero.InitializeAsDefault();
            ModPackage.Instance.LoadEntityForEditing(hero);
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

        /*
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
        */

        string poolBtnText = _currentPoolIndex == 0 ? "Mod Pool: New Hero" : $"Mod Pool: {CurrentHero.entityName}";
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnOpenPool", poolBtnText, 0.70f, OpenModPoolModal),
            GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { if (isDrawingUI) return; CurrentHero.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })));

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
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => {
                CurrentHero.hp = (string.IsNullOrWhiteSpace(val) || !int.TryParse(val, out int parsedHp)) ? 0 : parsedHp;
                NotifyStateChanged();
            }),
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
            GridCellSpec.CreateLabel("P-Hue Swap:", 0.30f),
            GridCellSpec.CreateButton("PhueStartBtn", "Target", 0.35f, () => {
                if (uiGenerator.colorPicker == null) return;

                Color initialColor = CurrentHero.phue != null ? CurrentHero.phue.colorStart : Color.white;

                OpenColorPicker(initialColor, (color) => {
                    if (CurrentHero.phue == null) CurrentHero.phue = new Phue();
                    CurrentHero.phue.colorStart = color;
                    NotifyStateChanged();
                });
            }),
            GridCellSpec.CreateButton("PhueDestBtn", "Replace", 0.35f, () => {
                if (uiGenerator.colorPicker == null) return;

                Color initialColor = CurrentHero.phue != null ? CurrentHero.phue.colorDestination : Color.white;

                OpenColorPicker(initialColor, (color) => {
                    if (CurrentHero.phue == null) CurrentHero.phue = new Phue();
                    CurrentHero.phue.colorDestination = color;
                    NotifyStateChanged();
                });
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("P-Hue Range:", 0.30f),
            GridCellSpec.CreateSlider("PhueRangeSlider", 0, 99, true, 0.70f, (val) => {
                if (CurrentHero.phue == null) CurrentHero.phue = new Phue();
                CurrentHero.phue.colorRange = Mathf.RoundToInt(val);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("T-Hue Color:", 0.35f),
            GridCellSpec.CreateButton("ThueColorBtn", "Pick Color", 0.65f, () => {
                if (uiGenerator.colorPicker == null)
                {
                    Debug.LogWarning("FlexibleColorPicker reference is missing from the scene.");
                    return;
                }

                Color initialColor = Color.white;
                if (CurrentHero.thue != null)
                {
                    initialColor = CurrentHero.thue.colorHex;
                }

                OpenColorPicker(initialColor, (color) => {
                    if (CurrentHero.thue == null)
                    {
                        CurrentHero.thue = new Thue { colorRange = 0, colorOffset = 0 };
                    }

                    CurrentHero.thue.colorHex = color;
                    NotifyStateChanged();
                });
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("T-Hue Range:", 0.20f),
            GridCellSpec.CreateSlider("ThueRangeSlider", 0, 99, true, 0.30f, (val) => {
                CurrentHero.thue.colorRange = Mathf.RoundToInt(val);
                NotifyStateChanged();
            }),
            GridCellSpec.CreateLabel("T-Hue Shift:", 0.20f),
            GridCellSpec.CreateSlider("ThueOffsetSlider", -99, 99, true, 0.30f, (val) => {
                CurrentHero.thue.colorOffset = Mathf.RoundToInt(val);
                NotifyStateChanged();
            })
        ));

        HeroColorOption currentOption = SDColors.GetOptionFromColorCode(CurrentHero.colorClass);
        string currentFormattedName = SDColors.GetFormattedColorName(currentOption);

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Color Class:", 0.35f),
            GridCellSpec.CreateFilteredDropdown("Color", currentFormattedName, 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                HeroColorOption selectedColor = (HeroColorOption)val;
                CurrentHero.colorClass = SDColors.GetColorCode(selectedColor);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Speech:", 0.20f),
            GridCellSpec.CreateInput("Speech", "", 0.80f, (val) =>
            {
                CurrentHero.speech = val.SanitizeRichInput();
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentHero.doc = val.SanitizeRichInput(); NotifyStateChanged(); })
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

        var customAbilitiesList = ModPackage.Instance.CustomAbilities;
        Debug.Log($"[DEBUG HeroUI] Building Layout. ModPackage.Instance.CustomAbilities count: {(customAbilitiesList != null ? customAbilitiesList.Count : -1)}");

        if (customAbilitiesList != null)
        {
            for (int i = 0; i < customAbilitiesList.Count; i++)
            {
                var a = customAbilitiesList[i];
                Debug.Log($"[DEBUG HeroUI] Pool Ability [{i}]: Name='{a?.entityName}'");
            }
        }

        var customAbilityNames = customAbilitiesList?.Select(a => a.entityName).ToList() ?? new List<string>();
        Debug.Log($"[DEBUG HeroUI] Names passed to Dropdown: {string.Join(", ", customAbilityNames)}");

        // 2. Custom Abilities (Instantiated directly from selected name string)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Custom Ability:",
            uniqueKey: "CustomAbility",
            availableChoices: customAbilityNames,
            // Reading remains simple using the read-only property
            currentActiveItems: CurrentHero.customAbilityData?.Select(a => a.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (abilityName) =>
            {
                // Safely check if already present in our combined read-only property
                bool alreadyExists = CurrentHero.customAbilityData?.Any(a => a.entityName == abilityName) ?? false;

                if (!alreadyExists)
                {
                    // Retrieve the concrete template (SpellData or TacticData) from the pool
                    var template = ModPackage.Instance.CustomAbilities.FirstOrDefault(a => a.entityName == abilityName);
                    if (template != null)
                    {
                        // Clone the concrete subclass safely using its actual runtime type
                        string json = JsonUtility.ToJson(template);
                        AbilityData clonedAbility = JsonUtility.FromJson(json, template.GetType()) as AbilityData;

                        // Add using our new helper method
                        CurrentHero.AddCustomAbility(clonedAbility);
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (abilityName) =>
            {
                /*
                // Safely remove using our helper method
                if (CurrentHero.RemoveCustomAbility(abilityName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
                */
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
                {
                    Debug.LogError($"CUSTOM ITEMS NULL, CATATSROPHIC ERROR.", this);
                }
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

        CurrentHero.InitializeDiceFaces();

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
                //GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { face.effectID = id; NotifyStateChanged(); } }),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => {
                    if (string.IsNullOrWhiteSpace(val))
                    {
                        face.effectID = 0;
                        NotifyStateChanged();
                    }
                    else if (int.TryParse(val, out int id))
                    {
                        face.effectID = id;
                        NotifyStateChanged();
                    }
                }),

                GridCellSpec.CreateLabel("Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; NotifyStateChanged(); })
            ));

            layout.Add(new GridRowSpec(
                            GridCellSpec.CreateLabel("Pips:", 0.25f),
                            //GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => { if (int.TryParse(val, out int p)) { face.pips = p; NotifyStateChanged(); } }),
                            GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => {
                                if (string.IsNullOrWhiteSpace(val))
                                {
                                    face.pips = 0;
                                    NotifyStateChanged();
                                }
                                else if (int.TryParse(val, out int p))
                                {
                                    face.pips = p;
                                    NotifyStateChanged();
                                }
                            }),

                            GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => {
                                face.pips--; // Removed Mathf.Max restriction to allow negative values

                                // Retrieve the input field from diceUI to update the displayed text
                                if (diceUI != null && diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                                {
                                    input.SetTextWithoutNotify(face.pips.ToString());
                                }
                                NotifyStateChanged();
                            }),
                            GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => {
                                face.pips++;

                                // Retrieve the input field from diceUI to update the displayed text
                                if (diceUI != null && diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                                {
                                    input.SetTextWithoutNotify(face.pips.ToString());
                                }
                                NotifyStateChanged();
                            })
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
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.33f, () => CopyDiceFace(index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.33f, () => PasteDiceFace(index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Clear Dice", 0.33f, () => ClearDiceFace(index))
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
                        // Assign data
                        CurrentHero.imageOverride = encodedStr; // Use CurrentHero for HeroUI
                        _customImageString = encodedStr;
                        _customImageTexture = tex;

                        // CACHE the sprite allocation exactly once here!
                        if (_customImageCachedSprite != null) Destroy(_customImageCachedSprite);
                        _customImageCachedSprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));

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

        bool wasDrawing = isDrawingUI;
        isDrawingUI = true;

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

        isDrawingUI = wasDrawing; // RESTORE FLAG
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

        rawTextOutput.onValueChanged.AddListener((val) =>
        {
            if (syntaxHighlighterText != null)
            {
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(val);
            }
        });

        rawTextOutput.onEndEdit.AddListener((val) =>
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            if (val == CurrentHero.Export()) return;
            try
            {
                // Bypass the deleted Lexer and use the native parser directly
                HeroData importedHero = new HeroData();
                importedHero.Parse(val);

                if (importedHero != null)
                {
                    ModPackage.Instance.UpdateActiveEntityClone<HeroData>(importedHero);
                    ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);
                    UpdateUIFromData();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not parse manual edits to hero string: {ex.Message}");
            }
        });

        FullScreenUIGenerator.SetAnchors(inputObj.GetComponent<RectTransform>(), 0.0f, 0.08f, 1.0f, 0.58f);

        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Copy Hero String";
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => GUIUtility.systemCopyBuffer = CurrentHero.Export());
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
        /*
        if (CurrentHero.AddAbility(selectedAbility.name))
        {
            NotifyStateChanged();
            RebuildStatsUI();
        }
        */
    }
    private void RemoveAbilityFromHero(string abilityName)
    {
        /*
        if (CurrentHero.RemoveAbility(abilityName))
        {
            NotifyStateChanged();
            RebuildStatsUI();
        }
        */
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

    private void OpenModPoolModal()
    {
        if (iconPicker == null) return;

        var heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();
        Sprite[] heroSprites = new Sprite[heroes.Count + 1];

        heroSprites[0] = EntityUIHelpers.GetSpriteForPortrait("Statue");

        for (int i = 0; i < heroes.Count; i++)
        {
            var h = heroes[i];
            string imgStr = string.IsNullOrEmpty(h.imageOverride) || h.imageOverride == "None" ? h.baseReplica : h.imageOverride;
            heroSprites[i + 1] = EntityUIHelpers.GetSpriteForPortrait(imgStr);
        }

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = heroSprites,
            DisableDeduplication = true,
            AllowNullSprites = true,

            IsValid = (index, sprite) => true,

            CellSize = new Vector2(80,80),

            GetSearchName = (index, sprite) => index == 0 ? "Stand Alone Hero (New)" : heroes[index - 1].entityName,
            GetTooltip = (index, sprite) => index == 0 ? "Create a new blank hero" : heroes[index - 1].entityName,

            GetNameText = (index, sprite) => index == 0 ? "New Hero" : heroes[index - 1].entityName,
            GetTierText = (index, sprite) => index == 0 ? "" : heroes[index - 1].tier.ToString(),
            GetHPText = (index, sprite) => index == 0 ? "" : (heroes[index - 1].hp > 0 ? heroes[index - 1].hp.ToString() : ""),
            GetColor = (index, sprite) => index == 0
                ? Color.white
                : SDColors.GetColor(EntityUIHelpers.ReverseLookupColor(heroes[index - 1].colorClass)),

            OnSelectionMade = (index, sprite) =>
            {
                OnPoolHeroSelected(index);
            }
        };

        iconPicker.OpenModal(config);
    }

    private void OnPoolHeroSelected(int index)
    {
        if (isDrawingUI) return;
        _currentPoolIndex = index;

        var heroes = ModPackage.Instance.loadedMod.GetAll<HeroData>();

        if (index > 0 && (index - 1) < heroes.Count)
        {
            var originalHero = heroes[index - 1];
            ModPackage.Instance.LoadEntityForEditing(originalHero);
        }
        else
        {
            HeroData hero = new HeroData();
            hero.InitializeAsDefault();
            ModPackage.Instance.LoadEntityForEditing(hero);
        }

        ModPackage.Instance.NotifyActiveEntityChanged<HeroData>(this);

        RebuildStatsUI();
        RebuildDiceScrollView();
    }

    private string[] GetColorDropdownNames()
    {
        var options = (HeroColorOption[])Enum.GetValues(typeof(HeroColorOption));
        string[] formattedNames = new string[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            string hex = SDColors.GetColorHexForOption(options[i]);
            formattedNames[i] = $"<color=#{hex}>{options[i]}</color>";
        }
        return formattedNames;
    }

    private bool IsTabVisible()
    {
        RectTransform rootWrapper = GetRootWrapper();
        return rootWrapper != null && rootWrapper.gameObject.activeInHierarchy;
    }

    private void OpenColorPicker(Color initialColor, Action<Color> onColorChanged)
    {
        if (uiGenerator.colorPicker == null) return;

        uiGenerator.colorPicker.gameObject.SetActive(true);
        uiGenerator.colorPicker.SetColor(initialColor);

        uiGenerator.colorPicker.onColorChange.RemoveAllListeners();
        uiGenerator.colorPicker.onColorChange.AddListener(new UnityEngine.Events.UnityAction<Color>(onColorChanged));
    }

    private void CloseColorPicker()
    {
        if (uiGenerator.colorPicker != null)
        {
            uiGenerator.colorPicker.gameObject.SetActive(false);
        }
    }

    // Add this helper method anywhere inside the HeroUI class
    private string ResolveFacadeName(string facadeID)
    {
        if (string.IsNullOrEmpty(facadeID)) return facadeID;

        // 1. Try to get it directly first (works if it's already a full name)
        if (EntityUIHelpers.GetFacadeSprite(facadeID) != null) return facadeID;

        // 2. Pattern patch for S&D short names (e.g., "Aid9" -> "Aid", "9")
        var match = Regex.Match(facadeID, @"^([a-zA-Z]+)(\d+)$");
        if (match.Success)
        {
            string prefix = match.Groups[1].Value;
            string id = match.Groups[2].Value;
            string searchPrefix = $"{prefix}_{id}_"; // Rebuilds to "Aid_9_"

            // Legacy monolith fallback patching
            if (prefix.ToLower() == "bas" && int.TryParse(id, out int basId))
            {
                if (basId >= 188 && basId <= 219) searchPrefix = $"big_{basId - 188}_";
                else if (basId >= 220 && basId <= 247) searchPrefix = $"hug_{basId - 220}_";
                else if (basId >= 248 && basId <= 265) searchPrefix = $"tin_{basId - 248}_";
            }

            // Find the full sprite name in the database pool
            var sprite = EntityUIHelpers.AllActionSprites.FirstOrDefault(sp => sp != null && sp.name.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase));
            if (sprite != null)
            {
                return sprite.name;
            }
        }

        return facadeID;
    }

    private void SetButtonColorPreview(Button btn, Color color)
    {
        if (btn == null) return;

        // Reset the main button background to white to fix the "button turned black/invisible" issue
        if (btn.image != null) btn.image.color = Color.white;

        Transform preview = btn.transform.Find("ColorPreview");
        Image previewImg;
        if (preview == null)
        {
            // Dynamically instantiate a rect with a blank image
            GameObject go = new GameObject("ColorPreview", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(btn.transform, false);
            previewImg = go.GetComponent<Image>();

            // Anchor it as a neat little square on the right side of the button
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

        // Force alpha to 1 so the preview box is always fully opaque
        color.a = 1f;
        previewImg.color = color;
    }
}
