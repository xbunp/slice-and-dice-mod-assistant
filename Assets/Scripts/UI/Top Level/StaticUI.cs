using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public static class ColorPaletteWidget
{
    public class ColorDataBridge
    {
        public Func<int> GetH; public Action<int> SetH;
        public Func<int> GetS; public Action<int> SetS;
        public Func<int> GetV; public Action<int> SetV;

        // Optional bridges for entities that support Phue/Thue
        public Func<Phue> GetPhue; public Action<Phue> SetPhue;
        public Func<Thue> GetThue; public Action<Thue> SetThue;

        public Action OnChanged;
    }

    public static void AppendToLayout(List<GridRowSpec> layout, FullScreenUIGenerator uiGen, ColorDataBridge bridge)
    {
        // 1. Standard HSV Sliders
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Hue:", 0.30f),
            GridCellSpec.CreateSlider("SliH", -99, 99, true, 0.50f, (val) => { bridge.SetH(Mathf.RoundToInt(val)); bridge.OnChanged(); }),
            GridCellSpec.CreateInput("FacH", "H", 0.20f, (val) => { if (int.TryParse(val, out int h)) { bridge.SetH(h); bridge.OnChanged(); } })
        ));

        // ... (Add Saturation and Value rows here exactly once) ...

        // 2. If the entity supports P-Hue / T-Hue, generate them automatically!
        if (bridge.GetPhue != null)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("P-Hue Swap:", 0.30f),
                GridCellSpec.CreateButton("PhueStartBtn", "Target", 0.35f, () => {
                    /* Open Color Picker logic using bridge.GetPhue() */
                })
            // ... rest of Phue/Thue rows ...
            ));
        }
    }
}

/*
public static class EntityModifiersWidget
{
    public static void AppendStandardModifiers<T>(
        List<GridRowSpec> layout,
        T entity,
        Action onChanged,
        Func<T, List<string>> getTraits,
        Func<T, List<string>> getBlessings,
        Func<T, List<string>> getCurses,
        Func<T, List<OrbData>> getOrbs)
    {
        // 1. Traits
        AppendSingleSelector(layout, "Add Trait:", "Trait", SDColors.TraitNiceNames.Keys.ToList(), getTraits(entity),
            (name) => { getTraits(entity).Add(name); onChanged(); },
            (name) => { getTraits(entity).Remove(name); onChanged(); });

        // 2. Blessings
        AppendSingleSelector(layout, "Add Blessing:", "Blessing", BlessingDataset.Blessings.Keys.ToList(), getBlessings(entity),
            (name) => { getBlessings(entity).Add(name); onChanged(); },
            (name) => { getBlessings(entity).Remove(name); onChanged(); });

        // 3. Curses & Orbs automatically appended here...
    }
}
*/

public class SyntaxOutputPanel
{
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;

    public void Initialize(RectTransform parent, FullScreenUIGenerator generator, Func<string> getExportString, Action<string> onPaste)
    {
        // Put all the syntax highlighting, transparent text component, 
        // Copy Button, and Paste Button creation logic here ONCE.
    }

    public void Refresh(string newCompiledText)
    {
        rawTextOutput.SetTextWithoutNotify(newCompiledText);
        syntaxHighlighterText.text = EntityUIHelpers.FormatSyntaxHighlighting(newCompiledText);
    }
}

public static class DiceFaceWidget
{
    // The Bridge: Passes data and specific validation rules from the parent UI
    public class Bridge
    {
        public int FaceIndex;
        public string HeaderLabel;             // e.g., "--- TOP FACE ---" or "PRIMARY EFFECT"
        public Func<DiceSideData> GetFace;     // Gets the data object
        public Action OnChanged;               // Triggers save/rebuild

        // Feature Toggles
        public bool AllowFacades = true;
        public bool AllowCopyPaste = true;
        public bool AllowIndividualHSV = true;

        // Subtle Difference Handlers (Delegates)
        public Action OpenBaseModal;           // Parent defines how the modal opens!
        public Action OpenFacadeModal;
        public Func<int, Sprite> GetBaseSprite;
        public Func<string, Sprite> GetFacadeSprite;
        public Func<string[]> GetKeywordOptions;

