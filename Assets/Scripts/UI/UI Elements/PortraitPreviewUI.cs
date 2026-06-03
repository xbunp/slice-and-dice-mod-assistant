using System;
using System.Collections.Generic;
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
            if (sprite.name.Length >= 3)
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

    // NEW UNIFIED METHOD: Handles the switch selections, string parsing, and verification
    public void SetSlotIcon(int index, string facadeID, int effectID, string facadeColor, int pips)
    {
        // 1. Replaces the two switch blocks from your original code
        SlotUI slot = GetSlotByIndex(index);
        if (slot.background == null) return;

        // 2. Replaces the HSV string splitting and parsing
        int h = 0, s = 0, v = 0;
        string[] hsv = (facadeColor ?? "").Split(':');
        if (hsv.Length > 0 && int.TryParse(hsv[0], out int pH)) h = pH;
        if (hsv.Length > 1 && int.TryParse(hsv[1], out int pS)) s = pS;
        if (hsv.Length > 2 && int.TryParse(hsv[2], out int pV)) v = pV;

        // 3. Replaces the facadeID check and parsing logic
        if (!string.IsNullOrWhiteSpace(facadeID) && facadeID.Length >= 4)
        {
            string prefix = facadeID.Substring(0, 3);
            string idString = facadeID.Substring(3);

            if (int.TryParse(idString, out int parsedFacadeId))
            {
                SetIcon(slot.background, slot.pips, prefix, parsedFacadeId, h, s, v, pips);
                return;
            }
        }

        // 4. Fallback execution
        SetIcon(slot.background, slot.pips, "bas", effectID, h, s, v, pips);
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

    public void SetIcon(Image uiImage, Image pipImage, string prefix, int index, int h = 0, int s = 0, int v = 0, int pips = 0)
    {
        if (uiImage == null) return;

        if (uiImage.material != null)
        {
            uiImage.material.SetFloat("_Hue", h);
            uiImage.material.SetFloat("_Saturation", s);
            uiImage.material.SetFloat("_Value", v);
        }

        if (_spriteGroups.TryGetValue(prefix, out List<Sprite> sprites))
        {
            if (index >= 0 && index < sprites.Count)
            {
                uiImage.sprite = sprites[index];
            }
            else
            {
                Debug.LogWarning($"Index {index} out of bounds for prefix {prefix}");
            }
        }
        else
        {
            Debug.LogError($"Prefix {prefix} not found in sprite atlas.");
        }

        if (pipImage != null)
        {
            SetPipSprite(pipImage, pips);
        }
    }

    private void SetPipSprite(Image pipImage, int pips)
    {
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
}