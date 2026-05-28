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
    public TextMeshProUGUI CodeTextComponent => codeContentText;

    public void Initialize(int poolId, VirtualizedIdeController controller)
    {
        _controller = controller;
        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.anchorMin = new Vector2(0, 1);
        _rectTransform.anchorMax = new Vector2(1, 1);
        _rectTransform.pivot = new Vector2(0, 1);

        // FORCE CHILD ALIGNMENT: Mathematically force text meshes to match the parent's 22px height
        ForceVerticalStretch(lineNumberText?.GetComponent<RectTransform>());
        ForceVerticalStretch(codeContentText?.GetComponent<RectTransform>());
    }

    private void ForceVerticalStretch(RectTransform rt)
    {
        if (rt == null) return;
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(rt.anchorMin.x, 0f); // Stretch to bottom
        rt.anchorMax = new Vector2(rt.anchorMax.x, 1f); // Stretch to top
        rt.offsetMin = new Vector2(rt.offsetMin.x, 0f); // Zero out bottom offset
        rt.offsetMax = new Vector2(rt.offsetMax.x, 0f); // Zero out top offset
    }

    public void SetRowPosition(int lineIndex, float lineHeight)
    {
        _lineIndex = lineIndex;
        _rectTransform.anchoredPosition = new Vector2(0f, -lineIndex * lineHeight);
        _rectTransform.sizeDelta = new Vector2(0f, lineHeight); // Ensure width is 100% stretch
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
            // Removed _rectTransform argument
            _controller.RequestLineEdit(_lineIndex);
        }
    }
}