using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Configuration object passed by the caller to define exactly what the modal should display.
/// </summary>
public struct IconPickerConfig
{
    public Sprite[] Sprites;

    // Delegates allow the caller to dictate logic without hardcoding it in the modal.
    public Func<int, Sprite, bool> IsValid;         // Which sprites should be included? (null = include all)
    public Func<int, Sprite, string> GetSearchName; // What string to use for the search bar? (null = Sprite.name)
    public Func<int, Sprite, string> GetTooltip;    // What tooltip to show on hover? (null = Sprite.name)

    public Action<int, Sprite> OnSelectionMade;     // Callback when the user clicks an icon
}

public class IconPickerModal : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modalPanel;
    public Transform gridContent;
    public IconPickerItem iconButtonPrefab;
    public TMP_InputField searchInputField;
    public Button cancelButton;

    [Header("Performance Settings")]
    public int spawnBatchSize = 250;

    [Header("Size Filter")]
    public Color selectedFilterColor = Color.gray;
    [Space]
    public Button regularSizeButton;
    public Button bigSizeButton;
    public Button hugeSizeButton;
    public Button smallSizeButton;

    private int _activeSizeFilter = -1;
    private Dictionary<Button, int> _sizeButtonsMap;
    private Dictionary<Button, Color> _defaultButtonColors = new Dictionary<Button, Color>();

    private Action<int, Sprite> _onIconSelectedCallback;
    private Coroutine _populateRoutine;

    // Object Pooling
    private List<IconPickerItem> _activeIcons = new List<IconPickerItem>(500);
    private Stack<IconPickerItem> _pool = new Stack<IconPickerItem>(500);

    // Cached References
    private LayoutGroup _layoutGroup;
    private ScrollRect _scrollRect;

    // Data Caching - Prevents evaluating heavy strings inside tight loops
    private struct CachedIcon
    {
        public int OriginalIndex;
        public Sprite Sprite;
        public string SearchName;
        public string TooltipText;
        public int Width;
        public bool IsValid;
    }

    private CachedIcon[] _cachedIcons;

    private void Awake()
    {
        _layoutGroup = gridContent.GetComponent<LayoutGroup>();
        _scrollRect = gridContent.GetComponentInParent<ScrollRect>();

        cancelButton.onClick.AddListener(CloseModal);
        searchInputField.onValueChanged.AddListener(FilterIcons);
        modalPanel.SetActive(false);

        InitializeSizeFilters();
    }

    private void InitializeSizeFilters()
    {
        _sizeButtonsMap = new Dictionary<Button, int>
        {
            { smallSizeButton, 12 },
            { regularSizeButton, 16 },
            { bigSizeButton, 22 },
            { hugeSizeButton, 28 }
        };

        foreach (var kvp in _sizeButtonsMap)
        {
            Button btn = kvp.Key;
            int targetSize = kvp.Value;

            if (btn != null)
            {
                if (btn.image != null) _defaultButtonColors[btn] = btn.image.color;
                btn.onClick.AddListener(() => OnSizeFilterClicked(btn, targetSize));
            }
        }
    }

    private void OnSizeFilterClicked(Button clickedButton, int targetSize)
    {
        _activeSizeFilter = (_activeSizeFilter == targetSize) ? -1 : targetSize;
        UpdateSizeButtonVisuals();
        FilterIcons(searchInputField.text);
    }

    private void UpdateSizeButtonVisuals()
    {
        foreach (var kvp in _sizeButtonsMap)
        {
            Button btn = kvp.Key;
            if (btn == null || btn.image == null) continue;

            btn.image.color = (kvp.Value == _activeSizeFilter)
                ? selectedFilterColor
                : (_defaultButtonColors.TryGetValue(btn, out Color defColor) ? defColor : Color.white);
        }
    }

    /// <summary>
    /// Opens the modal with a customized configuration.
    /// </summary>
    public void OpenModal(IconPickerConfig config)
    {
        _onIconSelectedCallback = config.OnSelectionMade;

        // Build a cache ONCE so searching and filtering is instantaneous
        BuildCache(config);

        modalPanel.SetActive(true);

        if (_populateRoutine != null) StopCoroutine(_populateRoutine);

        searchInputField.SetTextWithoutNotify(""); // Prevents triggering the event listener during setup

        _activeSizeFilter = -1;
        UpdateSizeButtonVisuals();

        _populateRoutine = StartCoroutine(PopulateGridRoutine(""));
    }

    // Process all expensive string manipulations and filtering ONCE up front
    private void BuildCache(IconPickerConfig config)
    {
        if (config.Sprites == null) return;

        _cachedIcons = new CachedIcon[config.Sprites.Length];

        for (int i = 0; i < config.Sprites.Length; i++)
        {
            Sprite sprite = config.Sprites[i];
            bool isValid = false;
            int width = -1;
            string searchName = string.Empty;
            string tooltipText = string.Empty;

            if (sprite != null)
            {
                // Fallbacks to default behaviour if the caller didn't provide delegates
                isValid = config.IsValid?.Invoke(i, sprite) ?? true;

                if (isValid)
                {
                    width = Mathf.RoundToInt(sprite.rect.width);
                    searchName = config.GetSearchName?.Invoke(i, sprite) ?? sprite.name;
                    tooltipText = config.GetTooltip?.Invoke(i, sprite) ?? sprite.name;
                }
            }

            _cachedIcons[i] = new CachedIcon
            {
                OriginalIndex = i,
                Sprite = sprite,
                SearchName = searchName,
                TooltipText = tooltipText,
                Width = width,
                IsValid = isValid
            };
        }
    }

    private void FilterIcons(string searchQuery)
    {
        if (!modalPanel.activeSelf) return;

        if (_populateRoutine != null) StopCoroutine(_populateRoutine);
        _populateRoutine = StartCoroutine(PopulateGridRoutine(searchQuery));
    }

    private IEnumerator PopulateGridRoutine(string searchQuery)
    {
        ReturnAllToPool();

        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;

        // Suspend layout rebuilding to completely eliminate layout lag while pooling
        if (_layoutGroup != null) _layoutGroup.enabled = false;

        bool isSearchEmpty = string.IsNullOrWhiteSpace(searchQuery);
        int spawnedThisFrame = 0;

        for (int i = 0; i < _cachedIcons.Length; i++)
        {
            CachedIcon icon = _cachedIcons[i];

            if (!icon.IsValid) continue;
            if (_activeSizeFilter != -1 && icon.Width != _activeSizeFilter) continue;
            if (!isSearchEmpty && icon.SearchName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;

            SpawnIconUI(icon);
            spawnedThisFrame++;

            if (spawnedThisFrame >= spawnBatchSize)
            {
                if (_layoutGroup != null) _layoutGroup.enabled = true;

                spawnedThisFrame = 0;
                yield return null;

                if (_layoutGroup != null) _layoutGroup.enabled = false;
            }
        }

        // Finalize Layout
        if (_layoutGroup != null) _layoutGroup.enabled = true;
        _populateRoutine = null;
    }

    private void SpawnIconUI(CachedIcon iconCache)
    {
        IconPickerItem item;
        if (_pool.Count > 0)
        {
            item = _pool.Pop();
            item.gameObject.SetActive(true);
            item.transform.SetAsLastSibling();
        }
        else
        {
            item = Instantiate(iconButtonPrefab, gridContent);
        }

        item.Setup(iconCache.OriginalIndex, iconCache.Sprite, iconCache.TooltipText, OnIconClicked);
        _activeIcons.Add(item);
    }

    private void OnIconClicked(int effectIndex, Sprite selectedSprite)
    {
        _onIconSelectedCallback?.Invoke(effectIndex, selectedSprite);
        CloseModal();
    }

    public void CloseModal()
    {
        if (_populateRoutine != null) StopCoroutine(_populateRoutine);

        ReturnAllToPool();

        modalPanel.SetActive(false);
    }

    private void ReturnAllToPool()
    {
        if (_layoutGroup != null) _layoutGroup.enabled = false;

        foreach (var item in _activeIcons)
        {
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }

        _activeIcons.Clear();

        if (_layoutGroup != null) _layoutGroup.enabled = true;
    }
}