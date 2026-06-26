using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


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

    public bool DisableDeduplication;
    public bool AllowNullSprites;

    public Func<int, Sprite, string> GetNameText;
    public Func<int, Sprite, string> GetTierText;
    public Func<int, Sprite, string> GetHPText;
    public Func<int, Sprite, Color> GetColor;

    public Vector2? CellSize;                       // Override the grid cell size (null = use default layout size)
    public Vector2? CellSpacing;                    // Override the grid cell spacing (null = use default layout spacing)
}

public class IconPickerModal : MonoBehaviour
{
    public static IconPickerModal Instance { get; private set; }

    [Header("UI References")]
    public GameObject modalPanel;
    public Transform gridContent;

    [Tooltip("Standard item button prefab")]
    public IconPickerItem iconButtonPrefab;

    [Tooltip("PortraitUIButton prefab (must also have IconPickerItem attached to it)")]
    public IconPickerItem portraitButtonPrefab;

    public TMP_InputField searchInputField;
    public Button cancelButton;

    [Header("Performance Settings")]
    public int spawnBatchSize = 250;

    private Vector2 _defaultCellSize = new Vector2(64,64);
    private Vector2 _defaultCellSpacing = new Vector2(4, 4);

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

    // Separate pools to prevent standard items and portrait items from getting mixed up
    private List<IconPickerItem> _activeIcons = new List<IconPickerItem>(500);
    private Stack<IconPickerItem> _standardPool = new Stack<IconPickerItem>(500);
    private Stack<IconPickerItem> _portraitPool = new Stack<IconPickerItem>(500);

    private LayoutGroup _layoutGroup;
    private ScrollRect _scrollRect;
    private bool _isPortraitMode = false;

    private struct CachedIcon
    {
        public int OriginalIndex;
        public Sprite Sprite;
        public string SearchName;
        public string TooltipText;
        public int Width;
        public MonsterSize? AssociatedMonsterSize;
        public bool IsValid;

        // Custom Layout Overrides
        public bool HasCustomText;
        public string NameText;
        public string TierText;
        public string HPText;
        public Color BgColor;
    }

    private CachedIcon[] _cachedIcons;

