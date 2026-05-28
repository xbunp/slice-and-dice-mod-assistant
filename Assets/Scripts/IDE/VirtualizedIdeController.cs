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
    private void Awake()
    {
        InitializeSharedInputField();
        InitializeVirtualPool();
        viewportScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
        RefreshDocumentLayout();
    }

    private void InitializeSharedInputField()
    {
        sharedInputField.gameObject.SetActive(false);
        sharedInputField.lineType = TMP_InputField.LineType.MultiLineNewline;

        sharedInputField.textComponent.enableAutoSizing = false;
        sharedInputField.textComponent.fontSize = 13;
        sharedInputField.onValueChanged.AddListener(OnActiveLineTextChanged);
        sharedInputField.onEndEdit.AddListener(OnActiveLineEndEdit);
    }

    private void InitializeVirtualPool()
    {
        float viewportHeight = viewportScrollRect.viewport.rect.height;
        int maxVisibleRows = Mathf.CeilToInt(viewportHeight / lineHeight) + extraBufferRows;

        for (int i = 0; i < maxVisibleRows; i++)
        {
            GameObject rowObj = Instantiate(lineRowPrefab, contentContainer);

            // FORCE: Clean local scale to prevent geometric text size discrepancies
            rowObj.transform.localScale = Vector3.one;

            IdeLineRow row = rowObj.GetComponent<IdeLineRow>();
            row.Initialize(i, this);
            _visibleRowsPool.Add(row);
            rowObj.SetActive(false);
        }
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

        // Determine first and last visible line indices
        int startLine = Mathf.Max(0, Mathf.FloorToInt(contentY / lineHeight) - 2);
        int visibleCount = Mathf.CeilToInt(viewportHeight / lineHeight) + extraBufferRows;
        int endLine = Mathf.Min(_rawLines.Count - 1, startLine + visibleCount);

        if (startLine == _startLineIndex && endLine == _endLineIndex && !forceRepaint)
        {
            return; // No layout update needed
        }

        _startLineIndex = startLine;
        _endLineIndex = endLine;

        // Map visible lines to our pool
        for (int i = 0; i < _visibleRowsPool.Count; i++)
        {
            int lineIndex = _startLineIndex + i;
            IdeLineRow row = _visibleRowsPool[i];

            if (lineIndex <= _endLineIndex && lineIndex < _rawLines.Count)
            {
                row.gameObject.SetActive(true);
                row.SetRowPosition(lineIndex, lineHeight);

                // Hide row graphics if this is the active editing row (to avoid text overlap)
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
    public void RequestLineEdit(int lineIndex, RectTransform rowRect, int startCaretPos = -1)
    {
        if (_activeEditLineIndex != -1 && _activeEditLineIndex != lineIndex)
        {
            CommitSharedInputToModel();
        }

        _activeEditLineIndex = lineIndex;

        IdeLineRow clickedRow = GetVisualRowByLineIndex(lineIndex);
        if (clickedRow == null) return;

        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        inputRt.SetParent(contentContainer, false);
        inputRt.SetAsLastSibling();

        inputRt.localScale = Vector3.one;
        inputRt.anchorMin = new Vector2(0, 1);
        inputRt.anchorMax = new Vector2(1, 1);
        inputRt.pivot = new Vector2(0, 1);
        inputRt.offsetMin = new Vector2(0, inputRt.offsetMin.y);
        inputRt.offsetMax = new Vector2(0, inputRt.offsetMax.y);
        inputRt.anchoredPosition = new Vector2(0, -lineIndex * lineHeight);
        inputRt.sizeDelta = new Vector2(inputRt.sizeDelta.x, lineHeight);

        TextMeshProUGUI sourceText = clickedRow.CodeTextComponent;
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

            RectTransform textAreaRt = sharedInputField.textViewport;
            RectTransform sourceTextRt = sourceText.GetComponent<RectTransform>();
            if (textAreaRt != null && sourceTextRt != null)
            {
                textAreaRt.localScale = Vector3.one;
                textAreaRt.anchorMin = sourceTextRt.anchorMin;
                textAreaRt.anchorMax = sourceTextRt.anchorMax;
                textAreaRt.pivot = sourceTextRt.pivot;
                textAreaRt.offsetMin = sourceTextRt.offsetMin;
                textAreaRt.offsetMax = sourceTextRt.offsetMax;
            }
        }

        sharedInputField.gameObject.SetActive(true);
        sharedInputField.text = _rawLines[lineIndex];
        sharedInputField.Select();
        sharedInputField.ActivateInputField();

        // Set caret position to start of new line if specified
        if (startCaretPos >= 0)
        {
            sharedInputField.caretPosition = Mathf.Min(startCaretPos, sharedInputField.text.Length);
        }

        UpdateVirtualViewport(true);
    }

    private void OnActiveLineTextChanged(string newText)
    {
        if (_activeEditLineIndex == -1) return;

        // Check if the user typed a line break or typed a tab
        if (newText.Contains("\n") || newText.Contains("\r"))
        {
            HandleLineSplit(newText);
            return;
        }

        _rawLines[_activeEditLineIndex] = newText;
        _highlightedCache.Remove(_activeEditLineIndex); // Invalidate cache
    }

    private void OnActiveLineEndEdit(string text)
    {
        // When clicking outside, or hitting Enter, save changes
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
        string cleanVal = inputVal.Replace("\r", "");
        string[] split = cleanVal.Split('\n');

        if (split.Length > 1)
        {
            // Update current line to only contain text before the split
            _rawLines[_activeEditLineIndex] = split[0];
            _highlightedCache.Remove(_activeEditLineIndex);

            // Insert new lines below
            for (int i = 1; i < split.Length; i++)
            {
                _rawLines.Insert(_activeEditLineIndex + i, split[i]);
            }

            int targetLine = _activeEditLineIndex + 1;

            CommitSharedInputToModel();
            RefreshDocumentLayout();

            // Focus the newly split line, placing cursor at index 0
            if (targetLine < _rawLines.Count)
            {
                Canvas.ForceUpdateCanvases();
                IdeLineRow targetRow = GetVisualRowByLineIndex(targetLine);
                if (targetRow != null)
                {
                    RequestLineEdit(targetLine, targetRow.GetComponent<RectTransform>(), 0);
                }
            }
        }
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

    private string ApplySyntaxColorsSync(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;
        Match m;

        try { m = SyntaxRegex.Match(input); } catch { return input; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AppendEscapedText(sb, input, lastIndex, idx - lastIndex);

            if (m.Groups["floor"].Success) AppendWithColor(sb, input, idx, len, ColorFloor);
            else if (m.Groups["phase"].Success)
            {
                if (input[idx] == 'p' && len > 3)
                {
                    sb.Append("<color=").Append(ColorPhasePrefix).Append(">");
                    AppendEscapedText(sb, input, idx, 3);
                    sb.Append("</color><color=").Append(ColorPhaseCode).Append(">");
                    AppendEscapedText(sb, input, idx + 3, len - 3);
                    sb.Append("</color>");
                }
                else AppendWithColor(sb, input, idx, len, ColorPhasePrefix);
            }
            else if (m.Groups["delimiter"].Success) AppendWithColor(sb, input, idx, len, ColorDelimiter);
            else if (m.Groups["sq_bracket"].Success || m.Groups["bracket"].Success) AppendWithColor(sb, input, idx, len, ColorBracket);
            else if (m.Groups["itempool_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscapedText(sb, input, idx, 9); // "itempool."
                sb.Append("</color><color=").Append(ColorItem).Append(">");
                AppendEscapedText(sb, input, idx + 9, len - 9);
                sb.Append("</color>");
            }
            else if (m.Groups["sd_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscapedText(sb, input, idx, 3); // "sd."
                sb.Append("</color><color=").Append(ColorSdRed).Append(">");
                AppendEscapedText(sb, input, idx + 3, len - 3);
                sb.Append("</color>");
            }
            else if (m.Groups["ritemx"].Success) AppendWithColor(sb, input, idx, len, ColorItem);
            else if (m.Groups["hsv_block"].Success)
            {
                sb.Append("<color=").Append(ColorMethod).Append(">");
                AppendEscapedText(sb, input, idx, 4); // "hsv."
                sb.Append("</color><color=").Append(ColorNumber).Append(">");
                AppendEscapedText(sb, input, idx + 4, len - 4);
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
            else AppendEscapedText(sb, input, idx, len);

            lastIndex = idx + len;
            m = m.NextMatch();
        }

        if (lastIndex < input.Length) AppendEscapedText(sb, input, lastIndex, input.Length - lastIndex);

        return sb.ToString();
    }

    private void AppendEscapedText(StringBuilder sb, string text, int start, int length)
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
        AppendEscapedText(sb, text, start, length);
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
}