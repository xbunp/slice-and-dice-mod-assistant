using UnityEngine;

public class TooltipSystem : MonoBehaviour
{
    private static TooltipSystem instance;

    public Tooltip tooltip;

    [Header("Position Offsets")]
    [Tooltip("Offset used on Desktop (usually negative to place below cursor)")]
    public Vector2 offset = new Vector2(0, -30);

    [Tooltip("Offset used on Mobile/WebGL touch screens (usually positive to place above finger)")]
    public Vector2 mobileOffset = new Vector2(0, 100);

    private bool isMobileDevice;

    private void Awake()
    {
        // Establish singleton instance
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Detect if the running device is a mobile platform
        isMobileDevice = Application.isMobilePlatform;

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
        Vector2 inputPos = Input.mousePosition;

        // Use the appropriate offset based on the platform
        Vector2 currentOffset = isMobileDevice ? mobileOffset : offset;
        Vector2 newPos = inputPos + currentOffset;

        // Use the safe public RectTransform property
        float width = tooltip.RectTransform.rect.width;
        float height = tooltip.RectTransform.rect.height;

        // Clamp horizontally (applies to both PC and Mobile)
        if (newPos.x < 0)
        {
            newPos.x = 0;
        }
        else if (newPos.x + width > Screen.width)
        {
            newPos.x = Screen.width - width;
        }

        // Clamp vertically
        if (isMobileDevice)
        {
            // MOBILE LOGIC: Keep it above the finger, clamp to top of screen if it goes too high
            if (newPos.y > Screen.height)
            {
                newPos.y = Screen.height;
            }
            else if (newPos.y - height < 0)
            {
                newPos.y = height;
            }
        }
        else
        {
            // PC LOGIC: Default below-cursor logic, flip above if it hits the bottom edge
            if (newPos.y - height < 0)
            {
                newPos.y = inputPos.y - offset.y; // Flip above cursor
            }
            else if (newPos.y > Screen.height)
            {
                newPos.y = Screen.height;
            }
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