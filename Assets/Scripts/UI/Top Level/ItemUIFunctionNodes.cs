using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class NodeRegistry
{
    private static Dictionary<ItemNodeType, AuthoringNodeDef> _nodes;
    private static void EnsureInitialized()
    {
        if (_nodes == null)
        {
            _nodes = new Dictionary<ItemNodeType, AuthoringNodeDef>();
            Register(new EquippableNodeDef());
            Register(new BaseItemNodeDef());
            Register(new HatNodeDef());
            Register(new LearnAbilityNodeDef());
            Register(new OperatorNodeDef());
            Register(new ManualBracketNodeDef());
            Register(new RawStringNodeDef());
        }
    }
    public static AuthoringNodeDef Get(ItemNodeType type)
    {
        EnsureInitialized();
        return _nodes.TryGetValue(type, out var def) ? def : _nodes[ItemNodeType.RawString];
    }
    public static IEnumerable<AuthoringNodeDef> GetAll()
    {
        EnsureInitialized();
        return _nodes.Values;
    }
    private static void Register(AuthoringNodeDef def) => _nodes[def.NodeType] = def;
}

// --- NODE DEFINITIONS ---
// 2. The Definition Interface
public abstract class AuthoringNodeDef
{
    public virtual string NodeNiceName { get; protected set; } = "Unnamed Node";
    public abstract ItemNodeType NodeType { get; }
    public abstract Color GetColor();
    public abstract string GetTitle(EntityCard card);
    public virtual bool IsEntity => false;   // True for Heroes, Monsters, BaseItems
    public virtual bool IsOperator => false; // True for #, .mrg., .splice.
    public virtual bool HasDeleteButton => true;
    public virtual bool HasPayloadPort => true;

    // Core Behaviors
    //public abstract string Compile(EntityCard card);

    public abstract void DrawInspector(ItemUI ui, EntityCard card);
}

public class HatNodeDef : AuthoringNodeDef
{
    public override ItemNodeType NodeType => ItemNodeType.Hat;
    public override bool IsEntity => true;
    public override bool HasPayloadPort => true;
    public override bool HasDeleteButton => true;
    public override string NodeNiceName => "(Hat) Set Dice Faces";
    public override Color GetColor() => new Color(0.8f, 0.4f, 0.7f);

    private GridReferences _diceUI;
    private RectTransform _diceGridTarget;
    private LayoutElement _diceGridLayoutElement;
    private LayoutElement _mainContainerLayoutElement;

    private int _currentDiceTab = 0;
    private DiceFacesPreviewUI _previewUI;
    private int _currentMask = 1; // Default (left)

    private DiceFaceBuilderWidget _diceWidget;

    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null) return;

        // --- Safely retrieve EntityData (HeroData or MonsterData) ---
        if (!(card.MechanicData.PayloadData is EntityData entityData))
        {
            var defaultHero = new HeroData();
            defaultHero.InitializeAsBlank();
            defaultHero.baseReplica = "Fey";
            defaultHero.InitializeDiceFaces();
            card.MechanicData.PayloadData = defaultHero;
            entityData = defaultHero;
        }

        bool isMonster = entityData is MonsterData;

        // --- Strictly force re-initialization of mask & tab for THIS specific card instance ---
        if (card.MechanicData.Targets == null || card.MechanicData.Targets.Count == 0)
        {
            card.MechanicData.Targets = new List<string> { "left" };
        }

        string currentTargetStr = card.MechanicData.Targets[0];
        var foundAlias = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.name != null && a.name.Equals(currentTargetStr, StringComparison.OrdinalIgnoreCase));

        if (foundAlias.name != null)
        {
            _currentMask = foundAlias.mask;
        }
        else
        {
            _currentMask = 1; // Default to "left" mask (1)
            card.MechanicData.Targets[0] = "left";
        }

        _currentDiceTab = 0; // Reset tab view to "All" when switching hat cards
        // -----------------------------------------------------------------------------------------

        // 1. Hat Type Selector (Hero Hat vs Monster Hat)
        ui.CreateInspectorDropdown("Hat Type", new List<string> { "Hero (Hat)", "Monster (Hat)" }, isMonster ? 1 : 0, (idx) => {
            bool wantsMonster = idx == 1;
            if (isMonster != wantsMonster)
            {
                ConvertHatPayloadType(ui, card);
            }
        });

        // 2. Entity Fields (Hero vs Monster)
        if (isMonster)
        {
            var md = entityData as MonsterData;
            ui.CreateInspectorInputField("Monster Name", md.baseMonster ?? "Wolf", (val) => {
                md.baseMonster = val;
                ui.AutoCompile();
            });
        }
        else
        {
            var hd = entityData as HeroData;
            hd.baseReplica = "Fey";
        }

        // Initialize the generic dice widget using base EntityData
        _diceWidget = new DiceFaceBuilderWidget(
            getDiceSides: () => entityData.diceSides,
            allowFacades: () => true,
            openBaseModal: (idx) => OpenBaseModal(idx, entityData, () => { ui.AutoCompile(); RebuildHatDiceGrid(ui, card, entityData); }),
            openFacadeModal: (idx) => OpenFacadeModal(idx, entityData, () => { ui.AutoCompile(); RebuildHatDiceGrid(ui, card, entityData); }),
            getBaseSprite: (id) => EntityUIHelpers.GetBaseSprite(id),
            getFacadeSprite: (facId) => EntityUIHelpers.GetFacadeSprite(facId),
            onStateChanged: () => { ui.AutoCompile(); UpdateHatDiceUIFromData(entityData); },
            onRebuildRequested: () => { ui.AutoCompile(); RebuildHatDiceGrid(ui, card, entityData); }
        );

        // Master Container
        GameObject containerObj = new GameObject("HatDiceContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        _mainContainerLayoutElement = containerObj.GetComponent<LayoutElement>();

        var containerLayout = containerObj.AddComponent<VerticalLayoutGroup>();
        containerLayout.spacing = 10f;
        containerLayout.childControlHeight = true;
        containerLayout.childControlWidth = true;
        containerLayout.childForceExpandHeight = false;

        // Create dropdown filter initialized strictly from card data
        CreateTargetDropdowns(containerObj.transform, ui, card);

        // Dice Face Preview UI Instantiation
        if (fsg.dicePreviewAlonePrefab != null)
        {
            GameObject dicePreviewObj = UnityEngine.Object.Instantiate(fsg.dicePreviewAlonePrefab, containerObj.transform, false);

            LayoutElement previewLayout = dicePreviewObj.GetComponent<LayoutElement>() ?? dicePreviewObj.AddComponent<LayoutElement>();
            previewLayout.minHeight = 110f;
            previewLayout.preferredHeight = 120f;
            previewLayout.flexibleHeight = 0f;

            _previewUI = dicePreviewObj.GetComponent<DiceFacesPreviewUI>();

            if (_previewUI != null)
            {
                _previewUI.OnFaceSelected += (faceIndex) =>
                {
                    _currentDiceTab = faceIndex + 1;
                    RebuildHatDiceGrid(ui, card, entityData);
                };
            }
        }

        // Grid Container Setup
        GameObject gridTargetObj = new GameObject("DiceGridTarget", typeof(RectTransform), typeof(LayoutElement));
        gridTargetObj.transform.SetParent(containerObj.transform, false);
        _diceGridTarget = gridTargetObj.GetComponent<RectTransform>();
        _diceGridLayoutElement = gridTargetObj.GetComponent<LayoutElement>();

        RebuildHatDiceGrid(ui, card, entityData);
    }

    private void ConvertHatPayloadType(ItemUI ui, EntityCard card)
    {
        if (card == null || card.MechanicData == null) return;

        EntityData oldData = card.MechanicData.PayloadData as EntityData;
        if (oldData is MonsterData)
        {
            HeroData hero = new HeroData();
            hero.InitializeAsBlank();
            hero.baseReplica = "Fey";
            if (oldData != null && oldData.diceSides != null) hero.diceSides = oldData.diceSides;
            else hero.InitializeDiceFaces();
            card.MechanicData.PayloadData = hero;
        }
        else
        {
            MonsterData monster = new MonsterData();
            monster.InitializeAsBlank();
            monster.baseMonster = "Wolf";
            if (oldData != null && oldData.diceSides != null) monster.diceSides = oldData.diceSides;
            else monster.InitializeDiceFaces();
            card.MechanicData.PayloadData = monster;
        }

        ui.SelectCard(card);
        ui.AutoCompile();
    }

    public static string GetHatDiceString(EntityData entityData)
    {
        if (entityData == null) return "Fey";
        StringBuilder sb = new StringBuilder();
        string baseName = "Fey";
        if (entityData is HeroData hd)
            baseName = string.IsNullOrEmpty(hd.baseReplica) ? "Fey" : hd.baseReplica;
        else if (entityData is MonsterData md)
            baseName = string.IsNullOrEmpty(md.baseMonster) ? "Wolf" : md.baseMonster;
        sb.Append(baseName);

        // 1. Append the .sd. block
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            if (entityData.diceSides != null && entityData.diceSides[i] != null && (entityData.diceSides[i].effectID != 0 || entityData.diceSides[i].pips != 0))
            {
                lastActiveIndex = i;
            }
        }
        if (lastActiveIndex != -1)
        {
            sb.Append(".sd.");
            for (int i = 0; i <= lastActiveIndex; i++)
            {
                var side = entityData.diceSides[i];
                if (side == null || (side.effectID == 0 && side.pips == 0))
                {
                    sb.Append("0");
                }
                else
                {
                    if (side.pips == 0) sb.Append(side.effectID);
                    else sb.Append($"{side.effectID}-{side.pips}");
                }
                if (i < lastActiveIndex) sb.Append(":");
            }
        }

        // 2. Output authoritatively tracked modifiers & payloads
        string faceModifiers = entityData.BuildFaceModifiers(includeInlineFacades: false);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            sb.Append(faceModifiers);
        }

        return sb.ToString();
    }

    public override string GetTitle(EntityCard card)
    {
        string targets = card.MechanicData.Targets.Count > 0 ? string.Join(".", card.MechanicData.Targets) : "mid";

        if (card.MechanicData.PayloadData is HeroData heroData && !string.IsNullOrEmpty(heroData.baseReplica))
            return $"[{targets}] Hat: {heroData.baseReplica}";
        if (card.MechanicData.PayloadData is MonsterData monsterData && !string.IsNullOrEmpty(monsterData.baseMonster))
            return $"[{targets}] Egg: {monsterData.baseMonster}";

        return $"[{targets}] Hat (Empty)";
    }

    // --- GRID GENERATOR & DATA SYNCHRONIZATION ---
    private void RebuildHatDiceGrid(ItemUI ui, EntityCard card, EntityData entityData)
    {
        if (_diceGridTarget == null || _diceWidget == null) return;

        List<GridRowSpec> diceLayout = GenerateHatDiceLayout(ui, card, entityData, _currentDiceTab);

        _diceUI = FullScreenUIGenerator.Instance.RebuildGrid(_diceGridTarget, diceLayout, false);
        _diceWidget.SetGridReferences(_diceUI);

        _diceGridLayoutElement.minHeight = _diceUI.TotalHeight;
        _mainContainerLayoutElement.minHeight = 70f + 150f + 35f + 10f + _diceUI.TotalHeight;

        Canvas.ForceUpdateCanvases();
        UpdateHatDiceUIFromData(entityData);
    }

    private List<GridRowSpec> GenerateHatDiceLayout(ItemUI ui, EntityCard card, EntityData entityData, int tabIndex)
    {
        var layout = new List<GridRowSpec>();

        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            if (tabIndex == 0 && (_currentMask & (1 << i)) == 0) continue;

            layout.AddRange(_diceWidget.GenerateLayout(i));

            if (tabIndex == 0 && i < 5)
            {
                layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{i}", "", 1.0f)));
            }
        }

        return layout;
    }

    private void UpdateHatDiceUIFromData(EntityData entityData)
    {
        if (_previewUI != null && entityData != null && entityData.diceSides != null)
        {
            int activeFaceIndex = _currentDiceTab == 0 ? -1 : _currentDiceTab - 1;
            _previewUI.UpdateFaceStates(_currentMask, activeFaceIndex);

            for (int i = 0; i < 6; i++)
            {
                var f = entityData.diceSides[i];
                if (f != null)
                {
                    _previewUI.SetSlotIcon(i, f.facadeID, f.effectID, f.facadeColor, f.pips);
                }
            }
        }

        if (_diceWidget == null) return;

        int startIndex = (_currentDiceTab == 0) ? 0 : _currentDiceTab - 1;
        int endIndex = (_currentDiceTab == 0) ? 6 : _currentDiceTab;

        for (int i = startIndex; i < endIndex; i++)
        {
            if (_currentDiceTab == 0 && (_currentMask & (1 << i)) == 0) continue;

            _diceWidget.UpdateUIFromData(i);
            _diceWidget.UpdateVisuals(i);
        }
    }

    private GameObject CreateDropdownRow(Transform parent, string labelText, List<string> options, int initialIndex, System.Action<int> onValueChanged)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null || fsg.dropdownPrefab == null) return null;

        GameObject rowObj = new GameObject("DropdownRow", typeof(RectTransform));
        rowObj.transform.SetParent(parent, false);

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.minHeight = 35f;
        rowLE.preferredHeight = 35f;
        rowLE.flexibleHeight = 0f;

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childForceExpandWidth = false;

        GameObject labelObj = new GameObject("RowLabel", typeof(RectTransform));
        labelObj.transform.SetParent(rowObj.transform, false);

        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.minWidth = 120f;
        labelLE.preferredWidth = 120f;
        labelLE.flexibleWidth = 0f;

        var textComp = labelObj.AddComponent<TextMeshProUGUI>();
        textComp.text = labelText;
        textComp.fontSize = 14f;
        textComp.alignment = TextAlignmentOptions.Left;
        textComp.color = Color.white;

        GameObject dropdownObj = UnityEngine.Object.Instantiate(fsg.dropdownPrefab, rowObj.transform, false);
        var dropdownLE = dropdownObj.GetComponent<LayoutElement>() ?? dropdownObj.AddComponent<LayoutElement>();
        dropdownLE.flexibleWidth = 1f;

        TMP_Dropdown dropdown = dropdownObj.GetComponentInChildren<TMP_Dropdown>(true) ?? dropdownObj.GetComponent<TMP_Dropdown>();
        if (dropdown != null)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.value = initialIndex;
            dropdown.onValueChanged.AddListener((val) => onValueChanged?.Invoke(val));
        }

        return rowObj;
    }

    // --- SELF-CONTAINED MODAL TRIGGER LOGIC ---
    private IconPickerModal GetIconPicker()
    {
        return IconPickerModal.Instance;
    }

    private void OpenBaseModal(int faceIndex, EntityData entityData, System.Action onComplete)
    {
        var iconPicker = GetIconPicker();
        if (iconPicker == null || entityData == null || entityData.diceSides == null) return;

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
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        entityData.diceSides[faceIndex].effectID = parsedId;
                        onComplete?.Invoke();
                    }
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    private void OpenFacadeModal(int faceIndex, EntityData entityData, System.Action onComplete)
    {
        var iconPicker = GetIconPicker();
        if (iconPicker == null || entityData == null || entityData.diceSides == null) return;

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

                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31) facadeStr = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27) facadeStr = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17) facadeStr = $"bas{248 + parsedId}";
                        else facadeStr = $"{parts[0]}{parts[1]}";

                        entityData.diceSides[faceIndex].facadeID = facadeStr;
                    }
                    else
                    {
                        entityData.diceSides[faceIndex].facadeID = filename;
                    }
                    onComplete?.Invoke();
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    private void CreateTargetDropdowns(Transform parent, ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null || fsg.dropdownPrefab == null) return;

        var reversedAliases = DiceTargetHelper.TargetAliases.Reverse().ToList();
        List<string> mySideOptions = reversedAliases.Select(a => DiceTargetHelper.FormatAliasName(a.name)).ToList();

        var singleFaceAliases = DiceTargetHelper.TargetAliases
            .Where(a => a.name == "left" || a.name == "mid" || a.name == "top" || a.name == "bot" || a.name == "right" || a.name == "rightmost")
            .OrderBy(a => {
                if (a.name == "left") return 0;
                if (a.name == "mid") return 1;
                if (a.name == "top") return 2;
                if (a.name == "bot") return 3;
                if (a.name == "right") return 4;
                return 5;
            })
            .ToList();

        List<string> hatSideOptions = new List<string> { "(Same Face)" };
        hatSideOptions.AddRange(singleFaceAliases.Select(a => DiceTargetHelper.FormatAliasName(a.name)));

        string currentMySide = card.MechanicData.Targets.Count > 0 ? card.MechanicData.Targets[0] : "left";
        string currentHatSide = card.MechanicData.Targets.Count > 1 ? card.MechanicData.Targets[1] : null;

        int initialMyIndex = reversedAliases.FindIndex(a => a.name != null && a.name.Equals(currentMySide, StringComparison.OrdinalIgnoreCase));
        initialMyIndex = Mathf.Max(0, initialMyIndex);

        int initialHatIndex = 0;
        if (!string.IsNullOrEmpty(currentHatSide))
        {
            int foundIdx = singleFaceAliases.FindIndex(a => a.name != null && a.name.Equals(currentHatSide, StringComparison.OrdinalIgnoreCase));
            if (foundIdx != -1)
            {
                initialHatIndex = foundIdx + 1;
            }
        }

        Action updateState = () =>
        {
            ui.AutoCompile();
            ui.RefreshSidebar();

            if (card.MechanicData.PayloadData is EntityData entityData)
            {
                RebuildHatDiceGrid(ui, card, entityData);
            }
        };

        CreateDropdownRow(parent, "Target Side(s):", mySideOptions, initialMyIndex, (val) =>
        {
            var selectedAlias = reversedAliases[val];
            _currentMask = selectedAlias.mask;

            if (card.MechanicData.Targets.Count == 0)
            {
                card.MechanicData.Targets.Add(selectedAlias.name);
            }
            else
            {
                card.MechanicData.Targets[0] = selectedAlias.name;
            }

            int faceIndex = _currentDiceTab - 1;
            if (faceIndex >= 0 && (_currentMask & (1 << faceIndex)) == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    if ((_currentMask & (1 << i)) != 0)
                    {
                        _currentDiceTab = i + 1;
                        break;
                    }
                }
            }

            updateState();
        });

        CreateDropdownRow(parent, "Hat Source Side:", hatSideOptions, initialHatIndex, (val) =>
        {
            if (val == 0)
            {
                if (card.MechanicData.Targets.Count > 1)
                {
                    card.MechanicData.Targets.RemoveAt(1);
                }
            }
            else
            {
                var selectedSingleAlias = singleFaceAliases[val - 1];
                if (card.MechanicData.Targets.Count == 0)
                {
                    card.MechanicData.Targets.Add("left");
                }

                if (card.MechanicData.Targets.Count > 1)
                {
                    card.MechanicData.Targets[1] = selectedSingleAlias.name;
                }
                else
                {
                    card.MechanicData.Targets.Add(selectedSingleAlias.name);
                }
            }

            updateState();
        });
    }
}

