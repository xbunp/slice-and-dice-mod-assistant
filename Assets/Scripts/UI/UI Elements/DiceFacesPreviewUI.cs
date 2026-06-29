using System;
using UnityEngine;
using UnityEngine.UI;

public class DiceFacesPreviewUI : MonoBehaviour
{
    [System.Serializable]
    public struct SlotUI
    {
        public Image background;
        public Image pips;
        public Button button;
        public Image selectionOutline; // Assign a border graphic if desired
    }

    [Header("Slots")]
    public SlotUI left;
    public SlotUI middle;
    public SlotUI top;
    public SlotUI bottom;
    public SlotUI right;
    public SlotUI rightmost;

    [Header("Config")]
    [SerializeField] private Sprite[] pipsprites;
    public string DefaultBasePrefix { get; set; } = "bas";
    public Func<string> GetBasePrefixDelegate { get; set; }

    public event Action<int> OnFaceSelected; // Triggered with index 0 to 5

    private void Awake()
    {
        CloneMaterial(left.background);
        CloneMaterial(middle.background);
        CloneMaterial(top.background);
        CloneMaterial(bottom.background);
        CloneMaterial(right.background);
        CloneMaterial(rightmost.background);

        if (left.button != null) left.button.onClick.AddListener(() => OnFaceSelected?.Invoke(0));
        if (middle.button != null) middle.button.onClick.AddListener(() => OnFaceSelected?.Invoke(1));
        if (top.button != null) top.button.onClick.AddListener(() => OnFaceSelected?.Invoke(2));
        if (bottom.button != null) bottom.button.onClick.AddListener(() => OnFaceSelected?.Invoke(3));
        if (right.button != null) right.button.onClick.AddListener(() => OnFaceSelected?.Invoke(4));
        if (rightmost.button != null) rightmost.button.onClick.AddListener(() => OnFaceSelected?.Invoke(5));
    }

    private void OnDestroy()
    {
        if (left.button != null) left.button.onClick.RemoveAllListeners();
        if (middle.button != null) middle.button.onClick.RemoveAllListeners();
        if (top.button != null) top.button.onClick.RemoveAllListeners();
        if (bottom.button != null) bottom.button.onClick.RemoveAllListeners();
        if (right.button != null) right.button.onClick.RemoveAllListeners();
        if (rightmost.button != null) rightmost.button.onClick.RemoveAllListeners();
    }

    private void CloneMaterial(Image img)
    {
        if (img != null && img.material != null)
        {
            // Clone the material so each face can have unique colors
            img.material = Instantiate(img.material);

            // Safety check: Does this material actually support HSV?
            if (!img.material.HasProperty("_Hue"))
            {
                Debug.LogWarning($"[DiceFacesPreviewUI] The material on {img.gameObject.name} does not have a '_Hue' property! " +
                                 "HSV sliders will not work. Please assign the correct custom shader material to this Image in the Prefab.");
            }
        }
    }

    public SlotUI GetSlotByIndex(int index) => index switch { 0 => left, 1 => middle, 2 => top, 3 => bottom, 4 => right, _ => rightmost };

    public void UpdateFaceStates(int activeBitmask, int selectedFaceIndex)
    {
        for (int i = 0; i < 6; i++)
        {
            SlotUI slot = GetSlotByIndex(i);
            bool isActive = (activeBitmask & (1 << i)) != 0;
            bool isSelected = (i == selectedFaceIndex);

            Color targetColor = isActive ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            if (slot.background != null) slot.background.color = targetColor;
            if (slot.pips != null) slot.pips.color = targetColor;

            if (slot.button != null)
            {
                ColorBlock cb = slot.button.colors;
                cb.normalColor = isSelected ? Color.yellow : Color.white;
                slot.button.colors = cb;
            }
            if (slot.selectionOutline != null) slot.selectionOutline.enabled = isSelected;
        }
    }

    public void SetSlotIcon(int index, string facadeID, int effectID, string facadeColor, int pips, string basePrefix = null)
    {
        SlotUI slot = GetSlotByIndex(index);
        if (slot.background == null) return;

        SetPipSprite(slot.pips, pips);

        int h = 0, s = 0, v = 0;
        string[] hsv = (facadeColor ?? "").Split(':');
        if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
        if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS;
        if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

        // DIAGNOSTIC LOG: This will tell us if the data is arriving as 0, or if parsing fails
        Debug.Log($"[DiceFacesPreviewUI] Slot {index} | Raw FacadeColor: '{facadeColor}' | Parsed HSV: ({h}, {s}, {v})");

        Sprite targetSprite = null;

        if (slot.background.material != null)
        {
            // 1. Update the base material (stores the data)
            slot.background.material.SetFloat("_Hue", h);
            slot.background.material.SetFloat("_Saturation", s);
            slot.background.material.SetFloat("_Value", v);

            // 2. BYPASS MASK BUG: Force the active Stencil material to update immediately
            Material activeMat = slot.background.canvasRenderer.GetMaterial();
            if (activeMat != null)
            {
                activeMat.SetFloat("_Hue", h);
                activeMat.SetFloat("_Saturation", s);
                activeMat.SetFloat("_Value", v);
            }
        }

        if (!string.IsNullOrWhiteSpace(facadeID))
        {
            targetSprite = EntityUIHelpers.GetFacadeSprite(facadeID);
        }

        if (targetSprite == null)
        {
            string resolvedPrefix = basePrefix ?? ((GetBasePrefixDelegate != null) ? GetBasePrefixDelegate() : DefaultBasePrefix);
            string baseShorthand = $"{resolvedPrefix}{effectID}";

            targetSprite = EntityUIHelpers.GetFacadeSprite(baseShorthand);

            if (targetSprite == null && resolvedPrefix.Equals("bas", StringComparison.OrdinalIgnoreCase))
            {
                targetSprite = EntityUIHelpers.GetBaseSprite(effectID);
            }
        }
        if (targetSprite != null)
        {
            slot.background.sprite = targetSprite;
            slot.background.enabled = true;

            if (slot.background.material != null)
            {
                // DIAGNOSTIC LOG: Let's see if the material has the properties before we set them
                //bool hasHue = slot.background.material.HasProperty("_Hue");
                //Debug.Log($"[DiceFacesPreviewUI] Slot {index} Material Name: '{slot.background.material.name}' | Has _Hue property: {hasHue}");

                //slot.background.material.SetFloat("_Hue", h);
                //slot.background.material.SetFloat("_Saturation", s);
                //slot.background.material.SetFloat("_Value", v);
            }
        }
        
        else
        {
            slot.background.sprite = null;
            slot.background.enabled = false;
        }

    }

    private void SetPipSprite(Image pipImage, int pips)
    {
        if (pipImage == null) return;
        if (pipsprites == null || pipsprites.Length == 0 || pips <= 0) { pipImage.enabled = false; return; }
        pipImage.enabled = true;
        pipImage.sprite = pipsprites[Mathf.Clamp(pips - 1, 0, pipsprites.Length - 1)];
    }
}