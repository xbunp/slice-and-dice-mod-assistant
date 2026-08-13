using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ==========================================
// 1. DATA ENUMS & BACKEND STATE
// ==========================================

public enum PhaseNodeType
{
    None,
    PhaseRoot,          // Top-level containers (ch, ph, phmp, phi)

    // Core Engine Phases (The big alphabet)
    PhaseMessage,       // ph.4
    PhaseHeroChange,    // ph.5
    PhaseReset,         // ph.6
    PhaseItemCombine,   // ph.7
    PhasePositionSwap,  // ph.8
    PhaseChallenge,     // ph.9
    PhaseBoolean,       // ph.b
    PhaseChoice,        // ph.c
    PhaseRunEnd,        // ph.e
    PhaseLinked,        // ph.l
    PhaseRandomReveal,  // ph.r
    PhaseSeq,           // ph.s
    PhaseTrade,         // ph.t
    PhaseLevelEnd,      // ph.2
    PhaseGenTransform,  // ph.g
    PhaseBoolean2,      // ph.z
    PhaseCombat,        // ph.0, 1, 3, d

    // Reward Tags (For Choosable / Trade / Choice / Boolean payloads)
    RewardStandard,     // m, i, l, g
    RewardRandom,       // r
    RewardRandomRange,  // q
    RewardOr,           // o 
    RewardValue,        // v
    RewardReplace,      // p
    RewardEnu,          // e
    RewardSkip,         // s

    // Formatting & Wrappers
    ChoiceOption,       // @1, @2 (For Boolean/Seq buttons)
    ActionBlock,        // !m(...) 
    RawString
}

public class PhaseCardData
{
    // A highly reusable set of strings/ints that map differently based on Node Type
    public string PrimaryText = "";
    public string SecondaryText = "";
    public string TertiaryText = "";

    public int Num1 = 1;
    public int Num2 = 1;
    public int Num3 = 1;

    // Default Initialization states based on type
    public PhaseCardData(PhaseNodeType type)
    {
        if (type == PhaseNodeType.PhaseRoot) PrimaryText = "ph.!";
        else if (type == PhaseNodeType.RewardStandard) PrimaryText = "m";
        else if (type == PhaseNodeType.PhaseCombat) PrimaryText = "0";
    }
}

// ==========================================
// 2. THE VISUAL NODE (PHASE CARD)
// ==========================================

public class PhaseCard : ReorderableItem, IPointerClickHandler
{
    public PhaseNodeType NodeType;
    public PhaseCardData Data;
    public ReorderableZone PayloadPort { get; set; }

    public string CardName => PhaseNodeRegistry.Get(NodeType).GetTitle(this);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        PhasesUI.Instance?.SelectCard(this);
    }
    public string Compile() => PhaseSyntaxCompiler.CompileCard(this);
}

