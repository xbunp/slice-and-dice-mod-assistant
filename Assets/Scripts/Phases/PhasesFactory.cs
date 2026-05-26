using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhasesFactory : RootUI
{
    public bool IDEFormatString = true;

    private bool isUiActive = true;
    private bool isUpdatingText = false;

    private const long PerformanceThresholdMs = 10;
    private bool isPendingUpdate = false;
    private string pendingValue;

    // References for double-input overlay syntax highlighting
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;

    // =========================================================================
    // Image Compression Cache
    // =========================================================================
    private Dictionary<string, string> imgCache = new Dictionary<string, string>();
    private int imgCounter = 0;

    // Matches 'img.' followed by any characters that aren't a dot, ending with '.'
    private static readonly Regex ImgRegex = new Regex(@"img\.([^\.]+)\.", RegexOptions.Compiled);

    // =========================================================================
    // Syntax Highlighting Engine Palette (Precompiled & Cached)
    // =========================================================================

    private const string ColorFloor = "#569CD6";        // Blue (Keywords / Structure)
    private const string ColorPhasePrefix = "#569CD6";  // Blue (ph. prefix)
    private const string ColorPhaseCode = "#4EC9B0";    // Teal (Classes / Types / Core Actions)
    private const string ColorDelimiter = "#C586C0";    // Purple (Control / Flow operators)
    private const string ColorNumber = "#B5CEA8";       // Light Green (Constants / Value Literals)
    private const string ColorText = "#D4D4D4";         // Light Gray (Default identifiers)

    private const string ColorMod = "#CE9178";          // Light Terracotta (Modifiers / Strings)
    private const string ColorItem = "#9CDCFE";         // Cyan (Items / Variables)
    private const string ColorLvl = "#B5CEA8";          // Soft Green (Levelup variants)
    private const string ColorHero = "#DCDCAA";         // Light Yellow (Hero Specs / Entities)
    private const string ColorRand = "#D8A0DF";         // Soft Orchid/Pink (Random / Ranges)
    private const string ColorValue = "#4FC1FF";        // Sky Blue (State Variables / Values)
    private const string ColorSkip = "#FF7575";         // Soft Coral/Red (Bypass/Skip command)
    private const string ColorDefaultReward = "#FFD700"; // Goldenrod (Unspecified/Fallback)

    private static readonly Regex SyntaxRegex = new Regex(
        @"(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
        @"|(?<phase>ph\.[!0-9bcedglrstz])" +
        @"|(?<delimiter>&|@1|@2|@3|@4|@6|@7|;)" +
        @"|(?<reward>\(?[miglrqoveps][a-zA-Z0-9_\-~.\^ ]*\)?)" +
        @"|(?<number>\b\d+\b)" +
        @"|(?<text>[a-zA-Z_][a-zA-Z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private void Update()
    {
        // 1. Intercept massive pastes before TMP_InputField can freeze the main thread
        if (rawTextOutput != null && rawTextOutput.isFocused && !isUpdatingText)
        {
            bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
            bool modifier = isMac ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (modifier && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboard) && clipboard.Length > 15000)
                {
                    // Lock the input instantly so TMP native OnGUI ignores the paste event
                    rawTextOutput.readOnly = true;

                    // Calculate where the text should be pasted based on user's cursor/selection
                    int selectStart = rawTextOutput.selectionStringFocusPosition;
                    int selectEnd = rawTextOutput.selectionStringAnchorPosition;
                    int start = Mathf.Min(selectStart, selectEnd);
                    int end = Mathf.Max(selectStart, selectEnd);

                    string currentText = rawTextOutput.text ?? "";
                    string newText;

                    if (start != end && start >= 0 && end <= currentText.Length)
                    {
                        newText = currentText.Remove(start, end - start).Insert(start, clipboard);
                    }
                    else
                    {
                        int caret = Mathf.Clamp(rawTextOutput.caretPosition, 0, currentText.Length);
                        newText = currentText.Insert(caret, clipboard);
                    }

                    // We consume this payload natively, bypassing TMP entirely
                    StartCoroutine(ProcessHeavyTextUpdatesCoroutine(newText));
                    return; // Skip standard checks
                }
            }
        }

        // 2. Standard deferred update for standard typing or smaller text inputs
        if (isPendingUpdate && !isUpdatingText)
        {
            isPendingUpdate = false;
            StartCoroutine(ProcessHeavyTextUpdatesCoroutine(pendingValue));
        }
    }

    private IEnumerator ProcessHeavyTextUpdatesCoroutine(string value)
    {
        isUpdatingText = true;
        UIPopup popup = null;

        bool isLargeText = value != null && value.Length > 15000;

        if (isLargeText)
        {
            if (rawTextOutput != null) rawTextOutput.readOnly = true;

            popup = uiGenerator.CreatePopup("Processing large text block, please wait...", false);
            Canvas.ForceUpdateCanvases();

            // Yield so the GPU physically renders the popup to the screen before the thread lock occurs
            yield return null;
            yield return null;
        }

        // --- ALL HEAVY LIFTING DONE SYNCHRONOUSLY BELOW ---
        ProcessHeavyTextUpdates(value);

        if (popup != null) popup.Dismiss();

        if (isLargeText && rawTextOutput != null)
        {
            rawTextOutput.readOnly = false;
        }

        isUpdatingText = false;
    }

    private void ToggleIDE()
    {
        IDEFormatString = !IDEFormatString;
        UpdateToggleButtonText();

        if (rawTextOutput != null)
        {
            pendingValue = rawTextOutput.text;
            isPendingUpdate = true;
        }
    }

    private void UpdateToggleButtonText()
    {
        if (generatedScreen != null &&
            generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences refs))
        {
            if (refs.Buttons.TryGetValue("Button1", out Button btn))
            {
                var textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (textMesh != null) textMesh.text = IDEFormatString ? "Toggle IDE Formatting OFF" : "Toggle IDE Formatting ON";
            }
        }
    }

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
        float leftScrollHeight = leftPanelHeight - buttonRowHeight - spacing;

        List<ColumnSpec> columns = new List<ColumnSpec>();
        List<GridRowSpec> leftRows = new List<GridRowSpec>
        {
            new GridRowSpec(leftSpacerHeight, GridCellSpec.CreateLabel("LeftSpacer", "", 1.0f)),
            new GridRowSpec(buttonRowHeight,
                GridCellSpec.CreateButton("Button1", "Toggle IDE Formatting", 0.5f, () => ToggleIDE()),
                GridCellSpec.CreateButton("Button2", "Copy & Restore Code", 0.5f, () => CopyAndRestoreCode()) // NEW COPY BUTTON
            ),
            new GridRowSpec(leftScrollHeight, GridCellSpec.CreateScrollView("LeftInputScrollView", 1.0f)),
            new GridRowSpec(0f, GridCellSpec.CreateInput("MainLeftInput", "Enter logs or notes here...", 1.0f, OnLeftInputChanged))
        };
        columns.Add(new ColumnSpec("Left_Column", 0.0f, 0.5f, leftRows));

        List<GridRowSpec> rightTopRows = new List<GridRowSpec>
        {
            new GridRowSpec(rightTopSpacerHeight, GridCellSpec.CreateLabel("RightTopSpacer", "", 1.0f)),
            new GridRowSpec(rightPanelHeight, GridCellSpec.CreateScrollView("RightTopScrollView", 1.0f))
        };
        columns.Add(new ColumnSpec("RightTop_Column", 0.5f, 1.0f, rightTopRows));

        List<GridRowSpec> rightBottomRows = new List<GridRowSpec>
        {
            new GridRowSpec(rightBottomSpacerHeight, GridCellSpec.CreateLabel("RightBottomSpacer", "", 1.0f)),
            new GridRowSpec(rightPanelHeight, GridCellSpec.CreateScrollView("RightBottomScrollView", 1.0f))
        };
        columns.Add(new ColumnSpec("RightBottom_Column", 0.5f, 1.0f, rightBottomRows));

        uiGenerator.PopulateScreen(generatedScreen, columns, useMargins);

        if (generatedScreen != null) PostProcessLayout();
    }
    private void PostProcessLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            if (leftRefs.Inputs.TryGetValue("MainLeftInput", out TMP_InputField leftInput) &&
                leftRefs.ScrollViews.TryGetValue("LeftInputScrollView", out ScrollRect scrollView))
            {
                rawTextOutput = leftInput;

                scrollView.horizontal = false;
                scrollView.vertical = true;
                rawTextOutput.transform.SetParent(scrollView.content, false);

                var contentLayout = scrollView.content.GetComponent<VerticalLayoutGroup>();
                if (contentLayout == null) contentLayout = scrollView.content.gameObject.AddComponent<VerticalLayoutGroup>();
                contentLayout.childControlHeight = true;
                contentLayout.childControlWidth = true;
                contentLayout.childForceExpandHeight = true;
                contentLayout.childForceExpandWidth = true;

                var contentFitter = scrollView.content.GetComponent<ContentSizeFitter>();
                if (contentFitter == null) contentFitter = scrollView.content.gameObject.AddComponent<ContentSizeFitter>();
                contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                var contentLayoutElement = scrollView.content.GetComponent<LayoutElement>();
                if (contentLayoutElement != null) Destroy(contentLayoutElement);

                var inputFitter = rawTextOutput.GetComponent<ContentSizeFitter>();
                if (inputFitter != null) Destroy(inputFitter);

                var inputLayoutElement = rawTextOutput.GetComponent<LayoutElement>();
                if (inputLayoutElement == null) inputLayoutElement = rawTextOutput.gameObject.AddComponent<LayoutElement>();

                float viewportHeight = scrollView.viewport != null ? scrollView.viewport.rect.height : scrollView.GetComponent<RectTransform>().rect.height;
                inputLayoutElement.minHeight = viewportHeight;

                var scrollPassThrough = rawTextOutput.GetComponent<ScrollPassThrough>();
                if (scrollPassThrough == null)
                {
                    scrollPassThrough = rawTextOutput.gameObject.AddComponent<ScrollPassThrough>();
                }
                scrollPassThrough.TargetScrollRect = scrollView;

                if (rawTextOutput.textViewport != null)
                {
                    rawTextOutput.textViewport.anchorMin = Vector2.zero;
                    rawTextOutput.textViewport.anchorMax = Vector2.one;
                    rawTextOutput.textViewport.offsetMin = new Vector2(8, 8);
                    rawTextOutput.textViewport.offsetMax = new Vector2(-8, -8);
                }

                if (rawTextOutput.textComponent != null)
                {
                    RectTransform textRt = rawTextOutput.textComponent.GetComponent<RectTransform>();
                    if (textRt != null)
                    {
                        textRt.anchorMin = Vector2.zero;
                        textRt.anchorMax = Vector2.one;
                        textRt.offsetMin = Vector2.zero;
                        textRt.offsetMax = Vector2.zero;
                    }
                }

                if (rawTextOutput.placeholder != null)
                {
                    RectTransform placeholderRt = rawTextOutput.placeholder.GetComponent<RectTransform>();
                    if (placeholderRt != null)
                    {
                        placeholderRt.anchorMin = Vector2.zero;
                        placeholderRt.anchorMax = Vector2.one;
                        placeholderRt.offsetMin = Vector2.zero;
                        placeholderRt.offsetMax = Vector2.zero;
                    }
                }

                rawTextOutput.lineType = TMP_InputField.LineType.MultiLineNewline;
                rawTextOutput.textComponent.alignment = TextAlignmentOptions.TopLeft;

                var placeholder = rawTextOutput.placeholder as TextMeshProUGUI;
                if (placeholder != null) placeholder.alignment = TextAlignmentOptions.TopLeft;

                rawTextOutput.textComponent.color = Color.clear;
                rawTextOutput.customCaretColor = true;
                rawTextOutput.caretColor = Color.white;
                rawTextOutput.richText = false;
                rawTextOutput.textComponent.enableAutoSizing = false;
                rawTextOutput.pointSize = 12;
                rawTextOutput.textComponent.autoSizeTextContainer = false;

                GameObject highlighterObj = Instantiate(uiGenerator.labelPrefab, rawTextOutput.textComponent.transform.parent);
                highlighterObj.name = "SyntaxHighlighter";
                syntaxHighlighterText = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();

                var canvasGroup = highlighterObj.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;

                foreach (var script in highlighterObj.GetComponents<MonoBehaviour>())
                {
                    if (script != null && !(script is TextMeshProUGUI)) DestroyImmediate(script);
                }

                RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
                RectTransform textCompRt = rawTextOutput.textComponent.GetComponent<RectTransform>();
                highlightRt.anchorMin = textCompRt.anchorMin;
                highlightRt.anchorMax = textCompRt.anchorMax;
                highlightRt.offsetMin = textCompRt.offsetMin;
                highlightRt.offsetMax = textCompRt.offsetMax;
                highlightRt.pivot = textCompRt.pivot;

                syntaxHighlighterText.enableAutoSizing = false;
                syntaxHighlighterText.fontSize = 12;
                syntaxHighlighterText.alignment = rawTextOutput.textComponent.alignment;
                syntaxHighlighterText.margin = rawTextOutput.textComponent.margin;
                syntaxHighlighterText.enableWordWrapping = rawTextOutput.textComponent.enableWordWrapping;
                syntaxHighlighterText.autoSizeTextContainer = false;
                syntaxHighlighterText.richText = true;

                OnLeftInputChanged(rawTextOutput.text);
            }
        }

        UpdateToggleButtonText();

        ClearSpacerText("Left_Column", "LeftSpacer");
        ClearSpacerText("RightTop_Column", "RightTopSpacer");
        ClearSpacerText("RightBottom_Column", "RightBottomSpacer");

        PopulateDemoScrollView("RightTop_Column", "RightTopScrollView", "Right Top Button");
        PopulateDemoScrollView("RightBottom_Column", "RightBottomScrollView", "Right Bottom Button");
    }
    private void ClearSpacerText(string columnName, string spacerKey)
    {
        if (generatedScreen.ColumnRefs.TryGetValue(columnName, out GridReferences refs))
        {
            if (refs.ImagePanels.TryGetValue(spacerKey, out Image img))
            {
                img.color = Color.clear;
                img.raycastTarget = false;
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
        isUiActive = !isUiActive;
        foreach (var keyValuePair in generatedScreen.ColumnPanels)
        {
            if (keyValuePair.Value != null) keyValuePair.Value.gameObject.SetActive(isUiActive);
        }
    }

    private void AppendNewlineAndIndent(StringBuilder sb, int indent)
    {
        while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
        if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
        if (indent > 0) sb.Append(' ', indent);
    }
    private void AppendEscaped(StringBuilder sb, string text, int start, int length)
    {
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (c == '<') sb.Append("&lt;");
            else if (c == '>') sb.Append("&gt;");
            else sb.Append(c);
        }
    }
    private void AppendWithColor(StringBuilder sb, string text, int start, int length, string colorHex)
    {
        sb.Append("<color=").Append(colorHex).Append(">");
        AppendEscaped(sb, text, start, length);
        sb.Append("</color>");
    }
    private char GetRewardTagChar(string input, int start, int length)
    {
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            if (input[i] != '(') return input[i];
        }
        return '\0';
    }

    // =========================================================================
    // Core Engine Logic
    // =========================================================================

    private string CompressImages(string text, StringBuilder log = null)
    {
        Stopwatch sw = Stopwatch.StartNew();
        if (string.IsNullOrEmpty(text) || !text.Contains("img.")) return text;

        string compressed = ImgRegex.Replace(text, match => {
            string innerData = match.Groups[1].Value;

            // Skip compression if already compressed, or if the string is 25 characters or fewer
            if (innerData.StartsWith("IMG_") || innerData.Length <= 25) return match.Value;

            string newId = $"IMG_{++imgCounter}";
            imgCache[newId] = innerData;
            return $"img.{newId}.";
        });

        sw.Stop();
        log?.AppendLine($"    [Sub-Profiler] CompressImages took: {sw.ElapsedMilliseconds} ms (Extracted {imgCounter} images)");
        return compressed;
    }

    private string RestoreImages(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("img.IMG_")) return text;

        return Regex.Replace(text, @"img\.(IMG_\d+)\.", match => {
            string id = match.Groups[1].Value;
            if (imgCache.TryGetValue(id, out string data))
            {
                return $"img.{data}.";
            }
            return match.Value; // Fallback
        });
    }

    private void UpdateInputFieldHeight(string formattedText, StringBuilder log = null)
    {
        if (rawTextOutput == null) return;
        var inputLayoutElement = rawTextOutput.GetComponent<LayoutElement>();
        if (inputLayoutElement == null) return;

        Stopwatch prefValSw = Stopwatch.StartNew();
        float textHeight = rawTextOutput.textComponent.GetPreferredValues(formattedText).y;
        prefValSw.Stop();

        float extraPadding = 32f;
        float viewportHeight = 100f;

        if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
        {
            if (leftRefs.ScrollViews.TryGetValue("LeftInputScrollView", out ScrollRect scrollView))
            {
                viewportHeight = scrollView.viewport != null
                    ? scrollView.viewport.rect.height
                    : scrollView.GetComponent<RectTransform>().rect.height;

                float newHeight = Mathf.Max(viewportHeight, textHeight + extraPadding);

                if (Mathf.Abs(inputLayoutElement.preferredHeight - newHeight) > 1f)
                {
                    inputLayoutElement.preferredHeight = newHeight;
                }
            }
        }

        log?.AppendLine($"    [Sub-Profiler] UpdateInputFieldHeight -> GetPreferredValues: {prefValSw.ElapsedMilliseconds} ms | Rebuild: Deferred Native Canvas update");
    }

    public string MinifyModString(string raw, StringBuilder log = null)
    {
        Stopwatch sw = Stopwatch.StartNew();
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

        sw.Stop();
        log?.AppendLine($"    [Sub-Profiler] MinifyModString took: {sw.ElapsedMilliseconds} ms");
        return sb.ToString();
    }

    private void OnLeftInputChanged(string value)
    {
        if (isUpdatingText) return;
        pendingValue = value;
        isPendingUpdate = true;
    }

    private void ProcessHeavyTextUpdates(string value)
    {
        Stopwatch totalSw = Stopwatch.StartNew();
        StringBuilder logAccumulator = new StringBuilder();

        // 0. Pre-process Image Compression (Drastically lowers string length before expensive operations)
        Stopwatch stageSw = Stopwatch.StartNew();
        string compressedValue = CompressImages(value, logAccumulator);
        stageSw.Stop();
        long compressTime = stageSw.ElapsedMilliseconds;

        // 1. Minify & Auto Format Stage
        stageSw.Restart();
        string formattedValue = compressedValue;
        if (IDEFormatString)
        {
            formattedValue = AutoFormatModString(compressedValue, logAccumulator);
        }
        else
        {
            formattedValue = MinifyModString(compressedValue, logAccumulator);
        }
        stageSw.Stop();
        long formatTime = stageSw.ElapsedMilliseconds;

        // 2. Raw Text UI Assignment Stage
        stageSw.Restart();
        if (rawTextOutput != null && rawTextOutput.text != formattedValue)
        {
            int originalCaret = rawTextOutput.caretPosition;
            rawTextOutput.SetTextWithoutNotify(formattedValue);
            rawTextOutput.caretPosition = Mathf.Min(originalCaret, formattedValue.Length);
        }
        stageSw.Stop();
        long uiAssignTime = stageSw.ElapsedMilliseconds;

        // 3. Syntax Highlighting Stage
        stageSw.Restart();
        string highlightedValue = string.Empty;
        if (syntaxHighlighterText != null)
        {
            highlightedValue = FormatSyntaxHighlighting(formattedValue, logAccumulator);
            syntaxHighlighterText.text = highlightedValue;
        }
        stageSw.Stop();
        long syntaxTime = stageSw.ElapsedMilliseconds;

        // 4. Height Adjustment Stage
        stageSw.Restart();
        UpdateInputFieldHeight(formattedValue, logAccumulator);
        stageSw.Stop();
        long heightTime = stageSw.ElapsedMilliseconds;

        totalSw.Stop();
        long totalTime = totalSw.ElapsedMilliseconds;

        if (totalTime >= PerformanceThresholdMs)
        {
            StringBuilder finalReport = new StringBuilder();
            finalReport.AppendLine($"[Profiler Warning] Deferred update took ({totalTime} ms) | Original Input Length: {value?.Length ?? 0} -> Compressed: {compressedValue?.Length ?? 0}");
            finalReport.AppendLine($"  - Step 0: Image Compression: {compressTime} ms");
            finalReport.AppendLine($"  - Step 1: AutoFormatModString: {formatTime} ms");
            finalReport.AppendLine($"  - Step 2: UI Assignment/Caret: {uiAssignTime} ms");
            finalReport.AppendLine($"  - Step 3: Syntax Highlighting: {syntaxTime} ms");
            finalReport.AppendLine($"  - Step 4: Height Adjustment: {heightTime} ms");
            finalReport.Append(logAccumulator.ToString());

            UnityEngine.Debug.LogWarning(finalReport.ToString());
        }
    }

    public string AutoFormatModString(string raw, StringBuilder log = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string minified = MinifyModString(raw, log);

        Stopwatch formatLoopSw = Stopwatch.StartNew();
        StringBuilder sb = new StringBuilder(minified.Length + 500);
        int indentLevel = 0;

        for (int i = 0; i < minified.Length; i++)
        {
            char c = minified[i];

            if (c == '&')
            {
                AppendNewlineAndIndent(sb, indentLevel * 4);
                sb.Append(c);
            }
            else if (c == '@' && i + 1 < minified.Length && char.IsDigit(minified[i + 1]))
            {
                AppendNewlineAndIndent(sb, (indentLevel + 1) * 4);
                sb.Append(c);
                sb.Append(minified[++i]);
            }
            else if (c == ';')
            {
                AppendNewlineAndIndent(sb, (indentLevel + 1) * 4);
                sb.Append(c);
            }
            else if ((c == '{' && i + 1 < minified.Length && minified[i + 1] == '}') ||
                     (c == '(' && i + 1 < minified.Length && minified[i + 1] == ')'))
            {
                sb.Append(c);
                sb.Append(minified[++i]);
            }
            else if (c == '{' || c == '(')
            {
                AppendNewlineAndIndent(sb, indentLevel * 4);
                sb.Append(c);

                indentLevel++;
                AppendNewlineAndIndent(sb, indentLevel * 4);
            }
            else if (c == '}' || c == ')')
            {
                indentLevel = Math.Max(0, indentLevel - 1);
                AppendNewlineAndIndent(sb, indentLevel * 4);
                sb.Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }

        string result = sb.ToString().TrimStart();
        formatLoopSw.Stop();

        log?.AppendLine($"    [Sub-Profiler] AutoFormatModString (Loop) took: {formatLoopSw.ElapsedMilliseconds} ms");
        return result;
    }

    public string FormatSyntaxHighlighting(string input, StringBuilder log = null)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        Stopwatch regexSw = Stopwatch.StartNew();
        MatchCollection matches;
        int matchCount = 0;
        try
        {
            matches = SyntaxRegex.Matches(input);
            matchCount = matches.Count;
        }
        catch (Exception ex)
        {
            log?.AppendLine($"    [Sub-Profiler] FormatSyntaxHighlighting -> Regex execution failed: {ex.Message}");
            return input;
        }
        regexSw.Stop();

        Stopwatch buildSw = Stopwatch.StartNew();
        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;

        foreach (Match m in matches)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AppendEscaped(sb, input, lastIndex, idx - lastIndex);

            if (m.Groups["floor"].Success) AppendWithColor(sb, input, idx, len, ColorFloor);
            else if (m.Groups["phase"].Success)
            {
                if (len > 3)
                {
                    sb.Append("<color=").Append(ColorPhasePrefix).Append(">");
                    AppendEscaped(sb, input, idx, 3);
                    sb.Append("</color><color=").Append(ColorPhaseCode).Append(">");
                    AppendEscaped(sb, input, idx + 3, len - 3);
                    sb.Append("</color>");
                }
                else AppendWithColor(sb, input, idx, len, ColorPhasePrefix);
            }
            else if (m.Groups["delimiter"].Success) AppendWithColor(sb, input, idx, len, ColorDelimiter);
            else if (m.Groups["reward"].Success)
            {
                char tagChar = GetRewardTagChar(input, idx, len);
                string rewardColor = tagChar switch
                {
                    'm' => ColorMod,
                    'i' => ColorItem,
                    'l' => ColorLvl,
                    'g' => ColorHero,
                    'r' => ColorRand,
                    'q' => ColorRand,
                    'o' => ColorRand,
                    'v' => ColorValue,
                    's' => ColorSkip,
                    _ => ColorDefaultReward
                };
                AppendWithColor(sb, input, idx, len, rewardColor);
            }
            else if (m.Groups["number"].Success) AppendWithColor(sb, input, idx, len, ColorNumber);
            else if (m.Groups["text"].Success) AppendWithColor(sb, input, idx, len, ColorText);
            else AppendEscaped(sb, input, idx, len);

            lastIndex = idx + len;
        }

        if (lastIndex < input.Length) AppendEscaped(sb, input, lastIndex, input.Length - lastIndex);

        buildSw.Stop();
        log?.AppendLine($"    [Sub-Profiler] FormatSyntaxHighlighting -> Regex: {regexSw.ElapsedMilliseconds} ms ({matchCount} matches) | Reconstruction: {buildSw.ElapsedMilliseconds} ms");

        return sb.ToString();
    }

    private void CopyAndRestoreCode()
    {
        if (rawTextOutput == null) return;

        string regular = MinifyModString(rawTextOutput.text);
        string currentText = regular;
        string restoredText = RestoreImages(currentText);

        // Insert linebreaks after commas
        string formattedText = InsertLinebreaksAfterCommas(restoredText);

        GUIUtility.systemCopyBuffer = formattedText;
        UnityEngine.Debug.Log($"Copied to clipboard. Final Length: {formattedText.Length}");

        // Optional: you could spawn a quick popup here if you want visual confirmation
        uiGenerator.CreatePopup("Copied output to clipboard!", true, null);
    }

    private string InsertLinebreaksAfterCommas(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Replaces each comma with a comma followed by a newline character
        return input.Replace(",", ",\n");
    }
}