public class EquippableNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Equippable Item Appearance";
    public override bool IsEntity => true;
    public override ItemNodeType NodeType => ItemNodeType.Equippable;
    public override Color GetColor() => new Color(0.6f, 0.5f, 0.1f); // Gold
    private static Material _cachedShaderMaterial;

    // Shader data buffers for the preview material
    private static float[] _opTypes = new float[16];
    private static Vector4[] _opColorTargets = new Vector4[16];
    private static Vector4[] _opColorReplaces = new Vector4[16];
    private static Vector4[] _opParams = new Vector4[16];

    public override string GetTitle(EntityCard card) =>
        string.IsNullOrEmpty(card.RootData.entityName) ? "[Equippable]" : $"[Equippable] {card.RootData.entityName}";

    /*
    public override string Compile(EntityCard card)
    {
        if (card?.RootData == null) return string.Empty;

        // 1. Dynamically evaluate the visual children inside this Equippable's drop zone
        string compiledChildren = StringAuthoringUIManager.CompileZone(card.PayloadPort?.Entrants.Cast<EntityCard>());

        string baseExpr = "Void";
        string baseItemName = "Void";

        if (!string.IsNullOrWhiteSpace(compiledChildren))
        {
            baseExpr = compiledChildren;

            string firstToken = baseExpr.Split(new char[] { '.', '#', '(', ')', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            baseItemName = firstToken ?? "Custom";
        }

        bool hasClearModifiers = card.RootData.ClearDescription || card.RootData.ClearIcon;

        // 2. Wrap the Base Expression with modifiers if present
        if (hasClearModifiers)
        {
            string descMod = card.RootData.ClearDescription ? "#cleardesc" : "";
            string iconMod = card.RootData.ClearIcon ? "#clearicon" : "";
            baseExpr = $"({baseExpr}{descMod}{iconMod})";
        }

        List<string> parts = new List<string> { baseExpr };

        // 3. Handle Image Override and Draw Instructions
        if (!string.IsNullOrEmpty(card.RootData.imageOverride))
        {
            string imgName = card.RootData.imageOverride.Trim();
            bool isBase = IsBaseItem(imgName);
            bool startsWithIte = imgName.StartsWith("ite", StringComparison.OrdinalIgnoreCase);

            if (imgName.StartsWith("("))
            {
                // Fully custom injected rect/draw bracket string
                parts.Add($"img.{imgName}");

                if (card.RootData.HsvShift.HasValue)
                {
                    var hsv = card.RootData.HsvShift.Value;
                    parts.Add($"hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}");
                }
            }
            else if (isBase || startsWithIte)
            {
                // Standard internal game items
                string formattedImgName = isBase ? GetBaseItemName(imgName) : imgName;
                parts.Add($"img.{formattedImgName}");

                if (card.RootData.HsvShift.HasValue)
                {
                    var hsv = card.RootData.HsvShift.Value;
                    parts.Add($"hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}");
                }
            }
            else
            {
                // Custom drawn sprites (facades, bas16, etc.)
                string drawOffset = ":-1:-1";

                if (baseItemName.Equals("Void", StringComparison.OrdinalIgnoreCase))
                {
                    // Clean format for Void bases
                    parts.Add($"draw.{imgName}{drawOffset}");

                    if (card.RootData.HsvShift.HasValue)
                    {
                        var hsv = card.RootData.HsvShift.Value;
                        parts.Add($"hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}");
                    }
                }
                else
                {
                    // Nested format for Non-Void bases: .img.void.draw.(void.img.bas16.hsv.X:X:X):-1:-1
                    if (card.RootData.HsvShift.HasValue)
                    {
                        var hsv = card.RootData.HsvShift.Value;
                        parts.Add($"img.void.draw.(void.img.{imgName}.hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}){drawOffset}");
                    }
                    else
                    {
                        parts.Add($"img.void.draw.{imgName}{drawOffset}");
                    }
                }
            }
        }
        else if (card.RootData.HsvShift.HasValue)
        {
            // HSV shift with no image override
            var hsv = card.RootData.HsvShift.Value;
            parts.Add($"hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}");
        }

        // 4. Append remaining standard fields as sibling dots
        if (card.RootData.Tier.HasValue)
        {
            parts.Add($"tier.{card.RootData.Tier.Value}");
        }

        if (!string.IsNullOrEmpty(card.RootData.DocumentedDescription))
        {
            parts.Add($"doc.{card.RootData.DocumentedDescription}");
        }

        if (!string.IsNullOrEmpty(card.RootData.entityName))
        {
            parts.Add($"n.{card.RootData.entityName}");
        }

        return string.Join(".", parts);
    }
    */
    private bool IsBaseItem(string imageName)
    {
        if (string.IsNullOrEmpty(imageName)) return false;

        string normalized = imageName.Replace(" ", "").ToLower();
        foreach (var name in Enum.GetNames(typeof(BaseItems)))
        {
            if (name.ToLower() == normalized) return true;
        }
        return false;
    }
    private string GetBaseItemName(string imageName)
    {
        string normalized = imageName.Replace(" ", "").ToLower();
        foreach (var name in Enum.GetNames(typeof(BaseItems)))
        {
            if (name.ToLower() == normalized)
            {
                // Inserts a space before capital letters (except the first letter) to match expected formatting
                return System.Text.RegularExpressions.Regex.Replace(name, @"(\B[A-Z])", " $1");
            }
        }
        return imageName;
    }

    private void MoveVisual(ItemData data, int index, int dir, Action onComplete)
    {
        int nIdx = index + dir;
        if (nIdx >= 0 && nIdx < data.visuals.Count)
        {
            var item = data.visuals[index];
            data.visuals.RemoveAt(index);
            data.visuals.Insert(nIdx, item);
            onComplete();
        }
    }
    private void OpenColorPicker(Color initialColor, Action<Color> onColorChanged)
    {
        var cp = FullScreenUIGenerator.Instance?.colorPicker;
        if (cp == null) return;
        cp.onColorChange.RemoveAllListeners();
        cp.gameObject.SetActive(true);
        cp.SetColor(initialColor);
        cp.onColorChange.AddListener(new UnityEngine.Events.UnityAction<Color>(onColorChanged));
    }
    private void OpenFacadeModal(string currentValue, Action<string, Sprite> onSelectionMade)
    {
        var iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = sprite.name.Split('_');
                    if (p.Length > 1 && int.TryParse(p[1], out int id) && id > 187)
                        return IconPickerModal.GetCleanLeafName(sprite.name);
                }
                return sprite.name;
            },
            GetTooltip = (index, sprite) => sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');
                    string facadeStr;

                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        string prefix = parts[0].ToLower();
                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31) facadeStr = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27) facadeStr = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17) facadeStr = $"bas{248 + parsedId}";
                        else facadeStr = $"{parts[0]}{parts[1]}";
                    }
                    else
                    {
                        facadeStr = filename;
                    }

                    onSelectionMade?.Invoke(facadeStr, sprite);
                }
            }
        };

        iconPicker.OpenModal(config);
    }

    // ==========================================================
    // HELPER METHODS FOR FACADE PICKING
    // ==========================================================
    /*
    private void OpenFacadeModal(EntityCard card, ItemUI ui, GridReferences refs)
    {
        var iconPicker = UnityEngine.Object.FindObjectOfType<IconPickerModal>(true);
        if (iconPicker == null) return;

        IconPickerConfig config = new IconPickerConfig
        {
            Sprites = EntityUIHelpers.AllActionSprites,
            IsValid = (index, sprite) => EntityUIHelpers.IsSpriteValid(sprite),
            GetSearchName = (index, sprite) =>
            {
                if (sprite.name.StartsWith("bas_", System.StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = sprite.name.Split('_');
                    if (p.Length > 1 && int.TryParse(p[1], out int id) && id > 187)
                        return IconPickerModal.GetCleanLeafName(sprite.name);
                }
                return sprite.name;
            },
            GetTooltip = (index, sprite) => sprite.name,
            OnSelectionMade = (index, sprite) =>
            {
                if (sprite != null)
                {
                    string filename = sprite.name;
                    string[] parts = filename.Split('_');
                    string facadeStr;

                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        string prefix = parts[0].ToLower();
                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31) facadeStr = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27) facadeStr = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17) facadeStr = $"bas{248 + parsedId}";
                        else facadeStr = $"{parts[0]}{parts[1]}";
                    }
                    else
                    {
                        facadeStr = filename;
                    }

                    card.RootData.imageOverride = facadeStr;

                    if (refs != null && refs.Inputs.TryGetValue("ImgRef", out var input))
                        input.SetTextWithoutNotify(facadeStr);

                    if (refs != null && refs.Buttons.TryGetValue("FacBtn", out var btn))
                        SetButtonIcon(btn, sprite, card.RootData);

                    ui.AutoCompile();
                }
            }
        };

        iconPicker.OpenModal(config);
    }
    */
    private Image GetButtonIconImage(Button btn)
    {
        if (btn == null)
        {
            Debug.LogWarning("[EquipInspectorDebug] GetButtonIconImage failed: Button parameter is NULL.");
            return null;
        }

        // Traverse UP the UI hierarchy to find the root cell container, 
        // ensuring we find HSVButtonIcon even if it is on a parent/sibling GameObject
        Transform current = btn.transform;
        while (current != null)
        {
            HSVButtonIcon hsvIcon = current.GetComponent<HSVButtonIcon>();
            if (hsvIcon != null)
            {
                if (hsvIcon.icon != null)
                {
                    Debug.Log($"[EquipInspectorDebug] Successfully located icon '{hsvIcon.icon.name}' via HSVButtonIcon on '{current.name}'");
                    return hsvIcon.icon;
                }
                else
                {
                    Debug.LogWarning($"[EquipInspectorDebug] Found HSVButtonIcon on '{current.name}', but its 'icon' field is UNASSIGNED in the inspector!");
                }
            }
            current = current.parent;
        }

        // Fallbacks
        var imgBtn = btn.GetComponent<ImageButton>();
        if (imgBtn != null && imgBtn.image != null)
        {
            Debug.Log($"[EquipInspectorDebug] HSVButtonIcon not found. Falling back to ImageButton: '{imgBtn.image.name}'");
            return imgBtn.image;
        }

        Transform iconTransform = btn.transform.Find("Icon");
        if (iconTransform != null)
        {
            var iconImg = iconTransform.GetComponent<Image>();
            Debug.Log($"[EquipInspectorDebug] HSVButtonIcon not found. Falling back to 'Icon' child transform: '{iconImg.name}'");
            return iconImg;
        }

        Debug.Log($"[EquipInspectorDebug] HSVButtonIcon not found. Falling back to button's background image: '{btn.image?.name ?? "null"}'");
        return btn.image;
    }
    private void ApplyMaterialAdjustments(Image img, ItemData data)
    {
        if (img == null || data == null) return;

        string uniqueInstanceName = "HSV_Instance_" + img.GetInstanceID();

        if (img.material == null || img.material == img.defaultMaterial)
        {
            Debug.LogError("NO SHADER ASSIGNED!", img);
        }
        else if (img.material.name != uniqueInstanceName)
        {
            img.material = UnityEngine.Object.Instantiate(img.material);
            img.material.name = uniqueInstanceName;
        }

        int h = data.h;
        int s = data.s;
        int v = data.v;

        Material baseMat = img.material;
        if (baseMat != null)
        {
            if (baseMat.HasProperty("_Hue")) baseMat.SetFloat("_Hue", h);
            if (baseMat.HasProperty("_Saturation")) baseMat.SetFloat("_Saturation", s);
            if (baseMat.HasProperty("_Value")) baseMat.SetFloat("_Value", v);

            // Apply PHue
            if (data.phue != null)
            {
                if (baseMat.HasProperty("_PColor")) baseMat.SetColor("_PColor", data.phue.colorStart);
                if (baseMat.HasProperty("_PReplaceColor")) baseMat.SetColor("_PReplaceColor", data.phue.colorDestination);
                if (baseMat.HasProperty("_PRange")) baseMat.SetFloat("_PRange", data.phue.colorRange);
            }

            // Apply THue
            if (data.thue != null)
            {
                if (baseMat.HasProperty("_THueColor")) baseMat.SetColor("_THueColor", data.thue.colorHex);
                if (baseMat.HasProperty("_THueRange")) baseMat.SetFloat("_THueRange", data.thue.colorRange);
                if (baseMat.HasProperty("_THueShift")) baseMat.SetFloat("_THueShift", data.thue.colorOffset);
            }
        }

        Material renderMat = img.materialForRendering;
        if (renderMat != null && renderMat != baseMat)
        {
            if (renderMat.HasProperty("_Hue")) renderMat.SetFloat("_Hue", h);
            if (renderMat.HasProperty("_Saturation")) renderMat.SetFloat("_Saturation", s);
            if (renderMat.HasProperty("_Value")) renderMat.SetFloat("_Value", v);

            // Apply PHue
            if (data.phue != null)
            {
                if (renderMat.HasProperty("_PColor")) renderMat.SetColor("_PColor", data.phue.colorStart);
                if (renderMat.HasProperty("_PReplaceColor")) renderMat.SetColor("_PReplaceColor", data.phue.colorDestination);
                if (renderMat.HasProperty("_PRange")) renderMat.SetFloat("_PRange", data.phue.colorRange);
            }

            // Apply THue
            if (data.thue != null)
            {
                if (renderMat.HasProperty("_THueColor")) renderMat.SetColor("_THueColor", data.thue.colorHex);
                if (renderMat.HasProperty("_THueRange")) renderMat.SetFloat("_THueRange", data.thue.colorRange);
                if (renderMat.HasProperty("_THueShift")) renderMat.SetFloat("_THueShift", data.thue.colorOffset);
            }
        }

        // 4. Force immediate Canvas redraw
        img.SetMaterialDirty();
    }
    private void RefreshButtonMaterial(GridReferences refs, ItemData data)
    {
        if (refs == null)
        {
            Debug.LogWarning("[EquipInspectorDebug] RefreshButtonMaterial aborted: GridReferences parameter is NULL.");
            return;
        }

        if (refs.Buttons.TryGetValue("FacBtn", out var facBtn))
        {
            Debug.Log("[EquipInspectorDebug] 'FacBtn' button successfully retrieved from GridReferences.");
            Image targetImg = GetButtonIconImage(facBtn);
            if (targetImg != null)
            {
                ApplyMaterialAdjustments(targetImg, data);
            }
        }
        else
        {
            Debug.LogWarning("[EquipInspectorDebug] 'FacBtn' key was NOT found in GridReferences.Buttons!");
        }
    }
    private void SetButtonIcon(Button btn, Sprite sprite, ItemData data)
    {
        if (btn == null) return;

        Image targetImg = GetButtonIconImage(btn);
        if (targetImg != null)
        {
            targetImg.sprite = sprite;
            targetImg.gameObject.SetActive(sprite != null);

            // Immediately apply current shader values to the newly assigned sprite
            ApplyMaterialAdjustments(targetImg, data);
        }
    }

    private void RebuildInspectorUI(ItemUI ui, EntityCard card)
    {
        foreach (Transform child in ui.InspectorContent) UnityEngine.Object.Destroy(child.gameObject);
        DrawInspector(ui, card);
    }
    private void PopulateVisualInputs(GridReferences currentRefs, ItemData data)
    {
        for (int i = 0; i < data.visuals.Count; i++)
        {
            var vis = data.visuals[i];
            if (vis.Type == VisualType.P && currentRefs.Sliders.TryGetValue($"VisPhueRange_{i}", out var sr)) sr.SetValueWithoutNotify(vis.p?.colorRange ?? 0);
            if (vis.Type == VisualType.THue && currentRefs.Sliders.TryGetValue($"VisThueRange_{i}", out var tr)) tr.SetValueWithoutNotify(vis.thue?.colorRange ?? 0);
            if (vis.Type == VisualType.THue && currentRefs.Sliders.TryGetValue($"VisThueOffset_{i}", out var to)) to.SetValueWithoutNotify(vis.thue?.colorOffset ?? 0);
            if (vis.Type == VisualType.HSV)
            {
                if (currentRefs.Sliders.TryGetValue($"VisHsvH_{i}", out var h)) h.SetValueWithoutNotify(vis.h);
                if (currentRefs.Sliders.TryGetValue($"VisHsvS_{i}", out var s)) s.SetValueWithoutNotify(vis.s);
                if (currentRefs.Sliders.TryGetValue($"VisHsvV_{i}", out var v)) v.SetValueWithoutNotify(vis.v);
            }
            if (vis.Type == VisualType.Hue && currentRefs.Sliders.TryGetValue($"VisHue_{i}", out var hu)) hu.SetValueWithoutNotify(vis.hue);

            if (vis.Type == VisualType.Draw)
            {
                if (currentRefs.Inputs.TryGetValue($"VisDrawStr_{i}", out var ds)) ds.SetTextWithoutNotify(vis.RawValue);
                if (currentRefs.Inputs.TryGetValue($"VisDrawX_{i}", out var dx)) dx.SetTextWithoutNotify(vis.x.ToString());
                if (currentRefs.Inputs.TryGetValue($"VisDrawY_{i}", out var dy)) dy.SetTextWithoutNotify(vis.y.ToString());
                if (currentRefs.Buttons.TryGetValue($"VisDrawBtn_{i}", out var btn)) SetButtonIcon(btn, SpriteCacheHelper.GetFacadeSprite(vis.RawValue), data);
            }
            if (vis.Type == VisualType.Rect && currentRefs.Inputs.TryGetValue($"VisStr_{i}", out var sIn))
            {
                sIn.SetTextWithoutNotify(vis.RawValue);
            }

            // FIX: Border Color Preview natively tinting the button
            if (vis.Type == VisualType.B && currentRefs.Buttons.TryGetValue($"VisBorderBtn_{i}", out var bBtn))
            {
                if (bBtn.image != null)
                {
                    Color c = Color.white;
                    if (!string.IsNullOrEmpty(vis.RawValue) && ColorUtility.TryParseHtmlString("#" + vis.RawValue, out var parsed)) c = parsed;
                    bBtn.image.color = c;
                }
            }
        }
    }
    private void BuildVisualsLayout(List<GridRowSpec> layout, ItemData data, ItemUI ui, Func<GridReferences> getRefs, Action saveAndRebuild)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Visual Modifier:", 0.40f),
            GridCellSpec.CreateFilteredDropdown("AddVisualDrop", "Select...", 0.60f,
                new string[] { "Select...", "P-Hue Swap", "T-Hue Color", "Global HSV", "Global Hue", "Draw Overlay", "Rect Overlay", "Border Color" },
                (idx) => {
                    var type = idx switch
                    {
                        1 => VisualType.P,
                        2 => VisualType.THue,
                        3 => VisualType.HSV,
                        4 => VisualType.Hue,
                        5 => VisualType.Draw,
                        6 => VisualType.Rect,
                        7 => VisualType.B,
                        _ => (VisualType?)null
                    };

                    if (type.HasValue)
                    {
                        var newVis = new VisualModifier { Type = type.Value };
                        if (type == VisualType.P) newVis.p = new Phue { colorRange = 1 };
                        else if (type == VisualType.THue) newVis.thue = new Thue { colorRange = 1 };
                        else if (type == VisualType.HSV) newVis.v = 1;
                        else if (type == VisualType.Draw) { newVis.RawValue = "None"; newVis.x = -1; newVis.y = -1; }
                        else if (type == VisualType.B) { newVis.RawValue = "fff"; }

                        data.visuals.Add(newVis);
                        saveAndRebuild();
                    }
                })
        ));

        for (int i = 0; i < data.visuals.Count; i++)
        {
            int index = i;
            var vis = data.visuals[index];

            // Use the lazy Func evaluator to ensure we always have the active layout instances
            Action updateState = () => { ui.AutoCompile(); RefreshButtonMaterial(getRefs(), data); };

            string upText = index == 0 ? "-" : "▲";
            string downText = index == data.visuals.Count - 1 ? "-" : "▼";
            string titleName = vis.Type switch
            {
                VisualType.P => "P-Hue Swap",
                VisualType.THue => "T-Hue Range Shift",
                VisualType.HSV => "Global HSV",
                VisualType.Hue => "Global Hue",
                VisualType.Draw => "Draw Overlay",
                VisualType.Rect => "Rect Overlay",
                VisualType.B => "Border Color",
                _ => vis.Type.ToString()
            };

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"<color=#aaaaaa>-- {titleName.ToUpper()} --</color>", 0.50f),
                GridCellSpec.CreateButton($"VisUp_{index}", upText, 0.15f, () => { MoveVisual(data, index, -1, saveAndRebuild); }),
                GridCellSpec.CreateButton($"VisDown_{index}", downText, 0.15f, () => { MoveVisual(data, index, 1, saveAndRebuild); }),
                GridCellSpec.CreateButton($"VisDel_{index}", "<color=red>[X]</color>", 0.20f, () => { data.visuals.RemoveAt(index); saveAndRebuild(); })
            ));

            // [ ... Skipping unmodified VisualType.P, THue, HSV, Hue layout code for brevity ... ] 
            if (vis.Type == VisualType.P)
            {
                if (vis.p == null) vis.p = new Phue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Colors:", 0.30f),
                    GridCellSpec.CreateButton($"VisPhueStartBtn_{index}", "Target", 0.35f, () => OpenColorPicker(vis.p.colorStart, c => { vis.p.colorStart = c; updateState(); })),
                    GridCellSpec.CreateButton($"VisPhueDestBtn_{index}", "Replace", 0.35f, () => OpenColorPicker(vis.p.colorDestination, c => { vis.p.colorDestination = c; updateState(); }))
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Range:", 0.30f),
                    GridCellSpec.CreateSlider($"VisPhueRange_{index}", 0, 99, true, 0.70f, v => { vis.p.colorRange = Mathf.RoundToInt(v); updateState(); })
                ));
            }
            else if (vis.Type == VisualType.THue)
            {
                if (vis.thue == null) vis.thue = new Thue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Target Color:", 0.35f),
                    GridCellSpec.CreateButton($"VisThueColorBtn_{index}", "Pick Color", 0.65f, () => OpenColorPicker(vis.thue.colorHex, c => { vis.thue.colorHex = c; updateState(); }))
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Rng:", 0.15f),
                    GridCellSpec.CreateSlider($"VisThueRange_{index}", 0, 99, true, 0.35f, v => { vis.thue.colorRange = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateLabel("Shft:", 0.15f),
                    GridCellSpec.CreateSlider($"VisThueOffset_{index}", -99, 99, true, 0.35f, v => { vis.thue.colorOffset = Mathf.RoundToInt(v); updateState(); })
                ));
            }
            else if (vis.Type == VisualType.HSV)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvH_{index}", -99, 99, true, 0.50f, v => { vis.h = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvHIn_{index}", vis.h.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.h = v; updateState(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sat:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvS_{index}", -99, 99, true, 0.50f, v => { vis.s = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvSIn_{index}", vis.s.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.s = v; updateState(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Val:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvV_{index}", -99, 99, true, 0.50f, v => { vis.v = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvVIn_{index}", vis.v.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.v = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Hue)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHue_{index}", -99, 99, true, 0.50f, v => { vis.hue = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHueIn_{index}", vis.hue.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.hue = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Draw)
            {
                // FIX: Pull currentRefs safely from the func evaluator so the callback can find the generated UI
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sprite Ref:", 0.25f),
                    GridCellSpec.CreateDiceButton($"VisDrawBtn_{index}", "F", 0.15f, () => OpenFacadeModal(vis.RawValue, (facStr, spr) => {
                        vis.RawValue = facStr;
                        var refs = getRefs();
                        if (refs != null && refs.Inputs.TryGetValue($"VisDrawStr_{index}", out var inp)) inp.SetTextWithoutNotify(facStr);
                        if (refs != null && refs.Buttons.TryGetValue($"VisDrawBtn_{index}", out var btn)) SetButtonIcon(btn, spr, data);
                        updateState();
                    })),
                    GridCellSpec.CreateInput($"VisDrawStr_{index}", vis.RawValue, 0.60f, val => { vis.RawValue = val; updateState(); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Offset X:", 0.25f),
                    GridCellSpec.CreateInput($"VisDrawX_{index}", vis.x.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.x = v; updateState(); } }),
                    GridCellSpec.CreateLabel("Y:", 0.25f),
                    GridCellSpec.CreateInput($"VisDrawY_{index}", vis.y.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.y = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Rect)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Value:", 0.25f),
                    GridCellSpec.CreateInput($"VisStr_{index}", vis.RawValue, 0.75f, val => { vis.RawValue = val; updateState(); })
                ));
            }
            else if (vis.Type == VisualType.B)
            {
                // FIX: Uses unity color picker and dynamically syncs the button background
                Color initialBorderCol = Color.white;
                if (!string.IsNullOrEmpty(vis.RawValue) && ColorUtility.TryParseHtmlString("#" + vis.RawValue, out var parsedCol))
                {
                    initialBorderCol = parsedCol;
                }

                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Border Hex:", 0.25f),
                    GridCellSpec.CreateButton($"VisBorderBtn_{index}", "Pick Color", 0.75f, () => {
                        OpenColorPicker(initialBorderCol, c => {
                            vis.RawValue = ColorUtility.ToHtmlStringRGB(c).ToLower();
                            var refs = getRefs();
                            if (refs != null && refs.Buttons.TryGetValue($"VisBorderBtn_{index}", out var bBtn) && bBtn.image != null)
                            {
                                bBtn.image.color = c;
                            }
                            updateState();
                        });
                    })
                ));
            }
        }
    }

    // ==========================================================
    // SRP PIPELINE HELPER METHODS
    // ==========================================================

    private void EnsureVisualsInitialized(ItemData data)
    {
        if (data.visuals == null) data.visuals = new List<VisualModifier>();
    }
    private RectTransform CreateGridContainer(Transform parent, out LayoutElement layoutElem)
    {
        GameObject containerObj = new GameObject("EquipGridContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(parent, false);
        layoutElem = containerObj.GetComponent<LayoutElement>();
        return containerObj.GetComponent<RectTransform>();
    }
    private void BuildCoreIdentifierRows(List<GridRowSpec> layout, ItemData data, ItemUI ui)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Item Name:", 0.25f),
            GridCellSpec.CreateInput("Name", data.entityName, 0.75f, (val) => { data.entityName = val; ui.RefreshSidebar(); ui.AutoCompile(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Tier:", 0.25f),
            GridCellSpec.CreateInput("Tier", data.Tier?.ToString() ?? "", 0.75f, (val) => { if (int.TryParse(val, out int t)) data.Tier = t; else data.Tier = null; ui.AutoCompile(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc (Desc):", 0.25f),
            GridCellSpec.CreateInput("Doc", data.doc, 0.75f, (val) => { data.doc = val.SanitizeRichInput(); ui.AutoCompile(); })
        ));
    }
    private void BuildBaseImageRow(List<GridRowSpec> layout, ItemData data, ItemUI ui, Func<GridReferences> getRefs)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Base ImgRef:", 0.28f),
            GridCellSpec.CreateDiceButton("FacBtn", "F", 0.12f, () => OpenFacadeModal(data.imageOverride, (facadeStr, spr) => {
                data.imageOverride = facadeStr;
                var refs = getRefs();
                if (refs != null && refs.Inputs.TryGetValue("ImgRef", out var inp)) inp.SetTextWithoutNotify(facadeStr);
                if (refs != null && refs.Buttons.TryGetValue("FacBtn", out var btn)) SetButtonIcon(btn, spr, data);
                ui.AutoCompile();
            })),
            GridCellSpec.CreateInput("ImgRef", data.imageOverride, 0.60f, (val) => { data.imageOverride = val; ui.AutoCompile(); })
        ));
    }
    private void BuildToggleRows(List<GridRowSpec> layout, ItemData data, ItemUI ui)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateToggle("ClearDesc", "Suppress Doc", 0.5f, (val) => { data.ClearDescription = val; ui.AutoCompile(); }),
            GridCellSpec.CreateToggle("ClearIcon", "Suppress Icon", 0.5f, (val) => { data.ClearIcon = val; ui.AutoCompile(); })
        ));
    }
    private void PopulateBaseInputs(GridReferences currentRefs, ItemData data)
    {
        if (currentRefs == null) return;
        if (currentRefs.Inputs.TryGetValue("Name", out var nameIn)) nameIn.SetTextWithoutNotify(data.entityName);
        if (currentRefs.Inputs.TryGetValue("Tier", out var tierIn)) tierIn.SetTextWithoutNotify(data.Tier?.ToString() ?? "");
        if (currentRefs.Inputs.TryGetValue("Doc", out var docIn)) docIn.SetTextWithoutNotify(data.doc);
        if (currentRefs.Inputs.TryGetValue("ImgRef", out var imgIn)) imgIn.SetTextWithoutNotify(data.imageOverride);
    }
    private void PopulateTogglesAndIcons(GridReferences currentRefs, ItemData data)
    {
        if (currentRefs == null) return;
        if (currentRefs.Toggles.TryGetValue("ClearDesc", out var descToggle)) descToggle.SetIsOnWithoutNotify(data.ClearDescription);
        if (currentRefs.Toggles.TryGetValue("ClearIcon", out var iconToggle)) iconToggle.SetIsOnWithoutNotify(data.ClearIcon);
        if (currentRefs.Buttons.TryGetValue("FacBtn", out var facBtn)) SetButtonIcon(facBtn, EntityUIHelpers.GetFacadeSprite(data.imageOverride), data);
    }

    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null || card?.RootData == null) return;

        EnsureVisualsInitialized(card.RootData);

        // --- 1. BUILD PREVIEW UI SECTION ---
        BuildPreviewSection(ui.InspectorContent, out Image previewIcon);

        // --- 2. BUILD SETTINGS CONTAINER ---
        RectTransform containerRect = CreateGridContainer(ui.InspectorContent, out LayoutElement layoutElem);

        var layout = new List<GridRowSpec>();
        GridReferences currentRefs = null;
        Func<GridReferences> getRefs = () => currentRefs;

        // Unified callback to dynamically sync both the grid UI materials and the big Preview Sprite
        Action updateState = () => {
            ui.AutoCompile();
            RefreshButtonMaterial(getRefs(), card.RootData);
            UpdatePreviewVisuals(previewIcon, card.RootData);
        };

        Action saveAndRebuild = () => {
            ui.AutoCompile();
            ui.RefreshSidebar();
            RebuildInspectorUI(ui, card);
        };

        // --- 3. BUILD LAYOUT SPECS PIPELINE ---
        BuildCoreIdentifierRows(layout, card.RootData, ui);
        BuildBaseImageRow(layout, card.RootData, ui, getRefs, updateState);

        BuildVisualsLayout(layout, card.RootData, getRefs, saveAndRebuild, updateState);

        BuildToggleRows(layout, card.RootData, ui);

        // --- 4. REBUILD PHYSICAL GRID ---
        currentRefs = fsg.RebuildGrid(containerRect, layout, false);

        // --- 5. POST-RENDER POPULATION PIPELINE ---
        PopulateBaseInputs(currentRefs, card.RootData);
        PopulateTogglesAndIcons(currentRefs, card.RootData);
        PopulateVisualInputs(currentRefs, card.RootData);

        // Push initial state to the preview image
        UpdatePreviewVisuals(previewIcon, card.RootData);

        layoutElem.minHeight = currentRefs.TotalHeight + (fsg.rowHeight * 2);
        layoutElem.flexibleHeight = 0;
    }

    // ==========================================================
    // SRP PIPELINE HELPER METHODS
    // ==========================================================

    private void BuildPreviewSection(Transform parent, out Image previewIcon)
    {
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform), typeof(LayoutElement));
        previewContainer.transform.SetParent(parent, false);

        var le = previewContainer.GetComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 120f;
        le.flexibleHeight = 0f;

        GameObject imgObj = new GameObject("PreviewIcon", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(previewContainer.transform, false);

        var rt = imgObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.anchoredPosition = Vector2.zero;

        previewIcon = imgObj.GetComponent<Image>();
        previewIcon.preserveAspect = true;

        // Load the array-based multi-op shader material from Resources
        Material mat = Resources.Load<Material>("UI_Custom_HSV_Adjustment_Array");
        if (mat == null) mat = Resources.Load<Material>("Materials/UI_Custom_HSV_Adjustment_Array");

        if (mat != null)
        {
            previewIcon.material = UnityEngine.Object.Instantiate(mat);
            previewIcon.material.name = "ItemPreviewMat_" + previewIcon.GetInstanceID();
        }
        else
        {
            Debug.LogError("COULD NOT FIND Resources/UI_Custom_HSV_Adjustment_Array!!! Did you rename or move it?");
        }
    }
    public void UpdatePreviewVisuals(Image preview, ItemData data)
    {
        if (preview == null || data == null) return;
        string baseRef = data.imageOverride;
        bool isBaseEmpty = string.IsNullOrWhiteSpace(baseRef) ||
                           baseRef.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                           baseRef.Equals("blank", StringComparison.OrdinalIgnoreCase);
        Sprite resolvedSprite = null;
        if (!isBaseEmpty)
        {
            resolvedSprite = SpriteCacheHelper.GetFacadeSprite(baseRef);
        }
        if (resolvedSprite == null && data.visuals != null)
        {
            var firstDraw = data.visuals.FirstOrDefault(v =>
                v.Type == VisualType.Draw &&
                !string.IsNullOrWhiteSpace(v.RawValue) &&
                !v.RawValue.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !v.RawValue.Equals("blank", StringComparison.OrdinalIgnoreCase));
            if (firstDraw != null)
            {
                resolvedSprite = SpriteCacheHelper.GetFacadeSprite(firstDraw.RawValue);
            }
        }
        if (resolvedSprite == null)
        {
            resolvedSprite = SpriteCacheHelper.GetFacadeSprite("SpellPlaceholder");
        }
        preview.sprite = resolvedSprite;
        preview.color = Color.white; // Ensure base tint isn't interfering with the shader

        // 2. Calculate and push array shader data
        if (preview.material == null) return;

        int count = 0;
        if (data.visuals != null)
        {
            foreach (var v in data.visuals)
            {
                if (count >= 16) break; // Shader max ops limit

                if (v.Type == VisualType.P && v.p != null)
                {
                    _opTypes[count] = 1; // PSwap
                    _opColorTargets[count] = v.p.colorStart;
                    _opColorReplaces[count] = v.p.colorDestination;
                    _opParams[count] = new Vector4(v.p.colorRange, 1.46f, 0, 0); // 1.46 standard multiplier
                    count++;
                }
                else if (v.Type == VisualType.THue && v.thue != null)
                {
                    _opTypes[count] = 2; // THue
                    _opColorTargets[count] = v.thue.colorHex;
                    _opColorReplaces[count] = Vector4.zero;
                    _opParams[count] = new Vector4(v.thue.colorRange, 1.46f, v.thue.colorOffset, 0);
                    count++;
                }
                else if (v.Type == VisualType.HSV)
                {
                    _opTypes[count] = 3; // Global HSV
                    _opColorTargets[count] = Vector4.zero;
                    _opColorReplaces[count] = new Vector4(v.h, v.s, v.v, 0);
                    _opParams[count] = Vector4.zero;
                    count++;
                }
                else if (v.Type == VisualType.Hue)
                {
                    _opTypes[count] = 3; // Global HSV (Hue only)
                    _opColorTargets[count] = Vector4.zero;
                    _opColorReplaces[count] = new Vector4(v.hue, 0, 0, 0);
                    _opParams[count] = Vector4.zero;
                    count++;
                }
            }
        }

        // Fill remaining empty slots with 0 to prevent data bleed from previous frames
        for (int i = count; i < 16; i++)
        {
            _opTypes[i] = 0;
            _opColorTargets[i] = Vector4.zero;
            _opColorReplaces[i] = Vector4.zero;
            _opParams[i] = Vector4.zero;
        }

        // CRITICAL FIX: Push arrays explicitly to ALL active material instances 
        void PushToMaterial(Material m)
        {
            if (m == null) return;
            m.SetInt("_OpCount", count);
            m.SetFloatArray("_OpTypes", _opTypes);
            m.SetVectorArray("_OpColorTargets", _opColorTargets);
            m.SetVectorArray("_OpColorReplaces", _opColorReplaces);
            m.SetVectorArray("_OpParams", _opParams);
        }

        PushToMaterial(preview.material);
        if (preview.materialForRendering != null && preview.materialForRendering != preview.material)
        {
            PushToMaterial(preview.materialForRendering);
        }

        // CRITICAL FIX: Force the CanvasRenderer to immediately sync the material array state
        if (preview.canvasRenderer != null && preview.materialForRendering != null)
        {
            // Ensure the CanvasRenderer has allocated at least 1 material slot
            if (preview.canvasRenderer.materialCount == 0)
            {
                preview.canvasRenderer.materialCount = 1;
            }

            preview.canvasRenderer.SetMaterial(preview.materialForRendering, 0);
        }

        preview.SetMaterialDirty();
    }
    private void BuildBaseImageRow(List<GridRowSpec> layout, ItemData data, ItemUI ui, Func<GridReferences> getRefs, Action updateState)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Base ImgRef:", 0.28f),
            GridCellSpec.CreateDiceButton("FacBtn", "F", 0.12f, () => OpenFacadeModal(data.imageOverride, (facadeStr, spr) => {
                data.imageOverride = facadeStr;
                var refs = getRefs();
                if (refs != null && refs.Inputs.TryGetValue("ImgRef", out var inp)) inp.SetTextWithoutNotify(facadeStr);
                if (refs != null && refs.Buttons.TryGetValue("FacBtn", out var btn)) SetButtonIcon(btn, spr, data);
                updateState();
            })),
            GridCellSpec.CreateInput("ImgRef", data.imageOverride, 0.60f, (val) => { data.imageOverride = val; updateState(); })
        ));
    }
    private void BuildVisualsLayout(List<GridRowSpec> layout, ItemData data, Func<GridReferences> getRefs, Action saveAndRebuild, Action updateState)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Visual Modifier:", 0.40f),
            GridCellSpec.CreateFilteredDropdown("AddVisualDrop", "Select...", 0.60f,
                new string[] { "Select...", "P-Hue Swap", "T-Hue Color", "Global HSV", "Global Hue", "Draw Overlay", "Rect Overlay", "Border Color" },
                (idx) => {
                    var type = idx switch
                    {
                        1 => VisualType.P,
                        2 => VisualType.THue,
                        3 => VisualType.HSV,
                        4 => VisualType.Hue,
                        5 => VisualType.Draw,
                        6 => VisualType.Rect,
                        7 => VisualType.B,
                        _ => (VisualType?)null
                    };

                    if (type.HasValue)
                    {
                        var newVis = new VisualModifier { Type = type.Value };
                        if (type == VisualType.P) newVis.p = new Phue { colorRange = 1 };
                        else if (type == VisualType.THue) newVis.thue = new Thue { colorRange = 1 };
                        else if (type == VisualType.HSV) newVis.v = 1;
                        else if (type == VisualType.Draw) { newVis.RawValue = "None"; newVis.x = -1; newVis.y = -1; }
                        else if (type == VisualType.B) { newVis.RawValue = "fff"; }

                        data.visuals.Add(newVis);
                        saveAndRebuild();
                    }
                })
        ));

        for (int i = 0; i < data.visuals.Count; i++)
        {
            int index = i;
            var vis = data.visuals[index];

            string upText = index == 0 ? "-" : "▲";
            string downText = index == data.visuals.Count - 1 ? "-" : "▼";
            string titleName = vis.Type switch
            {
                VisualType.P => "P-Hue Swap",
                VisualType.THue => "T-Hue Range Shift",
                VisualType.HSV => "Global HSV",
                VisualType.Hue => "Global Hue",
                VisualType.Draw => "Draw Overlay",
                VisualType.Rect => "Rect Overlay",
                VisualType.B => "Border Color",
                _ => vis.Type.ToString()
            };

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"<color=#aaaaaa>-- {titleName.ToUpper()} --</color>", 0.50f),
                GridCellSpec.CreateButton($"VisUp_{index}", upText, 0.15f, () => { MoveVisual(data, index, -1, saveAndRebuild); }),
                GridCellSpec.CreateButton($"VisDown_{index}", downText, 0.15f, () => { MoveVisual(data, index, 1, saveAndRebuild); }),
                GridCellSpec.CreateButton($"VisDel_{index}", "<color=red>[X]</color>", 0.20f, () => { data.visuals.RemoveAt(index); saveAndRebuild(); })
            ));

            if (vis.Type == VisualType.P)
            {
                if (vis.p == null) vis.p = new Phue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Colors:", 0.30f),
                    GridCellSpec.CreateButton($"VisPhueStartBtn_{index}", "Target", 0.35f, () => OpenColorPicker(vis.p.colorStart, c => { vis.p.colorStart = c; updateState(); })),
                    GridCellSpec.CreateButton($"VisPhueDestBtn_{index}", "Replace", 0.35f, () => OpenColorPicker(vis.p.colorDestination, c => { vis.p.colorDestination = c; updateState(); }))
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Range:", 0.30f),
                    GridCellSpec.CreateSlider($"VisPhueRange_{index}", 0, 99, true, 0.70f, v => { vis.p.colorRange = Mathf.RoundToInt(v); updateState(); })
                ));
            }
            else if (vis.Type == VisualType.THue)
            {
                if (vis.thue == null) vis.thue = new Thue();
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Target Color:", 0.35f),
                    GridCellSpec.CreateButton($"VisThueColorBtn_{index}", "Pick Color", 0.65f, () => OpenColorPicker(vis.thue.colorHex, c => { vis.thue.colorHex = c; updateState(); }))
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Rng:", 0.15f),
                    GridCellSpec.CreateSlider($"VisThueRange_{index}", 0, 99, true, 0.35f, v => { vis.thue.colorRange = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateLabel("Shft:", 0.15f),
                    GridCellSpec.CreateSlider($"VisThueOffset_{index}", -99, 99, true, 0.35f, v => { vis.thue.colorOffset = Mathf.RoundToInt(v); updateState(); })
                ));
            }
            else if (vis.Type == VisualType.HSV)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvH_{index}", -99, 99, true, 0.50f, v => { vis.h = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvHIn_{index}", vis.h.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.h = v; updateState(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sat:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvS_{index}", -99, 99, true, 0.50f, v => { vis.s = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvSIn_{index}", vis.s.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.s = v; updateState(); } })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Val:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHsvV_{index}", -99, 99, true, 0.50f, v => { vis.v = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHsvVIn_{index}", vis.v.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.v = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Hue)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.25f),
                    GridCellSpec.CreateSlider($"VisHue_{index}", -99, 99, true, 0.50f, v => { vis.hue = Mathf.RoundToInt(v); updateState(); }),
                    GridCellSpec.CreateInput($"VisHueIn_{index}", vis.hue.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.hue = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Draw)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sprite Ref:", 0.25f),
                    GridCellSpec.CreateDiceButton($"VisDrawBtn_{index}", "F", 0.15f, () => OpenFacadeModal(vis.RawValue, (facStr, spr) => {
                        vis.RawValue = facStr;
                        var refs = getRefs();
                        if (refs != null && refs.Inputs.TryGetValue($"VisDrawStr_{index}", out var inp)) inp.SetTextWithoutNotify(facStr);
                        if (refs != null && refs.Buttons.TryGetValue($"VisDrawBtn_{index}", out var btn)) SetButtonIcon(btn, spr, data);
                        updateState();
                    })),
                    GridCellSpec.CreateInput($"VisDrawStr_{index}", vis.RawValue, 0.60f, val => { vis.RawValue = val; updateState(); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Offset X:", 0.25f),
                    GridCellSpec.CreateInput($"VisDrawX_{index}", vis.x.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.x = v; updateState(); } }),
                    GridCellSpec.CreateLabel("Y:", 0.25f),
                    GridCellSpec.CreateInput($"VisDrawY_{index}", vis.y.ToString(), 0.25f, val => { if (int.TryParse(val, out int v)) { vis.y = v; updateState(); } })
                ));
            }
            else if (vis.Type == VisualType.Rect)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Value:", 0.25f),
                    GridCellSpec.CreateInput($"VisStr_{index}", vis.RawValue, 0.75f, val => { vis.RawValue = val; updateState(); })
                ));
            }
            else if (vis.Type == VisualType.B)
            {
                Color initialBorderCol = Color.white;
                if (!string.IsNullOrEmpty(vis.RawValue) && ColorUtility.TryParseHtmlString("#" + vis.RawValue, out var parsedCol))
                {
                    initialBorderCol = parsedCol;
                }

                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Border Hex:", 0.25f),
                    GridCellSpec.CreateButton($"VisBorderBtn_{index}", "Pick Color", 0.75f, () => {
                        OpenColorPicker(initialBorderCol, c => {
                            vis.RawValue = ColorUtility.ToHtmlStringRGB(c).ToLower();
                            var refs = getRefs();
                            if (refs != null && refs.Buttons.TryGetValue($"VisBorderBtn_{index}", out var bBtn) && bBtn.image != null)
                            {
                                bBtn.image.color = c;
                            }
                            updateState();
                        });
                    })
                ));
            }
        }
    }
}

