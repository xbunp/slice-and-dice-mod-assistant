using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class IdeLineRow : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI lineNumberText;
    [SerializeField] private TextMeshProUGUI codeContentText;

    private int _lineIndex = -1;
    private VirtualizedIdeController _controller;
    private RectTransform _rectTransform;

    public int CurrentLineIndex => _lineIndex;

    // EXPOSE THIS: So the controller can read the source formatting
    public TextMeshProUGUI CodeTextComponent => codeContentText;

    public void Initialize(int poolId, VirtualizedIdeController controller)
    {
        _controller = controller;
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0, 1);
        _rectTransform.anchorMax = new Vector2(1, 1);
        _rectTransform.pivot = new Vector2(0, 1);
    }

    public void SetRowPosition(int lineIndex, float lineHeight)
    {
        _lineIndex = lineIndex;
        _rectTransform.anchoredPosition = new Vector2(0f, -lineIndex * lineHeight);
        _rectTransform.sizeDelta = new Vector2(_rectTransform.sizeDelta.x, lineHeight);
    }

    public void UpdateRowDisplay(int lineIndex, string highlightedText, bool isEditingThisRow)
    {
        lineNumberText.text = (lineIndex + 1).ToString();
        codeContentText.text = isEditingThisRow ? string.Empty : highlightedText;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_controller != null && _lineIndex != -1)
        {
            _controller.RequestLineEdit(_lineIndex, _rectTransform);
        }
    }
}