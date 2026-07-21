using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class StaticUI
{
    public static void SetButtonIcon(Button btn, Sprite sprite)
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
}

public class DiceFaceBuilderWidget
{
    public class PayloadType
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public bool HasBaseAndPips { get; set; }
        public bool HasStringPayload { get; set; }
    }

    public static readonly List<PayloadType> RegisteredPayloads = new List<PayloadType>
    {
    new PayloadType { Id = "standard", DisplayName = "Standard Base", HasBaseAndPips = true, HasStringPayload = false },
    new PayloadType { Id = "sticker", DisplayName = "Sticker (Give Item)", HasBaseAndPips = false, HasStringPayload = true },
    new PayloadType { Id = "cast", DisplayName = "Cast (Ability)", HasBaseAndPips = false, HasStringPayload = true },
    new PayloadType { Id = "enchant", DisplayName = "Enchant (Give Modifier)", HasBaseAndPips = false, HasStringPayload = true },
    new PayloadType { Id = "egg", DisplayName = "Egg (Summon)", HasBaseAndPips = false, HasStringPayload = true }
    };

    public static DiceSideData SharedClipboard = null;
    public static readonly List<string> TabNames = new List<string> { "Left", "Middle", "Top", "Bottom", "Right", "Rightmost" };

    // CHANGED: Strictly use DiceSideData[] array
    private Func<DiceSideData[]> _getDiceSides;
    private Func<bool> _allowFacades;
    private Action<int> _openBaseModal;
    private Action<int> _openFacadeModal;
    private Func<int, Sprite> _getBaseSprite;
    private Func<string, Sprite> _getFacadeSprite;
    private Action _onStateChanged;
    private Action _onRebuildRequested;

    private GridReferences _diceUI;
    //private Dictionary<int, string> _uiFaceTypeOverrides = new Dictionary<int, string>();

    // ADDED: Track manual toggle state overrides per face index
    private Dictionary<int, bool> _castIsCustomOverride = new Dictionary<int, bool>();

    public DiceFaceBuilderWidget(
        Func<DiceSideData[]> getDiceSides,
        Func<bool> allowFacades,
        Action<int> openBaseModal,
        Action<int> openFacadeModal,
        Func<int, Sprite> getBaseSprite,
        Func<string, Sprite> getFacadeSprite,
        Action onStateChanged,
        Action onRebuildRequested)
    {
        _getDiceSides = getDiceSides;
        _allowFacades = allowFacades;
        _openBaseModal = openBaseModal;
        _openFacadeModal = openFacadeModal;
        _getBaseSprite = getBaseSprite;
        _getFacadeSprite = getFacadeSprite;
        _onStateChanged = onStateChanged;
        _onRebuildRequested = onRebuildRequested;
    }

    public void SetGridReferences(GridReferences gridRefs)
    {
        _diceUI = gridRefs;
    }
    public void CopyDiceFace(int index)
    {
        var sides = _getDiceSides?.Invoke();
        if (sides != null && index >= 0 && index < sides.Length)
            SharedClipboard = sides[index].Clone();
    }
    public void PasteDiceFace(int index)
    {
        if (SharedClipboard == null) return;
        var sides = _getDiceSides?.Invoke();
        if (sides != null && index >= 0 && index < sides.Length)
        {
            sides[index] = SharedClipboard.Clone();
            _onStateChanged?.Invoke();
            _onRebuildRequested?.Invoke();
        }
    }
    public void ClearDiceFace(int index)
    {
        var sides = _getDiceSides?.Invoke();
        if (sides != null && index >= 0 && index < sides.Length)
        {
            sides[index] = new DiceSideData();
            _onStateChanged?.Invoke();
            _onRebuildRequested?.Invoke();
        }
    }

    private void AddKeywordToFace(int faceIndex, int dropdownValue)
    {
        if (dropdownValue <= 0) return;
        string[] rawOptions = Enum.GetNames(typeof(EffectKeyword));

        string targetKeyword = rawOptions[dropdownValue - 1].ToLower();

        var sides = _getDiceSides?.Invoke();
        if (sides != null && faceIndex >= 0 && faceIndex < sides.Length)
        {
            var face = sides[faceIndex];
            if (!face.keywords.Contains(targetKeyword))
            {
                face.keywords.Add(targetKeyword);
                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke();
            }
        }
    }
    private void RemoveKeywordFromFace(int faceIndex, string keyword)
    {
        var sides = _getDiceSides?.Invoke();
        if (sides != null && faceIndex >= 0 && faceIndex < sides.Length)
        {
            string target = keyword.ToLower();
            if (sides[faceIndex].keywords.Remove(target))
            {
                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke();
            }
            else
            {
                // Fallback helper in case of legacy mixed-case leftovers in the active session
                int idx = sides[faceIndex].keywords.FindIndex(k => string.Equals(k, keyword, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    sides[faceIndex].keywords.RemoveAt(idx);
                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                }
            }
        }
    }
    private void UpdateFaceHsv(int faceIndex, int componentIndex, int value)
    {
        if (_allowFacades != null && !_allowFacades()) return;
        var sides = _getDiceSides?.Invoke();
        if (sides == null || faceIndex < 0 || faceIndex >= sides.Length) return;

        // Cap the value between -99 and 99
        value = Mathf.Clamp(value, -99, 99);

        var face = sides[faceIndex];
        bool facadeAutoAssigned = false;

        if (string.IsNullOrEmpty(face.facadeID))
        {
            Sprite baseSprite = _getBaseSprite?.Invoke(face.effectID);
            if (baseSprite != null)
            {
                string[] parts = baseSprite.name.Split('_');
                if (parts.Length >= 2)
                {
                    // Concatenate the prefix and the number directly without an underscore
                    face.facadeID = $"{parts[0]}{parts[1]}";
                    facadeAutoAssigned = true;
                }
            }
        }

        string[] partsColor = (face.facadeColor ?? "").Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> hsv = new List<string>(partsColor);
        while (hsv.Count < 3) hsv.Add("0");

        hsv[componentIndex] = value.ToString();

        if (hsv[0] == "0" && hsv[1] == "0" && hsv[2] == "0")
        {
            face.facadeColor = null;
        }
        else
        {
            face.facadeColor = $"{hsv[0]}:{hsv[1]}:{hsv[2]}";
        }

        string inputKey = componentIndex == 0 ? $"FacH_{faceIndex}" : (componentIndex == 1 ? $"FacS_{faceIndex}" : $"FacV_{faceIndex}");
        if (_diceUI != null && _diceUI.Inputs.TryGetValue(inputKey, out var input))
            input.SetTextWithoutNotify(value != 0 ? value.ToString() : "");

        string sliderKey = componentIndex == 0 ? $"SliH_{faceIndex}" : (componentIndex == 1 ? $"SliS_{faceIndex}" : $"SliV_{faceIndex}");
        if (_diceUI != null && _diceUI.Sliders.TryGetValue(sliderKey, out var slider))
            slider.SetValueWithoutNotify(value);

        _onStateChanged?.Invoke();

        if (facadeAutoAssigned) UpdateUIFromData(faceIndex);
    }

    private void IncrementPips(int index)
    {
        var sides = _getDiceSides?.Invoke();
        if (sides != null && index >= 0 && index < sides.Length)
        {
            sides[index].pips++;
            if (_diceUI != null && _diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                input.SetTextWithoutNotify(sides[index].pips.ToString());
            _onStateChanged?.Invoke();
        }
    }
    private void DecrementPips(int index)
    {
        var sides = _getDiceSides?.Invoke();
        if (sides != null && index >= 0 && index < sides.Length)
        {
            sides[index].pips--;
            if (_diceUI != null && _diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                input.SetTextWithoutNotify(sides[index].pips.ToString());
            _onStateChanged?.Invoke();
        }
    }

    // --- LAYOUT GENERATION ---
    public List<GridRowSpec> GenerateLayout(int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        var sides = _getDiceSides?.Invoke();
        if (sides == null) return layout;

        int startIndex = Mathf.Clamp(tabIndex, 0, sides.Length - 1);
        int endIndex = startIndex + 1;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            var face = sides[index];
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            var faceType = GetFaceType(index, face);
            bool allowFac = _allowFacades != null && _allowFacades();

            var faceRows = new List<GridRowSpec>();

            faceRows.Add(BuildFaceHeader(index, faceName));
            faceRows.Add(BuildFaceTypeSelector(index, face, faceType));

            // 1. Base
            if (faceType.HasBaseAndPips) faceRows.Add(BuildBaseRow(index, face));

            // 2. Facade
            if (allowFac) faceRows.AddRange(BuildFacades(index, face));

            // 3. Pips
            if (faceType.HasBaseAndPips) faceRows.Add(BuildPipsRow(index, face));

            // 3.5 HSV
            if (allowFac) faceRows.AddRange(BuildHSV(index, face));

            // 4. Payloads
            // 4. Payloads
            if (faceType.HasStringPayload)
            {
                if (faceType.Id == "sticker")
                {
                    faceRows.Add(BuildTargetSelector(index, face));
                    faceRows.Add(BuildTogtimeRow(index, face));
                    faceRows.AddRange(BuildStickerPayload(index, face, faceType));
                }
                else if (faceType.Id == "enchant") // ADDED: Directs Enchant type to the custom modifier payload builder
                {
                    faceRows.Add(BuildTargetSelector(index, face));
                    faceRows.Add(BuildTogtimeRow(index, face));
                    faceRows.AddRange(BuildEnchantPayload(index, face, faceType));
                }
                else if (faceType.Id == "cast")
                {
                    //faceRows.Add(BuildTargetSelector(index, face)); // no targets, spell handles that.
                    faceRows.AddRange(BuildCastPayload(index, face, faceType));
                }
                else if (faceType.Id == "egg")
                {
                    faceRows.AddRange(BuildEggPayload(index, face, faceType));
                }
                else
                {
                    // Only other payload types (e.g. enchant, egg) get the generic text input field
                    faceRows.Add(BuildStringPayload(index, face, faceType));
                }
            }

            // 5. Shared Footer Elements
            faceRows.Add(BuildSideDescRow(index, face));
            faceRows.AddRange(BuildKeywords(index, face));
            faceRows.Add(BuildClipboardButtons(index));

            var diceBgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f))
            {
                isBackground = true,
                rowSpan = faceRows.Count
            };

            layout.Add(diceBgRow);
            layout.AddRange(faceRows);
        }

        return layout;
    }
    public void UpdateUIFromData(int tabIndex)
    {
        if (_diceUI == null) return;
        var sides = _getDiceSides?.Invoke();
        if (sides == null) return;

        int startIndex = Mathf.Clamp(tabIndex, 0, sides.Length - 1);
        int endIndex = startIndex + 1;

        for (int i = startIndex; i < endIndex; i++)
        {
            var face = sides[i];
            var faceType = GetFaceType(i, face);

            // FIX: Force dropdowns to visually display the correct selections!
            if (_diceUI.Dropdowns.TryGetValue($"FaceTypeDrop_{i}", out var typeDrop))
            {
                int typeIndex = RegisteredPayloads.IndexOf(faceType);
                if (typeIndex >= 0) typeDrop.SetValueWithoutNotify(typeIndex);
            }

            // Keep the custom item selector defaulted to the placeholder message
            if (_diceUI.Dropdowns.TryGetValue($"StickerItemDrop_{i}", out var stickerDrop))
            {
                stickerDrop.SetValueWithoutNotify(0);
            }

            if (_diceUI.Dropdowns.TryGetValue($"EnchantModifierDrop_{i}", out var enchantDrop))
            {
                enchantDrop.SetValueWithoutNotify(0);
            }

            if (_diceUI.Dropdowns.TryGetValue($"CastAbilityDrop_{i}", out var castDrop))
            {
                castDrop.SetValueWithoutNotify(0);
            }

            if (_diceUI.Dropdowns.TryGetValue($"EggSummonDrop_{i}", out var eggDrop))
            {
                eggDrop.SetValueWithoutNotify(0);
            }

            if (faceType.HasBaseAndPips)
            {
                if (_diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
                if (_diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());
            }

            if (faceType.HasStringPayload)
            {
                if (_diceUI.Inputs.TryGetValue($"PayloadData_{i}", out var pData)) pData.SetTextWithoutNotify(GetFacePayload(face));
            }
            if (_diceUI.Inputs.TryGetValue($"SideDesc_{i}", out var dDesc))
            {
                dDesc.SetTextWithoutNotify(face.sidesc ?? "");
            }

            if (_allowFacades != null && _allowFacades())
            {
                if (_diceUI.Inputs.TryGetValue($"Facade_{i}", out var dFac)) dFac.SetTextWithoutNotify(face.facadeID);

                int h = 0, s = 0, v = 0;
                string[] hsv = (face.facadeColor ?? "").Split(':');
                if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
                if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS;
                if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

                if (_diceUI.Sliders.TryGetValue($"SliH_{i}", out var sliH)) sliH.SetValueWithoutNotify(h);
                if (_diceUI.Sliders.TryGetValue($"SliS_{i}", out var sliS)) sliS.SetValueWithoutNotify(s);
                if (_diceUI.Sliders.TryGetValue($"SliV_{i}", out var sliV)) sliV.SetValueWithoutNotify(v);

                if (_diceUI.Inputs.TryGetValue($"FacH_{i}", out var dH)) dH.SetTextWithoutNotify(h != 0 ? h.ToString() : "");
                if (_diceUI.Inputs.TryGetValue($"FacS_{i}", out var dS)) dS.SetTextWithoutNotify(s != 0 ? s.ToString() : "");
                if (_diceUI.Inputs.TryGetValue($"FacV_{i}", out var dV)) dV.SetTextWithoutNotify(v != 0 ? v.ToString() : "");
            }

            if (_diceUI.Dropdowns.TryGetValue($"PayloadTargetDrop_{i}", out var targetDrop))
            {
                int displayVal = face.payloadTarget == null ? 0 : (int)face.payloadTarget.Value + 1;
                targetDrop.SetValueWithoutNotify(displayVal);
            }
        }
    }
    public void UpdateVisuals(int tabIndex)
    {
        if (_diceUI == null || _diceUI.Buttons == null) return;
        var sides = _getDiceSides?.Invoke();
        if (sides == null) return;

        int startIndex = Mathf.Clamp(tabIndex, 0, sides.Length - 1);
        int endIndex = startIndex + 1;

        for (int i = startIndex; i < endIndex; i++)
        {
            var face = sides[i];
            var faceType = GetFaceType(i, face);

            if (faceType.HasBaseAndPips && _diceUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
            {
                Sprite s = _getBaseSprite?.Invoke(face.effectID);
                StaticUI.SetButtonIcon(baseBtn, s);
            }

            if (_allowFacades != null && _allowFacades() && _diceUI.Buttons.TryGetValue($"FacBtn_{i}", out var facBtn))
            {
                Sprite s = _getFacadeSprite?.Invoke(face.facadeID);
                StaticUI.SetButtonIcon(facBtn, s);
            }
        }
    }

    // --- MODULAR ROW BUILDERS ---
    private GridRowSpec BuildBaseRow(int index, DiceSideData face)
    {
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Base:", 0.25f),
            GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.20f, () => _openBaseModal?.Invoke(index)),
            GridCellSpec.CreateLabel("ID:", 0.15f),
            GridCellSpec.CreateInput($"ID_{index}", "ID", 0.40f, (val) =>
            {
                if (string.IsNullOrWhiteSpace(val)) { face.effectID = 0; _onStateChanged?.Invoke(); }
                else if (int.TryParse(val, out int id)) { face.effectID = id; _onStateChanged?.Invoke(); }
            })
        );
    }
    private GridRowSpec BuildPipsRow(int index, DiceSideData face)
    {
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Pips:", 0.25f),
            GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) =>
            {
                if (string.IsNullOrWhiteSpace(val)) { face.pips = 0; _onStateChanged?.Invoke(); }
                else if (int.TryParse(val, out int p)) { face.pips = p; _onStateChanged?.Invoke(); }
            }),
            GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => DecrementPips(index)),
            GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => IncrementPips(index))
        );
    }
    private GridRowSpec BuildFaceHeader(int index, string faceName)
    {
        return new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f));
    }
    private GridRowSpec BuildStringPayload(int index, DiceSideData face, PayloadType faceType)
    {
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Data:", 0.25f),
            GridCellSpec.CreateInput($"PayloadData_{index}", "Enter payload...", 0.75f, (val) =>
            {
                SetFacePayload(face, faceType.Id, val);
                _onStateChanged?.Invoke();
            })
        );
    }
    private List<GridRowSpec> BuildFacades(int index, DiceSideData face)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("Facade:", 0.25f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.20f, () => _openFacadeModal?.Invoke(index)),
                GridCellSpec.CreateLabel("ID:", 0.15f),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.40f, (val) => { face.facadeID = val; _onStateChanged?.Invoke(); })
            )
        };
    }
    private List<GridRowSpec> BuildHSV(int index, DiceSideData face)
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("Hue:", 0.30f),
                GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 0, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateFaceHsv(index, 0, h); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("Sat:", 0.30f),
                GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 1, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateFaceHsv(index, 1, s); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("Val:", 0.30f),
                GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 2, Mathf.RoundToInt(val))),
                GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateFaceHsv(index, 2, v); })
            )
        };
    }
    private List<GridRowSpec> BuildKeywords(int index, DiceSideData face)
    {
        var keywordRows = new List<GridRowSpec>();
        string[] keywordOptions = EntityUIHelpers.GetKeywordOptions();

        keywordRows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, keywordOptions, (val) => AddKeywordToFace(index, val))
        ));

        foreach (var kw in face.keywords)
        {
            string keywordString = kw;
            string coloredLabel = EntityUIHelpers.GetColoredKeywordLabel(keywordString);
            keywordRows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"KwTag_{index}_{keywordString}", coloredLabel, 0.80f),
                GridCellSpec.CreateButton($"KwDel_{index}_{keywordString}", "[X]", 0.20f, () => RemoveKeywordFromFace(index, keywordString))
            ));
        }

        return keywordRows;
    }
    private GridRowSpec BuildClipboardButtons(int index)
    {
        return new GridRowSpec(
            GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.33f, () => CopyDiceFace(index)),
            GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.33f, () => PasteDiceFace(index)),
            GridCellSpec.CreateButton($"BtnClear_{index}", "Clear Dice", 0.33f, () => ClearDiceFace(index))
        );
    }
    private GridRowSpec BuildFaceTypeSelector(int index, DiceSideData face, PayloadType faceType)
    {
        var typeOptions = RegisteredPayloads.Select(t => t.DisplayName).ToArray();
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Face Type:", 0.30f),

            GridCellSpec.CreateFilteredDropdown($"FaceTypeDrop_{index}", faceType.DisplayName, 0.70f, typeOptions, (val) =>
            {
                if (val < 0 || val >= RegisteredPayloads.Count) return;
                string newTypeId = RegisteredPayloads[val].Id;
                if (newTypeId != GetFaceType(index, face).Id)
                {
                    SetFacePayload(face, newTypeId, GetFacePayload(face));
                    if (newTypeId == "egg" && !GetFacePayload(face).Contains("#blindfold"))
                    {
                        SetFacePayload(face, newTypeId, GetFacePayload(face) + "#blindfold");
                    }

                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                }
            })
        );
    }
    private GridRowSpec BuildTargetSelector(int index, DiceSideData face)
    {
        var enumNames = Enum.GetNames(typeof(DiceSideData.PayloadTarget));

        // 1. Dynamically name the first option based on context
        string defaultLabel = $"Default ({DiceSideData.GetInherentDefaultTargetName(face.faceType)})";
        List<string> targetOptions = new List<string> { defaultLabel };

        foreach (var name in enumNames)
        {
            targetOptions.Add(System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2"));
        }

        int currentVisualIndex = face.payloadTarget == null ? 0 : (int)face.payloadTarget.Value + 1;

        return new GridRowSpec(
            GridCellSpec.CreateLabel("Target Override:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"PayloadTargetDrop_{index}", targetOptions[currentVisualIndex], 0.70f, targetOptions.ToArray(), (val) => {
                if (val == 0)
                {
                    face.payloadTarget = null; // Revert to Inherent
                }
                else if (val > 0 && (val - 1) < enumNames.Length)
                {
                    var selectedTarget = (DiceSideData.PayloadTarget)(val - 1);

                    // 2. Optimization: Collapse redundant explicit selections back to null
                    if (DiceSideData.IsTargetInherentDefault(face.faceType, selectedTarget))
                    {
                        face.payloadTarget = null;
                    }
                    else
                    {
                        face.payloadTarget = selectedTarget;
                    }
                }
                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke(); // Rebuild to instantly update the visual dropdown state if collapsed
            })
        );
    }
    private GridRowSpec BuildSideDescRow(int index, DiceSideData face)
    {
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Desc:", 0.25f),
            GridCellSpec.CreateInput($"SideDesc_{index}", "Enter side description...", 0.75f, (val) =>
            {
                // ADDED: Sanitize rich input syntax
                face.sidesc = (val ?? "").SanitizeRichInput();
                _onStateChanged?.Invoke();
            })
        );
    }
    private GridRowSpec BuildTogtimeRow(int index, DiceSideData face)
    {
        string labelText = face.togtime ? "For entire fight" : "for 1 turn";
        return new GridRowSpec(
            GridCellSpec.CreateLabel("Duration:", 0.30f),
            GridCellSpec.CreateButton($"BtnTogtime_{index}", labelText, 0.70f, () => {
                face.togtime = !face.togtime;
                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke();
            })
        );
    }

    // Full Face Builders
    private List<GridRowSpec> BuildStickerPayload(int index, DiceSideData face, PayloadType faceType)
    {
        var rows = new List<GridRowSpec>();

        var customItems = ModPackage.Instance?.CustomItems;
        var itemNames = new List<string> { "-- Select Custom Item --" };

        if (customItems != null)
        {
            itemNames.AddRange(customItems
                .Select(i => !string.IsNullOrEmpty(i.unityName) ? i.unityName : (!string.IsNullOrEmpty(i.entityName) ? i.entityName : "Unnamed Item"))
                .Distinct());
        }

        string currentPayload = GetFacePayload(face);
        bool hasPayload = !string.IsNullOrWhiteSpace(currentPayload);

        // 2. Dropdown (Mirrors Collection Selector behavior)
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Set Item:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"StickerItemDrop_{index}", "-- Select Custom Item --", 0.70f, itemNames.ToArray(), (val) => {
                if (val <= 0 || val >= itemNames.Count) return;

                string selectedName = itemNames[val];
                var targetItem = ModPackage.Instance?.CustomItems?.FirstOrDefault(i => i.unityName == selectedName || i.entityName == selectedName);

                if (targetItem != null)
                {
                    string itemSyntax = targetItem.Export();
                    if (itemSyntax.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
                    {
                        itemSyntax = itemSyntax.Substring(2);
                    }

                    SetFacePayload(face, faceType.Id, itemSyntax);
                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                }
            })
        ));

        // 3. Active entry with [X] button
        if (hasPayload)
        {
            string displayLabel = currentPayload.Length > 25 ? currentPayload.Substring(0, 22) + "..." : currentPayload;

            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"ActiveSticker_{index}", displayLabel, 0.80f),
                GridCellSpec.CreateButton($"DelSticker_{index}", "[X]", 0.20f, () => {
                    SetFacePayload(face, faceType.Id, "");
                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                })
            ));
        }

        return rows;
    }
    private List<GridRowSpec> BuildCastPayload(int index, DiceSideData face, PayloadType faceType)
    {
        var rows = new List<GridRowSpec>();
        bool isCustom = IsCastCustom(index, face);

        // 1. Toggle switch row
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Cast Source:", 0.30f),
            GridCellSpec.CreateButton($"BtnToggleCastType_{index}", isCustom ? "► CUSTOM (Click for Base)" : "► BASE (Click for Custom)", 0.70f, () => ToggleCastType(index, face))
        ));

        string currentPayload = GetFacePayload(face);
        bool hasPayload = !string.IsNullOrWhiteSpace(currentPayload);

        // 2. Render Base Ability Selector
        if (!isCustom)
        {
            var baseAbilities = BaseAbilityDatabase.Abilities;
            var dropdownChoices = new List<string> { "-- Select Base Ability --" };

            if (baseAbilities != null)
            {
                dropdownChoices.AddRange(baseAbilities
                    .Select(a => $"{a.name} ({a.cost}): {(a.effect ?? "").Replace("\n", " | ")}")
                    .ToList());
            }

            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Set Ability:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"CastBaseAbilityDrop_{index}", "-- Select Base Ability --", 0.70f, dropdownChoices.ToArray(), (val) => {
                    if (val <= 0 || val >= dropdownChoices.Count || baseAbilities == null) return;

                    var targetAbility = baseAbilities[val - 1]; // Offset by 1 for placeholder
                    if (targetAbility != null)
                    {
                        SetFacePayload(face, faceType.Id, targetAbility.name);
                        _onStateChanged?.Invoke();
                        _onRebuildRequested?.Invoke();
                    }
                })
            ));

            if (hasPayload)
            {
                var matchingAbility = baseAbilities?.FirstOrDefault(a => a.name == currentPayload);
                string displayLabel = matchingAbility != null ? matchingAbility.name : currentPayload;
                if (displayLabel.Length > 25) displayLabel = displayLabel.Substring(0, 22) + "...";

                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"ActiveCast_{index}", displayLabel, 0.80f),
                    GridCellSpec.CreateButton($"DelCast_{index}", "[X]", 0.20f, () => {
                        SetFacePayload(face, faceType.Id, "");
                        _onStateChanged?.Invoke();
                        _onRebuildRequested?.Invoke();
                    })
                ));
            }
        }
        // 3. Render Custom Ability Selector
        else
        {
            var customAbilities = ModPackage.Instance?.CustomAbilities;
            var abilityNames = new List<string> { "-- Select Custom Ability --" };

            if (customAbilities != null)
            {
                abilityNames.AddRange(customAbilities
                    .Select(a => !string.IsNullOrEmpty(a.entityName) ? a.entityName : "Unnamed Ability")
                    .Distinct());
            }

            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Set Ability:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"CastAbilityDrop_{index}", "-- Select Custom Ability --", 0.70f, abilityNames.ToArray(), (val) => {
                    if (val <= 0 || val >= abilityNames.Count) return;

                    string selectedName = abilityNames[val];
                    var targetAbility = ModPackage.Instance?.CustomAbilities?.FirstOrDefault(a => a.entityName == selectedName);

                    if (targetAbility != null)
                    {
                        string abilitySyntax = targetAbility.Export();

                        if (abilitySyntax.StartsWith("a.", StringComparison.OrdinalIgnoreCase) ||
                            abilitySyntax.StartsWith("c.", StringComparison.OrdinalIgnoreCase))
                        {
                            abilitySyntax = abilitySyntax.Substring(2);
                        }

                        SetFacePayload(face, faceType.Id, abilitySyntax);
                        _onStateChanged?.Invoke();
                        _onRebuildRequested?.Invoke();
                    }
                })
            ));

            if (hasPayload)
            {
                string displayLabel = currentPayload.Length > 25 ? currentPayload.Substring(0, 22) + "..." : currentPayload;

                rows.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"ActiveCast_{index}", displayLabel, 0.80f),
                    GridCellSpec.CreateButton($"DelCast_{index}", "[X]", 0.20f, () => {
                        SetFacePayload(face, faceType.Id, "");
                        _onStateChanged?.Invoke();
                        _onRebuildRequested?.Invoke();
                    })
                ));
            }
        }

        return rows;
    }
    private List<GridRowSpec> BuildEnchantPayload(int index, DiceSideData face, PayloadType faceType)
    {
        var rows = new List<GridRowSpec>();

        var customModifiers = ModPackage.Instance?.CustomModifiers;
        var modifierNames = new List<string> { "-- Select Custom Modifier --" };

        if (customModifiers != null)
        {
            modifierNames.AddRange(customModifiers
                .Select(m => !string.IsNullOrEmpty(m.entityName) ? m.entityName : "Unnamed Modifier")
                .Distinct());
        }

        string currentPayload = GetFacePayload(face);
        bool hasPayload = !string.IsNullOrWhiteSpace(currentPayload);

        // Modifier Dropdown Selection
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Set Modifier:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"EnchantModifierDrop_{index}", "-- Select Custom Modifier --", 0.70f, modifierNames.ToArray(), (val) => {
                if (val <= 0 || val >= modifierNames.Count) return;

                string selectedName = modifierNames[val];
                var targetModifier = ModPackage.Instance?.CustomModifiers?.FirstOrDefault(m => m.entityName == selectedName);

                if (targetModifier != null)
                {
                    string modifierSyntax = targetModifier.Export();
                    if (modifierSyntax.StartsWith("m.", StringComparison.OrdinalIgnoreCase))
                    {
                        modifierSyntax = modifierSyntax.Substring(2);
                    }

                    SetFacePayload(face, faceType.Id, modifierSyntax);
                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                }
            })
        ));

        // Active selection with clear [X] button
        if (hasPayload)
        {
            string displayLabel = currentPayload.Length > 25 ? currentPayload.Substring(0, 22) + "..." : currentPayload;

            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"ActiveEnchant_{index}", displayLabel, 0.80f),
                GridCellSpec.CreateButton($"DelEnchant_{index}", "[X]", 0.20f, () => {
                    SetFacePayload(face, faceType.Id, "");
                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                })
            ));
        }

        return rows;
    }
    private List<GridRowSpec> BuildEggPayload(int index, DiceSideData face, PayloadType faceType)
    {
        var rows = new List<GridRowSpec>();

        string rawPayload = GetFacePayload(face);
        bool isSafe = rawPayload.EndsWith("#blindfold");
        bool killsUser = !isSafe;
        string cleanSummon = isSafe ? rawPayload.Substring(0, rawPayload.Length - 10) : rawPayload;

        // 1. Kill User Toggle
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Kill User?", 0.30f),
            GridCellSpec.CreateButton($"BtnEggKill_{index}", killsUser ? "Yes (Hero dies)" : "No (Adds blindfold)", 0.70f, () => {
                if (killsUser) SetFacePayload(face, faceType.Id, cleanSummon + "#blindfold");
                else SetFacePayload(face, faceType.Id, cleanSummon);

                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke();
            })
        ));

        // 2. Summon Entity Dropdown
        var entityNames = new List<string> { "-- Select Entity to Summon --" };
        if (ModPackage.Instance != null)
        {
            if (ModPackage.Instance.Heroes != null)
                entityNames.AddRange(ModPackage.Instance.Heroes.Select(h => !string.IsNullOrEmpty(h.entityName) ? h.entityName : "Unnamed Hero"));

            if (ModPackage.Instance.Monsters != null)
                entityNames.AddRange(ModPackage.Instance.Monsters.Select(m => !string.IsNullOrEmpty(m.entityName) ? m.entityName : "Unnamed Monster"));

            entityNames = entityNames.Distinct().ToList();
        }

        bool hasCleanPayload = !string.IsNullOrWhiteSpace(cleanSummon);

        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Set Summon:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"EggSummonDrop_{index}", "-- Select Entity to Summon --", 0.70f, entityNames.ToArray(), (val) => {
                if (val <= 0 || val >= entityNames.Count) return;

                string selectedName = entityNames[val];
                SetFacePayload(face, faceType.Id, selectedName + (isSafe ? "#blindfold" : ""));

                _onStateChanged?.Invoke();
                _onRebuildRequested?.Invoke();
            })
        ));

        // 3. Active selection with clear [X] button
        if (hasCleanPayload)
        {
            string displayLabel = cleanSummon.Length > 25 ? cleanSummon.Substring(0, 22) + "..." : cleanSummon;

            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"ActiveEgg_{index}", displayLabel, 0.80f),
                GridCellSpec.CreateButton($"DelEgg_{index}", "[X]", 0.20f, () => {
                    SetFacePayload(face, faceType.Id, isSafe ? "#blindfold" : "");

                    _onStateChanged?.Invoke();
                    _onRebuildRequested?.Invoke();
                })
            ));
        }

        return rows;
    }

    // Payload Getters / Setters
    private string GetFacePayload(DiceSideData face)
    {
        return face.payload ?? "";
    }
    private PayloadType GetFaceType(int faceIndex, DiceSideData face)
    {
        string targetId = "standard";
        switch (face.faceType)
        {
            case DiceSideData.DiceFaceType.Base: targetId = "standard"; break;
            case DiceSideData.DiceFaceType.Sticker: targetId = "sticker"; break;
            case DiceSideData.DiceFaceType.Cast: targetId = "cast"; break;
            case DiceSideData.DiceFaceType.Enchant: targetId = "enchant"; break;
            case DiceSideData.DiceFaceType.Egg: targetId = "egg"; break;
        }
        return RegisteredPayloads.First(p => p.Id == targetId);
    }
    private void SetFacePayload(DiceSideData face, string typeId, string payloadData)
    {
        face.payload = payloadData ?? "";
        switch (typeId)
        {
            case "standard": face.faceType = DiceSideData.DiceFaceType.Base; break;
            case "sticker": face.faceType = DiceSideData.DiceFaceType.Sticker; break;
            case "cast": face.faceType = DiceSideData.DiceFaceType.Cast; break;
            case "enchant": face.faceType = DiceSideData.DiceFaceType.Enchant; break;
            case "egg": face.faceType = DiceSideData.DiceFaceType.Egg; break;
            default: face.faceType = DiceSideData.DiceFaceType.Base; break;
        }
    }
    private bool IsCastCustom(int index, DiceSideData face)
    {
        if (_castIsCustomOverride.TryGetValue(index, out bool val))
        {
            return val;
        }

        // Auto-detect based on active payload
        if (string.IsNullOrEmpty(face.payload))
        {
            return false; // Default to Base
        }

        // If the current payload matches an entity name in custom abilities, treat it as custom
        var customAbilities = ModPackage.Instance?.CustomAbilities;
        if (customAbilities != null && customAbilities.Any(a => a.entityName == face.payload || a.Export().Contains(face.payload)))
        {
            return true;
        }

        return false;
    }
    private void ToggleCastType(int index, DiceSideData face)
    {
        bool nextState = !IsCastCustom(index, face);
        _castIsCustomOverride[index] = nextState;

        // Clear active selection to avoid weird cross-over state
        SetFacePayload(face, "cast", "");

        _onStateChanged?.Invoke();
        _onRebuildRequested?.Invoke();
    }
}