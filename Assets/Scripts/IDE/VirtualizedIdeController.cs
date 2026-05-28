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

    // =========================================================================
    // State Tracking
    // =========================================================================
    private int _activeEditLineIndex = -1;
    private bool _isUpdatingText = false;

    // =========================================================================
    // Precompiled Syntax Highlighting Regex (from your engine)
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
    // Initialization
    // =========================================================================

    private void Start()
    {
        // 1. Setup listeners and input overlay
        InitializeSharedInputField();
        viewportScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);

        // 2. Build the initial pool of rows (30 is a safe starting point)
        InitializeVirtualPool(30);

        // 3. Size the container and render the viewport
        RefreshDocumentLayout();
    }

    /*
    private void Awake()
    {
        InitializeSharedInputField();
        InitializeVirtualPool();
        viewportScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
        RefreshDocumentLayout();
    }
    */

    private void InitializeSharedInputField()
    {
        sharedInputField.gameObject.SetActive(false);
        sharedInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        sharedInputField.textComponent.enableAutoSizing = false;
        sharedInputField.textComponent.fontSize = 13;

        // FORCE ZERO MARGINS: Stop the text from padding itself inside the input box
        sharedInputField.textComponent.margin = new Vector4(0f, 0f, 0f, 0f);
        if (sharedInputField.textViewport != null)
        {
            sharedInputField.textViewport.offsetMin = Vector2.zero;
            sharedInputField.textViewport.offsetMax = Vector2.zero;
        }

        sharedInputField.onValueChanged.AddListener(OnActiveLineTextChanged);
        sharedInputField.onEndEdit.AddListener(OnActiveLineEndEdit);
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
                row.UpdateRowDisplay(lineIndex, GetHighlightedText(lineIndex), isEditingThisRow);
            }
            else
            {
                row.gameObject.SetActive(false);
            }
        }
    }

    public void RefreshDocumentLayout()
    {
        // Set the height of our content container directly, avoiding VerticalLayoutGroup
        float totalHeight = _rawLines.Count * lineHeight;
        contentContainer.sizeDelta = new Vector2(contentContainer.sizeDelta.x, totalHeight);

        UpdateVirtualViewport(true);
    }

    // =========================================================================
    // Document Editing & The Shared Input Field
    // =========================================================================
    // Added 'startCaretPos' parameter
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

        IdeLineRow clickedRow = GetVisualRowByLineIndex(lineIndex);
        IdeLineRow formattingSource = clickedRow != null ? clickedRow : _visibleRowsPool[0];

        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        inputRt.SetParent(contentContainer, false);
        inputRt.SetAsLastSibling();

        // 1. Force strictly top-stretch anchors
        inputRt.anchorMin = new Vector2(0, 1);
        inputRt.anchorMax = new Vector2(1, 1);
        inputRt.pivot = new Vector2(0, 1);
        inputRt.localScale = Vector3.one;
        inputRt.localRotation = Quaternion.identity;

        // 2. ABSOLUTE OFFSET MATH (Fixes the off-screen stretching bug)
        float leftOffset = formattingSource.CodeTextComponent.rectTransform.offsetMin.x;
        float topY = -lineIndex * lineHeight;
        float bottomY = topY - lineHeight;

        // offsetMin is (Left, Bottom). offsetMax is (Right, Top).
        inputRt.offsetMin = new Vector2(leftOffset, bottomY);
        inputRt.offsetMax = new Vector2(0, topY);

        TextMeshProUGUI sourceText = formattingSource.CodeTextComponent;
        TMP_Text targetText = sharedInputField.textComponent;

        if (sourceText != null && targetText != null)
        {
            targetText.font = sourceText.font;
            targetText.fontSize = sourceText.fontSize;
            targetText.fontStyle = sourceText.fontStyle;
            targetText.alignment = sourceText.alignment;
            targetText.lineSpacing = sourceText.lineSpacing;
            targetText.characterSpacing = sourceText.characterSpacing;
            targetText.wordSpacing = sourceText.wordSpacing;
            targetText.enableWordWrapping = sourceText.enableWordWrapping;
            targetText.margin = sourceText.margin;
            targetText.extraPadding = sourceText.extraPadding;
            targetText.enableAutoSizing = false;

            targetText.rectTransform.localScale = Vector3.one;
            sourceText.rectTransform.localScale = Vector3.one;
        }

        sharedInputField.gameObject.SetActive(true);
        sharedInputField.SetTextWithoutNotify(_rawLines[lineIndex]);

        // Force UI update so text meshes are instantly valid
        sharedInputField.ForceLabelUpdate();
        if (targetText != null) targetText.ForceMeshUpdate();

        // Move Caret safely
        if (startCaretPos >= 0)
        {
            sharedInputField.caretPosition = Mathf.Min(startCaretPos, sharedInputField.text.Length);
        }
        else
        {
            sharedInputField.caretPosition = sharedInputField.text.Length;
        }

        // Safely reset horizontal scrolling if it drifted during a previous paste
        sharedInputField.MoveTextStart(false);

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

        // BOUNDARY CHECK: Measure preferred text width against the input field's physical width
        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        float textWidth = sharedInputField.textComponent.preferredWidth;

        // 20px safety margin to split before characters visually clip the right edge
        float maxWidth = inputRt.rect.width - 20f;

        if (textWidth > maxWidth && maxWidth > 100f)
        {
            TriggerAutoLineSplit();
        }
    }

    private void OnActiveLineEndEdit(string text)
    {
        // Ignore focus loss if it was caused by us programmatically clicking a new line
        if (_isSwitchingLine) return;

        CommitSharedInputToModel();
    }

    private void CommitSharedInputToModel()
    {
        if (_activeEditLineIndex == -1) return;

        _rawLines[_activeEditLineIndex] = sharedInputField.text;
        _highlightedCache.Remove(_activeEditLineIndex); // Clear cache

        _activeEditLineIndex = -1;
        sharedInputField.gameObject.SetActive(false);
        UpdateVirtualViewport(true);
    }

    private void HandleLineSplit(string inputVal)
    {
        _isSwitchingLine = true; // Lock events while splitting

        string cleanVal = inputVal.Replace("\r", "");
        string[] split = cleanVal.Split('\n');

        if (split.Length > 1)
        {
            // 1. Clean the current line's data
            _rawLines[_activeEditLineIndex] = split[0];
            _highlightedCache.Remove(_activeEditLineIndex);

            // 2. Force the input field to drop the '\n' character 
            // so it doesn't accidentally save it as a multiline string in CommitSharedInputToModel()
            sharedInputField.SetTextWithoutNotify(split[0]);

            // 3. Inject new lines into the data model below the current line
            for (int i = 1; i < split.Length; i++)
            {
                _rawLines.Insert(_activeEditLineIndex + i, split[i]);
            }

            int targetLine = _activeEditLineIndex + 1;

            CommitSharedInputToModel();
            RefreshDocumentLayout();

            // 4. Focus the newly split line safely
            if (targetLine < _rawLines.Count)
            {
                Canvas.ForceUpdateCanvases();
                IdeLineRow targetRow = GetVisualRowByLineIndex(targetLine);
                if (targetRow != null)
                {
                    // UPDATED: Now uses the correct parameters
                    RequestLineEdit(targetLine, 0);
                }
            }
        }

        _isSwitchingLine = false; // Unlock events
    }

    private IdeLineRow GetVisualRowByLineIndex(int lineIndex)
    {
        foreach (var r in _visibleRowsPool)
        {
            if (r.gameObject.activeSelf && r.CurrentLineIndex == lineIndex) return r;
        }
        return null;
    }

    // =========================================================================
    // Loading and Pasting Massive Text (Safe WebGL and Editor)
    // =========================================================================
    public void LoadEntireDocument(string rawText)
    {
        _isUpdatingText = true;
        _activeEditLineIndex = -1;
        sharedInputField.gameObject.SetActive(false);

        _rawLines.Clear();
        _highlightedCache.Clear();

        // Fast splitting on line breaks
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

    // =========================================================================
    // Lazy Highlighting Engine
    // =========================================================================
    private string GetHighlightedText(int lineIndex)
    {
        if (_highlightedCache.TryGetValue(lineIndex, out string cache))
        {
            return cache;
        }

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

    // =========================================================================
    // Greedy Color Accumulator State
    // =========================================================================
    private string _activeColor = null;
    private StringBuilder _colorAccumulator = new StringBuilder();

    private void FlushColorSegment(StringBuilder mainSb)
    {
        if (_colorAccumulator.Length == 0) return;

        // GREEDY MESHING: If the color segment matches default text, write it directly without any tags
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
        // If the color changes, flush the previous colored segment first
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

            // Use TMP's built-in <noparse> tags to safely escape brackets
            if (c == '<')
            {
                sb.Append("<noparse><</noparse>");
            }
            else if (c == '>')
            {
                sb.Append("<noparse>></noparse>");
            }
            else
            {
                sb.Append(c);
            }
        }
    }
    // =========================================================================
    // Optimized Greedy Highlighting Engine
    // =========================================================================
    public string ApplySyntaxColorsSync(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;

        // Reset the accumulator state for this line
        _activeColor = null;
        _colorAccumulator.Clear();

        Match m;
        try { m = SyntaxRegex.Match(input); } catch { return input; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            // Collect unmatched gap text (like spaces or punctuation) as un-tagged text
            if (idx > lastIndex)
            {
                AccumulateText(sb, input, lastIndex, idx - lastIndex, null);
            }

            // Accumulate matching tokens based on their syntax group
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
                AccumulateText(sb, input, idx, 9, ColorMethod); // "itempool."
                AccumulateText(sb, input, idx + 9, len - 9, ColorItem);
            }
            else if (m.Groups["sd_block"].Success)
            {
                AccumulateText(sb, input, idx, 3, ColorMethod); // "sd."
                AccumulateText(sb, input, idx + 3, len - 3, ColorSdRed);
            }
            else if (m.Groups["ritemx"].Success) AccumulateText(sb, input, idx, len, ColorItem);
            else if (m.Groups["hsv_block"].Success)
            {
                AccumulateText(sb, input, idx, 4, ColorMethod); // "hsv."
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

        if (lastIndex < input.Length)
        {
            AccumulateText(sb, input, lastIndex, input.Length - lastIndex, null);
        }

        // Flush any remaining text segment left in the buffer to finish the line
        FlushColorSegment(sb);

        return sb.ToString();
    }

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
        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();

        // Evaluate geometry
        textComp.ForceMeshUpdate();
        float maxWidth = inputRt.rect.width - 20f;

        int splitIndex = currentText.Length - 1;
        bool overflowFound = false;

        // Find the exact character that crosses the right boundary
        for (int i = 0; i < textComp.textInfo.characterCount; i++)
        {
            if (textComp.textInfo.characterInfo[i].bottomRight.x > maxWidth)
            {
                splitIndex = i;
                overflowFound = true;
                break;
            }
        }

        if (!overflowFound)
        {
            _isSwitchingLine = false;
            return;
        }

        // Try to break at the last space to keep whole words intact
        int lastSpace = currentText.LastIndexOf(' ', splitIndex);
        if (lastSpace > 0 && lastSpace > (splitIndex - 20))
        {
            splitIndex = lastSpace;
        }

        string left = currentText.Substring(0, splitIndex).TrimEnd();
        string right = currentText.Substring(splitIndex).TrimStart();

        _rawLines[_activeEditLineIndex] = left;
        _highlightedCache.Remove(_activeEditLineIndex);
        sharedInputField.SetTextWithoutNotify(left);

        _rawLines.Insert(_activeEditLineIndex + 1, right);
        int targetLine = _activeEditLineIndex + 1;

        CommitSharedInputToModel();
        RefreshDocumentLayout();
        EnsureLineIsVisible(targetLine);
        Canvas.ForceUpdateCanvases();
        UpdateVirtualViewport(true);

        if (targetLine < _rawLines.Count)
        {
            // Jump to the newly created line
            RequestLineEdit(targetLine, right.Length);
        }

        _isSwitchingLine = false;

        // RECURSIVE CASCADING PASTE FIX
        // Force the input field to recalculate its bounds with the new 'right' text. 
        // If it STILL overflows (e.g. pasted a 1000-character paragraph), recursively split it again.
        sharedInputField.ForceLabelUpdate();
        if (sharedInputField.textComponent.preferredWidth > maxWidth)
        {
            TriggerAutoLineSplit();
        }
    }

    private void EnsureLineIsVisible(int lineIndex)
    {
        float viewportHeight = viewportScrollRect.viewport.rect.height;
        float lineTopY = lineIndex * lineHeight;
        float lineBottomY = lineTopY + lineHeight;
        float currentScrollY = contentContainer.anchoredPosition.y;

        // Scroll down if the line is pushed below the visible viewport
        if (lineBottomY > currentScrollY + viewportHeight)
        {
            float targetScrollY = lineBottomY - viewportHeight;
            contentContainer.anchoredPosition = new Vector2(contentContainer.anchoredPosition.x, targetScrollY);
        }
        // Scroll up if the line is pushed above the visible viewport
        else if (lineTopY < currentScrollY)
        {
            contentContainer.anchoredPosition = new Vector2(contentContainer.anchoredPosition.x, lineTopY);
        }
    }
}