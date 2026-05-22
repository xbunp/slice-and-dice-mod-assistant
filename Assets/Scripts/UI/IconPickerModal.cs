using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class IconPickerModal : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modalPanel;
    public Transform gridContent;
    public IconPickerItem iconButtonPrefab; // Use the dedicated script!
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

    private static string[] _basTooltipNames;
    private static bool _tooltipsInitialized = false;

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

    private Sprite[] _lastIconSet;
    private CachedIcon[] _cachedIcons;

    private static readonly HashSet<string> AllowedBasePrefixes = new HashSet<string>
    {
        "bas", "ite", "spe", "alp", "Lem", "eba", "pos", "Ese", "kas", "Eme", "dee", "har",
        "Spi", "Yca", "Ber", "Sef", "Leo", "Col", "OkN", "Mut", "Ric", "dar", "sym", "Sea",
        "Bal", "The", "ale", "Dog", "the", "Can", "Liz", "Che", "Ale", "dan", "PEP", "Aid",
        "Enc", "Ksy", "pow", "Fre", "Med", "Sul"
    };

    private void Awake()
    {
        _layoutGroup = gridContent.GetComponent<LayoutGroup>();
        _scrollRect = gridContent.GetComponentInParent<ScrollRect>();

        cancelButton.onClick.AddListener(CloseModal);
        searchInputField.onValueChanged.AddListener(FilterIcons);
        modalPanel.SetActive(false);

        InitializeSizeFilters();
        InitializeTooltipNames();
    }

    private void InitializeTooltipNames()
    {
        if (_tooltipsInitialized) return;

        _basTooltipNames = new string[188];
        for (int i = 0; i < _basTooltipNames.Length; i++)
        {
            _basTooltipNames[i] = $"Base Icon {i}";
        }

        foreach (var kvp in DefaultDiceData.EffectMap)
        {
            int enumIndex = (int)kvp.Value;
            if (enumIndex >= 0 && enumIndex < _basTooltipNames.Length)
            {
                _basTooltipNames[enumIndex] = kvp.Key;
            }
        }

        _tooltipsInitialized = true;
    }

    private static bool TryGetBasValue(string spriteName, out int basValue)
    {
        basValue = -1;
        if (string.IsNullOrEmpty(spriteName)) return false;

        if (spriteName.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
        {
            int startIndex = 4; // Length of "bas_"
            int endIndex = startIndex;
            while (endIndex < spriteName.Length && char.IsDigit(spriteName[endIndex]))
            {
                endIndex++;
            }

            if (endIndex > startIndex)
            {
                string numStr = spriteName.Substring(startIndex, endIndex - startIndex);
                return int.TryParse(numStr, out basValue);
            }
        }
        return false;
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

    public void OpenModal(Sprite[] iconSet, Dictionary<int, string> iconNames, Action<int, Sprite> onSelectionMade)
    {
        _onIconSelectedCallback = onSelectionMade;

        // Build a cache ONCE so searching and filtering is instantaneous
        BuildCache(iconSet, iconNames);

        modalPanel.SetActive(true);

        if (_populateRoutine != null) StopCoroutine(_populateRoutine);

        searchInputField.SetTextWithoutNotify(""); // Prevents triggering the event listener during setup

        _activeSizeFilter = -1;
        UpdateSizeButtonVisuals();

        _populateRoutine = StartCoroutine(PopulateGridRoutine(""));
    }

    // Process all expensive string manipulations ONCE up front
    private void BuildCache(Sprite[] iconSet, Dictionary<int, string> iconNames)
    {
        if (_lastIconSet == iconSet && _cachedIcons != null && _cachedIcons.Length == iconSet.Length)
            return; // Already cached this set!

        _lastIconSet = iconSet;
        _cachedIcons = new CachedIcon[iconSet.Length];

        for (int i = 0; i < iconSet.Length; i++)
        {
            Sprite sprite = iconSet[i];
            bool isValid = false;
            int width = -1;
            string searchName = iconNames.TryGetValue(i, out string name) ? name : $"Unknown_{i}";
            string tooltipText = string.Empty;

            if (sprite != null)
            {
                isValid = true;
                width = Mathf.RoundToInt(sprite.rect.width);
                tooltipText = sprite.name; // Default: Facade mode

                bool isBaseAtlas = sprite.texture != null && sprite.texture.name.Contains("base_atlas");
                if (isBaseAtlas)
                {
                    int underscoreIndex = sprite.name.IndexOf('_');
                    string prefix = underscoreIndex > 0 ? sprite.name.Substring(0, underscoreIndex) : string.Empty;

                    if (!AllowedBasePrefixes.Contains(prefix))
                    {
                        isValid = false;
                    }
                    else if (prefix.Equals("bas", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetBasValue(sprite.name, out int basVal))
                        {
                            if (basVal >= 0 && basVal < _basTooltipNames.Length)
                            {
                                tooltipText = _basTooltipNames[basVal];
                            }
                        }
                    }
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
            // Removed 'ref'. This copies the struct by value, which is very cheap.
            CachedIcon icon = _cachedIcons[i];

            if (!icon.IsValid) continue;
            if (_activeSizeFilter != -1 && icon.Width != _activeSizeFilter) continue;
            if (!isSearchEmpty && icon.SearchName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;

            SpawnIconUI(icon); // Removed 'ref' here as well
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

        // Pass the new tooltip parameter to Setup
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
        // Disabling the layout group prevents Unity from calculating a layout rebuild 
        // for EVERY single item being disabled, making closure instant.
        if (_layoutGroup != null) _layoutGroup.enabled = false;

        foreach (var item in _activeIcons)
        {
            item.gameObject.SetActive(false);
            _pool.Push(item);
        }

        _activeIcons.Clear();

        // Re-enable ready for the next opening
        if (_layoutGroup != null) _layoutGroup.enabled = true;
    }
}