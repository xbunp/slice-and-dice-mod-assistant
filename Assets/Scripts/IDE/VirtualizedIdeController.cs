using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualizedIdeController : MonoBehaviour
{
    private bool _isSwitchingLine = false;

    private bool _isMultiSelecting = false;
    private int _selStartLine = -1;
    private int _selStartChar = -1;
    private int _selEndLine = -1;
    private int _selEndChar = -1;

    [Header("UI Scroll Settings")]
    [SerializeField] private ScrollRect viewportScrollRect;
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private int extraBufferRows = 5;

    [Header("Prefabs & Resources")]
    [SerializeField] private GameObject lineRowPrefab; // A prefab with IdeLineRow components
    [SerializeField] private TMP_InputField sharedInputField; // The single overlay input field

    [Header("Syntax Coloring")]
    [SerializeField] private Color defaultTextColor = new Color(0.83f, 0.83f, 0.83f);

    // =========================================================================
    // Core Document Model (Data)
    // =========================================================================
    private List<string> _rawLines = new List<string> { "" }; // The actual text document
    private Dictionary<int, string> _highlightedCache = new Dictionary<int, string>();

    // =========================================================================
    // Virtualization Pools
    // =========================================================================
    private List<IdeLineRow> _visibleRowsPool = new List<IdeLineRow>();
    private int _startLineIndex = -1;
    private int _endLineIndex = -1;
    private int _activeEditLineIndex = -1;
    private bool _isUpdatingText = false;

    // Precomputed layout parameters
    private float _lockedLeftOffset;
    private float _lockedViewportWidth;

    // =========================================================================
    // Precompiled Syntax Highlighting Regex
    // =========================================================================
    private static readonly Regex SyntaxRegex = new Regex(
        @"(?<=^|[(&@!~\[])(?<floor>e?\d+(?:\.\d+)?\.|\d+-\d+\.|\-?\d+\.)" +
        @"|(?<phase>ph\.[!0-9bcedglrstz]|\.phi\b)" +
        @"|(?<delimiter>&|@\d+|;|\b(?i:Hidden|skip(?: all)?|temporary|Delevel|Level Up|No Flee)\b)" +
        @"|(?<sq_bracket>\[[^\]]*\])" +
        @"|(?<bracket>[{}()])" +
        @"|(?<itempool_block>itempool\.[^\.]*)" +
        @"|(?<sd_block>sd\.[^.)]*)" +
        @"|(?<ritemx>(?i)(?:i\.)?ritemx\.[^.)]*)" +
        @"|(?<hsv_block>(?i)hsv\.[^\.]*)" +
        @"|(?<k_block>k\.[^.#]*)" +
        @"|(?<tog>\btog[a-zA-Z0-9_]*\b)" +
        @"|(?<method>\b(?:img|col|n|tier|facade|sidesc|heropool|learn|hp|bal|mn|hat|abilitydata|replica|h|ch|hsv|part|difficulty|diff|splice|jinx|allitem|self|p|topbot|brittle|left|right|row|all|right5|right3|right2|mid2|left2|rightmost|bot|top|mid)\.|\.(?:modtier|add|doc|all|egg|hsv|speech|unpack)\b)" +
        @"|(?<reward>\(?[miglrqovs]\.[a-zA-Z0-9_\-~\^\/ ]*\)?|(?<=[\!&@+=])\(?[miglrqovs]\b\)?|\(?[miglrqovs][a-zA-Z0-9_\-~\^\/]*[\~^\/][a-zA-Z0-9_\-~\^\/]*\)?)" +
        @"|(?<number>\b\d+\b)" +
        @"|(?<text>[a-zA-Z_][a-zA-Z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    private const string ColorFloor = "#569CD6";
    private const string ColorPhasePrefix = "#569CD6";
    private const string ColorPhaseCode = "#4EC9B0";
    private const string ColorDelimiter = "#C586C0";
    private const string ColorNumber = "#B5CEA8";
    private const string ColorText = "#D4D4D4";
    private const string ColorBracket = "#569cd6";
    private const string ColorMethod = "#dcdcaa";
    private const string ColorSdRed = "#FFA961";
    private const string ColorMossGreen = "#A4C365";
    private const string ColorNeonGreen = "#39FF14";
    private const string ColorMod = "#CE9178";
    private const string ColorItem = "#9CDCFE";
    private const string ColorLvl = "#B5CEA8";
    private const string ColorHero = "#DCDCAA";
    private const string ColorRand = "#D8A0DF";
    private const string ColorValue = "#4FC1FF";
    private const string ColorSkip = "#FF7575";
    private const string ColorDefaultReward = "#FFD700";

    // =========================================================================
    // Proactive Initialization Sanity Pass
    // =========================================================================

    private void Start()
    {
        PerformStrictInitializationSanityPass();

        sharedInputField.onValueChanged.AddListener(OnActiveLineTextChanged);
        sharedInputField.onEndEdit.AddListener(OnActiveLineEndEdit);
        viewportScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);

        InitializeVirtualPool(30);
        RefreshDocumentLayout();
    }
    private void Update()
    {
        // Only process manipulation if the user is actively typing in the field
        if (_isSwitchingLine || _activeEditLineIndex == -1 || !sharedInputField.isFocused) return;

        // 1. REMOVE GAP ABOVE: Backspace at the very beginning of a line
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (sharedInputField.caretPosition == 0 && sharedInputField.selectionFocusPosition == sharedInputField.selectionAnchorPosition)
            {
                if (_activeEditLineIndex > 0)
                {
                    MergeWithPreviousLine();
                    return;
                }
            }
        }

        // 2. REMOVE GAP BELOW: Delete at the very end of a line
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if (sharedInputField.caretPosition == sharedInputField.text.Length && sharedInputField.selectionFocusPosition == sharedInputField.selectionAnchorPosition)
            {
                if (_activeEditLineIndex < _rawLines.Count - 1)
                {
                    MergeWithNextLine();
                    return;
                }
            }
        }

        // 3. NAVIGATE UP: Up arrow key
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (_activeEditLineIndex > 0)
            {
                int caret = sharedInputField.caretPosition;
                RequestLineEdit(_activeEditLineIndex - 1, caret);
                return;
            }
        }

        // 4. NAVIGATE DOWN: Down arrow key
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (_activeEditLineIndex < _rawLines.Count - 1)
            {
                int caret = sharedInputField.caretPosition;
                RequestLineEdit(_activeEditLineIndex + 1, caret);
                return;
            }
        }
    }

    private void PerformStrictInitializationSanityPass()
    {
        // 1. Lock down ScrollRect physics
        viewportScrollRect.horizontal = false;
        viewportScrollRect.vertical = true;

        // 2. Lock down the Viewport bounds
        RectTransform viewportRt = viewportScrollRect.viewport;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        // 3. Lock down Content container anchors (Top-Stretch)
        contentContainer.anchorMin = new Vector2(0, 1);
        contentContainer.anchorMax = new Vector2(1, 1);
        contentContainer.pivot = new Vector2(0, 1);
        contentContainer.offsetMin = new Vector2(0, contentContainer.offsetMin.y);
        contentContainer.offsetMax = new Vector2(0, contentContainer.offsetMax.y);

        // 4. Calculate absolute left boundary, accounting for both RectTransform offset AND TMP margins
        IdeLineRow rowComponent = lineRowPrefab.GetComponent<IdeLineRow>();
        RectTransform rowCodeTextRt = rowComponent.CodeTextComponent.rectTransform;
        TMP_Text sourceText = rowComponent.CodeTextComponent;

        _lockedLeftOffset = rowCodeTextRt.offsetMin.x + sourceText.margin.x;

        // 5. Lock down the Input Field Container (Aligned strictly to the code text area)
        sharedInputField.gameObject.SetActive(false);
        sharedInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        inputRt.SetParent(contentContainer, false);
        inputRt.anchorMin = new Vector2(0, 1);
        inputRt.anchorMax = new Vector2(1, 1);
        inputRt.pivot = new Vector2(0, 1);
        inputRt.localScale = Vector3.one;
        inputRt.offsetMin = new Vector2(_lockedLeftOffset, 0);
        inputRt.offsetMax = Vector2.zero;

        // 6. Lock down internal Viewport structure
        RectTransform textAreaRt = sharedInputField.textViewport;
        if (textAreaRt != null)
        {
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = Vector2.zero;
            textAreaRt.offsetMax = Vector2.zero;
            textAreaRt.localScale = Vector3.one;
        }

        // 7. Lock down the Text Renderer inside the input box (Stop horizontal text scrolling)
        TMP_Text inputTmp = sharedInputField.textComponent;
        inputTmp.rectTransform.anchorMin = Vector2.zero;
        inputTmp.rectTransform.anchorMax = Vector2.one;
        inputTmp.rectTransform.offsetMin = Vector2.zero;
        inputTmp.rectTransform.offsetMax = Vector2.zero;

        // Clone the styling from the prefab once during startup
        inputTmp.font = sourceText.font;
        inputTmp.fontSize = sourceText.fontSize;
        inputTmp.fontStyle = sourceText.fontStyle;
        inputTmp.alignment = sourceText.alignment;
        inputTmp.lineSpacing = sourceText.lineSpacing;
        inputTmp.characterSpacing = sourceText.characterSpacing;
        inputTmp.wordSpacing = sourceText.wordSpacing;
        inputTmp.enableWordWrapping = false;
        inputTmp.margin = new Vector4(0, sourceText.margin.y, sourceText.margin.z, sourceText.margin.w); // Left margin is physically handled by leftOffset
        inputTmp.extraPadding = false;
        inputTmp.enableAutoSizing = false;

        // Force layout calculations to get exact, non-collapsed viewport dimensions
        Canvas.ForceUpdateCanvases();
        _lockedViewportWidth = viewportRt.rect.width;
    }
    private void InitializeVirtualPool(int initialSize)
    {
        for (int i = 0; i < initialSize; i++)
        {
            CreateAndAddNewRowToPool();
        }
    }
    private IdeLineRow CreateAndAddNewRowToPool()
    {
        GameObject rowObj = Instantiate(lineRowPrefab, contentContainer);
        rowObj.transform.localScale = Vector3.one;

        IdeLineRow row = rowObj.GetComponent<IdeLineRow>();
        row.Initialize(_visibleRowsPool.Count, this);
        _visibleRowsPool.Add(row);
        rowObj.SetActive(false);
        return row;
    }

    // =========================================================================
    // Virtualization Rendering Core
    // =========================================================================

    private void OnScrollPositionChanged(Vector2 scrollPosition)
    {
        UpdateVirtualViewport(false);
    }
    private void UpdateVirtualViewport(bool forceRepaint)
    {
        if (_isUpdatingText) return;

        float contentY = contentContainer.anchoredPosition.y;
        float viewportHeight = viewportScrollRect.viewport.rect.height;

        // Calculate first and last visible line indices
        int startLine = Mathf.Max(0, Mathf.FloorToInt(contentY / lineHeight) - 2);
        int visibleCount = Mathf.CeilToInt(viewportHeight / lineHeight) + extraBufferRows;
        int endLine = Mathf.Min(_rawLines.Count - 1, startLine + visibleCount);

        if (startLine == _startLineIndex && endLine == _endLineIndex && !forceRepaint)
        {
            return;
        }

        _startLineIndex = startLine;
        _endLineIndex = endLine;

        // AUTO-GROWTH CHECK: If the current screen needs more rows than exist in the pool, spawn them instantly
        int requiredRowsCount = (endLine - startLine) + 1;
        while (_visibleRowsPool.Count < requiredRowsCount)
        {
            CreateAndAddNewRowToPool();
        }

        // Map visible lines to our expanded pool
        for (int i = 0; i < _visibleRowsPool.Count; i++)
        {
            int lineIndex = _startLineIndex + i;
            IdeLineRow row = _visibleRowsPool[i];

            if (lineIndex <= _endLineIndex && lineIndex < _rawLines.Count)
            {
                row.gameObject.SetActive(true);
                row.SetRowPosition(lineIndex, lineHeight);

                bool isEditingThisRow = (lineIndex == _activeEditLineIndex);

                // =========================================================================
                // Proactive Virtual Highlight Calculation
                // =========================================================================
                int hlStart = -1;
                int hlEnd = -1;

                if (_isMultiSelecting)
                {
                    int minLine = Mathf.Min(_selStartLine, _selEndLine);
                    int maxLine = Mathf.Max(_selStartLine, _selEndLine);

                    if (lineIndex >= minLine && lineIndex <= maxLine)
                    {
                        if (_selStartLine == _selEndLine) // Single-line selection
                        {
                            hlStart = Mathf.Min(_selStartChar, _selEndChar);
                            hlEnd = Mathf.Max(_selStartChar, _selEndChar);
                        }
                        else if (lineIndex == minLine) // TOP line of the selection block
                        {
                            // If dragged down, top is start; if dragged up, top is end
                            hlStart = (minLine == _selStartLine) ? _selStartChar : _selEndChar;
                            hlEnd = _rawLines[lineIndex].Length;
                        }
                        else if (lineIndex == maxLine) // BOTTOM line of the selection block
                        {
                            // If dragged down, bottom is end; if dragged up, bottom is start
                            hlStart = 0;
                            hlEnd = (maxLine == _selEndLine) ? _selEndChar : _selStartChar;
                        }
                        else // FULL middle line
                        {
                            hlStart = 0;
                            hlEnd = _rawLines[lineIndex].Length;
                        }
                    }
                }

                row.UpdateRowDisplay(lineIndex, GetHighlightedText(lineIndex), isEditingThisRow, hlStart, hlEnd);
            }
            else
            {
                row.gameObject.SetActive(false);
            }
        }
    }
    public void RefreshDocumentLayout()
    {
        float totalHeight = _rawLines.Count * lineHeight;
        contentContainer.sizeDelta = new Vector2(contentContainer.sizeDelta.x, totalHeight);
        UpdateVirtualViewport(true);
    }

    // =========================================================================
    // Lean Runtime Editing
    // =========================================================================

    public void RequestLineEdit(int lineIndex, int startCaretPos = -1)
    {
        _isSwitchingLine = true;

        if (_activeEditLineIndex != -1 && _activeEditLineIndex != lineIndex)
        {
            CommitSharedInputToModel();
        }

        _activeEditLineIndex = lineIndex;
        EnsureLineIsVisible(lineIndex);
        UpdateVirtualViewport(true);

        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        inputRt.SetAsLastSibling();

        // Match the exact Y boundaries mathematically
        float topY = -lineIndex * lineHeight;
        float bottomY = topY - lineHeight;
        inputRt.offsetMin = new Vector2(_lockedLeftOffset, bottomY);
        inputRt.offsetMax = new Vector2(0, topY);

        sharedInputField.gameObject.SetActive(true);
        sharedInputField.SetTextWithoutNotify(_rawLines[lineIndex]);

        if (startCaretPos >= 0)
        {
            sharedInputField.caretPosition = Mathf.Min(startCaretPos, sharedInputField.text.Length);
        }
        else
        {
            sharedInputField.caretPosition = sharedInputField.text.Length;
        }

        sharedInputField.Select();
        sharedInputField.ActivateInputField();

        UpdateVirtualViewport(true);
        _isSwitchingLine = false;
    }
    private void OnActiveLineTextChanged(string newText)
    {
        if (_isSwitchingLine || _activeEditLineIndex == -1) return;

        if (newText.Contains("\n") || newText.Contains("\r"))
        {
            HandleLineSplit(newText);
            return;
        }

        _rawLines[_activeEditLineIndex] = newText;
        _highlightedCache.Remove(_activeEditLineIndex);

        // Deterministic bounds evaluation using the Viewport and the text preferred width
        float textWidth = sharedInputField.textComponent.preferredWidth;
        float absoluteMaxWidth = _lockedViewportWidth - _lockedLeftOffset - 20f;

        if (textWidth > absoluteMaxWidth && absoluteMaxWidth > 100f)
        {
            TriggerAutoLineSplit();
        }
    }
    private void OnActiveLineEndEdit(string text)
    {
        if (_isSwitchingLine) return;
        CommitSharedInputToModel();
    }
    private void CommitSharedInputToModel()
    {
        if (_activeEditLineIndex == -1) return;

        _rawLines[_activeEditLineIndex] = sharedInputField.text;
        _highlightedCache.Remove(_activeEditLineIndex);

        _activeEditLineIndex = -1;
        sharedInputField.gameObject.SetActive(false);
        UpdateVirtualViewport(true);
    }
    private int FindOverflowSplitIndex(string text, float maxWidth, TMP_Text textComponent)
    {
        int low = 1;
        int high = text.Length;
        int result = text.Length;

        // Binary search utilizing the font's preferred width generator
        while (low <= high)
        {
            int mid = (low + high) / 2;
            string substring = text.Substring(0, mid);
            float width = textComponent.GetPreferredValues(substring).x;

            if (width > maxWidth)
            {
                result = mid - 1;
                high = mid - 1; // Search left
            }
            else
            {
                low = mid + 1; // Search right
            }
        }
        return Mathf.Clamp(result, 1, text.Length - 1);
    }
    private void EnsureLineIsVisible(int lineIndex)
    {
        float viewportHeight = viewportScrollRect.viewport.rect.height;
        float lineTopY = lineIndex * lineHeight;
        float lineBottomY = lineTopY + lineHeight;
        float currentScrollY = contentContainer.anchoredPosition.y;

        if (lineBottomY > currentScrollY + viewportHeight)
        {
            float targetScrollY = lineBottomY - viewportHeight;
            contentContainer.anchoredPosition = new Vector2(contentContainer.anchoredPosition.x, targetScrollY);
        }
        else if (lineTopY < currentScrollY)
        {
            contentContainer.anchoredPosition = new Vector2(contentContainer.anchoredPosition.x, lineTopY);
        }
    }

    // =========================================================================
    // Core Engine Logic (Loading, Exporting, Syntax)
    // =========================================================================

    public void LoadEntireDocument(string rawText)
    {
        _isUpdatingText = true;
        _activeEditLineIndex = -1;
        sharedInputField.gameObject.SetActive(false);

        _rawLines.Clear();
        _highlightedCache.Clear();

        string cleanString = rawText.Replace("\r", "");
        string[] lines = cleanString.Split('\n');

        _rawLines.AddRange(lines);
        if (_rawLines.Count == 0) _rawLines.Add("");

        _isUpdatingText = false;
        RefreshDocumentLayout();
    }
    public string ExportDocument()
    {
        CommitSharedInputToModel();
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < _rawLines.Count; i++)
        {
            sb.Append(_rawLines[i]);
            if (i < _rawLines.Count - 1) sb.Append("\n");
        }
        return sb.ToString();
    }
    private string GetHighlightedText(int lineIndex)
    {
        if (_highlightedCache.TryGetValue(lineIndex, out string cache)) return cache;

        string rawText = _rawLines[lineIndex];
        string highlighted = ApplySyntaxColorsSync(rawText);
        _highlightedCache[lineIndex] = highlighted;
        return highlighted;
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

    private string _activeColor = null;
    private StringBuilder _colorAccumulator = new StringBuilder();

    private void FlushColorSegment(StringBuilder mainSb)
    {
        if (_colorAccumulator.Length == 0) return;

        if (string.IsNullOrEmpty(_activeColor) || _activeColor == ColorText)
        {
            mainSb.Append(_colorAccumulator.ToString());
        }
        else
        {
            mainSb.Append("<color=").Append(_activeColor).Append(">");
            mainSb.Append(_colorAccumulator.ToString());
            mainSb.Append("</color>");
        }
        _colorAccumulator.Clear();
    }
    private void AccumulateText(StringBuilder mainSb, string text, int start, int length, string colorHex)
    {
        if (colorHex != _activeColor)
        {
            FlushColorSegment(mainSb);
            _activeColor = colorHex;
        }
        AppendEscapedText(_colorAccumulator, text, start, length);
    }
    private void AppendEscapedText(StringBuilder sb, string text, int start, int length)
    {
        int end = start + length;
        for (int i = start; i < end; i++)
        {
            char c = text[i];
            if (c == '<') sb.Append("<noparse><</noparse>");
            else if (c == '>') sb.Append("<noparse>></noparse>");
            else sb.Append(c);
        }
    }
    public string ApplySyntaxColorsSync(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;

        _activeColor = null;
        _colorAccumulator.Clear();

        Match m;
        try { m = SyntaxRegex.Match(input); } catch { return input; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AccumulateText(sb, input, lastIndex, idx - lastIndex, null);

            if (m.Groups["floor"].Success) AccumulateText(sb, input, idx, len, ColorFloor);
            else if (m.Groups["phase"].Success)
            {
                if (input[idx] == 'p' && len > 3)
                {
                    AccumulateText(sb, input, idx, 3, ColorPhasePrefix);
                    AccumulateText(sb, input, idx + 3, len - 3, ColorPhaseCode);
                }
                else AccumulateText(sb, input, idx, len, ColorPhasePrefix);
            }
            else if (m.Groups["delimiter"].Success) AccumulateText(sb, input, idx, len, ColorDelimiter);
            else if (m.Groups["sq_bracket"].Success || m.Groups["bracket"].Success) AccumulateText(sb, input, idx, len, ColorBracket);
            else if (m.Groups["itempool_block"].Success)
            {
                AccumulateText(sb, input, idx, 9, ColorMethod);
                AccumulateText(sb, input, idx + 9, len - 9, ColorItem);
            }
            else if (m.Groups["sd_block"].Success)
            {
                AccumulateText(sb, input, idx, 3, ColorMethod);
                AccumulateText(sb, input, idx + 3, len - 3, ColorSdRed);
            }
            else if (m.Groups["ritemx"].Success) AccumulateText(sb, input, idx, len, ColorItem);
            else if (m.Groups["hsv_block"].Success)
            {
                AccumulateText(sb, input, idx, 4, ColorMethod);
                AccumulateText(sb, input, idx + 4, len - 4, ColorNumber);
            }
            else if (m.Groups["k_block"].Success) AccumulateText(sb, input, idx, len, ColorMossGreen);
            else if (m.Groups["tog"].Success) AccumulateText(sb, input, idx, len, ColorNeonGreen);
            else if (m.Groups["method"].Success) AccumulateText(sb, input, idx, len, ColorMethod);
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
                AccumulateText(sb, input, idx, len, rewardColor);
            }
            else if (m.Groups["number"].Success) AccumulateText(sb, input, idx, len, ColorNumber);
            else if (m.Groups["text"].Success) AccumulateText(sb, input, idx, len, ColorText);
            else AccumulateText(sb, input, idx, len, null);

            lastIndex = idx + len;
            m = m.NextMatch();
        }

        if (lastIndex < input.Length) AccumulateText(sb, input, lastIndex, input.Length - lastIndex, null);

        FlushColorSegment(sb);
        return sb.ToString();
    }

    // =========================================================================
    // Standard Text Editor Navigation & Manipulation
    // =========================================================================

    private void TriggerAutoLineSplit()
    {
        _isSwitchingLine = true;

        string currentText = sharedInputField.text;
        if (string.IsNullOrEmpty(currentText))
        {
            _isSwitchingLine = false;
            return;
        }

        TMP_Text textComp = sharedInputField.textComponent;
        textComp.ForceMeshUpdate();

        float absoluteMaxWidth = _lockedViewportWidth - _lockedLeftOffset - 20f;
        int splitIndex = FindOverflowSplitIndex(currentText, absoluteMaxWidth, textComp);

        int lastSpace = currentText.LastIndexOf(' ', splitIndex);
        if (lastSpace > 0 && lastSpace > (splitIndex - 20))
        {
            splitIndex = lastSpace;
        }

        string left = currentText.Substring(0, splitIndex).TrimEnd();
        string right = currentText.Substring(splitIndex).TrimStart();

        _rawLines[_activeEditLineIndex] = left;
        sharedInputField.SetTextWithoutNotify(left);

        // CRITICAL FIX: Clear the highlighting cache. Inserting new lines shifts
        // all subsequent indices up, rendering existing cache keys corrupted.
        _highlightedCache.Clear();

        _rawLines.Insert(_activeEditLineIndex + 1, right);
        int targetLine = _activeEditLineIndex + 1;

        CommitSharedInputToModel();
        RefreshDocumentLayout();

        if (targetLine < _rawLines.Count)
        {
            RequestLineEdit(targetLine, right.Length);
        }

        _isSwitchingLine = false;

        sharedInputField.ForceLabelUpdate();
        if (sharedInputField.textComponent.preferredWidth > absoluteMaxWidth)
        {
            TriggerAutoLineSplit();
        }
    }
    private void HandleLineSplit(string inputVal)
    {
        _isSwitchingLine = true;

        string cleanVal = inputVal.Replace("\r", "");
        string[] split = cleanVal.Split('\n');

        if (split.Length > 1)
        {
            _rawLines[_activeEditLineIndex] = split[0];
            sharedInputField.SetTextWithoutNotify(split[0]);

            // CRITICAL FIX: Clear the highlighting cache. Inserting new lines shifts
            // all subsequent indices up, rendering existing cache keys corrupted.
            _highlightedCache.Clear();

            for (int i = 1; i < split.Length; i++)
            {
                _rawLines.Insert(_activeEditLineIndex + i, split[i]);
            }

            int targetLine = _activeEditLineIndex + 1;

            CommitSharedInputToModel();
            RefreshDocumentLayout();

            if (targetLine < _rawLines.Count)
            {
                RequestLineEdit(targetLine, 0);
            }
        }

        _isSwitchingLine = false;
    }
    private void MergeWithPreviousLine()
    {
        _isSwitchingLine = true;

        int currentLine = _activeEditLineIndex;
        int prevLine = currentLine - 1;

        string currentText = sharedInputField.text;
        string prevText = _rawLines[prevLine];

        int newCaretPos = prevText.Length;

        _rawLines[prevLine] = prevText + currentText;

        // CRITICAL FIX: Clear the entire highlighting cache. Because we are removing a line,
        // all indices below it shift down, rendering existing cache keys corrupted.
        _highlightedCache.Clear();

        _rawLines.RemoveAt(currentLine);

        _activeEditLineIndex = -1;
        sharedInputField.gameObject.SetActive(false);

        RefreshDocumentLayout();
        RequestLineEdit(prevLine, newCaretPos);

        _isSwitchingLine = false;
    }
    private void MergeWithNextLine()
    {
        _isSwitchingLine = true;

        int currentLine = _activeEditLineIndex;
        int nextLine = currentLine + 1;

        string currentText = sharedInputField.text;
        string nextText = _rawLines[nextLine];

        string mergedText = currentText + nextText;
        sharedInputField.SetTextWithoutNotify(mergedText);

        _rawLines[currentLine] = mergedText;

        // CRITICAL FIX: Clear the entire highlighting cache. Because we are removing a line,
        // all indices below it shift down, rendering existing cache keys corrupted.
        _highlightedCache.Clear();

        _rawLines.RemoveAt(nextLine);

        RefreshDocumentLayout();

        sharedInputField.caretPosition = currentText.Length;

        _isSwitchingLine = false;
    }

    // =========================================================================
    // Standard Text Editor Navigation & Manipulation
    // =========================================================================

    public void OnRowPointerDown(int lineIndex, PointerEventData eventData)
    {
        // Cancel single-line input
        CommitSharedInputToModel();

        _isMultiSelecting = false; // Will become true if they drag
        _selStartLine = lineIndex;
        _selStartChar = GetCharIndexFromMousePosition(lineIndex, eventData.position);

        // Treat as a standard click request initially
        RequestLineEdit(lineIndex, _selStartChar);
    }
    public void OnRowDrag(PointerEventData eventData)
    {
        if (!_isMultiSelecting)
        {
            // Transition from Editing to MultiSelecting
            _isMultiSelecting = true;
            CommitSharedInputToModel(); // Hides the Input Field
        }

        // Calculate global line index based on mouse Y position in the container
        RectTransformUtility.ScreenPointToLocalPointInRectangle(contentContainer, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

        int draggedLineIndex = Mathf.Clamp(Mathf.FloorToInt(-localPoint.y / lineHeight), 0, _rawLines.Count - 1);
        int draggedCharIndex = GetCharIndexFromMousePosition(draggedLineIndex, eventData.position);

        _selEndLine = draggedLineIndex;
        _selEndChar = draggedCharIndex;

        // Force the virtualizer to redraw the highlights immediately
        UpdateVirtualViewport(true);
    }
    public void OnRowPointerUp(PointerEventData eventData)
    {
        if (!_isMultiSelecting) return;
        // Selection is locked in. The UI will sit in Read-Only Multi-Select mode until a key is pressed.
    }
    private int GetCharIndexFromMousePosition(int lineIndex, Vector2 screenPos)
    {
        IdeLineRow visualRow = GetVisualRowByLineIndex(lineIndex);
        if (visualRow == null) return _rawLines[lineIndex].Length;

        RectTransform textRt = visualRow.CodeTextComponent.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textRt, screenPos, null, out Vector2 localMousePos);

        // Use TMP's native spatial lookup to find the closest character
        int charIndex = TMP_TextUtilities.FindIntersectingCharacter(visualRow.CodeTextComponent, localMousePos, null, true);

        if (charIndex == -1) return _rawLines[lineIndex].Length; // Clicked past the end of the text
        return charIndex;
    }

    private IdeLineRow GetVisualRowByLineIndex(int lineIndex)
    {
        foreach (var r in _visibleRowsPool)
        {
            if (r.gameObject.activeSelf && r.CurrentLineIndex == lineIndex) return r;
        }
        return null;
    }

}