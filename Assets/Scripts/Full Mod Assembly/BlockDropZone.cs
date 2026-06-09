using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModEditor.UI
{
    /// <summary>
    /// Identifies a VerticalLayoutGroup as a valid target for dropping code blocks.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BlockDropZone : MonoBehaviour
    {
        public RectTransform RectTransform { get; private set; }

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// Bridges the Unity UI GameObject back to our C# compiler logic.
    /// </summary>
    public class VisualBlockComponent : MonoBehaviour
    {
        public UIBlockNode UI_Node;
    }

    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DragReorderItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Transform _originalParent;
        private GameObject _placeholder;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;

        public System.Action OnDragEnded;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;

            // 1. Create Layout Placeholder
            _placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(LayoutElement));
            _placeholder.transform.SetParent(_originalParent, false);
            _placeholder.transform.SetSiblingIndex(transform.GetSiblingIndex());

            LayoutElement placeholderLayout = _placeholder.GetComponent<LayoutElement>();
            LayoutElement myLayout = GetComponent<LayoutElement>();

            if (myLayout != null)
            {
                placeholderLayout.preferredWidth = myLayout.preferredWidth;
                placeholderLayout.preferredHeight = myLayout.preferredHeight;
                placeholderLayout.minHeight = myLayout.minHeight;
            }
            else
            {
                _placeholder.GetComponent<RectTransform>().sizeDelta = _rectTransform.sizeDelta;
            }

            // 2. Detach and pop to top rendering layer
            transform.SetParent(_canvas.transform, true);
            _canvasGroup.blocksRaycasts = false; // Let raycasts pass through to find drop zones
            _canvasGroup.alpha = 0.8f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Move item
            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                transform.position = eventData.position;
            }
            else
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector3 worldPoint);
                transform.position = worldPoint;
            }

            // Raycast to find the deepest DropZone under the mouse
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            BlockDropZone validDropZone = null;
            foreach (var res in results)
            {
                BlockDropZone zone = res.gameObject.GetComponentInParent<BlockDropZone>();
                // Prevent dropping a container into its own child!
                if (zone != null && !zone.transform.IsChildOf(this.transform))
                {
                    validDropZone = zone;
                    break;
                }
            }

            if (validDropZone != null)
            {
                if (_placeholder.transform.parent != validDropZone.transform)
                {
                    _placeholder.transform.SetParent(validDropZone.transform, false);
                }
                UpdatePlaceholderPosition(validDropZone.transform);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.SetParent(_placeholder.transform.parent, false);
            transform.SetSiblingIndex(_placeholder.transform.GetSiblingIndex());

            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;

            Destroy(_placeholder);

            // Execute the bound callback directly
            OnDragEnded?.Invoke();
        }

        private void UpdatePlaceholderPosition(Transform targetParent)
        {
            int newSiblingIndex = targetParent.childCount;

            for (int i = 0; i < targetParent.childCount; i++)
            {
                Transform child = targetParent.GetChild(i);
                if (child == _placeholder.transform) continue;

                // Vertical insertion logic
                if (transform.position.y > child.position.y)
                {
                    newSiblingIndex = i;
                    if (_placeholder.transform.parent == targetParent && _placeholder.transform.GetSiblingIndex() < newSiblingIndex)
                    {
                        newSiblingIndex--;
                    }
                    break;
                }
            }

            _placeholder.transform.SetSiblingIndex(newSiblingIndex);
        }
    }
}