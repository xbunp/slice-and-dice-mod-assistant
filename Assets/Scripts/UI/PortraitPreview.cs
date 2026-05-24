using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PortraitPreview : MonoBehaviour
{
    public Image backdrop;
    public Image portrait;

    public TMP_Text myName;
    public TMP_Text hp;
    public TMP_Text tier;

    public Image Left, Middle, Right, Rightmost, Top, Bottom;
    public Image PipsLeft, PipsMiddle, PipsRight, PipsRightmost, PipsTop, PipsBottom;

    private Dictionary<string, List<Sprite>> _spriteGroups;

    [SerializeField] private Sprite[] pipsprites;

    private void Awake()
    {
        _spriteGroups = new Dictionary<string, List<Sprite>>();

        // Create independent material instances so modifying one dice's HSV doesn't affect the others
        CloneMaterial(Left);
        CloneMaterial(Middle);
        CloneMaterial(Right);
        CloneMaterial(Rightmost);
        CloneMaterial(Top);
        CloneMaterial(Bottom);

        // Load the new atlas
        Sprite[] allSprites = SpriteCache.GetCommunitySprites(); //todo support base sprites

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
    }

    private void CloneMaterial(Image img)
    {
        if (img != null && img.material != null)
        {
            img.material = Instantiate(img.material);
        }
    }

    // UPDATED SIGNATURE: Added pipImage parameter to link dice with its respective pip display
    public void SetDiceIcon(Image uiImage, Image pipImage, string prefix, int index, int h = 0, int s = 0, int v = 0, int pips = 0)
    {
        if (uiImage == null) return;

        // Apply HSV to the material
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

        // Apply corresponding pip icon configuration
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

        // Map pip counts: values 1-10 map to indices 0-9. Values 11 or higher map to index 10.
        int targetIndex = Mathf.Clamp(pips - 1, 0, pipsprites.Length - 1);
        pipImage.sprite = pipsprites[targetIndex];
    }

    // Setters for the Backdrop UI
    public void SetHeroColor(Color color)
    {
        if (backdrop != null)
        {
            backdrop.color = color;
        }
        if (tier != null)
        {
            tier.color = color;
        }
        if (myName != null)
        {
            myName.color = color;
        }
    }

    // Setters for text fields
    public void SetNameText(string text)
    {
        if (myName != null)
        {
            myName.text = text;
        }
    }

    public void SetHPText(string text)
    {
        if (hp != null)
        {
            hp.text = text;
        }
    }

    public void SetTierText(string text)
    {
        if (tier != null)
        {
            tier.text = text;
        }
    }
}