using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem; // Assumes Input System package based on Pointer.current, fallbacks handled

namespace ModEditor
{
    public class ContextMenuTrigger : MonoBehaviour, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private FilteredDropdown dropdownPrefab;
        [SerializeField] private RectTransform canvasRectTransform;

        [Header("Settings")]
        [SerializeField] private float padding = 50f;

        private FilteredDropdown _spawnedDropdown;
        private GameObject _activeDropdownList;
        private bool _isInitialized;
        private bool _hasFoundList;

        // Callback event when a block is successfully selected from the dropdown
        public Action<string> OnBlockSelected;
        private List<string> _availableBlocks = new List<string>();

        /// <summary>
        /// Configure the contextual block menu options
        /// </summary>
        public void Initialize(RectTransform canvasRt, FilteredDropdown prefab, List<string> blockOptions, Action<string> onBlockSelectedCallback)
        {
            canvasRectTransform = canvasRt;
            dropdownPrefab = prefab;
            _availableBlocks = blockOptions;
            OnBlockSelected = onBlockSelectedCallback;
        }

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

            // Populate from configured mod block list
            _spawnedDropdown.options.Clear();
            foreach (var block in _availableBlocks)
            {
                _spawnedDropdown.options.Add(new TMP_Dropdown.OptionData(block));
            }

            // Capture dropdown selection index
            _spawnedDropdown.onValueChanged.AddListener(OnDropdownValueSelected);

            StartCoroutine(ShowDropdownDeferred(_spawnedDropdown));
        }

        private void OnDropdownValueSelected(int index)
        {
            if (_spawnedDropdown == null || index < 0 || index >= _spawnedDropdown.options.Count) return;

            string selectedBlockType = _spawnedDropdown.options[index].text;
            OnBlockSelected?.Invoke(selectedBlockType);

            DismissDropdown();
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

            if (_hasFoundList && (_activeDropdownList == null || _activeDropdownList.Equals(null)))
            {
                DismissDropdown();
                return;
            }

            // Fallback for input detection systems
            Vector2 mousePosition = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Pointer.current != null)
                mousePosition = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
            else
                mousePosition = (Vector2)Input.mousePosition;
#else
            mousePosition = (Vector2)Input.mousePosition;
#endif

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

            RectTransform baseRect = _spawnedDropdown.GetComponent<RectTransform>();
            Rect baseScreenRect = GetScreenRect(baseRect, cam);
            Rect paddedBaseRect = GetPaddedRect(baseScreenRect, padding);

            if (paddedBaseRect.Contains(mousePosition)) return true;

            if (_activeDropdownList != null)
            {
                RectTransform listRect = _activeDropdownList.GetComponent<RectTransform>();
                Rect listScreenRect = GetScreenRect(listRect, cam);
                Rect paddedListRect = GetPaddedRect(listScreenRect, padding);

                if (paddedListRect.Contains(mousePosition)) return true;
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
                _spawnedDropdown.onValueChanged.RemoveListener(OnDropdownValueSelected);
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
}