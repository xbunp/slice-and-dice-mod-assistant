using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    // Dynamically fetch the correct sprite based on the monster's size
    protected override Sprite GetBaseDiceSprite(int effectID)
    {
        if (EntityUIHelpers.AllActionSprites == null) return null;

        string expectedPrefix = GetPrefixForSize(CurrentEntity.size);
        string searchString = $"{expectedPrefix}_{effectID}_";

        foreach (var sprite in EntityUIHelpers.AllActionSprites)
        {
            if (sprite != null && sprite.name.StartsWith(searchString))
            {
                return sprite;
            }
        }
        return null; // Fallback if no matching sprite is found
    }

    protected override Sprite GetFacadeDiceSprite(string facadeID) => EntityUIHelpers.GetFacadeSprite(facadeID);
    protected override bool AllowFacades() => false;
    protected override string ExportEntity(MonsterData entity) => MonsterData.Export(entity);
    protected override MonsterData ParseEntity(string data) => MonsterData.Parse(data);

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

    protected override void OpenBaseModal(int faceIndex)
    {
        if (iconPicker == null) return;

        MonsterSize currentSize = CurrentEntity.size;
        string expectedPrefix = GetPrefixForSize(currentSize);

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,

            // Filter to ONLY show face sprites belonging to this monster's size category
            IsValid = (index, sprite) => sprite != null && sprite.name.StartsWith($"{expectedPrefix}_"),

            // Fetch the human-readable tooltip from our mapped MonsterDatabase definitions
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

    protected override void OpenFacadeModal(int faceIndex) { /* Intentionally left blank as monsters don't use facades */ }

    protected override void UpdateSpecificUIFromData()
    {
        if (statsUI.Inputs.TryGetValue("BaseMonster", out var repNameIn)) repNameIn.SetTextWithoutNotify(CurrentEntity.baseMonster);
        if (statsUI.Inputs.TryGetValue("Bal", out var balIn)) balIn.SetTextWithoutNotify(CurrentEntity.bal);
    }

    protected override void UpdateSpecificVisuals()
    {
        if (portraitPreview != null)
        {
            portraitPreview.SetTierText(""); // Hide Tier visualization for Monsters
            portraitPreview.SetHeroColor(EffectKeywordColors.Purple); // Default background tinting for monsters

            bool isUsingCustomImage = !string.IsNullOrEmpty(_customImageString) && CurrentEntity.imageOverride == _customImageString;
            if (isUsingCustomImage && _customImageTexture != null && portraitPreview.portrait != null)
            {
                portraitPreview.portrait.sprite = Sprite.Create(_customImageTexture, new Rect(0, 0, _customImageTexture.width, _customImageTexture.height), new Vector2(0.5f, 0.5f));
            }
            else
            {
                // Bind strictly to baseMonster
                Sprite targetSprite = EntityUIHelpers.GetSpriteForPortrait(CurrentEntity.baseMonster);
                if (targetSprite != null && portraitPreview.portrait != null)
                {
                    portraitPreview.portrait.sprite = targetSprite;
                }
            }
        }

        if (statsUI != null && statsUI.Buttons != null)
        {
            // Update only the single monster selection button icon
            if (statsUI.Buttons.TryGetValue("MonsterBtn", out var monsterBtn))
            {
                Sprite s = EntityUIHelpers.GetSpriteForPortrait(CurrentEntity.baseMonster);
                SetButtonIcon(monsterBtn, s);
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
            List<string> poolOptions = new List<string> { "Stand Alone Monster (New)" };
            foreach (var monster in monsters)
                poolOptions.Add(string.IsNullOrEmpty(monster.entityName) ? "New Monster" : monster.entityName);

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Mod Pool:", 0.20f),
                GridCellSpec.CreateDropdown("PoolDropdown", "", 0.50f, poolOptions.ToArray(), OnPoolDropdownChanged),
                GridCellSpec.CreateButton("BtnSavePool", "Save to Mod", 0.30f, SaveToModPool)
            ));
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Monster Name:", 0.35f),
            GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { CurrentEntity.entityName = val; NotifyStateChanged(); })
        ));

        // The Monster selection triggers now dynamically track the Monster Size.
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Base:", 0.15f),
            GridCellSpec.CreateDiceButton("MonsterBtn", "P", 0.15f, () => {
                OpenMonsterPortraitsModal((selectedMonster, selectedSprite) => {
                    CurrentEntity.baseMonster = selectedMonster.ToString();
                    CurrentEntity.size = MonsterDatabase.GetMonsterSize(selectedMonster); // Set Size Based on Database

                    NotifyStateChanged();
                    UpdateUIFromData();
                    RebuildDiceScrollView(); // Force rebuild dice so faces update to match the new size category
                });
            }),
            GridCellSpec.CreateInput("BaseMonster", "Wolf", 0.15f, (val) => {
                CurrentEntity.baseMonster = val;
                if (Enum.TryParse(val, true, out MonsterType parsedType))
                {
                    CurrentEntity.size = MonsterDatabase.GetMonsterSize(parsedType);
                }
                NotifyStateChanged();
                RebuildDiceScrollView(); // Force rebuild dice so faces update to match the new size category
            }),
            GridCellSpec.CreateButton("ToggleCustomBtn", showCustomImagePanel ? "Custom-" : "Custom+", 0.15f, ToggleCustomImagePanel)
        ));

        if (showCustomImagePanel) layout.Add(new GridRowSpec(200, GridCellSpec.CreateCustomImg("CustomImgPanel", 1.0f)));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("HP:", 0.2f),
            GridCellSpec.CreateInput("HP", "", 0.3f, (val) => { if (int.TryParse(val, out int hp)) CurrentEntity.hp = hp; NotifyStateChanged(); }),
            GridCellSpec.CreateLabel("Bal:", 0.2f),
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

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc:", 0.20f),
            GridCellSpec.CreateInput("Doc", "", 0.80f, (val) => { CurrentEntity.doc = val; NotifyStateChanged(); })
        ));

        // Shared Lists
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