using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RootUIFactory : MonoBehaviour
{
    private List<string> tabNames = new List<string> { "Heroes", "Phases", "Monsters", "Settings" };

    [SerializeField] private FullScreenUIGenerator uiGenerator;
    [SerializeField] private RootUI[] uiTabs;
    [SerializeField] private float topBarHeight = 45f;

    private RectTransform mainWrapper;
    private RectTransform topBarContainer;
    private RectTransform contentContainer;

    // THIS is the list of panels that MUST be passed into the NavigationTabs so the buttons actually work.
    private List<GameObject> tabWrappers = new List<GameObject>();

    void Start()
    {
        if (uiGenerator == null || uiGenerator.canvas == null)
        {
            Debug.LogError("UI Generator or Canvas is missing.", this);
            return;
        }

        // 1. First set up the overarching zero-margin boundaries.
        BuildLayoutContainers();

        // 2. Build the actual Pages first! This populates the tabWrappers list with actual GameObjects.
        BuildUIChildren();

        // 3. FINALLY, build the Top Bar and pass those tabWrappers in!
        BuildTopBar();

        // Default to displaying the first tab visually
        OnTabChanged(0);
    }

    private void BuildLayoutContainers()
    {
        // 1. Create Main Wrapper Flush to the screen edge
        GameObject wrapperObj = new GameObject("Root_UI_Wrapper", typeof(RectTransform));
        mainWrapper = wrapperObj.GetComponent<RectTransform>();
        mainWrapper.SetParent(uiGenerator.canvas.transform, false);
        FullScreenUIGenerator.SetAnchors(mainWrapper, 0f, 0f, 1f, 1f); // 100% Flush

        // 2. Top Bar Area
        GameObject topBarObj = new GameObject("Top_Bar_Container", typeof(RectTransform));
        topBarContainer = topBarObj.GetComponent<RectTransform>();
        topBarContainer.SetParent(mainWrapper, false);
        FullScreenUIGenerator.SetAnchors(topBarContainer, 0f, 1f, 1f, 1f);
        topBarContainer.pivot = new Vector2(0.5f, 1f);
        topBarContainer.sizeDelta = new Vector2(0f, topBarHeight);

        // 3. Content Area taking up the remainder
        GameObject contentObj = new GameObject("Main_Content_Container", typeof(RectTransform));
        contentContainer = contentObj.GetComponent<RectTransform>();
        contentContainer.SetParent(mainWrapper, false);
        FullScreenUIGenerator.SetAnchors(contentContainer, 0f, 0f, 1f, 1f);
        contentContainer.offsetMax = new Vector2(0f, -topBarHeight); // Offset by exactly the height of top bar
    }

    public void BuildUIChildren()
    {
        tabWrappers.Clear();
        Canvas originalCanvas = uiGenerator.canvas;

        // Dynamic Canvas targeting setup
        Canvas contentCanvas = contentContainer.gameObject.AddComponent<Canvas>();

        // FIX: Added GraphicRaycaster component so nested canvas buttons register click inputs
        contentContainer.gameObject.AddComponent<GraphicRaycaster>();

        uiGenerator.canvas = contentCanvas;

        for (int i = 0; i < uiTabs.Length; i++)
        {
            if (uiTabs[i] == null) continue;

            uiTabs[i].Initialize(uiGenerator);

            RectTransform tabWrapper = uiTabs[i].GetRootWrapper();
            if (tabWrapper != null)
            {
                FullScreenUIGenerator.SetAnchors(tabWrapper, 0f, 0f, 1f, 1f);
                tabWrapper.offsetMin = Vector2.zero;
                tabWrapper.offsetMax = Vector2.zero;

                tabWrappers.Add(tabWrapper.gameObject);
            }
        }

        uiGenerator.canvas = originalCanvas;
    }

    private void BuildTopBar()
    {
        Canvas originalCanvas = uiGenerator.canvas;
        Canvas topCanvas = topBarContainer.gameObject.AddComponent<Canvas>();

        // FIX: Added GraphicRaycaster component so the Navigation tabs register click inputs
        topBarContainer.gameObject.AddComponent<GraphicRaycaster>();

        uiGenerator.canvas = topCanvas;

        List<GridRowSpec> topBarRows = new List<GridRowSpec>
        {
            new GridRowSpec(topBarHeight,
                GridCellSpec.CreateNavigationTabs("TopTabs", tabNames, tabWrappers, 1.0f, OnTabChanged))
        };

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            new ColumnSpec("TopBar_Column", 0.0f, 1.0f, topBarRows)
        };

        uiGenerator.SetupScreen(columns, false);

        uiGenerator.canvas = originalCanvas;
    }

    private void OnTabChanged(int tabIndex)
    {
        // Redundancy lock: If the NavigationTab controller fails, this manually forces the panels.
        for (int i = 0; i < tabWrappers.Count; i++)
        {
            if (tabWrappers[i] != null)
            {
                tabWrappers[i].SetActive(i == tabIndex);
            }
        }
    }
}