// ==========================================
// 3. THE UI MANAGER
// ==========================================

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class PhasesUI : RootUI
{
    public class UpdateTitleUtility : MonoBehaviour
    {
        PhaseCard card; TextMeshProUGUI txt;
        public void Init(PhaseCard c, TextMeshProUGUI t) { card = c; txt = t; }
        void Update() { if (card != null && txt != null) txt.text = card.CardName; }
    }

    public static PhasesUI Instance { get; private set; }

    [Header("Configuration")]
    public float topBarHeight = 50f;
    public float sidebarWidth = 300f;
    public float inspectorWidth = 400f;

    public RectTransform BreadcrumbPanel { get; private set; }
    public RectTransform SidebarContent { get; private set; }
    public RectTransform MainCanvasContent { get; private set; }
    public RectTransform InspectorContent { get; private set; }

    private TMP_InputField _compiledOutputField;
    private ReorderableZone _rootZone;
    private PhaseCard _selectedCard;

    private List<PhaseNodeType> _dropdownNodeTypes = new List<PhaseNodeType>();
    private bool _pendingCompile = false;
    private float _compileTimer = 0f;

    private GameObject buttonPrefab => FullScreenUIGenerator.Instance.buttonPrefab;
    private GameObject dropdownPrefab => FullScreenUIGenerator.Instance.dropdownPrefab;
    private GameObject inputFieldPrefab => FullScreenUIGenerator.Instance.inputFieldPrefab;
    public ReorderableZone GetRootZone() => _rootZone;

    private void Awake() => Instance = this;

    protected override void BuildUIAndBind()
    {
        generatedScreen = uiGenerator.SetupScreen(new List<ColumnSpec>(), false);
        BuildTopBar();
        BuildSidebar();
        BuildMainCanvas();
        BuildInspector();

        _rootZone = MainCanvasContent.gameObject.AddComponent<ReorderableZone>();
        _rootZone.SetCanvas(FullScreenUIGenerator.Instance.canvas);
        _rootZone.OnZoneChanged += RefreshSidebar;
    }

    public void AutoCompile()
    {
        _pendingCompile = true;
        _compileTimer = 0f;
    }

    private void Update()
    {
        if (_pendingCompile)
        {
            _compileTimer += Time.deltaTime;
            if (_compileTimer >= 0.15f)
            {
                _pendingCompile = false;
                _compileTimer = 0f;
                if (_compiledOutputField != null && _rootZone != null)
                {
                    _compiledOutputField.text = PhaseSyntaxCompiler.CompileZone(_rootZone.Entrants.Cast<PhaseCard>(), "");
                }
            }
        }
    }

    private void BuildTopBar()
    {
        BreadcrumbPanel = CreateRect("ToolbarBar", generatedScreen.RootWrapper);
        SetAnchors(BreadcrumbPanel, 0, 1, 1, 1);
        BreadcrumbPanel.pivot = new Vector2(0.5f, 1);
        BreadcrumbPanel.offsetMin = new Vector2(0, -topBarHeight);
        BreadcrumbPanel.offsetMax = Vector2.zero;
        AddColor(BreadcrumbPanel, new Color(0.12f, 0.2f, 0.15f));

        if (inputFieldPrefab != null)
        {
            GameObject outputObj = Instantiate(inputFieldPrefab, BreadcrumbPanel);
            RectTransform outputRect = outputObj.GetComponent<RectTransform>();
            SetAnchors(outputRect, 0.02f, 0.1f, 0.98f, 0.9f);
            outputRect.offsetMin = Vector2.zero;
            outputRect.offsetMax = Vector2.zero;

            _compiledOutputField = outputObj.GetComponent<TMP_InputField>();
            _compiledOutputField.readOnly = true;
        }
    }
    private void BuildSidebar()
    {
        RectTransform sidebarBG = CreateRect("SidebarArea", generatedScreen.RootWrapper);
        SetAnchors(sidebarBG, 0, 0, 0, 1);
        sidebarBG.pivot = new Vector2(0, 1);
        sidebarBG.offsetMin = Vector2.zero;
        sidebarBG.offsetMax = new Vector2(sidebarWidth, -topBarHeight);
        AddColor(sidebarBG, new Color(0.1f, 0.1f, 0.12f));

        if (dropdownPrefab != null)
        {
            GameObject ddObj = Instantiate(dropdownPrefab, sidebarBG);
            RectTransform ddRect = ddObj.GetComponent<RectTransform>();
            SetAnchors(ddRect, 0.05f, 0.92f, 0.95f, 0.98f);
            ddRect.offsetMin = Vector2.zero;
            ddRect.offsetMax = Vector2.zero;

            var nodeDropdown = ddObj.GetComponent<TMP_Dropdown>();
            nodeDropdown.ClearOptions();

            List<string> options = new List<string> { "-- Add Node --" };
            _dropdownNodeTypes.Clear();
            _dropdownNodeTypes.Add(PhaseNodeType.None);

            foreach (PhaseNodeDef nodeDef in PhaseNodeRegistry.GetAll())
            {
                options.Add(nodeDef.NodeNiceName);
                _dropdownNodeTypes.Add(nodeDef.NodeType);
            }

            nodeDropdown.AddOptions(options);
            nodeDropdown.onValueChanged.AddListener((idx) => OnAddNodeSelected(idx, nodeDropdown));
        }

        if (buttonPrefab != null)
        {
            GameObject importObj = Instantiate(buttonPrefab, sidebarBG);
            var importBtn = importObj.GetComponent<Button>();
            importObj.GetComponentInChildren<TextMeshProUGUI>().text = "Import";

            RectTransform importRect = importObj.GetComponent<RectTransform>();
            SetAnchors(importRect, 0.05f, 0.86f, 0.95f, 0.91f);
            importRect.offsetMin = Vector2.zero;
            importRect.offsetMax = Vector2.zero;

            importBtn.onClick.AddListener(ImportFromClipboard);
        }

        RectTransform scrollView = CreateRect("SidebarScrollViewContainer", sidebarBG);
        SetAnchors(scrollView, 0, 0, 1, 0.9f);
        scrollView.offsetMin = Vector2.zero;
        scrollView.offsetMax = Vector2.zero;

        SidebarContent = CreateScrollView(scrollView, "SidebarContent");

        var layout = SidebarContent.gameObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(15, 15, 15, 15);
        layout.spacing = 5;
    }
    private void BuildMainCanvas()
    {
        RectTransform mainAreaBG = CreateRect("MainCanvasArea", generatedScreen.RootWrapper);
        SetAnchors(mainAreaBG, 0, 0, 1, 1);
        mainAreaBG.pivot = new Vector2(0, 1);
        mainAreaBG.offsetMin = new Vector2(sidebarWidth, 0);
        mainAreaBG.offsetMax = new Vector2(-inspectorWidth, -topBarHeight);
        AddColor(mainAreaBG, new Color(0.15f, 0.18f, 0.22f));

        MainCanvasContent = CreateScrollView(mainAreaBG, "MainScrollView", true);
        var mainLayout = MainCanvasContent.gameObject.GetComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(40, 40, 40, 40);
        mainLayout.spacing = 20;
    }
    private void BuildInspector()
    {
        RectTransform inspectorBG = CreateRect("InspectorArea", generatedScreen.RootWrapper);
        SetAnchors(inspectorBG, 1, 0, 1, 1);
        inspectorBG.pivot = new Vector2(1, 1);
        inspectorBG.offsetMin = new Vector2(-inspectorWidth, 0);
        inspectorBG.offsetMax = new Vector2(0, -topBarHeight);
        AddColor(inspectorBG, new Color(0.12f, 0.12f, 0.14f));

        InspectorContent = CreateScrollView(inspectorBG, "InspectorScrollView");
        var inspLayout = InspectorContent.GetComponent<VerticalLayoutGroup>();
        inspLayout.padding = new RectOffset(20, 20, 20, 20);
        inspLayout.spacing = 15;
    }

    private void OnAddNodeSelected(int index, TMP_Dropdown dropdown)
    {
        if (index == 0 || _rootZone == null) return;
        PhaseCard newCard = CreatePhaseCard(_dropdownNodeTypes[index]);
        if (newCard != null) _rootZone.AddEntrant(newCard);

        dropdown.SetValueWithoutNotify(0);
        RefreshSidebar();
        AutoCompile();
    }
    public PhaseCard CreatePhaseCard(PhaseNodeType type)
    {
        var def = PhaseNodeRegistry.Get(type);

        RectTransform cardRect = CreateRect($"PhaseCard_{type}", null);
        cardRect.sizeDelta = new Vector2(280f, 80f);
        AddColor(cardRect, def.GetColor());
        cardRect.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = true;

        var vLayout = cardRect.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.spacing = 10;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;

        cardRect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        PhaseCard card = cardRect.gameObject.AddComponent<PhaseCard>();
        card.NodeType = type;
        card.Data = new PhaseCardData(type);

        RectTransform headerRow = CreateRect("HeaderRow", cardRect);
        var hLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;

        var titleText = CreateRect("Title", headerRow).gameObject.AddComponent<TextMeshProUGUI>();
        titleText.color = Color.white;
        titleText.fontStyle = TMPro.FontStyles.Bold;

        card.gameObject.AddComponent<UpdateTitleUtility>().Init(card, titleText);

        if (buttonPrefab != null)
        {
            GameObject deleteBtnObj = Instantiate(buttonPrefab, headerRow);
            var btnLayout = deleteBtnObj.GetComponent<LayoutElement>() ?? deleteBtnObj.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = 25f;
            btnLayout.preferredHeight = 25f;
            btnLayout.flexibleWidth = 0f;
            btnLayout.flexibleHeight = 0f;

            var deleteBtn = deleteBtnObj.GetComponent<Button>();
            deleteBtnObj.GetComponentInChildren<TextMeshProUGUI>().text = "X";
            deleteBtn.onClick.AddListener(() => DeleteCard(card));
        }

        if (def.HasPayloadPort)
        {
            RectTransform portRect = CreateRect("PayloadPort", cardRect);
            AddColor(portRect, new Color(0, 0, 0, 0.3f));

            var portLayout = portRect.gameObject.AddComponent<VerticalLayoutGroup>();
            portLayout.padding = new RectOffset(15, 15, 15, 15);
            portLayout.spacing = 10;
            portLayout.childControlWidth = true;
            portLayout.childControlHeight = true;

            portRect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            portRect.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

            var zone = portRect.gameObject.AddComponent<ReorderableZone>();
            zone.SetCanvas(FullScreenUIGenerator.Instance.canvas);
            zone.OnZoneChanged += () => { RefreshSidebar(); AutoCompile(); };
            card.PayloadPort = zone;
        }

        return card;
    }
    public void SelectCard(PhaseCard card)
    {
        _selectedCard = card;
        foreach (Transform child in InspectorContent) Destroy(child.gameObject);
        if (card != null) PhaseNodeRegistry.Get(card.NodeType).DrawInspector(this, card);
    }
    public void DeleteCard(PhaseCard card)
    {
        if (_selectedCard == card)
        {
            _selectedCard = null;
            foreach (Transform child in InspectorContent) Destroy(child.gameObject);
        }

        var parentZone = card.transform.parent?.GetComponent<ReorderableZone>();

        if (parentZone != null)
            parentZone.Entrants.Remove(card);
        else if (_rootZone != null)
            _rootZone.Entrants.Remove(card);

        Destroy(card.gameObject);
        RefreshSidebar();
        AutoCompile();
    }
    public void RefreshSidebar()
    {
        foreach (Transform child in SidebarContent) Destroy(child.gameObject);

        if (_rootZone != null)
        {
            foreach (var entrant in _rootZone.Entrants)
            {
                if (entrant is PhaseCard card) AppendToSidebar(card, 0);
            }
        }
    }
    private void AppendToSidebar(PhaseCard card, int indentLevel)
    {
        RectTransform rowRect = CreateRect($"Row_{card.CardName}", SidebarContent);
        var layout = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(indentLevel * 10, 0, 0, 0);
        rowRect.gameObject.AddComponent<LayoutElement>().minHeight = 25f;

        rowRect.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectCard(card));

        RectTransform textRect = CreateRect("Label", rowRect);
        var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = $" {(indentLevel == 0 ? "■" : "-")} {card.CardName}";
        text.fontSize = 14;
        text.color = new Color(0.8f, 0.8f, 0.8f);

        if (card.PayloadPort != null)
        {
            foreach (var childItem in card.PayloadPort.Entrants)
            {
                if (childItem is PhaseCard childCard) AppendToSidebar(childCard, indentLevel + 1);
            }
        }
    }

    // ==========================================
    // UI LAYOUT GENERATION UTILITIES
    // ==========================================

    private RectTransform CreateRect(string name, Transform parent)
    {
        // Safe GameObject creation to ensure RectTransform is present implicitly
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
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
        scrollRoot.offsetMin = Vector2.zero;
        scrollRoot.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = useHorizontal;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 0.18f;

        // Viewport (Masking)
        RectTransform viewport = CreateRect("Viewport", scrollRoot);
        SetAnchors(viewport, 0, 0, 1, 1);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.pivot = new Vector2(0, 1);

        Image vpImage = viewport.gameObject.AddComponent<Image>();
        vpImage.color = new Color(1, 1, 1, 0.01f); // nearly invisible mask

        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        RectTransform content = CreateRect("Content", viewport);
        SetAnchors(content, 0, 1, 1, 1); // Anchored to top, stretches width
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

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
    public void CreateInspectorInputField(string label, string initialValue, UnityEngine.Events.UnityAction<string> onValueChanged)
    {
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        container.gameObject.AddComponent<HorizontalLayoutGroup>();
        container.gameObject.AddComponent<LayoutElement>().minHeight = 35f;

        var lblRect = CreateRect("Label", container);
        var lblTxt = lblRect.gameObject.AddComponent<TextMeshProUGUI>();
        lblTxt.text = label;
        lblTxt.fontSize = 14;
        lblTxt.color = Color.grey;
        lblRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        GameObject inputObj = Instantiate(inputFieldPrefab, container);
        var inputField = inputObj.GetComponent<TMP_InputField>();
        inputField.text = initialValue;
        inputField.onValueChanged.AddListener(onValueChanged);
    }
    public void CreateInspectorDropdown(string label, List<string> options, int currentIndex, UnityEngine.Events.UnityAction<int> onValueChanged)
    {
        RectTransform container = CreateRect($"Field_{label}", InspectorContent);
        container.gameObject.AddComponent<HorizontalLayoutGroup>();
        container.gameObject.AddComponent<LayoutElement>().minHeight = 35f;

        var lblRect = CreateRect("Label", container);
        var lblTxt = lblRect.gameObject.AddComponent<TextMeshProUGUI>();
        lblTxt.text = label;
        lblTxt.fontSize = 14;
        lblTxt.color = Color.grey;
        lblRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

        GameObject ddObj = Instantiate(dropdownPrefab, container);
        var tmpDrop = ddObj.GetComponent<TMP_Dropdown>();
        tmpDrop.ClearOptions();
        tmpDrop.AddOptions(options);
        tmpDrop.SetValueWithoutNotify(currentIndex);
        tmpDrop.onValueChanged.AddListener(onValueChanged);
    }

    public void ClearWorkspace()
    {
        if (InspectorContent != null)
            foreach (Transform child in InspectorContent) Destroy(child.gameObject);

        if (MainCanvasContent != null)
        {
            if (_rootZone != null) _rootZone.Entrants.Clear();
            foreach (Transform child in MainCanvasContent)
            {
                if (child.GetComponent<PhaseCard>() != null)
                    Destroy(child.gameObject);
            }
        }

        if (_compiledOutputField != null) _compiledOutputField.text = string.Empty;
        _selectedCard = null;
    }
    public void ImportFromClipboard()
    {
        string clip = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clip))
        {
            Debug.LogWarning("Clipboard is empty.");
            return;
        }
        LoadPhaseStringIntoUI(clip);
    }
    public void LoadPhaseStringIntoUI(string fullString)
    {
        // The Decompiler will handle clearing and building
        PhaseDecompiler.Decompile(fullString, this);
        RefreshSidebar();
        AutoCompile();
    }
}

