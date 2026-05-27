using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;


public class PhasesFactory : RootUI
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);
#endif

    public bool IDEFormatString = true;

    private string tempClipboardBuffer;

    private bool isUiActive = true;
    private bool isUpdatingText = false;

    private const long PerformanceThresholdMs = 10;
    private bool isPendingUpdate = false;
    private string pendingValue;

    private ContentSizeFitter contentFitter;

    // --- IDE Typing & Debounce State ---
    private Coroutine debounceCoroutine;
    private Coroutine backgroundHighlightCoroutine;
    private float typingDebounceDelay = 0.75f; // Wait 750ms after last keystroke before highlighting

    // References for double-input overlay syntax highlighting
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;

    // =========================================================================
    // Image Compression Cache
    // =========================================================================
    private Dictionary<string, string> imgCache = new Dictionary<string, string>();
    private int imgCounter = 0;

    private static readonly Regex ImgRegex = new Regex(@"img\.(.*?)\.|\[(.*?)\]", RegexOptions.Compiled);
    // =========================================================================
    // Syntax Highlighting Engine Palette (Precompiled & Cached)
    // =========================================================================


    // =========================================================================
    // Syntax Highlighting Engine Palette (Precompiled & Cached)
    // =========================================================================

    private const string ColorFloor = "#569CD6";        // Blue
    private const string ColorPhasePrefix = "#569CD6";  // Blue
    private const string ColorPhaseCode = "#4EC9B0";    // Teal
    private const string ColorDelimiter = "#C586C0";    // Purple (Used for & @ ; Hidden)
    private const string ColorNumber = "#B5CEA8";       // Light Green
    private const string ColorText = "#D4D4D4";         // Light Gray

    private const string ColorBracket = "#569cd6";      // Blueish (for brackets)
    private const string ColorMethod = "#dcdcaa";       // Yellow (for methods like img. sd. n.)
    private const string ColorSdRed = "#FFA961";        // Red (for sd sequences)
    private const string ColorMossGreen = "#A4C365";    // Moss Green for k. blocks
    private const string ColorNeonGreen = "#39FF14";    // Neon Green for tog* blocks

    private const string ColorMod = "#CE9178";          // Light Terracotta
    private const string ColorItem = "#9CDCFE";         // Cyan (Used for itempool members)
    private const string ColorLvl = "#B5CEA8";          // Soft Green
    private const string ColorHero = "#DCDCAA";         // Light Yellow
    private const string ColorRand = "#D8A0DF";         // Soft Orchid/Pink
    private const string ColorValue = "#4FC1FF";        // Sky Blue
    private const string ColorSkip = "#FF7575";         // Soft Coral/Red
    private const string ColorDefaultReward = "#FFD700"; // Goldenrod

    private static readonly Regex SyntaxRegex = new Regex(
            // 1. Floor
            @"(?<=^|[(&@!~\[])(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
            // 2. Phase: Added \.phi
            @"|(?<phase>ph\.[!0-9bcedglrstz]|\.phi\b)" +
            // 3. Delimiter
            @"|(?<delimiter>&|@\d+|;|\b(?i:Hidden|skip(?: all)?|temporary|Delevel|Level Up|No Flee)\b)" +
            // 4. Square Brackets
            @"|(?<sq_bracket>\[[^\]]*\])" +
            // 5. Normal Brackets
            @"|(?<bracket>[{}()])" +
            // 6. Itempool Block
            @"|(?<itempool_block>itempool\.[^\.]*)" +
            // 7. SD Block: Now ends at . OR )
            @"|(?<sd_block>sd\.[^.)]*)" +
            // 8. Ritemx Block: Optional i., ends at . OR )
            @"|(?<ritemx>(?i)(?:i\.)?ritemx\.[^.)]*)" +
            // 9. HSV Block
            @"|(?<hsv_block>(?i)hsv\.[^\.]*)" +
            // 10. K Block: Now ends at . OR #
            @"|(?<k_block>k\.[^.#]*)" +
            // 11. Tog toggles
            @"|(?<tog>\btog[a-zA-Z0-9_]*\b)" +
            // 12. Methods (Added all new prefixes and suffixes)
            @"|(?<method>\b(?:img|col|n|tier|facade|sidesc|heropool|learn|hp|bal|mn|hat|abilitydata|replica|h|ch|hsv|part|difficulty|diff|splice|jinx|allitem|self|p|topbot|brittle|left|right|row|all|right5|right3|right2|mid2|left2|rightmost|bot|top|mid)\.|\.(?:modtier|add|doc|all|egg|hsv|speech|unpack)\b)" +
            // 13. Reward
            @"|(?<reward>\(?[miglrqovs]\.[a-zA-Z0-9_\-~\^\/ ]*\)?|(?<=[\!&@+=])\(?[miglrqovs]\b\)?|\(?[miglrqovs][a-zA-Z0-9_\-~\^\/]*[\~^\/][a-zA-Z0-9_\-~\^\/]*\)?)" +
            // 14. Number
            @"|(?<number>\b\d+\b)" +
            // 15. Text
            @"|(?<text>[a-zA-Z_][a-zA-Z0-9_]*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    /*
    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (rawTextOutput != null && rawTextOutput.isFocused && !isUpdatingText)
        {
            bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
            bool modifier = isMac ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (modifier && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboard) && clipboard.Length > 15000)
                {
                    PasteTextDirectly(clipboard);
                    return;
                }
            }
        }
