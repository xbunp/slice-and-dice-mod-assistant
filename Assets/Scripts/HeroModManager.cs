using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SDColors;

[RequireComponent(typeof(FullScreenUIGenerator))]
public class HeroModManager : MonoBehaviour
{
    private FullScreenUIGenerator uiGenerator;
    private HeroData currentHero;
    private bool isSyncing = false;

    [Header("Right Column Prefabs")]
    public GameObject framedIconPrefab; // Prefab with FramedIcon script
    public GameObject dicePreviewPrefab; // Prefab with DicePreview script

    [Header("Dynamically Generated Components")]
    // References to dynamically generated components
    private GridReferences statsUI;
    private GridReferences diceUI;
    private TMP_InputField rawTextOutput;
    private DicePreview dicePreview;
    private FramedIcon heroIcon;
    [Space]
    private Image heroIconBackground;
    private TextMeshProUGUI syntaxHighlighterText;

    // Internal shit. 
    private Sprite[] atlasSprites;
    private GeneratedScreen currentScreen = new GeneratedScreen();

    [Header("UI Modal References")]
    [SerializeField] private IconPickerModal diceFaceIconPicker;

    // Sprite Caches
    private Sprite[] _baseActionSprites;
    private Dictionary<int, string> _baseActionNames;

    private Sprite[] _allActionSprites;
    private Dictionary<int, string> _allActionNames;

    private int currentDiceTab = 0; // 0 = All, 1 = Left, 2 = Middle, etc.
    private ScrollRect diceScrollRect;

    private Dictionary<int, List<string>> cleanDropdownKeywords = new Dictionary<int, List<string>>();

    // Clipboard storage for per-face Copy/Paste operations
    private int copiedEffectID;
    private int copiedPips;
    private string copiedFacadeID;
    private string copiedFacadeColor;
    private List<string> copiedKeywords;
    private bool hasCopiedDiceData = false;

    void Start()
    {
        uiGenerator = GetComponent<FullScreenUIGenerator>();
        currentHero = new HeroData();

        LoadAllSprites();
        BuildUIAndBind();

        UpdateUIFromData();
        GenerateRawText();
    }

    private void LoadAllSprites()
    {
        Sprite[] baseAtlas = SpriteCache.GetBaseSprites();
        Sprite[] commAtlas = SpriteCache.GetCommunitySprites();

        List<Sprite> allSp = new List<Sprite>();
        allSp.AddRange(baseAtlas);
        allSp.AddRange(commAtlas);

        List<Sprite> basSp = new List<Sprite>();

        _allActionSprites = allSp.ToArray();
        _allActionNames = new Dictionary<int, string>();

        for (int i = 0; i < _allActionSprites.Length; i++)
        {
            string sName = _allActionSprites[i].name;
            _allActionNames[i] = sName;

            // Gather only "bas" items for the Base Actions filter
            if (sName.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
            {
                basSp.Add(_allActionSprites[i]);
            }
        }

        _baseActionSprites = basSp.ToArray();
        _baseActionNames = new Dictionary<int, string>();
        for (int i = 0; i < _baseActionSprites.Length; i++)
        {
            _baseActionNames[i] = _baseActionSprites[i].name;
        }
    }

    private void BuildUIAndBind()
    {
        string[] heroNamesList = Enum.GetNames(typeof(HeroType));

        // 1. Core Stats Layout Specification
        var statsLayout = new List<GridRowSpec>
        {
            // Row 1: Name
            new GridRowSpec(
                GridCellSpec.CreateLabel("Hero Name Label", "Hero Name:", 0.35f),
                GridCellSpec.CreateInput("Name", "", 0.65f, (val) => { currentHero.heroName = val; OnUIChanged(); })
            ),
            // Row 2: Base Class
            new GridRowSpec(
                GridCellSpec.CreateLabel("Base Class Label", "Replica Base:", 0.35f),
                GridCellSpec.CreateDropdown("Replica", "", 0.65f, heroNamesList, (val) => {
                    currentHero.baseReplica = heroNamesList[val];
                    OnUIChanged();
                })
            ),
            // Row 3: Image Override
            new GridRowSpec(
                GridCellSpec.CreateLabel("Image Override Label", "Icon Override:", 0.35f),
                GridCellSpec.CreateDropdown("ImageOverride", "", 0.65f, heroNamesList, (val) => {
                    currentHero.imageOverride = heroNamesList[val];
                    OnUIChanged();
                })
            ),
            // Row 4: Color Class
            new GridRowSpec(
                GridCellSpec.CreateLabel("Color Label", "Color Class:", 0.35f),
                GridCellSpec.CreateDropdown("Color", "", 0.65f, SDColors.GetFormattedColorNames(), (val) => {
                    HeroColorOption selectedColor = (HeroColorOption)val;
                    currentHero.colorClass = SDColors.GetCode(selectedColor);
                    heroIcon.frame.color = SDColors.GetColor(selectedColor);
                    OnUIChanged();
                })
            ),
            // Row 5: HP & Tier
            new GridRowSpec(
                GridCellSpec.CreateLabel("HP Label", "HP:", 0.2f),
                GridCellSpec.CreateInput("HP", "", 0.3f, (val) => { if (int.TryParse(val, out int hp)) currentHero.hp = hp; OnUIChanged(); }),
                GridCellSpec.CreateLabel("Tier Label", "Tier:", 0.2f),
                GridCellSpec.CreateInput("Tier", "", 0.3f, (val) => { if (int.TryParse(val, out int t)) currentHero.tier = t; OnUIChanged(); })
            ),

            // Row 6: RESET BUTTON
            new GridRowSpec(
                GridCellSpec.CreateButton("BtnReset", "Reset All to Default", 1.0f, ResetToDefault)
            )
        };

        // 2. Middle Column: Tabs + ScrollView Base
        List<string> tabNames = new List<string> { "All", "Left", "Middle", "Top", "Bottom", "Right", "Rightmost" };
        var middleBaseLayout = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateNavigationTabs("DiceTabs", tabNames, new List<GameObject>(), 1.0f, OnDiceTabSelected)),
            new GridRowSpec(600f, GridCellSpec.CreateScrollView("DiceScrollView", 1.0f))
        };

