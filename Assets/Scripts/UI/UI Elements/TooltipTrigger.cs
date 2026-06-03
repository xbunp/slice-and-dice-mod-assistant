using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [TextArea(3, 10)]
    public string content = "";

    [Header("Mobile Settings")]
    [Tooltip("How long the user must hold the button to show the tooltip.")]
    [SerializeField] private float holdDuration = 0.5f;

    private Button button;
    private Coroutine holdCoroutine;
    private bool isLongPressActive = false;
    private bool isMobileDevice = false;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Detects iOS, Android, and Mobile WebGL browsers
        isMobileDevice = Application.isMobilePlatform;
    }

    // ==========================================
    // PC / DESKTOP HOVER LOGIC
    // ==========================================

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isMobileDevice) return; // Skip hover logic on mobile

        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isMobileDevice)
        {
            // If mobile user drags finger off the button, cancel the hold
            EndHold();
        }
        else
        {
            HideTooltip();
        }
    }

    // ==========================================
    // MOBILE LONG-PRESS LOGIC
    // ==========================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isMobileDevice) return; // Skip hold logic on PC

        isLongPressActive = false;
        holdCoroutine = StartCoroutine(StartHoldTimer());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isMobileDevice) return; // Skip hold logic on PC

        EndHold();
    }

    private IEnumerator StartHoldTimer()
    {
        yield return new WaitForSeconds(holdDuration);

        isLongPressActive = true;
        ShowTooltip();

        // Disable button click interaction temporarily so releasing 
        // the press doesn't trigger the button's action.
        if (button != null)
        {
            button.interactable = false;
        }
    }

    private void EndHold()
    {
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
        }

        if (isLongPressActive)
        {
            HideTooltip();
            isLongPressActive = false;

            if (button != null)
            {
                // Re-enable the button on the next frame to bypass the release click event
                StartCoroutine(ReenableButton());
            }
        }
    }

    private IEnumerator ReenableButton()
    {
        yield return null; // Wait 1 frame
        if (button != null)
        {
            button.interactable = true;
        }
    }

    // ==========================================
    // SHARED UTILITIES
    // ==========================================

    private void ShowTooltip()
    {
        if (!string.IsNullOrEmpty(content))
        {
            TooltipSystem.Show(content);
        }
    }

    private void HideTooltip()
    {
        TooltipSystem.Hide();
    }

    private void OnDisable()
    {
        EndHold();
        HideTooltip();
    }
}