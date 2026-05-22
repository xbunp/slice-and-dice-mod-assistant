using UnityEngine;
using TMPro;
using UnityEngine.UI;

[ExecuteInEditMode]
public class Tooltip : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    public LayoutElement layoutElement;
    public int characterLimit = 80;

    private RectTransform _rectTransform;
    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }
            return _rectTransform;
        }
    }

    public void SetText(string content)
    {
        if (textComponent == null)
        {
            Debug.LogError("Text Component is missing on the Tooltip script!", this);
            return;
        }

        textComponent.text = content;

        if (layoutElement != null)
        {
            int length = textComponent.text.Length;
            layoutElement.enabled = length > characterLimit;
        }
    }
}