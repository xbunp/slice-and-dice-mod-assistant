using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownWidthFitter : MonoBehaviour, IPointerDownHandler
{
    private TMP_Dropdown _dropdown;

    private void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
        EnsureChildrenStretch();
    }

    // IPointerDownHandler triggers the moment the mouse clicks DOWN.
    // TMP_Dropdown opens the list on mouse UP. 
    // This guarantees our resize finishes BEFORE the dropdown list is generated.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_dropdown.template == null) return;

        // 1. Measure text to find the width of the longest option
        float maxWidth = GetComponent<RectTransform>().rect.width; // Minimum width is the button itself
        float padding = 75f; // Space for the scrollbar and checkmark

        var itemText = _dropdown.template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (itemText != null)
        {
            float longestText = 0f;
            foreach (var option in _dropdown.options)
            {
                // Native TMPro method to accurately measure text before it renders
                float textWidth = itemText.GetPreferredValues(option.text).x;
                if (textWidth > longestText)
                {
                    longestText = textWidth;
                }
            }

            if (longestText > 0)
            {
                maxWidth = Mathf.Max(maxWidth, longestText + padding);
            }
        }

        // 2. Determine expansion direction dynamically
        RectTransform btnRt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        btnRt.GetWorldCorners(corners);
        float centerX = (corners[0].x + corners[2].x) / 2f;

        // If on the left side of the screen, expand Right (Pivot = 0). 
        // If on the right side of the screen, expand Left (Pivot = 1).
        float pivotX = (centerX < Screen.width / 2f) ? 0f : 1f;

        // 3. Apply width and pivot directly to the hidden Template
        RectTransform templateRt = _dropdown.template;
        templateRt.pivot = new Vector2(pivotX, 1f);
        templateRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth);
    }

    /// <summary>
    /// Programmatically fixes the internal anchors of the Dropdown template so 
    /// that children natively stretch to fill the width, bypassing editor restrictions.
    /// </summary>
    private void EnsureChildrenStretch()
    {
        if (_dropdown.template == null) return;

        // 1. Force the VerticalLayoutGroup on Content to control the child size
        var content = _dropdown.template.Find("Viewport/Content");
        if (content != null)
        {
            var layoutGroup = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                // Crucial: This forces the layout group to physically stretch the Item's width
                layoutGroup.childControlWidth = true;
                layoutGroup.childForceExpandWidth = true;
            }
        }

        // 2. Clear any rigid LayoutElement constraints on the Item itself
        var item = _dropdown.template.Find("Viewport/Content/Item");
        if (item != null)
        {
            var layoutElement = item.GetComponent<UnityEngine.UI.LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = -1f; // Remove any locked preferred width
                layoutElement.flexibleWidth = 1f;  // Tell it to fill all available horizontal space
            }
        }

        // 3. Ensure the basic anchors are set to stretch
        string[] pathsToStretch = {
            "Viewport",
            "Viewport/Content",
            "Viewport/Content/Item",
            "Viewport/Content/Item/Item Text"
        };

        foreach (string path in pathsToStretch)
        {
            var child = _dropdown.template.Find(path) as RectTransform;
            if (child != null)
            {
                child.anchorMin = new Vector2(0f, child.anchorMin.y);
                child.anchorMax = new Vector2(1f, child.anchorMax.y);
            }
        }
    }
}