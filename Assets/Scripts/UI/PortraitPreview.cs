using System;
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

    public Button leftButton, middleButton, rightButton, rightmostButton, topButton, bottomButton;

    // Event invoked when a face button is clicked. 
    // The integer parameter corresponds to the tab indices: Left (1), Middle (2), Top (3), Bottom (4), Right (5), Rightmost (6).
    public event Action<int> OnFaceSelected;

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
        CloneMaterial(portrait); // Fix: Clone the main portrait material to protect its HSV state

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

        SetupButtonListeners();
    }

    private void SetupButtonListeners()
    {
        // Bind buttons to invoke the event with the corresponding index from the tabNames list
        if (leftButton != null) leftButton.onClick.AddListener(() => OnFaceSelected?.Invoke(1));
        if (middleButton != null) middleButton.onClick.AddListener(() => OnFaceSelected?.Invoke(2));
        if (topButton != null) topButton.onClick.AddListener(() => OnFaceSelected?.Invoke(3));
        if (bottomButton != null) bottomButton.onClick.AddListener(() => OnFaceSelected?.Invoke(4));
        if (rightButton != null) rightButton.onClick.AddListener(() => OnFaceSelected?.Invoke(5));
        if (rightmostButton != null) rightmostButton.onClick.AddListener(() => OnFaceSelected?.Invoke(6));
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent potential memory leaks
        if (leftButton != null) leftButton.onClick.RemoveAllListeners();
        if (middleButton != null) middleButton.onClick.RemoveAllListeners();
        if (topButton != null) topButton.onClick.RemoveAllListeners();
        if (bottomButton != null) bottomButton.onClick.RemoveAllListeners();
        if (rightButton != null) rightButton.onClick.RemoveAllListeners();
        if (rightmostButton != null) rightmostButton.onClick.RemoveAllListeners();
    }

    private void CloneMaterial(Image img)
    {
        if (img != null && img.material != null)
        {
            img.material = Instantiate(img.material);
        }
    }

    // UPDATED SIGNATURE: Added pipImage parameter to link dice with its respective pip display
    public void SetIcon(Image uiImage, Image pipImage, string prefix, int index, int h = 0, int s = 0, int v = 0, int pips = 0)
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

    internal void SetPortraitHSV(int h, int s, int v)
    {
        if (portrait.material != null)
        {
            portrait.material.SetFloat("_Hue", h);
            portrait.material.SetFloat("_Saturation", s);
            portrait.material.SetFloat("_Value", v);
        }
    }
}