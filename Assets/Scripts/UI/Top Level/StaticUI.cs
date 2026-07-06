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

    public List<GridRowSpec> GenerateLayout(int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        string[] keywordOptions = EntityUIHelpers.GetKeywordOptions();
        var sides = _getDiceSides?.Invoke();
        if (sides == null) return layout;

        int startIndex = Mathf.Clamp(tabIndex, 0, sides.Length - 1);
        int endIndex = startIndex + 1;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            var face = sides[index];
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            bool allowFac = _allowFacades != null && _allowFacades();
            int totalFaceRows = (allowFac ? 8 : 5) + face.keywords.Count;

            var diceBgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f));
            diceBgRow.isBackground = true;
            diceBgRow.rowSpan = totalFaceRows;
            layout.Add(diceBgRow);

            layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)));

            if (allowFac)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Base:", 0.15f),
                    GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => _openBaseModal?.Invoke(index)),
                    GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => {
                        if (string.IsNullOrWhiteSpace(val)) { face.effectID = 0; _onStateChanged?.Invoke(); }
                        else if (int.TryParse(val, out int id)) { face.effectID = id; _onStateChanged?.Invoke(); }
                    }),
                    GridCellSpec.CreateLabel("Facade:", 0.15f),
                    GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => _openFacadeModal?.Invoke(index)),
                    GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; _onStateChanged?.Invoke(); })
                ));
            }
            else
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Base:", 0.25f),
                    GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.20f, () => _openBaseModal?.Invoke(index)),
                    GridCellSpec.CreateLabel("ID:", 0.15f),
                    GridCellSpec.CreateInput($"ID_{index}", "ID", 0.40f, (val) => {
                        if (string.IsNullOrWhiteSpace(val)) { face.effectID = 0; _onStateChanged?.Invoke(); }
                        else if (int.TryParse(val, out int id)) { face.effectID = id; _onStateChanged?.Invoke(); }
                    })
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Pips:", 0.25f),
                GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => {
                    if (string.IsNullOrWhiteSpace(val)) { face.pips = 0; _onStateChanged?.Invoke(); }
                    else if (int.TryParse(val, out int p)) { face.pips = p; _onStateChanged?.Invoke(); }
                }),
                GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => {
                    face.pips--;
                    if (_diceUI != null && _diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                        input.SetTextWithoutNotify(face.pips.ToString());
                    _onStateChanged?.Invoke();
                }),
                GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => {
                    face.pips++;
                    if (_diceUI != null && _diceUI.Inputs.TryGetValue($"Pips_{index}", out var input))
                        input.SetTextWithoutNotify(face.pips.ToString());
                    _onStateChanged?.Invoke();
                })
            ));

            if (allowFac)
            {
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Hue:", 0.30f),
                    GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 0, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) UpdateFaceHsv(index, 0, h); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Sat:", 0.30f),
                    GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 1, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { if (int.TryParse(val, out int s)) UpdateFaceHsv(index, 1, s); })
                ));
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel("Val:", 0.30f),
                    GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => UpdateFaceHsv(index, 2, Mathf.RoundToInt(val))),
                    GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { if (int.TryParse(val, out int v)) UpdateFaceHsv(index, 2, v); })
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
                GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, keywordOptions, (val) => AddKeywordToFace(index, val))
            ));

            foreach (var kw in face.keywords)
            {
                string keywordString = kw;
                string coloredLabel = EntityUIHelpers.GetColoredKeywordLabel(keywordString);
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"KwTag_{index}_{keywordString}", coloredLabel, 0.80f),
                    GridCellSpec.CreateButton($"KwDel_{index}_{keywordString}", "[X]", 0.20f, () => RemoveKeywordFromFace(index, keywordString))
                ));
            }

            layout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.33f, () => CopyDiceFace(index)),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.33f, () => PasteDiceFace(index)),
                GridCellSpec.CreateButton($"BtnClear_{index}", "Clear Dice", 0.33f, () => ClearDiceFace(index))
            ));
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
            if (_diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.SetTextWithoutNotify(face.effectID.ToString());
            if (_diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.SetTextWithoutNotify(face.pips.ToString());

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

            if (_diceUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
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
}