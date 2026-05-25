using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class AbilityDataFactory : MonoBehaviour
{
    [Header("UI Dependencies")]
    public FullScreenUIGenerator uiGenerator;
    public Font defaultFont; // Used for generated raw text panels

    // Generated References
    public GeneratedScreen currentScreen;
    public GridReferences statsUI;
    public ScrollRect diceScrollRect;
    public Text rawTextOutput;
    public Text syntaxHighlighterText;

    private bool isSyncing = false;

    // The data model for our Ability Factory
    [System.Serializable]
    public class AbilityState
    {
        public string targetHero = "Thief";
        public int attachmentMode = 0; // 0=Direct, 1=Item(i.t), 2=LearnSpell(learn.s), 3=LearnTactic(learn.t)
        public string itemName = "Custom Item";
        public int itemTier = 1;

        public string baseReplica = "Statue";
        public string abilityName = "Custom Spell";
        public string imageOverride = "None";
        public int h = 0, s = 0, v = 0;

        // Sides mapping: 0:Left(Main), 1:Mid(Extra), 2:Top(Tactic 1), 3:Bot(Tactic 2), 4:Right(Mana Cost), 5:Rightmost(Tactic 3)
        public DiceSide[] diceSides = new DiceSide[6];
        public List<string> globalSpellKeywords = new List<string>();

        public AbilityState()
        {
            for (int i = 0; i < 6; i++) diceSides[i] = new DiceSide();
        }
    }

    public AbilityState currentAbility = new AbilityState();

    // S&D specific hidden Spell Keywords mapped to their internal IDs
    private Dictionary<string, string> spellKeywordsMap = new Dictionary<string, string>
    {
        { "None", "" },
        { "Channel", "ritemx.302ea5e.part.0" },
        { "Deplete", "ritemx.539ce9a" },
        { "Single Cast", "ritemx.132fb.part.1" },
        { "Spell Rescue", "ritemx.62e8" },
        { "Cooldown", "ritemx.161bf" },
        { "Future", "Unpack.ritemx.644f" }
    };

    private void Start()
    {
        if (uiGenerator != null)
        {
            BuildUIAndBind();
        }
    }

    private void BuildUIAndBind()
    {
        string[] attachmentModes = { "Direct to Hero", "As Item Wrapper (i.t.)", "Item: Learn Spell (learn.s)", "Item: Learn Tactic (learn.t)" };

        // 1. Core Config Layout Specification
        var statsLayout = new List<GridRowSpec>
        {
            // Row 1: Target Hero & Wrapper Mode
            new GridRowSpec(
                GridCellSpec.CreateLabel("TargetLbl", "Host Hero:", 0.25f),
                GridCellSpec.CreateInput("TargetHero", currentAbility.targetHero, 0.25f, (val) => { currentAbility.targetHero = val; OnUIChanged(); }),
                GridCellSpec.CreateLabel("ModeLbl", "Mode:", 0.15f),
                GridCellSpec.CreateDropdown("ModeDrop", "Mode", 0.35f, attachmentModes, (val) => {
                    currentAbility.attachmentMode = val;
                    OnUIChanged();
                })
            ),
            
            // Row 2: Item Modifiers (Only impacts output if Mode > 0)
            new GridRowSpec(
                GridCellSpec.CreateLabel("ItemNameLbl", "Item Name:", 0.25f),
                GridCellSpec.CreateInput("ItemName", currentAbility.itemName, 0.40f, (val) => { currentAbility.itemName = val; OnUIChanged(); }),
                GridCellSpec.CreateLabel("ItemTierLbl", "Tier:", 0.15f),
                GridCellSpec.CreateInput("ItemTier", currentAbility.itemTier.ToString(), 0.20f, (val) => { if(int.TryParse(val, out int t)) currentAbility.itemTier = t; OnUIChanged(); })
            ),

            // Row 3: Ability Internals
            new GridRowSpec(
                GridCellSpec.CreateLabel("AbilityNameLbl", "Ability Name:", 0.35f),
                GridCellSpec.CreateInput("AbilityName", currentAbility.abilityName, 0.65f, (val) => { currentAbility.abilityName = val; OnUIChanged(); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("BaseReplicaLbl", "Spell Base (Hero):", 0.35f),
                GridCellSpec.CreateInput("BaseReplica", currentAbility.baseReplica, 0.65f, (val) => { currentAbility.baseReplica = val; OnUIChanged(); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("ImageOverrideLbl", "Image Override:", 0.35f),
                GridCellSpec.CreateInput("ImageOverride", currentAbility.imageOverride, 0.65f, (val) => { currentAbility.imageOverride = val; OnUIChanged(); })
            ),

            // Row 4: Special Spell Modifiers
            new GridRowSpec(
                GridCellSpec.CreateLabel("SpellKwLbl", "Add Spell Keyword:", 0.35f),
                GridCellSpec.CreateDropdown("SpellKwDrop", "Select Keyword", 0.65f, new List<string>(spellKeywordsMap.Keys).ToArray(), (val) => {
                    string[] keys = new List<string>(spellKeywordsMap.Keys).ToArray();
                    string kwId = spellKeywordsMap[keys[val]];
                    if (!string.IsNullOrEmpty(kwId) && !currentAbility.globalSpellKeywords.Contains(kwId)) {
                        currentAbility.globalSpellKeywords.Add(kwId);
                        OnUIChanged();
                    }
                })
            ),

            // HSV Sliders for the spell icon
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblSpellHue", "Hue:", 0.30f),
                GridCellSpec.CreateSlider("SpellSliH", -99, 99, true, 0.50f, (val) => { currentAbility.h = (int)val; OnUIChanged(); }),
                GridCellSpec.CreateInput("SpellFacH", "H", 0.20f, (val) => { if(int.TryParse(val, out int v)) { currentAbility.h = v; OnUIChanged(); } })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblSpellSat", "Saturation:", 0.30f),
                GridCellSpec.CreateSlider("SpellSliS", -99, 99, true, 0.50f, (val) => { currentAbility.s = (int)val; OnUIChanged(); }),
                GridCellSpec.CreateInput("SpellFacS", "S", 0.20f, (val) => { if(int.TryParse(val, out int v)) { currentAbility.s = v; OnUIChanged(); } })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblSpellVal", "Value:", 0.30f),
                GridCellSpec.CreateSlider("SpellSliV", -99, 99, true, 0.50f, (val) => { currentAbility.v = (int)val; OnUIChanged(); }),
                GridCellSpec.CreateInput("SpellFacV", "V", 0.20f, (val) => { if(int.TryParse(val, out int v)) { currentAbility.v = v; OnUIChanged(); } })
            )
        };

        // 2. Middle Column: Tabs + ScrollView Base
        List<string> tabNames = new List<string> {
            "All", "Left (Primary FX)", "Middle (Extra FX)", "Top (Tactic Cost)", "Bottom (Tactic Cost)", "Right (Mana Cost)", "Rightmost (Tactic Cost)"
        };
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
            new ColumnSpec("RightOutput", 0.71f, 0.98f) // Uses custom layout constructor natively
        };

        // 4. GENERATE THE UI
        currentScreen = uiGenerator.SetupScreen(columns);

        // Bind Left Column Cache
        statsUI = currentScreen.ColumnRefs["LeftStats"];

        // Grab the ScrollView so we can inject the dice UI into it later
        var middleRefs = currentScreen.ColumnRefs["MiddleDiceBase"];
        diceScrollRect = middleRefs.ScrollViews["DiceScrollView"];

        // Call your stub here to inject UI elements into the generated diceScrollRect.content
        // RebuildDiceScrollView();

        // 5. Create Right Column Text References manually since it's a Custom Panel
        if (currentScreen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }

        GenerateRawText(); // Initial run
    }

    /// <summary>
    /// Constructs the text output displays inside the specified Right Custom Panel
    /// </summary>
    private void BuildRightPanelContent(RectTransform rightPanel)
    {
        // Wrapper Object
        GameObject txtWrapper = new GameObject("TextOutputWrapper", typeof(RectTransform));
        txtWrapper.transform.SetParent(rightPanel, false);
        FullScreenUIGenerator.SetAnchors(txtWrapper.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.95f);

        // 1. Syntax Highlighted Text
        GameObject syntaxObj = new GameObject("SyntaxHighlighterText", typeof(RectTransform), typeof(Text));
        syntaxObj.transform.SetParent(txtWrapper.transform, false);
        RectTransform syntaxRt = syntaxObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(syntaxRt, 0.0f, 0.0f, 1.0f, 1.0f);

        syntaxHighlighterText = syntaxObj.GetComponent<Text>();
        syntaxHighlighterText.font = defaultFont != null ? defaultFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        syntaxHighlighterText.fontSize = 14;
        syntaxHighlighterText.alignment = TextAnchor.UpperLeft;
        syntaxHighlighterText.horizontalOverflow = HorizontalWrapMode.Wrap;
        syntaxHighlighterText.verticalOverflow = VerticalWrapMode.Overflow;
        syntaxHighlighterText.supportRichText = true;

        // 2. Invisible Raw Output Text (Usually layered over for copying)
        GameObject rawObj = new GameObject("RawTextOutput", typeof(RectTransform), typeof(Text));
        rawObj.transform.SetParent(txtWrapper.transform, false);
        RectTransform rawRt = rawObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(rawRt, 0.0f, 0.0f, 1.0f, 1.0f);

        rawTextOutput = rawObj.GetComponent<Text>();
        rawTextOutput.font = syntaxHighlighterText.font;
        rawTextOutput.fontSize = 14;
        rawTextOutput.color = new Color(0, 0, 0, 0.01f); // nearly invisible, used to allow users to select/copy plain text overlaying syntax highlighting
        rawTextOutput.alignment = TextAnchor.UpperLeft;
        rawTextOutput.horizontalOverflow = HorizontalWrapMode.Wrap;
        rawTextOutput.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void GenerateRawText()
    {
        isSyncing = true;

        // 1. Build Base SD Array
        string sdString = "";
        for (int i = 0; i < 6; i++)
        {
            if (currentAbility.diceSides[i].effectID == 0 && currentAbility.diceSides[i].pips == 0) sdString += "0-0";
            else sdString += $"{currentAbility.diceSides[i].effectID}-{currentAbility.diceSides[i].pips}";
            if (i < 5) sdString += ":";
        }

        // Define HSV Segment
        string heroHsvStr = "";
        if (currentAbility.h != 0 || currentAbility.s != 0 || currentAbility.v != 0)
        {
            heroHsvStr = $".hsv.{currentAbility.h}:{currentAbility.s}:{currentAbility.v}";
        }

        string innerHeroStr = $"{currentAbility.baseReplica}.sd.{sdString}";

        // 2. Build Item Modifiers (.i.) for Keywords and Facades attached to specific faces
        string modifiersStr = "";
        for (int i = 0; i < 6; i++)
        {
            var face = currentAbility.diceSides[i];
            List<string> modChunks = new List<string>();

            foreach (var kw in face.keywords)
            {
                if (!string.IsNullOrWhiteSpace(kw))
                    modChunks.Add($"k.{kw.Trim().ToLower()}");
            }

            if (!string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";
                string[] hsv = (face.facadeColor ?? "").Split(':');
                string h = hsv.Length > 0 && !string.IsNullOrWhiteSpace(hsv[0]) ? hsv[0].Trim() : "0";
                string s = hsv.Length > 1 && !string.IsNullOrWhiteSpace(hsv[1]) ? hsv[1].Trim() : "";
                string v = hsv.Length > 2 && !string.IsNullOrWhiteSpace(hsv[2]) ? hsv[2].Trim() : "";

                if (!string.IsNullOrEmpty(v)) { s = string.IsNullOrEmpty(s) ? "0" : s; facStr += $":{h}:{s}:{v}"; }
                else if (!string.IsNullOrEmpty(s)) facStr += $":{h}:{s}";
                else facStr += $":{h}";

                modChunks.Add(facStr);
            }

            if (modChunks.Count > 0)
            {
                string joinedMods = string.Join("#", modChunks);
                // Requires external implementation mapping 0-5 to face names
                // string faceTargetName = DiceTargetHelper.FaceNames[i]; 
                string faceTargetName = "left"; // Placeholder
                modifiersStr += $".i.{faceTargetName}.{joinedMods}";
            }
        }

        // 3. Append Global Spell Keywords
        foreach (var kwItem in currentAbility.globalSpellKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kwItem)) modifiersStr += $".i.{kwItem}";
        }

        // 4. Handle Name and Image assignments
        bool hasImageOverride = !string.IsNullOrEmpty(currentAbility.imageOverride) && currentAbility.imageOverride != "None";
        string imgOverrideStr = "";

        if (hasImageOverride)
        {
            imgOverrideStr = $".img.{currentAbility.imageOverride}{heroHsvStr}";
        }
        else if (!string.IsNullOrEmpty(heroHsvStr))
        {
            innerHeroStr += heroHsvStr;
        }

        string nameStr = !string.IsNullOrEmpty(currentAbility.abilityName) ? $".n.{currentAbility.abilityName}" : "";

        string innerText = $"({innerHeroStr}{modifiersStr}{imgOverrideStr}{nameStr})";

        // 5. Wrap with appropriate Outer Host Structure depending on Item Mode
        string plainText = "";
        switch (currentAbility.attachmentMode)
        {
            case 1: plainText = $"i.t.{currentAbility.targetHero}.abilitydata.{innerText}"; break;
            case 2: plainText = $"learn.s{currentAbility.targetHero}.abilitydata.{innerText}.n.{currentAbility.itemName}.tier.{currentAbility.itemTier}"; break;
            case 3: plainText = $"learn.t{currentAbility.targetHero}.abilitydata.{innerText}.n.{currentAbility.itemName}.tier.{currentAbility.itemTier}"; break;
            case 0: default: plainText = $"{currentAbility.targetHero}.abilitydata.{innerText}"; break;
        }

        if (rawTextOutput != null) rawTextOutput.text = plainText;
        if (syntaxHighlighterText != null) syntaxHighlighterText.text = FormatSyntaxHighlighting(plainText);

        isSyncing = false;
    }

    // Handlers
    private void OnUIChanged() { if (!isSyncing) GenerateRawText(); }
    private void OnDiceTabSelected(int tabIndex) { /* Switch ScrollView Content */ }
    private string FormatSyntaxHighlighting(string str) { return str; }
}

// Dummy Class requirement for missing ref context
public class DiceSide { public int effectID = 0, pips = 0; public List<string> keywords = new List<string>(); public string facadeID = "", facadeColor = ""; }