    private void Awake()
    {
        // Singleton initialization
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate instance of {nameof(IconPickerModal)} found on {gameObject.name}. Destroying the duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _layoutGroup = gridContent.GetComponent<LayoutGroup>();
        _scrollRect = gridContent.GetComponentInParent<ScrollRect>();

        cancelButton.onClick.AddListener(CloseModal);
        searchInputField.onValueChanged.AddListener(FilterIcons);
        modalPanel.SetActive(false);

        if (_layoutGroup is GridLayoutGroup gridLayout)
        {
            _defaultCellSize = gridLayout.cellSize;
            _defaultCellSpacing = gridLayout.spacing;
        }

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

    public void OpenModal(IconPickerConfig config)
    {
        _onIconSelectedCallback = config.OnSelectionMade;

        ApplyLayoutConfiguration(config);
        BuildCache(config);

        this.gameObject.SetActive(true);
        modalPanel.SetActive(true);
        transform.SetAsLastSibling();

        if (_populateRoutine != null) StopCoroutine(_populateRoutine);

        searchInputField.SetTextWithoutNotify("");

        _activeSizeFilter = -1;
        UpdateSizeButtonVisuals();

        _populateRoutine = StartCoroutine(PopulateGridRoutine(""));
    }

    private void FilterIcons(string searchQuery)
    {
        if (!modalPanel.activeSelf) return;

        if (_populateRoutine != null) StopCoroutine(_populateRoutine);
        _populateRoutine = StartCoroutine(PopulateGridRoutine(searchQuery));
    }

    private void SpawnIconUI(CachedIcon iconCache)
    {
        IconPickerItem item;

        if (iconCache.HasCustomText && portraitButtonPrefab != null)
        {
            // Use the portrait pool and prefab
            if (_portraitPool.Count > 0)
            {
                item = _portraitPool.Pop();
                item.gameObject.SetActive(true);
                item.transform.SetAsLastSibling();
            }
            else
            {
                item = Instantiate(portraitButtonPrefab, gridContent);
            }

            // Setup the unmodified IconPickerItem parts
            item.Setup(iconCache.OriginalIndex, iconCache.Sprite, iconCache.TooltipText, OnIconClicked);

            // Configure the PortraitUIButton script directly from the modal
            PortraitUIButton portraitUI = item.GetComponent<PortraitUIButton>();
            if (portraitUI != null)
            {
                if (portraitUI.portrait != null) portraitUI.portrait.sprite = iconCache.Sprite;
                if (portraitUI.entityName != null) portraitUI.entityName.text = iconCache.NameText;
                if (portraitUI.tier != null) portraitUI.tier.text = iconCache.TierText;
                if (portraitUI.hp != null) portraitUI.hp.text = iconCache.HPText;

                portraitUI.SetColor(iconCache.BgColor);
            }
        }
        else
        {
            // Use the standard pool and prefab
            if (_standardPool.Count > 0)
            {
                item = _standardPool.Pop();
                item.gameObject.SetActive(true);
                item.transform.SetAsLastSibling();
            }
            else
            {
                item = Instantiate(iconButtonPrefab, gridContent);
            }

            item.Setup(iconCache.OriginalIndex, iconCache.Sprite, iconCache.TooltipText, OnIconClicked);
        }

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

            // Put the item in the correct pool depending on which prefab it came from
            if (item.GetComponent<PortraitUIButton>() != null)
            {
                _portraitPool.Push(item);
            }
            else
            {
                _standardPool.Push(item);
            }
        }

        _activeIcons.Clear();
        if (_layoutGroup != null) _layoutGroup.enabled = true;
    }

    private MonsterSize GetExpectedMonsterSize(int filterValue)
    {
        switch (filterValue)
        {
            case 12: return MonsterSize.Tiny;
            case 16: return MonsterSize.HeroSized;
            case 22: return MonsterSize.Big;
            case 28: return MonsterSize.Huge;
            default: return MonsterSize.HeroSized;
        }
    }

    private IEnumerator PopulateGridRoutine(string searchQuery)
    {
        ReturnAllToPool();

        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
        if (_layoutGroup != null) _layoutGroup.enabled = false;

        bool isSearchEmpty = string.IsNullOrWhiteSpace(searchQuery);
        int spawnedThisFrame = 0;

        for (int i = 0; i < _cachedIcons.Length; i++)
        {
            CachedIcon icon = _cachedIcons[i];

            if (!icon.IsValid) continue;

            if (_activeSizeFilter != -1)
            {
                if (_isPortraitMode)
                {
                    MonsterSize targetMonsterSize = GetExpectedMonsterSize(_activeSizeFilter);
                    if (icon.AssociatedMonsterSize != targetMonsterSize) continue;
                }
                else
                {
                    if (icon.Width != _activeSizeFilter) continue;
                }
            }

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

        if (_layoutGroup != null) _layoutGroup.enabled = true;
        _populateRoutine = null;
    }

    public static string GetCleanLeafName(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return string.Empty;
        spriteName = spriteName.ToLower();

        if (spriteName.StartsWith("prt_"))
        {
            int firstUnderscore = spriteName.IndexOf('_');
            if (firstUnderscore != -1)
            {
                int secondUnderscore = spriteName.IndexOf('_', firstUnderscore + 1);
                if (secondUnderscore != -1)
                {
                    int lastUnderscore = spriteName.LastIndexOf('_');
                    if (lastUnderscore > secondUnderscore)
                    {
                        return spriteName.Substring(secondUnderscore + 1, lastUnderscore - secondUnderscore - 1);
                    }
                }
            }
        }

        int lastSlash = spriteName.LastIndexOf('/');
        if (lastSlash != -1) return spriteName.Substring(lastSlash + 1);

        return spriteName;
    }

    public static bool TryGetMonsterType(string spriteName, out MonsterType monster)
    {
        monster = default;
        if (HeroSpriteDatabase.SpriteToMonsterMap == null) return false;

        string cleanLeaf = GetCleanLeafName(spriteName);
        if (string.IsNullOrEmpty(cleanLeaf)) return false;

        if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(spriteName, out monster)) return true;

        foreach (var kvp in HeroSpriteDatabase.SpriteToMonsterMap)
        {
            if (GetCleanLeafName(kvp.Key) == cleanLeaf)
            {
                monster = kvp.Value;
                return true;
            }
        }
        return false;
    }

