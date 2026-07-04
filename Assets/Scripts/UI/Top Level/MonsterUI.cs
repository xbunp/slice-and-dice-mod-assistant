using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================
// MONSTER SPECIFIC UI
// =====================================================================
public class MonsterUI : EntityUI<MonsterData>
{
    // Helper to get the correct string prefix for a size, matching our AtlasProcessor setup
    private string GetPrefixForSize(MonsterSize size)
    {
        switch (size)
        {
            case MonsterSize.Tiny: return "tin";
            case MonsterSize.Big: return "big";
            case MonsterSize.Huge: return "hug";
            case MonsterSize.HeroSized:
            default: return "bas";
        }
    }

    // Helper to extract the ID from the normalized sprite name (e.g. "big_10_bats_22x22" -> 10)

    // Dynamically fetch the correct sprite based on the monster's size

    protected override Sprite GetFacadeDiceSprite(string facadeID) => EntityUIHelpers.GetFacadeSprite(facadeID);
    protected override bool AllowFacades() => true;
    protected override string ExportEntity(MonsterData entity) => MonsterData.Export(entity);
    protected override MonsterData ParseEntity(string data)
    {
        MonsterData monster = new MonsterData();
        monster.Parse(data);
        return monster;
    }

    private void OpenMonsterPortraitsModal(Action<MonsterType, Sprite> onMonsterSelected)
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && HeroSpriteDatabase.SpriteToMonsterMap.ContainsKey(sprite.name),
            GetSearchName = (index, sprite) => HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster) ? monster.ToString() : sprite.name,
            GetTooltip = (index, sprite) => HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster) ? monster.ToString() : sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                {
                    onMonsterSelected?.Invoke(monster, sprite);
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    ////////////////////////////////////////////
    ///

    private void OpenAllPortraitsModal(Action<bool, object, Sprite> onPortraitSelected)
    {
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => sprite != null && (HeroSpriteDatabase.SpriteToMonsterMap.ContainsKey(sprite.name) || HeroSpriteDatabase.SpriteToHeroMap.ContainsKey(sprite.name)),
            GetSearchName = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero)) return hero.ToString();
                if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster)) return monster.ToString();
                return sprite.name;
            },
            GetTooltip = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero)) return hero.ToString();
                if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster)) return monster.ToString();
                return sprite.name;
            },
            OnSelectionMade = (index, sprite) =>
            {
                if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero))
                {
                    onPortraitSelected?.Invoke(true, hero, sprite);
                }
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                {
                    onPortraitSelected?.Invoke(false, monster, sprite);
                }
            }
        };
        iconPicker.OpenModal(config);
    }
    protected override void UpdateSpecificUIFromData()
    {
        if (statsUI.Inputs.TryGetValue("BaseMonster", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentEntity.baseMonster);
        if (statsUI.Inputs.TryGetValue("Bal", out var balIn)) balIn.SetTextWithoutNotify(CurrentEntity.bal);
        if (statsUI.Inputs.TryGetValue("OverrideName", out var overIn)) overIn.SetTextWithoutNotify(CurrentEntity.imageOverride);
    }
    protected override void UpdateSpecificVisuals()
    {
        if (portraitPreview != null)
        {
            portraitPreview.GetBasePrefixDelegate = () => GetPrefixForSize(CurrentEntity.size);

            portraitPreview.SetTierText(""); // Hide Tier visualization for Monsters
            portraitPreview.SetHeroColor(EffectKeywordColors.Purple); // Default background tinting for monsters

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentEntity.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                portraitPreview.portrait.sprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
                portraitPreview.portrait.enabled = true;
            }
            else
            {
                // Check for both Null/Empty AND "None" before determining if we have an override
                bool hasOverride = !string.IsNullOrEmpty(CurrentEntity.imageOverride) &&
                                   !CurrentEntity.imageOverride.Equals("None", StringComparison.OrdinalIgnoreCase);

                string targetImageName = hasOverride ? CurrentEntity.imageOverride : CurrentEntity.baseMonster;
                Sprite targetSprite = GetPortraitSprite(targetImageName);

                if (targetSprite != null && portraitPreview.portrait != null)
                {
                    portraitPreview.portrait.sprite = targetSprite;
                    portraitPreview.portrait.enabled = true; // Ensure renderer is active
                }
                else if (portraitPreview.portrait != null)
                {
                    portraitPreview.portrait.sprite = null;
                    portraitPreview.portrait.enabled = false; // Disable renderer to prevent Unity's white block fallback
                }
            }
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("MonsterBtn", out var monsterBtn))
            {
                Sprite s = GetPortraitSprite(CurrentEntity.baseMonster);
                SetButtonIcon(monsterBtn, s);
            }

            if (statsUI.Buttons.TryGetValue("OverrideBtn", out var overrideBtn))
            {
                Sprite s = GetPortraitSprite(CurrentEntity.imageOverride);
                SetButtonIcon(overrideBtn, s);
            }
        }
    }
    protected override List<GridRowSpec> GenerateStatsLayout()
    {
        var layout = new List<GridRowSpec>();

        layout.Add(new GridRowSpec(GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)));

        var monsters = ModPackage.Instance.loadedMod.GetAll<MonsterData>();
        if (monsters != null)
        {
            string poolBtnText = _currentPoolIndex == 0 ? "Mod Pool: New Monster" : $"Mod Pool: {CurrentEntity.entityName}";
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateButton("BtnOpenPool", poolBtnText, 0.70f, OpenModPoolModal),
                GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
            ));
        }



        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Monster Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentEntity.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })
        ));

        // The Monster selection triggers now dynamically track the Monster Size.
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Base:", 0.15f),
            GridCellSpec.CreateDiceButton("MonsterBtn", "P", 0.15f, () =>
            {
                OpenMonsterPortraitsModal((selectedMonster, selectedSprite) =>
                {
                    CurrentEntity.baseMonster = selectedMonster.ToString();
                    CurrentEntity.size = MonsterDatabase.GetMonsterSize(selectedMonster); // Set Size Based on Database

                    NotifyStateChanged();
                    UpdateUIFromData();
                    RebuildDiceScrollView(); // Force rebuild dice so faces update to match the new size category
                });
            }),
            GridCellSpec.CreateInput("BaseMonster", "Wolf", 0.70f, (val) =>
            {
                CurrentEntity.baseMonster = val;
                if (Enum.TryParse(val, true, out MonsterType parsedType))
                {
                    CurrentEntity.size = MonsterDatabase.GetMonsterSize(parsedType);
                }
                NotifyStateChanged();
                RebuildDiceScrollView(); // Force rebuild dice so faces update to match the new size category
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Icon Override:", 0.30f),
            GridCellSpec.CreateDiceButton("OverrideBtn", "P", 0.15f, () => OpenAllPortraitsModal((isHero, enumValue, selectedSprite) =>
            {
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
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => {
                CurrentEntity.hp = (string.IsNullOrWhiteSpace(val) || !int.TryParse(val, out int parsedHp)) ? 0 : parsedHp;
                NotifyStateChanged();
            }), GridCellSpec.CreateLabel("Bal:", 0.2f),
            GridCellSpec.CreateInput("Bal", "", 0.3f, (val) => { CurrentEntity.bal = val; NotifyStateChanged(); })
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

        // ==========================================
        // ADDED: P-HUE & T-HUE LAYOUTS
        // ==========================================
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("P-Hue Swap:", 0.30f),
            GridCellSpec.CreateButton("PhueStartBtn", "Target", 0.35f, () => {
                if (uiGenerator.colorPicker == null) return;
                Color initialColor = CurrentEntity.phue != null ? CurrentEntity.phue.colorStart : Color.white;
                OpenColorPicker(initialColor, (color) => {
                    if (CurrentEntity.phue == null) CurrentEntity.phue = new Phue();
                    CurrentEntity.phue.colorStart = color;
                    NotifyStateChanged();
                });
            }),
            GridCellSpec.CreateButton("PhueDestBtn", "Replace", 0.35f, () => {
                if (uiGenerator.colorPicker == null) return;
                Color initialColor = CurrentEntity.phue != null ? CurrentEntity.phue.colorDestination : Color.white;
                OpenColorPicker(initialColor, (color) => {
                    if (CurrentEntity.phue == null) CurrentEntity.phue = new Phue();
                    CurrentEntity.phue.colorDestination = color;
                    NotifyStateChanged();
                });
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("P-Hue Range:", 0.30f),
            GridCellSpec.CreateSlider("PhueRangeSlider", 0, 99, true, 0.70f, (val) => {
                if (CurrentEntity.phue == null) CurrentEntity.phue = new Phue();
                CurrentEntity.phue.colorRange = Mathf.RoundToInt(val);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("T-Hue Color:", 0.35f),
            GridCellSpec.CreateButton("ThueColorBtn", "Pick Color", 0.65f, () => {
                if (uiGenerator.colorPicker == null) return;
                Color initialColor = CurrentEntity.thue != null ? CurrentEntity.thue.colorHex : Color.white;
                OpenColorPicker(initialColor, (color) => {
                    if (CurrentEntity.thue == null) CurrentEntity.thue = new Thue { colorRange = 0, colorOffset = 0 };
                    CurrentEntity.thue.colorHex = color;
                    NotifyStateChanged();
                });
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("T-Hue Range:", 0.20f),
            GridCellSpec.CreateSlider("ThueRangeSlider", 0, 99, true, 0.30f, (val) => {
                if (CurrentEntity.thue == null) CurrentEntity.thue = new Thue();
                CurrentEntity.thue.colorRange = Mathf.RoundToInt(val);
                NotifyStateChanged();
            }),
            GridCellSpec.CreateLabel("T-Hue Shift:", 0.20f),
            GridCellSpec.CreateSlider("ThueOffsetSlider", -99, 99, true, 0.30f, (val) => {
                if (CurrentEntity.thue == null) CurrentEntity.thue = new Thue();
                CurrentEntity.thue.colorOffset = Mathf.RoundToInt(val);
                NotifyStateChanged();
            })
        ));
        // ==========================================


        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentEntity.doc = val.SanitizeRichInput(); NotifyStateChanged(); })
        ));

        string[] rawNames = Enum.GetNames(typeof(BaseItems));
        string[] formattedItemNames = rawNames.Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2")).ToArray();

        // 3. Items (Strings)
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Item:",
            uniqueKey: "Item",
            availableChoices: formattedItemNames,
            currentActiveItems: CurrentEntity.items ?? new List<string>(),
            getKey: (itemName) => itemName,
            getDisplay: (itemName) => itemName,
            onAdd: (itemName) =>
            {
                if (CurrentEntity.items == null)
                    CurrentEntity.items = new List<string>();

                if (!CurrentEntity.items.Contains(itemName))
                {
                    CurrentEntity.items.Add(itemName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (itemName) =>
            {
                if (CurrentEntity.items != null && CurrentEntity.items.Remove(itemName))
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
            currentActiveItems: CurrentEntity.customItems?.Select(i => i.entityName).ToList() ?? new List<string>(), // Note: change to 'name' if ItemData uses 'name'
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (itemName) =>
            {
                if (CurrentEntity.customItems == null)
                {
                    Debug.LogError($"CUSTOM ITEMS NULL, CATATSROPHIC ERROR.", this);
                }
                if (!CurrentEntity.customItems.Any(i => i.entityName == itemName)) // Note: change to 'name' if needed
                {
                    CurrentEntity.customItems.Add(new ItemData { entityName = itemName }); // Note: change to 'name' if needed
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (itemName) =>
            {
                if (CurrentEntity.customItems != null)
                {
                    var target = CurrentEntity.customItems.FirstOrDefault(i => i.entityName == itemName); // Note: change to 'name' if needed
                    if (target != null && CurrentEntity.customItems.Remove(target))
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
            currentActiveItems: CurrentEntity.traits ?? new List<string>(),
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
                if (CurrentEntity.traits == null)
                    CurrentEntity.traits = new List<string>();

                if (!CurrentEntity.traits.Contains(traitName))
                {
                    CurrentEntity.traits.Add(traitName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (traitName) =>
            {
                if (CurrentEntity.traits != null && CurrentEntity.traits.Remove(traitName))
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
            currentActiveItems: CurrentEntity.blessings ?? new List<string>(),
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
                if (CurrentEntity.blessings == null)
                    CurrentEntity.blessings = new List<string>();

                if (!CurrentEntity.blessings.Contains(blessingName))
                {
                    CurrentEntity.blessings.Add(blessingName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (blessingName) =>
            {
                if (CurrentEntity.blessings != null && CurrentEntity.blessings.Remove(blessingName))
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
            currentActiveItems: CurrentEntity.curses ?? new List<string>(),
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
                if (CurrentEntity.curses == null)
                    CurrentEntity.curses = new List<string>();

                if (!CurrentEntity.curses.Contains(curseName))
                {
                    CurrentEntity.curses.Add(curseName);
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            },
            onRemove: (curseName) =>
            {
                if (CurrentEntity.curses != null && CurrentEntity.curses.Remove(curseName))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        // ==========================================
        // ADDED: ORB SELECTORS FOR MONSTER UI
        // ==========================================
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Base Orb:", uniqueKey: "BaseOrb",
            availableChoices: OrbData.ValidBaseOrbs.ToList(),
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && o.isHardcoded).Select(o => o.hardcodedAbilityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (orbName) => {
                bool alreadyExists = CurrentEntity.customOrbs?.Any(o => o != null && o.isHardcoded && string.Equals(o.hardcodedAbilityName, orbName, StringComparison.OrdinalIgnoreCase)) ?? false;
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
                var target = CurrentEntity.customOrbs?.FirstOrDefault(o => o != null && o.isHardcoded && string.Equals(o.hardcodedAbilityName, orbName, StringComparison.OrdinalIgnoreCase));
                if (target != null && CurrentEntity.customOrbs.Remove(target))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );

        var customAbilityNames = ModPackage.Instance.CustomAbilities?.Select(a => a.entityName).ToList() ?? new List<string>();
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Orb:", uniqueKey: "CustomOrb",
            availableChoices: customAbilityNames,
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && !o.isHardcoded).Select(o => o.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (abilityName) => {
                bool alreadyExists = CurrentEntity.customOrbs?.Any(o => o != null && !o.isHardcoded && string.Equals(o.entityName, abilityName, StringComparison.OrdinalIgnoreCase)) ?? false;
                if (!alreadyExists)
                {
                    var template = ModPackage.Instance.CustomAbilities?.FirstOrDefault(a => string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                    if (template != null)
                    {
                        string json = JsonUtility.ToJson(template);
                        OrbData clonedOrb = JsonUtility.FromJson<OrbData>(json);
                        clonedOrb.isHardcoded = false;
                        clonedOrb.carrierPrefix = "sthief.abilitydata";
                        CurrentEntity.AddCustomAbility(clonedOrb);
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (abilityName) => {
                var target = CurrentEntity.customOrbs?.FirstOrDefault(o => o != null && !o.isHardcoded && string.Equals(o.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                if (target != null && CurrentEntity.customOrbs.Remove(target))
                {
                    NotifyStateChanged();
                    RebuildStatsUI();
                }
            }
        );
        // ==========================================

        return layout;
    }

    //////////////////
    ///

    // =====================================================================
    // SIZE SUFFIX HELPERS
    // =====================================================================
    private string GetSizeSuffix(MonsterSize size)
    {
        switch (size)
        {
            case MonsterSize.Tiny: return "_12x12";
            case MonsterSize.Big: return "_22x22";
            case MonsterSize.Huge: return "_28x28";
            case MonsterSize.HeroSized:
            default: return "_16x16";
        }
    }

    private int ExtractIdFromSpriteName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return -1;
        string[] parts = spriteName.Split('_');
        if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
        {
            return parsedId;
        }
        return -1;
    }

    // =====================================================================
    // OPEN BASE DICE FACE MODAL (SIZE CONSTRAINED)
    // =====================================================================

    // =====================================================================
    // OPEN FACADE DICE FACE MODAL (SIZE CONSTRAINED)
    // =====================================================================

    // =====================================================================
    // PORTRAIT SPRITE RESOLVER FALLBACK
    // =====================================================================
    private Sprite GetPortraitSprite(string baseMonsterName)
    {
        if (string.IsNullOrEmpty(baseMonsterName) || baseMonsterName.Equals("None", StringComparison.OrdinalIgnoreCase))
            return null;

        if (EntityUIHelpers.AllActionSprites == null)
            return null;

        string targetLeaf = baseMonsterName.ToLower();

        // Search action sprites for our custom "prt_" prefix matching this monster leaf name
        foreach (var sprite in EntityUIHelpers.AllActionSprites)
        {
            if (sprite != null && sprite.name.StartsWith("prt_"))
            {
                string leafName = IconPickerModal.GetCleanLeafName(sprite.name);
                if (leafName == targetLeaf)
                {
                    return sprite;
                }
            }
        }

        // Standard lookup fallback
        return EntityUIHelpers.GetSpriteForPortrait(baseMonsterName);
    }

    ///////////////////////////////////////////////
    ///


    // --- INSIDE MonsterUI.cs ---
    // Add this helper method inside the class:

    private int GetMaxBaseIdForSize(MonsterSize size)
    {
        switch (size)
        {
            case MonsterSize.Tiny: return 17;
            case MonsterSize.Big: return 31;
            case MonsterSize.Huge: return 27;
            case MonsterSize.HeroSized:
            default: return 187;
        }
    }

    // Then update OpenBaseModal, OpenFacadeModal, and GetBaseDiceSprite:

    protected override void OpenBaseModal(int faceIndex)
    {
        if (iconPicker == null) return;

        MonsterSize currentSize = CurrentEntity.size;
        string expectedPrefix = GetPrefixForSize(currentSize);
        int maxAllowedId = GetMaxBaseIdForSize(currentSize);

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,

            // STRICT FILTER: Must start with prefix AND be within the official ID boundaries
            IsValid = (index, sprite) =>
            {
                if (sprite == null || !sprite.name.StartsWith($"{expectedPrefix}_", StringComparison.OrdinalIgnoreCase)) return false;
                int id = ExtractIdFromSpriteName(sprite.name);
                return id >= 0 && id <= maxAllowedId;
            },

            GetSearchName = (index, sprite) =>
            {
                int id = ExtractIdFromSpriteName(sprite.name);
                return MonsterDatabase.GetFaceName(currentSize, id);
            },
            GetTooltip = (index, sprite) =>
            {
                int id = ExtractIdFromSpriteName(sprite.name);
                return $"ID {id}: {MonsterDatabase.GetFaceName(currentSize, id)}";
            },

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    int parsedId = ExtractIdFromSpriteName(sprite.name);
                    if (parsedId >= 0)
                    {
                        CurrentEntity.diceSides[faceIndex].effectID = parsedId;
                        NotifyStateChanged();
                        RebuildDiceScrollView();
                    }
                }
            }
        };
        iconPicker.OpenModal(config);
    }
    protected override void OpenFacadeModal(int faceIndex)
    {
        if (iconPicker == null) return;

        MonsterSize currentSize = CurrentEntity.size;
        string sizeSuffix = GetSizeSuffix(currentSize);
        int maxAllowedId = GetMaxBaseIdForSize(currentSize);

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,

            IsValid = (index, sprite) =>
            {
                if (sprite == null) return false;
                string name = sprite.name;

                if (name.StartsWith("prt_") || name.StartsWith("trg_") || name.StartsWith("ui_"))
                    return false;

                return name.Contains(sizeSuffix);
            },

            GetSearchName = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("tin_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("big_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("hug_", StringComparison.OrdinalIgnoreCase))
                {
                    int id = ExtractIdFromSpriteName(sprite.name);

                    if (id > maxAllowedId) return IconPickerModal.GetCleanLeafName(sprite.name);

                    return MonsterDatabase.GetFaceName(currentSize, id);
                }
                return IconPickerModal.GetCleanLeafName(sprite.name);
            },

            GetTooltip = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("tin_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("big_", StringComparison.OrdinalIgnoreCase) ||
                    sprite.name.StartsWith("hug_", StringComparison.OrdinalIgnoreCase))
                {
                    int id = ExtractIdFromSpriteName(sprite.name);

                    if (id > maxAllowedId) return $"Community Facade [{IconPickerModal.GetCleanLeafName(sprite.name)}]";

                    return $"Base ID {id}: {MonsterDatabase.GetFaceName(currentSize, id)}";
                }
                return $"Community Facade [{IconPickerModal.GetCleanLeafName(sprite.name)}]";
            },

            // =====================================================================
            // UPDATE THIS CALLBACK BELOW
            // =====================================================================
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

                        // TRANSLATION LAYER: Sequential bas offsets for base game monster faces
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

                        CurrentEntity.diceSides[faceIndex].facadeID = facadeStr;
                    }
                    else
                    {
                        // Fallback case for non-standard community structures
                        CurrentEntity.diceSides[faceIndex].facadeID = filename;
                    }

                    NotifyStateChanged();
                    RebuildDiceScrollView();
                }
            }
        };
        iconPicker.OpenModal(config);
    }
    protected override Sprite GetBaseDiceSprite(int effectID)
    {
        if (EntityUIHelpers.AllActionSprites == null) return null;

        string expectedPrefix = GetPrefixForSize(CurrentEntity.size);
        string searchString = $"{expectedPrefix}_{effectID}_";

        foreach (var sprite in EntityUIHelpers.AllActionSprites)
        {
            if (sprite != null && sprite.name.StartsWith(searchString, StringComparison.OrdinalIgnoreCase))
            {
                return sprite;
            }
        }
        return null;
    }



    private void OpenModPoolModal()
    {
        if (iconPicker == null) return;

        var monsters = ModPackage.Instance.loadedMod.GetAll<MonsterData>();
        Sprite[] monsterSprites = new Sprite[monsters.Count + 1];

        monsterSprites[0] = EntityUIHelpers.GetSpriteForPortrait("Statue");

        for (int i = 0; i < monsters.Count; i++)
        {
            var h = monsters[i];

            // Check if we have a valid custom override image; otherwise, fall back to the base monster name
            bool hasOverride = !string.IsNullOrEmpty(h.imageOverride) &&
                               !h.imageOverride.Equals("None", StringComparison.OrdinalIgnoreCase);

            string targetImageName = hasOverride ? h.imageOverride : h.baseMonster;

            // Use the specific GetPortraitSprite method to resolve the correct custom/base sprite
            monsterSprites[i + 1] = GetPortraitSprite(targetImageName);
        }

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = monsterSprites,
            DisableDeduplication = true,
            AllowNullSprites = true,

            IsValid = (index, sprite) => true,

            CellSize = new Vector2(80, 80),

            GetSearchName = (index, sprite) => index == 0 ? "Stand Alone Monster (New)" : monsters[index - 1].entityName,
            GetTooltip = (index, sprite) => index == 0 ? "Create a new blank Monster" : monsters[index - 1].entityName,

            GetNameText = (index, sprite) => index == 0 ? "New Monster" : monsters[index - 1].entityName,
            GetTierText = (index, sprite) => "",
            GetHPText = (index, sprite) => index == 0 ? "-" : (monsters[index - 1].hp > 0 ? monsters[index - 1].hp.ToString() : ""),
            GetColor = (index, sprite) => EffectKeywordColors.Purple,

            OnSelectionMade = (index, sprite) =>
            {
                OnPoolMonsterSelected(index);
            }
        };

        iconPicker.OpenModal(config);
    }

    private void OnPoolMonsterSelected(int index)
    {
        if (isDrawingUI) return;
        _currentPoolIndex = index;

        var monsters = ModPackage.Instance.loadedMod.GetAll<MonsterData>();

        if (index > 0 && (index - 1) < monsters.Count)
        {
            var originalMonster = monsters[index - 1];
            ModPackage.Instance.LoadEntityForEditing(originalMonster);
        }
        else
        {
            ModPackage.Instance.LoadEntityForEditing(new MonsterData());
        }

        ModPackage.Instance.NotifyActiveEntityChanged<MonsterData>(this);

        RebuildStatsUI();
        RebuildDiceScrollView();
    }
}