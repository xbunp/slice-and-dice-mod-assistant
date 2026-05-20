using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum CellType { Input, Dropdown, Label }

public class GridReferences
{
    public Dictionary<string, TMP_InputField> Inputs = new Dictionary<string, TMP_InputField>();
    public Dictionary<string, TMP_Dropdown> Dropdowns = new Dictionary<string, TMP_Dropdown>();
}

public class GridRowSpec
{
    public List<GridCellSpec> cells = new List<GridCellSpec>();
    public GridRowSpec(params GridCellSpec[] cells) { this.cells.AddRange(cells); }
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

    public static GridCellSpec CreateDropdown(string key, string label, float ratio, string[] options, Action<int> onChanged)
    {
        return new GridCellSpec { type = CellType.Dropdown, key = key, labelText = label, widthRatio = ratio, dropdownOptions = new List<string>(options), onIntChanged = onChanged };
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
    public Dictionary<string, GridReferences> ColumnRefs = new Dictionary<string, GridReferences>();
    public Dictionary<string, RectTransform> CustomPanels = new Dictionary<string, RectTransform>();
}