public class BaseItemNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Base / Ritems Pack";
    public override bool IsEntity => true;
    public override ItemNodeType NodeType => ItemNodeType.BaseItem;
    public override Color GetColor() => new Color(0.4f, 0.3f, 0.3f); // Gold

    // Cache the formatted names so we don't calculate Regex every frame
    private static string[] _formattedItemNames;
    private static string[] FormattedItemNames
    {
        get
        {
            if (_formattedItemNames == null)
            {
                string[] rawNames = Enum.GetNames(typeof(BaseItems));
                _formattedItemNames = rawNames.Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2")).ToArray();
            }
            return _formattedItemNames;
        }
    }

    // --- INTERNAL DATA STRUCTURE FOR THE UI ---
    private class BaseItemEntry
    {
        public BasePackEntryType Type = BasePackEntryType.BaseItem;
        public bool Unpack;
        public string ItemName = "Void";
        public string Part = "";
        public string NextOp = "#";
        public string Target = "none";

        // New Extended Fields
        public int Repeats = 1;
        public int Multiplier = 1;
        public bool PerTier = false;
    }

    public enum BasePackEntryType
    {
        BaseItem,
        Ritem,
        Ritemx,
        Keyword,
        TogItem
    }

    private static string[] _entryTypeOptions;
    private static string[] EntryTypeOptions
    {
        get
        {
            if (_entryTypeOptions == null)
            {
                var names = Enum.GetNames(typeof(BasePackEntryType))
                                .Select(name => Regex.Replace(name, "([a-z])([A-Z])", "$1 $2"))
                                .ToList();
                names.Insert(0, "-- Add to Pack --");
                _entryTypeOptions = names.ToArray();
            }
            return _entryTypeOptions;
        }
    }

    private static string[] GetOptionArray(BasePackEntryType type)
    {
        switch (type)
        {
            case BasePackEntryType.BaseItem: return FormattedItemNames;
            case BasePackEntryType.Keyword: return Enum.GetNames(typeof(EffectKeyword));
            case BasePackEntryType.TogItem: return ItemDomainRules.TogItems.ToArray();
            case BasePackEntryType.Ritem: return new string[] { "ritem.0" };
            case BasePackEntryType.Ritemx: return new string[] { "ritemx.0" };
            default: return new string[0];
        }
    }

    private void NormalizeOperators(List<BaseItemEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (i == entries.Count - 1)
            {
                entries[i].NextOp = ""; // Last item never has a trailing operator
            }
            else if (string.IsNullOrEmpty(entries[i].NextOp))
            {
                entries[i].NextOp = "#"; // Default fallback to prevent concatenation
            }
        }
    }

    private static string[] _targetOptions;
    private static string[] GetTargetOptions()
    {
        if (_targetOptions == null)
        {
            var aliases = DiceTargetHelper.TargetAliases.Select(alias => alias.name).ToList();
            aliases.Insert(0, "none");
            _targetOptions = aliases.ToArray();
        }
        return _targetOptions;
    }

    public override string GetTitle(EntityCard card)
    {
        string payload = string.IsNullOrWhiteSpace(card.MechanicData.PayloadString) ? "Empty Pack" : card.MechanicData.PayloadString;
        if (payload.Length > 30) payload = payload.Substring(0, 30) + "...";
        return $"[Packed] {payload}";
    }

    // ==========================================
    // INSPECTOR ORCHESTRATION
    // ==========================================

    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null) return;

        GameObject containerObj = new GameObject("BaseItemGridContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        var layoutElem = containerObj.GetComponent<LayoutElement>();

        var layout = new List<GridRowSpec>();
        List<BaseItemEntry> entries = ParsePayload(card.MechanicData.PayloadString);

        // Actions to pass down for state changes
        Action saveState = () => SaveState(card, entries, ui, false);
        Action saveAndRebuild = () => SaveState(card, entries, ui, true);

        // 1. Build Top Header
        BuildTopSelectorRow(layout, entries, saveAndRebuild);

        // 2. Build Sub-Rows Iteratively
        for (int i = 0; i < entries.Count; i++)
        {
            BuildEntryUI(layout, i, entries, saveState, saveAndRebuild);
        }

        // 3. Compile the actual physical UI
        var refs = fsg.RebuildGrid(containerObj.GetComponent<RectTransform>(), layout, false);

        // 4. Fill values safely post-instantiation
        for (int i = 0; i < entries.Count; i++)
        {
            PopulateEntryValues(refs, i, entries[i]);
        }

        layoutElem.minHeight = refs.TotalHeight + (fsg.rowHeight * 2);
        layoutElem.flexibleHeight = 0;
    }

    private void BuildTopSelectorRow(List<GridRowSpec> layout, List<BaseItemEntry> entries, Action saveAndRebuild)
    {
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Expression:", 0.35f),
            GridCellSpec.CreateFilteredDropdown("TypeSelector", "-- Select Type --", 0.65f, EntryTypeOptions, (val) =>
            {
                if (val <= 0) return;
                BasePackEntryType selectedType = (BasePackEntryType)(val - 1);

                if (entries.Count > 0) entries.Last().NextOp = "#";

                BaseItemEntry newEntry = new BaseItemEntry { Type = selectedType, NextOp = "" };
                string[] defaultOptions = GetOptionArray(selectedType);

                newEntry.ItemName = selectedType switch
                {
                    BasePackEntryType.BaseItem => FormattedItemNames[0],
                    BasePackEntryType.Ritem => "ritem.0",
                    BasePackEntryType.Ritemx => "ritemx.0",
                    BasePackEntryType.Keyword => Enum.GetNames(typeof(EffectKeyword)).FirstOrDefault() ?? "acidic",
                    BasePackEntryType.TogItem => ItemDomainRules.TogItems.FirstOrDefault() ?? "togtime",
                    _ => defaultOptions.Length > 0 ? defaultOptions[0] : "Void"
                };

                entries.Add(newEntry);
                saveAndRebuild();
            })
        ));

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel("Spacer_Top", "", 1.0f)));
    }
    private void BuildEntryUI(List<GridRowSpec> layout, int index, List<BaseItemEntry> entries, Action saveState, Action saveAndRebuild)
    {
        var entry = entries[index];

        Action onUp = () => {
            if (index > 0)
            {
                // 1. Save the exact order of operators
                var ops = entries.Select(e => e.NextOp).ToList();

                // 2. Move the item
                var itemToMove = entries[index];
                entries.RemoveAt(index);
                entries.Insert(index - 1, itemToMove);

                // 3. Restore operators to their original slots & sanitize
                for (int i = 0; i < entries.Count; i++) entries[i].NextOp = ops[i];
                NormalizeOperators(entries);

                saveAndRebuild();
            }
        };

        Action onDown = () => {
            if (index < entries.Count - 1)
            {
                var ops = entries.Select(e => e.NextOp).ToList();

                var itemToMove = entries[index];
                entries.RemoveAt(index);
                entries.Insert(index + 1, itemToMove);

                for (int i = 0; i < entries.Count; i++) entries[i].NextOp = ops[i];
                NormalizeOperators(entries);

                saveAndRebuild();
            }
        };

        Action onDelete = () => {
            entries.RemoveAt(index);
            NormalizeOperators(entries);
            saveAndRebuild();
        };

        switch (entry.Type)
        {
            case BasePackEntryType.BaseItem:
            case BasePackEntryType.Ritem:
            case BasePackEntryType.Ritemx:
                BuildBaseOrRitemRows(layout, index, entry, saveState, saveAndRebuild, onUp, onDown, onDelete);
                break;
            case BasePackEntryType.Keyword:
                BuildKeywordRow(layout, index, entry, saveState, onUp, onDown, onDelete);
                break;
            case BasePackEntryType.TogItem:
                BuildTogItemRow(layout, index, entry, saveState, onUp, onDown, onDelete);
                break;
        }

        if (index < entries.Count - 1)
        {
            BuildOperatorRow(layout, index, entry, saveAndRebuild);
        }
    }

    // --- DRY HELPER METHODS ---
    private GridCellSpec CreateTargetDropdownSpec(int index, BaseItemEntry entry, float ratio, Action saveState)
    {
        return GridCellSpec.CreateFilteredDropdown($"Target_{index}", entry.Target, ratio, GetTargetOptions(), (val) => {
            entry.Target = GetTargetOptions()[val];
            saveState();
        });
    }
    private GridRowSpec BuildControlRow(IEnumerable<GridCellSpec> contentSpecs, int index, Action onUp, Action onDown, Action onDelete)
    {
        var cells = new List<GridCellSpec>(contentSpecs)
    {
        GridCellSpec.CreateButton($"Up_{index}", "▲", 0.08f, onUp),
        GridCellSpec.CreateButton($"Dn_{index}", "▼", 0.08f, onDown),
        GridCellSpec.CreateButton($"Del_{index}", "X", 0.08f, onDelete)
    };
        return new GridRowSpec(cells.ToArray());
    }

    // --- REFACTORED ROW BUILDERS ---
    private void BuildSideDefinitionRow(List<GridRowSpec> layout, int index, BaseItemEntry entry, Action saveState, Action onUp, Action onDown, Action onDelete)
    {
        layout.Add(BuildControlRow(new[] {
        GridCellSpec.CreateLabel("Target Side:", 0.25f),
        CreateTargetDropdownSpec(index, entry, 0.51f, saveState)
    }, index, onUp, onDown, onDelete));
    }
    private void BuildBaseOrRitemRows(List<GridRowSpec> layout, int index, BaseItemEntry entry, Action saveState, Action saveAndRebuild, Action onUp, Action onDown, Action onDelete)
    {
        string[] currentOptions = GetOptionArray(entry.Type);

        GridCellSpec itemSpec = (entry.Type == BasePackEntryType.BaseItem)
            ? GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.46f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); })
            : GridCellSpec.CreateInput($"Item_{index}", entry.ItemName, 0.46f, (val) => { entry.ItemName = val; saveState(); });

        // Row 1: Target Side, Item Dropdown/Input, and Action Buttons (Up/Down/Del)
        layout.Add(BuildControlRow(new[] {
        CreateTargetDropdownSpec(index, entry, 0.30f, saveState),
        itemSpec
    }, index, onUp, onDown, onDelete));

        // Row 2: Unpack, Per-Tier, and Part
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateToggle($"Unpack_{index}", "Unpack", 0.25f, (val) => { entry.Unpack = val; saveState(); }),
            GridCellSpec.CreateToggle($"Tier_{index}", "Per-Tier", 0.25f, (val) => OnTierToggleChanged(val, entry, saveState, saveAndRebuild)),
            GridCellSpec.CreateLabel("Part:", 0.15f),
            GridCellSpec.CreateInput($"Part_{index}", entry.Part, 0.35f, (val) => { entry.Part = val; saveState(); })
        ));

        // Row 3: Repeat and Multiplier
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Repeat (xN):", 0.20f),
            GridCellSpec.CreateInput($"Rep_{index}", entry.Repeats.ToString(), 0.30f, (val) => OnRepeatsChanged(val, entry, saveState, saveAndRebuild)),
            GridCellSpec.CreateLabel("Multiplier (.m):", 0.20f),
            GridCellSpec.CreateInput($"Mult_{index}", entry.Multiplier.ToString(), 0.30f, (val) => { if (int.TryParse(val, out int v)) { entry.Multiplier = v; saveState(); } })
        ));
    }
    private void BuildKeywordRow(List<GridRowSpec> layout, int index, BaseItemEntry entry, Action saveState, Action onUp, Action onDown, Action onDelete)
    {
        string[] currentOptions = GetOptionArray(entry.Type);

        layout.Add(BuildControlRow(new[] {
        CreateTargetDropdownSpec(index, entry, 0.25f, saveState),
        GridCellSpec.CreateLabel("k.", 0.06f),
        GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.45f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); })
    }, index, onUp, onDown, onDelete));
    }
    private void BuildTogItemRow(List<GridRowSpec> layout, int index, BaseItemEntry entry, Action saveState, Action onUp, Action onDown, Action onDelete)
    {
        string[] currentOptions = GetOptionArray(entry.Type);

        layout.Add(BuildControlRow(new[] {
        CreateTargetDropdownSpec(index, entry, 0.25f, saveState),
        GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.51f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); })
    }, index, onUp, onDown, onDelete));
    }
    private void BuildOperatorRow(List<GridRowSpec> layout, int index, BaseItemEntry entry, Action saveAndRebuild)
    {
        string opLabel = NodeOperatorUtility.GetLabel(entry.NextOp);

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("", 0.38f),
            GridCellSpec.CreateButton($"Op_{index}", opLabel, 0.24f, () => {
                entry.NextOp = NodeOperatorUtility.CycleOp(entry.NextOp);
                saveAndRebuild();
            }),
            GridCellSpec.CreateLabel("", 0.38f)
        ));
    }
    private void OnRepeatsChanged(string val, BaseItemEntry entry, Action saveState, Action saveAndRebuild)
    {
        if (int.TryParse(val, out int v))
        {
            entry.Repeats = v;
            if (v > 1 && entry.PerTier)
            {
                entry.PerTier = false;
                saveAndRebuild();
            }
            else
            {
                saveState();
            }
        }
    }
    private void OnTierToggleChanged(bool val, BaseItemEntry entry, Action saveState, Action saveAndRebuild)
    {
        entry.PerTier = val;
        if (val && entry.Repeats > 1)
        {
            entry.Repeats = 1;
            saveAndRebuild();
        }
        else
        {
            saveState();
        }
    }
    private void PopulateEntryValues(GridReferences refs, int i, BaseItemEntry entry)
    {
        if (refs.Toggles.TryGetValue($"Unpack_{i}", out var tglU)) tglU.SetIsOnWithoutNotify(entry.Unpack);
        if (refs.Inputs.TryGetValue($"Part_{i}", out var inpP)) inpP.SetTextWithoutNotify(entry.Part);
        if (refs.Inputs.TryGetValue($"Rep_{i}", out var inpR)) inpR.SetTextWithoutNotify(entry.Repeats.ToString());
        if (refs.Inputs.TryGetValue($"Mult_{i}", out var inpM)) inpM.SetTextWithoutNotify(entry.Multiplier.ToString());
        if (refs.Toggles.TryGetValue($"Tier_{i}", out var tglT)) tglT.SetIsOnWithoutNotify(entry.PerTier);

        // Populate Item text input for Ritem / Ritemx
        if (refs.Inputs.TryGetValue($"Item_{i}", out var inpItem))
        {
            inpItem.SetTextWithoutNotify(entry.ItemName);
        }

        // Case insensitive lookup for Item Names in Filtered Dropdowns
        if (refs.FilteredDropdowns.TryGetValue($"Item_{i}", out var drop))
        {
            string[] sourceArray = GetOptionArray(entry.Type);
            int dropIdx = Array.FindIndex(sourceArray, x => x.Equals(entry.ItemName, StringComparison.OrdinalIgnoreCase));
            if (dropIdx >= 0) drop.SetValueWithoutNotify(dropIdx);
        }

        // Case insensitive lookup for Targets
        if (refs.FilteredDropdowns.TryGetValue($"Target_{i}", out var targetDrop))
        {
            int targetIdx = Array.FindIndex(GetTargetOptions(), x => x.Equals(entry.Target, StringComparison.OrdinalIgnoreCase));
            if (targetIdx >= 0) targetDrop.SetValueWithoutNotify(targetIdx);
        }
    }

    // ==========================================
    // BACKEND PARSING & SAVING
    // ==========================================
    private void SaveState(EntityCard card, List<BaseItemEntry> entries, ItemUI ui, bool forceInspectorRebuild)
    {
        List<string> parts = new List<string>();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string s = "";

            switch (e.Type)
            {
                case BasePackEntryType.BaseItem:
                case BasePackEntryType.Ritemx:
                case BasePackEntryType.Ritem:
                    string prefix = "";
                    string targetPrefix = string.IsNullOrEmpty(e.Target) || e.Target == "none" ? "" : $"{e.Target}.";

                    if (e.PerTier) prefix += "pertier.";
                    else if (e.Repeats != 1 && e.Repeats != 0) prefix += $"x{e.Repeats}.";
                    if (e.Unpack) prefix += "unpack.";

                    s = targetPrefix + prefix + e.ItemName;
                    if (!string.IsNullOrWhiteSpace(e.Part)) s += $".part.{e.Part}";
                    if (e.Multiplier != 1) s += $".m.{e.Multiplier}";
                    break;

                case BasePackEntryType.Keyword:
                    string kwTarget = string.IsNullOrEmpty(e.Target) || e.Target == "none" ? "" : $"{e.Target}.";
                    s = $"{kwTarget}k.{e.ItemName}";
                    break;

                case BasePackEntryType.TogItem:
                    string togTarget = string.IsNullOrEmpty(e.Target) || e.Target == "none" ? "" : $"{e.Target}.";
                    s = $"{togTarget}{e.ItemName}";
                    break;
            }

            if (!string.IsNullOrEmpty(s))
            {
                bool needsBrackets = s.Contains(".part.") || s.Contains(".m.") || s.Contains("pertier.") || s.Contains("unpack.");
                bool hasExplicitTarget = !string.IsNullOrEmpty(e.Target) && e.Target != "none";

                if ((needsBrackets || hasExplicitTarget) && !s.StartsWith("("))
                {
                    s = $"({s})";
                }

                if (i < entries.Count - 1) s += e.NextOp;
                parts.Add(s);
            }
        }

        card.MechanicData.PayloadString = string.Join("", parts);
        ui.AutoCompile();

        if (forceInspectorRebuild)
        {
            ui.SelectCard(card);
            ui.RefreshSidebar();
        }
    }
    private List<BaseItemEntry> ParsePayload(string payload)
    {
        var entries = new List<BaseItemEntry>();
        if (string.IsNullOrWhiteSpace(payload)) return entries;

        string clean = payload.Replace("(", "").Replace(")", "");
        string[] tokens = Regex.Split(clean, NodeOperatorUtility.ParseRegexPattern);

        for (int i = 0; i < tokens.Length; i += 2)
        {
            string segment = tokens[i].Trim();
            string opStr = (i + 1 < tokens.Length) ? tokens[i + 1] : "";

            var entry = new BaseItemEntry { NextOp = opStr };

            string target = "none";
            string workingSegment = segment;
            var targetMatch = Regex.Match(segment, @"^([a-z0-9_]+)\.(k\.|tog|ritemx\.)");
            if (targetMatch.Success)
            {
                target = targetMatch.Groups[1].Value;
                workingSegment = segment.Substring(targetMatch.Groups[1].Length + 1);
            }

            entry.Target = target;

            if (workingSegment.StartsWith("k."))
            {
                entry.Type = BasePackEntryType.Keyword;
                entry.ItemName = workingSegment.Substring(2);
            }
            else if (GetOptionArray(BasePackEntryType.TogItem).Any(tog => workingSegment.EndsWith(tog)))
            {
                entry.Type = BasePackEntryType.TogItem;
                entry.ItemName = workingSegment;
            }
            else if (workingSegment.StartsWith("ritemx.") || workingSegment.Contains(".ritemx."))
            {
                entry.Type = BasePackEntryType.Ritemx;
                ParseBaseOrRitemx(entry, workingSegment);
            }
            else if (workingSegment.StartsWith("ritem.") || workingSegment.Contains(".ritem."))
            {
                entry.Type = BasePackEntryType.Ritem;
                ParseBaseOrRitemx(entry, workingSegment);
            }
            else
            {
                entry.Type = BasePackEntryType.BaseItem;
                ParseBaseOrRitemx(entry, workingSegment);
            }

            entries.Add(entry);
        }
        return entries;
    }
    private void ParseBaseOrRitemx(BaseItemEntry entry, string segment)
    {
        string cleanSeg = segment;

        // Reset defaults
        entry.Repeats = 1;
        entry.Multiplier = 1;
        entry.PerTier = false;

        // 1. Extract Repeats (e.g. x5.)
        if (cleanSeg.StartsWith("pertier."))
        {
            entry.PerTier = true;
            cleanSeg = cleanSeg.Substring(8);
        }
        else
        {
            var repeatMatch = Regex.Match(cleanSeg, @"^x(\d+)\.");
            if (repeatMatch.Success)
            {
                entry.Repeats = int.Parse(repeatMatch.Groups[1].Value);
                cleanSeg = cleanSeg.Substring(repeatMatch.Length);
            }
        }

        // 2. Extract Unpack
        if (cleanSeg.StartsWith("unpack."))
        {
            entry.Unpack = true;
            cleanSeg = cleanSeg.Substring(7);
        }

        // 4. Extract Multiplier (e.g. .m.-1 or .m.6)
        var multMatch = Regex.Match(cleanSeg, @"\.m\.(-?\d+)");
        if (multMatch.Success)
        {
            entry.Multiplier = int.Parse(multMatch.Groups[1].Value);
            cleanSeg = cleanSeg.Remove(multMatch.Index, multMatch.Length);
        }

        // 5. Extract Part (e.g. .part.2)
        var partMatch = Regex.Match(cleanSeg, @"\.part\.(\d+)$");
        if (partMatch.Success)
        {
            entry.Part = partMatch.Groups[1].Value;
            cleanSeg = cleanSeg.Remove(partMatch.Index, partMatch.Length);
        }

        // 6. Whatever is left is the core item name
        entry.ItemName = cleanSeg;
    }
}

