using UnityEngine;
using UnityEngine.UI;

public class DiceFaceUIElement : MonoBehaviour
{
    [Header("UI References")]
    public Image targetFaceImage;
    public Button setFaceButton;

    /// <summary>
    /// Clean method to update the visual state of this button from the Mod Manager.
    /// </summary>
    public void SetIcon(Sprite sprite)
    {
        if (targetFaceImage == null) return;

        if (sprite != null)
        {
            targetFaceImage.sprite = sprite;
            targetFaceImage.color = Color.white;
        }
        else
        {
            targetFaceImage.sprite = null;
            // Faded placeholder look when nothing is set
            targetFaceImage.color = new Color(1f, 1f, 1f, 0.2f);
        }
    }
}