// ==========================================
// 4. REGISTRY & AUTHORING DEFINITIONS
// ==========================================

public abstract class PhaseNodeDef
{
    public virtual string NodeNiceName { get; protected set; } = "Unnamed Phase Node";
    public abstract PhaseNodeType NodeType { get; }
    public abstract Color GetColor();
    public abstract string GetTitle(PhaseCard card);
    public virtual bool HasPayloadPort => false;
    public abstract void DrawInspector(PhasesUI ui, PhaseCard card);
}

public static class PhaseNodeRegistry
{
    private static Dictionary<PhaseNodeType, PhaseNodeDef> _nodes;
    public static void EnsureInitialized()
    {
        if (_nodes != null) return;
        _nodes = new Dictionary<PhaseNodeType, PhaseNodeDef>();

        // Root Types
        Register(new PhaseRootNodeDef());
        Register(new PhaseMessageNodeDef());
        Register(new PhaseHeroChangeNodeDef());
        Register(new PhaseResetNodeDef());
        Register(new PhaseItemCombineNodeDef());
        Register(new PhasePositionSwapNodeDef());
        Register(new PhaseChallengeNodeDef());
        Register(new PhaseBooleanNodeDef());
        Register(new PhaseBoolean2NodeDef());
        Register(new PhaseChoiceNodeDef());
        Register(new PhaseRunEndNodeDef());
        Register(new PhaseLinkedNodeDef());
        Register(new PhaseRandomRevealNodeDef());
        Register(new PhaseSeqNodeDef());
        Register(new PhaseTradeNodeDef());
        Register(new PhaseLevelEndNodeDef());
        Register(new PhaseGenTransformNodeDef());
        Register(new PhaseCombatNodeDef());

        // Payload Options
        Register(new StandardRewardNodeDef());
        Register(new RandomRewardNodeDef());
        Register(new RandomRangeNodeDef());
        Register(new OrNodeDef());
        Register(new ValueNodeDef());
        Register(new ReplaceNodeDef());
        Register(new EnuNodeDef());
        Register(new SkipNodeDef());

        // Structure
        Register(new ChoiceOptionNodeDef());
        Register(new ActionBlockNodeDef());
        Register(new PhaseRawStringNodeDef());
    }
    private static void Register(PhaseNodeDef def) => _nodes[def.NodeType] = def;
    public static PhaseNodeDef Get(PhaseNodeType type) { EnsureInitialized(); return _nodes[type]; }
    public static IEnumerable<PhaseNodeDef> GetAll() { EnsureInitialized(); return _nodes.Values; }
}

