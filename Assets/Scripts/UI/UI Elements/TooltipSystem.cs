using UnityEngine;
using UnityEngine.UI;

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
    private Canvas parentCanvas;
    private RectTransform parentRect;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        isMobileDevice = Application.isMobilePlatform;

        if (tooltip != null)
        {
            tooltip.gameObject.SetActive(false);

            // Cache parent references for positioning calculations
            parentRect = tooltip.RectTransform.parent as RectTransform;
            parentCanvas = tooltip.GetComponentInParent<Canvas>();
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
        if (parentRect == null || parentCanvas == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 currentOffset = isMobileDevice ? mobileOffset : offset;

        // 1. Convert screen mouse position to parent local coordinate space
        // This handles Canvas scaling, camera settings, and screen resolutions automatically.
        Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, uiCamera, out Vector2 localMousePos))
        {
            return;
        }

        Vector2 targetLocalPos = localMousePos + currentOffset;

        // 2. Get tooltip dimensions in local coordinates
        float width = tooltip.RectTransform.rect.width;
        float height = tooltip.RectTransform.rect.height;

        float pivotX = tooltip.RectTransform.pivot.x;
        float pivotY = tooltip.RectTransform.pivot.y;

        // 3. Define boundaries relative to the parent UI container's local Rect
        float minX = parentRect.rect.xMin + (width * pivotX);
        float maxX = parentRect.rect.xMax - (width * (1f - pivotX));
        float minY = parentRect.rect.yMin + (height * pivotY);
        float maxY = parentRect.rect.yMax - (height * (1f - pivotY));

        // Clamp horizontally
        targetLocalPos.x = Mathf.Clamp(targetLocalPos.x, minX, maxX);

        // Clamp vertically
        if (isMobileDevice)
        {
            targetLocalPos.y = Mathf.Clamp(targetLocalPos.y, minY, maxY);
        }
        else
        {
            // Desktop flip behavior
            float bottomEdge = targetLocalPos.y - (height * pivotY);

            // If the tooltip falls below the bottom bounds of the parent container
            if (bottomEdge < parentRect.rect.yMin)
            {
                // Flip vertically relative to local mouse position
                targetLocalPos.y = localMousePos.y - currentOffset.y;
            }

            // Apply safety clamp to top/bottom limits
            targetLocalPos.y = Mathf.Clamp(targetLocalPos.y, minY, maxY);
        }

        // Apply the resolved coordinates directly to the localPosition
        tooltip.RectTransform.localPosition = targetLocalPos;
    }

    public static void Show(string content)
    {
        if (instance == null)
        {
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

        // Force layout update so rect boundaries are immediately calculated on this frame
        LayoutRebuilder.ForceRebuildLayoutImmediate(instance.tooltip.RectTransform);

        instance.tooltip.gameObject.SetActive(true);

        // Re-cache references if they were lost during hierarchy changes
        if (instance.parentRect == null || instance.parentCanvas == null)
        {
            instance.parentRect = instance.tooltip.RectTransform.parent as RectTransform;
            instance.parentCanvas = instance.tooltip.GetComponentInParent<Canvas>();
        }
    }

    public static void Hide()
    {
        if (instance != null && instance.tooltip != null)
        {
            instance.tooltip.gameObject.SetActive(false);
        }
    }
}