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

        // 1. Reset and Pool Buttons
        AppendHeaderButtons(layout, "Hero", OpenModPoolModal);

        // 2. Name
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hero Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { if (isDrawingUI) return; CurrentEntity.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })
        ));

        // 3. Replica Base
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Replica Base:", 0.35f),
            GridCellSpec.CreateDiceButton("ReplicaBtn", "P", 0.15f, () => OpenHeroPortraitsModal((selectedHero, selectedSprite) => {
                CurrentEntity.baseReplica = selectedHero.ToString();
                NotifyStateChanged();
                UpdateUIFromData();
            })),
            GridCellSpec.CreateInput("ReplicaName", "Statue", 0.50f, (val) => { CurrentEntity.baseReplica = val; NotifyStateChanged(); })
        ));

        // 4. Icon Override & Custom Image Panel
        AppendIconOverrideLayout(layout);

        // 5. HP & Tier
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
                    CurrentEntity.tier = -1;
                }
                else if (int.TryParse(val, out int t))
                {
                    CurrentEntity.tier = t;
                }
                NotifyStateChanged();
            })
        ));

        // 6. Color Class
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

        // 7. Color Modifiers (Order: P -> THue -> HSV)
        AppendColorModifiersLayout(layout);

        // 8. Speech
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Speech:", 0.20f),
            GridCellSpec.CreateInput("Speech", "", 0.80f, (val) => { CurrentEntity.speech = val.SanitizeRichInput(); NotifyStateChanged(); })
        ));

        // 9. Docs
        AppendDocLayout(layout);

        // 10. Base Abilities (Hero Specific)
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

        // 11. Items, Custom Abilities & Collections
        var customAbilityNames = ModPackage.Instance.CustomAbilities?.Select(a => a.entityName).ToList() ?? new List<string>();

        AppendCustomAbilitiesSelector(layout, customAbilityNames);
        AppendStandardItemsSelector(layout);
        AppendCustomItemsSelector(layout);
        AppendTraitsBlessingsCursesSelectors(layout);
        AppendOrbSelectors(layout, customAbilityNames);

        return layout;
    }
}