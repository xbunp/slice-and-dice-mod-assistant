using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming you use TextMeshPro for modern Unity UI

public class IconPickerModal : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modalPanel;
    public Transform gridContent;
    public GameObject iconButtonPrefab;
    public TMP_InputField searchInputField;
    public Button cancelButton;

    private Action<int, Sprite> _onIconSelectedCallback;

    // Pooling systems
    private List<IconEntry> _activeIcons = new List<IconEntry>();
    private Stack<GameObject> _pool = new Stack<GameObject>();

    private class IconEntry
    {
        public GameObject gameObject;
        public string searchableName;
    }

    private void Awake()
    {
        cancelButton.onClick.AddListener(CloseModal);
        searchInputField.onValueChanged.AddListener(FilterIcons);
        modalPanel.SetActive(false);
    }

    public void OpenModal(Sprite[] iconSet, Dictionary<int, string> iconNames, Action<int, Sprite> onSelectionMade)
    {
        _onIconSelectedCallback = onSelectionMade;
        modalPanel.SetActive(true);
        searchInputField.text = "";

        // Reset for new population
        ClearActiveIcons();

        for (int i = 0; i < iconSet.Length; i++)
        {
            if (iconSet[i] == null) continue;

            // Get from pool or instantiate
            GameObject btnObj = GetFromPool();
            Button btn = btnObj.GetComponent<Button>();
            Image img = btnObj.GetComponentInChildren<Image>();

            img.sprite = iconSet[i];
            string iconName = iconNames.ContainsKey(i) ? iconNames[i] : $"Unknown_{i}";

            // Clear previous listeners to prevent stacking
            btn.onClick.RemoveAllListeners();

            int capturedIndex = i;
            Sprite capturedSprite = iconSet[i];
            btn.onClick.AddListener(() => OnIconClicked(capturedIndex, capturedSprite));

            _activeIcons.Add(new IconEntry { gameObject = btnObj, searchableName = iconName });
        }
    }

    private GameObject GetFromPool()
    {
        if (_pool.Count > 0)
        {
            GameObject obj = _pool.Pop();
            obj.SetActive(true);
            obj.transform.SetParent(gridContent); // Ensure it's in the correct parent
            return obj;
        }
        return Instantiate(iconButtonPrefab, gridContent);
    }

    private void OnIconClicked(int effectIndex, Sprite selectedSprite)
    {
        _onIconSelectedCallback?.Invoke(effectIndex, selectedSprite);
        CloseModal();
    }

    public void CloseModal()
    {
        modalPanel.SetActive(false);
        // Return active icons to the pool instead of destroying them
        ReturnAllToPool();
    }

    private void ReturnAllToPool()
    {
        foreach (var entry in _activeIcons)
        {
            entry.gameObject.SetActive(false);
            _pool.Push(entry.gameObject);
        }
        _activeIcons.Clear();
    }

    private void ClearActiveIcons()
    {
        _activeIcons.Clear();
    }

    private void FilterIcons(string searchQuery)
    {
        bool isSearchEmpty = string.IsNullOrWhiteSpace(searchQuery);
        foreach (var entry in _activeIcons)
        {
            bool match = isSearchEmpty || entry.searchableName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
            entry.gameObject.SetActive(match);
        }
    }
}