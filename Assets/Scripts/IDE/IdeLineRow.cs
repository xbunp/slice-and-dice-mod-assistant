using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Added for Image

public class IdeLineRow : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private TextMeshProUGUI lineNumberText;
    [SerializeField] private TextMeshProUGUI codeContentText;
    [SerializeField] private Image selectionHighlightBox; // Assign your semi-transparent blue image here

    private int _lineIndex = -1;
    private VirtualizedIdeController _controller;
    private RectTransform _rectTransform;

    public int CurrentLineIndex => _lineIndex;
    public TextMeshProUGUI CodeTextComponent => codeContentText;

    public void Initialize(int poolId, VirtualizedIdeController controller)
    {
        _controller = controller;
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0, 1);
        _rectTransform.anchorMax = new Vector2(1, 1);
        _rectTransform.pivot = new Vector2(0, 1);

        // PROACTIVE FIX: Force the text component to use a Left-Center pivot 
        // so local X coordinates are always positive.
        if (codeContentText != null)
        {
            codeContentText.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        // Ensure highlight box is anchored Left-Stretch
        if (selectionHighlightBox != null)
        {
            RectTransform hlRt = selectionHighlightBox.rectTransform;
            hlRt.anchorMin = new Vector2(0, 0);
            hlRt.anchorMax = new Vector2(0, 1);
            hlRt.pivot = new Vector2(0, 0.5f);
        }
    }

    public void SetRowPosition(int lineIndex, float lineHeight)
    {
        _lineIndex = lineIndex;
        _rectTransform.anchoredPosition = new Vector2(0f, -lineIndex * lineHeight);
        _rectTransform.sizeDelta = new Vector2(0f, lineHeight);
    }

    // UPDATED to receive highlight coordinates
    public void UpdateRowDisplay(int lineIndex, string highlightedText, bool isEditingThisRow, int highlightStartChar = -1, int highlightEndChar = -1)
    {
        lineNumberText.text = (lineIndex + 1).ToString();
        codeContentText.text = isEditingThisRow ? string.Empty : highlightedText;

        // Process Virtualized Highlight
        if (highlightStartChar == -1 || highlightEndChar == -1 || isEditingThisRow)
        {
            selectionHighlightBox.gameObject.SetActive(false);
        }
        else
        {
            codeContentText.ForceMeshUpdate(); // Ensure geometry exists to measure
            var textInfo = codeContentText.textInfo;

            float startX = 0f;
            float endX = 0f;

            int charCount = textInfo.characterCount;
            if (charCount > 0)
            {
                int s = Mathf.Clamp(highlightStartChar, 0, charCount);
                int e = Mathf.Clamp(highlightEndChar, 0, charCount);

                // If no characters on this line are within the selection range, hide the highlight
                if (s == e)
                {
                    selectionHighlightBox.gameObject.SetActive(false);
                    return;
                }

                startX = (s > 0) ? textInfo.characterInfo[s - 1].bottomRight.x : textInfo.characterInfo[0].bottomLeft.x;
                endX = (e > 0) ? textInfo.characterInfo[e - 1].bottomRight.x : textInfo.characterInfo[0].bottomLeft.x;
            }

            selectionHighlightBox.gameObject.SetActive(true);

            // Apply physical padding offsets
            float leftOffset = codeContentText.rectTransform.offsetMin.x;
            RectTransform hlRt = selectionHighlightBox.rectTransform;

            hlRt.anchoredPosition = new Vector2(leftOffset + startX, 0);
            hlRt.sizeDelta = new Vector2(Mathf.Max(5f, endX - startX), hlRt.sizeDelta.y); // At least 5px wide to show empty line selections
        }
    }
    // --- NEW: Drag and Select Handlers ---
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_controller != null && _lineIndex != -1)
            _controller.OnRowPointerDown(_lineIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_controller != null)
            _controller.OnRowDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_controller != null)
            _controller.OnRowPointerUp(eventData);
    }
}