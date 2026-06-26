using System.Collections.Generic;
using System.Linq;
using TMPro; // Assuming TextMeshPro is used for clarity, replace with standard Text if needed
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Rendering.VolumeComponent;

public enum ItemNodeType
{
    None,
    Appearance,
    Equippable,
    BaseItem,
    Operator,
    Hat,
    Bracket,
    RawString

    // old shit
    /*
    Equippable, Sticker, Splice, Merge, Unpack, ItemPart,
    Hero, Monster, Modifier, Ability,

    ItemRoot,
    FaceModifier,
    TriggeredAbility,
    EntityPayload,
    Operation,
    RawString
    */
}

// 1. The refined EntityCard retains strong typing.
public class EntityCard : ReorderableItem, IPointerClickHandler
{
    public ItemNodeType NodeType;
    public string CustomPrefix = "";

    // Strongly-Typed Backend References
    public ItemData RootData = new ItemData();
    public ItemMechanic MechanicData = new ItemMechanic();
    public SDData SemanticData;

    public ReorderableZone PayloadPort { get; set; }

    public string CardName => NodeRegistry.Get(NodeType).GetTitle(this);

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (eventData.dragging) return;
        StringAuthoringUIManager.Instance?.SelectCard(this);
    }

    public string Compile()
    {
        // Generate the standard text (which inherently concatenates children)
        string rawStr = NodeRegistry.Get(NodeType).Compile(this);

        // If we are doing the visual overlay pass, wrap this node's output in its color
        if (StringAuthoringUIManager.IsCompilingRichText && !string.IsNullOrWhiteSpace(rawStr))
        {
            Color nodeColor = NodeRegistry.Get(NodeType).GetColor();
            nodeColor = nodeColor * 1.5f;
            string hex = ColorUtility.ToHtmlStringRGB(nodeColor);

            return $"<color=#{hex}>{rawStr}</color>";
        }

        return rawStr;
    }

    // Safely extracts compiled strings from children
    public IEnumerable<string> GetChildCompilations()
    {
        if (PayloadPort == null || PayloadPort.Entrants.Count == 0) yield break;
        foreach (var child in PayloadPort.Entrants.Cast<EntityCard>())
        {
            string childStr = child.Compile();
            if (!string.IsNullOrWhiteSpace(childStr)) yield return childStr;
        }
    }
}

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class StringAuthoringUIManager : RootUI
{
    // Small utility to keep the card title text up to date
    public class UpdateTitleUtility : MonoBehaviour
    {
        EntityCard card; TMPro.TextMeshProUGUI txt;
        public void Init(EntityCard c, TMPro.TextMeshProUGUI t) { card = c; txt = t; }
        void Update() { if (card != null && txt != null) txt.text = card.CardName; }
    }

    public static StringAuthoringUIManager Instance { get; private set; }
    private TextMeshProUGUI _sidebarText;
    private RectTransform _rootCanvasRect;
    private Canvas _cachedCanvas;
    private TMPro.TMP_InputField _compiledOutputField;

    private GameObject inputFieldPrefab => FullScreenUIGenerator.Instance.inputFieldPrefab;
    private GameObject buttonPrefab => FullScreenUIGenerator.Instance.buttonPrefab;
    private GameObject dropdownPrefab => FullScreenUIGenerator.Instance.dropdownPrefab;

    private List<ItemNodeType> _dropdownNodeTypes = new List<ItemNodeType>();

    // References to our major panels
    public RectTransform BreadcrumbPanel { get; private set; }
    public RectTransform SidebarContent { get; private set; }
    public RectTransform MainCanvasContent { get; private set; }
    public RectTransform InspectorContent { get; private set; }

    [Header("Configuration")]
    public float topBarHeight = 50f;
    public float sidebarWidth = 300f;
    public float inspectorWidth = 400f;

    private ReorderableZone _rootZone;
    private TMPro.TMP_Dropdown _loadDropdown;
    private bool _isUpdatingDropdown = false;
    private EntityCard _selectedCard;
    private TextMeshProUGUI _syntaxHighlighterText;
    public static bool IsCompilingRichText = false;

    private void Awake()
    {
        Instance = this;
    }
    private void OnDestroy()
    {
        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModLoaded -= PopulateLoadDropdown;
        }
        if (_rootZone != null)
        {
            _rootZone.OnZoneChanged -= RefreshSidebar;
        }
    }
    public void Start()
    {
        // 1. Ensure we have an event system for drag logic
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // 2. Build the visual workspace structure immediately (safe, purely layout)
        //BuildWorkspace();

        // 3. Defer database connections until ModPackage is awake and loaded
        StartCoroutine(WaitForModPackage());
    }

    protected override void BuildUIAndBind()
    {
        // 1. Generate standard screen mapping to supply the root wrapper to RootUI
        List<ColumnSpec> columns = new List<ColumnSpec>();
        generatedScreen = uiGenerator.SetupScreen(columns, false);
        _rootCanvasRect = generatedScreen.RootWrapper;
        _cachedCanvas = FullScreenUIGenerator.Instance.canvas;

        // 2. Build out internal layout frames nested inside the generated wrapper
        BuildTopBar();
        BuildSidebar();
        BuildMainCanvas();
        BuildInspector();

        // Setup the root drop zone for the main canvas
        SetupRootWorkspaceZone();
    }
    private System.Collections.IEnumerator WaitForModPackage()
    {
        // Poll and wait until ModPackage is instantiated in memory
        while (ModPackage.Instance == null)
        {
            yield return null;
        }

        // Wait until ModPackage has finished creating/loading its mod database
        while (!ModPackage.Instance.isModLoaded)
        {
            yield return null;
        }

        // Now that the ModPackage is 100% ready, populate and initialize editing
        InitializeDataOnStart();
    }
    private void InitializeDataOnStart()
    {
        // Populate dropdown from loaded mod items
        PopulateLoadDropdown();

        // Subscribe to global mod loaded events
        ModPackage.Instance.OnModLoaded += PopulateLoadDropdown;

        // Initialize the workspace with a blank item to start authoring right away
        CreateNewItem();
    }
    private void BuildTopBar()
    {
        // Container
        BreadcrumbPanel = CreateRect("ToolbarBar", _rootCanvasRect);
        SetAnchors(BreadcrumbPanel, 0, 1, 1, 1);
        BreadcrumbPanel.pivot = new Vector2(0.5f, 1);
        BreadcrumbPanel.offsetMin = new Vector2(0, -topBarHeight);
        BreadcrumbPanel.offsetMax = Vector2.zero;
        AddColor(BreadcrumbPanel, new Color(0.15f, 0.15f, 0.15f));

        // Output Field (Using Prefab)
        if (inputFieldPrefab != null)
        {
            GameObject outputObj = Instantiate(inputFieldPrefab, BreadcrumbPanel);
            RectTransform outputRect = outputObj.GetComponent<RectTransform>();
            outputRect.anchorMin = new Vector2(0.02f, 0.1f);
            outputRect.anchorMax = new Vector2(0.98f, 0.9f);
            outputRect.offsetMin = Vector2.zero;
            outputRect.offsetMax = Vector2.zero;

            _compiledOutputField = outputObj.GetComponent<TMPro.TMP_InputField>();
            _compiledOutputField.readOnly = true;

            // Make interactive input text transparent so overlay remains visible
            _compiledOutputField.textComponent.color = Color.clear;
            _compiledOutputField.richText = false;

            GameObject highlighterObj = new GameObject("SyntaxHighlighter", typeof(RectTransform));
            highlighterObj.transform.SetParent(_compiledOutputField.textComponent.transform.parent, false);

            _syntaxHighlighterText = highlighterObj.AddComponent<TMPro.TextMeshProUGUI>();

            CanvasGroup canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
            RectTransform textCompRt = _compiledOutputField.textComponent.GetComponent<RectTransform>();
            highlightRt.anchorMin = textCompRt.anchorMin;
            highlightRt.anchorMax = textCompRt.anchorMax;
            highlightRt.offsetMin = textCompRt.offsetMin;
            highlightRt.offsetMax = textCompRt.offsetMax;
            highlightRt.pivot = textCompRt.pivot;

            _syntaxHighlighterText.enableAutoSizing = false;
            _syntaxHighlighterText.fontSize = _compiledOutputField.textComponent.fontSize;
            _syntaxHighlighterText.alignment = _compiledOutputField.textComponent.alignment;
            _syntaxHighlighterText.margin = _compiledOutputField.textComponent.margin;
            _syntaxHighlighterText.enableWordWrapping = _compiledOutputField.textComponent.enableWordWrapping;
            _syntaxHighlighterText.richText = true;
        }
    }

    private void BuildSidebar()
    {
        // Main Sidebar Container
        RectTransform sidebarBG = CreateRect("SidebarArea", _rootCanvasRect);
        SetAnchors(sidebarBG, 0, 0, 0, 1);
        sidebarBG.pivot = new Vector2(0, 1);
        sidebarBG.offsetMin = Vector2.zero;
        sidebarBG.offsetMax = new Vector2(sidebarWidth, -topBarHeight);
        AddColor(sidebarBG, new Color(0.1f, 0.1f, 0.1f));

        float toolbarHeight = 110;

        // Delegate Toolbar creation
        BuildSidebarToolbar(sidebarBG, toolbarHeight);

        // Sidebar Content Scroll View (Offset below the toolbar)
        RectTransform sidebarScrollView = CreateRect("SidebarScrollViewContainer", sidebarBG);
        SetAnchors(sidebarScrollView, 0, 0, 1, 1);
        sidebarScrollView.offsetMin = Vector2.zero;
        sidebarScrollView.offsetMax = new Vector2(0, -toolbarHeight - 10f);

        SidebarContent = CreateScrollView(sidebarScrollView, "SidebarScrollView");

        // Format the tree layout
        var sidebarLayout = SidebarContent.gameObject.GetComponent<VerticalLayoutGroup>();
        sidebarLayout.padding = new RectOffset(15, 15, 15, 15);
        sidebarLayout.spacing = 5;
        sidebarLayout.childControlHeight = true;
        sidebarLayout.childControlWidth = true;
        sidebarLayout.childForceExpandHeight = false;

        var sidebarFitter = SidebarContent.gameObject.AddComponent<ContentSizeFitter>();
        sidebarFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    private void BuildSidebarToolbar(RectTransform parentBG, float toolbarHeight)
    {
        RectTransform sidebarToolbar = CreateRect("SidebarToolbar", parentBG);
        SetAnchors(sidebarToolbar, 0, 1, 1, 1);
        sidebarToolbar.pivot = new Vector2(0.5f, 1);
        sidebarToolbar.offsetMin = new Vector2(10, -toolbarHeight);
        sidebarToolbar.offsetMax = new Vector2(-10, -5);

        var toolLayout = sidebarToolbar.gameObject.AddComponent<VerticalLayoutGroup>();
        toolLayout.spacing = 6;
        toolLayout.childControlWidth = true; toolLayout.childControlHeight = true;
        toolLayout.childForceExpandWidth = true; toolLayout.childForceExpandHeight = false;

        // --- ROW 1: Load Mod Dropdown ---
        if (dropdownPrefab != null)
        {
            GameObject ddObj = Instantiate(dropdownPrefab, sidebarToolbar);
            _loadDropdown = ddObj.GetComponent<TMPro.TMP_Dropdown>();
            _loadDropdown.ClearOptions();

            _loadDropdown.onValueChanged.AddListener((idx) =>
            {
                if (_isUpdatingDropdown || idx == 0) return;

                ItemData selectedItem = ModPackage.Instance.Items[idx - 1];
                ModPackage.Instance.LoadEntityForEditing(selectedItem);

                ItemData activeClone = ModPackage.Instance.GetActiveEntity<ItemData>();
                if (activeClone != null) LoadItemIntoUI(activeClone);
            });

            var ddLayout = ddObj.GetComponent<LayoutElement>() ?? ddObj.AddComponent<LayoutElement>();
            ddLayout.preferredHeight = 30f;
        }

        // --- ROW 2: Save / New Buttons ---
        RectTransform buttonRow = CreateRect("ButtonRow", sidebarToolbar);
        var buttonRowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonRowLayout.spacing = 6;
        buttonRowLayout.childControlWidth = true; buttonRowLayout.childControlHeight = true;
        buttonRowLayout.childForceExpandWidth = true; buttonRowLayout.childForceExpandHeight = true;

        var rowLayoutElem = buttonRow.gameObject.AddComponent<LayoutElement>();
        rowLayoutElem.preferredHeight = 30f;
        rowLayoutElem.flexibleHeight = 0f;

        if (buttonPrefab != null)
        {
            GameObject saveObj = Instantiate(buttonPrefab, buttonRow);
            var saveBtn = saveObj.GetComponent<UnityEngine.UI.Button>();
            var saveText = saveObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (saveText != null) { saveText.text = "Save"; saveText.fontSize = 14; }

            // FIX: Linked to actual Save logic
            saveBtn.onClick.RemoveAllListeners();
            saveBtn.onClick.AddListener(() => SaveActiveItem());
        }

        if (buttonPrefab != null)
        {
            GameObject newObj = Instantiate(buttonPrefab, buttonRow);
            var newBtn = newObj.GetComponent<UnityEngine.UI.Button>();
            var newText = newObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (newText != null) { newText.text = "New"; newText.fontSize = 14; }

            // FIX: Linked to actual New logic
            newBtn.onClick.RemoveAllListeners();
            newBtn.onClick.AddListener(() => CreateNewItem());
        }

        // Import Button
        if (buttonPrefab != null)
        {
            GameObject importObj = Instantiate(buttonPrefab, buttonRow);
            var importBtn = importObj.GetComponent<UnityEngine.UI.Button>();
            var importText = importObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (importText != null) { importText.text = "Import"; importText.fontSize = 14; }

            importBtn.onClick.AddListener(() => ImportFromClipboard());
        }

        // --- ROW 3: Add Logic Node Dropdown ---
        if (dropdownPrefab != null)
        {
            GameObject ddObj = Instantiate(dropdownPrefab, sidebarToolbar);
            var nodeDropdown = ddObj.GetComponent<TMPro.TMP_Dropdown>();
            nodeDropdown.ClearOptions();

            // Prepare the lists for dropdown data
            List<string> options = new List<string> { "-- Add Logic Node --" };

            _dropdownNodeTypes.Clear();
            _dropdownNodeTypes.Add(ItemNodeType.None); // Placeholder for index 0

            // Populate from Registry
            foreach (AuthoringNodeDef nodeDef in NodeRegistry.GetAll())
            {
                options.Add(nodeDef.NodeNiceName);
                _dropdownNodeTypes.Add(nodeDef.NodeType);
            }
            nodeDropdown.AddOptions(options);

            // Bind listener
            nodeDropdown.onValueChanged.AddListener((idx) => OnAddNodeSelected(idx, nodeDropdown));
            var ddLayout = ddObj.GetComponent<LayoutElement>() ?? ddObj.AddComponent<LayoutElement>();
            ddLayout.preferredHeight = 30f;
        }
    }
    public void ClearWorkspace()
    {
        // 1. Clear the Inspector Content (Right Panel)
        if (InspectorContent != null)
        {
            foreach (Transform child in InspectorContent)
            {
                Destroy(child.gameObject);
            }
        }

        // 2. Clear the visual Cards from the Main Canvas (Middle Panel)
        if (MainCanvasContent != null)
        {
            // Clear the logical entrants tracking list
            if (_rootZone != null)
            {
                _rootZone.Entrants.Clear();
            }

            foreach (Transform child in MainCanvasContent)
            {
                // We only destroy game objects representing visual cards.
                // We must NOT destroy the scroll view's structural layout elements.
                if (child.GetComponent<EntityCard>() != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 3. Reset compiler text field
        if (_compiledOutputField != null)
        {
            _compiledOutputField.text = string.Empty;
        }
    }
    public void PopulateLoadDropdown()
    {
        if (_loadDropdown == null || ModPackage.Instance == null) return;

        _isUpdatingDropdown = true;
        _loadDropdown.ClearOptions();

        var options = new System.Collections.Generic.List<string> { "Load Saved Item..." };
        foreach (var item in ModPackage.Instance.Items)
        {
            string displayName = string.IsNullOrEmpty(item.entityName) ? "Unnamed Item" : item.entityName;
            if (item.Tier.HasValue) displayName += $" (Tier {item.Tier})";
            options.Add(displayName);
        }

        _loadDropdown.AddOptions(options);
        _loadDropdown.SetValueWithoutNotify(0);
        _isUpdatingDropdown = false;
    }
    /// <summary>
    /// Generates a blank item session and resets the workspace.
    /// </summary>
    public void CreateNewItem()
    {
        // FIX: Guard against running before MainCanvasContent has been instantiated in BuildMainCanvas()
        if (MainCanvasContent == null) return;

        // 1. Create a blank backend editing session
        ItemData newItem = new ItemData();
        ModPackage.Instance.LoadEntityForEditing(newItem);

        // 2. Clear the workspace canvas UI
        ClearWorkspace();

        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();
        if (rootZone != null)
        {
            EntityCard equipCard = CreateEntityCard(ItemNodeType.Equippable) as EntityCard;
            if (equipCard != null) // Guard against null cards
            {
                equipCard.RootData.entityName = "New Item";
                rootZone.AddEntrant(equipCard);
            }
        }

        // 4. Force a UI layout update and output compilation
        RefreshSidebar();
        AutoCompile();
    }
    /// <summary>
    /// Compiles the visual tree back into the active clone and saves it to ModPackage.
    /// </summary>
    public void SaveActiveItem()
    {
        ItemData activeClone = ModPackage.Instance?.GetActiveEntity<ItemData>();
        if (activeClone == null) return;

        // Ensure the visual canvas is fully synced to the backend clone
        AutoCompile();

        // Commit the session changes
        ModPackage.Instance.SaveActiveEntity<ItemData>();

        // Refresh load list in case the item was renamed
        PopulateLoadDropdown();
        Debug.Log($"Successfully saved item: {activeClone.entityName ?? "Unnamed Item"}");
    }
    /// <summary>
    /// Clears the workspace and recursively reconstructs the visual tree from ItemData.
    /// </summary>
    /// <summary>
    /// Clears the workspace and reconstructs the visual tree with only top-level visual nodes.
    /// </summary>
    public void LoadItemIntoUI(ItemData item)
    {
        ClearWorkspace();

        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();
        if (rootZone == null) return;

        Debug.Log($"<color=cyan>[UI Load]</color> Loading Item. Mechanics: {item.Mechanics.Count}");

        // Mechanics are dropped directly onto the Workspace Canvas. No root card wrapper!
        foreach (var mechanic in item.Mechanics)
        {
            LoadMechanicIntoUI(mechanic, rootZone);
        }

        RefreshSidebar();
        AutoCompile();
    }
    /// <summary>
    /// Spawns the visual mechanic card. Sub-payloads are left to the inspector.
    /// </summary>
    private void LoadMechanicIntoUI(ItemMechanic mechanic, ReorderableZone targetZone)
    {
        ItemNodeType type = ItemNodeType.BaseItem;
        if (mechanic.Prefix == "hat") type = ItemNodeType.Hat;
        //else if (mechanic.Prefix == "sticker") type = ItemNodeType.Sticker;
        //else if (mechanic.Unpack) type = ItemNodeType.Unpack;
        //else if (mechanic.PartIndex.HasValue) type = ItemNodeType.ItemPart;
        //else if (!string.IsNullOrEmpty(mechanic.SplicedItem)) type = ItemNodeType.Splice;
        //else if (!string.IsNullOrEmpty(mechanic.MergedItem)) type = ItemNodeType.Merge;

        EntityCard mechCard = CreateEntityCard(type) as EntityCard;
        mechCard.MechanicData = mechanic;
        targetZone.AddEntrant(mechCard);

        // NOTICE: We do not unpack mechanic.PayloadData or mechanic.PayloadString.
        // They are parameters of this mechanic node, edited via the Inspector panel.
    }
    /// <summary>
    /// Evaluates the metadata and flags on a mechanic instance, spawns its card UI representation, 
    /// and proceeds to identify and pass on its sub-payload elements.
    /// </summary>
    /// 
    private void BuildMainCanvas()
    {
        RectTransform mainAreaBG = CreateRect("MainCanvasArea", _rootCanvasRect);
        SetAnchors(mainAreaBG, 0, 0, 1, 1);
        mainAreaBG.pivot = new Vector2(0, 1);
        mainAreaBG.offsetMin = new Vector2(sidebarWidth, 0);
        mainAreaBG.offsetMax = new Vector2(-inspectorWidth, -topBarHeight);
        AddColor(mainAreaBG, new Color(0.2f, 0.2f, 0.2f));

        MainCanvasContent = CreateScrollView(mainAreaBG, "MainScrollView", true);

        var mainLayout = MainCanvasContent.gameObject.GetComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(40, 40, 40, 40);
        mainLayout.spacing = 20;
    }
    private void LoadMechanicIntoUI(ItemMechanic mechanic, ReorderableZone targetZone, int depth = 0)
    {
        string indent = new string(' ', depth * 4);

        ItemNodeType type = ItemNodeType.BaseItem;

        // Map mechanics to visual node strategies
        if (mechanic.Prefix == "hat") type = ItemNodeType.Hat;
        //else if (mechanic.Prefix == "sticker") type = ItemNodeType.Sticker;
        //else if (mechanic.Unpack) type = ItemNodeType.Unpack;
        //else if (mechanic.PartIndex.HasValue) type = ItemNodeType.ItemPart;
        //else if (!string.IsNullOrEmpty(mechanic.SplicedItem)) type = ItemNodeType.Splice;
        //else if (!string.IsNullOrEmpty(mechanic.MergedItem)) type = ItemNodeType.Merge;
        else if (mechanic.Prefix == "facade" || mechanic.Prefix == "sidesc" || mechanic.Prefix == "img" || mechanic.Prefix == "doc")
            type = ItemNodeType.Appearance; // <-- Map to new Appearance node

        EntityCard mechCard = CreateEntityCard(type) as EntityCard;
        mechCard.MechanicData = mechanic;
        targetZone.AddEntrant(mechCard);

        if (mechanic.PayloadData != null)
        {
            LoadDataPayloadIntoUI(mechanic.PayloadData, "", mechCard.PayloadPort, depth + 1);
        }
        else if (!string.IsNullOrEmpty(mechanic.PayloadString) && type != ItemNodeType.Appearance)
        {
            // Notice we skip unpacking RawStrings if it's an Appearance node. 
            // Appearance values (like "Che5:89") just belong in the inspector, not as child nodes.
            string cleanPayload = mechanic.PayloadString.Trim();

            EntityCard rawCard = CreateEntityCard(ItemNodeType.RawString) as EntityCard;
            if (cleanPayload.StartsWith("(") && cleanPayload.EndsWith(")"))
                cleanPayload = cleanPayload.Substring(1, cleanPayload.Length - 2);

            rawCard.MechanicData.PayloadString = cleanPayload;
            mechCard.PayloadPort.AddEntrant(rawCard);
        }
    }
    private void LoadDataPayloadIntoUI(object payloadData, string prefix, ReorderableZone targetZone, int depth = 0)
    {
        if (payloadData == null) return;

        if (payloadData is ItemData nestedItem)
        {
            // Removed the IsPurelyCosmetic check. If there's an ItemData wrapper, it gets a node!
            EntityCard nestedEquip = CreateEntityCard(ItemNodeType.Equippable) as EntityCard;
            nestedEquip.RootData = nestedItem;
            nestedEquip.CustomPrefix = prefix;
            targetZone.AddEntrant(nestedEquip);

            foreach (var childMech in nestedItem.Mechanics)
            {
                LoadMechanicIntoUI(childMech, nestedEquip.PayloadPort, depth + 1);
            }
        }
        // ... (rest of method remains the same)
    }
    private void BuildInspector()
    {
        RectTransform inspectorBG = CreateRect("InspectorArea", _rootCanvasRect);
        SetAnchors(inspectorBG, 1, 0, 1, 1);
        inspectorBG.pivot = new Vector2(1, 1);
        inspectorBG.offsetMin = new Vector2(-inspectorWidth, 0);
        inspectorBG.offsetMax = new Vector2(0, -topBarHeight);
        AddColor(inspectorBG, new Color(0.12f, 0.12f, 0.12f));

        InspectorContent = CreateScrollView(inspectorBG, "InspectorScrollView");

        var inspLayout = InspectorContent.GetComponent<VerticalLayoutGroup>();
        inspLayout.padding = new RectOffset(20, 20, 20, 20);
        inspLayout.spacing = 15;
    }
    private void SetupRootWorkspaceZone()
    {
        _rootZone = MainCanvasContent.gameObject.AddComponent<ReorderableZone>();
        _rootZone.SetCanvas(_cachedCanvas);
        _rootZone.OnZoneChanged += RefreshSidebar;
    }
    public void CreateInspectorInputField(string label, string initialValue, UnityEngine.Events.UnityAction<string> onValueChanged)
    {
        // Container
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlHeight = true; layout.childControlWidth = true;
        container.gameObject.AddComponent<LayoutElement>().minHeight = 35f;

        // Label
        RectTransform labelRect = CreateRect("Label", container);
        var labelText = labelRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 14;
        labelText.color = Color.grey;
        labelRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        // Input Field (Using Prefab)
        // Output Field (Using Prefab)
        if (inputFieldPrefab != null)
        {
            GameObject outputObj = Instantiate(inputFieldPrefab, BreadcrumbPanel);
            RectTransform outputRect = outputObj.GetComponent<RectTransform>();
            outputRect.anchorMin = new Vector2(0.02f, 0.1f);
            outputRect.anchorMax = new Vector2(0.98f, 0.9f);
            outputRect.offsetMin = Vector2.zero;
            outputRect.offsetMax = Vector2.zero;

            _compiledOutputField = outputObj.GetComponent<TMPro.TMP_InputField>();
            _compiledOutputField.readOnly = true;

            // Stacked Syntax Highlighter Overlay setup
            _compiledOutputField.textComponent.color = Color.clear; // Make real text transparent
            _compiledOutputField.richText = false;

            // Create a clean GameObject dynamically to avoid script stripping & dependency errors
            GameObject highlighterObj = new GameObject("SyntaxHighlighter", typeof(RectTransform));
            highlighterObj.transform.SetParent(_compiledOutputField.textComponent.transform.parent, false);

            _syntaxHighlighterText = highlighterObj.AddComponent<TMPro.TextMeshProUGUI>();

            var canvasGroup = highlighterObj.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = highlighterObj.AddComponent<CanvasGroup>();
            }
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform highlightRt = highlighterObj.GetComponent<RectTransform>();
            RectTransform textCompRt = _compiledOutputField.textComponent.GetComponent<RectTransform>();
            highlightRt.anchorMin = textCompRt.anchorMin;
            highlightRt.anchorMax = textCompRt.anchorMax;
            highlightRt.offsetMin = textCompRt.offsetMin;
            highlightRt.offsetMax = textCompRt.offsetMax;
            highlightRt.pivot = textCompRt.pivot;

            _syntaxHighlighterText.enableAutoSizing = false;
            _syntaxHighlighterText.fontSize = _compiledOutputField.textComponent.fontSize;
            _syntaxHighlighterText.alignment = _compiledOutputField.textComponent.alignment;
            _syntaxHighlighterText.margin = _compiledOutputField.textComponent.margin;
            _syntaxHighlighterText.enableWordWrapping = _compiledOutputField.textComponent.enableWordWrapping;
            _syntaxHighlighterText.richText = true;

            _compiledOutputField.onValueChanged.AddListener((val) =>
            {
                if (_syntaxHighlighterText != null)
                {
                    //_syntaxHighlighterText.text = ColorHelpers.FormatSyntaxHighlighting(val);
                }
            });
        }
    }
    public void ImportFromClipboard()
    {
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clip))
        {
            Debug.LogWarning("Clipboard is empty.");
            return;
        }

        ItemData importedItem = new ItemData();
        try
        {
            importedItem.Parse(clip);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse clipboard string as ItemData: {e.Message}");
            return;
        }

        // Load it into the active ModPackage session so saves work seamlessly
        ModPackage.Instance.LoadEntityForEditing(importedItem);

        ItemData activeClone = ModPackage.Instance.GetActiveEntity<ItemData>();
        if (activeClone != null)
        {
            LoadItemIntoUI(activeClone);
        }
    }
    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        RectTransform rect = go.AddComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }
    private void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
    }
    private void AddColor(RectTransform rect, Color color)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
    }
    private RectTransform CreateScrollView(RectTransform parent, string name, bool useHorizontal = false)
    {
        // ScrollRect setup
        RectTransform scrollRoot = CreateRect(name, parent);
        SetAnchors(scrollRoot, 0, 0, 1, 1);
        scrollRoot.offsetMin = Vector2.zero; scrollRoot.offsetMax = Vector2.zero;
        ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = useHorizontal;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 0.18f;

        // Viewport (Masking)
        RectTransform viewport = CreateRect("Viewport", scrollRoot);
        SetAnchors(viewport, 0, 0, 1, 1);
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
        viewport.pivot = new Vector2(0, 1);
        Image vpImage = viewport.gameObject.AddComponent<Image>();
        vpImage.color = new Color(1, 1, 1, 0.01f); // nearly invisible mask
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        RectTransform content = CreateRect("Content", viewport);
        SetAnchors(content, 0, 1, 1, 1); // Anchored to top, stretches width
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;

        var vLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.childControlHeight = true;
        vLayout.childControlWidth = true;
        vLayout.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport;
        scrollRect.content = content;

        return content;
    }
    public void RefreshSidebar()
    {
        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();

        // 1. VALIDATE FIRST: Ensure the canvas has all its required operators before we draw anything
        if (rootZone != null)
        {
            ValidateWorkspaceOperators(rootZone);
        }

        // 2. CLEAR: Wipe the old sidebar UI
        foreach (Transform child in SidebarContent) Destroy(child.gameObject);

        // 3. HEADER: Build Workspace Header
        RectTransform headerRect = CreateRect("TreeHeader", SidebarContent);
        var layout = headerRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        headerRect.gameObject.AddComponent<LayoutElement>().minHeight = 30f;

        var headerBg = headerRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        headerBg.color = new Color(1, 1, 1, 0.05f);
        var headerBtn = headerRect.gameObject.AddComponent<UnityEngine.UI.Button>();
        //headerBtn.onClick.AddListener(() => InspectWorkspaceItem());

        var headerText = CreateRect("Label", headerRect).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        ItemData activeClone = ModPackage.Instance?.GetActiveEntity<ItemData>();
        string title = string.IsNullOrEmpty(activeClone?.entityName) ? "Item Hierarchy" : activeClone.entityName;
        headerText.text = $"<b>{title}</b>";
        headerText.fontSize = 16;
        headerText.color = new Color(0.8f, 0.6f, 0.2f); // Gold
        headerText.alignment = TextAlignmentOptions.Center;

        // 4. DRAW: Loop through the newly validated list to build the rows
        if (rootZone != null)
        {
            foreach (var entrant in rootZone.Entrants)
            {
                if (entrant is EntityCard card) AppendToSidebar(card, 1);
            }
        }

        // 5. COMPILE: Force the raw text string to update to match the validated layout
        AutoCompile();
    }
    private void AppendToSidebar(EntityCard card, int indentLevel)
    {
        // Create a clickable row for this item
        RectTransform rowRect = CreateRect($"Row_{card.CardName}", SidebarContent);
        var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;

        // Push the text to the right based on how deep we are nested
        layout.padding = new RectOffset(indentLevel * 10, 0, 0, 0);

        rowRect.gameObject.AddComponent<LayoutElement>().minHeight = 25f;

        // Make the row background a clickable button
        var bgImage = rowRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(1, 1, 1, 0.05f); // Very faint background
        var button = rowRect.gameObject.AddComponent<UnityEngine.UI.Button>();

        // When clicked, send this card to the right-side inspector
        button.onClick.AddListener(() =>
        {
            SelectCard(card);
        });

        // Add the text label
        RectTransform textRect = CreateRect("Label", rowRect);
        var text = textRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        string prefix = indentLevel == 0 ? " ■ " : " - ";
        text.text = $"{prefix}{card.CardName}";
        text.fontSize = 14;
        text.color = new Color(0.8f, 0.8f, 0.8f);

        // Recursively add children found in this card's payload port
        if (card.PayloadPort != null)
        {
            foreach (var childItem in card.PayloadPort.Entrants)
            {
                if (childItem is EntityCard childCard)
                {
                    AppendToSidebar(childCard, indentLevel + 1);
                }
            }
        }
    }

    public void AutoCompile()
    {
        if (_compiledOutputField == null || MainCanvasContent == null) return;
        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();

        if (rootZone == null) return;

        var cards = rootZone.Entrants.Cast<EntityCard>();

        // 1. Compile plain text for the interactable/copyable background field
        IsCompilingRichText = false;
        _compiledOutputField.text = CompileZone(cards);

        // 2. Compile rich text for the foreground syntax overlay
        if (_syntaxHighlighterText != null)
        {
            IsCompilingRichText = true;
            _syntaxHighlighterText.text = CompileZone(cards);
            IsCompilingRichText = false; // Reset
        }
    }
    private void OnAddNodeSelected(int index, TMPro.TMP_Dropdown dropdown)
    {
        // Safety checks: skip if placeholder is selected or workspace zone is missing
        if (index == 0 || _rootZone == null) return;

        // Safety check: ensure index is within the bounds of the dynamic list
        if (index < 0 || index >= _dropdownNodeTypes.Count) return;

        // Retrieve the correct enum type dynamically from our mapped list
        ItemNodeType type = _dropdownNodeTypes[index];

        // Create the card and drop it onto the main canvas workspace
        EntityCard newCard = CreateEntityCard(type) as EntityCard;
        if (newCard != null)
        {
            _rootZone.AddEntrant(newCard);
        }

        // Reset the dropdown visually so it can be used again
        dropdown.SetValueWithoutNotify(0);
        RefreshSidebar();
    }
    public void CreateInspectorDropdown(string label, List<string> options, int currentIndex, UnityEngine.Events.UnityAction<int> onValueChanged)
    {
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        container.gameObject.AddComponent<LayoutElement>().minHeight = 35f;

        RectTransform labelRect = CreateRect("Label", container);
        var labelText = labelRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        labelText.text = label; labelText.fontSize = 14; labelText.color = Color.grey;
        labelRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        if (dropdownPrefab != null)
        {
            GameObject ddObj = Instantiate(dropdownPrefab, container);
            var dropdown = ddObj.GetComponent<TMPro.TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.value = currentIndex;
            dropdown.onValueChanged.AddListener(onValueChanged);
        }
    }
    public void CreateInspectorTextArea(string label, string initialValue, UnityEngine.Events.UnityAction<string> onValueChanged)
    {
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        var layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlHeight = true; layout.childControlWidth = true;

        RectTransform labelRect = CreateRect("Label", container);
        var labelText = labelRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 14;
        labelText.color = Color.grey;
        labelRect.gameObject.AddComponent<LayoutElement>().minHeight = 20f;

        if (inputFieldPrefab != null)
        {
            GameObject inputObj = Instantiate(inputFieldPrefab, container);
            var inputField = inputObj.GetComponent<TMPro.TMP_InputField>();
            inputField.lineType = TMPro.TMP_InputField.LineType.MultiLineNewline;

            var inputLayout = inputObj.GetComponent<LayoutElement>() ?? inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredHeight = 100f;

            inputField.text = initialValue;
            inputField.onValueChanged.AddListener(onValueChanged);
        }
    }
    public void SelectCard(EntityCard card)
    {
        _selectedCard = card;
        foreach (Transform child in InspectorContent) Destroy(child.gameObject);

        if (card != null)
        {
            NodeRegistry.Get(card.NodeType).DrawInspector(this, card);
        }
    }
    public void DeleteCard(EntityCard card)
    {
        if (card == null) return;

        // 1. If we are deleting the card currently loaded in the inspector, clear it.
        if (_selectedCard == card)
        {
            _selectedCard = null;
            foreach (Transform child in InspectorContent) Destroy(child.gameObject);
        }

        // 2. Remove the card from its parent drop zone's internal entrants list
        var parentZone = card.transform.parent != null ? card.transform.parent.GetComponent<ReorderableZone>() : null;
        if (parentZone != null)
        {
            parentZone.Entrants.Remove(card);
        }
        else if (_rootZone != null)
        {
            _rootZone.Entrants.Remove(card);
        }

        // 3. Destroy the physical game object
        Destroy(card.gameObject);

        // 4. Update workspace layout and auto-compile
        RefreshSidebar();
        AutoCompile();
    }
    public static string CompileZone(IEnumerable<EntityCard> cards)
    {
        if (cards == null) return "";

        // Pure, explicit concatenation. The nodes output exactly what they mean.
        var parts = cards.Select(c => c.Compile()).Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join("", parts);
    }
    private void ValidateWorkspaceOperators(ReorderableZone zone)
    {
        if (zone == null) return;
        bool layoutChanged = false;

        // PASS 1: Destroy dangling or redundant operators
        for (int i = zone.Entrants.Count - 1; i >= 0; i--)
        {
            if (zone.Entrants[i] is EntityCard card && card.NodeType == ItemNodeType.Operator)
            {
                bool isFirst = (i == 0);
                bool isLast = (i == zone.Entrants.Count - 1);

                var prevCard = !isFirst ? zone.Entrants[i - 1] as EntityCard : null;
                var nextCard = !isLast ? zone.Entrants[i + 1] as EntityCard : null;

                // If an operator is at the ends, or next to something that doesn't want operators, kill it.
                if (isFirst || isLast || prevCard?.NodeType == ItemNodeType.Operator || nextCard?.NodeType == ItemNodeType.Operator)
                {
                    zone.Entrants.RemoveAt(i);
                    Destroy(card.gameObject);
                    layoutChanged = true;
                }
            }
        }

        // PASS 2: Auto-Spawn operators between nodes that require them
        for (int i = 0; i < zone.Entrants.Count - 1; i++)
        {
            if (zone.Entrants[i] is EntityCard card1 && zone.Entrants[i + 1] is EntityCard card2)
            {
                // If two BaseItems (or other payloads) are touching, force an Operator between them
                if (card1.NodeType == ItemNodeType.BaseItem && card2.NodeType == ItemNodeType.BaseItem)
                {
                    EntityCard opCard = CreateEntityCard(ItemNodeType.Operator) as EntityCard;
                    opCard.MechanicData.PayloadString = "#"; // Default to AND

                    opCard.transform.SetParent(zone.transform, false);
                    opCard.transform.SetSiblingIndex(card2.transform.GetSiblingIndex());

                    zone.Entrants.Insert(i + 1, opCard);
                    layoutChanged = true;
                    i++; // Skip the newly inserted operator
                }
            }
        }

        if (layoutChanged) RefreshSidebar();
    }
    public ReorderableItem CreateEntityCard(ItemNodeType nodeType)
    {
        var def = NodeRegistry.Get(nodeType);

        // 1. Create the base card layout and components
        EntityCard entityCard = CreateRootCard(nodeType, def.GetColor());
        RectTransform cardRect = entityCard.GetComponent<RectTransform>();

        // 2. Construct the header container
        RectTransform headerRow = CreateHeaderRow(cardRect);

        // 3. Populate header elements
        AddTitle(headerRow, entityCard);

        if (nodeType == ItemNodeType.Hat)
        {
            entityCard.MechanicData.PayloadData = new HeroData();
            (entityCard.MechanicData.PayloadData as HeroData).InitializeDiceFaces();
            entityCard.MechanicData.Prefix = "hat"; // Ensure the prefix is correct
        }

        if (def.HasDeleteButton)
        {
            AddDeleteButton(headerRow, entityCard);
        }

        // 4. Setup payload drop zone if required
        if (def.HasPayloadPort)
        {
            AddPayloadPort(cardRect, entityCard);
        }

        return entityCard;
    }
    private EntityCard CreateRootCard(ItemNodeType nodeType, Color backgroundColor)
    {
        RectTransform cardRect = CreateRect($"Card_{nodeType}", null);
        cardRect.sizeDelta = new Vector2(280f, 80f);
        cardRect.localScale = Vector3.one;

        AddColor(cardRect, backgroundColor);

        // CanvasGroup initialization
        var canvasGroup = cardRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        var entityCard = cardRect.gameObject.AddComponent<EntityCard>();
        entityCard.NodeType = nodeType;

        // Outer layout settings
        var vLayout = cardRect.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.spacing = 10;
        vLayout.childControlHeight = true;
        vLayout.childControlWidth = true;

        var fitter = cardRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return entityCard;
    }
    private RectTransform CreateHeaderRow(RectTransform parent)
    {
        RectTransform headerRow = CreateRect("HeaderRow", parent);
        var hLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = 10;

        return headerRow;
    }
    private void AddTitle(RectTransform headerRow, EntityCard entityCard)
    {
        RectTransform titleRect = CreateRect("Title", headerRow);
        var titleText = titleRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.fontSize = 18;
        titleText.color = Color.white;
        titleText.fontStyle = TMPro.FontStyles.Bold;

        var titleLayout = titleRect.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        entityCard.gameObject.AddComponent<UpdateTitleUtility>().Init(entityCard, titleText);
    }
    private void AddDeleteButton(RectTransform headerRow, EntityCard entityCard)
    {
        GameObject deleteBtnObj;
        if (buttonPrefab != null)
        {
            deleteBtnObj = Instantiate(buttonPrefab, headerRow);
            var img = deleteBtnObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0.7f, 0.2f, 0.2f);
            }
        }
        else
        {
            deleteBtnObj = new GameObject("DeleteBtn");
            deleteBtnObj.transform.SetParent(headerRow, false);
            var img = deleteBtnObj.AddComponent<Image>();
            img.color = new Color(0.7f, 0.2f, 0.2f);
        }

        var btnLayout = deleteBtnObj.GetComponent<LayoutElement>() ?? deleteBtnObj.AddComponent<LayoutElement>();
        btnLayout.preferredWidth = 25f;
        btnLayout.preferredHeight = 25f;
        btnLayout.flexibleWidth = 0;
        btnLayout.flexibleHeight = 0;

        var deleteBtn = deleteBtnObj.GetComponent<UnityEngine.UI.Button>() ?? deleteBtnObj.AddComponent<UnityEngine.UI.Button>();

        var btnText = deleteBtnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (btnText == null)
        {
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(deleteBtnObj.transform, false);
            btnText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
        }
        btnText.text = " X";
        btnText.fontSize = 12;
        btnText.color = Color.white;
        btnText.fontStyle = TMPro.FontStyles.Bold;
        btnText.alignment = TMPro.TextAlignmentOptions.Center;

        deleteBtn.onClick.RemoveAllListeners();
        deleteBtn.onClick.AddListener(() => DeleteCard(entityCard));
    }
    private void AddPayloadPort(RectTransform parent, EntityCard entityCard)
    {
        RectTransform portRect = CreateRect("PayloadPort", parent);
        AddColor(portRect, new Color(0, 0, 0, 0.3f));

        var portLayout = portRect.gameObject.AddComponent<VerticalLayoutGroup>();
        portLayout.padding = new RectOffset(15, 15, 15, 15);
        portLayout.spacing = 5;
        portLayout.childControlHeight = true;
        portLayout.childControlWidth = true;

        var portFitter = portRect.gameObject.AddComponent<ContentSizeFitter>();
        portFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        portRect.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

        var zone = portRect.gameObject.AddComponent<ReorderableZone>();
        zone.SetCanvas(_cachedCanvas);
        zone.OnZoneChanged += RefreshSidebar;

        entityCard.PayloadPort = zone;
    }
}