public class RawStringNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Raw String Injection";
    public override ItemNodeType NodeType => ItemNodeType.RawString;
    public override Color GetColor() => new Color(0.2f, 0.4f, 0.6f); // Blue

    public override string GetTitle(EntityCard card)
    {
        string payload = card.MechanicData.PayloadString ?? "";
        if (payload.Length > 20) payload = payload.Substring(0, 20) + "...";
        return $"[Raw] {payload}";
    }
    /*
    public override string Compile(EntityCard card) => card.MechanicData.PayloadString;
    */
    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        ui.CreateInspectorTextArea("Raw Payload", card.MechanicData.PayloadString, v => { card.MechanicData.PayloadString = v; ui.AutoCompile(); });
    }
}

public class OperatorNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Join Operator (#, merge, splice)";

    public override ItemNodeType NodeType => ItemNodeType.Operator;
    public override bool IsOperator => true; // Tells the compiler NOT to add dots around this
    public override Color GetColor() => new Color(0.3f, 0.3f, 0.3f); // Dark Grey
    public override bool HasDeleteButton => false;
    public override bool HasPayloadPort => false;

    private string CycleOp(string current)
    {
        if (current == "#") return ".mrg.";
        if (current == ".mrg.") return ".splice.";
        if (current == ".splice.") return ".i.";
        return "#";
    }
    /*
    public override string Compile(EntityCard card)
    {
        return string.IsNullOrEmpty(card.MechanicData.PayloadString) ? "#" : card.MechanicData.PayloadString;
    }
    */
    public override string GetTitle(EntityCard card)
    {
        string op = string.IsNullOrEmpty(card.MechanicData.PayloadString) ? "#" : card.MechanicData.PayloadString;
        return NodeOperatorUtility.GetLabel(op);
    }
    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null)
        {
            Debug.LogError("FullScreenUIGenerator Instance missing!");
            return;
        }

        // 1. Create an isolated layout container inside the Inspector
        GameObject containerObj = new GameObject("OperatorGridContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        var layoutElem = containerObj.GetComponent<LayoutElement>();

        var layout = new List<GridRowSpec>();

        // 2. Determine labels based on current state
        string currentOp = string.IsNullOrEmpty(card.MechanicData.PayloadString) ? "#" : card.MechanicData.PayloadString;
        string opLabel = NodeOperatorUtility.GetLabel(currentOp);

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Join Operator:", 0.4f),
            GridCellSpec.CreateButton("BtnCycleOp", opLabel, 0.6f, () => {
                card.MechanicData.PayloadString = NodeOperatorUtility.CycleOp(currentOp);
                ui.AutoCompile();
                ui.RefreshSidebar();
                ui.SelectCard(card);
            })
        ));

        // 4. Generate the Physical Grid
        var refs = fsg.RebuildGrid(containerObj.GetComponent<RectTransform>(), layout, false);

        // 5. Size the container perfectly
        layoutElem.minHeight = refs.TotalHeight + (fsg.rowHeight * 2);
        layoutElem.flexibleHeight = 0;
    }
}

