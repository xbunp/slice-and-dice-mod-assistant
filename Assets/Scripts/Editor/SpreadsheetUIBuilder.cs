using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class SpreadsheetUIBuilder
{
    [MenuItem("GameObject/UI/Create Spreadsheet Layout")]
    public static void CreateSpreadsheetLayout()
    {
        // 1. Create Main Container (Vertical)
        GameObject container = new GameObject("SpreadsheetContainer", typeof(RectTransform));
        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true; // This makes Row 2 stretch
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // 2. Create Row 1 (Horizontal)
        GameObject row1 = new GameObject("Row1", typeof(RectTransform));
        row1.transform.SetParent(container.transform, false);
        HorizontalLayoutGroup hlg = row1.AddComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false; // Keeps items next to each other
        hlg.spacing = 10;

        // Create items for Row 1
        CreateEmptyUI("NameField", row1.transform);
        CreateEmptyUI("InputField1", row1.transform);

        // 3. Create Row 2 (Full Width)
        CreateEmptyUI("FullWidthInputField", container.transform);

        // Parent to selection
        if (Selection.activeGameObject != null)
            container.transform.SetParent(Selection.activeGameObject.transform, false);
    }

    private static void CreateEmptyUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        // This makes the object visible in the layout system 
        // without needing a Graphic component
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 30;
        le.preferredWidth = 100;
    }
}