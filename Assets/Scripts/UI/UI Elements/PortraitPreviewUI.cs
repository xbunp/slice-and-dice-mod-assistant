using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PortraitPreviewUI : MonoBehaviour
{
    [System.Serializable]
    public struct SlotUI
    {
        public Image background;
        public Image pips;
        public Button button;
    }

    [Header("Character Info")]
    public Image backdrop;
    public Image portrait;
    public TMP_Text myName;
    public TMP_Text hp;
    public TMP_Text tier;

    [Header("Slots")]
    public SlotUI left;
    public SlotUI middle;
    public SlotUI right;
    public SlotUI rightmost;
    public SlotUI top;
    public SlotUI bottom;

    public event Action<int> OnFaceSelected;
    private Dictionary<string, List<Sprite>> _spriteGroups;
    [SerializeField] private Sprite[] pipsprites;
    public string DefaultBasePrefix { get; set; } = "bas";
    public Func<string> GetBasePrefixDelegate { get; set; }

    private void Awake()
    {
        _spriteGroups = new Dictionary<string, List<Sprite>>();

        CloneMaterial(left.background);
        CloneMaterial(middle.background);
        CloneMaterial(right.background);
        CloneMaterial(rightmost.background);
        CloneMaterial(top.background);
        CloneMaterial(bottom.background);
        CloneMaterial(portrait);

        Sprite[] allSprites = SpriteCache.GetCommunitySprites();

        foreach (var sprite in allSprites)
        {
            if (sprite != null && sprite.name.Length >= 3)
            {
                string prefix = sprite.name.Substring(0, 3);
                if (!_spriteGroups.ContainsKey(prefix))
                    _spriteGroups[prefix] = new List<Sprite>();

                _spriteGroups[prefix].Add(sprite);
            }
        }

        SetupButtonListeners();
    }

    private void SetupButtonListeners()
    {
        if (left.button != null) left.button.onClick.AddListener(() => OnFaceSelected?.Invoke(1));
        if (middle.button != null) middle.button.onClick.AddListener(() => OnFaceSelected?.Invoke(2));
        if (top.button != null) top.button.onClick.AddListener(() => OnFaceSelected?.Invoke(3));
        if (bottom.button != null) bottom.button.onClick.AddListener(() => OnFaceSelected?.Invoke(4));
        if (right.button != null) right.button.onClick.AddListener(() => OnFaceSelected?.Invoke(5));
        if (rightmost.button != null) rightmost.button.onClick.AddListener(() => OnFaceSelected?.Invoke(6));
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
            img.material = Instantiate(img.material);
        }
    }

    private SlotUI GetSlotByIndex(int index)
    {
        return index switch
        {
            0 => left,
            1 => middle,
            2 => top,
            3 => bottom,
            4 => right,
            _ => rightmost
        };
    }

    private void SetPipSprite(Image pipImage, int pips)
    {
        if (pipImage == null) return;

        if (pipsprites == null || pipsprites.Length == 0)
        {
            Debug.LogWarning("Pipsprites array is empty or unassigned.");
            return;
        }

        if (pips <= 0)
        {
            pipImage.enabled = false;
            return;
        }

        pipImage.enabled = true;

        int targetIndex = Mathf.Clamp(pips - 1, 0, pipsprites.Length - 1);
        pipImage.sprite = pipsprites[targetIndex];
    }

    public void SetHeroColor(Color color)
    {
        if (backdrop != null) backdrop.color = color;
        if (tier != null) tier.color = color;
        if (myName != null) myName.color = color;
    }

    public void SetNameText(string text)
    {
        if (myName != null) myName.text = text;
    }

    public void SetHPText(string text)
    {
        if (hp != null) hp.text = text;
    }

    public void SetTierText(string text)
    {
        if (tier != null) tier.text = text;
    }

    internal void SetPortraitHSV(int h, int s, int v)
    {
        if (portrait != null && portrait.material != null)
        {
            portrait.material.SetFloat("_Hue", h);
            portrait.material.SetFloat("_Saturation", s);
            portrait.material.SetFloat("_Value", v);
        }
    }

    public void SetSlotIcon(int index, string facadeID, int effectID, string facadeColor, int pips, string basePrefix = null)
    {
        Debug.Log($"[DEBUG] [PortraitPreviewUI.SetSlotIcon] Entered for slot {index}. params -> facadeID: '{facadeID}' | effectID: {effectID} | basePrefix: '{basePrefix}'");

        SlotUI slot = GetSlotByIndex(index);
        if (slot.background == null)
        {
            Debug.LogError($"[DEBUG] [PortraitPreviewUI.SetSlotIcon] Slot background Image component is NULL at index {index}!");
            return;
        }

        int h = 0, s = 0, v = 0;
        string[] hsv = (facadeColor ?? "").Split(':');
        if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
        if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS; // Matches previous index assignment
        if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

        Sprite targetSprite = null;

        // 1. Resolve facade
        if (!string.IsNullOrWhiteSpace(facadeID))
        {
            targetSprite = EntityUIHelpers.GetFacadeSprite(facadeID);
        }

        // 2. Fall back to base
        if (targetSprite == null)
        {
            h = 0; s = 0; v = 0;

            string resolvedPrefix = basePrefix ?? ((GetBasePrefixDelegate != null) ? GetBasePrefixDelegate() : DefaultBasePrefix);
            string baseShorthand = $"{resolvedPrefix}{effectID}";

            targetSprite = EntityUIHelpers.GetFacadeSprite(baseShorthand);

            if (targetSprite == null && resolvedPrefix.Equals("bas", StringComparison.OrdinalIgnoreCase))
            {
                targetSprite = EntityUIHelpers.GetBaseSprite(effectID);
            }
        }

        // 3. Render
        if (targetSprite != null)
        {
            slot.background.sprite = targetSprite;
            slot.background.enabled = true;

            if (slot.background.material != null)
            {
                slot.background.material.SetFloat("_Hue", h);
                slot.background.material.SetFloat("_Saturation", s);
                slot.background.material.SetFloat("_Value", v);
            }

            if (slot.pips != null)
            {
                SetPipSprite(slot.pips, pips);
            }
        }
        else
        {
            slot.background.sprite = null;
            slot.background.enabled = false;
        }
    }

    public bool SetIcon(Image uiImage, Image pipImage, string prefix, int index, int h = 0, int s = 0, int v = 0, int pips = 0)
    {
        if (uiImage == null) return false;

        string shorthandKey = $"{prefix}{index}";
        Sprite targetSprite = EntityUIHelpers.GetFacadeSprite(shorthandKey);

        if (targetSprite == null && prefix.Equals("bas", StringComparison.OrdinalIgnoreCase))
        {
            targetSprite = EntityUIHelpers.GetBaseSprite(index);
        }

        if (targetSprite != null)
        {
            uiImage.sprite = targetSprite;
            uiImage.enabled = true;

            if (uiImage.material != null)
            {
                uiImage.material.SetFloat("_Hue", h);
                uiImage.material.SetFloat("_Saturation", s);
                uiImage.material.SetFloat("_Value", v);
            }

            if (pipImage != null)
            {
                SetPipSprite(pipImage, pips);
            }
            return true;
        }
        else
        {
            uiImage.sprite = null;
            uiImage.enabled = false;

            if (pipImage != null)
            {
                pipImage.enabled = false;
            }
            return false;
        }
    }

    // Add this method to PortraitPreviewUI
    public void SetPortraitPHue(Phue phue)
    {
        if (portrait != null && portrait.material != null)
        {
            if (phue != null)
            {
                portrait.material.SetColor("_PColor", phue.colorStart);
                portrait.material.SetColor("_PReplaceColor", phue.colorDestination);
                portrait.material.SetFloat("_PRange", phue.colorRange);
            }
            else
            {
                // Zero it out if the phue data is cleared
                portrait.material.SetFloat("_PRange", 0);
            }
        }
    }

    // Update your existing THue method to this (so it safely zeroes out when cleared)
    public void SetPortraitTHue(Thue thue)
    {
        if (portrait != null && portrait.material != null)
        {
            if (thue != null && (thue.colorRange != 0 || thue.colorOffset != 0))
            {
                portrait.material.SetColor("_THueColor", thue.colorHex);
                portrait.material.SetFloat("_THueRange", thue.colorRange);
                portrait.material.SetFloat("_THueShift", thue.colorOffset);
            }
            else
            {
                portrait.material.SetFloat("_THueRange", 0);
                portrait.material.SetFloat("_THueShift", 0);
            }
        }
    }

    /// <summary>
    /// Safely updates the active state of character information components if they are assigned.
    /// </summary>
    public void SetCharacterInfoActive(bool active)
    {
        if (myName != null && myName.gameObject != null) myName.gameObject.SetActive(active);
        if (hp != null && hp.gameObject != null) hp.gameObject.SetActive(active);
        if (tier != null && tier.gameObject != null) tier.gameObject.SetActive(active);
        if (portrait != null && portrait.gameObject != null) portrait.gameObject.SetActive(active);
        if (backdrop != null && backdrop.gameObject != null) backdrop.gameObject.SetActive(active);
    }
}