using SliceDiceTextMod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class PhasesFactoryOld : RootUI
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);
#endif

    private TMP_Text hierarchy;

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
    private Coroutine massUpdateCoroutine;

    // References for double-input overlay syntax highlighting
    private TMP_InputField rawTextOutput;
    private TextMeshProUGUI syntaxHighlighterText;

    // =========================================================================
    // Image Compression Cache
    // =========================================================================
    private Dictionary<string, string> imgCache = new Dictionary<string, string>();
    private int imgCounter = 0;

    // --- IDE Chunking Architecture ---
    private class CodeChunkUI
    {
        public GameObject Root;
        public TMP_InputField Input;
        public TextMeshProUGUI Highlighter;
        public LayoutElement Layout;
        public Coroutine DebounceRoutine;
    }

    private List<CodeChunkUI> codeChunks = new List<CodeChunkUI>();
    private CodeChunkUI activeChunk;
    private ScrollRect leftScrollView;
    private float typingDebounceDelay = 0.5f;

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
    }
    */
    private void BuildHierarchy(string rawTextOutput)
    {
        hierarchy.text = ModAnalyzer.BuildHierarchy(rawTextOutput);
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
                GridCellSpec.CreateButton("Button3", "Copy & Restore Code", 0.33f, () => CopyAndRestoreCode())
            ),
            // The ScrollView will now hold all of our chunked inputs!
            new GridRowSpec(leftScrollHeight, GridCellSpec.CreateScrollView("LeftInputScrollView", 1.0f))
        };
        columns.Add(new ColumnSpec("Left_Column", 0.0f, 0.5f, leftRows));

        List<GridRowSpec> rightTopRows = new List<GridRowSpec>
        {
            new GridRowSpec(rightTopSpacerHeight, GridCellSpec.CreateImagePanel("RightTopSpacer", 1.0f)),
            new GridRowSpec(rightPanelHeight, GridCellSpec.CreateScrollView("RightTopScrollView", 1.0f))
        };
        columns.Add(new ColumnSpec("RightTop_Column", 0.5f, 1.0f, rightTopRows));

        List<GridRowSpec> rightBottomRows = new List<GridRowSpec>
        {
            new GridRowSpec(rightBottomSpacerHeight, GridCellSpec.CreateImagePanel("RightBottomSpacer", 1.0f)),
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
                        if (!string.IsNullOrEmpty(clipboardText)) StartCoroutine(RebuildAllChunks(clipboardText));
                    });
                });
            }

            if (leftRefs.ScrollViews.TryGetValue("LeftInputScrollView", out ScrollRect scrollView))
            {
                leftScrollView = scrollView;
                scrollView.horizontal = false;
                scrollView.vertical = true;

                var contentLayout = scrollView.content.GetComponent<VerticalLayoutGroup>();
                if (contentLayout == null) contentLayout = scrollView.content.gameObject.AddComponent<VerticalLayoutGroup>();
                contentLayout.childControlHeight = true;
                contentLayout.childControlWidth = true;
                contentLayout.childForceExpandHeight = false; // Prevents massive stretching
                contentLayout.childForceExpandWidth = true;
                contentLayout.spacing = 10f; // Gap between code blocks
                contentLayout.padding = new RectOffset(5, 5, 5, 5);

                var contentFitter = scrollView.content.GetComponent<ContentSizeFitter>();
                if (contentFitter == null) contentFitter = scrollView.content.gameObject.AddComponent<ContentSizeFitter>();
                contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                // Spawn an initial empty chunk to type in
                if (codeChunks.Count == 0) codeChunks.Add(CreateChunk());
            }
        }

        UpdateToggleButtonText();

        ClearSpacerText("Left_Column", "LeftSpacer");
        ClearSpacerText("RightTop_Column", "RightTopSpacer");
        ClearSpacerText("RightBottom_Column", "RightBottomSpacer");

        PopulateScrollViewWithLabel("RightTop_Column", "RightTopScrollView", "Right Top Panel Output Log");
        PopulateDemoScrollView("RightBottom_Column", "RightBottomScrollView", "Right Bottom Button");
    }

    private CodeChunkUI CreateChunk()
    {
        GameObject inputObj = Instantiate(uiGenerator.inputFieldPrefab, leftScrollView.content);
        TMP_InputField input = inputObj.GetComponent<TMP_InputField>();

        LayoutElement layout = inputObj.GetComponent<LayoutElement>();
        if (layout == null) layout = inputObj.AddComponent<LayoutElement>();
        layout.minHeight = 60f;

        var fitter = inputObj.GetComponent<ContentSizeFitter>();
        if (fitter != null) Destroy(fitter);

        // --- FIX: Add the Scroll Pass Through to the Chunk ---
        var scrollPassThrough = inputObj.GetComponent<ScrollPassThrough>();
        if (scrollPassThrough == null) scrollPassThrough = inputObj.AddComponent<ScrollPassThrough>();
        scrollPassThrough.TargetScrollRect = leftScrollView;
        // -----------------------------------------------------

        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.textComponent.alignment = TextAlignmentOptions.TopLeft;
        input.textComponent.color = Color.clear;
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.richText = false;
        input.textComponent.enableAutoSizing = false;
        input.pointSize = 12;

        if (input.textViewport != null)
        {
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(8, 8);
            input.textViewport.offsetMax = new Vector2(-8, -8);
        }

        GameObject highlighterObj = Instantiate(uiGenerator.labelPrefab, input.textComponent.transform.parent);
        highlighterObj.name = "SyntaxHighlighter";
        TextMeshProUGUI highlighter = highlighterObj.GetComponentInChildren<TextMeshProUGUI>();

        var cg = highlighterObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = highlighterObj.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
        RectTransform textCompRt = input.textComponent.GetComponent<RectTransform>();
        highlightRt.anchorMin = textCompRt.anchorMin;
        highlightRt.anchorMax = textCompRt.anchorMax;
        highlightRt.offsetMin = textCompRt.offsetMin;
        highlightRt.offsetMax = textCompRt.offsetMax;
        highlightRt.pivot = textCompRt.pivot;

        highlighter.enableAutoSizing = false;
        highlighter.fontSize = 12;
        highlighter.alignment = input.textComponent.alignment;
        highlighter.margin = input.textComponent.margin;
        highlighter.enableWordWrapping = input.textComponent.enableWordWrapping;
        highlighter.richText = true;

        CodeChunkUI chunk = new CodeChunkUI { Root = inputObj, Input = input, Highlighter = highlighter, Layout = layout };

        input.onValueChanged.AddListener((val) => OnChunkInputChanged(chunk, val));
        input.onSelect.AddListener((str) => activeChunk = chunk);

        return chunk;
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
        if (activeChunk == null && codeChunks.Count > 0) activeChunk = codeChunks[0];
        if (activeChunk == null) return;

        activeChunk.Input.readOnly = true;

        int selectStart = activeChunk.Input.selectionStringFocusPosition;
        int selectEnd = activeChunk.Input.selectionStringAnchorPosition;
        int start = Mathf.Min(selectStart, selectEnd);
        int end = Mathf.Max(selectStart, selectEnd);

        string currentText = activeChunk.Input.text ?? "";
        string newText;

        if (start != end && start >= 0 && end <= currentText.Length)
        {
            newText = currentText.Remove(start, end - start).Insert(start, clipboard);
        }
        else
        {
            int caret = Mathf.Clamp(activeChunk.Input.caretPosition, 0, currentText.Length);
            newText = currentText.Insert(caret, clipboard);
        }

        // If the paste is massive, stitch the whole document and run the heavy async rebuilder
        if (newText.Length > 15000 || newText.Contains(","))
        {
            StringBuilder fullCode = new StringBuilder();
            for (int i = 0; i < codeChunks.Count; i++)
            {
                if (codeChunks[i] == activeChunk) fullCode.Append(newText);
                else fullCode.Append(codeChunks[i].Input.text);
            }
            StartCoroutine(RebuildAllChunks(fullCode.ToString()));
        }
        else
        {
            // It's a small paste, handle it on just this chunk instantly
            activeChunk.Input.text = newText;
            if (activeChunk.DebounceRoutine != null) StopCoroutine(activeChunk.DebounceRoutine);
            activeChunk.DebounceRoutine = StartCoroutine(HandleChunkDebounce(activeChunk, newText));
            activeChunk.Input.readOnly = false;
        }
    }

    // --- ASYNC PROCESSORS ---
    private IEnumerator RebuildAllChunks(string fullText)
    {
        isUpdatingText = true;
        UIPopup popup = uiGenerator.CreatePopup("Processing code blocks, please wait...", false);
        yield return null; yield return null;

        foreach (var c in codeChunks) Destroy(c.Root);
        codeChunks.Clear();

        // --- FIX: Split but retain the comma at the end of the chunk! ---
        string[] blocks = Regex.Split(fullText, "(?<=,)");

        for (int i = 0; i < blocks.Length; i++)
        {
            if (string.IsNullOrEmpty(blocks[i])) continue;

            var chunk = CreateChunk();
            codeChunks.Add(chunk);

            string compressed = CompressImagesSync(blocks[i]);
            string formatted = IDEFormatString ? AutoFormatModStringSync(compressed) : MinifyModString(compressed);

            chunk.Input.SetTextWithoutNotify(formatted);
            chunk.Highlighter.text = FormatSyntaxHighlightingSync(formatted);
            UpdateChunkHeight(chunk, formatted);

            if (i % 5 == 0) yield return null;
        }

        if (codeChunks.Count == 0) codeChunks.Add(CreateChunk());

        foreach (var chunk in codeChunks)
        {
            if (ColorUtility.TryParseHtmlString(ColorText, out Color myColor)) chunk.Highlighter.color = myColor;
            else chunk.Highlighter.color = Color.white;
            chunk.Input.textComponent.color = Color.clear;
        }

        popup.Dismiss();
        BuildHierarchy(fullText);
        isUpdatingText = false;
    }
    private void OnChunkInputChanged(CodeChunkUI chunk, string value)
    {
        if (isUpdatingText) return;

        // INSTANT FEEDBACK
        chunk.Input.textComponent.color = Color.white;
        chunk.Highlighter.color = Color.clear;

        if (chunk.DebounceRoutine != null) StopCoroutine(chunk.DebounceRoutine);
        chunk.DebounceRoutine = StartCoroutine(HandleChunkDebounce(chunk, value));
    }
    private IEnumerator HandleChunkDebounce(CodeChunkUI chunk, string value)
    {
        yield return new WaitForSeconds(typingDebounceDelay);

        // Process chunk synchronously (It's < 20,000 chars, it will be instant!)
        string compressed = CompressImagesSync(value);
        string formatted = IDEFormatString ? AutoFormatModStringSync(compressed) : MinifyModString(compressed);

        if (chunk.Input.text != formatted)
        {
            int caret = chunk.Input.caretPosition;
            chunk.Input.SetTextWithoutNotify(formatted);
            chunk.Input.caretPosition = Mathf.Min(caret, formatted.Length);
        }

        chunk.Highlighter.text = FormatSyntaxHighlightingSync(formatted);
        UpdateChunkHeight(chunk, formatted);

        // Restore Visibility
        ColorUtility.TryParseHtmlString(ColorText, out Color myColor);
        chunk.Highlighter.color = myColor;
        chunk.Input.textComponent.color = Color.clear;
    }
    private void UpdateChunkHeight(CodeChunkUI chunk, string formattedText)
    {
        float textHeight = chunk.Input.textComponent.GetPreferredValues(formattedText).y;
        float newHeight = Mathf.Max(60f, textHeight + 20f); // 20f padding

        if (Mathf.Abs(chunk.Layout.preferredHeight - newHeight) > 1f)
        {
            chunk.Layout.preferredHeight = newHeight;
            // Force scroll view to accept the new height organically
            LayoutRebuilder.ForceRebuildLayoutImmediate(leftScrollView.content);
        }
    }
    private void CopyAndRestoreCode()
    {
        StringBuilder fullCode = new StringBuilder();
        for (int i = 0; i < codeChunks.Count; i++)
        {
            fullCode.Append(MinifyModString(codeChunks[i].Input.text));
            // Removed the manual append(",") since the chunk already contains it!
        }

        string restoredText = RestoreImages(fullCode.ToString());
        string formattedText = InsertLinebreaksAfterCommas(restoredText);

#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardHelper.CopyToClipboard(formattedText);
#else
        GUIUtility.systemCopyBuffer = formattedText;
#endif
        uiGenerator.CreatePopup("Copied output to clipboard!", true, null);
    }
    private void ToggleIDE()
    {
        IDEFormatString = !IDEFormatString;
        UpdateToggleButtonText();

        // Cancel any ongoing massive updates to prevent overlap
        if (massUpdateCoroutine != null) StopCoroutine(massUpdateCoroutine);

        // Cancel individual typings
        foreach (var chunk in codeChunks)
        {
            if (chunk.DebounceRoutine != null) StopCoroutine(chunk.DebounceRoutine);
        }

        // Start the Async Mass Update
        massUpdateCoroutine = StartCoroutine(ReformatAllChunksAsync());
    }
    private IEnumerator ReformatAllChunksAsync()
    {
        isUpdatingText = true;
        UIPopup popup = uiGenerator.CreatePopup("Reformatting code blocks, please wait...", false);
        yield return null; yield return null;

        for (int i = 0; i < codeChunks.Count; i++)
        {
            var chunk = codeChunks[i];

            string compressed = CompressImagesSync(chunk.Input.text);
            string formatted = IDEFormatString ? AutoFormatModStringSync(compressed) : MinifyModString(compressed);

            if (chunk.Input.text != formatted)
            {
                chunk.Input.SetTextWithoutNotify(formatted);
            }

            chunk.Highlighter.text = FormatSyntaxHighlightingSync(formatted);
            UpdateChunkHeight(chunk, formatted);

            if (ColorUtility.TryParseHtmlString(ColorText, out Color myColor)) chunk.Highlighter.color = myColor;
            else chunk.Highlighter.color = Color.white;
            chunk.Input.textComponent.color = Color.clear;

            // Yield every 5 blocks to prevent freezing the UI
            if (i % 5 == 0) yield return null;
        }

        if (popup != null) popup.Dismiss();
        isUpdatingText = false;
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        bool isMac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
        bool modifier = isMac ? Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand) : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (modifier && Input.GetKeyDown(KeyCode.V) && !isUpdatingText)
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            if (!string.IsNullOrEmpty(clipboard) && (clipboard.Length > 15000 || clipboard.Contains(",")))
            {
                StartCoroutine(RebuildAllChunks(clipboard));
            }
        }
