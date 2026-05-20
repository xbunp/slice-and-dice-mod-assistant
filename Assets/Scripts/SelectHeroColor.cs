using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HeroColors;

public class SelectHeroColor : MonoBehaviour
{
    public TMP_Dropdown colorDropdown;
    public Image BG;

    void Start()
    {
        colorDropdown.ClearOptions();

        // Populate the dropdown with the enum names automatically
        string[] names = Enum.GetNames(typeof(ColorOption));
        colorDropdown.AddOptions(new System.Collections.Generic.List<string>(names));
    }

    // Call this when the dropdown value changes
    public void OnDropdownChanged(int index)
    {
        ColorOption selected = (ColorOption)index;
        Debug.Log("Selected: " + selected.ToString());
        Debug.Log("Color Code: " + HeroColors.GetCode(selected));

        // If you have a sprite to change:
        // mySpriteRenderer.color = ColorSystem.GetColor(selected);
        BG.color = HeroColors.GetColor(selected);
    }
}