        // Optional Copy/Paste Handlers
        public Action OnCopy;
        public Action OnPaste;
        public Action OnClear;
    }

    public static void AppendToLayout(List<GridRowSpec> layout, Bridge bridge)
    {
        var face = bridge.GetFace();
        if (face == null) return;

        int index = bridge.FaceIndex;

        // 1. Calculate height dynamically
        int totalRows = (bridge.AllowFacades ? 8 : 5) + face.keywords.Count;
        var bgRow = new GridRowSpec(GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f))
        {
            isBackground = true,
            rowSpan = totalRows
        };
        layout.Add(bgRow);

        // 2. Header
        string header = string.IsNullOrEmpty(bridge.HeaderLabel) ? $"--- FACE {index} ---" : bridge.HeaderLabel;
        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"LblFaceName_{index}", header, 1.0f)));

        // 3. Base ID & Facade Selection
        if (bridge.AllowFacades)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Base:", 0.15f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, bridge.OpenBaseModal),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => {
                    if (int.TryParse(val, out int id)) { face.effectID = id; bridge.OnChanged(); }
                }),
                GridCellSpec.CreateLabel("Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, bridge.OpenFacadeModal),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { face.facadeID = val; bridge.OnChanged(); })
            ));
        }
        else
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("Base:", 0.25f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.20f, bridge.OpenBaseModal),
                GridCellSpec.CreateLabel("ID:", 0.15f),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.40f, (val) => {
                    if (int.TryParse(val, out int id)) { face.effectID = id; bridge.OnChanged(); }
                })
            ));
        }

        // 4. Pips Row (+/- Buttons)
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Pips:", 0.25f),
            GridCellSpec.CreateInput($"Pips_{index}", "", 0.35f, (val) => {
                if (int.TryParse(val, out int p)) { face.pips = p; bridge.OnChanged(); }
            }),
            GridCellSpec.CreateButton($"BtnPipDown_{index}", "▼", 0.20f, () => { face.pips = Mathf.Max(0, face.pips - 1); bridge.OnChanged(); }),
            GridCellSpec.CreateButton($"BtnPipUp_{index}", "▲", 0.20f, () => { face.pips++; bridge.OnChanged(); })
        ));

        // 5. Individual HSV Sliders (If allowed)
        if (bridge.AllowFacades && bridge.AllowIndividualHSV)
        {
            AppendHsvRow(layout, index, "Hue:", "H", 0, face, bridge);
            AppendHsvRow(layout, index, "Sat:", "S", 1, face, bridge);
            AppendHsvRow(layout, index, "Val:", "V", 2, face, bridge);
        }

        // 6. Keywords
        string[] kwOptions = bridge.GetKeywordOptions?.Invoke() ?? new string[] { "" };
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("Add Keyword:", 0.30f),
            GridCellSpec.CreateFilteredDropdown($"KwDrop_{index}", "", 0.70f, kwOptions, (dropdownVal) => {
                if (dropdownVal > 0 && dropdownVal < kwOptions.Length)
                {
                    string kw = kwOptions[dropdownVal];
                    if (!face.keywords.Contains(kw)) { face.keywords.Add(kw); bridge.OnChanged(); }
                }
            })
        ));

        // Existing Keywords List
        foreach (var kw in face.keywords.ToList())
        {
            string keywordString = kw;
            string coloredLabel = EntityUIHelpers.GetColoredKeywordLabel(keywordString);
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"KwTag_{index}_{keywordString}", coloredLabel, 0.80f),
                GridCellSpec.CreateButton($"KwDel_{index}_{keywordString}", "[X]", 0.20f, () => {
                    face.keywords.Remove(keywordString);
                    bridge.OnChanged();
                })
            ));
        }

        // 7. Copy / Paste / Clear Actions
        if (bridge.AllowCopyPaste)
        {
            layout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.33f, bridge.OnCopy),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.33f, bridge.OnPaste),
                GridCellSpec.CreateButton($"BtnClear_{index}", "Clear Dice", 0.33f, bridge.OnClear)
            ));
        }

        layout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
    }

    private static void AppendHsvRow(List<GridRowSpec> layout, int index, string label, string prefix, int compIdx, DiceSideData face, Bridge bridge)
    {
        // Helper to cleanly keep HSV modification modular inside the widget
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel(label, 0.30f),
            GridCellSpec.CreateSlider($"Sli{prefix}_{index}", -99, 99, true, 0.50f, (val) => {
                UpdateFaceHsvString(face, compIdx, Mathf.RoundToInt(val));
                bridge.OnChanged();
            }),
            GridCellSpec.CreateInput($"Fac{prefix}_{index}", prefix, 0.20f, (val) => {
                if (int.TryParse(val, out int v)) { UpdateFaceHsvString(face, compIdx, v); bridge.OnChanged(); }
            })
        ));
    }

    private static void UpdateFaceHsvString(DiceSideData face, int compIdx, int val)
    {
        string[] partsColor = (face.facadeColor ?? "").Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> hsv = new List<string>(partsColor);
        while (hsv.Count < 3) hsv.Add("0");
        hsv[compIdx] = val.ToString();

        if (hsv[0] == "0" && hsv[1] == "0" && hsv[2] == "0") face.facadeColor = null;
        else face.facadeColor = $"{hsv[0]}:{hsv[1]}:{hsv[2]}";
    }

    /*
    private void BuildPrimaryFaceLayout(List<GridRowSpec> layout, int modeIndex, List<string> validKeywords)
    {
        string targetHint = modeIndex == 2 ? "(Can be targeted)" : (modeIndex == 3 ? "(MUST be untargeted)" : "Target ally or enemy.");

        // INSTANT WIN: One method call builds the entire UI, AND gives abilities Copy/Paste/HSV for free!
        DiceFaceWidget.AppendToLayout(layout, new DiceFaceWidget.Bridge
        {
            FaceIndex = 0,
            HeaderLabel = $"PRIMARY EFFECT {targetHint}",
            GetFace = () => CurrentAbility.diceSides[0],
            OnChanged = () => { NotifyStateChanged(); RebuildAbilityScrollView(); },

            AllowFacades = false, // Abilities don't need facades on faces
            AllowCopyPaste = true, // BUT now they get Copy/Paste for free!

            OpenBaseModal = () => OpenEffectBaseModal(0, isPrimary: true),
            GetBaseSprite = (id) => EntityUIHelpers.GetBaseSprite(id),
            GetKeywordOptions = () => validKeywords.ToArray(),

            OnCopy = () => CopyDiceFace(0),
            OnPaste = () => PasteDiceFace(0),
            OnClear = () => ClearDiceFace(0)
        });
    }

    protected virtual List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var layout = new List<GridRowSpec>();
        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        CurrentEntity.InitializeDiceFaces();

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i; // Local copy for closures

            DiceFaceWidget.AppendToLayout(layout, new DiceFaceWidget.Bridge
            {
                FaceIndex = index,
                HeaderLabel = $"--- {DiceTargetHelper.FaceNames[index].ToUpper()} FACE ---",
                GetFace = () => CurrentEntity.diceSides[index],
                OnChanged = () => { NotifyStateChanged(); RebuildDiceScrollView(); },

                AllowFacades = AllowFacades(),
                AllowCopyPaste = true,

                // Passes the subtle entity differences directly!
                OpenBaseModal = () => OpenBaseModal(index),
                OpenFacadeModal = () => OpenFacadeModal(index),
                GetBaseSprite = (id) => GetBaseDiceSprite(id),
                GetFacadeSprite = (facadeId) => GetFacadeDiceSprite(facadeId),
                GetKeywordOptions = () => EntityUIHelpers.GetKeywordOptions(),

                OnCopy = () => CopyDiceFace(index),
                OnPaste = () => PasteDiceFace(index),
                OnClear = () => ClearDiceFace(index)
            });
        }
        return layout;
    }
    */
}