// ----------------------------------------------------
// CORE PHASE DEFINITIONS (The 'ph.' alphabet)
// ----------------------------------------------------

public class PhaseRootNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseRoot;
    public override string NodeNiceName => "Root: Generic (ch/ph)";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.1f, 0.4f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[{card.Data.PrimaryText}] Root Container";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var types = new List<string> { "ph.! (SCPhase Screen)", "ch (Silent Choosable)", "ph (Boolean ChoicePhase)", "phi (Indexed Phase)", "phmp (Mod Pick)" };
        int idx = types.FindIndex(t => t.StartsWith(card.Data.PrimaryText));
        ui.CreateInspectorDropdown("Phase Type", types, Math.Max(0, idx), (v) => { card.Data.PrimaryText = types[v].Split(' ')[0]; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Timing Prefix", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
        if (card.Data.PrimaryText == "ph.!") ui.CreateInspectorInputField("Menu Title (;)", card.Data.TertiaryText, (val) => { card.Data.TertiaryText = val; ui.AutoCompile(); });
    }
}

public class PhaseMessageNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseMessage;
    public override string NodeNiceName => "ph.4 Message";
    public override Color GetColor() => new Color(0.2f, 0.5f, 0.7f);
    public override string GetTitle(PhaseCard card) => $"[ph.4] Message";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Message Text", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Button Text", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
    }
}

