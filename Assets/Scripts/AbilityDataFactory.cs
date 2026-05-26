using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public enum AbilityType { Spell, Tactic }

public class AbilityDataFactory : RootUI
{
    [Header("UI Dependencies")]
    public Font defaultFont;

    // Generated References
    public GridReferences statsUI;
    public ScrollRect diceScrollRect;
    public Text rawTextOutput;
    public Text syntaxHighlighterText;

    private bool isSyncing = false;

    [System.Serializable]
    public class AbilityState
    {
        public string targetHero = "Thief";
        public int attachmentMode = 0; // 0=Direct, 1=Item(i.t), 2=LearnSpell(learn.s), 3=LearnTactic(learn.t)
        public string itemName = "Custom Item";
        public int itemTier = 1;

        public AbilityType abilityType = AbilityType.Spell; // Branching Type
        public string baseReplica = "Statue";
        public string abilityName = "Custom Spell";
        public string imageOverride = "None";
        public int h = 0, s = 0, v = 0;

        // Sides mapping: 
        // 0: Left (Primary FX)
        // 1: Middle (Secondary FX)
        // 2: Top (Tactic Cost 1)
        // 3: Bottom (Tactic Cost 2)
        // 4: Right (Mana Cost - Must be 0-0 for Tactics)
        // 5: Rightmost (Tactic Cost 3)
        public DiceSide[] diceSides = new DiceSide[6];
        public List<string> globalSpellKeywords = new List<string>();

        public AbilityState()
        {
            for (int i = 0; i < 6; i++) diceSides[i] = new DiceSide();
        }
    }

    public AbilityState currentAbility = new AbilityState();

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

    protected override void BuildUIAndBind()
    {
        /*

        string[] attachmentModes = { "Direct to Hero", "As Item Wrapper (i.t.)", "Item: Learn Spell (learn.s)", "Item: Learn Tactic (learn.t)" };
        string[] abilityTypes = { "Spell", "Tactic" };

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
            
            // Row 2: Ability Branching Selector
            new GridRowSpec(
                GridCellSpec.CreateLabel("TypeLbl", "Ability Type:", 0.35f),
                GridCellSpec.CreateDropdown("TypeDrop", "Type", 0.65f, abilityTypes, (val) => {
                    currentAbility.abilityType = (AbilityType)val;
                    OnUIChanged();
                })
            ),

            // Row 3: Item Modifiers (Only impacts output if Mode > 0)
            new GridRowSpec(
                GridCellSpec.CreateLabel("ItemNameLbl", "Item Name:", 0.25f),
                GridCellSpec.CreateInput("ItemName", currentAbility.itemName, 0.40f, (val) => { currentAbility.itemName = val; OnUIChanged(); }),
                GridCellSpec.CreateLabel("ItemTierLbl", "Tier:", 0.15f),
                GridCellSpec.CreateInput("ItemTier", currentAbility.itemTier.ToString(), 0.20f, (val) => { if(int.TryParse(val, out int t)) currentAbility.itemTier = t; OnUIChanged(); })
            ),

            // Row 4: Ability Internals
            new GridRowSpec(
                GridCellSpec.CreateLabel("AbilityNameLbl", "Ability Name:", 0.35f),
                GridCellSpec.CreateInput("AbilityName", currentAbility.abilityName, 0.65f, (val) => { currentAbility.abilityName = val; OnUIChanged(); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("BaseReplicaLbl", "Base Replica:", 0.35f),
                GridCellSpec.CreateInput("BaseReplica", currentAbility.baseReplica, 0.65f, (val) => { currentAbility.baseReplica = val; OnUIChanged(); })
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("ImageOverrideLbl", "Image Override:", 0.35f),
                GridCellSpec.CreateInput("ImageOverride", currentAbility.imageOverride, 0.65f, (val) => { currentAbility.imageOverride = val; OnUIChanged(); })
            ),

            // Row 5: Special Spell Modifiers
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

            // HSV Sliders for the icon
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

        // 2. Middle Column Layout
        List<string> tabNames = new List<string> {
            "All", "Left (Primary FX)", "Middle (Extra FX)", "Top (Tactic Cost)", "Bottom (Tactic Cost)", "Right (Mana Cost)", "Rightmost (Tactic Cost)"
        };
        var middleBaseLayout = new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateNavigationTabs("DiceTabs", tabNames, new List<GameObject>(), 1.0f, OnDiceTabSelected)),
            new GridRowSpec(600f, GridCellSpec.CreateScrollView("DiceScrollView", 1.0f))
        };

        // 3. Columns mapping
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftStats", 0.02f, 0.35f, statsLayout),
            new ColumnSpec("MiddleDiceBase", 0.38f, 0.68f, middleBaseLayout),
            new ColumnSpec("RightOutput", 0.71f, 0.98f)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
        statsUI = generatedScreen.ColumnRefs["LeftStats"];
        diceScrollRect = generatedScreen.ColumnRefs["MiddleDiceBase"].ScrollViews["DiceScrollView"];

        if (generatedScreen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }

        GenerateRawText();
        */
    }

