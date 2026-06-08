using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class ContextMenuTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private FilteredDropdown dropdownPrefab;
    [SerializeField] private RectTransform canvasRectTransform;

    [Header("Settings")]
    [SerializeField] private float padding = 50f; // Padding in pixels around the UI bounds

    private FilteredDropdown _spawnedDropdown;
    private GameObject _activeDropdownList;
    private bool _isInitialized;
    private bool _hasFoundList;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            SpawnDropdown(eventData.position);
        }
    }

    private void SpawnDropdown(Vector2 screenPosition)
    {
        DismissDropdown();

        if (dropdownPrefab == null || canvasRectTransform == null)
        {
            Debug.LogWarning("ContextMenuTrigger: Missing prefab or canvas reference.", this);
            return;
        }

        _spawnedDropdown = Instantiate(dropdownPrefab, canvasRectTransform);
        _spawnedDropdown.gameObject.SetActive(true);

        RectTransform rectTransform = _spawnedDropdown.GetComponent<RectTransform>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPosition, null, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }

        // Setup test options
        _spawnedDropdown.options.Clear();
        _spawnedDropdown.options.Add(new TMP_Dropdown.OptionData("Option A"));
        _spawnedDropdown.options.Add(new TMP_Dropdown.OptionData("Option B"));
        _spawnedDropdown.options.Add(new TMP_Dropdown.OptionData("Alternative C"));

        StartCoroutine(ShowDropdownDeferred(_spawnedDropdown));
    }

    private IEnumerator ShowDropdownDeferred(FilteredDropdown dropdown)
    {
        yield return null;

        if (dropdown != null)
        {
            dropdown.Show();
            _isInitialized = true;
        }
    }

    private void Update()
    {
        if (_spawnedDropdown == null || !_isInitialized) return;

        // Locate the active dropdown list container recursively starting from the root canvas
        if (_activeDropdownList == null && !_hasFoundList)
        {
            Canvas rootCanvas = _spawnedDropdown.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                _activeDropdownList = FindChildRecursive(rootCanvas.transform, "Dropdown List");
                if (_activeDropdownList != null)
                {
                    _hasFoundList = true;
                }
            }
        }

        // If the dropdown list was open and active, but is now destroyed (e.g. selected option or clicked blocker)
        if (_hasFoundList && (_activeDropdownList == null || _activeDropdownList.Equals(null)))
        {
            DismissDropdown();
            return;
        }

        Vector2 mousePosition = Pointer.current != null ? Pointer.current.position.ReadValue() : (Vector2)Input.mousePosition;

        if (!IsMouseInActiveArea(mousePosition))
        {
            DismissDropdown();
        }
    }

    private bool IsMouseInActiveArea(Vector2 mousePosition)
    {
        if (_spawnedDropdown == null) return false;

        Canvas canvas = _spawnedDropdown.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        // 1. Check base dropdown boundaries
        RectTransform baseRect = _spawnedDropdown.GetComponent<RectTransform>();
        Rect baseScreenRect = GetScreenRect(baseRect, cam);
        Rect paddedBaseRect = GetPaddedRect(baseScreenRect, padding);

        if (paddedBaseRect.Contains(mousePosition))
        {
            return true;
        }

        // 2. Check open list boundaries
        if (_activeDropdownList != null)
        {
            RectTransform listRect = _activeDropdownList.GetComponent<RectTransform>();
            Rect listScreenRect = GetScreenRect(listRect, cam);
            Rect paddedListRect = GetPaddedRect(listScreenRect, padding);

            if (paddedListRect.Contains(mousePosition))
            {
                return true;
            }
        }

        return false;
    }

    private Rect GetScreenRect(RectTransform rectTransform, Camera cam)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float x = Mathf.Min(screenMin.x, screenMax.x);
        float y = Mathf.Min(screenMin.y, screenMax.y);
        float width = Mathf.Abs(screenMax.x - screenMin.x);
        float height = Mathf.Abs(screenMax.y - screenMin.y);

        return new Rect(x, y, width, height);
    }

    private Rect GetPaddedRect(Rect rect, float pad)
    {
        return new Rect(
            rect.x - pad,
            rect.y - pad,
            rect.width + (pad * 2f),
            rect.height + (pad * 2f)
        );
    }

    private GameObject FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;

            GameObject result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void DismissDropdown()
    {
        StopAllCoroutines();
        _isInitialized = false;
        _hasFoundList = false;

        if (_spawnedDropdown != null)
        {
            _spawnedDropdown.Hide();
            Destroy(_spawnedDropdown.gameObject);
            _spawnedDropdown = null;
        }
        _activeDropdownList = null;
    }

    private void OnDestroy()
    {
        DismissDropdown();
    }
}