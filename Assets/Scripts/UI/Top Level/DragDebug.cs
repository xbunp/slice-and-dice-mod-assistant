using UnityEngine;
using UnityEngine.EventSystems;

public class DragDebugHelper : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData eventData)
    {
        var rt = GetComponent<RectTransform>();
        var cg = GetComponent<CanvasGroup>();

        Debug.Log($"<color=orange>[DRAG BEGIN]</color> Card Name: {gameObject.name}\n" +
                  $"Parent Container: {(rt.parent != null ? rt.parent.name : "NULL")}\n" +
                  $"Local Scale: {rt.localScale}\n" +
                  $"Size Delta: {rt.sizeDelta}\n" +
                  $"Anchored Position: {rt.anchoredPosition}\n" +
                  $"CanvasGroup Alpha: {(cg != null ? cg.alpha.ToString() : "N/A")}\n" +
                  $"CanvasGroup BlocksRaycasts: {(cg != null ? cg.blocksRaycasts.ToString() : "N/A")}");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var rt = GetComponent<RectTransform>();
        Debug.Log($"<color=green>[DRAG END]</color> Card Name: {gameObject.name}\n" +
                  $"Parent Container: {(rt.parent != null ? rt.parent.name : "NULL")}\n" +
                  $"Local Scale: {rt.localScale}\n" +
                  $"Size Delta: {rt.sizeDelta}");
    }
}