    private void GenerateRawText()
    {
        isSyncing = true;

        // Apply branching rule to the Right Side (index 4)
        if (currentAbility.abilityType == AbilityType.Tactic)
        {
            // Tactics require the right side to be blank
            currentAbility.diceSides[4].effectID = 0;
            currentAbility.diceSides[4].pips = 0;
        }
        else if (currentAbility.abilityType == AbilityType.Spell)
        {
            // Spells require a valid mana-cost setup on the right side.
            // If the user hasn't defined a mana side here, default to basic mana (76)
            if (currentAbility.diceSides[4].effectID == 0)
            {
                currentAbility.diceSides[4].effectID = 76;
            }
            if (currentAbility.diceSides[4].pips == 0)
            {
                currentAbility.diceSides[4].pips = 1; // Default to 1-cost
            }
        }

        // 1. Build Base SD Array
        string sdString = "";
        for (int i = 0; i < 6; i++)
        {
            if (currentAbility.diceSides[i].effectID == 0 && currentAbility.diceSides[i].pips == 0) sdString += "0-0";
            else sdString += $"{currentAbility.diceSides[i].effectID}-{currentAbility.diceSides[i].pips}";
            if (i < 5) sdString += ":";
        }

        string heroHsvStr = "";
        if (currentAbility.h != 0 || currentAbility.s != 0 || currentAbility.v != 0)
        {
            heroHsvStr = $".hsv.{currentAbility.h}:{currentAbility.s}:{currentAbility.v}";
        }

        string innerHeroStr = $"{currentAbility.baseReplica}.sd.{sdString}";

        // 2. Side-specific modifiers
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
                string faceTargetName = "left"; // Placeholder mapping, replace with mapping logic if needed
                modifiersStr += $".i.{faceTargetName}.{joinedMods}";
            }
        }

        // 3. Global modifiers
        foreach (var kwItem in currentAbility.globalSpellKeywords)
        {
            if (!string.IsNullOrWhiteSpace(kwItem)) modifiersStr += $".i.{kwItem}";
        }

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

        // 5. Wrap outer syntax
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

    private void ParseRawText(string rawText)
    {
        currentAbility.globalSpellKeywords.Clear();
        for (int i = 0; i < 6; i++)
        {
            if (currentAbility.diceSides[i].keywords == null) currentAbility.diceSides[i].keywords = new List<string>();
            else currentAbility.diceSides[i].keywords.Clear();
            currentAbility.diceSides[i].facadeID = "";
            currentAbility.diceSides[i].facadeColor = "";
        }

        Match mOuter = Regex.Match(rawText, @"(?:(i\.t\.|learn\.s|learn\.t))?([a-zA-Z0-9]+)\.abilitydata\.\((.*?)\)(?:\.n\.([a-zA-Z0-9_\s]+)\.tier\.(\d+))?");
        if (!mOuter.Success) return;

        string wrapType = mOuter.Groups[1].Value;
        currentAbility.targetHero = mOuter.Groups[2].Value;
        string innerText = mOuter.Groups[3].Value;

        if (wrapType == "i.t.") currentAbility.attachmentMode = 1;
        else if (wrapType == "learn.s") currentAbility.attachmentMode = 2;
        else if (wrapType == "learn.t") currentAbility.attachmentMode = 3;
        else currentAbility.attachmentMode = 0;

        if (mOuter.Groups[4].Success) currentAbility.itemName = mOuter.Groups[4].Value;
        if (mOuter.Groups[5].Success && int.TryParse(mOuter.Groups[5].Value, out int tier)) currentAbility.itemTier = tier;

        Match mBase = Regex.Match(innerText, @"^([a-zA-Z0-9]+)\.sd\.");
        if (mBase.Success) currentAbility.baseReplica = mBase.Groups[1].Value;

        Match mImage = Regex.Match(innerText, @"(?<=\.)img\.([a-zA-Z0-9]+(?:\.[a-zA-Z0-9]+)?)(?=\.hsv|\.n|\.col|\.hp|\.tier|\.img|\.sd|\.speech|\.doc|\.i|\)|$)");
        if (mImage.Success) currentAbility.imageOverride = mImage.Groups[1].Value;
        else currentAbility.imageOverride = "None";

        Match mHeroHsv = Regex.Match(innerText, @"(?<=\.)hsv\.([\-\d:]+)");
        if (mHeroHsv.Success)
        {
            string[] parts = mHeroHsv.Groups[1].Value.Split(':');
            if (parts.Length > 0 && int.TryParse(parts[0], out int pH)) currentAbility.h = pH; else currentAbility.h = 0;
            if (parts.Length > 1 && int.TryParse(parts[1], out int pS)) currentAbility.s = pS; else currentAbility.s = 0;
            if (parts.Length > 2 && int.TryParse(parts[2], out int pV)) currentAbility.v = pV; else currentAbility.v = 0;
        }

        Match mName = Regex.Match(innerText, @"(?<=\.)n\.([a-zA-Z0-9_\s]+)");
        if (mName.Success) currentAbility.abilityName = mName.Groups[1].Value;

        // Parse Side Data
        Match mSd = Regex.Match(innerText, @"(?<=\.)sd\.([0-9\-\:]+)");
        if (mSd.Success)
        {
            string[] sides = mSd.Groups[1].Value.Split(':');
            for (int i = 0; i < Mathf.Min(6, sides.Length); i++)
            {
                if (sides[i] == "0" || sides[i] == "0-0")
                {
                    currentAbility.diceSides[i].effectID = 0;
                    currentAbility.diceSides[i].pips = 0;
                }
                else
                {
                    int hyphenIndex = sides[i].IndexOf('-');
                    if (hyphenIndex > 0)
                    {
                        if (int.TryParse(sides[i].Substring(0, hyphenIndex), out int effID)) currentAbility.diceSides[i].effectID = effID;
                        if (int.TryParse(sides[i].Substring(hyphenIndex + 1), out int pips)) currentAbility.diceSides[i].pips = pips;
                    }
                }
            }

            // AUTO-BRANCHING DETECTION RULE:
            // Check the 5th side (index 4 / Right side). If it is blank (0-0), this is parsed as a Tactic. 
            // Otherwise, it is parsed as a Spell.
            if (currentAbility.diceSides[4].effectID == 0 && currentAbility.diceSides[4].pips == 0)
            {
                currentAbility.abilityType = AbilityType.Tactic;
            }
            else
            {
                currentAbility.abilityType = AbilityType.Spell;
            }
        }

        // Parse Injections (.i.)
        MatchCollection iMatches = Regex.Matches(innerText, @"\.i\.([a-zA-Z0-9]+)\.?(.*?)(?=\.i\.|\.img\.|\.hsv\.|\.n\.|\)|\s|$)");
        foreach (Match m in iMatches)
        {
            string target = m.Groups[1].Value.ToLower();
            string mods = m.Groups[2].Value;

            if (target.StartsWith("ritemx") || target.StartsWith("unpack"))
            {
                string fullRitem = string.IsNullOrEmpty(mods) ? target : $"{target}.{mods}";
                currentAbility.globalSpellKeywords.Add(fullRitem);
                continue;
            }

            // Normal Side Parsers would flow here
        }
    }

    private void BuildRightPanelContent(RectTransform rightPanel)
    {
        GameObject txtWrapper = new GameObject("TextOutputWrapper", typeof(RectTransform));
        txtWrapper.transform.SetParent(rightPanel, false);
        FullScreenUIGenerator.SetAnchors(txtWrapper.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.95f);

        GameObject syntaxObj = new GameObject("SyntaxHighlighterText", typeof(RectTransform), typeof(Text));
        syntaxObj.transform.SetParent(txtWrapper.transform, false);
        FullScreenUIGenerator.SetAnchors(syntaxObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 1.0f, 1.0f);

        syntaxHighlighterText = syntaxObj.GetComponent<Text>();
        syntaxHighlighterText.font = defaultFont != null ? defaultFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        syntaxHighlighterText.fontSize = 14;
        syntaxHighlighterText.alignment = TextAnchor.UpperLeft;
        syntaxHighlighterText.horizontalOverflow = HorizontalWrapMode.Wrap;
        syntaxHighlighterText.verticalOverflow = VerticalWrapMode.Overflow;
        syntaxHighlighterText.supportRichText = true;

        GameObject rawObj = new GameObject("RawTextOutput", typeof(RectTransform), typeof(Text));
        rawObj.transform.SetParent(txtWrapper.transform, false);
        FullScreenUIGenerator.SetAnchors(rawObj.GetComponent<RectTransform>(), 0.0f, 0.0f, 1.0f, 1.0f);

        rawTextOutput = rawObj.GetComponent<Text>();
        rawTextOutput.font = syntaxHighlighterText.font;
        rawTextOutput.fontSize = 14;
        rawTextOutput.color = new Color(0, 0, 0, 0.01f);
        rawTextOutput.alignment = TextAnchor.UpperLeft;
        rawTextOutput.horizontalOverflow = HorizontalWrapMode.Wrap;
        rawTextOutput.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void OnUIChanged() { if (!isSyncing) GenerateRawText(); }
    private void OnDiceTabSelected(int tabIndex) { }
    private string FormatSyntaxHighlighting(string str) { return str; }
}
// Dummy Class requirement for missing ref context
public class DiceSide { public int effectID = 0, pips = 0; public List<string> keywords = new List<string>(); public string facadeID = "", facadeColor = ""; }