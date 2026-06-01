using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public class IdeTextChangeEvent : UnityEvent<string> { }

public class CustomIdeInputField : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void CopyToClipboardWebGL(string text);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void UpdateWebGLSelectionCache(string text);
#endif

    private string _lastCachedSelection = null;

    [Header("Core References")]
    public TMP_Text textComponent;
    public RectTransform caretRect;
    public Image caretImage;

    [Header("Selection Highlight")]
    public RectTransform selectionHighlightRect;
    public Image selectionHighlightImage;

    [Header("Settings")]
    public float caretBlinkRate = 0.5f;

    [Header("Events")]
    public IdeTextChangeEvent onValueChanged = new IdeTextChangeEvent();
    public IdeTextChangeEvent onEndEdit = new IdeTextChangeEvent();
    public UnityEvent onCaretMoved = new UnityEvent();

    [Header("Key Repeat Settings")]
    public float keyRepeatDelay = 0.5f; // Initial pause before repeating starts
    public float keyRepeatRate = 0.05f;  // Delay between repeats (0.05s = 20 letters/sec)

    private float _keyRepeatTimer = 0f;
    private bool _isKeyRepeating = false;
    private Key _activeRepeatKey = Key.None;

    private float _lastClickTime = 0f;
    private int _clickCount = 0;
    private const float DOUBLE_CLICK_TIME = 0.3f; // Max delay between clicks (seconds)

    // API Parity with TMP_InputField
    private string _text = "";
    public string text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            _selectionAnchorPosition = Mathf.Clamp(_selectionAnchorPosition, 0, _text.Length);
            _caretPosition = Mathf.Clamp(_caretPosition, 0, _text.Length);
            UpdateTextComponent();
            onValueChanged?.Invoke(_text);
        }
    }
    private int _caretPosition = 0;
    public int caretPosition
    {
        get => _caretPosition;
        set
        {
            _caretPosition = Mathf.Clamp(value, 0, _text.Length);
            UpdateCaretVisuals();
            ResetCaretBlink();
            onCaretMoved?.Invoke();
        }
    }
    private int _selectionAnchorPosition = 0;
    public int selectionAnchorPosition
    {
        get => _selectionAnchorPosition;
        set
        {
            _selectionAnchorPosition = Mathf.Clamp(value, 0, _text.Length);
            UpdateCaretVisuals();
        }
    }
    public bool isFocused { get; private set; } = false;

    // Internal state
    private float _blinkTimer;
    private bool _caretVisible = true;
    private bool _isDragging = false;

    private void OnEnable()
    {
        Keyboard.current.onTextInput += OnTextInput;
    }
    private void OnDisable()
    {
        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= OnTextInput;
    }
    private void Update()
    {
        if (!isFocused) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // --- Caret Blinking ---
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= caretBlinkRate)
        {
            _blinkTimer = 0f;
            _caretVisible = !_caretVisible;
            if (caretImage != null) caretImage.enabled = _caretVisible && !HasSelection();
        }

        // --- Clipboard Commands Intercept (Ctrl Key) ---
        bool isControlPressed = kb.ctrlKey.isPressed;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        isControlPressed |= kb.commandKey.isPressed;