public class PhaseHeroChangeNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseHeroChange;
    public override string NodeNiceName => "ph.5 Hero Change";
    public override Color GetColor() => new Color(0.2f, 0.5f, 0.7f);
    public override string GetTitle(PhaseCard card) => $"[ph.5] Reroll Hero {card.Data.Num1}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Hero Index (0=Top)", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        var types = new List<string> { "0 (Random Class)", "1 (Generated Hero)" };
        ui.CreateInspectorDropdown("Type", types, card.Data.Num2, (v) => { card.Data.Num2 = v; ui.AutoCompile(); });
    }
}

public class PhaseResetNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseReset;
    public override string NodeNiceName => "ph.6 Reset";
    public override Color GetColor() => new Color(0.6f, 0.2f, 0.2f);
    public override string GetTitle(PhaseCard card) => $"[ph.6] Reset Party";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseItemCombineNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseItemCombine;
    public override string NodeNiceName => "ph.7 Item Combine";
    public override Color GetColor() => new Color(0.2f, 0.5f, 0.7f);
    public override string GetTitle(PhaseCard card) => $"[ph.7] {card.Data.PrimaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var opts = new List<string> { "SecondHighestToTierThrees", "ZeroToThreeToSingle" };
        ui.CreateInspectorDropdown("Type", opts, Math.Max(0, opts.IndexOf(card.Data.PrimaryText)), (v) => { card.Data.PrimaryText = opts[v]; ui.AutoCompile(); });
    }
}

public class PhasePositionSwapNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhasePositionSwap;
    public override string NodeNiceName => "ph.8 Position Swap";
    public override Color GetColor() => new Color(0.2f, 0.5f, 0.7f);
    public override string GetTitle(PhaseCard card) => $"[ph.8] Swap {card.Data.Num1} and {card.Data.Num2}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Index 1", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Index 2", card.Data.Num2.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num2 = v; ui.AutoCompile(); });
    }
}

public class PhaseChallengeNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseChallenge;
    public override string NodeNiceName => "ph.9 Challenge";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.2f, 0.5f, 0.7f);
    public override string GetTitle(PhaseCard card) => $"[ph.9] Challenge";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Enemies (JSON Array)", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
    }
}

public class PhaseBooleanNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseBoolean;
    public override string NodeNiceName => "ph.b Boolean";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.4f, 0.3f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[ph.b] {card.Data.PrimaryText} >= {card.Data.Num1}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Variable", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Threshold", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Timing Prefix", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
    }
}

