using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;

public class VirtualizedIdeController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void UpdateWebGLSelectionCache(string text);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void InitializeNativeCopyListener();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void CopyToClipboardWebGL(string text);
#endif

    [Header("UI Scroll Settings")]
    [SerializeField] private ScrollRect viewportScrollRect;
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private int extraBufferRows = 5;

    [Header("Prefabs & Resources")]
    [SerializeField] private GameObject lineRowPrefab; // A prefab with IdeLineRow components
    [SerializeField] private CustomIdeInputField sharedInputField;

    // =========================================================================
    // Core Document Model (Data)
    // =========================================================================
    private List<string> _rawLines = new List<string> { "" };
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
    private bool _isSwitchingLine = false;

    // Selection State Machine
    private bool _isMultiSelecting = false;
    private int _selStartLine = -1;
    private int _selStartChar = -1;
    private int _selEndLine = -1;
    private int _selEndChar = -1;

    // Shift-Click Anchor State
    private int _lastAnchorLine = 0;
    private int _lastAnchorChar = 0;

    // Precomputed layout parameters
    private float _lockedLeftOffset;
    private float _lockedViewportWidth;

    // The active syntax provider
    private IdeSyntaxConfig _syntaxConfig;

    // Up/Down Navigation Repeat State
    private float _navRepeatTimer = 0f;
    private bool _isNavRepeating = false;
    private Key _activeNavRepeatKey = Key.None;

    private string _activeColor = null;
    private StringBuilder _colorAccumulator = new StringBuilder();

    // =========================================================================
    // Proactive Initialization Sanity Pass
    // =========================================================================

    private void Start()
    {
        //var config = new SDTextmodSyntaxConfig();
        //Initialize(config);
    }

    public void Initialize(IdeSyntaxConfig ideConfig)
    {
        // Set the active syntax configuration
        _syntaxConfig = ideConfig;

        PerformStrictInitializationSanityPass();

        sharedInputField.onValueChanged.AddListener(OnActiveLineTextChanged);
        sharedInputField.onEndEdit.AddListener(OnActiveLineEndEdit);
        viewportScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);