#endif
    }

    // Synchronous tools (No yield limits needed because chunks are so small)
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
            if (innerData.StartsWith("IMG_") || innerData.Length <= 25) sb.Append(m.Value);
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

    private string AutoFormatModStringSync(string raw)
    {
        string minified = MinifyModString(raw);
        StringBuilder sb = new StringBuilder(minified.Length + 500);
        int indentLevel = 0;
        for (int i = 0; i < minified.Length; i++)
        {
            char c = minified[i];
            if (c == ',') { indentLevel = 0; sb.Append(c); }
            else if (c == '&') { AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); }
            else if (c == '@' && i + 1 < minified.Length && char.IsDigit(minified[i + 1])) { AppendNewlineAndIndent(sb, (indentLevel + 1) * 4); sb.Append(c); sb.Append(minified[++i]); }
            else if (c == ';') { AppendNewlineAndIndent(sb, (indentLevel + 1) * 4); sb.Append(c); }
            else if ((c == '{' && i + 1 < minified.Length && minified[i + 1] == '}') || (c == '(' && i + 1 < minified.Length && minified[i + 1] == ')')) { sb.Append(c); sb.Append(minified[++i]); }
            else if (c == '{' || c == '(') { AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); indentLevel++; AppendNewlineAndIndent(sb, indentLevel * 4); }
            else if (c == '}' || c == ')') { indentLevel = Math.Max(0, indentLevel - 1); AppendNewlineAndIndent(sb, indentLevel * 4); sb.Append(c); }
            else sb.Append(c);
        }
        return sb.ToString().TrimStart();
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
    /*
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
    */
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
                scrollView.scrollSensitivity = 32;

                if (Mathf.Abs(inputLayoutElement.preferredHeight - newHeight) > 1f)
                {
                    inputLayoutElement.preferredHeight = newHeight;
                }
            }
        }

        log?.AppendLine($"    [Sub-Profiler] UpdateInputFieldHeight -> GetPreferredValues: {prefValSw.ElapsedMilliseconds} ms | Rebuild: Deferred Native Canvas update");
    }
    /*
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
    */
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

    // =========================================================================
    // CHANGED METHOD: PopulateScrollViewWithLabel
    // =========================================================================
    private void PopulateScrollViewWithLabel(string columnName, string scrollViewKey, string labelText)
    {
        if (generatedScreen.ColumnRefs.TryGetValue(columnName, out GridReferences refs))
        {
            if (refs.ScrollViews.TryGetValue(scrollViewKey, out ScrollRect scrollRect))
            {
                if (scrollRect.content != null && uiGenerator.labelPrefab != null)
                {
                    // 1. Correct scroll behavior settings
                    scrollRect.horizontal = false;
                    scrollRect.vertical = true;

                    // 2. Configure parent content layout engine
                    var contentLayout = scrollRect.content.GetComponent<VerticalLayoutGroup>();
                    if (contentLayout == null)
                    {
                        contentLayout = scrollRect.content.gameObject.AddComponent<VerticalLayoutGroup>();
                    }
                    contentLayout.padding = new RectOffset(10, 10, 10, 10);
                    contentLayout.spacing = 4f;
                    contentLayout.childControlHeight = true;
                    contentLayout.childControlWidth = true;
                    contentLayout.childForceExpandHeight = false;
                    contentLayout.childForceExpandWidth = true;

                    // 3. Ensure content adjusts size dynamically based on child preferred values
                    var sizeFitter = scrollRect.content.GetComponent<ContentSizeFitter>();
                    if (sizeFitter == null)
                    {
                        sizeFitter = scrollRect.content.gameObject.AddComponent<ContentSizeFitter>();
                    }
                    sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                    // 4. Reset child hierarchy
                    foreach (Transform child in scrollRect.content)
                    {
                        Destroy(child.gameObject);
                    }

                    // 5. Setup the label
                    GameObject labelObj = Instantiate(uiGenerator.labelPrefab, scrollRect.content);
                    labelObj.name = "HierarchyOutputLabel";

                    var layoutElement = labelObj.GetComponent<LayoutElement>();
                    if (layoutElement == null)
                    {
                        layoutElement = labelObj.AddComponent<LayoutElement>();
                    }

                    var textMesh = labelObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (textMesh != null)
                    {
                        textMesh.text = labelText;
                        textMesh.alignment = TextAlignmentOptions.TopLeft;
                        textMesh.color = Color.white;
                        textMesh.enableWordWrapping = true;
                        textMesh.richText = true;
                        textMesh.fontSize = 13f;
                        textMesh.enableAutoSizing = false;
                        textMesh.autoSizeTextContainer = false;
                        textMesh.raycastTarget = false;

                        // Force rect transform stretch settings
                        RectTransform textRt = textMesh.GetComponent<RectTransform>();
                        if (textRt != null)
                        {
                            textRt.anchorMin = Vector2.zero;
                            textRt.anchorMax = Vector2.one;
                            textRt.offsetMin = Vector2.zero;
                            textRt.offsetMax = Vector2.zero;
                        }

                        hierarchy = textMesh;
                    }
                }
            }
        }
    }

    public string FormatSyntaxHighlightingSync(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;

        // Lazy match execution
        Match m = null;
        try { m = SyntaxRegex.Match(input); } catch { return input; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AppendEscaped(sb, input, lastIndex, idx - lastIndex);

            if (m.Groups["floor"].Success) AppendWithColor(sb, input, idx, len, ColorFloor);
            else if (m.Groups["phase"].Success)
            {
                if (input[idx] == 'p' && len > 3)
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
                sb.Append("</color><color=").Append(ColorSdRed).Append(">");
                AppendEscaped(sb, input, idx + 3, len - 3);
                sb.Append("</color>");
            }
            else if (m.Groups["ritemx"].Success) AppendWithColor(sb, input, idx, len, ColorItem);
            else if (m.Groups["hsv_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscaped(sb, input, idx, 4); // "hsv."
                sb.Append("</color><color=").Append(ColorNumber).Append(">");
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
            m = m.NextMatch();
        }

        if (lastIndex < input.Length) AppendEscaped(sb, input, lastIndex, input.Length - lastIndex);

        return sb.ToString();
    }
}