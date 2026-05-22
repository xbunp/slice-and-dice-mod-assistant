using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(3, 10)]
    public string content = "";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(content))
        {
            TooltipSystem.Show(content);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Hide();
    }

    private void OnDisable()
    {
        // Prevents the tooltip from getting stuck open if the element is disabled while hovered
        TooltipSystem.Hide();
    }
}