public class ManualBracketNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Group Bracket ( )";

    // Using Operation as the enum category, or you can map this to a custom one
    public override ItemNodeType NodeType => ItemNodeType.Bracket;

    // Grey-ish color scheme
    public override Color GetColor() => new Color(0.45f, 0.45f, 0.45f);

    public override string GetTitle(EntityCard card)
    {
        string compiledChildren = ItemUI.CompileZone(card.PayloadPort?.Entrants.Cast<EntityCard>());

        if (string.IsNullOrWhiteSpace(compiledChildren))
            return "[ Group (Empty) ]";

        if (compiledChildren.Length > 20)
            compiledChildren = compiledChildren.Substring(0, 20) + "...";

        return $"[ Group ] ({compiledChildren})";
    }
    /*
    public override string Compile(EntityCard card)
    {
        // 1. Compile everything dropped inside this group's port
        string compiledChildren = StringAuthoringUIManager.CompileZone(card.PayloadPort?.Entrants.Cast<EntityCard>());

        if (string.IsNullOrWhiteSpace(compiledChildren))
            return string.Empty;

        // 2. Wrap the output explicitly in parentheses
        return $"({compiledChildren})";
    }
    */
    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null) return;

        // 1. Create layout container
        GameObject containerObj = new GameObject("BracketGridContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        var layoutElem = containerObj.GetComponent<LayoutElement>();

        var layout = new List<GridRowSpec>();

        // 2. Simple explanatory label row
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Bracket Grouping:", 0.4f),
            GridCellSpec.CreateLabel("Wraps all nested cards inside ( )", 0.6f)
        ));

        // 3. Build physical grid layout
        var refs = fsg.RebuildGrid(containerObj.GetComponent<RectTransform>(), layout, false);

        layoutElem.minHeight = refs.TotalHeight + (fsg.rowHeight * 2);
        layoutElem.flexibleHeight = 0;
    }
}