#endif

        if (isControlPressed)
        {
            if (kb.cKey.wasPressedThisFrame && HasSelection())
            {
                CopySelectedToClipboard();
                return;
            }
            if (kb.xKey.wasPressedThisFrame && HasSelection())
            {
                CopySelectedToClipboard();
                DeleteSelectedText();
                return;
            }
            /*
            if (kb.vKey.wasPressedThisFrame)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                PasteText(GUIUtility.systemCopyBuffer);
#endif
                return;
            }
            */
            return;
        }

        // --- UNIFIED KEY REPEAT STATE MACHINE (Backspace, Delete, Left, Right) ---
        Key currentPressedKey = Key.None;
        if (kb.backspaceKey.isPressed) currentPressedKey = Key.Backspace;
        else if (kb.deleteKey.isPressed) currentPressedKey = Key.Delete;
        else if (kb.leftArrowKey.isPressed) currentPressedKey = Key.LeftArrow;
        else if (kb.rightArrowKey.isPressed) currentPressedKey = Key.RightArrow;

        if (currentPressedKey != Key.None)
        {
            bool wasPressedThisFrame = false;
            if (currentPressedKey == Key.Backspace) wasPressedThisFrame = kb.backspaceKey.wasPressedThisFrame;
            else if (currentPressedKey == Key.Delete) wasPressedThisFrame = kb.deleteKey.wasPressedThisFrame;
            else if (currentPressedKey == Key.LeftArrow) wasPressedThisFrame = kb.leftArrowKey.wasPressedThisFrame;
            else if (currentPressedKey == Key.RightArrow) wasPressedThisFrame = kb.rightArrowKey.wasPressedThisFrame;

            if (currentPressedKey != _activeRepeatKey || wasPressedThisFrame)
            {
                // First click frame: trigger instantly
                _activeRepeatKey = currentPressedKey;
                _keyRepeatTimer = 0f;
                _isKeyRepeating = false;
                ExecuteKeyAction(currentPressedKey);
            }
            else
            {
                // Key is held down: track delay timers
                _keyRepeatTimer += Time.deltaTime;
                float currentThreshold = _isKeyRepeating ? keyRepeatRate : keyRepeatDelay;
                if (_keyRepeatTimer >= currentThreshold)
                {
                    _keyRepeatTimer = 0f;
                    _isKeyRepeating = true;
                    ExecuteKeyAction(currentPressedKey);
                }
            }
        }
        else
        {
            // No keys pressed: reset state machine
            _activeRepeatKey = Key.None;
            _keyRepeatTimer = 0f;
            _isKeyRepeating = false;
        }

        // --- Enter Key ---
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            InsertCharacter('\n');
        }
    }
    private void Awake()
    {
        // Force Text Component to Left Pivot
        if (textComponent != null)
        {
            textComponent.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        // Force Caret Anchors (Left-Stretch)
        if (caretRect != null)
        {
            caretRect.anchorMin = new Vector2(0, 0);
            caretRect.anchorMax = new Vector2(0, 1);
            caretRect.pivot = new Vector2(0, 0.5f);
        }

        // Force Highlight Anchors (Left-Stretch)
        if (selectionHighlightRect != null)
        {
            selectionHighlightRect.anchorMin = new Vector2(0, 0);
            selectionHighlightRect.anchorMax = new Vector2(0, 1);
            selectionHighlightRect.pivot = new Vector2(0, 0.5f);
        }
    }

    private void ExecuteKeyAction(Key key)
    {
        if (key == Key.Backspace)
        {
            if (HasSelection())
            {
                DeleteSelectedText();
            }
            else if (caretPosition > 0)
            {
                _text = _text.Remove(caretPosition - 1, 1);
                _selectionAnchorPosition = caretPosition - 1;
                caretPosition = caretPosition - 1;
                UpdateTextComponent();
                onValueChanged?.Invoke(_text);
            }
        }
        else if (key == Key.Delete)
        {
            if (HasSelection())
            {
                DeleteSelectedText();
            }
            else if (caretPosition < _text.Length)
            {
                _text = _text.Remove(caretPosition, 1);
                UpdateTextComponent();
                onValueChanged?.Invoke(_text);
            }
        }
        else if (key == Key.LeftArrow)
        {
            int targetCaret = caretPosition - 1;
            bool isShiftPressed = Keyboard.current.shiftKey.isPressed;
            if (isShiftPressed)
            {
                caretPosition = targetCaret; // Shift Selection
            }
            else
            {
                // COLLAPSE FIX: Update selectionAnchorPosition FIRST so that caretPosition's 
                // property setter runs the visual update with synchronized variables.
                selectionAnchorPosition = targetCaret;
                caretPosition = targetCaret;
            }
        }
        else if (key == Key.RightArrow)
        {
            int targetCaret = caretPosition + 1;
            bool isShiftPressed = Keyboard.current.shiftKey.isPressed;
            if (isShiftPressed)
            {
                caretPosition = targetCaret; // Shift Selection
            }
            else
            {
                // COLLAPSE FIX: Update selectionAnchorPosition FIRST so that caretPosition's 
                // property setter runs the visual update with synchronized variables.
                selectionAnchorPosition = targetCaret;
                caretPosition = targetCaret;
            }
        }
    }
    private void OnTextInput(char c)
    {
        if (!isFocused) return;
        if (char.IsControl(c)) return;

        InsertCharacter(c);
    }
    private void InsertCharacter(char c)
    {
        if (HasSelection()) DeleteSelectedText();

        _text = _text.Insert(caretPosition, c.ToString());
        _selectionAnchorPosition = caretPosition + 1;
        caretPosition = caretPosition + 1;
        UpdateTextComponent();
        onValueChanged?.Invoke(_text);
    }

    // --- Drag and Selection Event System Interceptors ---

    private int GetCaretIndexFromMouse(PointerEventData eventData)
    {
        Canvas canvas = textComponent.canvas;
        Camera uiCamera = (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        // Find local position to evaluate left/right splitting
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, uiCamera, out Vector2 localMousePos);

        if (localMousePos.x < 0f) return 0;

        textComponent.ForceMeshUpdate();
        if (textComponent.textInfo.characterCount == 0) return 0;

        // BUG FIX: Pass eventData.position (Screen Space) to TMP_TextUtilities!
        int nearestChar = TMP_TextUtilities.FindNearestCharacter(textComponent, eventData.position, uiCamera, true);
        if (nearestChar == -1) return _text.Length;

        TMP_CharacterInfo charInfo = textComponent.textInfo.characterInfo[nearestChar];
        float charCenter = (charInfo.bottomLeft.x + charInfo.bottomRight.x) / 2f;

        return (localMousePos.x < charCenter) ? nearestChar : nearestChar + 1;
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (textComponent != null)
        {
            textComponent.ForceMeshUpdate();
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, eventData.pressEventCamera);
            if (linkIndex != -1)
            {
                string linkId = textComponent.textInfo.linkInfo[linkIndex].GetLinkID();

                // Query parent to locate the active IDE controller
                var controller = GetComponentInParent<VirtualizedIdeController>();
                if (controller != null && controller.OnLinkActivated != null)
                {
                    controller.OnLinkActivated.Invoke(linkId);
                    return; // Return early, blocking caret updates or text selection
                }
            }
        }

        Select();

        // Increment or reset click sequence manually
        float now = Time.unscaledTime;
        if (now - _lastClickTime < DOUBLE_CLICK_TIME)
        {
            _clickCount++;
        }
        else
        {
            _clickCount = 1;
        }
        _lastClickTime = now;

        int targetIndex = GetCaretIndexFromMouse(eventData);

        if (_clickCount >= 3)
        {
            // Triple click: Select the entire line
            _selectionAnchorPosition = 0;
            caretPosition = _text.Length;
            _isDragging = false;
        }
        else if (_clickCount == 2)
        {
            // Double click: Select the moused-over word
            VirtualizedIdeController.GetWordBoundaries(_text, targetIndex, out int start, out int end);
            _selectionAnchorPosition = start;
            caretPosition = end;
            _isDragging = false;
        }
        else
        {
            // Single click
            _selectionAnchorPosition = targetIndex;
            caretPosition = targetIndex;
            _isDragging = true;
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        caretPosition = GetCaretIndexFromMouse(eventData);
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
    }

    // --- Clipboard Handlers ---

    public bool HasSelection()
    {
        return _caretPosition != _selectionAnchorPosition;
    }
    private void DeleteSelectedText()
    {
        if (!HasSelection()) return;

        int min = Mathf.Min(_caretPosition, _selectionAnchorPosition);
        int max = Mathf.Max(_caretPosition, _selectionAnchorPosition);

        _text = _text.Remove(min, max - min);

        _selectionAnchorPosition = min;
        _caretPosition = min;

        UpdateTextComponent();
        onValueChanged?.Invoke(_text);
    }
    private void CopySelectedToClipboard()
    {
        if (!HasSelection()) return;
        int min = Mathf.Min(_caretPosition, _selectionAnchorPosition);
        int max = Mathf.Max(_caretPosition, _selectionAnchorPosition);

        string selectedText = _text.Substring(min, max - min);
        GUIUtility.systemCopyBuffer = selectedText;

#if UNITY_WEBGL && !UNITY_EDITOR
    try { CopyToClipboardWebGL(selectedText); } catch { } // <-- Added native copy call fallback
#endif
    }
    public void PasteText(string clipboardText)
    {
        if (string.IsNullOrEmpty(clipboardText)) return;

        if (HasSelection()) DeleteSelectedText();

        _text = _text.Insert(caretPosition, clipboardText);
        _selectionAnchorPosition = caretPosition + clipboardText.Length;
        caretPosition = caretPosition + clipboardText.Length;

        UpdateTextComponent();
        onValueChanged?.Invoke(_text);
    }

    // --- API Methods for Controller ---

    public void SetTextWithoutNotify(string newText)
    {
        _text = newText ?? "";
        _selectionAnchorPosition = 0;
        _caretPosition = 0;
        UpdateTextComponent();
    }
    public void Select()
    {
        isFocused = true;
        ResetCaretBlink();
    }
    public void ActivateInputField()
    {
        Select();
    }
    public void DeactivateInputField()
    {
        isFocused = false;
        if (caretImage != null) caretImage.enabled = false;
        if (selectionHighlightRect != null) selectionHighlightRect.gameObject.SetActive(false);
        onEndEdit?.Invoke(_text);
    }

    // --- Rendering & Geometry ---

    private void UpdateTextComponent()
    {
        if (textComponent != null)
        {
            textComponent.text = _text;
            UpdateCaretVisuals();
        }
    }
    private void UpdateCaretVisuals()
    {
        if (textComponent == null) return;

        textComponent.ForceMeshUpdate();
        var textInfo = textComponent.textInfo;

        // 1. Update Blinking Caret Rect position
        if (caretRect != null)
        {
            float caretX = 0f;

            if (textInfo.characterCount > 0)
            {
                if (_caretPosition > 0)
                {
                    int charIndex = Mathf.Min(_caretPosition - 1, textInfo.characterCount - 1);
                    caretX = textInfo.characterInfo[charIndex].bottomRight.x;
                }
                else
                {
                    // Perfectly hug the left edge of the first letter if at index 0
                    caretX = textInfo.characterInfo[0].bottomLeft.x;
                }
            }

            caretRect.anchoredPosition = new Vector2(caretX, 0f);

            if (caretImage != null) caretImage.enabled = !HasSelection() && _caretVisible;
        }

        // 2. Update Selection Highlight Rect position & size
        if (selectionHighlightRect != null)
        {
            if (!HasSelection() || !isFocused)
            {
                selectionHighlightRect.gameObject.SetActive(false);
            }
            else
            {
                selectionHighlightRect.gameObject.SetActive(true);

                int start = Mathf.Min(_caretPosition, _selectionAnchorPosition);
                int end = Mathf.Max(_caretPosition, _selectionAnchorPosition);

                float startX = 0f;
                float endX = 0f;

                if (textInfo.characterCount > 0)
                {
                    if (start > 0)
                    {
                        int startCharIndex = Mathf.Min(start - 1, textInfo.characterCount - 1);
                        startX = textInfo.characterInfo[startCharIndex].bottomRight.x;
                    }
                    else
                    {
                        startX = textInfo.characterInfo[0].bottomLeft.x;
                    }

                    if (end > 0)
                    {
                        int endCharIndex = Mathf.Min(end - 1, textInfo.characterCount - 1);
                        endX = textInfo.characterInfo[endCharIndex].bottomRight.x;
                    }
                }

                selectionHighlightRect.anchoredPosition = new Vector2(startX, 0);
                selectionHighlightRect.sizeDelta = new Vector2(Mathf.Max(2f, endX - startX), selectionHighlightRect.sizeDelta.y);
            }
        }
        UpdateWebGLCacheFromSelection();
    }
    private void ResetCaretBlink()
    {
        _blinkTimer = 0f;
        _caretVisible = true;
        if (caretImage != null) caretImage.enabled = !HasSelection() && _caretVisible;
    }

    private void UpdateWebGLCacheFromSelection()
    {
        string currentSelection = "";
        if (HasSelection())
        {
            int min = Mathf.Min(_caretPosition, _selectionAnchorPosition);
            int max = Mathf.Max(_caretPosition, _selectionAnchorPosition);
            currentSelection = _text.Substring(min, max - min);
        }

        if (_lastCachedSelection != currentSelection)
        {
            _lastCachedSelection = currentSelection;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { UpdateWebGLSelectionCache(currentSelection); } catch { }
#endif
        }
    }



    // Click Tracking

    public void RecordExternalClick()
    {
        float now = Time.unscaledTime;
        if (now - _lastClickTime < DOUBLE_CLICK_TIME)
        {
            _clickCount++;
        }
        else
        {
            _clickCount = 1;
        }
        _lastClickTime = now;
    }

    public int GetClickCount()
    {
        return _clickCount;
    }
}