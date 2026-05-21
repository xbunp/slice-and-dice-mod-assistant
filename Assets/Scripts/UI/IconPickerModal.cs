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

    private Action<int, Sprite> _onIconSelectedCallback;
    private Sprite[] _currentIconSet;
    private Dictionary<int, string> _currentIconNames;
    private Coroutine _populateRoutine;

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
            if (_currentIconSet[i] == null) continue;

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