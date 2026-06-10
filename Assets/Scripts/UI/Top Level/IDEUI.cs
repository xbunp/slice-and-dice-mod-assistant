using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IDEUI : RootUI
{
    private VirtualizedIdeController mainIdeController;
    private VirtualizedIdeController phasesIdeController;
    private SDTextmodSyntaxConfig IDEconfig = new SDTextmodSyntaxConfig();

    [Header("Formatting State")]
    public bool IDEFormatString = true;
    private TextMeshProUGUI translationDisplay;

    // =========================================================================
    // Image Compression Cache
    // =========================================================================

    private Dictionary<string, string> imgCache = new Dictionary<string, string>();
    private int imgCounter = 0;
    private static readonly Regex ImgRegex = new Regex(@"img\.(.*?)\.|\[(.*?)\]", RegexOptions.Compiled);

    [Header("Overview State")]
    private bool showOverviewDetails = true;
    private List<ModBlockOverview> lastAnalyzedOverview;

    // =========================================================================
    // UI Layout Generation
    // =========================================================================

    protected override void BuildUIAndBind()
    {
        bool useMargins = false;
        generatedScreen = uiGenerator.CreateScreenWrapper(useMargins);

        if (generatedScreen == null || generatedScreen.RootWrapper == null) return;

        // 1. Get the safe canvas height (Matches HeroUI)
        float canvasHeight = 900f; // Baseline fallback
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null) canvasHeight = canvasRt.rect.height;
        }

        Canvas.ForceUpdateCanvases();

        float spacing = uiGenerator.rowSpacing;
        float buttonRowHeight = uiGenerator.rowHeight;

        // 2. Distribute initial height using the canvas dimensions
        float leftPanelHeight = canvasHeight - (spacing * 2f);
        float rightPanelHeight = (canvasHeight - (spacing * 3f)) / 2f;

        float leftIdeHeight = leftPanelHeight - buttonRowHeight - spacing;
        float rightTopIdeHeight = rightPanelHeight + spacing - buttonRowHeight;

        // Apply minimum safety bounds to prevent collapsing
        leftIdeHeight = Mathf.Max(leftIdeHeight, 400f);
        rightTopIdeHeight = Mathf.Max(rightTopIdeHeight, 200f);
        rightPanelHeight = Mathf.Max(rightPanelHeight, 200f);

        List<ColumnSpec> columns = new List<ColumnSpec>();

        // LEFT COLUMN (Pass calculated heights back in)
        List<GridRowSpec> leftRows = new List<GridRowSpec>
    {
        new GridRowSpec(buttonRowHeight,
            GridCellSpec.CreateButton("Button1", "Toggle IDE Formatting", 0.33f, () => ToggleIDE()),
            GridCellSpec.CreateButton("Button2", "Paste Textmod (FASTER)", 0.33f, () => OnPasteButtonClicked()),
            GridCellSpec.CreateButton("Button3", "Copy & Restore Code", 0.33f, () => CopyAndRestoreCode())
        ),
        new GridRowSpec(leftIdeHeight, GridCellSpec.CreateIDEInterface("LeftPanelIDE", 1.0f))
    };
        columns.Add(new ColumnSpec("Left_Column", 0.0f, 0.66f, leftRows));

        // RIGHT COLUMN (Pass calculated heights back in)
        List<GridRowSpec> rightRows = new List<GridRowSpec>
    {
        new GridRowSpec(buttonRowHeight, GridCellSpec.CreateButton("ToggleDetailsBtn", "Collapse Details", 1.0f, () => ToggleOverviewDetails())),
        new GridRowSpec(rightTopIdeHeight, GridCellSpec.CreateIDEInterface("RightTopIDE", 1.0f)),
        new GridRowSpec(rightPanelHeight, GridCellSpec.CreateScrollView("RightBottomScrollView", 1.0f))
    };
        columns.Add(new ColumnSpec("Right_Column", 0.66f, 1.0f, rightRows));

        uiGenerator.PopulateScreen(generatedScreen, columns, useMargins);

        if (generatedScreen != null) PostProcessLayout();
    }

    private void PostProcessLayout()
    {
        Canvas.ForceUpdateCanvases();

        // Bind Left Column
        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            if (leftRefs.Buttons.TryGetValue("Button2", out Button pasteBtn))
            {
                pasteBtn.onClick.RemoveAllListeners();
                pasteBtn.onClick.AddListener(() => OnPasteButtonClicked());
            }

            if (leftRefs.IDEInterfaces.TryGetValue("LeftPanelIDE", out VirtualizedIdeController ideObj))
            {
                mainIdeController = ideObj;
                IDEconfig = new SDTextmodSyntaxConfig { watchPaste = true, scrollHorizontal = true };
                mainIdeController.Initialize(IDEconfig);
                mainIdeController.TextPreprocessor = PreprocessPastedText;

                mainIdeController.OnSelectionChanged = HandleIdeSelectionChanged;
            }
        }

        // Bind Right Column (Safe lookups with renamed Column Key)
        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rightRefs))
        {
            if (rightRefs.Buttons.TryGetValue("ToggleDetailsBtn", out Button toggleBtn))
            {
                toggleBtn.onClick.RemoveAllListeners();
                toggleBtn.onClick.AddListener(() => ToggleOverviewDetails());
            }

            if (rightRefs.IDEInterfaces.TryGetValue("RightTopIDE", out VirtualizedIdeController ideObj2))
            {
                phasesIdeController = ideObj2;

                var preserveConfig = new PreserveHtmlSyntaxConfig
                {
                    watchPaste = false,
                    scrollHorizontal = true
                };

                phasesIdeController.Initialize(preserveConfig);
                phasesIdeController.OnLinkActivated = HandleOverviewLinkClicked;
            }
        }

        UpdateToggleButtonText();

        // Replace old demo button setup with translation setup
        SetupTranslationScrollView("Right_Column", "RightBottomScrollView");

        ApplyDynamicLayoutConstraints();
    }

    private void SetupTranslationScrollView(string columnName, string scrollViewKey)
    {
        if (generatedScreen.ColumnRefs.TryGetValue(columnName, out GridReferences refs))
        {
            if (refs.ScrollViews.TryGetValue(scrollViewKey, out ScrollRect scrollRect))
            {
                if (scrollRect.content != null)
                {
                    // Clear out default demo items
                    foreach (Transform child in scrollRect.content) Destroy(child.gameObject);

                    // Create text component dynamically
                    GameObject textObj = new GameObject("TranslationTextDisplay", typeof(RectTransform), typeof(TextMeshProUGUI));
                    textObj.transform.SetParent(scrollRect.content, false);

                    translationDisplay = textObj.GetComponent<TextMeshProUGUI>();
                    if (translationDisplay != null)
                    {
                        translationDisplay.fontSize = 14;
                        translationDisplay.color = Color.white;
                        translationDisplay.text = "Select code in the left panel to translate...";

                        // Span to fit the container
                        RectTransform rt = translationDisplay.rectTransform;
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.sizeDelta = Vector2.zero;
                    }
                }
            }
        }
    }

    private void HandleIdeSelectionChanged(string selectedText)
    {
        if (translationDisplay == null) return;

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            translationDisplay.text = "Select code in the left panel to translate...";
            return;
        }

        try
        {
            string translatedText = TextmodTranslator.Translate(selectedText);
            translationDisplay.text = translatedText;
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Translation failed: {ex.Message}");
            translationDisplay.text = "Error translating selected code.";
        }
    }

    private void HandleOverviewLinkClicked(string linkId)
    {
        if (mainIdeController == null || lastAnalyzedOverview == null) return;

        if (linkId.StartsWith("block_"))
        {
            if (int.TryParse(linkId.Substring(6), out int index) && index < lastAnalyzedOverview.Count)
            {
                string snippet = lastAnalyzedOverview[index].SearchSnippet;
                mainIdeController.ScrollToSnippet(snippet);
            }
        }
    }

    private void PopulateDemoScrollView(string columnName, string scrollViewKey, string buttonLabel)
    {
        if (generatedScreen.ColumnRefs.TryGetValue(columnName, out GridReferences refs))
        {
            if (refs.ScrollViews.TryGetValue(scrollViewKey, out ScrollRect scrollRect))
            {
                if (scrollRect.content != null && uiGenerator.buttonPrefab != null)
                {
                    foreach (Transform child in scrollRect.content) Destroy(child.gameObject);

                    GameObject btnObj = Instantiate(uiGenerator.buttonPrefab, scrollRect.content);
                    RectTransform rt = btnObj.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = Vector2.zero;
                        rt.sizeDelta = new Vector2(150, 40);
                    }

                    var textMesh = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (textMesh != null) textMesh.text = buttonLabel;

                    var button = btnObj.GetComponentInChildren<Button>();
                    if (button != null) button.onClick.AddListener(() => UnityEngine.Debug.Log($"{buttonLabel} clicked."));
                }
            }
        }
    }
    public void ToggleUI()
    {
        if (generatedScreen == null) return;
        bool isUiActive = !generatedScreen.ColumnPanels.Values.GetEnumerator().Current?.gameObject.activeSelf ?? true;

        foreach (var keyValuePair in generatedScreen.ColumnPanels)
        {
            if (keyValuePair.Value != null) keyValuePair.Value.gameObject.SetActive(isUiActive);
        }
    }

    // =========================================================================
    // Data Orchestration (Buttons -> Processing -> IDE)
    // =========================================================================

    private void OnPasteButtonClicked()
    {
        ClipboardManager.RequestPaste(uiGenerator, (clipboardText) => {
            if (!string.IsNullOrEmpty(clipboardText))
            {
                if (mainIdeController != null)
                {
                    // Triggers the IDE's native paste functionality (which automatically invokes our Preprocessor)
                    mainIdeController.SimulateExternalPaste(clipboardText);
                }
            }
        });
    }
    public void LoadDataIntoIDE(string rawModText)
    {
        if (mainIdeController == null || string.IsNullOrEmpty(rawModText)) return;

        // Use the unified formatting logic
        string finalFormattedText = PreprocessPastedText(rawModText);

        mainIdeController.LoadEntireDocument(finalFormattedText);
    }
    private void ToggleIDE()
    {
        if (mainIdeController == null) return;

        IDEFormatString = !IDEFormatString;
        UpdateToggleButtonText();

        // Extract current data, reprocess it with the new toggle state, and reload
        string currentRawData = mainIdeController.ExportDocument();
        LoadDataIntoIDE(currentRawData);
    }
    private void CopyAndRestoreCode()
    {
        if (mainIdeController == null) return;

        // 1. Pull the raw formatted text from the IDE
        string currentIDEText = mainIdeController.ExportDocument();

        // 2. Compress/Minify to raw game-engine format
        string minifiedCode = MinifyModString(currentIDEText);

        // 3. Decompress Image IDs back to massive image strings
        string restoredText = RestoreImages(minifiedCode);

        // 4. Final Export Formatting
        string finalOutputText = InsertLinebreaksAfterCommas(restoredText);

        // 5. Send to Clipboard
#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardManager.CopyToClipboard(finalOutputText);
#else
        GUIUtility.systemCopyBuffer = finalOutputText;
#endif

        uiGenerator.CreatePopup("Copied optimized output to clipboard!", true, null);
    }

    /// <summary>
    /// Master pipeline for taking raw external text, compressing it, formatting it, and loading it into the generic IDE.
    /// </summary>

    private void UpdateToggleButtonText()
    {
        if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences refs))
        {
            if (refs.Buttons.TryGetValue("Button1", out Button btn))
            {
                var textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (textMesh != null) textMesh.text = IDEFormatString ? "Toggle IDE Formatting OFF" : "Toggle IDE Formatting ON";
            }
        }
    }

    // =========================================================================
    // Core Mod Data Processing (Specific to Slice and Dice)
    // =========================================================================

    private string CompressImagesSync(string text)
    {
        if (string.IsNullOrEmpty(text) || (!text.Contains("img.") && !text.Contains("["))) return text;

        StringBuilder sb = new StringBuilder(text.Length);
        Match m = ImgRegex.Match(text);
        int lastIndex = 0;

        while (m.Success)
        {
            sb.Append(text, lastIndex, m.Index - lastIndex);
            bool isBracket = m.Value.StartsWith("[");
            string innerData = isBracket ? m.Groups[2].Value : m.Groups[1].Value;

            if (innerData.StartsWith("IMG_") || innerData.Length <= 25)
            {
                sb.Append(m.Value);
            }
            else
            {
                string newId = $"IMG_{++imgCounter}";
                imgCache[newId] = innerData;
                sb.Append(isBracket ? $"[{newId}]" : $"img.{newId}.");
            }

            lastIndex = m.Index + m.Length;
            m = m.NextMatch();
        }

        if (lastIndex < text.Length) sb.Append(text, lastIndex, text.Length - lastIndex);
        return sb.ToString();
    }
    private string RestoreImages(string text)
    {
        if (string.IsNullOrEmpty(text) || (!text.Contains("img.IMG_") && !text.Contains("[IMG_")))
            return text;

        return Regex.Replace(text, @"img\.(IMG_\d+)\.|\[(IMG_\d+)\]", match => {
            bool isBracket = match.Value.StartsWith("[");
            string id = isBracket ? match.Groups[2].Value : match.Groups[1].Value;

            if (imgCache.TryGetValue(id, out string data))
            {
                return isBracket ? $"[{data}]" : $"img.{data}.";
            }

            return match.Value;
        });
    }
    private string AutoFormatModStringSync(string raw)
    {
        string minified = MinifyModString(raw);
        StringBuilder sb = new StringBuilder(minified.Length + 500);
        int indentLevel = 0;
        bool isInPool = false;

        for (int i = 0; i < minified.Length; i++)
        {
            char c = minified[i];

            // 1. Detect entering any of the target pool states
            if (i + 9 <= minified.Length && minified.Substring(i, 9).Equals("itempool.", StringComparison.OrdinalIgnoreCase))
            {
                isInPool = true;
            }
            else if (i + 12 <= minified.Length && minified.Substring(i, 12).Equals("monsterpool.", StringComparison.OrdinalIgnoreCase))
            {
                isInPool = true;
            }
            else if (i + 9 <= minified.Length && minified.Substring(i, 9).Equals("heropool.", StringComparison.OrdinalIgnoreCase))
            {
                isInPool = true;
            }

            // 2. Reset the pool state when encountering a comma (delimiter)
            if (c == ',')
            {
                isInPool = false;
                indentLevel = 0;
                sb.Append(c);
                continue;
            }

            // 3. Linebreak after '+' symbol within any active pool
            if (c == '+' && isInPool)
            {
                sb.Append('+');
                AppendNewlineAndIndent(sb, indentLevel * 4);
                continue;
            }

            // 4. Default structural formatting rules
            if (c == '&') { AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); }
            else if (c == '@' && i + 1 < minified.Length && char.IsDigit(minified[i + 1])) { AppendNewlineAndIndent(sb, (indentLevel + 1) * 4); sb.Append(c); sb.Append(minified[++i]); }
            else if (c == ';') { AppendNewlineAndIndent(sb, (indentLevel + 1) * 4); sb.Append(c); }
            else if ((c == '{' && i + 1 < minified.Length && minified[i + 1] == '}') || (c == '(' && i + 1 < minified.Length && minified[i + 1] == ')')) { sb.Append(c); sb.Append(minified[++i]); }
            else if (c == '{' || c == '(') { AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); indentLevel++; AppendNewlineAndIndent(sb, indentLevel * 4); }
            else if (c == '}' || c == ')') { indentLevel = Math.Max(0, indentLevel - 1); AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); }
            else sb.Append(c);
        }
        return sb.ToString().TrimStart();
    }
    public string MinifyModString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        int len = raw.Length;
        var sb = new StringBuilder(len);
        bool lineStart = true;

        for (int i = 0; i < len; i++)
        {
            char c = raw[i];
            if (c == '\r' || c == '\n')
            {
                while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1])) sb.Length--;
                lineStart = true;
            }
            else
            {
                if (lineStart)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    lineStart = false;
                }
                sb.Append(c);
            }
        }

        while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1])) sb.Length--;
        return sb.ToString();
    }
    private void AppendNewlineAndIndent(StringBuilder sb, int indent)
    {
        while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
        if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
        if (indent > 0) sb.Append(' ', indent);
    }
    private string InsertLinebreaksAfterCommas(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Replace(",", ",\n");
    }

    private void ToggleOverviewDetails()
    {
        showOverviewDetails = !showOverviewDetails;
        UpdateDetailsToggleButtonText();

        if (lastAnalyzedOverview != null)
        {
            DisplayModOverview(lastAnalyzedOverview);
        }
    }
    private void UpdateDetailsToggleButtonText()
    {
        // Changed "RightTop_Column" to "Right_Column"
        if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences refs))
        {
            if (refs.Buttons.TryGetValue("ToggleDetailsBtn", out Button btn))
            {
                var textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (textMesh != null)
                {
                    textMesh.text = showOverviewDetails ? "Collapse Details" : "Expand Details";
                }
            }
        }
    }

    private string PreprocessPastedText(string rawPaste)
    {
        string compressed = CompressImagesSync(rawPaste);

        lastAnalyzedOverview = ModAnalyzer.Analyze(compressed);
        DisplayModOverview(lastAnalyzedOverview);

        return IDEFormatString
            ? AutoFormatModStringSync(compressed)
            : InsertLinebreaksAfterCommas(MinifyModString(compressed));
    }
    private void DisplayModOverview(List<ModBlockOverview> content)
    {
        string niceNames = BuildModOverview(content, showOverviewDetails);
        phasesIdeController.ReplaceIDEContent(niceNames, false);
    }
    public static string BuildModOverview(List<ModBlockOverview> content, bool showDetails = true)
    {
        if (content == null || content.Count == 0)
        {
            return "No active directives found in this mod.";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("==================================================");
        sb.AppendLine("            MOD DIRECTIVES OVERVIEW");
        sb.AppendLine("==================================================");
        sb.AppendLine($"Total Directives Found: {content.Count}\n");

        for (int i = 0; i < content.Count; i++)
        {
            var directive = content[i];

            // --- 1. MODIFIER TRUNCATION ---
            var primaryTypes = directive.BlockTypes.Where(t => !t.StartsWith("Mod:", StringComparison.OrdinalIgnoreCase)).ToList();
            var modTypes = directive.BlockTypes.Where(t => t.StartsWith("Mod:", StringComparison.OrdinalIgnoreCase)).ToList();

            string typeLabel;
            if (modTypes.Count > 3)
            {
                var truncatedMods = modTypes.Take(3).Concat(new[] { $"+{modTypes.Count - 3} modifiers" });
                typeLabel = string.Join(" / ", primaryTypes.Concat(truncatedMods));
            }
            else
            {
                typeLabel = string.Join(" / ", directive.BlockTypes);
            }

            // --- 2. PARENT TIMING EXTRACTION ---
            string minRoundStr = null;
            if (directive.Details != null && directive.Details.Any())
            {
                var parsedRounds = directive.Details
                    .Where(d => !string.IsNullOrEmpty(d.Round) && int.TryParse(d.Round, out _))
                    .Select(d => int.Parse(d.Round))
                    .ToList();

                if (parsedRounds.Any())
                {
                    minRoundStr = parsedRounds.Min().ToString();
                }
            }

            string blockTiming = !string.IsNullOrEmpty(minRoundStr)
                            ? $"{minRoundStr.PadLeft(2)}."
                            : "   ";

            // CHANGE: Replace the plain [X] append with the formatted link tag
            string linkTag = $"<color=#00FFFF><u><link=\"block_{i}\">[X]</link></u></color>";
            sb.AppendLine($"  {blockTiming} {linkTag} {typeLabel} - {directive.BlockName}");

            // --- 3. CONDITIONAL DETAILS RENDERING ---
            if (showDetails && directive.Details != null)
            {
                foreach (var detail in directive.Details)
                {
                    string timingPrefix = !string.IsNullOrEmpty(detail.Round)
                        ? $"{detail.Round.PadLeft(2)}."
                        : "   ";

                    string indent = new string(' ', detail.Depth * 4);

                    sb.AppendLine($"  {timingPrefix}  {indent}{detail.Title}: {detail.Value}");
                }
            }
            if (showDetails)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;

        var layoutElement = target.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        }

        // Setting flexibleHeight to 1 instructs layout groups to stretch this element to fill unused space
        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1f;
    }

    private void StretchToParent(RectTransform rt, float topOffset, float bottomOffset)
    {
        if (rt == null) return;

        // Anchors min(0,0) and max(1,1) bounds the RectTransform corners to stretch with its parent
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, bottomOffset);
        rt.offsetMax = new Vector2(0f, -topOffset);
    }

    private void ApplyDynamicLayoutConstraints()
    {
        float canvasHeight = 900f;
        if (uiGenerator != null)
        {
            RectTransform canvasRt = uiGenerator.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            if (canvasRt != null) canvasHeight = canvasRt.rect.height;
        }

        float spacing = uiGenerator.rowSpacing;
        float buttonRowHeight = uiGenerator.rowHeight;

        // 1. LEFT PANEL: Stretch the IDE from below the buttons to the bottom of the screen
        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            if (leftRefs.IDEInterfaces.TryGetValue("LeftPanelIDE", out VirtualizedIdeController ide))
            {
                RectTransform ideRt = ide.GetComponent<RectTransform>();
                RectTransform rowContainerRt = ideRt.parent as RectTransform;

                // Start right below the button row
                float leftTopOffset = buttonRowHeight + spacing;

                // Stretch row container from below the buttons to the bottom
                StretchToParent(rowContainerRt, leftTopOffset, 0f);
                StretchToParent(ideRt, 0f, 0f);
            }
        }

        // 2. RIGHT PANEL: Only stretch the bottom edge of the Lower ScrollView
        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rightRefs))
        {
            // NOTE: We leave RightTopIDE untouched so it keeps its fixed height and positioning.

            if (rightRefs.ScrollViews.TryGetValue("RightBottomScrollView", out ScrollRect scrollView))
            {
                RectTransform scrollRt = scrollView.GetComponent<RectTransform>();
                RectTransform rowContainerRt = scrollRt.parent as RectTransform;

                // Re-calculate the height of the top IDE exactly as done in BuildUIAndBind
                float rightPanelHeight = (canvasHeight - (spacing * 3f)) / 2f;
                float rightTopIdeHeight = rightPanelHeight + spacing - buttonRowHeight;
                rightTopIdeHeight = Mathf.Max(rightTopIdeHeight, 200f);

                // Calculate where the lower panel starts (Buttons height + Top IDE height + spacing)
                float rightTopOffset = buttonRowHeight + rightTopIdeHeight + (spacing * 2f);

                // Stretch the lower row container from that offset point all the way to the bottom
                StretchToParent(rowContainerRt, rightTopOffset, 0f);
                StretchToParent(scrollRt, 0f, 0f);
            }
        }
    }
}