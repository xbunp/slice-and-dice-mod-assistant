using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityUI : RootUI
{
    // UI Modal References
    private IconPickerModal iconPicker;

    // Dynamically Generated Components
    private GridReferences statsUI;
    private GridReferences abilityDataUI;
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;
    private ScrollRect statsScrollRect;
    private ScrollRect abilityScrollRect;

    // Custom Ability Preview Image
    private Image previewIcon;
    private TextMeshProUGUI previewName;

    // Navigation and Mod Pool State
    private int _currentPoolIndex = 0;
    private bool isDrawingUI = false;

    // Custom Image State
    private bool showCustomImagePanel = false;
    private string _customImageString;
    private Texture2D _customImageTexture;
    private ImageReceiver _persistentCustomImageReceiver;

    // Tactic Cost Definition Array
    private readonly string[] TacticCostOptions = new string[]
    {
        "None", "Damage Pip", "Shield Pip", "Heal Pip", "Mana Pip", "Any Pip",
        "Blank Side", "1 Pip", "2 Pip", "3 Pip", "4 Pip",
        "1 Keyword", "2 Keyword", "4 Keyword"
    };

    private AbilityData CurrentAbility
    {
        get
        {
            if (ModPackage.Instance == null) return null;
            var ability = ModPackage.Instance.GetActiveEntity<AbilityData>();
            if (ability == null)
            {
                ModPackage.Instance.LoadEntityForEditing(CreateNewSpell());
                ability = ModPackage.Instance.GetActiveEntity<AbilityData>();
            }
            return ability;
        }
    }

    public override void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        uiGenerator = uiGeneratorRef;
        if (iconPicker == null) iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);

        EntityUIHelpers.Initialize();
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
            ModPackage.Instance.OnModDataChanged -= OnStateChanged;
    }

    // =====================================================================
    // ABILITY INSTANTIATION & MODE TOGGLING
    // =====================================================================

    private AbilityData CreateNewSpell()
    {
        SpellData newSpell = new SpellData();
        newSpell.entityName = "New Ability";
        newSpell.imageOverride = "None"; // Force blank starting state

        // Default Primary Effect
        newSpell.diceSides[0].effectID = 15; // Damage
        newSpell.diceSides[0].pips = 1;

        return newSpell;
    }

    private void ToggleAbilityMode()
    {
        bool isCurrentlySpell = CurrentAbility is SpellData;
        AbilityData newAbility = isCurrentlySpell ? (AbilityData)new TacticData() : new SpellData();

        // Copy shared fields across
        newAbility.entityName = CurrentAbility.entityName;
        newAbility.imageOverride = CurrentAbility.imageOverride;
        newAbility.baseReplica = CurrentAbility.baseReplica;
        newAbility.items = new List<string>(CurrentAbility.items);
        newAbility.h = CurrentAbility.h;
        newAbility.s = CurrentAbility.s;
        newAbility.v = CurrentAbility.v;

        // Copy Primary and Secondary faces explicitly
        newAbility.diceSides[0] = CurrentAbility.diceSides[0].Clone();
        newAbility.diceSides[1] = CurrentAbility.diceSides[1].Clone();

        ModPackage.Instance.UpdateActiveEntityClone<AbilityData>(newAbility);
        NotifyStateChanged();
        RebuildAbilityScrollView();
    }

    // =====================================================================
    // ICON MODAL SELECTION
    // =====================================================================

    private void OpenImageOverrideModal()
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && (EntityUIHelpers.IsSpriteValid(sprite) || (sprite.rect.width == 10 && sprite.rect.height == 10)),
            GetSearchName = (index, sprite) => IconPickerModal.GetCleanLeafName(sprite.name),
            GetTooltip = (index, sprite) => IconPickerModal.GetCleanLeafName(sprite.name),
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');

                    // FIX: Capitalization matters. Retain original casing instead of forcing .ToLower()
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        CurrentAbility.imageOverride = $"{parts[0]}{parts[1]}";
                    }
                    else
                    {
                        CurrentAbility.imageOverride = filename;
                    }

                    NotifyStateChanged();
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    private void OpenEffectBaseModal(int faceIndex, bool isPrimary)
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.BaseActionSprites,
            IsValid = (index, sprite) =>
            {
                if (sprite == null || !EntityUIHelpers.IsSpriteValid(sprite)) return false;
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        if (parsedId > 187) return false;

                        // HOOK: Add specific filtering logic here
                        if (isPrimary) return IsPrimaryEffectValid(parsedId);
                        else return IsSecondaryEffectValid(parsedId);
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
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        CurrentAbility.diceSides[faceIndex].effectID = parsedId;
                        NotifyStateChanged();
                        RebuildAbilityScrollView();
                    }
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    // HOOK: Filter logic for Side 1 (Primary Effect)
    private bool IsPrimaryEffectValid(int baseId)
    {
        return true;
    }

    // HOOK: Filter logic for Side 2 (Secondary Effect) - Untargeted only
    private bool IsSecondaryEffectValid(int baseId)
    {
        return true;
    }

    // =====================================================================
    // STATE TO VIEW (PRESENTATION UPDATES)
    // =====================================================================

    private void OnStateChanged(object sender)
    {
        // 1. We are the ones typing/editing -> Update our own visuals (Zero Lag)
        if (object.ReferenceEquals(sender, this))
        {
            UpdateVisualsOnly();
            return;
        }

        // 2. A true database change occurred (Ability Saved, Mod Loaded)
        // We MUST rebuild the UI here to pull the newly saved Custom Abilities into the dropdown!
        if (sender == null)
        {
            RebuildStatsUI();
            return;
        }

        // 3. Another tab (like AbilityUI) is actively typing -> Just sync text fields (Zero Lag)
        UpdateUIFromData();
    }

    private void UpdateUIFromData()
    {
        if (statsUI == null || abilityDataUI == null) return;
        isDrawingUI = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(CurrentAbility.entityName);
        if (statsUI.Inputs.TryGetValue("OverrideName", out var overNameIn)) overNameIn.SetTextWithoutNotify(CurrentAbility.imageOverride);

        if (statsUI.Sliders.TryGetValue("HeroSliH", out var shH)) shH.SetValueWithoutNotify(CurrentAbility.h);
        if (statsUI.Sliders.TryGetValue("HeroSliS", out var shS)) shS.SetValueWithoutNotify(CurrentAbility.s);
        if (statsUI.Sliders.TryGetValue("HeroSliV", out var shV)) shV.SetValueWithoutNotify(CurrentAbility.v);

        if (statsUI.Inputs.TryGetValue("HeroFacH", out var hH)) hH.SetTextWithoutNotify(CurrentAbility.h.ToString());
        if (statsUI.Inputs.TryGetValue("HeroFacS", out var hS)) hS.SetTextWithoutNotify(CurrentAbility.s.ToString());
        if (statsUI.Inputs.TryGetValue("HeroFacV", out var hV)) hV.SetTextWithoutNotify(CurrentAbility.v.ToString());

        // Update Dice Side fields dynamically 
        UpdateFaceUIElements(0); // Primary
        UpdateFaceUIElements(1); // Secondary

        if (CurrentAbility is SpellData spell)
        {
            if (abilityDataUI.Inputs.TryGetValue("ManaPips", out var manaPipIn)) manaPipIn.SetTextWithoutNotify(spell.manaCost.ToString());
        }
        else if (CurrentAbility is TacticData tactic)
        {
            if (abilityDataUI.Inputs.TryGetValue("TacPip_2", out var tPip2)) tPip2.SetTextWithoutNotify(tactic.TacticCostTop.pips.ToString());
            if (abilityDataUI.Inputs.TryGetValue("TacPip_3", out var tPip3)) tPip3.SetTextWithoutNotify(tactic.TacticCostBottom.pips.ToString());
            if (abilityDataUI.Inputs.TryGetValue("TacPip_5", out var tPip5)) tPip5.SetTextWithoutNotify(tactic.TacticCostRightmost.pips.ToString());

            // Dropdowns
            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_2", out var tDrop2)) tDrop2.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostTop));
            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_3", out var tDrop3)) tDrop3.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostBottom));
            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_5", out var tDrop5)) tDrop5.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostRightmost));
        }

        isDrawingUI = false;
        UpdateVisualsOnly();
    }

    private void UpdateFaceUIElements(int index)
    {
        var face = CurrentAbility.diceSides[index];
        if (abilityDataUI.Inputs.TryGetValue($"ID_{index}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
        if (abilityDataUI.Inputs.TryGetValue($"Pips_{index}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());
    }

    private void UpdateVisualsOnly()
    {
        if (previewName != null) previewName.text = CurrentAbility.entityName;

        if (previewIcon != null)
        {
            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentAbility.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null)
            {
                previewIcon.sprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                Sprite targetSprite = EntityUIHelpers.GetFacadeSprite(CurrentAbility.imageOverride);
                previewIcon.sprite = targetSprite != null ? targetSprite : EntityUIHelpers.GetFacadeSprite("SpellPlaceholder");
            }

            // Reset flat color and apply HSV parameters directly to the material
            previewIcon.color = Color.white;
            if (previewIcon.material != null)
            {
                previewIcon.material.SetFloat("_Hue", CurrentAbility.h);
                previewIcon.material.SetFloat("_Saturation", CurrentAbility.s);
                previewIcon.material.SetFloat("_Value", CurrentAbility.v);
            }
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("OverrideBtn", out var overrideBtn))
                SetButtonIcon(overrideBtn, EntityUIHelpers.GetFacadeSprite(CurrentAbility.imageOverride));
        }

        for (int i = 0; i <= 1; i++) // Update Primary & Secondary Base Icons
        {
            if (abilityDataUI != null && abilityDataUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
                SetButtonIcon(baseBtn, EntityUIHelpers.GetBaseSprite(CurrentAbility.diceSides[i].effectID));
        }

        if (rawTextOutput != null)
        {
            // FIX: Wrap directly in standalone abilitydata.(...) syntax without leading dot
            string exportedString = $"abilitydata.{CurrentAbility.ExportWrapped()}";
            rawTextOutput.SetTextWithoutNotify(exportedString);

            if (syntaxHighlighterText != null)
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(exportedString);
        }
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

    // =====================================================================
    // VIEW TO STATE DISPATCHERS
    // =====================================================================

    private void NotifyStateChanged()
    {
        if (isDrawingUI) return;
        ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
    }

    private void ResetToDefault()
    {
        ModPackage.Instance.UpdateActiveEntityClone<AbilityData>(CreateNewSpell());
        showCustomImagePanel = false;
        _currentPoolIndex = 0;

        ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
        RebuildStatsUI();
        RebuildAbilityScrollView();
    }

    private void AddKeywordToFace(int faceIndex, int dropdownValue)
    {
        if (dropdownValue <= 0) return;

        // Retrieve the filtered list
        var faceKeywords = GetFilteredFaceKeywords();
        string targetKeyword = faceKeywords[dropdownValue]; // Index matches directly as "" is index 0

        if (!CurrentAbility.diceSides[faceIndex].keywords.Contains(targetKeyword))
        {
            CurrentAbility.diceSides[faceIndex].keywords.Add(targetKeyword);
            NotifyStateChanged();
            RebuildAbilityScrollView();
        }
    }

    private void RemoveKeywordFromFace(int faceIndex, string keyword)
    {
        if (CurrentAbility.diceSides[faceIndex].keywords.Remove(keyword))
        {
            NotifyStateChanged();
            RebuildAbilityScrollView();
        }
    }

    private void UpdateAbilityHsvData(int componentIndex, int value)
    {
        if (componentIndex == 0) CurrentAbility.h = value;
        else if (componentIndex == 1) CurrentAbility.s = value;
        else if (componentIndex == 2) CurrentAbility.v = value;

        string inputKey = componentIndex == 0 ? "HeroFacH" : (componentIndex == 1 ? "HeroFacS" : "HeroFacV");
        if (statsUI != null && statsUI.Inputs.TryGetValue(inputKey, out var input))
            input.SetTextWithoutNotify(value.ToString());

        string sliderKey = componentIndex == 0 ? "HeroSliH" : (componentIndex == 1 ? "HeroSliS" : "HeroSliV");
        if (statsUI != null && statsUI.Sliders.TryGetValue(sliderKey, out var slider))
            slider.SetValueWithoutNotify(value);

        NotifyStateChanged();
    }

    private void ApplyTacticCost(int faceIndex, int dropdownIndex, int pips)
    {
        var face = CurrentAbility.diceSides[faceIndex];

        switch (dropdownIndex)
        {
            case 0: face.effectID = 0; face.pips = 0; break; // None
            case 1: face.effectID = 15; face.pips = pips; break; // Damage
            case 2: face.effectID = 56; face.pips = pips; break; // Shield
            case 3: face.effectID = 103; face.pips = pips; break; // Heal
            case 4: face.effectID = 76; face.pips = pips; break; // Mana
            case 5: face.effectID = 136; face.pips = pips; break; // Any
            case 6: face.effectID = 8; face.pips = 0; break; // Blank
            case 7: face.effectID = 177; face.pips = 1; break; // 1 Pip
            case 8: face.effectID = 177; face.pips = 2; break; // 2 Pip
            case 9: face.effectID = 177; face.pips = 3; break; // 3 Pip
            case 10: face.effectID = 177; face.pips = 4; break; // 4 Pip
            case 11: face.effectID = 13; face.pips = pips; break; // 1 Keyword Hook (I Die Cantrip defaults to 1 kw)
            case 12: face.effectID = 0; face.pips = pips; break; // HOOK: 2 Keyword
            case 13: face.effectID = 0; face.pips = pips; break; // HOOK: 4 Keyword
        }

        NotifyStateChanged();
    }

    private int GetTacticCostDropdownIndex(DiceSideData face)
    {
        if (face.effectID == 0) return 0;
        if (face.effectID == 15) return 1;
        if (face.effectID == 56) return 2;
        if (face.effectID == 103) return 3;
        if (face.effectID == 76) return 4;
        if (face.effectID == 136) return 5;
        if (face.effectID == 8) return 6;
        if (face.effectID == 177)
        {
            if (face.pips == 1) return 7;
            if (face.pips == 2) return 8;
            if (face.pips == 3) return 9;
            if (face.pips == 4) return 10;
        }
        if (face.effectID == 13) return 11;

        return 0; // Default None/Custom fallback
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

        string poolBtnText = _currentPoolIndex == 0 ? "Mod Pool: New Ability" : $"Mod Pool: {CurrentAbility.entityName}";
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnOpenPool", poolBtnText, 0.70f, OpenModPoolModal),
            GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Ability Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentAbility.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Image:", 0.30f),
            GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, OpenImageOverrideModal),
            GridCellSpec.CreateInput("OverrideName", "None", 0.35f, (val) => { CurrentAbility.imageOverride = val; NotifyStateChanged(); }),
            GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.20f, () => { showCustomImagePanel = !showCustomImagePanel; RebuildStatsUI(); })
        ));

        if (showCustomImagePanel)
            layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hue:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliH", -99, 99, true, 0.50f, (val) => UpdateAbilityHsvData(0, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacH", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateAbilityHsvData(0, h); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Saturation:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliS", -99, 99, true, 0.50f, (val) => UpdateAbilityHsvData(1, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacS", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateAbilityHsvData(1, s); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Value:", 0.30f),
            GridCellSpec.CreateSlider("HeroSliV", -99, 99, true, 0.50f, (val) => UpdateAbilityHsvData(2, Mathf.RoundToInt(val))),
            GridCellSpec.CreateInput("HeroFacV", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateAbilityHsvData(2, v); })
        ));

        // Labeled "Ability-Only Keywords" mapping UI names to raw string constant values
        var abilityOnlyKeywords = new Dictionary<string, string> {
            { "Channel", SpecialAbilityKeywords.Channel },
            { "Cooldown", SpecialAbilityKeywords.Cooldown },
            { "Deplete", SpecialAbilityKeywords.Deplete },
            { "Future", SpecialAbilityKeywords.Future },
            { "Spell Rescue", SpecialAbilityKeywords.SpellRescue },
            { "Single Cast", SpecialAbilityKeywords.SingleCast }
        };

        // Map the raw Ritemx IDs stored in the data back to their clean UI Keys for the selector
        var activeKeywordKeys = CurrentAbility.items
            ?.Where(i => abilityOnlyKeywords.Values.Any(v => string.Equals(v, i, StringComparison.OrdinalIgnoreCase)))
            .Select(i => abilityOnlyKeywords.FirstOrDefault(x => string.Equals(x.Value, i, StringComparison.OrdinalIgnoreCase)).Key)
            .ToList() ?? new List<string>();

        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Ability-Only Keyword:",
            uniqueKey: "SpellKeywords",
            availableChoices: abilityOnlyKeywords.Keys.ToList(),
            currentActiveItems: activeKeywordKeys,
            getKey: (niceName) => niceName,
            getDisplay: (niceName) => niceName,
            onAdd: (niceName) =>
            {
                if (CurrentAbility.items == null) CurrentAbility.items = new List<string>();
                string internalId = abilityOnlyKeywords[niceName];
                if (!CurrentAbility.items.Contains(internalId))
                {
                    CurrentAbility.items.Add(internalId);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (niceName) =>
            {
                string internalId = abilityOnlyKeywords[niceName];
                if (CurrentAbility.items != null && CurrentAbility.items.Remove(internalId))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        return layout;
    }

    private List<GridRowSpec> GenerateAbilityLayout()
    {
        var layout = new List<GridRowSpec>();
        bool isSpell = CurrentAbility is SpellData;
        var validKeywords = GetFilteredFaceKeywords();
        //validKeywords.Insert(0, ""); // empty for dropdown

        // 1. TACTIC OR SPELL TOGGLE
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnToggleMode", isSpell ? "MODE: SPELL" : "MODE: TACTIC", 1.0f, ToggleAbilityMode)
        ));
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer", "", 1.0f)));

        // 2. PRIMARY EFFECT (Side 0)
        var pFace = CurrentAbility.diceSides[0];
        //string targetHint = pFace.effectID == 56 || pFace.effectID == 103 ? "(Targets: ALLIES)" : "(Targets: ENEMIES)"; // Simplistic targeting hint
        string targetHint = "Target ally or enemy.";

        var primBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgPrim", 1.0f)) { isBackground = true, rowSpan = 3 + pFace.keywords.Count };
        layout.Add(primBg);

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblPrim", $"PRIMARY EFFECT {targetHint}", 1.0f)));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateDiceButton($"BaseBtn_0", "B", 0.10f, () => OpenEffectBaseModal(0, true)),
            GridCellSpec.CreateInput($"ID_0", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { pFace.effectID = id; NotifyStateChanged(); } }),
            GridCellSpec.CreateLabel("Pips:", 0.20f),
            GridCellSpec.CreateInput($"Pips_0", "Pips", 0.20f, (val) => { if (int.TryParse(val, out int p)) { pFace.pips = p; NotifyStateChanged(); } }),
            GridCellSpec.CreateButton($"BtnPUp_0", "^", 0.15f, () => { pFace.pips++; NotifyStateChanged(); }),
            GridCellSpec.CreateButton($"BtnPDn_0", "v", 0.15f, () => { pFace.pips = Mathf.Max(0, pFace.pips - 1); NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Keyword:", 0.35f),
            GridCellSpec.CreateFilteredDropdown($"KwDrop_0", "", 0.65f, validKeywords.ToArray(), (val) => AddKeywordToFace(0, val))
        ));

        foreach (var kw in pFace.keywords)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"KwTag_0_{kw}", EntityUIHelpers.GetColoredKeywordLabel(kw), 0.80f),
                GridCellSpec.CreateButton($"KwDel_0_{kw}", "[X]", 0.20f, () => RemoveKeywordFromFace(0, kw))
            ));
        }
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer2", "", 1.0f)));

        // 3. SECONDARY EFFECT (Side 1)
        var sFace = CurrentAbility.diceSides[1];
        var secBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgSec", 1.0f)) { isBackground = true, rowSpan = 3 + sFace.keywords.Count };
        layout.Add(secBg);

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblSec", "SECONDARY EFFECT (Untargeted)", 1.0f)));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateDiceButton($"BaseBtn_1", "B", 0.10f, () => OpenEffectBaseModal(1, false)),
            GridCellSpec.CreateInput($"ID_1", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { sFace.effectID = id; NotifyStateChanged(); } }),
            GridCellSpec.CreateLabel("Pips:", 0.20f),
            GridCellSpec.CreateInput($"Pips_1", "Pips", 0.20f, (val) => { if (int.TryParse(val, out int p)) { sFace.pips = p; NotifyStateChanged(); } }),
            GridCellSpec.CreateButton($"BtnPUp_1", "^", 0.15f, () => { sFace.pips++; NotifyStateChanged(); }),
            GridCellSpec.CreateButton($"BtnPDn_1", "v", 0.15f, () => { sFace.pips = Mathf.Max(0, sFace.pips - 1); NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Keyword:", 0.35f),
            GridCellSpec.CreateFilteredDropdown($"KwDrop_1", "", 0.65f, validKeywords.ToArray(), (val) => AddKeywordToFace(1, val))
        ));

        foreach (var kw in sFace.keywords)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"KwTag_1_{kw}", EntityUIHelpers.GetColoredKeywordLabel(kw), 0.80f),
                GridCellSpec.CreateButton($"KwDel_1_{kw}", "[X]", 0.20f, () => RemoveKeywordFromFace(1, kw))
            ));
        }
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer3", "", 1.0f)));

        // 4. SPELL MANA OR TACTIC COSTS
        var costBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgCost", 1.0f)) { isBackground = true, rowSpan = isSpell ? 2 : 4 };
        layout.Add(costBg);

        if (isSpell)
        {
            var spell = CurrentAbility as SpellData;
            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "SPELL MANA COST", 1.0f)));
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Cost:", 0.30f),
                GridCellSpec.CreateInput("ManaPips", spell.manaCost.ToString(), 0.30f, (val) => { if (int.TryParse(val, out int p)) { spell.manaCost = p; NotifyStateChanged(); } }),
                GridCellSpec.CreateButton($"BtnCostUp", "^", 0.20f, () => { spell.manaCost++; NotifyStateChanged(); }),
                GridCellSpec.CreateButton($"BtnCostDn", "v", 0.20f, () => { spell.manaCost = Mathf.Max(0, spell.manaCost - 1); NotifyStateChanged(); })
            ));
        }
        else
        {
            var tactic = CurrentAbility as TacticData;
            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "TACTIC COSTS (Up to 3)", 1.0f)));

            // Tactic Cost 1 (Top Side / Index 2)
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateDropdown("TacDrop_2", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(2, val, tactic.TacticCostTop.pips)),
                GridCellSpec.CreateLabel("Pips:", 0.15f),
                GridCellSpec.CreateInput("TacPip_2", tactic.TacticCostTop.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(2, GetTacticCostDropdownIndex(tactic.TacticCostTop), p); } })
            ));

            // Tactic Cost 2 (Bottom Side / Index 3)
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateDropdown("TacDrop_3", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(3, val, tactic.TacticCostBottom.pips)),
                GridCellSpec.CreateLabel("Pips:", 0.15f),
                GridCellSpec.CreateInput("TacPip_3", tactic.TacticCostBottom.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(3, GetTacticCostDropdownIndex(tactic.TacticCostBottom), p); } })
            ));

            // Tactic Cost 3 (Rightmost Side / Index 5)
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateDropdown("TacDrop_5", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(5, val, tactic.TacticCostRightmost.pips)),
                GridCellSpec.CreateLabel("Pips:", 0.15f),
                GridCellSpec.CreateInput("TacPip_5", tactic.TacticCostRightmost.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(5, GetTacticCostDropdownIndex(tactic.TacticCostRightmost), p); } })
            ));
        }

        return layout;
    }

    private void RebuildStatsUI()
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

        if (showCustomImagePanel && statsUI.CustomImgImporter.TryGetValue("CustomImgPanel", out ImageReceiver dummyReceiver))
        {
            if (_persistentCustomImageReceiver == null)
            {
                _persistentCustomImageReceiver = dummyReceiver;
                _persistentCustomImageReceiver.OnImageGenerated = (encodedStr, tex) =>
                {
                    CurrentAbility.imageOverride = encodedStr;
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
        else if (_persistentCustomImageReceiver != null)
        {
            _persistentCustomImageReceiver.gameObject.SetActive(false);
        }

        isDrawingUI = wasDrawing;
        UpdateUIFromData();
    }

    private void RebuildAbilityScrollView()
    {
        if (abilityScrollRect == null) return;
        abilityDataUI = uiGenerator.RebuildGrid(abilityScrollRect.content, GenerateAbilityLayout());

        float extraHeight = 0f;
        var layoutGroup = abilityScrollRect.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (layoutGroup != null)
        {
            int childCount = abilityScrollRect.content.childCount;
            if (childCount > 1) extraHeight += layoutGroup.spacing * (childCount - 1);
            extraHeight += layoutGroup.padding.top + layoutGroup.padding.bottom;
        }

        abilityScrollRect.content.sizeDelta = new Vector2(0, abilityDataUI.TotalHeight + extraHeight);
        Canvas.ForceUpdateCanvases();
        UpdateUIFromData();
    }

    protected override void BuildUIAndBind()
    {
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftStats", 0.01f, 0.35f, new List<GridRowSpec>
            {
                new GridRowSpec(900f, GridCellSpec.CreateScrollView("StatsScrollView", 1.0f))
            }),
            new ColumnSpec("MiddleAbility", 0.365f, 0.685f, new List<GridRowSpec>
            {
                // Notice there are no navigation tabs here anymore
                new GridRowSpec(900f, GridCellSpec.CreateScrollView("AbilityScrollView", 1.0f))
            }),
            new ColumnSpec("RightOutput", 0.70f, 0.99f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        statsScrollRect = generatedScreen.ColumnRefs["LeftStats"].ScrollViews["StatsScrollView"];
        abilityScrollRect = generatedScreen.ColumnRefs["MiddleAbility"].ScrollViews["AbilityScrollView"];

        ApplyDynamicLayoutConstraints();

        if (generatedScreen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
            BuildRightPanelContent(rightPanel);

        RebuildStatsUI();
        RebuildAbilityScrollView();
    }

    private void BuildRightPanelContent(RectTransform parent)
    {
        // Preview Header
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
        previewContainer.transform.SetParent(parent, false);
        FullScreenUIGenerator.SetAnchors(previewContainer.GetComponent<RectTransform>(), 0.1f, 0.65f, 0.9f, 0.95f);

        // Simple Ability Icon Preview
        GameObject imgObj = new GameObject("PreviewIcon", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(previewContainer.transform, false);
        FullScreenUIGenerator.SetAnchors(imgObj.GetComponent<RectTransform>(), 0.25f, 0.3f, 0.75f, 0.9f);
        previewIcon = imgObj.GetComponent<Image>();
        previewIcon.preserveAspect = true;
        Material hsvMat = Resources.Load<Material>("UI_Custom_HSV_Adjustment");
        if (hsvMat != null)
        {
            previewIcon.material = Instantiate(hsvMat);
        }

        // Simple Ability Name Preview
        GameObject nameObj = Instantiate(uiGenerator.labelPrefab, previewContainer.transform);
        FullScreenUIGenerator.SetAnchors(nameObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 0.25f);
        previewName = nameObj.GetComponentInChildren<TextMeshProUGUI>();
        previewName.alignment = TextAlignmentOptions.Center;
        previewName.fontSize = 24;
        previewName.fontStyle = FontStyles.Bold;

        // Raw Text Readout
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
        syntaxHighlighterText = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();

        var canvasGroup = highlighterObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        foreach (var script in highlighterObj.GetComponents<MonoBehaviour>())
            if (script != null && !(script is TextMeshProUGUI)) DestroyImmediate(script);

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

        rawTextOutput.onValueChanged.AddListener((val) => { if (syntaxHighlighterText != null) syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(val); });

        rawTextOutput.onEndEdit.AddListener((val) =>
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            try
            {
                AbilityData imported = AbilityData.Parse(val);
                if (imported != null)
                {
                    ModPackage.Instance.UpdateActiveEntityClone<AbilityData>(imported);
                    ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
                    UpdateUIFromData();
                    RebuildAbilityScrollView();
                }
            }
            catch (Exception ex) { Debug.LogWarning($"Could not parse pasted ability string: {ex.Message}"); }
        });

        FullScreenUIGenerator.SetAnchors(inputObj.GetComponent<RectTransform>(), 0.0f, 0.08f, 1.0f, 0.58f);

        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Copy Ability String";
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => GUIUtility.systemCopyBuffer = GUIUtility.systemCopyBuffer = $"abilitydata.{CurrentAbility.ExportWrapped()}");
        FullScreenUIGenerator.SetAnchors(copyBtnObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Paste Ability String";
        pasteBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() =>
        {
            string cb = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(cb)) return;
            try
            {
                AbilityData imported = AbilityData.Parse(cb);
                if (imported != null)
                {
                    ModPackage.Instance.UpdateActiveEntityClone<AbilityData>(imported);
                    ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
                    UpdateUIFromData();
                    RebuildAbilityScrollView();
                }
            }
            catch (Exception ex) { Debug.LogWarning($"Could not paste: {ex.Message}"); }
        });
        FullScreenUIGenerator.SetAnchors(pasteBtnObj.GetComponent<RectTransform>(), 0.52f, 0.0f, 1.0f, 0.06f);
    }

    private void ApplyDynamicLayoutConstraints()
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

        if (abilityScrollRect != null)
        {
            RectTransform scrollRt = abilityScrollRect.GetComponent<RectTransform>();
            RectTransform rowRt = scrollRt.parent as RectTransform;
            ConfigureFlexibleLayout(rowRt);
            ConfigureFlexibleLayout(scrollRt);
            StretchToParent(rowRt, 10f, 10f); // Top offset removed since nav tabs are gone
            StretchToParent(scrollRt, 0f, 0f);
        }
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

    private void OpenModPoolModal()
    {
        if (iconPicker == null) return;

        var abilities = ModPackage.Instance.CustomAbilities;
        Sprite[] abilitySprites = new Sprite[abilities.Count + 1];

        abilitySprites[0] = EntityUIHelpers.GetFacadeSprite("SpellPlaceholder");

        for (int i = 0; i < abilities.Count; i++)
        {
            var a = abilities[i];
            string imgStr = string.IsNullOrEmpty(a.imageOverride) || a.imageOverride == "None" ? a.baseReplica : a.imageOverride;
            abilitySprites[i + 1] = EntityUIHelpers.GetFacadeSprite(imgStr);
        }

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = abilitySprites,
            DisableDeduplication = true,
            AllowNullSprites = true,
            IsValid = (index, sprite) => true,
            CellSize = new Vector2(80, 80),

            GetSearchName = (index, sprite) => index == 0 ? "New Ability" : abilities[index - 1].entityName,
            GetTooltip = (index, sprite) => index == 0 ? "Create a new custom Spell/Tactic" : abilities[index - 1].entityName,

            GetNameText = (index, sprite) => index == 0 ? "New Ability" : abilities[index - 1].entityName,
            GetTierText = (index, sprite) => "",
            GetHPText = (index, sprite) => "",
            GetColor = (index, sprite) => index == 0 ? Color.white : SDColors.GetColor(EntityUIHelpers.ReverseLookupColor(abilities[index - 1].colorClass)),
            OnSelectionMade = (index, sprite) =>
            {
                if (isDrawingUI) return;
                _currentPoolIndex = index;
                if (index > 0 && (index - 1) < abilities.Count) ModPackage.Instance.LoadEntityForEditing(abilities[index - 1]);
                else ModPackage.Instance.LoadEntityForEditing(CreateNewSpell());

                ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
                RebuildStatsUI();
                RebuildAbilityScrollView();
            }
        };

        iconPicker.OpenModal(config);
    }

    private void SaveToModPool()
    {
        ModPackage.Instance.SaveActiveEntity<AbilityData>();
        AbilityData savedAbility = ModPackage.Instance.GetActiveEntity<AbilityData>();

        var abilitiesList = ModPackage.Instance.CustomAbilities as List<AbilityData>;
        int newIndex = abilitiesList?.IndexOf(savedAbility) ?? -1;
        if (newIndex >= 0) _currentPoolIndex = newIndex + 1;

        ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
        RebuildStatsUI();
    }

    private List<string> GetFilteredFaceKeywords()
    {
        var allKws = Enum.GetNames(typeof(SpecialAbilityKeywords.AbilityEffectKeyword)).ToList();

        // Exclude the 6 specialized keywords that apply at the entity/item level
        var specialKws = new HashSet<string> { "Future" };
        var filtered = allKws.Where(k => !specialKws.Contains(k)).ToList();

        filtered.Insert(0, ""); // Add empty option at index 0
        return filtered;
    }
}