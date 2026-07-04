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
    public override string NodeNiceName => "(Hat) Set Dice Faces";
    public override Color GetColor() => new Color(0.3f, 0.5f, 0.85f); // Greenish
    public override ItemNodeType NodeType => ItemNodeType.Hat;
    public override bool IsEntity => true;

    private GridReferences _diceUI;
    private RectTransform _diceGridTarget;
    private LayoutElement _diceGridLayoutElement;
    private LayoutElement _mainContainerLayoutElement;

    private int _currentDiceTab = 0;
    private DiceSideData _diceClipboard = null;
    private DiceFacesPreviewUI _previewUI;
    private int _currentMask = 1; // Default (left)
    /*
    public override string Compile(EntityCard card)
    {
        if (!(card.MechanicData.PayloadData is HeroData heroData))
        {
            heroData = new HeroData();
            heroData.InitializeDiceFaces();
            card.MechanicData.PayloadData = heroData;
        }

        // 1. Determine Target Prefix
        string defaultAliasName = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.mask == _currentMask).name ?? "left";
        string targets = card.MechanicData.Targets.Count > 0 ? string.Join(".", card.MechanicData.Targets) : defaultAliasName;

        bool isAll = targets.Equals("all", StringComparison.OrdinalIgnoreCase);
        string prefix = isAll ? "" : $"{targets}.";

        // 2. Generate Hat Core
        string hatCore = $"hat.({GetHatDiceString(heroData)})";

        // 3. Append Child Modifiers
        string childModifiers = StringAuthoringUIManager.CompileZone(card.PayloadPort?.Entrants.Cast<EntityCard>() ?? new List<EntityCard>());

        if (!string.IsNullOrWhiteSpace(childModifiers))
        {
            if (childModifiers.StartsWith(".")) childModifiers = childModifiers.Substring(1);
            return $"{prefix}{hatCore}.i.({childModifiers})";
        }

        return $"{prefix}{hatCore}";
    }
    */
    /*
    public static string GetHatDiceString(HeroData heroData)
    {
        StringBuilder sb = new StringBuilder();

        // Use your base name without "replica."
        string baseName = string.IsNullOrEmpty(heroData.baseReplica) ? "Statue" : heroData.baseReplica;
        sb.Append(baseName);

        // 1. Append the .sd. block using your exact AppendDiceSides logic
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            if (heroData.diceSides[i] != null && (heroData.diceSides[i].effectID != 0 || heroData.diceSides[i].pips != 0))
            {
                lastActiveIndex = i;
            }
        }

        if (lastActiveIndex != -1)
        {
            sb.Append(".sd.");
            for (int i = 0; i <= lastActiveIndex; i++)
            {
                var side = heroData.diceSides[i];
                if (side == null || (side.effectID == 0 && side.pips == 0))
                {
                    sb.Append("0");
                }
                else
                {
                    if (side.pips == 0)
                    {
                        sb.Append(side.effectID);
                    }
                    else
                    {
                        sb.Append($"{side.effectID}-{side.pips}");
                    }
                }

                if (i < lastActiveIndex) sb.Append(":");
            }
        }

        // 2. Call your native method directly to append the Facades, HSV parameters, and Keywords perfectly
        string faceModifiers = heroData.BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            sb.Append(faceModifiers);
        }

        return sb.ToString();
    }
    */

    public static string GetHatDiceString(HeroData heroData)
    {
        StringBuilder sb = new StringBuilder();

        string baseName = string.IsNullOrEmpty(heroData.baseReplica) ? "Statue" : heroData.baseReplica;
        sb.Append(baseName);

        // 1. Append the .sd. block
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            if (heroData.diceSides[i] != null && (heroData.diceSides[i].effectID != 0 || heroData.diceSides[i].pips != 0))
            {
                lastActiveIndex = i;
            }
        }

        if (lastActiveIndex != -1)
        {
            sb.Append(".sd.");
            for (int i = 0; i <= lastActiveIndex; i++)
            {
                var side = heroData.diceSides[i];
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

        // 2. Append standard Facades, HSV, and Keywords
        string faceModifiers = heroData.BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            sb.Append(faceModifiers);
        }

        // 3. NATIVE STICKER APPENDING: Automatically output sticker modifiers per side
        string[] sideTargets = new string[] { "left", "right", "top", "bot", "mid", "rightmost" };
        for (int i = 0; i < 6; i++)
        {
            DiceSideData side = heroData.diceSides[i];
            if (side != null && !string.IsNullOrWhiteSpace(side.sticker))
            {
                string cleanSticker = side.sticker.Trim();
                // Wrap in brackets if it's a complex item syntax, otherwise keep clean
                if (!cleanSticker.StartsWith("(") && (cleanSticker.Contains(".") || cleanSticker.Contains("#") || cleanSticker.Contains(":")))
                {
                    cleanSticker = $"({cleanSticker})";
                }
                sb.Append($".{sideTargets[i]}.sticker.{cleanSticker}");
            }
        }

        return sb.ToString();
    }
    public override string GetTitle(EntityCard card)
    {
        string targets = card.MechanicData.Targets.Count > 0 ? string.Join(".", card.MechanicData.Targets) : "mid";

        if (card.MechanicData.PayloadData is HeroData heroData && !string.IsNullOrEmpty(heroData.baseReplica))
            return $"[{targets}] Hat: {heroData.baseReplica}";

        return $"[{targets}] Hat (Empty)";
    }

    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null) return;

        if (!(card.MechanicData.PayloadData is HeroData heroData))
        {
            heroData = new HeroData();
            heroData.InitializeDiceFaces();
            card.MechanicData.PayloadData = heroData;
        }

        // Initialize current mask from targets
        string defaultAliasName = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.mask == _currentMask).name ?? "left";

        string currentTargetStr = card.MechanicData.Targets.FirstOrDefault() ?? defaultAliasName;
        var foundAlias = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.name == currentTargetStr);
        if (foundAlias.name == null) foundAlias = DiceTargetHelper.TargetAliases.First(a => a.name == defaultAliasName);

        _currentMask = foundAlias.mask;

        // 1. Master Container
        GameObject containerObj = new GameObject("HatDiceContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        _mainContainerLayoutElement = containerObj.GetComponent<LayoutElement>();

        var containerLayout = containerObj.AddComponent<VerticalLayoutGroup>();
        containerLayout.spacing = 10f;
        containerLayout.childControlHeight = true;
        containerLayout.childControlWidth = true;
        containerLayout.childForceExpandHeight = false;

        // 2. Target Dropdown Filter
        CreateTargetDropdown(containerObj.transform, ui, card);

        // 3. Dice Face Preview UI Instantiation
        if (fsg.dicePreviewAlonePrefab != null)
        {
            GameObject dicePreviewObj = UnityEngine.Object.Instantiate(fsg.dicePreviewAlonePrefab, containerObj.transform, false);

            // CONSTRAIN PREVIEW SIZE: Prevent it from collapsing or stretching unpredictably
            LayoutElement previewLayout = dicePreviewObj.GetComponent<LayoutElement>() ?? dicePreviewObj.AddComponent<LayoutElement>();
            previewLayout.minHeight = 110f;
            previewLayout.preferredHeight = 120f;
            previewLayout.flexibleHeight = 0f;

            _previewUI = dicePreviewObj.GetComponent<DiceFacesPreviewUI>();

            if (_previewUI != null)
            {
                _previewUI.OnFaceSelected += (faceIndex) =>
                {
                    _currentDiceTab = faceIndex + 1; // Standard index to tab index shift (1-6)
                    RebuildHatDiceGrid(ui, card, heroData);
                };
            }
        }

        /*
        // CONSTRAIN BUTTON SIZE: Instantiate and restrict button height so it doesn't overlap
        GameObject btnAllObj = UnityEngine.Object.Instantiate(fsg.buttonPrefab, containerObj.transform, false);

        LayoutElement btnLayout = btnAllObj.GetComponent<LayoutElement>() ?? btnAllObj.AddComponent<LayoutElement>();
        btnLayout.minHeight = 30f;
        btnLayout.preferredHeight = 35f;
        btnLayout.flexibleHeight = 0f;

        Button btnAll = btnAllObj.GetComponent<Button>();
        TMP_Text btnAllText = btnAllObj.GetComponentInChildren<TMP_Text>();
        if (btnAllText != null)
        {
            btnAllText.text = "Edit All Active Faces";
            btnAllText.fontSize = 14f; // Keep font size readable but compact
        }

        btnAll.onClick.AddListener(() =>
        {
            _currentDiceTab = 0;
            RebuildHatDiceGrid(ui, card, heroData);
        });
        */

        // 4. Raw Grid Container Setup
        GameObject gridTargetObj = new GameObject("DiceGridTarget", typeof(RectTransform), typeof(LayoutElement));
        gridTargetObj.transform.SetParent(containerObj.transform, false);
        _diceGridTarget = gridTargetObj.GetComponent<RectTransform>();
        _diceGridLayoutElement = gridTargetObj.GetComponent<LayoutElement>();

        RebuildHatDiceGrid(ui, card, heroData);
    }

    // --- GRID GENERATOR & DATA SYNCHRONIZATION ---

    private void RebuildHatDiceGrid(ItemUI ui, EntityCard card, HeroData heroData)
    {
        if (_diceGridTarget == null) return;

        List<GridRowSpec> diceLayout = GenerateHatDiceLayout(ui, card, heroData, _currentDiceTab);

        // Build directly into the raw transform, false disables margin padding
        _diceUI = FullScreenUIGenerator.Instance.RebuildGrid(_diceGridTarget, diceLayout, false);

        // Size the internal grid target
        _diceGridLayoutElement.minHeight = _diceUI.TotalHeight;

        // Size the master container (Tabs Height + Layout Spacing + Grid Height)
        //_mainContainerLayoutElement.minHeight = 40f + 10f + _diceUI.TotalHeight;

        _mainContainerLayoutElement.minHeight = 35f + 150f + 35f + 10f + _diceUI.TotalHeight;


        Canvas.ForceUpdateCanvases();
        UpdateHatDiceUIFromData(heroData);
    }

    private List<GridRowSpec> GenerateHatDiceLayout(ItemUI ui, EntityCard card, HeroData heroData, int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        string[] keywordOptions = EntityUIHelpers.GetKeywordOptions();

        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            var face = heroData.diceSides[index];

            // Add this check to exclude inactive faces:
            if (tabIndex == 0 && (_currentMask & (1 << index)) == 0) continue;

            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            int totalFaceRows = 8 + face.keywords.Count;

            var diceBgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f));
            diceBgRow.isBackground = true;
            diceBgRow.rowSpan = totalFaceRows;
            layout.Add(diceBgRow);

            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Base:", 0.15f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index, heroData, () => { ui.AutoCompile(); RebuildHatDiceGrid(ui, card, heroData); })),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { face.effectID = id; ui.AutoCompile(); RebuildHatDiceGrid(ui, card, heroData); } }),
                GridCellSpec.CreateLabel("Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index, heroData, () => { ui.AutoCompile(); RebuildHatDiceGrid(ui, card, heroData); })),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; ui.AutoCompile(); UpdateHatDiceUIFromData(heroData); })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Pips:", 0.25f),
                GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => {
                    if (int.TryParse(val, out int p))
                    {
                        face.pips = p;
                        ui.AutoCompile();
                        UpdateHatDiceUIFromData(heroData); // <-- ADDED: Refresh the UI graphics
                    }
                }),
                GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => {
                    face.pips--;
                    if (_diceUI.Inputs.TryGetValue($"Pips_{index}", out var inp)) inp.SetTextWithoutNotify(face.pips.ToString());
                    ui.AutoCompile();
                    UpdateHatDiceUIFromData(heroData); // <-- ADDED: Refresh the UI graphics
                }),
                GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => {
                    face.pips++;
                    if (_diceUI.Inputs.TryGetValue($"Pips_{index}", out var inp)) inp.SetTextWithoutNotify(face.pips.ToString());
                    ui.AutoCompile();
                    UpdateHatDiceUIFromData(heroData); // <-- ADDED: Refresh the UI graphics
                })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Hue:", 0.30f),
                GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => UpdateHatFaceHsv(ui, heroData, index, 0, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateHatFaceHsv(ui, heroData, index, 0, h); })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Sat:", 0.30f),
                GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => UpdateHatFaceHsv(ui, heroData, index, 1, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateHatFaceHsv(ui, heroData, index, 1, s); })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Val:", 0.30f),
                GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => UpdateHatFaceHsv(ui, heroData, index, 2, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateHatFaceHsv(ui, heroData, index, 2, v); })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Sticker Item:", 0.30f),
                GridCellSpec.CreateInput($"Sticker_{index}", face.sticker ?? "", 0.70f, (val) => {
                    face.sticker = val;
                    ui.AutoCompile();
                })
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, keywordOptions, (val) => AddHatKeywordToFace(ui, card, heroData, index, val))
            ));

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, keywordOptions, (val) => AddHatKeywordToFace(ui, card, heroData, index, val))
            ));

            foreach (var kw in face.keywords)
            {
                string keywordString = kw;
                string coloredLabel = EntityUIHelpers.GetColoredKeywordLabel(keywordString);

                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"KwTag_{index}_{keywordString}", coloredLabel, 0.80f),
                    GridCellSpec.CreateButton($"KwDel_{index}_{keywordString}", "[X]", 0.20f, () => RemoveHatKeywordFromFace(ui, card, heroData, index, keywordString))
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.50f, () => CopyHatDiceFace(heroData, index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.50f, () => PasteHatDiceFace(ui, card, heroData, index))
            ));

            if (tabIndex == 0 && index < 5)
            {
                layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
            }
        }

        return layout;
    }

    private void UpdateHatDiceUIFromData(HeroData heroData)
    {
        // 1. Update Preview UI state and data
        if (_previewUI != null)
        {
            int activeFaceIndex = _currentDiceTab == 0 ? -1 : _currentDiceTab - 1;
            _previewUI.UpdateFaceStates(_currentMask, activeFaceIndex);

            for (int i = 0; i < 6; i++)
            {
                var f = heroData.diceSides[i];
                _previewUI.SetSlotIcon(i, f.facadeID, f.effectID, f.facadeColor, f.pips);
            }
        }

        // 2. Loop adjustments for input fields
        int startIndex = (_currentDiceTab == 0) ? 0 : _currentDiceTab - 1;
        int endIndex = (_currentDiceTab == 0) ? 6 : _currentDiceTab;

        for (int i = startIndex; i < endIndex; i++)
        {
            // Skip updating fields for inactive faces if in "All" mode
            if (_currentDiceTab == 0 && (_currentMask & (1 << i)) == 0) continue;

            var face = heroData.diceSides[i];
            if (_diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
            if (_diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());
            if (_diceUI.Inputs.TryGetValue($"Facade_{i}", out var dFac)) dFac.SetTextWithoutNotify(face.facadeID);

            if (_diceUI.Inputs.TryGetValue($"Sticker_{i}", out var dStk)) dStk.SetTextWithoutNotify(face.sticker ?? "");

            int h = 0, s = 0, v = 0;
            string[] hsvParts = (face.facadeColor ?? "").Split(':');
            if (hsvParts.Length > 0 && int.TryParse(hsvParts[0], out int pH)) h = pH;
            if (hsvParts.Length > 1 && int.TryParse(hsvParts[1], out int pS)) s = pS;
            if (hsvParts.Length > 2 && int.TryParse(hsvParts[2], out int pV)) v = pV;

            if (_diceUI.Sliders.TryGetValue($"SliH_{i}", out var sliH)) sliH.SetValueWithoutNotify(h);
            if (_diceUI.Sliders.TryGetValue($"SliS_{i}", out var sliS)) sliS.SetValueWithoutNotify(s);
            if (_diceUI.Sliders.TryGetValue($"SliV_{i}", out var sliV)) sliV.SetValueWithoutNotify(v);

            if (_diceUI.Inputs.TryGetValue($"FacH_{i}", out var dH)) dH.SetTextWithoutNotify(h != 0 ? h.ToString() : "");
            if (_diceUI.Inputs.TryGetValue($"FacS_{i}", out var dS)) dS.SetTextWithoutNotify(s != 0 ? s.ToString() : "");
            if (_diceUI.Inputs.TryGetValue($"FacV_{i}", out var dV)) dV.SetTextWithoutNotify(v != 0 ? v.ToString() : "");

            if (_diceUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
            {
                SetButtonIcon(baseBtn, EntityUIHelpers.GetBaseSprite(face.effectID));
            }
            if (_diceUI.Buttons.TryGetValue($"FacBtn_{i}", out var facBtn))
            {
                SetButtonIcon(facBtn, EntityUIHelpers.GetFacadeSprite(face.facadeID));
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

    // --- SELF-CONTAINED MODAL TRIGGER LOGIC ---

    private IconPickerModal GetIconPicker()
    {
        return IconPickerModal.Instance;
    }

    private void OpenBaseModal(int faceIndex, HeroData heroData, System.Action onComplete)
    {
        var iconPicker = GetIconPicker();
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
                    string[] parts = sprite.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
                    {
                        heroData.diceSides[faceIndex].effectID = parsedId;
                        onComplete?.Invoke();
                    }
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    private void OpenFacadeModal(int faceIndex, HeroData heroData, System.Action onComplete)
    {
        var iconPicker = GetIconPicker();
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

                        if (prefix == "big" && parsedId >= 0 && parsedId <= 31) facadeStr = $"bas{188 + parsedId}";
                        else if (prefix == "hug" && parsedId >= 0 && parsedId <= 27) facadeStr = $"bas{220 + parsedId}";
                        else if (prefix == "tin" && parsedId >= 0 && parsedId <= 17) facadeStr = $"bas{248 + parsedId}";
                        else facadeStr = $"{parts[0]}{parts[1]}";

                        heroData.diceSides[faceIndex].facadeID = facadeStr;
                    }
                    else
                    {
                        heroData.diceSides[faceIndex].facadeID = filename;
                    }
                    onComplete?.Invoke();
                }
            }
        };
        iconPicker.OpenModal(config);
    }

    // --- DICE INTERACTION UTILITIES ---

    private void CopyHatDiceFace(HeroData heroData, int index)
    {
        _diceClipboard = heroData.diceSides[index].Clone();
    }

    private void PasteHatDiceFace(ItemUI ui, EntityCard card, HeroData heroData, int index)
    {
        if (_diceClipboard == null) return;
        heroData.diceSides[index] = _diceClipboard.Clone();
        ui.AutoCompile(); // <-- Added
        RebuildHatDiceGrid(ui, card, heroData);
    }

    private void AddHatKeywordToFace(ItemUI ui, EntityCard card, HeroData heroData, int faceIndex, int dropdownValue)
    {
        if (dropdownValue <= 0) return;
        string[] rawOptions = Enum.GetNames(typeof(EffectKeyword));
        string targetKeyword = rawOptions[dropdownValue - 1];

        var face = heroData.diceSides[faceIndex];
        if (!face.keywords.Contains(targetKeyword))
        {
            face.keywords.Add(targetKeyword);
            ui.AutoCompile(); // <-- Added
            RebuildHatDiceGrid(ui, card, heroData);
        }
    }

    private void RemoveHatKeywordFromFace(ItemUI ui, EntityCard card, HeroData heroData, int faceIndex, string keyword)
    {
        var face = heroData.diceSides[faceIndex];
        if (face.keywords.Remove(keyword))
        {
            ui.AutoCompile(); // <-- Added
            RebuildHatDiceGrid(ui, card, heroData);
        }
    }

    private void UpdateHatFaceHsv(ItemUI ui, HeroData heroData, int faceIndex, int componentIndex, int value)
    {
        var face = heroData.diceSides[faceIndex];
        if (string.IsNullOrEmpty(face.facadeID))
        {
            Sprite baseSprite = EntityUIHelpers.GetBaseSprite(face.effectID);
            if (baseSprite != null)
            {
                string[] parts = baseSprite.name.Split('_');
                if (parts.Length >= 2)
                {
                    face.facadeID = $"{parts[0]}{parts[1]}";
                }
            }
        }

        string[] partsColor = (face.facadeColor ?? "").Split(':');
        List<string> hsv = new List<string>(partsColor);
        while (hsv.Count < 3) hsv.Add("0");

        hsv[componentIndex] = value.ToString();
        face.facadeColor = string.Join(":", hsv);

        // Update the visible Input Field
        string inputKey = componentIndex == 0 ? $"FacH_{faceIndex}" : (componentIndex == 1 ? $"FacS_{faceIndex}" : $"FacV_{faceIndex}");
        if (_diceUI.Inputs.TryGetValue(inputKey, out var input)) input.SetTextWithoutNotify(value != 0 ? value.ToString() : "");

        ui.AutoCompile();
        UpdateHatDiceUIFromData(heroData);
    }

    private void CreateTargetDropdown(Transform parent, ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null || fsg.dropdownPrefab == null) return;

        // 1. Create a Horizontal Row Container
        GameObject rowObj = new GameObject("TargetSidesRow", typeof(RectTransform));
        rowObj.transform.SetParent(parent, false);

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.minHeight = 35f;
        rowLE.preferredHeight = 35f;
        rowLE.flexibleHeight = 0f;

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlHeight = true;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = true; // Force both label and dropdown to fill the row height
        rowLayout.childForceExpandWidth = false;

        // 2. Create the "Target Sides:" Label
        GameObject labelObj = new GameObject("TargetSidesLabel", typeof(RectTransform));
        labelObj.transform.SetParent(rowObj.transform, false);

        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.minWidth = 100f;
        labelLE.preferredWidth = 100f;
        labelLE.flexibleWidth = 0f;

        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = "Target Sides:";
        labelText.fontSize = 14f;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;

        // 3. Instantiate the Dropdown copy inside the Row
        GameObject dropdownObj = UnityEngine.Object.Instantiate(fsg.dropdownPrefab, rowObj.transform, false);

        var dropdownLE = dropdownObj.GetComponent<LayoutElement>() ?? dropdownObj.AddComponent<LayoutElement>();
        dropdownLE.flexibleWidth = 1f; // Force dropdown to fill the remaining horizontal space

        TMP_Dropdown dropdown = dropdownObj.GetComponentInChildren<TMP_Dropdown>(true);
        if (dropdown == null) dropdown = dropdownObj.GetComponent<TMP_Dropdown>();

        if (dropdown != null)
        {
            dropdown.ClearOptions();

            // 4. Reverse the alias collection order for the visuals
            var reversedAliases = DiceTargetHelper.TargetAliases.Reverse().ToList();
            List<string> options = reversedAliases.Select(a => DiceTargetHelper.FormatAliasName(a.name)).ToList();
            dropdown.AddOptions(options);

            // 5. Initialize the default selection index based on the reversed order
            string defaultAliasName = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.mask == _currentMask).name ?? "left";
            string currentTarget = card.MechanicData.Targets.FirstOrDefault() ?? defaultAliasName;

            int initialIndex = reversedAliases.FindIndex(a => a.name == currentTarget);
            dropdown.value = Mathf.Max(0, initialIndex);

            // 6. Handle selection change logic
            dropdown.onValueChanged.AddListener((val) =>
            {
                var selectedAlias = reversedAliases[val];
                card.MechanicData.Targets = new List<string> { selectedAlias.name };
                _currentMask = selectedAlias.mask;

                int faceIndex = _currentDiceTab - 1;
                if ((_currentMask & (1 << faceIndex)) == 0)
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

                ui.AutoCompile();
                ui.RefreshSidebar();

                if (card.MechanicData.PayloadData is HeroData hero)
                {
                    RebuildHatDiceGrid(ui, card, hero);
                }
            });
        }
    }
}