public class PhaseBoolean2NodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseBoolean2;
    public override string NodeNiceName => "ph.z Boolean 2";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.5f, 0.3f, 0.5f);
    public override string GetTitle(PhaseCard card) => $"[ph.z] {card.Data.PrimaryText} >= {card.Data.Num1}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Variable", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Threshold", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
    }
}

public class PhaseChoiceNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseChoice;
    public override string NodeNiceName => "ph.c Choice";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.7f, 0.4f, 0.2f);
    public override string GetTitle(PhaseCard card) => $"[ph.c] {card.Data.PrimaryText} {card.Data.Num1}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var opts = new List<string> { "PointBuy", "Number", "UpToNumber", "Optional" };
        ui.CreateInspectorDropdown("Type", opts, Math.Max(0, opts.IndexOf(card.Data.PrimaryText)), (v) => { card.Data.PrimaryText = opts[v]; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Amount", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
    }
}

public class PhaseRunEndNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseRunEnd;
    public override string NodeNiceName => "ph.e Run End";
    public override Color GetColor() => new Color(0.6f, 0.1f, 0.1f);
    public override string GetTitle(PhaseCard card) => $"[ph.e] End Run";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseLinkedNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseLinked;
    public override string NodeNiceName => "ph.l Linked";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.3f, 0.6f, 0.3f);
    public override string GetTitle(PhaseCard card) => $"[ph.l] Linked Chain (@1)";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseRandomRevealNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseRandomReveal;
    public override string NodeNiceName => "ph.r Random Reveal";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.6f, 0.4f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[ph.r] Reveal Popup";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseSeqNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseSeq;
    public override string NodeNiceName => "ph.s Sequence";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.4f, 0.6f, 0.8f);
    public override string GetTitle(PhaseCard card) => $"[ph.s] Sequence Branch";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Initial Message", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
    }
}

public class PhaseTradeNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseTrade;
    public override string NodeNiceName => "ph.t Trade (Cursed Chest)";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.4f, 0.4f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[ph.t] Trade Phase";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseLevelEndNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseLevelEnd;
    public override string NodeNiceName => "ph.2 Level End Wrapper";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.2f, 0.2f, 0.2f);
    public override string GetTitle(PhaseCard card) => $"[ph.2] Level End";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseGenTransformNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseGenTransform;
    public override string NodeNiceName => "ph.g Phase Gen";
    public override Color GetColor() => new Color(0.5f, 0.7f, 0.3f);
    public override string GetTitle(PhaseCard card) => $"[ph.g] Gen Phase ({card.Data.PrimaryText})";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var opts = new List<string> { "h (Levelup)", "i (Item)" };
        ui.CreateInspectorDropdown("Type", opts, Math.Max(0, opts.FindIndex(o => o.StartsWith(card.Data.PrimaryText))), (v) => { card.Data.PrimaryText = opts[v].Substring(0, 1); ui.AutoCompile(); });
    }
}

public class PhaseCombatNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.PhaseCombat;
    public override string NodeNiceName => "ph. Combat Phases (0,1,3,d)";
    public override Color GetColor() => new Color(0.6f, 0.4f, 0.2f);
    public override string GetTitle(PhaseCard card) => $"[ph.{card.Data.PrimaryText}] Combat";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var opts = new List<string> { "0 (PlayerRoll)", "1 (Targeting)", "3 (EnemyRoll)", "d (Damage)" };
        ui.CreateInspectorDropdown("Phase", opts, Math.Max(0, opts.FindIndex(o => o.StartsWith(card.Data.PrimaryText))), (v) => { card.Data.PrimaryText = opts[v].Substring(0, 1); ui.AutoCompile(); });
        ui.CreateInspectorInputField("Args (;)", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
    }
}

// ----------------------------------------------------
// PAYLOAD TAGS & STRUCTURE 
// ----------------------------------------------------

public class StandardRewardNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardStandard;
    public override string NodeNiceName => "Tag: Standard (m, i, l, g)";
    public override Color GetColor() => new Color(0.2f, 0.6f, 0.3f);
    public override string GetTitle(PhaseCard card) => $"[{card.Data.PrimaryText}] {card.Data.SecondaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var tags = new List<string> { "m (Modifier)", "i (Item)", "l (Levelup)", "g (Hero)" };
        ui.CreateInspectorDropdown("Tag", tags, Math.Max(0, tags.FindIndex(t => t.StartsWith(card.Data.PrimaryText))), (idx) => { card.Data.PrimaryText = tags[idx].Substring(0, 1); ui.AutoCompile(); });
        ui.CreateInspectorInputField("Entity Name", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
    }
}

public class RandomRewardNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardRandom;
    public override string NodeNiceName => "Tag: Random Pool (r)";
    public override Color GetColor() => new Color(0.5f, 0.2f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[r] T{card.Data.Num1} (x{card.Data.Num2}) -> {card.Data.PrimaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Tier", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Count", card.Data.Num2.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num2 = v; ui.AutoCompile(); });
        var tags = new List<string> { "m", "i", "l", "g" };
        ui.CreateInspectorDropdown("Target Tag", tags, Math.Max(0, tags.FindIndex(t => t.StartsWith(card.Data.PrimaryText))), (idx) => { card.Data.PrimaryText = tags[idx].Substring(0, 1); ui.AutoCompile(); });
    }
}

