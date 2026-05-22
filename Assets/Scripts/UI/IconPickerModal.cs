using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IconPickerModal : MonoBehaviour
{
    [Header("UI References")]
    public GameObject modalPanel;
    public Transform gridContent;
    public GameObject iconButtonPrefab;
    public TMP_InputField searchInputField;
    public Button cancelButton;

    [Header("Performance Settings")]
    [Tooltip("How many buttons to spawn/enable per frame. Higher = faster load, Lower = smoother framerate.")]
    public int spawnBatchSize = 250;

    [Header("Size Filter")]
    public Color selectedFilterColor = Color.gray;
    [Space]
    public Button regularSizeButton;
    public Button bigSizeButton;
    public Button hugeSizeButton;
    public Button smallSizeButton;
    private int _activeSizeFilter = -1; // -1 means no size filter active
    private Dictionary<Button, int> _sizeButtonsMap;
    private Dictionary<Button, Color> _defaultButtonColors = new Dictionary<Button, Color>();

    private Action<int, Sprite> _onIconSelectedCallback;
    private Sprite[] _currentIconSet;
    private Dictionary<int, string> _currentIconNames;
    private Coroutine _populateRoutine;

    private List<IconEntry> _activeIcons = new List<IconEntry>();
    private Stack<GameObject> _pool = new Stack<GameObject>();

    private static readonly HashSet<string> AllowedBasePrefixes = new HashSet<string>
    {
        "bas", "ite", "spe", "alp", "Lem", "eba", "pos", "Ese", "kas", "Eme", "dee", "har",
        "Spi", "Yca", "Ber", "Sef", "Leo", "Col", "OkN", "Mut", "Ric", "dar", "sym", "Sea",
        "Bal", "The", "ale", "Dog", "the", "Can", "Liz", "Che", "Ale", "dan", "PEP", "Aid",
        "Enc", "Ksy", "pow", "Fre", "Med", "Sul"
    };
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

        InitializeSizeFilters();
    }

    private void InitializeSizeFilters()
    {
        // Map buttons directly to their integer target widths
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
                if (btn.image != null)
                {
                    _defaultButtonColors[btn] = btn.image.color;
                }
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
        _currentIconSet = iconSet;
        _currentIconNames = iconNames;

        modalPanel.SetActive(true);

        // Clear any loading currently in progress
        if (_populateRoutine != null) StopCoroutine(_populateRoutine);

        searchInputField.onValueChanged.RemoveListener(FilterIcons);
        searchInputField.text = "";
        searchInputField.onValueChanged.AddListener(FilterIcons);

        // Start progressive loading
        _populateRoutine = StartCoroutine(PopulateGridRoutine(""));

        _activeSizeFilter = -1;
        UpdateSizeButtonVisuals();
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

        // FIX: Reset scroll position to the top IMMEDIATELY before we start loading.
        // This ensures the view starts at the top, but won't snap you back later if you scroll.
        ScrollRect scrollRect = gridContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
        else
        {
            RectTransform rt = gridContent.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0);
        }

        bool isSearchEmpty = string.IsNullOrWhiteSpace(searchQuery);
        int spawnedThisFrame = 0;

        for (int i = 0; i < _currentIconSet.Length; i++)
        {
            Sprite sprite = _currentIconSet[i];
            if (sprite == null) continue;

            bool isBaseAtlas = sprite.texture != null && sprite.texture.name.Contains("base_atlas");
            if (isBaseAtlas)
            {
                int underscoreIndex = sprite.name.IndexOf('_');
                string prefix = underscoreIndex > 0 ? sprite.name.Substring(0, underscoreIndex) : string.Empty;

                if (!AllowedBasePrefixes.Contains(prefix))
                {
                    continue; // Discard unneeded base atlas sprites
                }
            }

            // Size comparison check added here (Float-to-int comparison)
            if (_activeSizeFilter != -1)
            {
                int spriteWidth = Mathf.RoundToInt(sprite.rect.width);
                if (spriteWidth != _activeSizeFilter)
                {
                    continue; // Skip icons that do not match the selected size
                }
            }

            string iconName = _currentIconNames.ContainsKey(i) ? _currentIconNames[i] : $"Unknown_{i}";

            if (isSearchEmpty || iconName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SpawnIconUI(i, _currentIconSet[i], iconName);
                spawnedThisFrame++;

                // Pause here to let Unity render the frame, then resume on the next frame
                if (spawnedThisFrame >= spawnBatchSize)
                {
                    spawnedThisFrame = 0;
                    yield return null;
                }
            }
        }

        // Snapping code has been removed from this position.
        _populateRoutine = null;
    }

    private void SpawnIconUI(int index, Sprite sprite, string searchableName)
    {
        GameObject btnObj = GetFromPool();
        Button btn = btnObj.GetComponent<Button>();

        Image img = btnObj.transform.Find("Icon") != null ?
                    btnObj.transform.Find("Icon").GetComponent<Image>() :
                    btnObj.GetComponent<Image>();

        if (img != null) img.sprite = sprite;

        btn.onClick.RemoveAllListeners();
        int capturedIndex = index;
        Sprite capturedSprite = sprite;
        btn.onClick.AddListener(() => OnIconClicked(capturedIndex, capturedSprite));

        _activeIcons.Add(new IconEntry { gameObject = btnObj, searchableName = searchableName });
    }

    private GameObject GetFromPool()
    {
        GameObject obj;
        if (_pool.Count > 0)
        {
            obj = _pool.Pop();
            obj.SetActive(true);
            obj.transform.SetParent(gridContent, false);
        }
        else
        {
            obj = Instantiate(iconButtonPrefab, gridContent);
        }

        obj.transform.SetAsLastSibling();
        return obj;
    }

    private void OnIconClicked(int effectIndex, Sprite selectedSprite)
    {
        _onIconSelectedCallback?.Invoke(effectIndex, selectedSprite);
        CloseModal();
    }

    public void CloseModal()
    {
        if (_populateRoutine != null) StopCoroutine(_populateRoutine);
        modalPanel.SetActive(false);
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
}