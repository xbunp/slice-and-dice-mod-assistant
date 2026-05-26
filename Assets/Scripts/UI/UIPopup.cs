using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopup : MonoBehaviour
{
    public TMP_Text text;
    public Button button;

    public void Dismiss()
    {
        Destroy(gameObject);
    }
}