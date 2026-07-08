using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class HeroUI : EntityUI<HeroData>
{
    private Sprite _customImageCachedSprite;

    // =====================================================================
    // SPECIFIC OVERRIDES
    // =====================================================================
    protected override bool AllowFacades() => true;

    protected override string ExportEntity(HeroData entity) => entity.Export();

    protected override HeroData ParseEntity(string data)
    {
        HeroData h = new HeroData();
        h.Parse(data);
        return h;
    }

    protected override HeroData CreateDefaultEntity()
    {
        HeroData hero = new HeroData();
        hero.InitializeAsDefault();
        return hero;
    }

    // =====================================================================
    // MODALS & HELPERS
    // =====================================================================
    protected override void OpenBaseModal(int faceIndex)
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

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
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

                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31)
                            facadeStr = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27)
                            facadeStr = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17)
                            facadeStr = $"bas{248 + parsedId}";
                        else
                            facadeStr = $"{parts[0]}{parts[1]}";

                        CurrentEntity.diceSides[faceIndex].facadeID = facadeStr;
                    }
                    else
                    {
                        CurrentEntity.diceSides[faceIndex].facadeID = filename;
                    }

                    NotifyStateChanged();
                    RebuildDiceScrollView();
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    protected override Sprite GetBaseDiceSprite(int effectID) => EntityUIHelpers.GetBaseSprite(effectID);

    protected override Sprite GetFacadeDiceSprite(string facadeID) => EntityUIHelpers.GetFacadeSprite(ResolveFacadeName(facadeID));

    protected override void UpdateIcon(int index)
    {
        if (portraitPreview == null) return;
        var face = CurrentEntity.diceSides[index];
        portraitPreview.SetSlotIcon(index, ResolveFacadeName(face.facadeID), face.effectID, face.facadeColor, face.pips);
    }

    private string ResolveFacadeName(string facadeID)
    {
        if (string.IsNullOrEmpty(facadeID)) return facadeID;

        if (EntityUIHelpers.GetFacadeSprite(facadeID) != null) return facadeID;

        var match = Regex.Match(facadeID, @"^([a-zA-Z]+)(\d+)$");
        if (match.Success)
        {
            string prefix = match.Groups[1].Value;
            string id = match.Groups[2].Value;
            string searchPrefix = $"{prefix}_{id}_";

            if (prefix.ToLower() == "bas" && int.TryParse(id, out int basId))
            {
                if (basId >= 188 && basId <= 219) searchPrefix = $"big_{basId - 188}_";
                else if (basId >= 220 && basId <= 247) searchPrefix = $"hug_{basId - 220}_";
                else if (basId >= 248 && basId <= 265) searchPrefix = $"tin_{basId - 248}_";
            }

            var sprite = EntityUIHelpers.AllActionSprites.FirstOrDefault(sp => sp != null && sp.name.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase));
            if (sprite != null) return sprite.name;
        }

        return facadeID;
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
                    onHeroSelected?.Invoke(hero, sprite);
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
                    onPortraitSelected?.Invoke(true, (int)hero, sprite);
                else if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster))
                    onPortraitSelected?.Invoke(false, (int)monster, sprite);
            }
        };
        iconPicker.OpenModal(config);
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
            CellSize = new Vector2(80, 80),
            GetSearchName = (index, sprite) => index == 0 ? "Stand Alone Hero (New)" : heroes[index - 1].entityName,
            GetTooltip = (index, sprite) => index == 0 ? "Create a new blank hero" : heroes[index - 1].entityName,
            GetNameText = (index, sprite) => index == 0 ? "New Hero" : heroes[index - 1].entityName,
            GetTierText = (index, sprite) => index == 0 ? "" : heroes[index - 1].tier.ToString(),
            GetHPText = (index, sprite) => index == 0 ? "" : (heroes[index - 1].hp > 0 ? heroes[index - 1].hp.ToString() : ""),
            GetColor = (index, sprite) => index == 0
                ? Color.white
                : SDColors.GetColor(EntityUIHelpers.ReverseLookupColor(heroes[index - 1].colorClass)),
            OnSelectionMade = (index, sprite) => OnPoolDropdownChanged(index)
        };

        iconPicker.OpenModal(config);
    }

    // =====================================================================
    // SPECIFIC UI DATA BINDING
    // =====================================================================
    protected override void UpdateSpecificUIFromData()
    {
        if (statsUI.Inputs.TryGetValue("Tier", out var tierIn))
        {
            int displayTier = CurrentEntity.GetEffectiveTier();

            // Populate with the effective tier (either explicit or inherent)
            tierIn.SetTextWithoutNotify(displayTier > 0 ? displayTier.ToString() : "");
        }
        if (statsUI.Inputs.TryGetValue("ReplicaName", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentEntity.baseReplica);
        if (statsUI.Inputs.TryGetValue("OverrideName", out var overNameIn)) overNameIn.SetTextWithoutNotify(CurrentEntity.imageOverride);
        if (statsUI.Inputs.TryGetValue("Speech", out var speechIn)) speechIn.SetTextWithoutNotify(CurrentEntity.speech);

        if (statsUI.Dropdowns.TryGetValue("Color", out var colDrop))
        {
            HeroColorOption colOpt = EntityUIHelpers.ReverseLookupColor(CurrentEntity.colorClass);
            colDrop.SetValueWithoutNotify((int)colOpt);
        }

        // Cache Custom Image implementation unique to HeroUI
        if (!string.IsNullOrEmpty(_customImageString) && CurrentEntity.imageOverride == _customImageString && _customImageTexture != null)
        {
            if (_customImageCachedSprite == null)
            {
                _customImageCachedSprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }

    protected override void UpdateSpecificVisuals()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetTierText(CurrentEntity.GetEffectiveTier().ToString());

            HeroColorOption colOpt = EntityUIHelpers.ReverseLookupColor(CurrentEntity.colorClass);
            portraitPreview.SetHeroColor(SDColors.GetColor(colOpt));

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentEntity.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                portraitPreview.portrait.sprite = _customImageCachedSprite;
            }
            else
            {
                Sprite targetSprite = EntityUIHelpers.GetSpriteForPortrait(string.IsNullOrEmpty(CurrentEntity.imageOverride) || CurrentEntity.imageOverride == "None" ? CurrentEntity.baseReplica : CurrentEntity.imageOverride);
                if (targetSprite != null && portraitPreview.portrait != null)
                {
                    portraitPreview.portrait.sprite = targetSprite;
                }
            }
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            if (statsUI.Buttons.TryGetValue("ReplicaBtn", out var replicaBtn))
            {
                Sprite s = EntityUIHelpers.GetSpriteForPortrait(CurrentEntity.baseReplica);
                SetButtonIcon(replicaBtn, s);
            }
            if (statsUI.Buttons.TryGetValue("OverrideBtn", out var overrideBtn))
            {
                Sprite s = EntityUIHelpers.GetSpriteForPortrait(CurrentEntity.imageOverride);
                SetButtonIcon(overrideBtn, s);
            }
        }
    }

    // =====================================================================
    // STATS LAYOUT GENERATION
    // =====================================================================
    protected override List<GridRowSpec> GenerateStatsLayout()
    {
        var layout = new List<GridRowSpec>();

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)
        ));

        string poolBtnText = _currentPoolIndex == 0 ? "Mod Pool: New Hero" : $"Mod Pool: {CurrentEntity.entityName}";
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton("BtnOpenPool", poolBtnText, 0.70f, OpenModPoolModal),
            GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { if (isDrawingUI) return; CurrentEntity.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })));

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
                    GridCellSpec.CreateInput("HP", "", 0.3f, (val) => {
                        CurrentEntity.hp = (string.IsNullOrWhiteSpace(val) || !int.TryParse(val, out int parsedHp)) ? 0 : parsedHp;
                        NotifyStateChanged();
                    }),
                    GridCellSpec.CreateLabel("Tier:", 0.2f),
                    GridCellSpec.CreateInput("Tier", "", 0.3f, (val) => {
                        if (isDrawingUI) return;

                        if (string.IsNullOrWhiteSpace(val))
                        {
                            CurrentEntity.tier = -1; // Empty box falls back to inherent replica tier
                        }
                        else if (int.TryParse(val, out int t))
                        {
                            CurrentEntity.tier = t; // Explicit number typed (including 0)
                        }
                        NotifyStateChanged();
                    })
                ));

        HeroColorOption currentOption = SDColors.GetOptionFromColorCode(CurrentEntity.colorClass);
        string currentFormattedName = SDColors.GetFormattedColorName(currentOption);
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Color Class:", 0.35f),
            GridCellSpec.CreateFilteredDropdown("Color", currentFormattedName, 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                HeroColorOption selectedColor = (HeroColorOption)val;
                CurrentEntity.colorClass = SDColors.GetColorCode(selectedColor);
                NotifyStateChanged();
            })
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
                CurrentEntity.thue.colorRange = Mathf.RoundToInt(val);
                NotifyStateChanged();
            }),
            GridCellSpec.CreateLabel("T-Hue Shift:", 0.20f),
            GridCellSpec.CreateSlider("ThueOffsetSlider", -99, 99, true, 0.30f, (val) => {
                CurrentEntity.thue.colorOffset = Mathf.RoundToInt(val);
                NotifyStateChanged();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Speech:", 0.20f),
            GridCellSpec.CreateInput("Speech", "", 0.80f, (val) => { CurrentEntity.speech = val.SanitizeRichInput(); NotifyStateChanged(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentEntity.doc = val.SanitizeRichInput(); NotifyStateChanged(); })
        ));

        AppendCollectionSelector<BaseAbility>(
                    layout: layout, label: "Add Ability:", uniqueKey: "BaseAbility",
                    availableChoices: BaseAbilityDatabase.Abilities,
                    currentActiveItems: CurrentEntity.baseAbilityData ?? new List<string>(),
                    getKey: (ability) => ability.name,
                    getDisplay: (ability) => $"{ability.name} ({ability.cost}): {(ability.effect ?? "").Replace("\n", " | ")}",
                    onAdd: (ability) => {
                        if (CurrentEntity.baseAbilityData == null) CurrentEntity.baseAbilityData = new List<string>();
                        if (!CurrentEntity.baseAbilityData.Contains(ability.name))
                        {
                            CurrentEntity.baseAbilityData.Add(ability.name);
                            NotifyStateChanged();
                            RebuildStatsUI();
                        }
                    },
                    onRemove: (abilityName) => {
                        if (CurrentEntity.baseAbilityData != null && CurrentEntity.baseAbilityData.Remove(abilityName))
                        {
                            NotifyStateChanged();
                            RebuildStatsUI();
                        }
                    }
                );

        var customAbilityNames = ModPackage.Instance.CustomAbilities?.Select(a => a.entityName).ToList() ?? new List<string>();
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Ability:", uniqueKey: "CustomAbility",
            availableChoices: customAbilityNames,
            currentActiveItems: CurrentEntity.customAbilityData?.Select(a => a.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (abilityName) => {
                bool alreadyExists = CurrentEntity.customAbilityData?.Any(a => a.entityName == abilityName) ?? false;
                if (!alreadyExists)
                {
                    var template = ModPackage.Instance.CustomAbilities.FirstOrDefault(a => a.entityName == abilityName);
                    if (template != null)
                    {
                        string json = JsonUtility.ToJson(template);
                        AbilityData clonedAbility = JsonUtility.FromJson(json, template.GetType()) as AbilityData;
                        CurrentEntity.AddCustomAbility(clonedAbility);
                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (abilityName) => { }
        );

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

        // --- UPDATED COLLECTION SELECTOR IN HeroUI ---
        AppendCollectionSelector<string>(
            layout: layout,
            label: "Add Custom Item:",
            uniqueKey: "CustomItem",
            // 1. Hook up available choices using unityName with entityName fallbacks
            availableChoices: ModPackage.Instance?.CustomItems?
                .Select(i => !string.IsNullOrEmpty(i.unityName) ? i.unityName : (!string.IsNullOrEmpty(i.entityName) ? i.entityName : "Unnamed Item"))
                .Distinct()
                .ToList() ?? new List<string>(),

            // 2. Map current items safely using unityName first to avoid string bloat in the UI
            currentActiveItems: CurrentEntity.customPayloads?
                .Where(p => p.Type == PayloadType.Item)
                .Select(p => p.Data as ItemData)
                .Where(item => item != null)
                .Select(item => !string.IsNullOrEmpty(item.unityName) ? item.unityName : item.entityName)
                .ToList() ?? new List<string>(),

            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (itemName) =>
            {
                if (CurrentEntity.customPayloads == null)
                {
                    CurrentEntity.customPayloads = new List<CustomPayload>();
                }

                var templateItem = ModPackage.Instance?.CustomItems?.FirstOrDefault(i =>
                    i.unityName == itemName || i.entityName == itemName);

                if (templateItem != null)
                {
                    bool alreadyExists = CurrentEntity.customPayloads.Any(p =>
                        p.Type == PayloadType.Item &&
                        ((p.Data as ItemData)?.unityName == itemName || (p.Data as ItemData)?.entityName == itemName));

                    if (!alreadyExists)
                    {
                        // PROPER FIX: Deep clone the item so it retains its effects and stats
                        string json = JsonUtility.ToJson(templateItem);
                        ItemData clonedItem = JsonUtility.FromJson<ItemData>(json);

                        // Optional: Ensure the names are forced to match the template if JsonUtility misses them
                        clonedItem.entityName = templateItem.entityName;
                        clonedItem.unityName = templateItem.unityName;

                        CurrentEntity.customPayloads.Add(new CustomPayload { Type = PayloadType.Item, Data = clonedItem });

                        // NOTE: If the hero also needs this item explicitly EQUIPPED (not just defined in the payload), 
                        // you may also need to add it to the standard items list here depending on your Export logic:
                        // if (CurrentEntity.items == null) CurrentEntity.items = new List<string>();
                        // if (!CurrentEntity.items.Contains(clonedItem.entityName)) CurrentEntity.items.Add(clonedItem.entityName);

                        NotifyStateChanged();
                        RebuildStatsUI();
                    }
                }
            },
            onRemove: (itemName) =>
            {
                if (CurrentEntity.customPayloads != null)
                {
                    // Locate payload utilizing the prioritized name schema
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

        // ==========================================
        // ADDED: ORB SELECTORS FOR HERO UI
        // ==========================================
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Base Orb:", uniqueKey: "BaseOrb",
            availableChoices: OrbData.ValidBaseOrbs.ToList(),
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && o.isHardcoded).Select(o => o.hardcodedAbilityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (orbName) => {
                if (CurrentEntity.customOrbs == null) CurrentEntity.customOrbs = new List<OrbData>();
                bool alreadyExists = CurrentEntity.customOrbs.Any(o => o != null && o.isHardcoded && string.Equals(o.hardcodedAbilityName, orbName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyExists)
                {
                    OrbData newOrb = new OrbData();
                    newOrb.Parse($"orb.{orbName}");
                    CurrentEntity.customOrbs.Add(newOrb);
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

        var customOrbAbilityNames = ModPackage.Instance.CustomAbilities?.Select(a => a.entityName).ToList() ?? new List<string>();
        AppendCollectionSelector<string>(
            layout: layout, label: "Add Custom Orb:", uniqueKey: "CustomOrb",
            availableChoices: customAbilityNames,
            currentActiveItems: CurrentEntity.customOrbs?.Where(o => o != null && !o.isHardcoded).Select(o => o.entityName).ToList() ?? new List<string>(),
            getKey: (name) => name,
            getDisplay: (name) => name,
            onAdd: (abilityName) => {
                if (CurrentEntity.customOrbs == null) CurrentEntity.customOrbs = new List<OrbData>();
                bool alreadyExists = CurrentEntity.customOrbs.Any(o => o != null && !o.isHardcoded && string.Equals(o.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                if (!alreadyExists)
                {
                    var template = ModPackage.Instance.CustomAbilities?.FirstOrDefault(a => string.Equals(a.entityName, abilityName, StringComparison.OrdinalIgnoreCase));
                    if (template != null)
                    {
                        string json = JsonUtility.ToJson(template);
                        OrbData clonedOrb = JsonUtility.FromJson<OrbData>(json);
                        clonedOrb.isHardcoded = false;
                        clonedOrb.carrierPrefix = "sthief.abilitydata";
                        CurrentEntity.customOrbs.Add(clonedOrb);
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
        // ==========================================

        return layout;
    }
}