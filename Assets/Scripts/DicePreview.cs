using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DicePreview : MonoBehaviour
{
    public Image Left, Middle, Right, Rightmost, Top, Bottom;

    // Dictionary to hold lists of sprites keyed by their 3-letter prefix
    private Dictionary<string, List<Sprite>> _spriteGroups;

    private void Awake()
    {
        _spriteGroups = new Dictionary<string, List<Sprite>>();

        // Load the new atlas
        Sprite[] allSprites = Resources.LoadAll<Sprite>("community_atlas_image");

        // Group sprites by the first 3 characters of their name
        // Example: "bas_213_chain" -> prefix is "bas"
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

    public void SetDiceIcon(Image uiImage, string prefix, int index)
    {
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