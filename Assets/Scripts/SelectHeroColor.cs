using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SDColors;

public class SelectHeroColor : MonoBehaviour
{
    public TMP_Dropdown colorDropdown;
    public Image BG;

    void Start()
    {
        colorDropdown.ClearOptions();

        // Populate the dropdown with the enum names automatically
        string[] names = Enum.GetNames(typeof(HeroColorOption));
        colorDropdown.AddOptions(new System.Collections.Generic.List<string>(names));
    }

    // Call this when the dropdown value changes
    public void OnDropdownChanged(int index)
    {
        HeroColorOption selected = (HeroColorOption)index;
        Debug.Log("Selected: " + selected.ToString());
        Debug.Log("Color Code: " + SDColors.GetColorCode(selected));

        // If you have a sprite to change:
        // mySpriteRenderer.color = ColorSystem.GetColor(selected);
        BG.color = SDColors.GetColor(selected);
    }
}
