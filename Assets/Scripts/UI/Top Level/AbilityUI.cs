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

    private Sprite _customImageCachedSprite;
    private bool _needsRebuild = false;

    // Dice Face Builder Widgets for Primary and Secondary faces
    private DiceFaceBuilderWidget _primaryFaceBuilder;
    private DiceFaceBuilderWidget _secondaryFaceBuilder;

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
                ModPackage.Instance.LoadEntityForEditing<AbilityData>(CreateNewSpell());
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
        InitializeDiceWidgets();
        BuildUIAndBind();

        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged += OnStateChanged;
            OnStateChanged(null);
        }
    }

    private void InitializeDiceWidgets()
    {
        // Initialize widget delegates for Primary Face (Index 0)
        _primaryFaceBuilder = new DiceFaceBuilderWidget(
            getDiceSides: () => CurrentAbility?.diceSides,
            allowFacades: () => false, // Abilities do not use standard facade ID overrides/HSV panels on their faces
            openBaseModal: (idx) => OpenEffectBaseModal(idx, true),
            openFacadeModal: (idx) => { },
            getBaseSprite: (id) => EntityUIHelpers.GetBaseSprite(id),
            getFacadeSprite: (id) => null,
            onStateChanged: () => NotifyStateChanged(),
            onRebuildRequested: () => RebuildAbilityScrollView()
        );

        // Initialize widget delegates for Secondary Face (Index 1)
        _secondaryFaceBuilder = new DiceFaceBuilderWidget(
            getDiceSides: () => CurrentAbility?.diceSides,
            allowFacades: () => false,
            openBaseModal: (idx) => OpenEffectBaseModal(idx, false),
            openFacadeModal: (idx) => { },
            getBaseSprite: (id) => EntityUIHelpers.GetBaseSprite(id),
            getFacadeSprite: (id) => null,
            onStateChanged: () => NotifyStateChanged(),
            onRebuildRequested: () => RebuildAbilityScrollView()
        );
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
        newSpell.imageOverride = "None";
        newSpell.baseReplica = "Fey";

        // Default Primary Effect
        newSpell.diceSides[0].effectID = 15; // Damage
        newSpell.diceSides[0].pips = 1;

        return newSpell;
    }

    // =====================================================================
    // ICON MODAL SELECTION
    // =====================================================================

    private void OpenImageOverrideModal()
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.CommunitySprites,
            IsValid = (index, sprite) => sprite != null,
            GetSearchName = (index, sprite) => IconPickerModal.GetCleanLeafName(sprite.name),
            GetTooltip = (index, sprite) => IconPickerModal.GetCleanLeafName(sprite.name),
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');

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

    private bool IsPrimaryEffectValid(int baseId) => true;
    private bool IsSecondaryEffectValid(int baseId) => true;

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

        if (sender != null) return;

        if (!gameObject.activeInHierarchy)
        {
            _needsRebuild = true;
            return;
        }

        RebuildStatsUI();
    }

    private void OnEnable()
    {
        if (_needsRebuild)
        {
            _needsRebuild = false;
            RebuildStatsUI();
        }
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

        // Update Dice Face Widgets UI state
        _primaryFaceBuilder?.SetGridReferences(abilityDataUI);
        _primaryFaceBuilder?.UpdateUIFromData(0);

        if (CurrentAbility is SpellData or TacticData)
        {
            _secondaryFaceBuilder?.SetGridReferences(abilityDataUI);
            _secondaryFaceBuilder?.UpdateUIFromData(1);
        }

        if (abilityDataUI.Dropdowns.TryGetValue("ModeDrop", out var modeDrop))
        {
            int modeIdx = 0;
            if (CurrentAbility is TacticData) modeIdx = 1;
            else if (CurrentAbility is OnHitData) modeIdx = 2;
            else if (CurrentAbility is TriggerHPData) modeIdx = 3;
            else if (CurrentAbility is OrbData) modeIdx = 4;
            modeDrop.SetValueWithoutNotify(modeIdx);
        }

        if (CurrentAbility is OrbData orb)
        {
            if (abilityDataUI.Dropdowns.TryGetValue("OrbTypeDrop", out var orbTypeDrop))
                orbTypeDrop.SetValueWithoutNotify(orb.isHardcoded ? 1 : 0);

            if (abilityDataUI.Dropdowns.TryGetValue("BaseOrbDrop", out var baseOrbDrop))
            {
                var baseOrbsList = OrbData.ValidBaseOrbs.ToList();
                int idx = baseOrbsList.FindIndex(o => string.Equals(o, orb.hardcodedAbilityName, StringComparison.OrdinalIgnoreCase));
                baseOrbDrop.SetValueWithoutNotify(idx >= 0 ? idx : 0);
            }

            if (abilityDataUI.Inputs.TryGetValue("CarrierPrefixInput", out var carrierIn))
                carrierIn.SetTextWithoutNotify(string.IsNullOrEmpty(orb.carrierPrefix) ? "sthief.abilitydata" : orb.carrierPrefix);
        }

        if (CurrentAbility is SpellData spell)
        {
            if (abilityDataUI.Inputs.TryGetValue("ManaPips", out var manaPipIn)) manaPipIn.SetTextWithoutNotify(spell.manaCost.ToString());
        }
        else if (CurrentAbility is TacticData tactic)
        {
            if (abilityDataUI.Inputs.TryGetValue("TacPip_2", out var tPip2)) tPip2.SetTextWithoutNotify(tactic.TacticCostTop.pips.ToString());
            if (abilityDataUI.Inputs.TryGetValue("TacPip_3", out var tPip3)) tPip3.SetTextWithoutNotify(tactic.TacticCostBottom.pips.ToString());
            if (abilityDataUI.Inputs.TryGetValue("TacPip_5", out var tPip5)) tPip5.SetTextWithoutNotify(tactic.TacticCostRightmost.pips.ToString());

            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_2", out var tDrop2)) tDrop2.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostTop));
            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_3", out var tDrop3)) tDrop3.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostBottom));
            if (abilityDataUI.Dropdowns.TryGetValue("TacDrop_5", out var tDrop5)) tDrop5.SetValueWithoutNotify(GetTacticCostDropdownIndex(tactic.TacticCostRightmost));
        }
        else if (CurrentAbility is TriggerHPData triggerHP)
        {
            if (abilityDataUI.Inputs.TryGetValue("TriggerHPInput", out var hpIn))
                hpIn.SetTextWithoutNotify(triggerHP.hp.ToString());

            if (abilityDataUI.Inputs.TryGetValue("TriggerHPDesc", out var hpDesc))
            {
                hpDesc.interactable = false;
                hpDesc.SetTextWithoutNotify(AbilityData.GetPipsAffectedDescription(triggerHP.hp));
            }

            if (abilityDataUI.Dropdowns.TryGetValue("ColorDrop", out var colorDrop))
            {
                HeroColorOption currentOption = SDColors.GetOptionFromColorCode(triggerHP.colorClass ?? "Grey");
                colorDrop.SetValueWithoutNotify((int)currentOption);
            }

            if (abilityDataUI.Dropdowns.TryGetValue("TriggerHPDrop", out var hpDrop))
            {
                int dpIdx = (triggerHP.hp >= 1 && triggerHP.hp <= 21) ? triggerHP.hp : 0;
                hpDrop.SetValueWithoutNotify(dpIdx);
            }
        }

        isDrawingUI = false;
        UpdateVisualsOnly();
    }

    private void UpdateVisualsOnly()
    {
        if (previewName != null) previewName.text = CurrentAbility.entityName;

        if (previewIcon != null)
        {
            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentAbility.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageCachedSprite != null)
            {
                previewIcon.sprite = _customImageCachedSprite;
            }
            else
            {
                Sprite targetSprite = EntityUIHelpers.GetFacadeSprite(CurrentAbility.imageOverride);
                previewIcon.sprite = targetSprite != null ? targetSprite : EntityUIHelpers.GetFacadeSprite("SpellPlaceholder");
            }

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
                StaticUI.SetButtonIcon(overrideBtn, EntityUIHelpers.GetFacadeSprite(CurrentAbility.imageOverride));
        }

        // Update Face Builder Icon Visuals
        _primaryFaceBuilder?.SetGridReferences(abilityDataUI);
        _primaryFaceBuilder?.UpdateVisuals(0);

        if (CurrentAbility is SpellData or TacticData)
        {
            _secondaryFaceBuilder?.SetGridReferences(abilityDataUI);
            _secondaryFaceBuilder?.UpdateVisuals(1);
        }

        if (rawTextOutput != null)
        {
            string exportedString = CurrentAbility is OrbData orb
                ? orb.ExportAsTrait(useITPrefix: true)
                : $"abilitydata.{CurrentAbility.ExportWrapped()}";

            rawTextOutput.SetTextWithoutNotify(exportedString);

            if (syntaxHighlighterText != null)
                syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(exportedString);
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
            case 0: face.effectID = 0; face.pips = 0; break;
            case 1: face.effectID = 15; face.pips = pips; break;
            case 2: face.effectID = 56; face.pips = pips; break;
            case 3: face.effectID = 103; face.pips = pips; break;
            case 4: face.effectID = 76; face.pips = pips; break;
            case 5: face.effectID = 136; face.pips = pips; break;
            case 6: face.effectID = 8; face.pips = 0; break;
            case 7: face.effectID = 177; face.pips = 1; break;
            case 8: face.effectID = 177; face.pips = 2; break;
            case 9: face.effectID = 177; face.pips = 3; break;
            case 10: face.effectID = 177; face.pips = 4; break;
            case 11: face.effectID = 13; face.pips = pips; break;
            case 12: face.effectID = 0; face.pips = pips; break;
            case 13: face.effectID = 0; face.pips = pips; break;
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

        return 0;
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

        var abilityOnlyKeywords = new Dictionary<string, string> {
            { "Channel", SpecialAbilityKeywords.Channel },
            { "Cooldown", SpecialAbilityKeywords.Cooldown },
            { "Deplete", SpecialAbilityKeywords.Deplete },
            { "Future", SpecialAbilityKeywords.Future },
            { "Spell Rescue", SpecialAbilityKeywords.SpellRescue },
            { "Single Cast", SpecialAbilityKeywords.SingleCast }
        };

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

        int currentModeIndex = 0;
        if (CurrentAbility is TacticData) currentModeIndex = 1;
        else if (CurrentAbility is OnHitData) currentModeIndex = 2;
        else if (CurrentAbility is TriggerHPData) currentModeIndex = 3;
        else if (CurrentAbility is OrbData) currentModeIndex = 4;

        string[] modeOptions = new string[] { "Spell", "Tactic", "On Hit", "Trigger HP", "Orb" };

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Ability Type:", 0.35f),
            GridCellSpec.CreateDropdown("ModeDrop", modeOptions[currentModeIndex], 0.65f, modeOptions, (val) => {
                if (val != currentModeIndex) ChangeAbilityMode(val);
            })
        ));
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer", "", 1.0f)));

        // 2. Build Primary Face using DiceFaceBuilderWidget Layout Extension
        BuildPrimaryFaceLayout(layout, currentModeIndex);

        // 3. Delegate to specialized layout builder
        switch (currentModeIndex)
        {
            case 0: BuildSpellLayout(layout); break;
            case 1: BuildTacticLayout(layout); break;
            case 2: BuildOnHitLayout(layout); break;
            case 3: BuildTriggerHPLayout(layout); break;
            case 4: BuildOrbLayout(layout); break;
        }

        return layout;
    }

    private void BuildOrbLayout(List<GridRowSpec> layout)
    {
        var orbData = CurrentAbility as OrbData;

        var orbBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgCost", 1.0f)) { isBackground = true, rowSpan = 3 };
        layout.Add(orbBg);

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "ORB TRIGGER SETTINGS (ON DEATH)", 1.0f)));

        string[] orbTypeOptions = new string[] { "Custom Ability Payload", "Hardcoded Base Game Ability" };
        int selectedTypeIdx = orbData.isHardcoded ? 1 : 0;

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Orb Type:", 0.35f),
            GridCellSpec.CreateDropdown("OrbTypeDrop", orbTypeOptions[selectedTypeIdx], 0.65f, orbTypeOptions, (val) => {
                orbData.isHardcoded = (val == 1);
                NotifyStateChanged();
                RebuildAbilityScrollView();
            })
        ));

        if (orbData.isHardcoded)
        {
            var baseOrbsList = OrbData.ValidBaseOrbs.ToList();
            int currentBaseIdx = baseOrbsList.FindIndex(o => string.Equals(o, orbData.hardcodedAbilityName, StringComparison.OrdinalIgnoreCase));
            if (currentBaseIdx < 0) currentBaseIdx = 0;

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Base Ability:", 0.35f),
                GridCellSpec.CreateDropdown("BaseOrbDrop", baseOrbsList[currentBaseIdx], 0.65f, baseOrbsList.ToArray(), (val) => {
                    if (val >= 0 && val < baseOrbsList.Count)
                    {
                        orbData.hardcodedAbilityName = baseOrbsList[val];
                        orbData.entityName = baseOrbsList[val];
                        NotifyStateChanged();
                        UpdateUIFromData();
                    }
                })
            ));
        }
        else
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Carrier Prefix:", 0.35f),
                GridCellSpec.CreateInput("CarrierPrefixInput", string.IsNullOrEmpty(orbData.carrierPrefix) ? "sthief.abilitydata" : orbData.carrierPrefix, 0.65f, (val) => {
                    orbData.carrierPrefix = string.IsNullOrWhiteSpace(val) ? "sthief.abilitydata" : val;
                    NotifyStateChanged();
                })
            ));
        }
    }

    private void BuildPrimaryFaceLayout(List<GridRowSpec> layout, int modeIndex)
    {
        string targetHint = "Target ally or enemy.";
        if (modeIndex == 2) targetHint = "(Can be targeted)";
        else if (modeIndex == 3) targetHint = "(MUST be untargeted)";

        // Generate Primary Face Layout using DiceFaceBuilderWidget
        var primaryLayout = _primaryFaceBuilder.GenerateLayout(0);

        // Wrap/Inject Header hint override if needed or append layout
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblPrim", $"PRIMARY EFFECT {targetHint}", 1.0f)));
        layout.AddRange(primaryLayout);
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer2", "", 1.0f)));
    }

    private void BuildSpellLayout(List<GridRowSpec> layout)
    {
        // Secondary Face Layout via DiceFaceBuilderWidget (Index 1)
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblSec", "SECONDARY EFFECT (Untargeted)", 1.0f)));
        layout.AddRange(_secondaryFaceBuilder.GenerateLayout(1));
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer3", "", 1.0f)));

        // Mana Cost Layout
        var costBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgCost", 1.0f)) { isBackground = true, rowSpan = 2 };
        layout.Add(costBg);

        var spell = CurrentAbility as SpellData;
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "SPELL MANA COST", 1.0f)));
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Cost:", 0.30f),
            GridCellSpec.CreateInput("ManaPips", spell.manaCost.ToString(), 0.30f, (val) => { if (int.TryParse(val, out int p)) { spell.manaCost = p; NotifyStateChanged(); } }),
            GridCellSpec.CreateButton($"BtnCostUp", "▲", 0.20f, () => { spell.manaCost++; NotifyStateChanged(); UpdateUIFromData(); }),
            GridCellSpec.CreateButton($"BtnCostDn", "▼", 0.20f, () => { spell.manaCost = Mathf.Max(0, spell.manaCost - 1); NotifyStateChanged(); UpdateUIFromData(); })
        ));
    }

    private void BuildTacticLayout(List<GridRowSpec> layout)
    {
        // Secondary Face Layout via DiceFaceBuilderWidget (Index 1)
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblSec", "SECONDARY EFFECT (Untargeted)", 1.0f)));
        layout.AddRange(_secondaryFaceBuilder.GenerateLayout(1));
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer3", "", 1.0f)));

        // Tactic Cost Layout
        var costBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgCost", 1.0f)) { isBackground = true, rowSpan = 4 };
        layout.Add(costBg);

        var tactic = CurrentAbility as TacticData;
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "TACTIC COSTS (Up to 3)", 1.0f)));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateDropdown("TacDrop_2", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(2, val, tactic.TacticCostTop.pips)),
            GridCellSpec.CreateLabel("Pips:", 0.15f),
            GridCellSpec.CreateInput("TacPip_2", tactic.TacticCostTop.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(2, GetTacticCostDropdownIndex(tactic.TacticCostTop), p); } })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateDropdown("TacDrop_3", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(3, val, tactic.TacticCostBottom.pips)),
            GridCellSpec.CreateLabel("Pips:", 0.15f),
            GridCellSpec.CreateInput("TacPip_3", tactic.TacticCostBottom.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(3, GetTacticCostDropdownIndex(tactic.TacticCostBottom), p); } })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateDropdown("TacDrop_5", "", 0.45f, TacticCostOptions, (val) => ApplyTacticCost(5, val, tactic.TacticCostRightmost.pips)),
            GridCellSpec.CreateLabel("Pips:", 0.15f),
            GridCellSpec.CreateInput("TacPip_5", tactic.TacticCostRightmost.pips.ToString(), 0.20f, (val) => { if (int.TryParse(val, out int p)) { ApplyTacticCost(5, GetTacticCostDropdownIndex(tactic.TacticCostRightmost), p); } })
        ));
    }

    private void BuildOnHitLayout(List<GridRowSpec> layout)
    {
        // On Hit uses only the Primary Face, handled dynamically.
    }

    private void BuildTriggerHPLayout(List<GridRowSpec> layout)
    {
        var triggerData = CurrentAbility as TriggerHPData;

        var thpBg = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgCost", 1.0f)) { isBackground = true, rowSpan = 5 };
        layout.Add(thpBg);

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblCost", "TRIGGER HP SETTINGS", 1.0f)));

        HeroColorOption currentOption = SDColors.GetOptionFromColorCode(triggerData.colorClass ?? "Grey");
        string currentFormattedName = SDColors.GetFormattedColorName(currentOption);

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP Pip Color:", 0.35f),
            GridCellSpec.CreateFilteredDropdown("ColorDrop", currentFormattedName, 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                HeroColorOption selectedColor = (HeroColorOption)val;
                triggerData.colorClass = SDColors.GetColorCode(selectedColor);
                NotifyStateChanged();
            })
        ));

        int currentDropdownIndex = (triggerData.hp >= 1 && triggerData.hp <= 21) ? triggerData.hp : 0;
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP Preset:", 0.35f),
            GridCellSpec.CreateDropdown("TriggerHPDrop", HpHelper.HpDropdownOptions[currentDropdownIndex], 0.65f, HpHelper.HpDropdownOptions, (val) => {
                if (val > 0)
                {
                    triggerData.hp = val;
                    NotifyStateChanged();
                    UpdateUIFromData();
                }
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HPModeLabel", "HP Mode:", 0.35f),
            GridCellSpec.CreateInput("TriggerHPDesc", AbilityData.GetPipsAffectedDescription(triggerData.hp), 0.65f, (val) => { })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP Value (Raw):", 0.35f),
            GridCellSpec.CreateInput("TriggerHPInput", triggerData.hp.ToString(), 0.65f, (val) => {
                if (int.TryParse(val, out int parsed))
                {
                    triggerData.hp = parsed;
                    NotifyStateChanged();
                    UpdateUIFromData();
                }
            })
        ));
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
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
        previewContainer.transform.SetParent(parent, false);
        FullScreenUIGenerator.SetAnchors(previewContainer.GetComponent<RectTransform>(), 0.1f, 0.65f, 0.9f, 0.95f);

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

        GameObject nameObj = Instantiate(uiGenerator.labelPrefab, previewContainer.transform);
        FullScreenUIGenerator.SetAnchors(nameObj.GetComponent<RectTransform>(), 0f, 0f, 1f, 0.25f);
        previewName = nameObj.GetComponentInChildren<TextMeshProUGUI>();
        previewName.alignment = TextAlignmentOptions.Center;
        previewName.fontSize = 24;
        previewName.fontStyle = FontStyles.Bold;

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

            string currentExport = $"abilitydata.{CurrentAbility.ExportWrapped()}";
            if (val == currentExport) return;

            try
            {
                string cleanVal = val.Trim();
                if (cleanVal.StartsWith("abilitydata.", StringComparison.OrdinalIgnoreCase))
                {
                    cleanVal = cleanVal.Substring(12).Trim();
                }

                AbilityData imported = AbilityData.WhatAmI(cleanVal);
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
        copyBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() => GUIUtility.systemCopyBuffer = $"abilitydata.{CurrentAbility.ExportWrapped()}");
        FullScreenUIGenerator.SetAnchors(copyBtnObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "Paste Ability String";
        pasteBtnObj.GetComponentInChildren<Button>().onClick.AddListener(() =>
        {
            string cb = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(cb)) return;

            string cleanVal = cb.Trim();
            if (cleanVal.StartsWith("abilitydata.", StringComparison.OrdinalIgnoreCase))
            {
                cleanVal = cleanVal.Substring(12).Trim();
            }

            try
            {
                AbilityData imported = AbilityData.WhatAmI(cleanVal);
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
            StretchToParent(rowRt, 10f, 10f);
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

                if (index > 0 && (index - 1) < abilities.Count)
                    ModPackage.Instance.LoadEntityForEditing<AbilityData>(abilities[index - 1]);
                else
                    ModPackage.Instance.LoadEntityForEditing<AbilityData>(CreateNewSpell());

                ModPackage.Instance.NotifyActiveEntityChanged<AbilityData>(this);
                RebuildStatsUI();
                RebuildAbilityScrollView();
            }
        };

        iconPicker.OpenModal(config);
    }

    private void ChangeAbilityMode(int newModeIndex)
    {
        AbilityData newAbility;

        switch (newModeIndex)
        {
            case 1: newAbility = new TacticData(); break;
            case 2: newAbility = new OnHitData(); break;
            case 3:
                newAbility = new TriggerHPData();
                ((TriggerHPData)newAbility).hp = 1;
                ((TriggerHPData)newAbility).colorClass = SDColors.GetColorCode(HeroColorOption.Grey);
                break;
            case 4:
                newAbility = new OrbData();
                ((OrbData)newAbility).isHardcoded = false;
                ((OrbData)newAbility).carrierPrefix = "sthief.abilitydata";
                break;
            case 0:
            default: newAbility = new SpellData(); break;
        }

        newAbility.entityName = CurrentAbility.entityName;
        newAbility.imageOverride = CurrentAbility.imageOverride;
        newAbility.baseReplica = CurrentAbility.baseReplica;
        newAbility.items = new List<string>(CurrentAbility.items ?? new List<string>());
        newAbility.h = CurrentAbility.h;
        newAbility.s = CurrentAbility.s;
        newAbility.v = CurrentAbility.v;

        newAbility.diceSides[0] = CurrentAbility.diceSides[0].Clone();
        newAbility.diceSides[1] = CurrentAbility.diceSides[1].Clone();

        ModPackage.Instance.UpdateActiveEntityClone<AbilityData>(newAbility);
        NotifyStateChanged();
        RebuildAbilityScrollView();
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
}