public class RandomRangeNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardRandomRange;
    public override string NodeNiceName => "Tag: Random Range (q)";
    public override Color GetColor() => new Color(0.6f, 0.3f, 0.5f);
    public override string GetTitle(PhaseCard card) => $"[q] T{card.Data.Num1}-{card.Data.Num2} (x{card.Data.Num3})";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Min Tier", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Max Tier", card.Data.Num2.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num2 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Count", card.Data.Num3.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num3 = v; ui.AutoCompile(); });
        var tags = new List<string> { "m", "i", "l", "g" };
        ui.CreateInspectorDropdown("Target Tag", tags, Math.Max(0, tags.FindIndex(t => t.StartsWith(card.Data.PrimaryText))), (idx) => { card.Data.PrimaryText = tags[idx].Substring(0, 1); ui.AutoCompile(); });
    }
}

public class OrNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardOr;
    public override string NodeNiceName => "Tag: Random Choice (o)";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.7f, 0.4f, 0.1f);
    public override string GetTitle(PhaseCard card) => $"[o] Pick One Randomly (@4)";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class ValueNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardValue;
    public override string NodeNiceName => "Tag: Mod Value (v)";
    public override Color GetColor() => new Color(0.6f, 0.6f, 0.1f);
    public override string GetTitle(PhaseCard card) => $"[v] {card.Data.PrimaryText} += {card.Data.Num1}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Variable", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Amount", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
    }
}

public class ReplaceNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardReplace;
    public override string NodeNiceName => "Tag: Replace Mod (p)";
    public override Color GetColor() => new Color(0.6f, 0.1f, 0.1f);
    public override string GetTitle(PhaseCard card) => $"[pm] Replace {card.Data.SecondaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Remove Mod", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
        var tags = new List<string> { "m", "i", "l", "g" };
        ui.CreateInspectorDropdown("New Reward Tag", tags, Math.Max(0, tags.FindIndex(t => t.StartsWith(card.Data.TertiaryText))), (idx) => { card.Data.TertiaryText = tags[idx].Substring(0, 1); ui.AutoCompile(); });
        ui.CreateInspectorInputField("New Reward Name", card.Data.SecondaryText, (val) => { card.Data.SecondaryText = val; ui.AutoCompile(); });
    }
}

public class EnuNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardEnu;
    public override string NodeNiceName => "Tag: Enu (e)";
    public override Color GetColor() => new Color(0.4f, 0.4f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[e] {card.Data.PrimaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        var options = new List<string> { "RandoKeywordT1Item", "RandoKeywordT5Item", "RandoKeywordT7Item" };
        ui.CreateInspectorDropdown("Type", options, Math.Max(0, options.IndexOf(card.Data.PrimaryText)), (v) => { card.Data.PrimaryText = options[v]; ui.AutoCompile(); });
    }
}

public class SkipNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RewardSkip;
    public override string NodeNiceName => "Tag: Skip (s)";
    public override Color GetColor() => new Color(0.3f, 0.3f, 0.3f);
    public override string GetTitle(PhaseCard card) => $"[s] Skip Option";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class ChoiceOptionNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.ChoiceOption;
    public override string NodeNiceName => "Branch Button Option (@X)";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.8f, 0.3f, 0.1f);
    public override string GetTitle(PhaseCard card) => $"@{card.Data.Num1} {card.Data.PrimaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Branch ID (1, 2)", card.Data.Num1.ToString(), (val) => { if (int.TryParse(val, out int v)) card.Data.Num1 = v; ui.AutoCompile(); });
        ui.CreateInspectorInputField("Branch Text", card.Data.PrimaryText, (val) => { card.Data.PrimaryText = val; ui.AutoCompile(); });
    }
}

public class ActionBlockNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.ActionBlock;
    public override string NodeNiceName => "Action Block (!m)";
    public override bool HasPayloadPort => true;
    public override Color GetColor() => new Color(0.6f, 0.2f, 0.3f);
    public override string GetTitle(PhaseCard card) => $"!m( ... )";
    public override void DrawInspector(PhasesUI ui, PhaseCard card) { }
}

public class PhaseRawStringNodeDef : PhaseNodeDef
{
    public override PhaseNodeType NodeType => PhaseNodeType.RawString;
    public override string NodeNiceName => "Raw String Injection";
    public override Color GetColor() => new Color(0.2f, 0.4f, 0.6f);
    public override string GetTitle(PhaseCard card) => $"[Raw] {card.Data.PrimaryText}";
    public override void DrawInspector(PhasesUI ui, PhaseCard card)
    {
        ui.CreateInspectorInputField("Raw Payload", card.Data.PrimaryText, v => { card.Data.PrimaryText = v; ui.AutoCompile(); });
    }
}

// ==========================================
// 5. THE SYNTAX COMPILER
// ==========================================

