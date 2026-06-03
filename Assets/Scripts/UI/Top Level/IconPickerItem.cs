using System;
using UnityEngine;
using UnityEngine.UI;

public class IconPickerItem : MonoBehaviour
{
    public Button button;
    public Image iconImage;
    public TooltipTrigger tooltipTrigger;

    private int _effectIndex;
    private Sprite _sprite;
    private Action<int, Sprite> _onSelected;

    private void Awake()
    {
        // Add listener ONCE. Zero memory allocation during runtime.
        button.onClick.AddListener(OnClick);
    }

    public void Setup(int index, Sprite sprite, Action<int, Sprite> onSelected)
    {
        _effectIndex = index;
        _sprite = sprite;
        _onSelected = onSelected;

        iconImage.sprite = sprite;
    }

    public void Setup(int index, Sprite sprite, string tooltipText, Action<int, Sprite> onSelected)
    {
        _effectIndex = index;
        _sprite = sprite;
        _onSelected = onSelected;

        iconImage.sprite = sprite;

        if (tooltipTrigger != null)
        {
            tooltipTrigger.content = tooltipText;
        }
    }

    private void OnClick()
    {
        _onSelected?.Invoke(_effectIndex, _sprite);
    }
}