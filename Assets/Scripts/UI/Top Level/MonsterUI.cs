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
    protected override string ExportEntity(MonsterData entity) => entity.Export();

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

    protected override MonsterData CreateDefaultEntity()
    {
        var newMonster = new MonsterData();
        newMonster.InitializeDiceFaces();
        newMonster.items = new List<string>();
        newMonster.traits = new List<string>();
        newMonster.blessings = new List<string>();
        newMonster.curses = new List<string>();
        newMonster.customOrbs = new List<OrbData>();
        newMonster.customPayloads = new List<CustomPayload>();

        return newMonster;
    }
    protected override void UpdateSpecificUIFromData()
    {
        if (statsUI.Inputs.TryGetValue("BaseMonster", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentEntity.baseMonster);

        // CHANGED: Bal is now bound as a Dropdown
        if (statsUI.Dropdowns.TryGetValue("Bal", out var balDrop))
        {
            // Note: Replace 'MonsterDatabase' with whatever class holds your GetMonsterNames() method
            var options = SDColors.GetMonsterNames().ToList();
            int idx = options.IndexOf(CurrentEntity.bal);
            balDrop.SetValueWithoutNotify(idx >= 0 ? idx : 0);
        }

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

        // 1. Reset and Pool Buttons
        AppendHeaderButtons(layout, "Monster", OpenModPoolModal);

        // 2. Name
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Monster Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentEntity.entityName = val.SanitizePlainInput(); NotifyStateChanged(); })
        ));

        // 3. Base Monster Selection
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Base:", 0.15f),
            GridCellSpec.CreateDiceButton("MonsterBtn", "P", 0.15f, () =>
            {
                OpenMonsterPortraitsModal((selectedMonster, selectedSprite) =>
                {
                    CurrentEntity.baseMonster = selectedMonster.ToString();
                    CurrentEntity.size = MonsterDatabase.GetMonsterSize(selectedMonster);
                    NotifyStateChanged();
                    UpdateUIFromData();
                    RebuildDiceScrollView();
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
                RebuildDiceScrollView();
            })
        ));

        // 4. Icon Override & Custom Image Panel
        AppendIconOverrideLayout(layout);

        // 5. HP & Balance
        var balOptions = SDColors.GetMonsterNames().ToList();
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP:", 0.2f),
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => {
                CurrentEntity.hp = (string.IsNullOrWhiteSpace(val) || !int.TryParse(val, out int parsedHp)) ? 0 : parsedHp;
                NotifyStateChanged();
            }),
            GridCellSpec.CreateLabel("Bal:", 0.2f),
            GridCellSpec.CreateFilteredDropdown("Bal", string.IsNullOrEmpty(CurrentEntity.bal) ? "Wolf" : CurrentEntity.bal, 0.3f, balOptions.ToArray(), (val) => {
                if (val >= 0 && val < balOptions.Count)
                {
                    CurrentEntity.bal = balOptions[val];
                    NotifyStateChanged();
                }
            })
        ));

        // 6. Color Modifiers (Order: P -> THue -> HSV)
        AppendColorModifiersLayout(layout);

        // 7. Docs
        AppendDocLayout(layout);

        // 8. Items, Abilities & Collections
        var customAbilityNames = ModPackage.Instance.CustomAbilities?.Select(a => a.entityName).ToList() ?? new List<string>();

        AppendStandardItemsSelector(layout);
        AppendCustomItemsSelector(layout);
        AppendCustomAbilitiesSelector(layout, customAbilityNames);
        AppendTraitsBlessingsCursesSelectors(layout);
        AppendOrbSelectors(layout, customAbilityNames);

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

            IsValid = (index, sprite) =>
            {
                if (sprite == null || !sprite.name.StartsWith($"{expectedPrefix}_", StringComparison.OrdinalIgnoreCase)) return false;
                if (sprite.name.StartsWith("alp", StringComparison.OrdinalIgnoreCase)) return false;
                int id = ExtractIdFromSpriteName(sprite.name);
                return id >= 0 && id <= maxAllowedId;
            },

            GetSearchName = (index, sprite) =>
            {
                int id = ExtractIdFromSpriteName(sprite.name);
                // Fix: Check if HeroSized to fetch the correct name from Hero tooltips
                if (currentSize == MonsterSize.HeroSized)
                {
                    return EntityUIHelpers.GetBaseTooltip(sprite);
                }
                return MonsterDatabase.GetFaceName(currentSize, id);
            },

            GetTooltip = (index, sprite) =>
            {
                int id = ExtractIdFromSpriteName(sprite.name);
                // Fix: Check if HeroSized to fetch the correct name from Hero tooltips
                if (currentSize == MonsterSize.HeroSized)
                {
                    string faceName = EntityUIHelpers.GetBaseTooltip(sprite);
                    return $"ID {id}: {faceName}";
                }
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

                if (name.StartsWith("prt_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("trg_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("ui_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("alp", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (currentSize == MonsterSize.HeroSized)
                {
                    return EntityUIHelpers.IsSpriteValid(sprite);
                }

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

                    // Fix: Check if HeroSized to fetch the correct name from Hero tooltips
                    if (currentSize == MonsterSize.HeroSized)
                    {
                        return EntityUIHelpers.GetBaseTooltip(sprite);
                    }
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

                    // Fix: Check if HeroSized to fetch the correct name from Hero tooltips
                    if (currentSize == MonsterSize.HeroSized)
                    {
                        string faceName = EntityUIHelpers.GetBaseTooltip(sprite);
                        return $"Base ID {id}: {faceName}";
                    }
                    return $"Base ID {id}: {MonsterDatabase.GetFaceName(currentSize, id)}";
                }
                return $"Community Facade [{IconPickerModal.GetCleanLeafName(sprite.name)}]";
            },

            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    CurrentEntity.diceSides[faceIndex].facadeID = EntityUIHelpers.FormatFacadeID(sprite.name);
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
            ModPackage.Instance.LoadEntityForEditing(CreateDefaultEntity());
        }

        ModPackage.Instance.NotifyActiveEntityChanged<MonsterData>(this);

        RebuildStatsUI();
        RebuildDiceScrollView();
    }
}