public static class PhaseSyntaxCompiler
{
    public static string CompileZone(IEnumerable<PhaseCard> cards, string joinDelim)
    {
        if (cards == null || !cards.Any()) return "";
        return string.Join(joinDelim, cards.Select(CompileCard).Where(s => !string.IsNullOrEmpty(s)));
    }

    public static string CompileCard(PhaseCard card)
    {
        var children = card.PayloadPort?.Entrants.Cast<PhaseCard>() ?? Enumerable.Empty<PhaseCard>();
        var d = card.Data;

        switch (card.NodeType)
        {
            // --- Core Engine Roots ---
            case PhaseNodeType.PhaseRoot:
                string pfx = string.IsNullOrEmpty(d.SecondaryText) ? "" : $"{d.SecondaryText}.";
                if (d.PrimaryText == "ch") return $"{pfx}ch.{CompileZone(children, "")}";
                if (d.PrimaryText == "ph.!") return $"{pfx}ph.!{(string.IsNullOrEmpty(d.TertiaryText) ? "" : d.TertiaryText + ";")}{CompileZone(children, "@3")}";
                return $"{pfx}{d.PrimaryText};1;{CompileZone(children, "")}"; // General ph/phi fallback

            case PhaseNodeType.PhaseMessage:
                string btnTxt = string.IsNullOrEmpty(d.SecondaryText) ? "" : $";{d.SecondaryText}";
                return $"ph.4{d.PrimaryText}{btnTxt}";

            case PhaseNodeType.PhaseHeroChange:
                return $"ph.5{d.Num1}{d.Num2}";

            case PhaseNodeType.PhaseReset:
                return $"ph.6";

            case PhaseNodeType.PhaseItemCombine:
                return $"ph.7{d.PrimaryText}";

            case PhaseNodeType.PhasePositionSwap:
                return $"ph.8{d.Num1}{d.Num2}";

            case PhaseNodeType.PhaseChallenge:
                return $"ph.9{{\"reward\":{{\"data\":\"{CompileZone(children, "@3")}\"}},\"type\":{{\"extraMonsters\":[{d.PrimaryText}]}}}}";

            case PhaseNodeType.PhaseBoolean:
                string boolPfx = string.IsNullOrEmpty(d.SecondaryText) ? "" : $"{d.SecondaryText}.";
                return $"{boolPfx}ph.b{d.PrimaryText};{d.Num1};{CompileZone(children, "@2")}";

            case PhaseNodeType.PhaseBoolean2:
                return $"ph.z{d.PrimaryText}@6{d.Num1}@6{CompileZone(children, "@7")}";

            case PhaseNodeType.PhaseChoice:
                return $"ph.c{d.PrimaryText}#{d.Num1};{CompileZone(children, "@3")}";

            case PhaseNodeType.PhaseRunEnd:
                return $"ph.e";

            case PhaseNodeType.PhaseLinked:
                return $"ph.l{CompileZone(children, "@1")}";

            case PhaseNodeType.PhaseRandomReveal:
                return $"ph.r{CompileZone(children, "")}";

            case PhaseNodeType.PhaseSeq:
                return $"ph.s{d.PrimaryText}@1{CompileZone(children, "@1")}";

            case PhaseNodeType.PhaseTrade:
                return $"ph.t{CompileZone(children, "@3")}";

            case PhaseNodeType.PhaseLevelEnd:
                return $"ph.2{{ps:[{CompileZone(children, ",")}]}}";

            case PhaseNodeType.PhaseGenTransform:
                return $"ph.g{d.PrimaryText}";

            case PhaseNodeType.PhaseCombat:
                string args = string.IsNullOrEmpty(d.SecondaryText) ? "" : d.SecondaryText;
                return $"ph.{d.PrimaryText}{args}";

            // --- Rewards ---
            case PhaseNodeType.RewardStandard:
                return $"{d.PrimaryText}{d.SecondaryText}";
            case PhaseNodeType.RewardRandom:
                return $"r{d.Num1}~{d.Num2}~{d.PrimaryText}";
            case PhaseNodeType.RewardRandomRange:
                return $"q{d.Num1}~{d.Num2}~{d.Num3}~{d.PrimaryText}";
            case PhaseNodeType.RewardOr:
                return $"o{CompileZone(children, "@4")}";
            case PhaseNodeType.RewardValue:
                return $"v{d.PrimaryText}V{d.Num1}";
            case PhaseNodeType.RewardReplace:
                return $"pm{d.PrimaryText}~{d.TertiaryText}{d.SecondaryText}";
            case PhaseNodeType.RewardEnu:
                return $"e{d.PrimaryText}";
            case PhaseNodeType.RewardSkip:
                return "s";

            // --- Wrappers ---
            case PhaseNodeType.ChoiceOption:
                string childStr = CompileZone(children, "@2");
                if (!string.IsNullOrEmpty(childStr)) childStr = $"@2{childStr}";
                return $"@{d.Num1}{d.PrimaryText}{childStr}";
            case PhaseNodeType.ActionBlock:
                return $"!m({CompileZone(children, "&")})";
            case PhaseNodeType.RawString:
                return d.PrimaryText;

            default:
                return "";
        }
    }
}