public class EquippableNodeDef : AuthoringNodeDef
{
    public override string NodeNiceName => "Equippable Item Appearance";
    public override bool IsEntity => true;
    public override ItemNodeType NodeType => ItemNodeType.Equippable;
    public override Color GetColor() => new Color(0.6f, 0.5f, 0.1f); // Gold
    private static Material _cachedShaderMaterial;

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
    public override void DrawInspector(ItemUI ui, EntityCard card)
    {
        var fsg = FullScreenUIGenerator.Instance;
        if (fsg == null) return;

        GameObject containerObj = new GameObject("EquipGridContainer", typeof(RectTransform), typeof(LayoutElement));
        containerObj.transform.SetParent(ui.InspectorContent, false);
        var layoutElem = containerObj.GetComponent<LayoutElement>();

        var layout = new List<GridRowSpec>();
        GridReferences currentRefs = null; // Captured reference for the callbacks to use

        // --- Core Identifiers ---
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Item Name:", 0.25f),
            GridCellSpec.CreateInput("Name", card.RootData.entityName, 0.75f, (val) =>
            {
                card.RootData.entityName = val;
                ui.RefreshSidebar();
                ui.AutoCompile(); // FIX: Now forces the string to update!
            })
        ));

        // FIX: Tier moved to its own row
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Tier:", 0.25f),
            GridCellSpec.CreateInput("Tier", card.RootData.Tier?.ToString() ?? "", 0.75f, (val) =>
            {
                if (int.TryParse(val, out int t)) card.RootData.Tier = t; else card.RootData.Tier = null;
                ui.AutoCompile();
            })
        ));

        // FIX: Image Ref moved to its own row with a Facade button
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Image Ref:", 0.25f),
            GridCellSpec.CreateDiceButton("FacBtn", "F", 0.15f, () => OpenFacadeModal(card, ui, currentRefs)),
            GridCellSpec.CreateInput("ImgRef", card.RootData.imageOverride, 0.60f, (val) =>
            {
                card.RootData.imageOverride = val;
                ui.AutoCompile();
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Doc (Desc):", 0.25f),
            GridCellSpec.CreateInput("Doc", card.RootData.DocumentedDescription, 0.75f, (val) => { card.RootData.DocumentedDescription = val; ui.AutoCompile(); })
        ));

        // --- HSV Shifting ---
        // FIX: Centralized update function that syncs BOTH the input fields and the sliders
        System.Action<int, int, int> updateHsv = (h, s, v) =>
        {
            card.RootData.HsvShift = new ItemHsvShift(h, s, v);
            if (currentRefs != null)
            {
                if (currentRefs.Inputs.TryGetValue("InpH", out var inpH)) inpH.SetTextWithoutNotify(h.ToString());
                if (currentRefs.Inputs.TryGetValue("InpS", out var inpS)) inpS.SetTextWithoutNotify(s.ToString());
                if (currentRefs.Inputs.TryGetValue("InpV", out var inpV)) inpV.SetTextWithoutNotify(v.ToString());

                if (currentRefs.Sliders.TryGetValue("SliH", out var sliH)) sliH.SetValueWithoutNotify(h);
                if (currentRefs.Sliders.TryGetValue("SliS", out var sliS)) sliS.SetValueWithoutNotify(s);
                if (currentRefs.Sliders.TryGetValue("SliV", out var sliV)) sliV.SetValueWithoutNotify(v);
            }
            ui.AutoCompile();
            RefreshButtonMaterial(currentRefs, card.RootData);
        };

        int initH = card.RootData.HsvShift?.Hue ?? 0;
        int initS = card.RootData.HsvShift?.Saturation ?? 0;
        int initV = card.RootData.HsvShift?.Value ?? 0;

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hue:", 0.25f),
            GridCellSpec.CreateSlider("SliH", -99, 99, true, 0.50f, (val) =>
            {
                // Fetch dynamically to avoid closure trap
                int curS = card.RootData.HsvShift?.Saturation ?? 0;
                int curV = card.RootData.HsvShift?.Value ?? 0;
                updateHsv(Mathf.RoundToInt(val), curS, curV);
            }),
            GridCellSpec.CreateInput("InpH", initH.ToString(), 0.25f, (val) =>
            {
                if (int.TryParse(val, out int h)) updateHsv(h, card.RootData.HsvShift?.Saturation ?? 0, card.RootData.HsvShift?.Value ?? 0);
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Sat:", 0.25f),
            GridCellSpec.CreateSlider("SliS", -99, 99, true, 0.50f, (val) =>
            {
                int curH = card.RootData.HsvShift?.Hue ?? 0;
                int curV = card.RootData.HsvShift?.Value ?? 0;
                updateHsv(curH, Mathf.RoundToInt(val), curV);
            }),
            GridCellSpec.CreateInput("InpS", initS.ToString(), 0.25f, (val) =>
            {
                if (int.TryParse(val, out int s)) updateHsv(card.RootData.HsvShift?.Hue ?? 0, s, card.RootData.HsvShift?.Value ?? 0);
            })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Val:", 0.25f),
            GridCellSpec.CreateSlider("SliV", -99, 99, true, 0.50f, (val) =>
            {
                int curH = card.RootData.HsvShift?.Hue ?? 0;
                int curS = card.RootData.HsvShift?.Saturation ?? 0;
                updateHsv(curH, curS, Mathf.RoundToInt(val));
            }),
            GridCellSpec.CreateInput("InpV", initV.ToString(), 0.25f, (val) =>
            {
                if (int.TryParse(val, out int v)) updateHsv(card.RootData.HsvShift?.Hue ?? 0, card.RootData.HsvShift?.Saturation ?? 0, v);
            })
        ));

        /*
        // --- T-Hue ---
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("T-Hue Rng:", 0.25f),
            GridCellSpec.CreateSlider("ThueR", 0, 99, true, 0.25f, (val) => { card.RootData.thue.colorRange = Mathf.RoundToInt(val); ui.AutoCompile(); }),
            GridCellSpec.CreateLabel("Shift:", 0.20f),
            GridCellSpec.CreateSlider("ThueO", -99, 99, true, 0.30f, (val) => { card.RootData.thue.colorOffset = Mathf.RoundToInt(val); ui.AutoCompile(); })
            RefreshButtonMaterial(currentRefs, card.RootData); 
        ));

        // --- Appearance Overrides ---
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Border Hex:", 0.25f),
            GridCellSpec.CreateInput("Border", card.RootData.BorderColorCode, 0.25f, (val) => { card.RootData.BorderColorCode = val; ui.AutoCompile(); }),
            GridCellSpec.CreateLabel("Palette:", 0.20f),
            GridCellSpec.CreateInput("Palette", card.RootData.PaletteOverride, 0.30f, (val) => { card.RootData.PaletteOverride = val; ui.AutoCompile(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Draw Instr:", 0.25f),
            GridCellSpec.CreateInput("Draw", card.RootData.UiDrawInstructions, 0.75f, (val) => { card.RootData.UiDrawInstructions = val; ui.AutoCompile(); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Rect Instr:", 0.25f),
            GridCellSpec.CreateInput("Rect", card.RootData.UiRectInstructions, 0.75f, (val) => { card.RootData.UiRectInstructions = val; ui.AutoCompile(); })
        ));
        */

        // --- Boolean Toggles ---
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateToggle("ClearDesc", "Suppress Doc", 0.5f, (val) => { card.RootData.ClearDescription = val; ui.AutoCompile(); }),
            GridCellSpec.CreateToggle("ClearIcon", "Suppress Icon", 0.5f, (val) => { card.RootData.ClearIcon = val; ui.AutoCompile(); })
        ));

        // 2. Build Grid and Assign Reference
        currentRefs = fsg.RebuildGrid(containerObj.GetComponent<RectTransform>(), layout, false);

        // 3. Initialize Visual States
        if (currentRefs.Toggles.TryGetValue("ClearDesc", out var descToggle)) descToggle.SetIsOnWithoutNotify(card.RootData.ClearDescription);
        if (currentRefs.Toggles.TryGetValue("ClearIcon", out var iconToggle)) iconToggle.SetIsOnWithoutNotify(card.RootData.ClearIcon);

        if (currentRefs.Buttons.TryGetValue("FacBtn", out var facBtn))
        {
            Sprite s = EntityUIHelpers.GetFacadeSprite(card.RootData.imageOverride);
            SetButtonIcon(facBtn, s, card.RootData);
        }

        // Initialize Sliders securely
        if (currentRefs.Sliders.TryGetValue("SliH", out var slH)) slH.SetValueWithoutNotify(initH);
        if (currentRefs.Sliders.TryGetValue("SliS", out var slS)) slS.SetValueWithoutNotify(initS);
        if (currentRefs.Sliders.TryGetValue("SliV", out var slV)) slV.SetValueWithoutNotify(initV);

        layoutElem.minHeight = currentRefs.TotalHeight + (fsg.rowHeight * 2);
        layoutElem.flexibleHeight = 0;
    }

    // ==========================================================
    // HELPER METHODS FOR FACADE PICKING
    // ==========================================================

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
    private void ApplyMaterialAdjustments(Image img, ItemData data) // Or whatever data class calls this
    {
        if (img == null || data == null) return;

        string uniqueInstanceName = "HSV_Instance_" + img.GetInstanceID();

        // 1. Force the material to be a unique runtime instance
        if (img.material == null || img.material == img.defaultMaterial)
        {
            Shader shader = Shader.Find("UI/Custom/P_THUE_HSV_Adjustment") ?? // Updated shader path hint just in case
                            Shader.Find("UI/P_Thue_HSV_Adjustment") ??
                            Shader.Find("P_Thue_HSV_Adjustment");
            if (shader != null)
            {
                img.material = new Material(shader);
                img.material.name = uniqueInstanceName;
            }
        }
        else if (img.material.name != uniqueInstanceName)
        {
            img.material = UnityEngine.Object.Instantiate(img.material);
            img.material.name = uniqueInstanceName;
        }

        int h = data.HsvShift?.Hue ?? 0;
        int s = data.HsvShift?.Saturation ?? 0;
        int v = data.HsvShift?.Value ?? 0;

        // 2. Apply properties to the BASE material
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

        // 3. PIERCE THE MASK: Apply properties to the actual active masked material being sent to the GPU!
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
        string[] currentOptions = GetOptionArray(entry.Type);

        // Define standard delete button width so all rows align on the right
        float btnDelW = 0.12f;

        if (entry.Type == BasePackEntryType.BaseItem || entry.Type == BasePackEntryType.Ritem || entry.Type == BasePackEntryType.Ritemx)
        {
            // Row 1: Core Identity & Deletion (Gives the dropdown maximum room)
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateToggle($"Unpack_{index}", "Unpack", 0.22f, (val) => { entry.Unpack = val; saveState(); }),
                GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.66f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); }),
                GridCellSpec.CreateButton($"Del_{index}", "X", btnDelW, () => { entries.RemoveAt(index); saveAndRebuild(); })
            ));

            // Row 2: Advanced Modifiers (Condensed labels for a clean, uniform fit)
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Part:", 0.12f),
                GridCellSpec.CreateInput($"Part_{index}", entry.Part, 0.14f, (val) => { entry.Part = val; saveState(); }),

                GridCellSpec.CreateLabel("Repeat:", 0.14f),
                GridCellSpec.CreateInput($"Rep_{index}", entry.Repeats.ToString(), 0.14f, (val) => {
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
                }),

                GridCellSpec.CreateLabel("M# Multi:", 0.12f),
                GridCellSpec.CreateInput($"Mult_{index}", entry.Multiplier.ToString(), 0.14f, (val) => { if (int.TryParse(val, out int v)) { entry.Multiplier = v; saveState(); } }),

                GridCellSpec.CreateToggle($"Tier_{index}", "Tier", 0.20f, (val) => {
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
                })
            ));
        }
        else if (entry.Type == BasePackEntryType.Keyword)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateFilteredDropdown($"Target_{index}", entry.Target, 0.35f, GetTargetOptions(), (val) => { entry.Target = GetTargetOptions()[val]; saveState(); }),
                GridCellSpec.CreateLabel("k.", 0.08f),
                GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.45f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); }),
                GridCellSpec.CreateButton($"Del_{index}", "X", btnDelW, () => { entries.RemoveAt(index); saveAndRebuild(); })
            ));
        }
        else if (entry.Type == BasePackEntryType.TogItem)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateFilteredDropdown($"Target_{index}", entry.Target, 0.35f, GetTargetOptions(), (val) => { entry.Target = GetTargetOptions()[val]; saveState(); }),
                GridCellSpec.CreateFilteredDropdown($"Item_{index}", entry.ItemName, 0.53f, currentOptions, (val) => { entry.ItemName = currentOptions[val]; saveState(); }),
                GridCellSpec.CreateButton($"Del_{index}", "X", btnDelW, () => { entries.RemoveAt(index); saveAndRebuild(); })
            ));
        }

        // Join Operator row between elements (Narrowed button to look more like a connector)
        if (index < entries.Count - 1)
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
    }

    private void PopulateEntryValues(GridReferences refs, int i, BaseItemEntry entry)
    {
        if (refs.Toggles.TryGetValue($"Unpack_{i}", out var tglU)) tglU.SetIsOnWithoutNotify(entry.Unpack);
        if (refs.Inputs.TryGetValue($"Part_{i}", out var inpP)) inpP.SetTextWithoutNotify(entry.Part);
        if (refs.Inputs.TryGetValue($"Rep_{i}", out var inpR)) inpR.SetTextWithoutNotify(entry.Repeats.ToString());
        if (refs.Inputs.TryGetValue($"Mult_{i}", out var inpM)) inpM.SetTextWithoutNotify(entry.Multiplier.ToString());
        if (refs.Toggles.TryGetValue($"Tier_{i}", out var tglT)) tglT.SetIsOnWithoutNotify(entry.PerTier);

        if (refs.FilteredDropdowns.TryGetValue($"Item_{i}", out var drop))
        {
            string[] sourceArray = GetOptionArray(entry.Type);
            int dropIdx = Array.IndexOf(sourceArray, entry.ItemName);
            if (dropIdx >= 0) drop.SetValueWithoutNotify(dropIdx);
        }

        if (refs.FilteredDropdowns.TryGetValue($"Target_{i}", out var targetDrop))
        {
            int targetIdx = Array.IndexOf(GetTargetOptions(), entry.Target);
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
                    if (e.PerTier) prefix += "pertier.";
                    else if (e.Repeats != 1 && e.Repeats != 0) prefix += $"x{e.Repeats}.";
                    if (e.Unpack) prefix += "unpack.";

                    s = prefix + e.ItemName;
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

            // SMART WRAPPING: Only wrap in brackets if the item contains sub-properties or complex modifiers
            // Simple atomic items like 'togtarg' or 'ritemx.0' stay bracket-free!
            bool needsBrackets = s.Contains(".part.") || s.Contains(".m.") || s.Contains("pertier.") || s.Contains("unpack.");
            if (needsBrackets && !s.StartsWith("("))
            {
                s = $"({s})";
            }

            if (i < entries.Count - 1) s += e.NextOp;

            parts.Add(s);
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