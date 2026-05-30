using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum CellType { Input, Dropdown, Label, Button, DiceButton, Slider, ScrollView, NavigationTabs, ImagePanel, PortraitPanel, CustomImgImporter, IDEInterface }

public class GridReferences
{
    public float TotalHeight;

    public Dictionary<string, TMP_InputField> Inputs = new Dictionary<string, TMP_InputField>();
    public Dictionary<string, TMP_Dropdown> Dropdowns = new Dictionary<string, TMP_Dropdown>();
    public Dictionary<string, Button> Buttons = new Dictionary<string, Button>();
    public Dictionary<string, Slider> Sliders = new Dictionary<string, Slider>();
    public Dictionary<string, ScrollRect> ScrollViews = new Dictionary<string, ScrollRect>();
    public Dictionary<string, NavigationTabsController> NavigationTabs = new Dictionary<string, NavigationTabsController>();
    public Dictionary<string, Image> ImagePanels = new Dictionary<string, Image>();
    public Dictionary<string, PortraitPreview> PortraitPanels = new Dictionary<string, PortraitPreview>();
    public Dictionary<string, ImageReceiver> CustomImgImporter = new Dictionary<string, ImageReceiver>();
    public Dictionary<string, VirtualizedIdeController> IDEInterfaces = new Dictionary<string, VirtualizedIdeController>();
}

public class GridRowSpec
{
    public List<GridCellSpec> cells = new List<GridCellSpec>();
    public float customHeight = -1f;

    public bool isBackground = false;
    public int rowSpan = 1;

    public GridRowSpec(params GridCellSpec[] cells) { this.cells.AddRange(cells); }
    public GridRowSpec(float customHeight, params GridCellSpec[] cells)
    {
        this.cells.AddRange(cells);
        this.customHeight = customHeight;
    }
}

public class GridCellSpec
{
    public CellType type;
    public string key;
    public string labelText;
    public float widthRatio;
    public List<string> dropdownOptions = new List<string>();
    public Action<string> onStringChanged;
    public Action<int> onIntChanged;
    public Action onClicked;

    public Sprite buttonSprite;

    // Panel
    public Color panelColor = Color.white;
    public Sprite panelSprite = null;

    //Sliders
    public float sliderMin;
    public float sliderMax;
    public bool sliderWholeNumbers;
    public Action<float> onFloatChanged;

    public List<string> tabNames = new List<string>();
    public List<GameObject> tabTargetPanels = new List<GameObject>();

    public static GridCellSpec CreateInput(string key, string label, float ratio, Action<string> onChanged)
    {
        return new GridCellSpec { type = CellType.Input, key = key, labelText = label, widthRatio = ratio, onStringChanged = onChanged };
    }

    public static GridCellSpec CreateLabel(string label, float ratio)
    {
        return new GridCellSpec { type = CellType.Label, key = label, labelText = label, widthRatio = ratio };
    }

    public static GridCellSpec CreateLabel(string key, string label, float ratio)
    {
        return new GridCellSpec
        {
            type = CellType.Label,
            key = key,
            labelText = label,
            widthRatio = ratio
        };
    }

    public static GridCellSpec CreateImagePanel(string key, float ratio, Color? color = null, Sprite sprite = null)
    {
        return new GridCellSpec
        {
            type = CellType.ImagePanel,
            key = key,
            widthRatio = ratio,
            panelColor = color ?? Color.white,
            panelSprite = sprite
        };
    }

    public static GridCellSpec CreateDropdown(string key, string label, float ratio, string[] options, Action<int> onChanged)
    {
        return new GridCellSpec { type = CellType.Dropdown, key = key, labelText = label, widthRatio = ratio, dropdownOptions = new List<string>(options), onIntChanged = onChanged };
    }

    public static GridCellSpec CreateButton(string key, string label, float ratio, Action onClicked, Sprite sprite = null)
    {
        return new GridCellSpec
        {
            type = CellType.Button,
            key = key,
            labelText = label,
            widthRatio = ratio,
            onClicked = onClicked,
            buttonSprite = sprite
        };
    }

    public static GridCellSpec CreateDiceButton(string key, string label, float ratio, Action onClicked)
    {
        return new GridCellSpec
        {
            type = CellType.DiceButton,
            key = key,
            labelText = label,
            widthRatio = ratio,
            onClicked = onClicked
        };
    }

    public static GridCellSpec CreateSlider(string key, float min, float max, bool wholeNumbers, float ratio, Action<float> onChanged)
    {
        return new GridCellSpec
        {
            type = CellType.Slider,
            key = key,
            sliderMin = min,
            sliderMax = max,
            sliderWholeNumbers = wholeNumbers,
            widthRatio = ratio,
            onFloatChanged = onChanged
        };
    }

    public static GridCellSpec CreateScrollView(string key, float ratio)
    {
        return new GridCellSpec
        {
            type = CellType.ScrollView,
            key = key,
            widthRatio = ratio
        };
    }

    public static GridCellSpec CreateCustomImg(string key, float ratio)
    {
        return new GridCellSpec
        {
            type = CellType.CustomImgImporter,
            key = key,
            widthRatio = ratio
        };
    }

    public static GridCellSpec CreatePortraitPanel(string key, float ratio)
    {
        return new GridCellSpec
        {
            type = CellType.PortraitPanel,
            key = key,
            widthRatio = ratio
        };
    }

    public static GridCellSpec CreateNavigationTabs(string key, List<string> tabNames, List<GameObject> targetPanels, float ratio, Action<int> onTabChanged = null)
    {
        return new GridCellSpec
        {
            type = CellType.NavigationTabs,
            key = key,
            tabNames = tabNames,
            tabTargetPanels = targetPanels,
            widthRatio = ratio,
            onIntChanged = onTabChanged
        };
    }

    public static GridCellSpec CreateIDEInterface(string key, float ratio)
    {
        return new GridCellSpec
        {
            type = CellType.IDEInterface,
            key = key,
            widthRatio = ratio
        };
    }
}

public class ColumnSpec
{
    public string name;
    public float anchorMinX;
    public float anchorMaxX;
    public bool isCustomLayout;
    public List<GridRowSpec> rows;

    public ColumnSpec(string name, float min, float max, List<GridRowSpec> rows)
    {
        this.name = name;
        this.anchorMinX = min;
        this.anchorMaxX = max;
        this.rows = rows;
        this.isCustomLayout = false;
    }

    public ColumnSpec(string name, float min, float max)
    {
        this.name = name;
        this.anchorMinX = min;
        this.anchorMaxX = max;
        this.isCustomLayout = true;
    }
}

public class GeneratedScreen
{
    public RectTransform RootWrapper;
    public Dictionary<string, GridReferences> ColumnRefs = new Dictionary<string, GridReferences>();
    public Dictionary<string, RectTransform> ColumnPanels = new Dictionary<string, RectTransform>();
    public Dictionary<string, RectTransform> CustomPanels = new Dictionary<string, RectTransform>();
}