public static class NodeOperatorUtility
{
    public const string ParseRegexPattern = @"(#|\.mrg\.|\.splice\.|\.i\.)";

    public static string CycleOp(string current)
    {
        if (current == "#") return ".mrg.";
        if (current == ".mrg.") return ".splice.";
        if (current == ".splice.") return ".i.";
        return "#";
    }

    public static string GetLabel(string op)
    {
        if (op == ".mrg.") return "[ MERGE .mrg. ]";
        if (op == ".splice.") return "[ SPLICE ]";
        if (op == ".i.") return "[ NEW ITEM .i. ]";
        return "[ AND # ]"; // Fallback/Default for "#"
    }
}

public class LearnAbilityNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Learn Ability";
    public override ItemNodeType NodeType => ItemNodeType.LearnAbility;
    public override bool IsOperator => false;
    public override bool IsEntity => true;
    public override bool HasDeleteButton => true;
    public override bool HasPayloadPort => false;

    public override string GetTitle(EntityCard card)
    {
        string payload = card.MechanicData.PayloadString ?? "";
        string name = "Unknown Ability";

        // Try to match the exact string to a custom ability in the package
        var matchingAbility = ModPackage.Instance?.CustomAbilities?.FirstOrDefault(a =>
            a.Export() == payload ||
            $"({a.Export()})" == payload ||
            a.Export() == payload.Trim('(', ')')
        );

        if (matchingAbility != null)
        {
            name = matchingAbility.entityName;
        }
        else
        {
            // Fallback: extract the name from the inline .n. tag using Regex
            var match = System.Text.RegularExpressions.Regex.Match(payload, @"\.n\.([^\.\#\)]+)");
            if (match.Success) name = match.Groups[1].Value;
        }

        return string.IsNullOrEmpty(payload) ? "Learn Ability" : $"Learn: {name}";
    }

    public override Color GetColor() => new Color(0.6f, 0.2f, 0.8f); // Distinct purple

    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        string currentName = "";
        string payload = card.MechanicData.PayloadString ?? "";

        var matchingAbility = ModPackage.Instance?.CustomAbilities?.FirstOrDefault(a =>
            a.Export() == payload ||
            $"({a.Export()})" == payload ||
            a.Export() == payload.Trim('(', ')')
        );

        if (matchingAbility != null) currentName = matchingAbility.entityName;
        else
        {
            var match = System.Text.RegularExpressions.Regex.Match(payload, @"\.n\.([^\.\#\)]+)");
            if (match.Success) currentName = match.Groups[1].Value;
        }

        //MASSIVE CODE STINK - WHY IS THE UI APPENDING PREFIXES? THE ABILITY SHOULD JUST DO THAT ITSELF. NOT ITEMUI RESPONSBILITY, ABILITYDATA RESPONSBILITY.
        ui.CreateInspectorAbilityDropdown("Ability:", currentName, (selected) => {
            var targetAbility = ModPackage.Instance?.CustomAbilities?.FirstOrDefault(a => a.entityName == selected);
            if (targetAbility != null)
            {
                string exportStr;
                // Route to the correct prefix based on the underlying ModPackage type
                if (targetAbility is OrbData orb)
                {
                    card.MechanicData.Prefix = "t.orb";
                    if (orb.isHardcoded)
                    {
                        string name = !string.IsNullOrEmpty(orb.hardcodedAbilityName) ? orb.hardcodedAbilityName.ToLower() : (orb.entityName?.ToLower() ?? "slice");
                        exportStr = name;
                    }
                    else
                    {
                        string carrier = !string.IsNullOrEmpty(orb.carrierPrefix) ? orb.carrierPrefix : "sthief.abilitydata";
                        exportStr = $"{carrier}.{targetAbility.Export()}";
                    }
                }
                else if (targetAbility is TriggerHPData)
                {
                    card.MechanicData.Prefix = "triggerhpdata";
                    exportStr = targetAbility.Export();
                }
                else if (targetAbility is OnHitData)
                {
                    card.MechanicData.Prefix = "onhitdata";
                    exportStr = targetAbility.Export();
                }
                else
                {
                    card.MechanicData.Prefix = "abilitydata";
                    exportStr = targetAbility.Export();
                }

                if (!exportStr.StartsWith("(")) exportStr = $"({exportStr})";
                card.MechanicData.PayloadString = exportStr;
            }
            else
            {
                card.MechanicData.Prefix = "abilitydata";
                card.MechanicData.PayloadString = "";
            }
            ui.RefreshSidebar();
            ui.AutoCompile();
        });
    }
}