        // 3. Define the column split mapping
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftStats", 0.02f, 0.35f, statsLayout),
            new ColumnSpec("MiddleDiceBase", 0.38f, 0.68f, middleBaseLayout),
            new ColumnSpec("RightOutput", 0.71f, 0.98f)
        };

        GeneratedScreen screen = uiGenerator.SetupScreen(columns);
        currentScreen = screen;
        statsUI = currentScreen.ColumnRefs["LeftStats"];

        // Grab the ScrollView so we can inject the dice UI into it
        var middleRefs = currentScreen.ColumnRefs["MiddleDiceBase"];
        diceScrollRect = middleRefs.ScrollViews["DiceScrollView"];

        // Initial Build of the Dice Content
        RebuildDiceScrollView();

        if (screen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }

        if (diceFaceIconPicker != null)
        {
            diceFaceIconPicker.transform.SetAsLastSibling();
        }
    }

    private void ResetToDefault()
    {
        // Reset the data model to a brand new state
        currentHero = new HeroData();

        // Ensure lists are initialized
        for (int i = 0; i < 6; i++)
        {
            if (currentHero.diceSides[i].keywords == null)
                currentHero.diceSides[i].keywords = new List<string>();
        }

        // Rebuild entire UI, map data, and regenerate text
        RefreshDiceUI();
    }

    private void OnDiceTabSelected(int index)
    {
        currentDiceTab = index;

        // Guard: Prevent rebuilding/updating before the initial screen setup is finished
        if (statsUI == null) return;

        RefreshDiceUI();
    }

    private void RebuildDiceScrollView()
    {
        if (diceScrollRect == null) return;

        var diceLayout = GenerateDiceLayout(currentDiceTab);
        diceUI = uiGenerator.RebuildGrid(diceScrollRect.content, diceLayout);

        // Completely relies on the precise hierarchy calculation from BuildGrid 
        diceScrollRect.content.sizeDelta = new Vector2(0, diceUI.TotalHeight);
    }

    /*
    private List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var diceLayout = new List<GridRowSpec>();
        string[] defaultKwOptions = GetDefaultKeywordOptions();

        // If index is 0 ("All"), show 0-5. Otherwise, show specific face (tabIndex - 1).
        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            // Row 1: ImagePanel Background for the Dice Block (Spans 6 layout blocks vertically)
            var diceBgRow = new GridRowSpec(
                GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f, new Color(0.15f, 0.15f, 0.15f, 1f))
            );
            diceBgRow.isBackground = true;
            diceBgRow.rowSpan = 6;
            diceLayout.Add(diceBgRow);

            // Row 2: Name (Alone on its own row)
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)
            ));

            // Row 3: Base image and Facade image side by side
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblBase_{index}", "Base:", 0.15f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index)),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { currentHero.diceSides[index].effectID = id; UpdateDiceIcon(index); OnUIChanged(); } }),

                GridCellSpec.CreateLabel($"LblFac_{index}", "Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { currentHero.diceSides[index].facadeID = val; UpdateDiceIcon(index); OnUIChanged(); })
            ));

            // Row 4: Pips (Shifted to its own row for spacing)
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblPip_{index}", "Pips:", 0.30f),
                GridCellSpec.CreateInput($"Pips_{index}", "Pips amount", 0.70f, (val) => { if (int.TryParse(val, out int p)) { currentHero.diceSides[index].pips = p; OnUIChanged(); } })
            ));

            // Rows 5-7: HSV Sliders separated
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblHue_{index}", "Hue:", 0.30f),
                GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 0, val); }),
                GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { UpdateHsvInput(index, 0, val); })
            ));

            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblSat_{index}", "Saturation:", 0.30f),
                GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 1, val); }),
                GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { UpdateHsvInput(index, 1, val); })
            ));

            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblVal_{index}", "Value:", 0.30f),
                GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 2, val); }),
                GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { UpdateHsvInput(index, 2, val); })
            ));

            // Fetch current active keywords for this face to calculate visual height requirement
            var activeKeywords = currentHero.diceSides[index].keywords;

            // Row 8: ImagePanel Background for the Keywords Block (Dropdown row + 1 row per active keyword)
            int keywordRowCount = 1; // Base dropdown row
            if (activeKeywords != null && activeKeywords.Count > 0)
            {
                keywordRowCount += activeKeywords.Count;
            }

            var kwBgRow = new GridRowSpec(
                GridCellSpec.CreateImagePanel($"BgKw_{index}", 1.0f, new Color(0.2f, 0.2f, 0.2f, 1f))
            );
            kwBgRow.isBackground = true;
            kwBgRow.rowSpan = keywordRowCount;
            diceLayout.Add(kwBgRow);

            // Row 9: Keywords Dropdown
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblKw_{index}", "Add Keyword:", 0.30f),
                GridCellSpec.CreateDropdown($"KwDrop_{index}", "", 0.70f, defaultKwOptions, (val) => AddKeywordFromDropdown(index, val))
            ));

            // Row 10+: Keyword entries (1 per row)
            if (activeKeywords != null && activeKeywords.Count > 0)
            {
                foreach (string kw in activeKeywords)
                {
                    string displayLabel = kw;
                    if (System.Enum.TryParse(kw, true, out EffectKeyword parsedKw))
                    {
                        if (EffectKeywordColors.Map.TryGetValue(parsedKw, out Color colorValue))
                        {
                            string hex = ColorUtility.ToHtmlStringRGB(colorValue);
                            displayLabel = $"<color=#{hex}>{kw}</color>";
                        }
                    }

                    diceLayout.Add(new GridRowSpec(
                        GridCellSpec.CreateLabel($"KwTag_{index}_{kw}", displayLabel, 0.80f),
                        GridCellSpec.CreateButton($"KwDel_{index}_{kw}", "[X]", 0.20f, () => RemoveKeyword(index, kw))
                    ));
                }
            }

            // Row Last: Copy/Paste Buttons
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.50f, () => {  Add copy functionality here *}),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.50f, () => {  Add paste functionality here  })
            ));

            // Layout spacer if displaying ALL dice
            if (tabIndex == 0 && i < 5)
            {
                diceLayout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
            }
        }
        return diceLayout;
    }
*/
    /*
    private List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var diceLayout = new List<GridRowSpec>();
        string[] defaultKwOptions = GetDefaultKeywordOptions();

        // If index is 0 ("All"), show 0-5. Otherwise, show specific face (tabIndex - 1).
        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            int index = i;
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            // Row 1: ImagePanel Background for the Dice Block
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f, new Color(0.15f, 0.15f, 0.15f, 1f))
            ));

            // Row 2: Name (Alone on its own row)
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)
            ));

            // Row 3: Base image and Facade image side by side
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblBase_{index}", "Base:", 0.15f),
                GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index)),
                GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) => { if (int.TryParse(val, out int id)) { currentHero.diceSides[index].effectID = id; UpdateDiceIcon(index); OnUIChanged(); } }),

                GridCellSpec.CreateLabel($"LblFac_{index}", "Facade:", 0.15f),
                GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
                GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) => { currentHero.diceSides[index].facadeID = val; UpdateDiceIcon(index); OnUIChanged(); })
            ));

            // Row 4: Pips (Shifted to its own row for spacing)
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblPip_{index}", "Pips:", 0.30f),
                GridCellSpec.CreateInput($"Pips_{index}", "Pips amount", 0.70f, (val) => { if (int.TryParse(val, out int p)) { currentHero.diceSides[index].pips = p; OnUIChanged(); } })
            ));

            // Rows 5-7: HSV Sliders separated
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblHue_{index}", "Hue:", 0.30f),
                GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 0, val); }),
                GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { UpdateHsvInput(index, 0, val); })
            ));

            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblSat_{index}", "Saturation:", 0.30f),
                GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 1, val); }),
                GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { UpdateHsvInput(index, 1, val); })
            ));

            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblVal_{index}", "Value:", 0.30f),
                GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 2, val); }),
                GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { UpdateHsvInput(index, 2, val); })
            ));

            // Row 8: ImagePanel Background for the Keywords Block
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateImagePanel($"BgKw_{index}", 1.0f, new Color(0.2f, 0.2f, 0.2f, 1f))
            ));

            // Row 9: Keywords Dropdown
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"LblKw_{index}", "Add Keyword:", 0.30f),
                GridCellSpec.CreateDropdown($"KwDrop_{index}", "", 0.70f, defaultKwOptions, (val) => AddKeywordFromDropdown(index, val))
            ));

            // Row 10+: Keyword entries (1 per row)
            var activeKeywords = currentHero.diceSides[index].keywords;
            if (activeKeywords != null && activeKeywords.Count > 0)
            {
                foreach (string kw in activeKeywords)
                {
                    string displayLabel = kw;
                    if (System.Enum.TryParse(kw, true, out EffectKeyword parsedKw))
                    {
                        if (EffectKeywordColors.Map.TryGetValue(parsedKw, out Color colorValue))
                        {
                            string hex = ColorUtility.ToHtmlStringRGB(colorValue);
                            displayLabel = $"<color=#{hex}>{kw}</color>";
                        }
                    }

                    diceLayout.Add(new GridRowSpec(
                        GridCellSpec.CreateLabel($"KwTag_{index}_{kw}", displayLabel, 0.80f),
                        GridCellSpec.CreateButton($"KwDel_{index}_{kw}", "[X]", 0.20f, () => RemoveKeyword(index, kw))
                    ));
                }
            }

            // Row Last: Copy/Paste Buttons
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.50f, () => { /* Add copy functionality here  }),
                GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.50f, () => { /* Add paste functionality here })
            ));

            // Layout spacer if displaying ALL dice
            if (tabIndex == 0 && i < 5)
            {
                diceLayout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
            }
        }
        return diceLayout;
    }
*/

    //==================================================================================================

    private List<GridRowSpec> GenerateDiceLayout(int tabIndex)
    {
        var diceLayout = new List<GridRowSpec>();
        string[] defaultKwOptions = GetDefaultKeywordOptions(); // Unchanged!

        int startIndex = (tabIndex == 0) ? 0 : tabIndex - 1;
        int endIndex = (tabIndex == 0) ? 6 : tabIndex;

        for (int i = startIndex; i < endIndex; i++)
        {
            // Set the default clean mapping for this face index directly in the loop
            cleanDropdownKeywords[i] = new List<string>(Enum.GetNames(typeof(EffectKeyword)));

            // Passing 'i' as a parameter isolates the index variable for lambda closures.
            AddFaceLayout(diceLayout, i, defaultKwOptions);

            // Layout spacer if displaying ALL dice
            if (tabIndex == 0 && i < 5)
            {
                diceLayout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{i}", "", 1.0f)));
            }
        }
        return diceLayout;
    }

    private void AddFaceLayout(List<GridRowSpec> layout, int index, string[] defaultKwOptions)
    {
        string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

        AddFaceBackground(layout, index);
        AddFaceHeader(layout, index, faceName);
        AddBaseAndFacadeControls(layout, index);
        AddPipsControl(layout, index);
        AddHsvSliders(layout, index);
        AddKeywordControls(layout, index, defaultKwOptions);
        AddCopyPasteControls(layout, index);
    }

    private void AddFaceBackground(List<GridRowSpec> layout, int index)
    {
        // Row 1: ImagePanel Background for the Dice Block (Spans 6 layout blocks vertically)
        var diceBgRow = new GridRowSpec(
            GridCellSpec.CreateImagePanel($"BgDice_{index}", 1.0f, new Color(0.15f, 0.15f, 0.15f, 1f))
        );
        diceBgRow.isBackground = true;
        diceBgRow.rowSpan = 6;
        layout.Add(diceBgRow);
    }

    private void AddFaceHeader(List<GridRowSpec> layout, int index, string faceName)
    {
        // Row 2: Name (Alone on its own row)
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblFaceName_{index}", $"--- {faceName} FACE ---", 1.0f)
        ));
    }

    private void AddBaseAndFacadeControls(List<GridRowSpec> layout, int index)
    {
        // Row 3: Base image and Facade image side by side
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblBase_{index}", "Base:", 0.15f),
            GridCellSpec.CreateDiceButton($"BaseBtn_{index}", "B", 0.10f, () => OpenBaseModal(index)),
            GridCellSpec.CreateInput($"ID_{index}", "ID", 0.20f, (val) =>
            {
                if (int.TryParse(val, out int id))
                {
                    currentHero.diceSides[index].effectID = id;
                    UpdateDiceIcon(index);
                    OnUIChanged();
                }
            }),

            GridCellSpec.CreateLabel($"LblFac_{index}", "Facade:", 0.15f),
            GridCellSpec.CreateDiceButton($"FacBtn_{index}", "F", 0.10f, () => OpenFacadeModal(index)),
            GridCellSpec.CreateInput($"Facade_{index}", "ID", 0.30f, (val) =>
            {
                currentHero.diceSides[index].facadeID = val;
                UpdateDiceIcon(index);
                OnUIChanged();
            })
        ));
    }

    private void AddPipsControl(List<GridRowSpec> layout, int index)
    {
        // Row 4: Pips (Shifted to its own row for spacing)
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblPip_{index}", "Pips:", 0.30f),
            GridCellSpec.CreateInput($"Pips_{index}", "Pips amount", 0.70f, (val) =>
            {
                if (int.TryParse(val, out int p))
                {
                    currentHero.diceSides[index].pips = p;
                    OnUIChanged();
                }
            })
        ));
    }

    private void AddHsvSliders(List<GridRowSpec> layout, int index)
    {
        // Rows 5-7: HSV Sliders separated
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblHue_{index}", "Hue:", 0.30f),
            GridCellSpec.CreateSlider($"SliH_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 0, val); }),
            GridCellSpec.CreateInput($"FacH_{index}", "H", 0.20f, (val) => { UpdateHsvInput(index, 0, val); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblSat_{index}", "Saturation:", 0.30f),
            GridCellSpec.CreateSlider($"SliS_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 1, val); }),
            GridCellSpec.CreateInput($"FacS_{index}", "S", 0.20f, (val) => { UpdateHsvInput(index, 1, val); })
        ));

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblVal_{index}", "Value:", 0.30f),
            GridCellSpec.CreateSlider($"SliV_{index}", -99, 99, true, 0.50f, (val) => { UpdateHsvFromSlider(index, 2, val); }),
            GridCellSpec.CreateInput($"FacV_{index}", "V", 0.20f, (val) => { UpdateHsvInput(index, 2, val); })
        ));
    }

    private void AddKeywordControls(List<GridRowSpec> layout, int index, string[] defaultKwOptions)
    {
        var activeKeywords = currentHero.diceSides[index].keywords;

        // Row 8: ImagePanel Background for the Keywords Block (Dropdown row + 1 row per active keyword)
        int keywordRowCount = 1; // Base dropdown row
        if (activeKeywords != null && activeKeywords.Count > 0)
        {
            keywordRowCount += activeKeywords.Count;
        }

        var kwBgRow = new GridRowSpec(
            GridCellSpec.CreateImagePanel($"BgKw_{index}", 1.0f, new Color(0.2f, 0.2f, 0.2f, 1f))
        );
        kwBgRow.isBackground = true;
        kwBgRow.rowSpan = keywordRowCount;
        layout.Add(kwBgRow);

        // Row 9: Keywords Dropdown
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"LblKw_{index}", "Add Keyword:", 0.30f),
            GridCellSpec.CreateDropdown($"KwDrop_{index}", "", 0.70f, defaultKwOptions, (val) => AddKeywordFromDropdown(index, val))
        ));

        // Row 10+: Keyword entries (1 per row)
        if (activeKeywords != null && activeKeywords.Count > 0)
        {
            foreach (string kw in activeKeywords)
            {
                string displayLabel = kw;
                if (System.Enum.TryParse(kw, true, out EffectKeyword parsedKw))
                {
                    if (EffectKeywordColors.Map.TryGetValue(parsedKw, out Color colorValue))
                    {
                        string hex = ColorUtility.ToHtmlStringRGB(colorValue);
                        displayLabel = $"<color=#{hex}>{kw}</color>";
                    }
                }

                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"KwTag_{index}_{kw}", displayLabel, 0.80f),
                    GridCellSpec.CreateButton($"KwDel_{index}_{kw}", "[X]", 0.20f, () => RemoveKeyword(index, kw))
                ));
            }
        }
    }

    private void AddCopyPasteControls(List<GridRowSpec> layout, int index)
    {
        // Row Last: Copy/Paste Buttons
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateButton($"BtnCopy_{index}", "Copy Dice", 0.50f, () => CopyDiceFace(index)),
            GridCellSpec.CreateButton($"BtnPaste_{index}", "Paste Dice", 0.50f, () => PasteDiceFace(index))
        ));
    }

    //===================================================================================================

    private void CopyDiceFace(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= currentHero.diceSides.Length) return;

        var source = currentHero.diceSides[sourceIndex];

        copiedEffectID = source.effectID;
        copiedPips = source.pips;
        copiedFacadeID = source.facadeID;
        copiedFacadeColor = source.facadeColor;

        // Perform a deep copy of the keyword list
        copiedKeywords = new List<string>(source.keywords ?? new List<string>());
        hasCopiedDiceData = true;
    }

    private void PasteDiceFace(int targetIndex)
    {
        if (!hasCopiedDiceData || targetIndex < 0 || targetIndex >= currentHero.diceSides.Length) return;

        var target = currentHero.diceSides[targetIndex];

        target.effectID = copiedEffectID;
        target.pips = copiedPips;
        target.facadeID = copiedFacadeID;
        target.facadeColor = copiedFacadeColor;

        // Deep copy from the clipboard back into the target list
        target.keywords = new List<string>(copiedKeywords ?? new List<string>());

        // Rebuild and refresh the UI. 
        // GenerateRawText() will run automatically, instantly converting the serialized 
        // face identifiers (e.g. ".i.left" -> ".i.right") to match the target index.
        RefreshDiceUI();
    }

    private void OpenBaseModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;

        diceFaceIconPicker.OpenModal(_baseActionSprites, _baseActionNames, (index, sprite) =>
        {
            string filename = _baseActionNames[index]; // e.g. "bas_15_dmg"
            string[] parts = filename.Split('_');

            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
            {
                currentHero.diceSides[faceIndex].effectID = parsedId;
                RefreshDiceUI();
            }
        });
    }

    private void OpenFacadeModal(int faceIndex)
    {
        if (diceFaceIconPicker == null) return;

        diceFaceIconPicker.OpenModal(_allActionSprites, _allActionNames, (index, sprite) =>
        {
            string filename = _allActionNames[index]; // e.g. "Che_6_dmgFlame"
            string[] parts = filename.Split('_');

            if (parts.Length >= 2)
            {
                // Joins the prefix "Che" and the number "6" -> "Che6"
                string facadeStr = $"{parts[0]}{parts[1]}";
                currentHero.diceSides[faceIndex].facadeID = facadeStr;
                RefreshDiceUI();
            }
        });
    }

    private void RefreshDiceUI()
    {
        // 1. Rebuild ONLY the inside of the scroll view, leaving Tabs & Scrollrect intact
        RebuildDiceScrollView();

        // 2. Populate the newly spawned input fields with your current data
        UpdateUIFromData();

        // 3. Fire the standard text output updates
        OnUIChanged();
    }

    private void BuildRightPanelContent(RectTransform parent)
    {
        // 1. Create a container for the side-by-side preview block (Top 25% of the panel)
        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform));
        previewContainer.transform.SetParent(parent, false);
        RectTransform containerRt = previewContainer.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(containerRt, 0.05f, 0.7f, 0.95f, 0.95f);

        // 2. Instantiate Hero Icon (Left: 0% to 33%)
        GameObject iconObj = Instantiate(framedIconPrefab, containerRt);
        heroIcon = iconObj.GetComponent<FramedIcon>();
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(iconRt, 0.0f, 0.0f, 0.33f, 1.0f);

        // 3. Instantiate Dice Preview (Right: 38% to 100%)
        GameObject diceObj = Instantiate(dicePreviewPrefab, containerRt);
        dicePreview = diceObj.GetComponent<DicePreview>();
        RectTransform diceRt = diceObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(diceRt, 0.38f, 0.0f, 1.0f, 1.0f);

        // 4. Label (Middle portion of the panel)
        GameObject labelObj = Instantiate(uiGenerator.labelPrefab, parent);
        labelObj.GetComponentInChildren<TextMeshProUGUI>().text = "Slice & Dice Hero String:";
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(labelRt, 0.0f, 0.6f, 1.0f, 0.65f);

        // 5. Code MultiLine Box (Bottom portion of the panel)
        GameObject inputObj = Instantiate(uiGenerator.inputFieldPrefab, parent);
        var innerLabel = inputObj.GetComponentInChildren<TextMeshProUGUI>();
        if (innerLabel != null) Destroy(innerLabel.gameObject);

        rawTextOutput = inputObj.GetComponentInChildren<TMP_InputField>();
        rawTextOutput.lineType = TMP_InputField.LineType.MultiLineNewline;

        // --- SYNTAX HIGHLIGHTING SETUP ---
        rawTextOutput.textComponent.color = Color.clear; // Keep raw text completely invisible
        rawTextOutput.customCaretColor = true;
        rawTextOutput.caretColor = Color.white;

        // CRITICAL: Disable Rich Text on the selectable input box.
        rawTextOutput.richText = false;

        // Force Auto Sizing OFF on the input field text component
        rawTextOutput.textComponent.enableAutoSizing = false;
        rawTextOutput.pointSize = 16;
        rawTextOutput.textComponent.autoSizeTextContainer = false;

        GameObject highlighterObj = Instantiate(uiGenerator.labelPrefab, rawTextOutput.textComponent.transform.parent);
        highlighterObj.name = "SyntaxHighlighter";
        syntaxHighlighterText = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();

        // Make the colored highlighter completely click-through & non-selectable
        var canvasGroup = highlighterObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        // Strip custom scripts and layout controllers
        foreach (var script in highlighterObj.GetComponents<MonoBehaviour>())
        {
            if (script != null && !(script is TextMeshProUGUI))
            {
                DestroyImmediate(script);
            }
        }

        RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
        RectTransform textCompRt = rawTextOutput.textComponent.GetComponent<RectTransform>();
        highlightRt.anchorMin = textCompRt.anchorMin;
        highlightRt.anchorMax = textCompRt.anchorMax;
        highlightRt.offsetMin = textCompRt.offsetMin;
        highlightRt.offsetMax = textCompRt.offsetMax;
        highlightRt.pivot = textCompRt.pivot;

        // Force Auto Sizing OFF on the highlighter and set size to 16
        syntaxHighlighterText.enableAutoSizing = false;
        syntaxHighlighterText.fontSize = 16;

        syntaxHighlighterText.alignment = rawTextOutput.textComponent.alignment;
        syntaxHighlighterText.margin = rawTextOutput.textComponent.margin;
        syntaxHighlighterText.enableWordWrapping = rawTextOutput.textComponent.enableWordWrapping;
        syntaxHighlighterText.autoSizeTextContainer = false;

        // Let ONLY the background overlay handle the color tags
        syntaxHighlighterText.richText = true;
        // ----------------------------------

        // Adjust input height to end at 0.08f to make room for buttons below
        RectTransform inputRt = inputObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(inputRt, 0.0f, 0.08f, 1.0f, 0.58f);

        rawTextOutput.onValueChanged.AddListener(OnRawTextChanged);

        // 6. Copy & Paste Backup Buttons (Anchored at the absolute bottom 0.0f to 0.06f)
        GameObject copyBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        copyBtnObj.name = "BtnCopyClipboard";
        var copyText = copyBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (copyText != null) copyText.text = "Copy String";
        Button copyBtn = copyBtnObj.GetComponentInChildren<Button>();

        // UPDATE: Call the platform-safe clipboard helper
        if (copyBtn != null) copyBtn.onClick.AddListener(() => CopyStringToClipboard(rawTextOutput.text));

        RectTransform copyRt = copyBtnObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(copyRt, 0.0f, 0.0f, 0.48f, 0.06f);

        GameObject pasteBtnObj = Instantiate(uiGenerator.buttonPrefab, parent);
        pasteBtnObj.name = "BtnPasteClipboard";
        var pasteText = pasteBtnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (pasteText != null) pasteText.text = "Paste String";
        Button pasteBtn = pasteBtnObj.GetComponentInChildren<Button>();
        if (pasteBtn != null) pasteBtn.onClick.AddListener(() => {
            rawTextOutput.text = GUIUtility.systemCopyBuffer;
            OnRawTextChanged(rawTextOutput.text);
        });
        RectTransform pasteRt = pasteBtnObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(pasteRt, 0.52f, 0.0f, 1.0f, 0.06f);
    }

    private void OnUIChanged()
    {
        if (isSyncing) return;
        GenerateRawText();
        UpdateHeroIcon();
    }

    private void OnRawTextChanged(string rawText)
    {
        if (isSyncing) return;

        // Colorize user input in real-time
        syntaxHighlighterText.text = FormatSyntaxHighlighting(rawText);

        // 1. Update the Data Model from the string
        ParseRawText(rawText);

        // 2. Rebuild the visual layout to accommodate new/removed keyword rows
        RebuildDiceScrollView();

        // 3. Populate the newly rebuilt layout with the parsed Data Model
        // (We do NOT call RefreshDiceUI() because that triggers OnUIChanged() -> GenerateRawText(), 
        // which would forcefully overwrite the text box the user is currently typing in!)
        UpdateUIFromData();
    }

    private void UpdateUIFromData()
    {
        if (statsUI == null || diceUI == null) return;

        isSyncing = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.text = currentHero.heroName;
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn)) hpIn.text = currentHero.hp.ToString();
        if (statsUI.Inputs.TryGetValue("Tier", out var tierIn)) tierIn.text = currentHero.tier.ToString();

        if (statsUI.Dropdowns.TryGetValue("Replica", out var repDrop))
        {
            if (Enum.TryParse(currentHero.baseReplica, true, out HeroType parsedName))
                repDrop.value = (int)parsedName;
        }

        if (statsUI.Dropdowns.TryGetValue("ImageOverride", out var imgDrop))
        {
            if (Enum.TryParse(currentHero.imageOverride, true, out HeroType parsedName))
                imgDrop.value = (int)parsedName;
        }

        if (statsUI.Dropdowns.TryGetValue("Color", out var colDrop))
        {
            HeroColorOption colOpt = ReverseLookupColor(currentHero.colorClass);
            colDrop.value = (int)colOpt;
            if (heroIconBackground) heroIconBackground.color = SDColors.GetColor(colOpt);
        }

        for (int i = 0; i < 6; i++)
        {
            var face = currentHero.diceSides[i];

            if (diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.text = face.effectID.ToString();
            if (diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.text = face.pips.ToString();
            if (diceUI.Inputs.TryGetValue($"Facade_{i}", out var dFac)) dFac.text = face.facadeID;

            int h = 0, s = 0, v = 0;
            string[] hsv = (face.facadeColor ?? "").Split(':');

            if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
            if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS;
            if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

            // Set text boxes
            if (diceUI.Inputs.TryGetValue($"FacH_{i}", out var dH)) dH.text = hsv.Length > 0 ? hsv[0] : "";
            if (diceUI.Inputs.TryGetValue($"FacS_{i}", out var dS)) dS.text = hsv.Length > 1 ? hsv[1] : "";
            if (diceUI.Inputs.TryGetValue($"FacV_{i}", out var dV)) dV.text = hsv.Length > 2 ? hsv[2] : "";

            // Set sliders
            if (diceUI.Sliders.TryGetValue($"SliH_{i}", out var sliH)) sliH.SetValueWithoutNotify(h);
            if (diceUI.Sliders.TryGetValue($"SliS_{i}", out var sliS)) sliS.SetValueWithoutNotify(s);
            if (diceUI.Sliders.TryGetValue($"SliV_{i}", out var sliV)) sliV.SetValueWithoutNotify(v);

            // --- Update buttons ---
            if (diceUI.Buttons.TryGetValue($"BaseBtn_{i}", out var baseBtn))
            {
                Sprite sSprite = GetSpriteForBase(face.effectID);
                SetButtonIcon(baseBtn, sSprite);
            }

            if (diceUI.Buttons.TryGetValue($"FacBtn_{i}", out var facBtn))
            {
                Sprite sSprite = GetSpriteForFacade(face.facadeID);
                SetButtonIcon(facBtn, sSprite);
            }

            UpdateDiceIcon(i);
        }

        UpdateHeroIcon();

        isSyncing = false;
    }

    private void GenerateRawText()
    {
        isSyncing = true;

        // 1. Build Base SD Array
        string sdString = "";
        for (int i = 0; i < 6; i++)
        {
            if (currentHero.diceSides[i].effectID == 0) sdString += "0";
            else sdString += $"{currentHero.diceSides[i].effectID}-{currentHero.diceSides[i].pips}";
            if (i < 5) sdString += ":";
        }

        // Determine if the colorClass should be omitted because it matches the default replica color
        string colorSegment = $".col.{currentHero.colorClass}";
        if (System.Enum.TryParse(currentHero.baseReplica, true, out HeroType baseHeroEnum))
        {
            if (HeroColorMap.TryGetValue(baseHeroEnum, out HeroColorOption defaultColorOption))
            {
                string defaultColorCode = GetCode(defaultColorOption);

                // If current color matches the base replica's default color, omit the color segment
                if (currentHero.colorClass == defaultColorCode)
                {
                    colorSegment = "";
                }
            }
        }

        // 2. Base Hero String (without image override in the middle)
        string baseHeroStr = $"replica.{currentHero.baseReplica}.n.{currentHero.heroName}{colorSegment}.hp.{currentHero.hp}.tier.{currentHero.tier}.sd.{sdString}";

        // 3. Build Item Modifiers (.i.) for Keywords and Facades
        string modifiersStr = "";
        for (int i = 0; i < 6; i++)
        {
            var face = currentHero.diceSides[i];
            List<string> modChunks = new List<string>();

            // Keywords 
            foreach (var kw in face.keywords)
            {
                if (!string.IsNullOrWhiteSpace(kw))
                    modChunks.Add($"k.{kw.Trim().ToLower()}");
            }

            // Facade 
            if (!string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";

                string[] hsv = (face.facadeColor ?? "").Split(':');
                string h = (hsv.Length > 0 && !string.IsNullOrWhiteSpace(hsv[0])) ? hsv[0].Trim() : "0";
                string s = (hsv.Length > 1 && !string.IsNullOrWhiteSpace(hsv[1])) ? hsv[1].Trim() : "";
                string v = (hsv.Length > 2 && !string.IsNullOrWhiteSpace(hsv[2])) ? hsv[2].Trim() : "";

                // Format logic:
                // If V exists, we must include S (default to 0 if left blank).
                if (!string.IsNullOrEmpty(v))
                {
                    s = string.IsNullOrEmpty(s) ? "0" : s;
                    facStr += $":{h}:{s}:{v}";
                }
                else if (!string.IsNullOrEmpty(s))
                {
                    facStr += $":{h}:{s}";
                }
                else
                {
                    facStr += $":{h}"; // Minimum requirement
                }

                modChunks.Add(facStr);
            }

            if (modChunks.Count > 0)
            {
                string joinedMods = string.Join("#", modChunks);
                string faceTargetName = DiceTargetHelper.FaceNames[i];
                modifiersStr += $".i.{faceTargetName}.{joinedMods}";
            }
        }

        // 4. Image Override Condition (constructed to be appended at the end of the string)
        string imgOverrideStr = "";
        if (!string.IsNullOrEmpty(currentHero.imageOverride)
            && currentHero.imageOverride != HeroType.None.ToString()
            && currentHero.imageOverride != currentHero.baseReplica)
        {
            imgOverrideStr = $".img.{currentHero.imageOverride}";
        }

        // 5. Assemble final string placing the image override at the absolute end
        string plainText = $"({baseHeroStr}{modifiersStr}{imgOverrideStr})";
        rawTextOutput.text = plainText;

        // Update syntax highlighting
        syntaxHighlighterText.text = FormatSyntaxHighlighting(plainText);

        isSyncing = false;
    }

    private void ParseRawText(string rawText)
    {
        // 1. Wipe volatile nested data so deleted items in the string actually disappear from the UI
        for (int i = 0; i < 6; i++)
        {
            if (currentHero.diceSides[i].keywords == null)
                currentHero.diceSides[i].keywords = new List<string>();
            else
                currentHero.diceSides[i].keywords.Clear();

            currentHero.diceSides[i].facadeID = "";
            currentHero.diceSides[i].facadeColor = "";
        }

        // 2. Parse Standard Core Variables (Using lookbehinds to prevent false matches)
        Match mReplica = Regex.Match(rawText, @"replica\.([a-zA-Z]+)");
        if (mReplica.Success) currentHero.baseReplica = mReplica.Groups[1].Value;

        Match mImage = Regex.Match(rawText, @"(?<=\.)img\.([a-zA-Z]+)");
        if (mImage.Success) currentHero.imageOverride = mImage.Groups[1].Value;
        else currentHero.imageOverride = "None";

        Match mName = Regex.Match(rawText, @"(?<=\.)n\.([a-zA-Z0-9_\s]+)");
        if (mName.Success) currentHero.heroName = mName.Groups[1].Value;

        Match mCol = Regex.Match(rawText, @"(?<=\.)col\.([a-z])");
        if (mCol.Success) currentHero.colorClass = mCol.Groups[1].Value;

        Match mHp = Regex.Match(rawText, @"(?<=\.)hp\.(\d+)");
        if (mHp.Success && int.TryParse(mHp.Groups[1].Value, out int hp)) currentHero.hp = hp;

        Match mTier = Regex.Match(rawText, @"(?<=\.)tier\.(\d+)");
        if (mTier.Success && int.TryParse(mTier.Groups[1].Value, out int tier)) currentHero.tier = tier;

        // 3. Parse Base SD Array using safe TryParse
        Match mSd = Regex.Match(rawText, @"(?<=\.)sd\.([0-9\-\:]+)");
        if (mSd.Success)
        {
            string[] sides = mSd.Groups[1].Value.Split(':');
            for (int i = 0; i < Mathf.Min(6, sides.Length); i++)
            {
                if (sides[i] == "0")
                {
                    currentHero.diceSides[i].effectID = 0;
                    currentHero.diceSides[i].pips = 0;
                }
                else
                {
                    string[] parts = sides[i].Split('-');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out int effID))
                            currentHero.diceSides[i].effectID = effID;
                        if (int.TryParse(parts[1], out int pips))
                            currentHero.diceSides[i].pips = pips;
                    }
                }
            }
        }

        // 4. Parse Modifiers (.i. blocks containing keywords & facades)
        // FIX: The modifier matcher is now non-greedy (.*?) and terminates at (.i., closing parenthesis, space, or end of string)
        MatchCollection iMatches = Regex.Matches(rawText, @"\.i\.([a-zA-Z0-9]+)\.(.*?)(?=\.i\.|\)|\s|$)");
        foreach (Match m in iMatches)
        {
            string target = m.Groups[1].Value.ToLower();
            string mods = m.Groups[2].Value;

            List<int> faces = GetFacesFromTarget(target);
            string[] chunks = mods.Split('#');

            foreach (string chunk in chunks)
            {
                if (chunk.StartsWith("k.", StringComparison.OrdinalIgnoreCase))
                {
                    string kw = chunk.Substring(2);
                    // Strip tags if they pasted rich text directly
                    kw = Regex.Replace(kw, "<.*?>", string.Empty).Trim();

                    foreach (int f in faces)
                    {
                        if (!currentHero.diceSides[f].keywords.Contains(kw, StringComparer.OrdinalIgnoreCase))
                            currentHero.diceSides[f].keywords.Add(kw);
                    }
                }
                else if (chunk.StartsWith("facade.", StringComparison.OrdinalIgnoreCase))
                {
                    string facData = chunk.Substring(7);
                    string[] parts = facData.Split(':');
                    string facId = parts[0];
                    string facColor = parts.Length > 1 ? string.Join(":", parts, 1, parts.Length - 1) : "";

                    foreach (int f in faces)
                    {
                        currentHero.diceSides[f].facadeID = facId;
                        currentHero.diceSides[f].facadeColor = facColor;
                    }
                }
            }
        }
    }

    // Helper for bidirectional mapping of targets (left -> 0, mid -> 1, etc.)
    private List<int> GetFacesFromTarget(string target)
    {
        List<int> faces = new List<int>();
        switch (target)
        {
            case "left": faces.Add(0); break;
            case "mid": faces.Add(1); break;
            case "top": faces.Add(2); break;
            case "bot": faces.Add(3); break;
            case "right": faces.Add(4); break;
            case "rightmost": faces.Add(5); break;
            case "all": faces.AddRange(new int[] { 0, 1, 2, 3, 4, 5 }); break;
            case "left2": faces.AddRange(new int[] { 0, 1 }); break;
            case "topbot": faces.AddRange(new int[] { 2, 3 }); break;
            case "right2": faces.AddRange(new int[] { 4, 5 }); break;
            case "right3": faces.AddRange(new int[] { 3, 4, 5 }); break;
                // Fallback for an unrecognized group target applies it nowhere to prevent errors
        }
        return faces;
    }

    private HeroColorOption ReverseLookupColor(string code)
    {
        foreach (HeroColorOption opt in Enum.GetValues(typeof(HeroColorOption)))
        {
            if (GetCode(opt) == code) return opt;
        }
        return HeroColorOption.Yellow;
    }

    private string FormatSyntaxHighlighting(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        string result = plainText;

        // 1. Color standard tags (replica, n, col, etc)
        string[] keys = { "replica", "image", "img", "n", "col", "hp", "tier" };
        foreach (string key in keys)
        {
            string hexColor = GetFixedColorForTag(key);
            string pattern = $"(?<=^|\\.|\\()({key}\\.[^\\.\\)]+)";
            result = Regex.Replace(result, pattern, $"<color=#{hexColor}>$1</color>");
        }

        // 2. Specialized handling for the SD block
        Match sdMatch = Regex.Match(result, @"sd\.([0-9\-\:]+)");
        if (sdMatch.Success)
        {
            string fullSdBlock = sdMatch.Value;
            string content = sdMatch.Groups[1].Value;

            string[] faces = content.Split(':');
            string[] coloredFaces = new string[faces.Length];

            for (int i = 0; i < faces.Length; i++)
            {
                // Use the index + a unique string to ensure differentiation
                string hex = GetFixedColorForTag("face_" + i + "_unique");
                coloredFaces[i] = $"<color=#{hex}>{faces[i]}</color>";
            }

            string coloredSdBlock = $"<color=#{GetFixedColorForTag("sd")}>sd.</color>" + string.Join(":", coloredFaces);
            result = result.Replace(fullSdBlock, coloredSdBlock);
        }

        // 3. Coordinate modifier blocks (.i.) with their matching face color
        string[] targetNames = { "left", "mid", "top", "bot", "right", "rightmost", "all", "row", "col", "topbot", "left2", "mid2", "right2", "right3", "right5" };
        string targetPattern = string.Join("|", targetNames);

        // FIX: Match everything up to the next .i., closing parenthesis, space, or end of string
        string patternTargeted = @"\.i\.(" + targetPattern + @")\.(.*?)(?=\.i\.|\)|\s|$)";

        result = Regex.Replace(result, patternTargeted, (match) =>
        {
            string target = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            string hexColor;
            int faceIndex = Array.IndexOf(DiceTargetHelper.FaceNames, target.ToLower());

            if (faceIndex != -1)
            {
                // Single face target matches the exact face color assigned in step 2
                hexColor = GetFixedColorForTag("face_" + faceIndex + "_unique");
            }
            else
            {
                // Group combinations get their own distinct color
                hexColor = GetFixedColorForTag("target_combo_" + target.ToLower());
            }

            return $"<color=#{hexColor}>.i.{target}.{content}</color>";
        }, RegexOptions.IgnoreCase);

        // 4. Highlight global modifiers (e.g. .i.k.wither or .i.facade.The0 missing a target)
        // FIX: Ensure it doesn't break at inner dots
        string patternGlobal = @"\.i\.(k\..*?|facade\..*?)(?=\.i\.|\)|\s|$)";
        string globalHex = GetFixedColorForTag("global_modifier");
        result = Regex.Replace(result, patternGlobal, $"<color=#{globalHex}>.i.$1</color>", RegexOptions.IgnoreCase);

        return result;
    }

    private string GetFixedColorForTag(string tag)
    {
        // FNV-1a hash algorithm for much better distribution than the previous one
        uint hash = 2166136261;
        foreach (char c in tag)
        {
            hash ^= (uint)c;
            hash *= 16777619;
        }

        // Use the hash to get a unique hue
        float hue = (hash % 1000) / 1000f;
        // Use high saturation and brightness
        Color color = Color.HSVToRGB(hue, 0.7f, 0.9f);

        return ColorUtility.ToHtmlStringRGB(color);
    }

    private void UpdateDiceIcon(int index)
    {
        if (dicePreview == null) return;

        var face = currentHero.diceSides[index];
        int effectId = face.effectID;
        string facadeId = face.facadeID;

        // Parse the H:S:V string into integers, defaulting to 0
        int h = 0, s = 0, v = 0;
        string[] hsv = (face.facadeColor ?? "").Split(':');
        if (hsv.Length > 0 && int.TryParse(hsv[0], out int parsedH)) h = parsedH;
        if (hsv.Length > 1 && int.TryParse(hsv[1], out int parsedS)) s = parsedS;
        if (hsv.Length > 2 && int.TryParse(hsv[2], out int parsedV)) v = parsedV;

        Image targetImage = index switch
        {
            0 => dicePreview.Left,
            1 => dicePreview.Middle,
            2 => dicePreview.Top,
            3 => dicePreview.Bottom,
            4 => dicePreview.Right,
            5 => dicePreview.Rightmost,
            _ => null
        };

        if (targetImage != null)
        {
            if (!string.IsNullOrWhiteSpace(facadeId) && facadeId.Length >= 4)
            {
                string prefix = facadeId.Substring(0, 3);
                string idString = facadeId.Substring(3);

                if (int.TryParse(idString, out int parsedFacadeId))
                {
                    // Pass H, S, V down to the material
                    dicePreview.SetDiceIcon(targetImage, prefix, parsedFacadeId, h, s, v);
                    return;
                }
            }

            // Also pass H, S, V even if falling back to base action
            dicePreview.SetDiceIcon(targetImage, "bas", effectId, h, s, v);
        }
    }

    private void UpdateHeroIcon()
    {
        if (heroIcon == null) return;

        // Determine which hero name is active based on the override rules
        string activeHeroNameStr = currentHero.baseReplica;
        if (!string.IsNullOrEmpty(currentHero.imageOverride)
            && currentHero.imageOverride != HeroType.None.ToString()
            && currentHero.imageOverride != currentHero.baseReplica)
        {
            activeHeroNameStr = currentHero.imageOverride;
        }

        if (Enum.TryParse(activeHeroNameStr, true, out HeroType activeHero))
        {
            if (HeroSpriteDatabase.HeroToSpriteMap.TryGetValue(activeHero, out string spriteName))
            {
                // Lazy-load the sliced spritesheet array once
                if (atlasSprites == null || atlasSprites.Length == 0)
                {
                    atlasSprites = SpriteCache.GetBaseSprites();
                }

                // Find the specific sub-sprite by name from the sliced array
                Sprite targetSprite = Array.Find(atlasSprites, s => s.name == spriteName);

                if (targetSprite != null && heroIcon.icon != null)
                {
                    heroIcon.icon.sprite = targetSprite;
                }
                else if (targetSprite == null)
                {
                    Debug.LogWarning($"Could not find sliced sprite '{spriteName}' inside 'base_atlas_image' spritesheet.");
                }
            }
        }
    }

    // --- KEYWORD SEARCH & SELECT LOGIC ---

    private string[] GetDefaultKeywordOptions()
    {
        var list = new List<string> { "Keyword" };
        foreach (string name in Enum.GetNames(typeof(EffectKeyword)))
        {
            if (Enum.TryParse(name, out EffectKeyword kw) && EffectKeywordColors.Map.TryGetValue(kw, out Color col))
            {
                string hex = ColorUtility.ToHtmlStringRGB(col);
                list.Add($"<color=#{hex}>{name}</color>");
            }
            else
            {
                list.Add(name);
            }
        }
        return list.ToArray();
    }

    private void FilterKeywordDropdown(int index, string search)
    {
        if (isSyncing) return;

        if (diceUI.Dropdowns.TryGetValue($"KwDrop_{index}", out var drop))
        {
            drop.ClearOptions();

            // Filter original clean Enum names
            var filteredNames = Enum.GetNames(typeof(EffectKeyword))
                .Where(k => string.IsNullOrEmpty(search) || k.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // Update the clean data cache for this face to match the new filtered order
            cleanDropdownKeywords[index] = filteredNames;

            List<string> options = new List<string> { "[ Select Keyword to Add ]" };
            foreach (string name in filteredNames)
            {
                if (Enum.TryParse(name, out EffectKeyword kw) && EffectKeywordColors.Map.TryGetValue(kw, out Color col))
                {
                    string hex = ColorUtility.ToHtmlStringRGB(col);
                    options.Add($"<color=#{hex}>{name}</color>");
                }
                else
                {
                    options.Add(name);
                }
            }

            drop.AddOptions(options);
            drop.value = 0;
            drop.RefreshShownValue();
        }
    }

    private void AddKeywordFromDropdown(int index, int dropVal)
    {
        if (isSyncing || dropVal == 0) return;

        // CRITICAL lookup: Retrieve the pure keyword from our clean data cache by index,
        // completely bypassing the UI's formatted rich-text string!
        if (cleanDropdownKeywords.TryGetValue(index, out var cleanList))
        {
            string cleanKw = cleanList[dropVal - 1]; // Offset by 1 for the placeholder option

            if (!currentHero.diceSides[index].keywords.Contains(cleanKw, StringComparer.OrdinalIgnoreCase))
            {
                currentHero.diceSides[index].keywords.Add(cleanKw);
                RefreshDiceUI();
            }
        }
    }

    private void RemoveKeyword(int index, string keyword)
    {
        if (isSyncing) return;

        // Since the data model is now 100% clean, we don't need any Regex string stripping here either
        var keywordsList = currentHero.diceSides[index].keywords;
        if (keywordsList.Contains(keyword))
        {
            keywordsList.Remove(keyword);
            RefreshDiceUI();
        }
    }

    private Sprite GetSpriteForBase(int id)
    {
        string search = $"bas_{id}";
        return Array.Find(_baseActionSprites, s => s != null && (s.name == search || s.name.StartsWith($"{search}_")));
    }

    private Sprite GetSpriteForFacade(string facadeId)
    {
        if (string.IsNullOrWhiteSpace(facadeId) || facadeId.Length < 4) return null;

        // Split "Che6" back into "Che" and "6" for finding "Che_6_..."
        string prefix = facadeId.Substring(0, 3);
        string num = facadeId.Substring(3);
        string search = $"{prefix}_{num}";

        return Array.Find(_allActionSprites, s => s != null && (s.name == search || s.name.StartsWith($"{search}_")));
    }

    private void SetButtonIcon(Button btn, Sprite sprite)
    {
        if (btn == null) return;

        // 1. Try DiceFaceUIElement first (Leave this alone, it handles DiceButtons)
        DiceFaceUIElement diceUIElement = btn.GetComponentInParent<DiceFaceUIElement>();
        if (diceUIElement != null)
        {
            diceUIElement.SetIcon(sprite);
            return;
        }

        // 2. Try the new ImageButton logic for standard buttons
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
                imgBtn.image.gameObject.SetActive(false); // Turn off if null
            }
            return;
        }

        // 3. Fallback for generic buttons
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

    private void UpdateFacadeColor(int index, int componentIndex, string val)
    {
        var face = currentHero.diceSides[index];

        // If no facade is currently assigned, automatically assign one matching the base action
        if (string.IsNullOrWhiteSpace(face.facadeID))
        {
            face.facadeID = $"bas{face.effectID}";

            // Update the facade text field to reflect the change
            if (diceUI != null && diceUI.Inputs.TryGetValue($"Facade_{index}", out var dFac))
            {
                dFac.text = face.facadeID;
            }

            // Update the facade button icon
            if (diceUI != null && diceUI.Buttons.TryGetValue($"FacBtn_{index}", out var facBtn))
            {
                Sprite sSprite = GetSpriteForFacade(face.facadeID);
                SetButtonIcon(facBtn, sSprite);
            }
        }

        string[] parts = (face.facadeColor ?? "").Split(':');

        // Ensure we always have 3 slots to work with
        List<string> list = new List<string>(parts);
        while (list.Count < 3) list.Add("");

        list[componentIndex] = val;
        face.facadeColor = string.Join(":", list);
    }

    private void UpdateHsvInput(int index, int componentIndex, string val)
    {
        if (isSyncing) return;

        if (string.IsNullOrEmpty(val) || val == "-")
        {
            UpdateFacadeColor(index, componentIndex, val);
            UpdateDiceIcon(index);
            OnUIChanged();
            return;
        }

        if (int.TryParse(val, out int num))
        {
            num = Mathf.Clamp(num, -99, 99);
            UpdateFacadeColor(index, componentIndex, num.ToString());

            // Silently sync the corresponding slider without causing an infinite loop
            isSyncing = true;
            string sliKey = componentIndex == 0 ? $"SliH_{index}" : componentIndex == 1 ? $"SliS_{index}" : $"SliV_{index}";
            if (diceUI.Sliders.TryGetValue(sliKey, out var slider)) slider.value = num;
            isSyncing = false;
        }

        UpdateDiceIcon(index);
        OnUIChanged();
    }

    private void UpdateHsvFromSlider(int index, int componentIndex, float val)
    {
        if (isSyncing) return;

        int num = Mathf.RoundToInt(val);
        UpdateFacadeColor(index, componentIndex, num.ToString());

        // Silently sync the corresponding text input without causing an infinite loop
        isSyncing = true;
        string inpKey = componentIndex == 0 ? $"FacH_{index}" : componentIndex == 1 ? $"FacS_{index}" : $"FacV_{index}";
        if (diceUI.Inputs.TryGetValue(inpKey, out var input)) input.text = num.ToString();
        isSyncing = false;

        UpdateDiceIcon(index);
        OnUIChanged();
    }

    private void CopyStringToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}