using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[AddComponentMenu("UI/Filtered Dropdown", 35)]
public class FilteredDropdown : TMP_Dropdown
{
    private TMP_InputField _filterInput;
    private ScrollRect _scrollRect;

    protected override void Awake()
    {
        base.Awake();

        // 1. Clear any default options configured in the inspector
        if (options != null)
        {
            options.Clear();
        }

        // 2. Clear template text to prevent "Option A" leakage
        if (template != null)
        {
            TMP_Text[] templateTexts = template.GetComponentsInChildren<TMP_Text>(true);
            foreach (var txt in templateTexts)
            {
                if (txt != null && (txt.name == "Item Label" || txt.text == "Option A"))
                {
                    txt.text = string.Empty;
                }
            }
        }

        // 3. Clear caption placeholder if it contains "Option A"
        if (captionText != null && (captionText.text == "Option A" || string.IsNullOrEmpty(captionText.text)))
        {
            captionText.text = string.Empty;
        }
    }

    protected override GameObject CreateDropdownList(GameObject template)
    {
        GameObject dropdownList = base.CreateDropdownList(template);

        _scrollRect = dropdownList.GetComponentInChildren<ScrollRect>();
        _filterInput = dropdownList.GetComponentInChildren<TMP_InputField>();

        if (_filterInput != null)
        {
            _filterInput.text = string.Empty;
            _filterInput.onValueChanged.AddListener(OnFilterChanged);
            _filterInput.ActivateInputField();

            // Safety cleanup: If the prefab has an unwanted search placeholder text, replace it.
            if (_filterInput.placeholder != null)
            {
                TMP_Text placeholderText = _filterInput.placeholder.GetComponent<TMP_Text>();
                if (placeholderText != null)
                {
                    // If placeholder matches the unwanted prompt, overwrite it with a generic search label
                    if (placeholderText.text.IndexOf("select keyword to add", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        placeholderText.text = "Search...";
                    }
                }
            }
        }

        return dropdownList;
    }

    protected override void DestroyDropdownList(GameObject dropdownList)
    {
        if (_filterInput != null)
        {
            _filterInput.onValueChanged.RemoveListener(OnFilterChanged);
            _filterInput = null;
        }

        _scrollRect = null;
        base.DestroyDropdownList(dropdownList);
    }

    private void OnFilterChanged(string filterText)
    {
        if (_scrollRect == null || _scrollRect.content == null)
            return;

        bool isFilterEmpty = string.IsNullOrEmpty(filterText);
        Transform content = _scrollRect.content;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform itemTrans = content.GetChild(i);
            if (itemTrans == null) continue;

            // FIX: Skip the inactive original template item container! 
            // This prevents the blank row from being enabled when the filter is empty.
            if (itemTrans.name == "Item" || itemTrans.name.Contains("Template"))
                continue;

            TMP_Text label = itemTrans.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                bool matches = isFilterEmpty || label.text.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
                itemTrans.gameObject.SetActive(matches);
            }
        }

        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}