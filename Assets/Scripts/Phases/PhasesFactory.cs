using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhasesFactory : RootUI
{
    private VirtualizedIdeController mainIdeController;
    private VirtualizedIdeController phasesIdeController;
    private SDTextmodSyntaxConfig IDEconfig = new SDTextmodSyntaxConfig();

    [Header("Formatting State")]
    public bool IDEFormatString = true;

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

        Canvas.ForceUpdateCanvases();

        float totalHeight = generatedScreen.RootWrapper.rect.height;
        float spacing = uiGenerator.rowSpacing;

        float rightPanelHeight = (totalHeight - (spacing * 3f)) / 2f;
        float leftPanelHeight = totalHeight - (spacing * 2f);

        float leftSpacerHeight = spacing;
        float rightTopSpacerHeight = spacing;
        float rightBottomSpacerHeight = rightPanelHeight + (spacing * 2f);

        float buttonRowHeight = uiGenerator.rowHeight;
        float leftIdeHeight = leftPanelHeight - buttonRowHeight - spacing;

        // Adjust the Right Top IDE height to compensate for the toggle button height
        float rightTopIdeHeight = rightPanelHeight + spacing - buttonRowHeight;

        List<ColumnSpec> columns = new List<ColumnSpec>();

        // LEFT COLUMN (Buttons + Main IDE Panel directly)
        List<GridRowSpec> leftRows = new List<GridRowSpec>
    {
        new GridRowSpec(leftSpacerHeight, GridCellSpec.CreateLabel("LeftSpacer", "", 1.0f)),
        new GridRowSpec(buttonRowHeight,
            GridCellSpec.CreateButton("Button1", "Toggle IDE Formatting", 0.33f, () => ToggleIDE()),
            GridCellSpec.CreateButton("Button2", "Paste Textmod (FASTER)", 0.33f, () => OnPasteButtonClicked()),
            GridCellSpec.CreateButton("Button3", "Copy & Restore Code", 0.33f, () => CopyAndRestoreCode())
        ),
        new GridRowSpec(leftIdeHeight, GridCellSpec.CreateIDEInterface("LeftPanelIDE", 1.0f))
    };
        columns.Add(new ColumnSpec("Left_Column", 0.0f, 0.66f, leftRows));

        // RIGHT TOP COLUMN (Now using the adjusted height)
        List<GridRowSpec> rightTopRows = new List<GridRowSpec>
    {
        new GridRowSpec(buttonRowHeight, GridCellSpec.CreateButton("ToggleDetailsBtn", "Collapse Details", 1.0f, () => ToggleOverviewDetails())),
        new GridRowSpec(rightTopIdeHeight, GridCellSpec.CreateIDEInterface("RightTopIDE", 1.0f))
    };
        columns.Add(new ColumnSpec("RightTop_Column", 0.66f, 1.0f, rightTopRows));

        // RIGHT BOTTOM COLUMN
        List<GridRowSpec> rightBottomRows = new List<GridRowSpec>
    {
        new GridRowSpec(rightBottomSpacerHeight, GridCellSpec.CreateImagePanel("RightBottomSpacer", 1.0f)),
        new GridRowSpec(rightPanelHeight, GridCellSpec.CreateScrollView("RightBottomScrollView", 1.0f))
    };
        columns.Add(new ColumnSpec("RightBottom_Column", 0.66f, 1.0f, rightBottomRows));

        uiGenerator.PopulateScreen(generatedScreen, columns, useMargins);

        if (generatedScreen != null) PostProcessLayout();
    }
    private void PostProcessLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            // Retrieve button references...
            if (leftRefs.Buttons.TryGetValue("Button2", out Button pasteBtn))
            {
                pasteBtn.onClick.RemoveAllListeners();
                pasteBtn.onClick.AddListener(() => OnPasteButtonClicked());
            }

            if (leftRefs.IDEInterfaces.TryGetValue("LeftPanelIDE", out VirtualizedIdeController ideObj))
            {
                mainIdeController = ideObj;
                IDEconfig = new SDTextmodSyntaxConfig();
                IDEconfig.watchPaste = true;
                mainIdeController.Initialize(IDEconfig);
                mainIdeController.TextPreprocessor = PreprocessPastedText;
            }
        }

        if (generatedScreen.ColumnRefs.TryGetValue("RightTop_Column", out GridReferences rightRefs))
        {
            if (rightRefs.Buttons.TryGetValue("ToggleDetailsBtn", out Button toggleBtn))
            {
                toggleBtn.onClick.RemoveAllListeners();
                toggleBtn.onClick.AddListener(() => ToggleOverviewDetails());
            }

            if (rightRefs.IDEInterfaces.TryGetValue("RightTopIDE", out VirtualizedIdeController ideObj2))
            {
                phasesIdeController = ideObj2;
                IDEconfig = new SDTextmodSyntaxConfig();
                IDEconfig.watchPaste = false;
                phasesIdeController.Initialize(IDEconfig);
                phasesIdeController.TextPreprocessor = PreprocessPastedText;
            }
        }

        UpdateToggleButtonText();

        ClearSpacerText("Left_Column", "LeftSpacer");
        ClearSpacerText("RightTop_Column", "RightTopSpacer");
        ClearSpacerText("RightBottom_Column", "RightBottomSpacer");

        // Example placeholders for the right side panels
        PopulateDemoScrollView("RightBottom_Column", "RightBottomScrollView", "Right Bottom Demo Button");
    }
    private void ClearSpacerText(string columnName, string spacerKey)
    {
        if (generatedScreen.ColumnRefs.TryGetValue(columnName, out GridReferences refs))
        {
            /*
            if (refs.ImagePanels.TryGetValue(spacerKey, out Image img))
            {
                img.color = Color.clear;
                img.raycastTarget = false;
            }
            */
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
        ClipboardHelper.CopyToClipboard(finalOutputText);
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
        if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("RightTop_Column", out GridReferences refs))
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
        lastAnalyzedOverview = ModAnalyzer.Analyze(rawPaste);
        DisplayModOverview(lastAnalyzedOverview);

        string compressed = CompressImagesSync(rawPaste);
        return IDEFormatString ? AutoFormatModStringSync(compressed) : MinifyModString(compressed);
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

            sb.AppendLine($"  {blockTiming} [X] {typeLabel} - {directive.BlockName}");

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
}