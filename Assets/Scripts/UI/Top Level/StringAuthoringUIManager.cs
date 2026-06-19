using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; // Assuming TextMeshPro is used for clarity, replace with standard Text if needed

public class EntityCard : ReorderableItem, IPointerClickHandler
{
    public string CardName { get; set; }
    public ReorderableZone PayloadPort { get; set; }

    // We can add data fields here that the string will eventually need
    public int Tier = 1;
    public string ImageRef = "";

    public void OnPointerClick(PointerEventData eventData)
    {
        // Don't trigger if we are currently dragging
        if (eventData.dragging) return;

        // Tell the UI Manager to select this card
        StringAuthoringUIManager.Instance?.SelectCard(this);
    }
}

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class StringAuthoringUIManager : MonoBehaviour
{
    public static StringAuthoringUIManager Instance { get; private set; }
    private TextMeshProUGUI _sidebarText;

    private RectTransform _rootCanvasRect;
    private Canvas _cachedCanvas;

    // References to our major panels
    public RectTransform BreadcrumbPanel { get; private set; }
    public RectTransform SidebarContent { get; private set; }
    public RectTransform MainCanvasContent { get; private set; }
    public RectTransform InspectorContent { get; private set; }

    [Header("Configuration")]
    public float topBarHeight = 50f;
    public float sidebarWidth = 300f;
    public float inspectorWidth = 300f;

    private ReorderableZone reorderableZone;

    private void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        // Ensure we have an event system for your drag logic
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        BuildWorkspace();

        PopulateDemoData();
        RefreshSidebar(); // Initial draw
    }

    private void OnDestroy()
    {
        // --- ALWAYS CLEAN UP STATIC EVENTS TO PREVENT MEMORY LEAKS ---
        reorderableZone.OnZoneChanged -= RefreshSidebar;
    }

    public void BuildWorkspace()
    {
        _rootCanvasRect = GetComponent<RectTransform>();
        _cachedCanvas = GetComponent<Canvas>();

        // 1. Breadcrumb Top Bar (Stretches horizontally, fixed height, anchored top)
        BreadcrumbPanel = CreateRect("BreadcrumbBar", _rootCanvasRect);
        SetAnchors(BreadcrumbPanel, 0, 1, 1, 1);
        BreadcrumbPanel.pivot = new Vector2(0.5f, 1f);
        BreadcrumbPanel.offsetMin = new Vector2(0, -topBarHeight);
        BreadcrumbPanel.offsetMax = new Vector2(0, 0);
        AddColor(BreadcrumbPanel, new Color(0.15f, 0.15f, 0.15f)); // Dark gray
        var bcLayout = BreadcrumbPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        bcLayout.padding = new RectOffset(15, 15, 0, 0);
        bcLayout.childAlignment = TextAnchor.MiddleLeft;

        // 2. Structural Sidebar (Anchored Left, stretches vertically below Top Bar)
        RectTransform sidebarBG = CreateRect("SidebarArea", _rootCanvasRect);
        SetAnchors(sidebarBG, 0, 0, 0, 1);
        sidebarBG.pivot = new Vector2(0, 1);
        sidebarBG.offsetMin = new Vector2(0, 0);
        sidebarBG.offsetMax = new Vector2(sidebarWidth, -topBarHeight);
        AddColor(sidebarBG, new Color(0.1f, 0.1f, 0.1f));
        SidebarContent = CreateScrollView(sidebarBG, "SidebarScrollView");

        // FIX 1: Use GETComponent instead of ADDComponent because CreateScrollView already added it
        var sidebarLayout = SidebarContent.gameObject.GetComponent<VerticalLayoutGroup>();
        sidebarLayout.padding = new RectOffset(15, 15, 15, 15);
        sidebarLayout.spacing = 5;
        sidebarLayout.childControlHeight = true;
        sidebarLayout.childControlWidth = true;
        sidebarLayout.childForceExpandHeight = false;

        var sidebarFitter = SidebarContent.gameObject.AddComponent<ContentSizeFitter>();
        sidebarFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 3. Main Drill-Down Canvas (Squeezed between Sidebar and Inspector)
        RectTransform mainAreaBG = CreateRect("MainCanvasArea", _rootCanvasRect);
        SetAnchors(mainAreaBG, 0, 0, 1, 1);
        mainAreaBG.pivot = new Vector2(0, 1);
        mainAreaBG.offsetMin = new Vector2(sidebarWidth, 0);
        mainAreaBG.offsetMax = new Vector2(-inspectorWidth, -topBarHeight); // Squeeze right side
        AddColor(mainAreaBG, new Color(0.2f, 0.2f, 0.2f));
        MainCanvasContent = CreateScrollView(mainAreaBG, "MainScrollView", true);

        // 4. Inspector / Properties Panel (Anchored Right)
        RectTransform inspectorBG = CreateRect("InspectorArea", _rootCanvasRect);
        SetAnchors(inspectorBG, 1, 0, 1, 1);
        inspectorBG.pivot = new Vector2(1, 1);
        inspectorBG.offsetMin = new Vector2(-inspectorWidth, 0);
        inspectorBG.offsetMax = new Vector2(0, -topBarHeight);
        AddColor(inspectorBG, new Color(0.12f, 0.12f, 0.12f));
        InspectorContent = CreateScrollView(inspectorBG, "InspectorScrollView");

        // Add padding to inspector layout
        // FIX 2: Use GetComponent here too, as CreateScrollView already added the VerticalLayoutGroup
        var inspLayout = InspectorContent.GetComponent<VerticalLayoutGroup>();
        inspLayout.padding = new RectOffset(20, 20, 20, 20);
        inspLayout.spacing = 15;

        // Setup Main Canvas Layout to stack root items vertically with padding
        // FIX 3: Use GetComponent here as well for safety consistency
        var mainLayout = MainCanvasContent.gameObject.GetComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(40, 40, 40, 40);
        mainLayout.spacing = 20;

        // The MainCanvasContent acts as the absolute ROOT payload port
        ReorderableZone rootZone = MainCanvasContent.gameObject.AddComponent<ReorderableZone>();
        reorderableZone = rootZone;
        reorderableZone.SetCanvas(_cachedCanvas);

        // Listen to the generic static zone change event instead of local instance action
        reorderableZone.OnZoneChanged += RefreshSidebar;
    }

    public ReorderableItem CreateEntityCard(string cardName, Color cardColor)
    {
        // 1. The Card Object
        RectTransform cardRect = CreateRect($"Card_{cardName}", null);
        AddColor(cardRect, cardColor);

        // Explicitly add CanvasGroup to ensure Awake finds it cleanly during setup
        cardRect.gameObject.AddComponent<CanvasGroup>();

        // CHANGE: Add the CONCRETE class EntityCard, not the abstract ReorderableItem
        var reorderableItem = cardRect.gameObject.AddComponent<EntityCard>();

        // Card Layout - vertically fits its contents (Title + Ports)
        var vLayout = cardRect.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.spacing = 10;
        vLayout.childControlHeight = true;
        vLayout.childControlWidth = true;
        vLayout.childForceExpandHeight = false;

        var fitter = cardRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 2. Title Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardRect, false);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = cardName;
        titleText.fontSize = 18;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyles.Bold;

        // 3. Payload Port (The Reorderable Zone nested inside the card)
        RectTransform portRect = CreateRect("PayloadPort", cardRect);
        AddColor(portRect, new Color(0, 0, 0, 0.3f)); // Dark translucent inset

        var portLayout = portRect.gameObject.AddComponent<VerticalLayoutGroup>();
        portLayout.padding = new RectOffset(15, 15, 15, 15);
        portLayout.spacing = 5;
        portLayout.childControlHeight = true;
        portLayout.childControlWidth = true;
        portLayout.childForceExpandHeight = false;

        var portFitter = portRect.gameObject.AddComponent<ContentSizeFitter>();
        portFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Ensure minimum size so you can drag things into an empty port
        var minSize = portRect.gameObject.AddComponent<LayoutElement>();
        minSize.minHeight = 40f;

        // This makes the internal area act as a drop zone
        var zone = portRect.gameObject.AddComponent<ReorderableZone>();
        zone.SetCanvas(_cachedCanvas);
        zone.OnZoneChanged += RefreshSidebar;

        reorderableItem.PayloadPort = zone;

        return reorderableItem;
    }

    public void SelectCard(EntityCard card)
    {
        // Clear the current inspector
        foreach (Transform child in InspectorContent)
        {
            Destroy(child.gameObject);
        }

        // --- Build Header ---
        // FIX: Use CreateRect instead of new GameObject
        RectTransform headerRect = CreateRect("InspectorHeader", InspectorContent);
        var headerText = headerRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        headerText.text = $"Inspecting: <b>{card.CardName}</b>";
        headerText.fontSize = 18;
        headerText.color = Color.white;

        // --- Add 'Name' Input Field ---
        CreateInspectorInputField("Name", card.CardName, (newValue) => {
            card.CardName = newValue;
            RefreshSidebar();
        });

        // --- Add 'Tier' Input Field ---
        CreateInspectorInputField("Tier", card.Tier.ToString(), (newValue) => {
            if (int.TryParse(newValue, out int t)) card.Tier = t;
        });

        // --- Add 'Image Ref' Input Field ---
        CreateInspectorInputField("Image Ref", card.ImageRef, (newValue) => {
            card.ImageRef = newValue;
        });
    }

    private void CreateInspectorInputField(string label, string initialValue, UnityEngine.Events.UnityAction<string> onValueChanged)
    {
        // Container
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlHeight = true; layout.childControlWidth = true;
        container.gameObject.AddComponent<LayoutElement>().minHeight = 35f;

        // Label
        // FIX: Use CreateRect instead of new GameObject
        RectTransform labelRect = CreateRect("Label", container);
        var labelText = labelRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = 14;
        labelText.color = Color.grey;
        labelRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        // Input Field Container
        // FIX: Use CreateRect instead of new GameObject
        RectTransform inputRect = CreateRect("InputField", container);
        var inputImage = inputRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f);
        var inputField = inputRect.gameObject.AddComponent<TMPro.TMP_InputField>();

        // Text child for the input
        // FIX: Use CreateRect instead of new GameObject
        RectTransform textRect = CreateRect("Text", inputRect);
        SetAnchors(textRect, 0, 0, 1, 1);

        // Add a small 5-pixel padding on the left/right inside the input box so text doesn't touch the edges
        textRect.offsetMin = new Vector2(5, 0);
        textRect.offsetMax = new Vector2(-5, 0);

        var tmpText = textRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.fontSize = 14;
        tmpText.color = Color.white;
        tmpText.alignment = TMPro.TextAlignmentOptions.Left;

        // Bind the text component to the InputField
        inputField.textComponent = tmpText;

        inputField.text = initialValue;
        inputField.onValueChanged.AddListener(onValueChanged);
    }

    private void PopulateDemoData()
    {
        // Get the root zone attached to our main canvas content
        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();

        // Create Root Card
        ReorderableItem itemCard = CreateEntityCard("Item: Emerald Eye", new Color(0.6f, 0.5f, 0.1f)); // Gold
        rootZone.AddEntrant(itemCard);

        // Get the internal payload port of the item card we just created
        ReorderableZone itemPayloadPort = itemCard.GetComponentInChildren<ReorderableZone>();

        // Create a Nested Card and put it inside the item's port
        ReorderableItem heroCard = CreateEntityCard("Hero: Statue", new Color(0.1f, 0.4f, 0.6f)); // Blue
        itemPayloadPort.AddEntrant(heroCard);

        // Get the internal payload port of the hero card
        ReorderableZone heroPayloadPort = heroCard.GetComponentInChildren<ReorderableZone>();

        // Create a deeply nested card
        ReorderableItem modifierCard = CreateEntityCard("Modifier: jinx", new Color(0.2f, 0.6f, 0.2f)); // Green
        heroPayloadPort.AddEntrant(modifierCard);
    }

    // --- UTILITIES FOR PURE CODE UI GENERATION ---

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
        // 1. Clear the old tree
        foreach (Transform child in SidebarContent)
        {
            Destroy(child.gameObject);
        }

        // 2. Add a Header
        RectTransform headerRect = CreateRect("TreeHeader", SidebarContent);
        var headerText = headerRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        headerText.text = "<b>String Structure</b>";
        headerText.fontSize = 16;
        headerText.color = Color.white;
        headerRect.gameObject.AddComponent<LayoutElement>().minHeight = 30f;

        // 3. Recursively draw the new interactive tree
        ReorderableZone rootZone = MainCanvasContent.GetComponent<ReorderableZone>();
        foreach (var entrant in rootZone.Entrants)
        {
            if (entrant is EntityCard card)
            {
                AppendToSidebar(card, 0);
            }
        }
    }

    private void AppendToSidebar(EntityCard card, int indentLevel)
    {
        // Create a clickable row for this item
        RectTransform rowRect = CreateRect($"Row_{card.CardName}", SidebarContent);
        var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;

        // Push the text to the right based on how deep we are nested
        layout.padding = new RectOffset(indentLevel * 20, 0, 0, 0);

        rowRect.gameObject.AddComponent<LayoutElement>().minHeight = 25f;

        // Make the row background a clickable button
        var bgImage = rowRect.gameObject.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(1, 1, 1, 0.05f); // Very faint background
        var button = rowRect.gameObject.AddComponent<UnityEngine.UI.Button>();

        // When clicked, send this card to the right-side inspector
        button.onClick.AddListener(() => {
            SelectCard(card);
        });

        // Add the text label
        RectTransform textRect = CreateRect("Label", rowRect);
        var text = textRect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        string prefix = indentLevel == 0 ? "■ " : "-> ";
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

}