using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RootUIFactory : MonoBehaviour
{
    public static RootUIFactory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [SerializeField] private FullScreenUIGenerator uiGenerator;
    [SerializeField] private float topBarHeight = 45f;

    // Direct, strongly-typed references to each instantiated class
    public IDEUI IDEUI { get; private set; }
    public FullModUI FullModUI { get; private set; } // Added ModFactory reference
    public HeroUI HeroUI { get; private set; }
    public MonsterUI MonsterUI { get; private set; }
    public ItemUI ItemUI { get; private set; }
    public AbilityUI AbilityUI { get; private set; }

    //public SettingsUI SettingsUI { get; private set; }
    //public AbilityDataFactory AbilityDataFactory { get; private set; } //todo: support abilities.

    private RectTransform mainWrapper;
    private RectTransform topBarContainer;
    private RectTransform contentContainer;

    private List<string> tabNames = new List<string>();
    private List<GameObject> tabWrappers = new List<GameObject>();

    public void InitializeEntireUI()
    {
        if (uiGenerator == null || uiGenerator.canvas == null)
        {
            Debug.LogError("UI Generator or Canvas is missing.", this);
            return;
        }

        // 1. Create the screen layouts
        BuildLayoutContainers();

        // 2. Instantiate and reference the classes directly
        BuildUIChildren();

        // 3. Build the Top Bar using the generated tab lists
        BuildTopBar();

        OnTabChanged(0);
    }

    private void BuildLayoutContainers()
    {
        GameObject wrapperObj = new GameObject("Root_UI_Wrapper", typeof(RectTransform));
        mainWrapper = wrapperObj.GetComponent<RectTransform>();
        mainWrapper.SetParent(uiGenerator.canvas.transform, false);
        FullScreenUIGenerator.SetAnchors(mainWrapper, 0f, 0f, 1f, 1f);

        GameObject topBarObj = new GameObject("Top_Bar_Container", typeof(RectTransform));
        topBarContainer = topBarObj.GetComponent<RectTransform>();
        topBarContainer.SetParent(mainWrapper, false);
        FullScreenUIGenerator.SetAnchors(topBarContainer, 0f, 1f, 1f, 1f);
        topBarContainer.pivot = new Vector2(0.5f, 1f);
        topBarContainer.sizeDelta = new Vector2(0f, topBarHeight);

        GameObject contentObj = new GameObject("Main_Content_Container", typeof(RectTransform));
        contentContainer = contentObj.GetComponent<RectTransform>();
        contentContainer.SetParent(mainWrapper, false);
        FullScreenUIGenerator.SetAnchors(contentContainer, 0f, 0f, 1f, 1f);
        contentContainer.offsetMax = new Vector2(0f, -topBarHeight);
    }

    public void BuildUIChildren()
    {
        tabWrappers.Clear();
        tabNames.Clear();
        Canvas originalCanvas = uiGenerator.canvas;

        Canvas contentCanvas = contentContainer.gameObject.AddComponent<Canvas>();
        contentContainer.gameObject.AddComponent<GraphicRaycaster>();

        uiGenerator.canvas = contentCanvas;

        // Instantiate classes directly, assign references, and register them as tabs
        IDEUI = CreateTabInstance<IDEUI>("IDEUI", "IDE");
        FullModUI = CreateTabInstance<FullModUI>("FullModUI", "Modifiers");
        HeroUI = CreateTabInstance<HeroUI>("HeroUI", "Heroes");
        MonsterUI = CreateTabInstance<MonsterUI>("MonsterUI", "Monsters");
        ItemUI = CreateTabInstance<ItemUI>("ItemUI", "Items");
        AbilityUI = CreateTabInstance<AbilityUI>("AbilityUI", "Spells & Tactics");

        uiGenerator.canvas = originalCanvas;
    }

    /// <summary>
    /// Instantiates a RootUI component, registers it as a tab, and initializes its layout.
    /// </summary>
    private T CreateTabInstance<T>(string gameObjectName, string displayName) where T : RootUI
    {
        GameObject obj = new GameObject(gameObjectName);
        obj.transform.SetParent(contentContainer, false);
        T component = obj.AddComponent<T>();

        component.Initialize(uiGenerator);

        RectTransform tabWrapper = component.GetRootWrapper();
        if (tabWrapper != null)
        {
            FullScreenUIGenerator.SetAnchors(tabWrapper, 0f, 0f, 1f, 1f);
            tabWrapper.offsetMin = Vector2.zero;
            tabWrapper.offsetMax = Vector2.zero;

            tabNames.Add(displayName);
            tabWrappers.Add(tabWrapper.gameObject);
        }

        return component;
    }

    /// <summary>
    /// Instantiates a MonoBehaviour class directly without assigning it to a top bar tab.
    /// </summary>
    private T CreateInstanceOnly<T>(string gameObjectName) where T : MonoBehaviour
    {
        GameObject obj = new GameObject(gameObjectName);
        obj.transform.SetParent(contentContainer, false);
        return obj.AddComponent<T>();
    }

    private void BuildTopBar()
    {
        Canvas originalCanvas = uiGenerator.canvas;
        Canvas topCanvas = topBarContainer.gameObject.AddComponent<Canvas>();
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
        for (int i = 0; i < tabWrappers.Count; i++)
        {
            if (tabWrappers[i] != null)
            {
                tabWrappers[i].SetActive(i == tabIndex);
            }
        }
    }
}