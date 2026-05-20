using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DiceFaceUIElement : MonoBehaviour
{
    [Header("UI References")]
    public Image targetFaceImage;
    public Button setFaceButton;
    public IconPickerModal modalManager;

    private Sprite[] _allSprites;
    private Dictionary<int, string> _spriteNames;

    private void Start()
    {
        LoadAllIconSets();
        setFaceButton.onClick.AddListener(OnSetFaceButtonClicked);
    }

    private void LoadAllIconSets()
    {
        // Load from both atlas sheets
        Sprite[] baseSprites = Resources.LoadAll<Sprite>("base_atlas_image");
        Sprite[] commSprites = Resources.LoadAll<Sprite>("community_atlas_image");

        List<Sprite> combined = new List<Sprite>();
        combined.AddRange(baseSprites);
        combined.AddRange(commSprites);

        _allSprites = combined.ToArray();
        _spriteNames = new Dictionary<int, string>();

        for (int i = 0; i < _allSprites.Length; i++)
        {
            // Sprite names are in format "Set_ID_Name" (e.g. Sef_73_uwidad)
            // We use the full name as the "searchable name" so the search box 
            // can find it by Set, ID, or the descriptive suffix.
            _spriteNames[i] = _allSprites[i].name;
        }
    }

    private void OnSetFaceButtonClicked()
    {
        modalManager.OpenModal(_allSprites, _spriteNames, OnFaceSelected);
    }

    private void OnFaceSelected(int effectIndex, Sprite selectedSprite)
    {
        targetFaceImage.sprite = selectedSprite;

        // Example: If you need to log the specific ID:
        // string fullName = _spriteNames[effectIndex]; // "Sef_73_uwidad"
        // string id = fullName.Split('_')[1];          // "73"
        // Debug.Log($"Selected sprite {fullName} with ID {id}");
    }
}