    public static bool TryGetHeroType(string spriteName, out HeroType hero)
    {
        hero = default;
        if (HeroSpriteDatabase.SpriteToHeroMap == null) return false;

        string cleanLeaf = GetCleanLeafName(spriteName);
        if (string.IsNullOrEmpty(cleanLeaf)) return false;

        if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(spriteName, out hero)) return true;

        foreach (var kvp in HeroSpriteDatabase.SpriteToHeroMap)
        {
            if (GetCleanLeafName(kvp.Key) == cleanLeaf)
            {
                hero = kvp.Value;
                return true;
            }
        }
        return false;
    }

    private MonsterSize? GetPortraitMonsterSize(Sprite sprite)
    {
        if (sprite == null) return null;
        if (TryGetMonsterType(sprite.name, out MonsterType monster)) return MonsterDatabase.GetMonsterSize(monster);
        if (TryGetHeroType(sprite.name, out HeroType _)) return MonsterSize.HeroSized;
        return null;
    }

    private void BuildCache(IconPickerConfig config)
    {
        if (config.Sprites == null) return;

        var cachedList = new List<CachedIcon>(config.Sprites.Length);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _isPortraitMode = false;

        for (int i = 0; i < config.Sprites.Length; i++)
        {
            Sprite sprite = config.Sprites[i];

            if (sprite == null && !config.AllowNullSprites) continue;

            if (!config.DisableDeduplication && sprite != null)
            {
                if (!seenNames.Add(sprite.name)) continue;
            }

            bool isValid = config.IsValid?.Invoke(i, sprite) ?? true;
            if (!isValid) continue;

            int width = sprite != null ? Mathf.RoundToInt(sprite.rect.width) : 0;
            string searchName = config.GetSearchName?.Invoke(i, sprite) ?? sprite?.name ?? "Unknown";
            string tooltipText = config.GetTooltip?.Invoke(i, sprite) ?? sprite?.name ?? "Unknown";
            MonsterSize? monsterSize = null;

            if (sprite != null && sprite.name.StartsWith("prt_", StringComparison.OrdinalIgnoreCase))
            {
                monsterSize = GetPortraitMonsterSize(sprite);
                if (monsterSize.HasValue) _isPortraitMode = true;
            }

            bool hasCustomText = config.GetNameText != null;

            cachedList.Add(new CachedIcon
            {
                OriginalIndex = i,
                Sprite = sprite,
                SearchName = searchName,
                TooltipText = tooltipText,
                Width = width,
                AssociatedMonsterSize = monsterSize,
                IsValid = true,
                HasCustomText = hasCustomText,
                NameText = hasCustomText ? config.GetNameText?.Invoke(i, sprite) : null,
                TierText = config.GetTierText?.Invoke(i, sprite),
                HPText = config.GetHPText?.Invoke(i, sprite),
                BgColor = config.GetColor != null ? config.GetColor(i, sprite) : Color.white
            });
        }

        _cachedIcons = cachedList.ToArray();
    }

    private void ApplyLayoutConfiguration(IconPickerConfig config)
    {
        if (_layoutGroup is GridLayoutGroup gridLayout)
        {
            gridLayout.cellSize = config.CellSize ?? _defaultCellSize;
            gridLayout.spacing = config.CellSpacing ?? _defaultCellSpacing;
        }
    }
}