using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ReorderableZone : MonoBehaviour
{
    [Header("State (Read Only)")]
    public List<ReorderableItem> Entrants = new List<ReorderableItem>();

    private GameObject _placeholder;
    private Canvas _rootCanvas;

    private void Awake()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// The official way to add a dumb item to this managed list.
    /// </summary>
    public void AddEntrant(ReorderableItem item)
    {
        if (!Entrants.Contains(item)) Entrants.Add(item);
        item.CurrentZone = this;
        item.transform.SetParent(this.transform, false);
    }

    /// <summary>
    /// The official way to remove an item from this list.
    /// </summary>
    public void RemoveEntrant(ReorderableItem item)
    {
        Entrants.Remove(item);
        if (item.CurrentZone == this) item.CurrentZone = null;
    }

    // --- BRAIN LOGIC FIRED BY DUMB ITEMS ---

    public void HandleBeginDrag(ReorderableItem item, PointerEventData eventData)
    {
        // 1. Controller creates the spatial placeholder
        _placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(LayoutElement));
        _placeholder.transform.SetParent(this.transform, false);
        _placeholder.transform.SetSiblingIndex(item.transform.GetSiblingIndex());

        // Match sizing so the layout doesn't collapse
        LayoutElement itemLayout = item.GetComponent<LayoutElement>();
        LayoutElement phLayout = _placeholder.GetComponent<LayoutElement>();
        if (itemLayout != null)
        {
            phLayout.preferredWidth = itemLayout.preferredWidth;
            phLayout.preferredHeight = itemLayout.preferredHeight;
        }
        else
        {
            _placeholder.GetComponent<RectTransform>().sizeDelta = item.Rect.sizeDelta;
        }

        // 2. Controller physically pops the item out of the layout so the mouse can move it
        item.transform.SetParent(_rootCanvas.transform, true);
        item.transform.SetAsLastSibling();

        item.CanvasGroup.blocksRaycasts = false; // Let rays pass through to find zones
        item.CanvasGroup.alpha = 0.6f;
    }

    public void HandleDrag(ReorderableItem item, PointerEventData eventData)
    {
        // Controller moves the item
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(item.Rect, eventData.position, eventData.pressEventCamera, out Vector3 worldPoint))
        {
            item.transform.position = worldPoint;
        }

        // Controller checks if the item has been dragged into a DIFFERENT Zone
        ReorderableZone hoveredZone = GetZoneUnderMouse(eventData);

        if (hoveredZone != null && hoveredZone != this)
        {
            TransferItemToNewZone(item, hoveredZone);
            // Pass the drag execution context to the new zone
            hoveredZone.HandleDrag(item, eventData);
            return;
        }

        // Math to figure out where the placeholder should be vertically
        UpdatePlaceholderIndex(item);
    }

    public void HandleEndDrag(ReorderableItem item, PointerEventData eventData)
    {
        item.CanvasGroup.blocksRaycasts = true;
        item.CanvasGroup.alpha = 1f;

        // Controller snaps the item back into its designated slot
        item.transform.SetParent(this.transform, false);
        item.transform.SetSiblingIndex(_placeholder.transform.GetSiblingIndex());

        Destroy(_placeholder);

        // Re-sync the array to match the actual layout hierarchy
        SyncEntrantsOrder();
    }

    // --- INTERNAL ZONE MANAGEMENT ---

    private void TransferItemToNewZone(ReorderableItem item, ReorderableZone newZone)
    {
        RemoveEntrant(item);
        newZone.Entrants.Add(item);
        item.CurrentZone = newZone;

        // Hand off the placeholder to the new zone's control
        _placeholder.transform.SetParent(newZone.transform, false);
        newZone.TakeoverPlaceholder(_placeholder);
        _placeholder = null;
    }

    public void TakeoverPlaceholder(GameObject incomingPlaceholder)
    {
        _placeholder = incomingPlaceholder;
    }

    private void UpdatePlaceholderIndex(ReorderableItem item)
    {
        int newIndex = transform.childCount;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == _placeholder.transform || child == item.transform) continue;

            if (item.transform.position.y > child.position.y)
            {
                newIndex = i;
                if (_placeholder.transform.parent == transform && _placeholder.transform.GetSiblingIndex() < newIndex)
                    newIndex--;
                break;
            }
        }
        _placeholder.transform.SetSiblingIndex(newIndex);
    }

    private ReorderableZone GetZoneUnderMouse(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var hit in results)
        {
            ReorderableZone zone = hit.gameObject.GetComponentInParent<ReorderableZone>();
            // Prevent dropping a parent zone into one of its own nested child zones
            if (zone != null && !zone.transform.IsChildOf(this.transform))
                return zone;
        }
        return null;
    }

    public void SyncEntrantsOrder()
    {
        Entrants.Clear();
        foreach (Transform child in transform)
        {
            ReorderableItem item = child.GetComponent<ReorderableItem>();
            if (item != null) Entrants.Add(item);
        }
    }
}

[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public abstract class ReorderableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // The item only knows who owns it. It makes no decisions.
    public ReorderableZone CurrentZone { get; set; }
    public RectTransform Rect { get; protected set; }
    public CanvasGroup CanvasGroup { get; protected set; }

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    // Blindly forward all Unity interaction events to the Brain (The Zone)
    public virtual void OnBeginDrag(PointerEventData eventData) => CurrentZone?.HandleBeginDrag(this, eventData);
    public virtual void OnDrag(PointerEventData eventData) => CurrentZone?.HandleDrag(this, eventData);
    public virtual void OnEndDrag(PointerEventData eventData) => CurrentZone?.HandleEndDrag(this, eventData);
}