#endif

        if (isPendingUpdate && !isUpdatingText)
        {
            isPendingUpdate = false;
            StartCoroutine(ProcessHeavyTextUpdatesCoroutine(pendingValue));
        }
    }
    */
    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (rawTextOutput != null && rawTextOutput.isFocused && !isUpdatingText)
        {
            bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
            bool modifier = isMac ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (modifier && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(clipboard) && clipboard.Length > 15000)
                {
                    PasteTextDirectly(clipboard);
                    return;
                }
            }
        }
#endif
    }

    // --- UI SETUP ---
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
                GridCellSpec.CreateButton("Button1", "Toggle IDE Formatting", 0.33f, () => ToggleIDE()),
                GridCellSpec.CreateButton("Button2", "Paste Textmod (FASTER)", 0.33f, () => {}),
                GridCellSpec.CreateButton("Button3", "Copy & Restore Code", 0.33f, () => CopyAndRestoreCode()) // NEW COPY BUTTON
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
            if (leftRefs.Buttons.TryGetValue("Button2", out Button pasteBtn))
            {
                pasteBtn.onClick.RemoveAllListeners();
                pasteBtn.onClick.AddListener(() => {
                    ClipboardManager.RequestPaste(uiGenerator, (clipboardText) => {
                        if (!string.IsNullOrEmpty(clipboardText))
                        {
                            UnityEngine.Debug.Log($"[PasteTextMod] Paste successful. Length: {clipboardText.Length}");
                            PasteTextDirectly(clipboardText);
                        }
                    });
                });
            }

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

                contentFitter = scrollView.content.GetComponent<ContentSizeFitter>();
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

    /*
    private void OnLeftInputChanged(string value)
    {
        if (isUpdatingText) return; // Ignore if we are doing a forced paste/format

        // INSTANT FEEDBACK: Show raw text (white) and hide the highlighter
        if (rawTextOutput != null && syntaxHighlighterText != null)
        {
            if (ColorUtility.TryParseHtmlString(ColorText, out Color myColor))
            {
                syntaxHighlighterText.color = myColor;
            }
            else
            {
                syntaxHighlighterText.color = Color.white;
            }
            syntaxHighlighterText.color = Color.clear;
        }

        // Stop the timer if the user keeps typing
        if (debounceCoroutine != null) StopCoroutine(debounceCoroutine);

        // Start a new timer
        debounceCoroutine = StartCoroutine(HandleTypingDebounce(value));
    }
    */

    private void OnLeftInputChanged(string value)
    {
        if (isUpdatingText) return;

        // 1. BREAK THE CHAIN OF DOOM: Instantly prevent the expensive layout calculation.
        if (contentFitter != null) contentFitter.enabled = false;

        // 2. INSTANT FEEDBACK: Show raw text and hide the (now stale) highlighter.
        if (rawTextOutput != null && syntaxHighlighterText != null)
        {
            rawTextOutput.textComponent.color = Color.white;
            syntaxHighlighterText.color = Color.clear;
        }

        // 3. Start the debounce timer.
        if (debounceCoroutine != null) StopCoroutine(debounceCoroutine);
        debounceCoroutine = StartCoroutine(HandleTypingDebounce(value));
    }
    private IEnumerator HandleTypingDebounce(string value)
    {
        // Wait for the user to stop typing for a moment
        yield return new WaitForSeconds(typingDebounceDelay);

        // If a background highlight is already running from a previous pause, stop it
        if (backgroundHighlightCoroutine != null) StopCoroutine(backgroundHighlightCoroutine);

        // Start the silent background highlighter
        backgroundHighlightCoroutine = StartCoroutine(BackgroundSyntaxHighlightOnlyAsync(value));
    }

    // --- Initiators  ---
    public void OnClipboardDataReceived(string clipboardText)
    {
        if (!string.IsNullOrEmpty(clipboardText))
        {
            UnityEngine.Debug.Log($"[PasteTextMod] Async Paste successful! Length: {clipboardText.Length}");
            PasteTextDirectly(clipboardText);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[PasteTextMod] Async Paste failed or was blocked.");
        }
    }
    private void PasteTextDirectly(string clipboard)
    {
        if (rawTextOutput == null) return;

        // Lock UI instantly
        rawTextOutput.readOnly = true;

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

        // We bypass TMP rendering update by bypassing standard input
        StartCoroutine(ProcessHeavyTextUpdatesCoroutine(newText));
    }

    // --- ASYNC PROCESSORS ---
    private IEnumerator ProcessHeavyTextUpdatesCoroutine(string value)
    {
        isUpdatingText = true;

        if (debounceCoroutine != null) StopCoroutine(debounceCoroutine);
        if (backgroundHighlightCoroutine != null) StopCoroutine(backgroundHighlightCoroutine);

        // --- NEW: Disable layout system during heavy processing ---
        if (contentFitter != null) contentFitter.enabled = false;

        UIPopup popup = null;
        bool isLargeText = value != null && value.Length > 15000;

        if (isLargeText)
        {
            if (rawTextOutput != null) rawTextOutput.readOnly = true;
            popup = uiGenerator.CreatePopup("Processing large text block, please wait...", false);
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null; // Ensure rendering happens
        }

        Stopwatch sw = Stopwatch.StartNew();
        string currentString = value;

        // Step 1: Compress Images asynchronously
        yield return StartCoroutine(CompressImagesAsync(currentString, result => currentString = result));

        // Step 2: Auto-Format or Minify asynchronously
        if (IDEFormatString)
        {
            yield return StartCoroutine(AutoFormatModStringAsync(currentString, result => currentString = result));
        }
        else
        {
            // Just call it synchronously, then yield a frame
            currentString = MinifyModString(currentString);
            yield return null;
        }

        // Step 3: Assign to UI (Synchronous, but we use WithoutNotify to avoid lag)
        if (rawTextOutput != null && rawTextOutput.text != currentString)
        {
            int originalCaret = rawTextOutput.caretPosition;
            rawTextOutput.SetTextWithoutNotify(currentString);
            rawTextOutput.caretPosition = Mathf.Min(originalCaret, currentString.Length);
        }
        yield return null;

        // Step 4: Syntax Highlighting asynchronously
        if (syntaxHighlighterText != null)
        {
            string highlighted = string.Empty;
            yield return StartCoroutine(FormatSyntaxHighlightingAsync(currentString, result => highlighted = result));
            syntaxHighlighterText.text = highlighted;
        }
        yield return null;

        if (contentFitter != null) contentFitter.enabled = true;
        UpdateInputFieldHeight(currentString);

        if (popup != null) popup.Dismiss();
        if (rawTextOutput != null) rawTextOutput.readOnly = false;

        if (rawTextOutput != null && syntaxHighlighterText != null)
        {
            if (ColorUtility.TryParseHtmlString(ColorText, out Color myColor))
            {
                syntaxHighlighterText.color = myColor;
            }
            else
            {
                syntaxHighlighterText.color = Color.white;
            }
            rawTextOutput.textComponent.color = Color.clear;
        }

        sw.Stop();
        UnityEngine.Debug.Log($"[Profiler] Async pipeline finished in {sw.ElapsedMilliseconds}ms real-time.");

        isUpdatingText = false;
    }
    private IEnumerator CompressImagesAsync(string text, Action<string> onComplete)
    {
        // Fast exit check: updated to also look for brackets
        if (string.IsNullOrEmpty(text) || (!text.Contains("img.") && !text.Contains("[")))
        {
            onComplete(text);
            yield break;
        }

        StringBuilder sb = new StringBuilder(text.Length);
        Match m = ImgRegex.Match(text); // Uses: @"img\.(.*?)\.|\[(.*?)\]"
        int lastIndex = 0;
        int operations = 0;

        while (m.Success)
        {
            // Append the text before the match (Zero allocation substring)
            sb.Append(text, lastIndex, m.Index - lastIndex);

            // Determine if it was a bracket match by checking the first character
            bool isBracket = m.Value.StartsWith("[");

            // If it's a bracket match, the data is in Group 2. Otherwise, Group 1.
            string innerData = isBracket ? m.Groups[2].Value : m.Groups[1].Value;

            if (innerData.StartsWith("IMG_") || innerData.Length <= 25)
            {
                sb.Append(m.Value);
            }
            else
            {
                string newId = $"IMG_{++imgCounter}";
                imgCache[newId] = innerData;

                // Reconstruct with the appropriate formatting
                if (isBracket)
                {
                    sb.Append($"[{newId}]");
                }
                else
                {
                    sb.Append($"img.{newId}.");
                }
            }

            lastIndex = m.Index + m.Length;
            operations++;

            if (operations > SettingsManager.REGEX_MATCHES_PER_FRAME)
            {
                operations = 0;
                yield return null; // Yield back to main thread
            }

            m = m.NextMatch();
        }

        if (lastIndex < text.Length) sb.Append(text, lastIndex, text.Length - lastIndex);

        onComplete(sb.ToString());
    }
    private IEnumerator AutoFormatModStringAsync(string raw, Action<string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            onComplete(string.Empty);
            yield break;
        }

        string minified = MinifyModString(raw);
        yield return null; // Breathe for one frame

        StringBuilder sb = new StringBuilder(minified.Length + 500);
        int indentLevel = 0;
        int operations = 0;

        for (int i = 0; i < minified.Length; i++)
        {
            char c = minified[i];

            if (c == ',')
            {
                indentLevel = 0;
                sb.Append(c);
            }
            else if (c == '&')
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

            operations++;
            if (operations > SettingsManager.CHARS_PER_FRAME)
            {
                operations = 0;
                yield return null;
            }
        }

        onComplete(sb.ToString().TrimStart());
    }
    private IEnumerator FormatSyntaxHighlightingAsync(string input, Action<string> onComplete)
    {
        if (string.IsNullOrEmpty(input))
        {
            onComplete(string.Empty);
            yield break;
        }

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;
        int operations = 0;

        Match m = null;
        try { m = SyntaxRegex.Match(input); } catch { onComplete(input); yield break; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AppendEscaped(sb, input, lastIndex, idx - lastIndex);

            if (m.Groups["floor"].Success) AppendWithColor(sb, input, idx, len, ColorFloor);
            else if (m.Groups["phase"].Success)
            {
                // Check if it's "ph.X" (Starts with 'p') to split the color
                if (input[idx] == 'p' && len > 3)
                {
                    sb.Append("<color=").Append(ColorPhasePrefix).Append(">");
                    AppendEscaped(sb, input, idx, 3);
                    sb.Append("</color><color=").Append(ColorPhaseCode).Append(">");
                    AppendEscaped(sb, input, idx + 3, len - 3);
                    sb.Append("</color>");
                }
                // Otherwise it's ".phi", tint the whole word
                else AppendWithColor(sb, input, idx, len, ColorPhasePrefix);
            }
            else if (m.Groups["delimiter"].Success) AppendWithColor(sb, input, idx, len, ColorDelimiter);
            else if (m.Groups["sq_bracket"].Success || m.Groups["bracket"].Success) AppendWithColor(sb, input, idx, len, ColorBracket);
            else if (m.Groups["itempool_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscaped(sb, input, idx, 9); // "itempool."
                sb.Append("</color><color=").Append(ColorItem).Append(">");
                AppendEscaped(sb, input, idx + 9, len - 9);
                sb.Append("</color>");
            }
            else if (m.Groups["sd_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscaped(sb, input, idx, 3); // "sd."
                sb.Append("</color><color=").Append(ColorSdRed).Append(">"); // Using the Coral Red
                AppendEscaped(sb, input, idx + 3, len - 3);
                sb.Append("</color>");
            }
            else if (m.Groups["ritemx"].Success) AppendWithColor(sb, input, idx, len, ColorItem);
            else if (m.Groups["hsv_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscaped(sb, input, idx, 4); // "hsv."
                sb.Append("</color><color=").Append(ColorNumber).Append(">"); // Numbers after hsv.
                AppendEscaped(sb, input, idx + 4, len - 4);
                sb.Append("</color>");
            }
            else if (m.Groups["k_block"].Success) AppendWithColor(sb, input, idx, len, ColorMossGreen);
            else if (m.Groups["tog"].Success) AppendWithColor(sb, input, idx, len, ColorNeonGreen);
            else if (m.Groups["method"].Success) AppendWithColor(sb, input, idx, len, ColorMethod);
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
            operations++;

            if (operations > SettingsManager.REGEX_MATCHES_PER_FRAME)
            {
                operations = 0;
                yield return null;
            }

            m = m.NextMatch();
        }

        if (lastIndex < input.Length) AppendEscaped(sb, input, lastIndex, input.Length - lastIndex);

        onComplete(sb.ToString());
    }
    private IEnumerator BackgroundSyntaxHighlightOnlyAsync(string text)
    {
        if (syntaxHighlighterText == null) yield break;

        string highlighted = string.Empty;

        // Run the async highlighter
        yield return StartCoroutine(FormatSyntaxHighlightingAsync(text, result => highlighted = result));

        if (syntaxHighlighterText != null)
        {
            syntaxHighlighterText.text = highlighted;

            // RE-ENABLE LAYOUT: Now that the user has stopped typing, we can safely recalculate.
            if (contentFitter != null) contentFitter.enabled = true;

            // Adjust the height (this will now trigger the calculation, but only once)
            UpdateInputFieldHeight(text);

            // SWAP VISIBILITY back to the colored text
            if (ColorUtility.TryParseHtmlString(ColorText, out Color myColor))
            {
                syntaxHighlighterText.color = myColor;
            }
            else
            {
                syntaxHighlighterText.color = Color.white;
            }

            if (rawTextOutput != null) rawTextOutput.textComponent.color = Color.clear;
        }

        backgroundHighlightCoroutine = null;
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
    // Other Stuff
    // =========================================================================
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
    private void PasteTextMod()
    {
        //if (isUpdatingText) return;

        // Directly capture to the persistent variable
        tempClipboardBuffer = GUIUtility.systemCopyBuffer;

        if (tempClipboardBuffer != null && tempClipboardBuffer.Length > 0)
        {
            UnityEngine.Debug.Log($"[PasteTest] Paste successful! Length: {tempClipboardBuffer.Length}");

            // Apply it directly to the UI exactly like your working button
            //rawTextOutput.text = tempClipboardBuffer;
            //OnRawTextChanged(tempClipboardBuffer);
        }
        else
        {
            UnityEngine.Debug.LogWarning("[PasteTest] Paste failed: systemCopyBuffer was null or empty.");
        }
    }

    // =========================================================================
    // Core Engine Logic
    // =========================================================================

    private string RestoreImages(string text)
    {
        // Fast exit check: updated to look for both compressed signatures
        if (string.IsNullOrEmpty(text) || (!text.Contains("img.IMG_") && !text.Contains("[IMG_")))
            return text;

        // Matches either img.IMG_123. or [IMG_123]
        return Regex.Replace(text, @"img\.(IMG_\d+)\.|\[(IMG_\d+)\]", match => {

            bool isBracket = match.Value.StartsWith("[");
            string id = isBracket ? match.Groups[2].Value : match.Groups[1].Value;

            if (imgCache.TryGetValue(id, out string data))
            {
                // Restore with the appropriate formatting
                return isBracket ? $"[{data}]" : $"img.{data}.";
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
    private void CopyAndRestoreCode()
    {
        if (rawTextOutput == null) return;

        string regular = MinifyModString(rawTextOutput.text);
        string currentText = regular;
        string restoredText = RestoreImages(currentText);
        string formattedText = InsertLinebreaksAfterCommas(restoredText);

        #if UNITY_WEBGL && !UNITY_EDITOR
                ClipboardHelper.CopyToClipboard(formattedText);
        #else
                GUIUtility.systemCopyBuffer = formattedText;
        #endif

        UnityEngine.Debug.Log($"Copied to clipboard. Final Length: {formattedText.Length}");

        // Spawn visual confirmation
        uiGenerator.CreatePopup("Copied output to clipboard!", true, null);
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
    private string InsertLinebreaksAfterCommas(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Replaces each comma with a comma followed by a newline character
        return input.Replace(",", ",\n");
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

            if (m.Groups["floor"].Success)
            {
                AppendWithColor(sb, input, idx, len, ColorFloor);
            }
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
                else
                {
                    AppendWithColor(sb, input, idx, len, ColorPhasePrefix);
                }
            }
            else if (m.Groups["delimiter"].Success)
            {
                AppendWithColor(sb, input, idx, len, ColorDelimiter);
            }
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
            else if (m.Groups["number"].Success)
            {
                AppendWithColor(sb, input, idx, len, ColorNumber);
            }
            else if (m.Groups["text"].Success)
            {
                AppendWithColor(sb, input, idx, len, ColorText);
            }
            else
            {
                AppendEscaped(sb, input, idx, len);
            }

            lastIndex = idx + len;
        }

        if (lastIndex < input.Length)
        {
            AppendEscaped(sb, input, lastIndex, input.Length - lastIndex);
        }

        buildSw.Stop();
        log?.AppendLine($"    [Sub-Profiler] FormatSyntaxHighlighting -> Regex: {regexSw.ElapsedMilliseconds} ms ({matchCount} matches) | Reconstruction: {buildSw.ElapsedMilliseconds} ms");

        return sb.ToString();
    }

    /*
    private IEnumerator MinifyModStringAsync(string raw, Action<string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            onComplete(string.Empty);
            yield break;
        }

        int len = raw.Length;
        var sb = new StringBuilder(len);
        bool lineStart = true;
        int operations = 0;

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

            operations++;
            if (operations > SettingsManager.CHARS_PER_FRAME)
            {
                operations = 0;
                yield return null;
            }
        }

        while (sb.Length > 0 && char.IsWhiteSpace(sb[sb.Length - 1])) sb.Length--;
        onComplete(sb.ToString());
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
    */
}