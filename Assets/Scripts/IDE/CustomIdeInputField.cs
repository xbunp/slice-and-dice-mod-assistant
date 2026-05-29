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

        // --- Raw Navigation & Clipboard Manipulation ---
        bool isControlPressed = kb.ctrlKey.isPressed;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        isControlPressed |= kb.commandKey.isPressed;
#endif

        if (isControlPressed)
        {
            // Ctrl + C (Copy)
            if (kb.cKey.wasPressedThisFrame && HasSelection())
            {
                CopySelectedToClipboard();
                return;
            }
            // Ctrl + X (Cut)
            if (kb.xKey.wasPressedThisFrame && HasSelection())
            {
                CopySelectedToClipboard();
                DeleteSelectedText();
                return;
            }
            // Ctrl + V (Paste)
            if (kb.vKey.wasPressedThisFrame)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                PasteText(GUIUtility.systemCopyBuffer);
#endif
                return;
            }
            return; // Ignore other standard entries while holding control
        }

        bool isShiftPressed = kb.shiftKey.isPressed;

        if (kb.leftArrowKey.wasPressedThisFrame)
        {
            int targetCaret = caretPosition - 1;
            if (isShiftPressed)
            {
                caretPosition = targetCaret; // Drag selection edge left
            }
            else
            {
                // Collapse selection left
                if (HasSelection()) caretPosition = Mathf.Min(caretPosition, _selectionAnchorPosition);
                else caretPosition = targetCaret;

                _selectionAnchorPosition = caretPosition;
            }
        }
        else if (kb.rightArrowKey.wasPressedThisFrame)
        {
            int targetCaret = caretPosition + 1;
            if (isShiftPressed)
            {
                caretPosition = targetCaret; // Drag selection edge right
            }
            else
            {
                // Collapse selection right
                if (HasSelection()) caretPosition = Mathf.Max(caretPosition, _selectionAnchorPosition);
                else caretPosition = targetCaret;

                _selectionAnchorPosition = caretPosition;
            }
        }
        else if (kb.backspaceKey.wasPressedThisFrame)
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
        else if (kb.deleteKey.wasPressedThisFrame)
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
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
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

    public void OnPointerDown(PointerEventData eventData)
    {
        Select();

        // Pass 'null' for camera to ensure stable Canvas Overlay coordinate evaluation
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, null, out Vector2 localMousePos);

        // Pivot is now at the left, so negative X strictly means they clicked the line numbers area
        if (localMousePos.x < 0f)
        {
            _selectionAnchorPosition = 0;
            caretPosition = 0;
            return;
        }

        int clickedChar = TMP_TextUtilities.FindIntersectingCharacter(textComponent, localMousePos, null, true);
        if (clickedChar == -1) clickedChar = _text.Length;

        _selectionAnchorPosition = clickedChar;
        caretPosition = clickedChar;
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        // Pass 'null' for camera to ensure stable Canvas Overlay coordinate evaluation
        RectTransformUtility.ScreenPointToLocalPointInRectangle(textComponent.rectTransform, eventData.position, null, out Vector2 localMousePos);

        int draggedChar = TMP_TextUtilities.FindIntersectingCharacter(textComponent, localMousePos, null, true);
        if (draggedChar == -1) draggedChar = _text.Length;

        caretPosition = draggedChar;
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

        GUIUtility.systemCopyBuffer = _text.Substring(min, max - min);
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
    }
    private void ResetCaretBlink()
    {
        _blinkTimer = 0f;
        _caretVisible = true;
        if (caretImage != null) caretImage.enabled = !HasSelection() && _caretVisible;
    }
}