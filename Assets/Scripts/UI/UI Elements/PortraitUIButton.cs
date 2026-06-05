using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PortraitUIButton : MonoBehaviour
{
    public TMP_Text entityName;
    public TMP_Text tier;
    public TMP_Text hp;

    public Image portrait;
    public Image background;

    public void SetColor(Color color)
    {
        background.color = color;
        tier.color = color; 
        entityName.color = color; 
    }
}
