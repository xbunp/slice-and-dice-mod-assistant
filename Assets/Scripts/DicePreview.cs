using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public class DicePreview : MonoBehaviour
{
    public Image Left, Middle, Right, Rightmost, Top, Bottom;
    private Dictionary<string, List<Sprite>> _spriteGroups;

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

    // UPDATED SIGNATURE to accept h, s, and v
    public void SetDiceIcon(Image uiImage, string prefix, int index, int h = 0, int s = 0, int v = 0)
    {
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
    }
}