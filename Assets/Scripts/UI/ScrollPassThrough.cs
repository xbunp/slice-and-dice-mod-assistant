using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class ScrollPassThrough : MonoBehaviour, IScrollHandler
{
    [Tooltip("The ScrollRect to forward scroll events to. If left empty, it will attempt to find one in the parent hierarchy.")]
    [SerializeField] private ScrollRect targetScrollRect;

    public ScrollRect TargetScrollRect
    {
        get => targetScrollRect;
        set => targetScrollRect = value;
    }

    private void Awake()
    {
        if (targetScrollRect == null)
        {
            targetScrollRect = GetComponentInParent<ScrollRect>();
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (targetScrollRect != null)
        {
            targetScrollRect.OnScroll(eventData);
        }
    }
}