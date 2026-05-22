using UnityEngine;

public class TooltipSystem : MonoBehaviour
{
    private static TooltipSystem instance;

    public Tooltip tooltip;
    public Vector2 offset = new Vector2(0, -25);

    private void Awake()
    {
        // Establish singleton instance
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Ensure the tooltip is hidden on start
        if (tooltip != null)
        {
            tooltip.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Tooltip reference is missing on the TooltipSystem component in the Inspector.", this);
        }
    }

    private void Update()
    {
        if (tooltip != null && tooltip.gameObject.activeSelf)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 newPos = mousePos + offset;

        // Use the safe public RectTransform property
        float width = tooltip.RectTransform.rect.width;
        float height = tooltip.RectTransform.rect.height;

        // Clamp horizontally
        if (newPos.x < 0)
        {
            newPos.x = 0;
        }
        else if (newPos.x + width > Screen.width)
        {
            newPos.x = Screen.width - width;
        }

        // Clamp vertically
        if (newPos.y - height < 0)
        {
            newPos.y = mousePos.y - offset.y; // Flip above cursor
        }
        else if (newPos.y > Screen.height)
        {
            newPos.y = Screen.height;
        }

        tooltip.transform.position = newPos;
    }

    public static void Show(string content)
    {
        if (instance == null)
        {
            // Fallback attempt to locate the system in the scene if not initialized yet
            instance = FindFirstObjectByType<TooltipSystem>();
            if (instance == null)
            {
                Debug.LogWarning("TooltipSystem instance is missing from the scene.");
                return;
            }
        }

        if (instance.tooltip == null)
        {
            Debug.LogWarning("Tooltip reference is unassigned on the TooltipSystem.", instance);
            return;
        }

        instance.tooltip.SetText(content);
        instance.tooltip.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null && instance.tooltip != null)
        {
            instance.tooltip.gameObject.SetActive(false);
        }
    }
}