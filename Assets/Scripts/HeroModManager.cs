using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    void Start()
    {
        uiGenerator = GetComponent<FullScreenUIGenerator>();
        currentHero = new HeroData();

        BuildUIAndBind();

        UpdateUIFromData();
        GenerateRawText();
    }

    private void BuildUIAndBind()
    {
        string[] heroNamesList = Enum.GetNames(typeof(AllNames));

        // 1. Core Stats Layout Specification
        // Each row contains a pair of: [Label (0.35 width) | Input/Dropdown (0.65 width)]
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
                GridCellSpec.CreateDropdown("Color", "", 0.65f, HeroColors.GetFormattedColorNames(), (val) => {
                    HeroColors.ColorOption selectedColor = (HeroColors.ColorOption)val;
                    currentHero.colorClass = HeroColors.GetCode(selectedColor);
                    heroIcon.frame.color = HeroColors.GetColor(selectedColor);
                    OnUIChanged();
                })
            ),
            // Row 5: HP & Tier (Stacked in half-columns to preserve label space)
            new GridRowSpec(
                GridCellSpec.CreateLabel("HP Label", "HP:", 0.2f),
                GridCellSpec.CreateInput("HP", "", 0.3f, (val) => { if (int.TryParse(val, out int hp)) currentHero.hp = hp; OnUIChanged(); }),
                GridCellSpec.CreateLabel("Tier Label", "Tier:", 0.2f),
                GridCellSpec.CreateInput("Tier", "", 0.3f, (val) => { if (int.TryParse(val, out int t)) currentHero.tier = t; OnUIChanged(); })
            )
        };

        // 2. Dice Layout Specification
        // Order: Left, Middle, Top, Bottom, Right, Rightmost
        string[] diceNames = { "Left", "Middle", "Top", "Bottom", "Right", "Rightmost" };
        // 2. Dice Layout Specification (Expanded to 2 rows per face)
        var diceLayout = new List<GridRowSpec>();
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            string faceName = DiceTargetHelper.FaceNames[index].ToUpper();

            // Top Row: Base Action & Pips
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateLabel($"Lbl_{index}", $"{faceName}:", 0.2f),
                GridCellSpec.CreateInput($"ID_{index}", "Base ID", 0.4f, (val) => {
                    if (int.TryParse(val, out int id)) { currentHero.diceSides[index].effectID = id; UpdateDiceIcon(index); OnUIChanged(); }
                }),
                GridCellSpec.CreateInput($"Pips_{index}", "Pips", 0.4f, (val) => {
                    if (int.TryParse(val, out int p)) { currentHero.diceSides[index].pips = p; OnUIChanged(); }
                })
            ));

            // Bottom Row: Facades & Keywords
            diceLayout.Add(new GridRowSpec(
                GridCellSpec.CreateInput($"Facade_{index}", "Facade ID", 0.3f, (val) => { currentHero.diceSides[index].facadeID = val; OnUIChanged(); }),
                GridCellSpec.CreateInput($"Color_{index}", "Color", 0.3f, (val) => { currentHero.diceSides[index].facadeColor = val; OnUIChanged(); }),
                GridCellSpec.CreateInput($"Kw_{index}", "Keywords (csv)", 0.4f, (val) => {
                    currentHero.diceSides[index].keywords = val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();
                    OnUIChanged();
                })
            ));

            // Tiny spacer row so faces don't bleed together
            if (i < 5) diceLayout.Add(new GridRowSpec(GridCellSpec.CreateLabel($"Spacer_{index}", "", 1.0f)));
        }

        // 3. Define the column split mapping
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("LeftStats", 0.02f, 0.35f, statsLayout),     // Slightly wider to fit text labels
            new ColumnSpec("MiddleDice", 0.38f, 0.68f, diceLayout),
            new ColumnSpec("RightOutput", 0.71f, 0.98f)                 // Custom layout
        };

        GeneratedScreen screen = uiGenerator.SetupScreen(columns);

        statsUI = screen.ColumnRefs["LeftStats"];
        diceUI = screen.ColumnRefs["MiddleDice"];

        if (screen.CustomPanels.TryGetValue("RightOutput", out RectTransform rightPanel))
        {
            BuildRightPanelContent(rightPanel);
        }
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
        // Set max X to 0.33f
        FullScreenUIGenerator.SetAnchors(iconRt, 0.0f, 0.0f, 0.33f, 1.0f);

        // 3. Instantiate Dice Preview (Right: 38% to 100% to allow a 5% gap)
        GameObject diceObj = Instantiate(dicePreviewPrefab, containerRt);
        dicePreview = diceObj.GetComponent<DicePreview>();
        RectTransform diceRt = diceObj.GetComponent<RectTransform>();
        // Set min X to 0.38f
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
        rawTextOutput.textComponent.color = Color.clear;
        rawTextOutput.customCaretColor = true;
        rawTextOutput.caretColor = Color.white;

        GameObject highlighterObj = Instantiate(uiGenerator.labelPrefab, rawTextOutput.textComponent.transform.parent);
        highlighterObj.name = "SyntaxHighlighter";
        syntaxHighlighterText = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();

        RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
        RectTransform textCompRt = rawTextOutput.textComponent.GetComponent<RectTransform>();
        highlightRt.anchorMin = textCompRt.anchorMin;
        highlightRt.anchorMax = textCompRt.anchorMax;
        highlightRt.offsetMin = textCompRt.offsetMin;
        highlightRt.offsetMax = textCompRt.offsetMax;
        highlightRt.pivot = textCompRt.pivot;

        syntaxHighlighterText.alignment = rawTextOutput.textComponent.alignment;
        syntaxHighlighterText.fontSize = rawTextOutput.textComponent.fontSize;
        syntaxHighlighterText.margin = rawTextOutput.textComponent.margin;
        syntaxHighlighterText.enableWordWrapping = rawTextOutput.textComponent.enableWordWrapping;
        syntaxHighlighterText.richText = true;
        // ----------------------------------

        RectTransform inputRt = inputObj.GetComponent<RectTransform>();
        FullScreenUIGenerator.SetAnchors(inputRt, 0.0f, 0.0f, 1.0f, 0.58f);

        rawTextOutput.onValueChanged.AddListener(OnRawTextChanged);
    }

    private void OnUIChanged()
    {
        if (isSyncing) return;
        GenerateRawText();
    }

    private void OnRawTextChanged(string rawText)
    {
        if (isSyncing) return;

        // Colorize user input in real-time
        syntaxHighlighterText.text = FormatSyntaxHighlighting(rawText);

        ParseRawText(rawText);
        UpdateUIFromData();
    }

    private void UpdateUIFromData()
    {
        isSyncing = true;

        if (statsUI.Inputs.TryGetValue("Name", out var nameIn)) nameIn.text = currentHero.heroName;
        if (statsUI.Inputs.TryGetValue("HP", out var hpIn)) hpIn.text = currentHero.hp.ToString();
        if (statsUI.Inputs.TryGetValue("Tier", out var tierIn)) tierIn.text = currentHero.tier.ToString();

        if (statsUI.Dropdowns.TryGetValue("Replica", out var repDrop))
        {
            if (Enum.TryParse(currentHero.baseReplica, true, out AllNames parsedName))
                repDrop.value = (int)parsedName;
        }

        if (statsUI.Dropdowns.TryGetValue("ImageOverride", out var imgDrop))
        {
            if (Enum.TryParse(currentHero.imageOverride, true, out AllNames parsedName))
                imgDrop.value = (int)parsedName;
        }

        if (statsUI.Dropdowns.TryGetValue("Color", out var colDrop))
        {
            HeroColors.ColorOption colOpt = ReverseLookupColor(currentHero.colorClass);
            colDrop.value = (int)colOpt;
            if (heroIconBackground) heroIconBackground.color = HeroColors.GetColor(colOpt);
        }

        for (int i = 0; i < 6; i++)
        {
            var face = currentHero.diceSides[i];

            if (diceUI.Inputs.TryGetValue($"ID_{i}", out var dId)) dId.text = face.effectID.ToString();
            if (diceUI.Inputs.TryGetValue($"Pips_{i}", out var dPip)) dPip.text = face.pips.ToString();
            if (diceUI.Inputs.TryGetValue($"Facade_{i}", out var dFac)) dFac.text = face.facadeID;
            if (diceUI.Inputs.TryGetValue($"Color_{i}", out var dCol)) dCol.text = face.facadeColor;
            if (diceUI.Inputs.TryGetValue($"Kw_{i}", out var dKw)) dKw.text = string.Join(", ", face.keywords);

            UpdateDiceIcon(i);
        }

        isSyncing = false;
    }

    private void GenerateRawText()
    {
        isSyncing = true;
        string sdString = "";
        for (int i = 0; i < 6; i++)
        {
            if (currentHero.diceSides[i].effectID == 0) sdString += "0";
            else sdString += $"{currentHero.diceSides[i].effectID}-{currentHero.diceSides[i].pips}";
            if (i < 5) sdString += ":";
        }

        string imgOverrideStr = "";
        if (!string.IsNullOrEmpty(currentHero.imageOverride) && currentHero.imageOverride != "Statue")
        {
            imgOverrideStr = $".img.{currentHero.imageOverride}";
        }

        // The pure text buffer used for copying/pasting
        string plainText = $"(replica.{currentHero.baseReplica}{imgOverrideStr}.n.{currentHero.heroName}.col.{currentHero.colorClass}.hp.{currentHero.hp}.tier.{currentHero.tier}.sd.{sdString})";
        rawTextOutput.text = plainText;

        // The colored text used for display
        syntaxHighlighterText.fontSize = 16;
        syntaxHighlighterText.autoSizeTextContainer = false;
        syntaxHighlighterText.text = FormatSyntaxHighlighting(plainText);

        isSyncing = false;
    }

    private void ParseRawText(string rawText)
    {
        Match mReplica = Regex.Match(rawText, @"replica\.([a-zA-Z]+)");
        if (mReplica.Success) currentHero.baseReplica = mReplica.Groups[1].Value;

        // Image override parser
        Match mImage = Regex.Match(rawText, @"\.img\.([a-zA-Z]+)");
        if (mImage.Success) currentHero.imageOverride = mImage.Groups[1].Value;
        else currentHero.imageOverride = "Statue"; // fallback

        Match mName = Regex.Match(rawText, @"n\.([a-zA-Z0-9_\s]+)");
        if (mName.Success) currentHero.heroName = mName.Groups[1].Value;

        Match mCol = Regex.Match(rawText, @"col\.([a-z])");
        if (mCol.Success) currentHero.colorClass = mCol.Groups[1].Value;

        Match mHp = Regex.Match(rawText, @"hp\.(\d+)");
        if (mHp.Success) currentHero.hp = int.Parse(mHp.Groups[1].Value);

        Match mTier = Regex.Match(rawText, @"tier\.(\d+)");
        if (mTier.Success) currentHero.tier = int.Parse(mTier.Groups[1].Value);

        Match mSd = Regex.Match(rawText, @"sd\.([0-9\-\:]+)");
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
                        currentHero.diceSides[i].effectID = int.Parse(parts[0]);
                        currentHero.diceSides[i].pips = int.Parse(parts[1]);
                    }
                }
            }
        }
    }

    private HeroColors.ColorOption ReverseLookupColor(string code)
    {
        foreach (HeroColors.ColorOption opt in Enum.GetValues(typeof(HeroColors.ColorOption)))
        {
            if (HeroColors.GetCode(opt) == code) return opt;
        }
        return HeroColors.ColorOption.Yellow;
    }

    // --- SYNTAX HIGHLIGHTING LOGIC ---
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
        int effectId = currentHero.diceSides[index].effectID;

        // Map index to the specific image slot in the prefab
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
            dicePreview.SetDiceIcon(targetImage, "bas", effectId);
    }
}