#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeNativeCopyListener();
#endif

        InitializeVirtualPool(30);
        RefreshDocumentLayout();
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

        // 5. Lock down the Input Field Container
        sharedInputField.gameObject.SetActive(false);
        RectTransform inputRt = sharedInputField.GetComponent<RectTransform>();
        inputRt.SetParent(contentContainer, false);
        inputRt.anchorMin = new Vector2(0, 1);
        inputRt.anchorMax = new Vector2(1, 1);
        inputRt.pivot = new Vector2(0, 1);
        inputRt.localScale = Vector3.one;
        inputRt.offsetMin = new Vector2(_lockedLeftOffset, 0);
        inputRt.offsetMax = Vector2.zero;

        // 6. Lock down the Text Renderer inside the custom input box
        TMP_Text inputTmp = sharedInputField.textComponent;
        inputTmp.rectTransform.anchorMin = Vector2.zero;
        inputTmp.rectTransform.anchorMax = Vector2.one;

        // FORCE PIVOT LEFT: Aligns local coordinate (0,0) with the starting text pixel
        inputTmp.rectTransform.pivot = new Vector2(0f, 0.5f);

        inputTmp.rectTransform.offsetMin = Vector2.zero;
        inputTmp.rectTransform.offsetMax = Vector2.zero;
        inputTmp.rectTransform.localScale = Vector3.one;
        inputTmp.rectTransform.localRotation = Quaternion.identity;

        // Clone the styling from the prefab once during startup
        inputTmp.font = sourceText.font;
        inputTmp.fontSize = sourceText.fontSize;
        inputTmp.fontStyle = sourceText.fontStyle;
        inputTmp.alignment = sourceText.alignment;
        inputTmp.lineSpacing = sourceText.lineSpacing;
        inputTmp.characterSpacing = sourceText.characterSpacing;
        inputTmp.wordSpacing = sourceText.wordSpacing;
        inputTmp.enableWordWrapping = false;
        inputTmp.margin = new Vector4(0, sourceText.margin.y, sourceText.margin.z, sourceText.margin.w);
        inputTmp.extraPadding = false;
        inputTmp.enableAutoSizing = false;

        // Force layout calculations...
        Canvas.ForceUpdateCanvases();
        _lockedViewportWidth = viewportRt.rect.width;

        // 7. Lock down the Text Renderer inside the input box (Stop horizontal text scrolling)
        inputTmp = sharedInputField.textComponent;
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
        inputTmp.margin = new Vector4(0, sourceText.margin.y, sourceText.margin.z, sourceText.margin.w);
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
    // Input System & Command Loop
    // =========================================================================

    private void Update()
    {
        if (_isSwitchingLine) return;

        if (_activeEditLineIndex == -1 && !_isMultiSelecting) return;

        // Get New Input System Keyboard State
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool isControlPressed = keyboard.ctrlKey.isPressed;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        isControlPressed |= keyboard.commandKey.isPressed;
#endif

        // --- Multi-Select Mode Shortcuts ---
        if (_isMultiSelecting)
        {
            if (isControlPressed && keyboard.cKey.wasPressedThisFrame)
            {
                CopySelectionToClipboard();
                return;
            }

            if (isControlPressed && keyboard.xKey.wasPressedThisFrame)
            {
                CopySelectionToClipboard();
                DeleteActiveSelection();
                return;
            }

            if (isControlPressed && keyboard.vKey.wasPressedThisFrame)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                ReadOSClipboard(gameObject.name, "OnWebGLPasteReceived");
#else
                PasteClipboardOverSelection(GUIUtility.systemCopyBuffer);
#endif
                return;
            }

            if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
            {
                _isSwitchingLine = true;

                int targetLine = Mathf.Min(_selStartLine, _selEndLine);
                int targetChar = _selStartLine == _selEndLine
                    ? Mathf.Min(_selStartChar, _selEndChar)
                    : (_selStartLine < _selEndLine ? _selStartChar : _selEndChar);

                DeleteActiveSelection();
                RequestLineEdit(targetLine, targetChar);

                _isSwitchingLine = false;
                return;
            }
        }

        // --- Standard Single-Line Input Shortcuts (When typing) ---
        // FIXED: Removed the obsolete readOnly conditions entirely
        if (sharedInputField.isFocused)
        {
            if (isControlPressed && keyboard.vKey.wasPressedThisFrame)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                ReadOSClipboard(gameObject.name, "OnWebGLPasteReceived");
#else
                PasteClipboardOverSelection(GUIUtility.systemCopyBuffer);
#endif
                return;
            }

            // Backspace at start of line -> Merge up
            if (keyboard.backspaceKey.wasPressedThisFrame)
            {
                if (sharedInputField.caretPosition == 0 && !sharedInputField.HasSelection())
                {
                    if (_activeEditLineIndex > 0)
                    {
                        MergeWithPreviousLine();
                        return;
                    }
                }
            }

            // Delete at end of line -> Merge down
            if (keyboard.deleteKey.wasPressedThisFrame)
            {
                if (sharedInputField.caretPosition == sharedInputField.text.Length && !sharedInputField.HasSelection())
                {
                    if (_activeEditLineIndex < _rawLines.Count - 1)
                    {
                        MergeWithNextLine();
                        return;
                    }
                }
            }

            // --- NAVIGATION KEY REPEAT STATE MACHINE (Up / Down) ---
            Key currentPressedNavKey = Key.None;
            if (keyboard.upArrowKey.isPressed) currentPressedNavKey = Key.UpArrow;
            else if (keyboard.downArrowKey.isPressed) currentPressedNavKey = Key.DownArrow;

            if (currentPressedNavKey != Key.None)
            {
                bool wasPressedThisFrame = false;
                if (currentPressedNavKey == Key.UpArrow) wasPressedThisFrame = keyboard.upArrowKey.wasPressedThisFrame;
                else if (currentPressedNavKey == Key.DownArrow) wasPressedThisFrame = keyboard.downArrowKey.wasPressedThisFrame;

                if (currentPressedNavKey != _activeNavRepeatKey || wasPressedThisFrame)
                {
                    _activeNavRepeatKey = currentPressedNavKey;
                    _navRepeatTimer = 0f;
                    _isNavRepeating = false;
                    ExecuteNavigationKey(currentPressedNavKey);
                }
                else
                {
                    _navRepeatTimer += Time.deltaTime;
                    // Match same rate/delay as the input field character repeat
                    float currentThreshold = _isNavRepeating ? 0.05f : 0.5f;
                    if (_navRepeatTimer >= currentThreshold)
                    {
                        _navRepeatTimer = 0f;
                        _isNavRepeating = true;
                        ExecuteNavigationKey(currentPressedNavKey);
                    }
                }
            }
            else
            {
                _activeNavRepeatKey = Key.None;
                _navRepeatTimer = 0f;
                _isNavRepeating = false;
            }
        }
    }

    private void ExecuteNavigationKey(Key key)
    {
        if (key == Key.UpArrow)
        {
            if (_activeEditLineIndex > 0)
            {
                int caret = sharedInputField.caretPosition;
                RequestLineEdit(_activeEditLineIndex - 1, caret);
            }
        }
        else if (key == Key.DownArrow)
        {
            if (_activeEditLineIndex < _rawLines.Count - 1)
            {
                int caret = sharedInputField.caretPosition;
                RequestLineEdit(_activeEditLineIndex + 1, caret);
            }
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

        int startLine = Mathf.Max(0, Mathf.FloorToInt(contentY / lineHeight) - 2);
        int visibleCount = Mathf.CeilToInt(viewportHeight / lineHeight) + extraBufferRows;
        int endLine = Mathf.Min(_rawLines.Count - 1, startLine + visibleCount);

        if (startLine == _startLineIndex && endLine == _endLineIndex && !forceRepaint)
        {
            return;
        }

        _startLineIndex = startLine;
        _endLineIndex = endLine;

        int requiredRowsCount = (endLine - startLine) + 1;
        while (_visibleRowsPool.Count < requiredRowsCount)
        {
            CreateAndAddNewRowToPool();
        }

        for (int i = 0; i < _visibleRowsPool.Count; i++)
        {
            int lineIndex = _startLineIndex + i;
            IdeLineRow row = _visibleRowsPool[i];

            if (lineIndex <= _endLineIndex && lineIndex < _rawLines.Count)
            {
                row.gameObject.SetActive(true);
                row.SetRowPosition(lineIndex, lineHeight);

                bool isEditingThisRow = (lineIndex == _activeEditLineIndex);

                int hlStart = -1;
                int hlEnd = -1;

                if (_isMultiSelecting)
                {
                    int minLine = Mathf.Min(_selStartLine, _selEndLine);
                    int maxLine = Mathf.Max(_selStartLine, _selEndLine);

                    if (lineIndex >= minLine && lineIndex <= maxLine)
                    {
                        if (_selStartLine == _selEndLine)
                        {
                            hlStart = Mathf.Min(_selStartChar, _selEndChar);
                            hlEnd = Mathf.Max(_selStartChar, _selEndChar);
                        }
                        else if (lineIndex == minLine)
                        {
                            hlStart = (minLine == _selStartLine) ? _selStartChar : _selEndChar;
                            hlEnd = _rawLines[lineIndex].Length;
                        }
                        else if (lineIndex == maxLine)
                        {
                            hlStart = 0;
                            hlEnd = (maxLine == _selEndLine) ? _selEndChar : _selStartChar;
                        }
                        else
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
    // Mouse Interaction & Multi-Select
    // =========================================================================

    public void OnRowPointerDown(int lineIndex, PointerEventData eventData)
    {
        var keyboard = Keyboard.current;
        bool isShiftPressed = keyboard != null && keyboard.shiftKey.isPressed;

        // Register Click 1 on the shared tracker before committing changes or swapping objects
        sharedInputField.RecordExternalClick();
        int clickCount = sharedInputField.GetClickCount();

        if (clickCount >= 3)
        {
            CommitSharedInputToModel();
            _isMultiSelecting = false;

            // Triple click: Open edit directly selecting the entire line
            string lineText = _rawLines[lineIndex];
            RequestLineEdit(lineIndex, lineText.Length, 0);
            return;
        }
        else if (clickCount == 2)
        {
            CommitSharedInputToModel();
            _isMultiSelecting = false;

            // Double click: Open edit directly selecting the targeted word
            int charIndex = GetCharIndexFromMousePosition(lineIndex, eventData.position);
            GetWordBoundaries(_rawLines[lineIndex], charIndex, out int start, out int end);

            RequestLineEdit(lineIndex, end, start);
            return;
        }

        // Single click logic
        if (isShiftPressed)
        {
            if (sharedInputField.gameObject.activeSelf && _activeEditLineIndex != -1)
            {
                _lastAnchorLine = _activeEditLineIndex;
                _lastAnchorChar = sharedInputField.caretPosition;
            }

            CommitSharedInputToModel();

            _isMultiSelecting = true;
            _selStartLine = _lastAnchorLine;
            _selStartChar = _lastAnchorChar;
            _selEndLine = lineIndex;
            _selEndChar = GetCharIndexFromMousePosition(lineIndex, eventData.position);

            UpdateVirtualViewport(true);
            UpdateSelectionCache();
        }
        else
        {
            CommitSharedInputToModel();

            _isMultiSelecting = false;

            _lastAnchorLine = lineIndex;
            _lastAnchorChar = GetCharIndexFromMousePosition(lineIndex, eventData.position);

            _selStartLine = _lastAnchorLine;
            _selStartChar = _lastAnchorChar;

            RequestLineEdit(lineIndex, _selStartChar);
            UpdateSelectionCache();
        }
    }
    public void OnRowDrag(PointerEventData eventData)
    {
        if (!_isMultiSelecting)
        {
            _isMultiSelecting = true;
            CommitSharedInputToModel();
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(contentContainer, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

        int draggedLineIndex = Mathf.Clamp(Mathf.FloorToInt(-localPoint.y / lineHeight), 0, _rawLines.Count - 1);
        int draggedCharIndex = GetCharIndexFromMousePosition(draggedLineIndex, eventData.position);

        _selEndLine = draggedLineIndex;
        _selEndChar = draggedCharIndex;

        UpdateVirtualViewport(true);
        UpdateSelectionCache();
    }
    public void OnRowPointerUp(PointerEventData eventData)
    {
        if (!_isMultiSelecting) return;
    }
    private int GetCharIndexFromMousePosition(int lineIndex, Vector2 screenPos)
    {
        IdeLineRow visualRow = GetVisualRowByLineIndex(lineIndex);
        if (visualRow == null) return _rawLines[lineIndex].Length;

        TMP_Text textComp = visualRow.CodeTextComponent;
        textComp.ForceMeshUpdate();

        Canvas canvas = textComp.canvas;
        Camera uiCamera = (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        // Find local position to evaluate left/right splitting
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textComp.rectTransform, screenPos, uiCamera, out Vector2 localMousePos);

        if (localMousePos.x < 0f) return 0;
        if (textComp.textInfo.characterCount == 0) return 0;

        // BUG FIX: Pass raw screenPos to TMP_TextUtilities! It expects Screen Space, not Local Space.
        int nearestChar = TMP_TextUtilities.FindNearestCharacter(textComp, screenPos, uiCamera, true);
        if (nearestChar == -1) return _rawLines[lineIndex].Length;

        TMP_CharacterInfo charInfo = textComp.textInfo.characterInfo[nearestChar];
        float charCenter = (charInfo.bottomLeft.x + charInfo.bottomRight.x) / 2f;

        return (localMousePos.x < charCenter) ? nearestChar : nearestChar + 1;
    }

    // =========================================================================
    // Lean Runtime Editing & Text Manipulation
    // =========================================================================

    // Locate RequestLineEdit and update its signature and caret assignment block:
    public void RequestLineEdit(int lineIndex, int startCaretPos = -1, int selectionAnchorPos = -1)
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
        inputRt.SetAsLastSibling();

        float topY = -lineIndex * lineHeight;
        float bottomY = topY - lineHeight;
        inputRt.offsetMin = new Vector2(_lockedLeftOffset, bottomY);
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
            targetText.extraPadding = sourceText.extraPadding;
            targetText.enableAutoSizing = false;

            targetText.margin = new Vector4(0f, sourceText.margin.y, sourceText.margin.z, sourceText.margin.w);

            targetText.rectTransform.localScale = Vector3.one;
            sourceText.rectTransform.localScale = Vector3.one;
        }

        sharedInputField.gameObject.SetActive(true);
        sharedInputField.SetTextWithoutNotify(_rawLines[lineIndex]);

        // Move Caret and Anchor safely using the new optional parameter
        int targetCaret = startCaretPos >= 0 ? Mathf.Min(startCaretPos, sharedInputField.text.Length) : sharedInputField.text.Length;
        int targetAnchor = selectionAnchorPos >= 0 ? Mathf.Min(selectionAnchorPos, sharedInputField.text.Length) : targetCaret;

        sharedInputField.caretPosition = targetCaret;
        sharedInputField.selectionAnchorPosition = targetAnchor;

        if (targetText != null)
        {
            targetText.rectTransform.anchoredPosition = Vector2.zero;
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

        // ADD THIS:
        sharedInputField.DeactivateInputField();

        sharedInputField.gameObject.SetActive(false);
        UpdateVirtualViewport(true);
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

        if (sharedInputField.textComponent.preferredWidth > absoluteMaxWidth)
        {
            TriggerAutoLineSplit();
        }
    }
    private int FindOverflowSplitIndex(string text, float maxWidth, TMP_Text textComponent)
    {
        int low = 1;
        int high = text.Length;
        int result = text.Length;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            string substring = text.Substring(0, mid);
            float width = textComponent.GetPreferredValues(substring).x;

            if (width > maxWidth)
            {
                result = mid - 1;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }
        return Mathf.Clamp(result, 1, text.Length - 1);
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
        _highlightedCache.Clear();

        _rawLines.RemoveAt(nextLine);

        RefreshDocumentLayout();
        sharedInputField.caretPosition = currentText.Length;

        _isSwitchingLine = false;
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
    // Clipboard Integration
    // =========================================================================

    private void DeleteActiveSelection()
    {
        if (!_isMultiSelecting) return;

        int startLine = Mathf.Min(_selStartLine, _selEndLine);
        int endLine = Mathf.Max(_selStartLine, _selEndLine);
        int startChar = _selStartLine < _selEndLine ? _selStartChar : _selEndChar;
        int endChar = _selStartLine < _selEndLine ? _selEndChar : _selStartChar;

        if (_selStartLine == _selEndLine)
        {
            startChar = Mathf.Min(_selStartChar, _selEndChar);
            endChar = Mathf.Max(_selStartChar, _selEndChar);
        }

        string startPreserved = _rawLines[startLine].Substring(0, startChar);
        string endPreserved = _rawLines[endLine].Substring(endChar);

        string mergedLine = startPreserved + endPreserved;

        int linesToRemove = endLine - startLine;
        for (int i = 0; i < linesToRemove; i++)
        {
            _rawLines.RemoveAt(startLine + 1);
        }

        _rawLines[startLine] = mergedLine;

        _highlightedCache.Clear();
        _isMultiSelecting = false;

        UpdateSelectionCache();
        RefreshDocumentLayout();
    }
    public void OnWebGLPasteReceived(string clipboardText)
    {
        if (string.IsNullOrEmpty(clipboardText)) return;
        PasteClipboardOverSelection(clipboardText);
    }
    private void PasteClipboardOverSelection(string clipboardText)
    {
        if (string.IsNullOrEmpty(clipboardText)) return;

        int targetLine;
        int targetChar;

        // =========================================================================
        // DYNAMIC INGESTION: Evaluate if pasting over a block or into a caret
        // =========================================================================
        if (_isMultiSelecting)
        {
            targetLine = Mathf.Min(_selStartLine, _selEndLine);
            targetChar = _selStartLine == _selEndLine
                ? Mathf.Min(_selStartChar, _selEndChar)
                : (_selStartLine < _selEndLine ? _selStartChar : _selEndChar);

            DeleteActiveSelection(); // Wipe selection, collapse rows
        }
        else
        {
            // We are just typing normally; use the active line and live caret position
            targetLine = _activeEditLineIndex;
            targetChar = sharedInputField.caretPosition;
        }

        _isSwitchingLine = true;

        string cleanString = clipboardText.Replace("\r", "");
        string[] pasteLines = cleanString.Split('\n');

        string originalLineText = _rawLines[targetLine];
        string left = originalLineText.Substring(0, targetChar);
        string right = originalLineText.Substring(targetChar);

        if (pasteLines.Length == 1)
        {
            _rawLines[targetLine] = left + pasteLines[0] + right;
            _highlightedCache.Clear();
            RefreshDocumentLayout();
            RequestLineEdit(targetLine, left.Length + pasteLines[0].Length);
        }
        else
        {
            _rawLines[targetLine] = left + pasteLines[0];

            for (int i = 1; i < pasteLines.Length - 1; i++)
            {
                _rawLines.Insert(targetLine + i, pasteLines[i]);
            }

            int lastPasteIndex = targetLine + pasteLines.Length - 1;
            _rawLines.Insert(lastPasteIndex, pasteLines[pasteLines.Length - 1] + right);

            _highlightedCache.Clear();
            RefreshDocumentLayout();
            RequestLineEdit(lastPasteIndex, pasteLines[pasteLines.Length - 1].Length);
        }

        _isSwitchingLine = false;
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
    // External Loading & Syntax Delegation
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
    private void FlushColorSegment(StringBuilder mainSb)
    {
        if (_colorAccumulator.Length == 0) return;

        if (string.IsNullOrEmpty(_activeColor) || _activeColor == _syntaxConfig.DefaultTextColor)
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
        if (string.IsNullOrEmpty(input) || _syntaxConfig == null) return string.Empty;

        StringBuilder sb = new StringBuilder(input.Length * 2);
        int lastIndex = 0;

        _activeColor = null;
        _colorAccumulator.Clear();

        Match m;
        try { m = _syntaxConfig.SyntaxRegex.Match(input); } catch { return input; }

        while (m.Success)
        {
            int idx = m.Index;
            int len = m.Length;

            if (idx > lastIndex) AccumulateText(sb, input, lastIndex, idx - lastIndex, null);

            _syntaxConfig.ProcessMatch(m, input, sb, AccumulateText);

            lastIndex = idx + len;
            m = m.NextMatch();
        }

        if (lastIndex < input.Length) AccumulateText(sb, input, lastIndex, input.Length - lastIndex, null);

        FlushColorSegment(sb);
        return sb.ToString();
    }

    // =========================================================================
    // Clipboard Integration
    // =========================================================================

    // HELPER: Retrieves the currently selected text block without touching any clipboards

    // 1. Core platform-agnostic clipboard writer
    private void WriteToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    try
    {
        UpdateWebGLSelectionCache(text); 
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"WebGL Copy failed: {ex.Message}");
    }
#endif
        GUIUtility.systemCopyBuffer = text ?? ""; // <-- Removed #else block to ensure fallback safety
    }

    private void CopySelectionToClipboard()
    {
        if (_isMultiSelecting)
        {
            string text = GetSelectedText();
            WriteToClipboard(text);

#if UNITY_WEBGL && !UNITY_EDITOR
        try { CopyToClipboardWebGL(text); } catch { } // <-- Added explicit copy call for WebGL keydown context
#endif
        }
    }

    // 2. Extractor helper (Pure data)
    private string GetSelectedText()
    {
        if (!_isMultiSelecting) return "";

        int startLine = Mathf.Min(_selStartLine, _selEndLine);
        int endLine = Mathf.Max(_selStartLine, _selEndLine);
        int startChar = _selStartLine < _selEndLine ? _selStartChar : _selEndChar;
        int endChar = _selStartLine < _selEndLine ? _selEndChar : _selStartChar;

        if (_selStartLine == _selEndLine)
        {
            startChar = Mathf.Min(_selStartChar, _selEndChar);
            endChar = Mathf.Max(_selStartChar, _selEndChar);
        }

        StringBuilder sb = new StringBuilder();
        for (int i = startLine; i <= endLine; i++)
        {
            string lineText = _rawLines[i];
            int s = (i == startLine) ? startChar : 0;
            int e = (i == endLine) ? endChar : lineText.Length;

            s = Mathf.Clamp(s, 0, lineText.Length);
            e = Mathf.Clamp(e, 0, lineText.Length);

            sb.Append(lineText.Substring(s, e - s));
            if (i < endLine) sb.Append("\n");
        }

        return sb.ToString();
    }

    // 3. Dynamic Cache Updater (Only runs on Drag in WebGL to satisfy browser security)
    private void UpdateSelectionCache()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_isMultiSelecting)
        {
            WriteToClipboard("");
            return;
        }
        WriteToClipboard(GetSelectedText());
#endif
    }

    public static void GetWordBoundaries(string text, int charIndex, out int start, out int end)
    {
        start = charIndex;
        end = charIndex;
        if (string.IsNullOrEmpty(text)) return;

        charIndex = Mathf.Clamp(charIndex, 0, text.Length - 1);
        char targetChar = text[charIndex];
        bool isWordChar = char.IsLetterOrDigit(targetChar) || targetChar == '_';

        while (start > 0)
        {
            char c = text[start - 1];
            bool cIsWord = char.IsLetterOrDigit(c) || c == '_';
            if (cIsWord != isWordChar) break;
            start--;
        }

        while (end < text.Length)
        {
            char c = text[end];
            bool cIsWord = char.IsLetterOrDigit(c) || c == '_';
            if (cIsWord != isWordChar) break;
            end++;
        }
    }
}