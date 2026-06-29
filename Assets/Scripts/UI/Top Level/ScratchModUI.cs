using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#region 1. CORE UI MANAGER (ScratchModUI)
public class ScratchModUI : RootUI
{
    public static ScratchModUI Instance { get; private set; }

    private TMP_Dropdown _leftDirectiveDropdown;

    // Workspace variables
    private ReorderableZone _rootWorkspaceZone;
    private Canvas _rootCanvas;

    // Inspector variables
    private RectTransform _inspectorContainer;
    private VisualBlockCard _currentlyInspectedCard;

    protected override void BuildUIAndBind()
    {
        Instance = this;

        float totalHeight = 600f;
        if (uiGenerator != null && uiGenerator.canvas != null)
        {
            _rootCanvas = uiGenerator.canvas;
            RectTransform canvasRt = _rootCanvas.GetComponent<RectTransform>();
            if (canvasRt != null) totalHeight = canvasRt.rect.height;
        }

        float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;

        List<string> dropdownOptions = new List<string> { "Add Modifier Block..." };
        dropdownOptions.AddRange(CodeBlocks._blockSyntaxOptions);

        List<GridRowSpec> leftRows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("LeftTitle", "Tools", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod", 1.0f, LoadModFromClipboard)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, dropdownOptions.ToArray(), OnDropdownSelected)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("LeftTitle2", "Quick Add Block", 1.0f)),
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("BtnOption1", "Clear Party", 0.8f, AddClearPartyBlock))
        };

        List<GridRowSpec> middleRows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("WORKSPACE_TITLE", "WORKSPACE (Drag to Reorder)", 1.0f)),
            new GridRowSpec(rowHeight,
                GridCellSpec.CreateButton("BtnCompile", "Compile", 0.5f, CompileBlocks),
                GridCellSpec.CreateButton("BtnClear", "Clear", 0.5f, ClearWorkspace)
            ),
            // Height will be overridden dynamically by ApplyDynamicLayoutConstraints
            new GridRowSpec(400f, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f))
        };

        List<GridRowSpec> rightRows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", "INSPECTOR PROPERTIES", 1.0f)),
            // Delete button is now cleanly handled inside the node's UI itself
            new GridRowSpec(400f, GridCellSpec.CreateScrollView("InspectorScrollArea", 1.0f))
        };

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.0f, 0.25f, leftRows),
            new ColumnSpec("Middle_Column", 0.26f, 0.65f, middleRows),
            new ColumnSpec("Right_Column", 0.66f, 1.00f, rightRows)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

        if (generatedScreen != null)
        {
            if (generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
            {
                if (leftRefs.Dropdowns.TryGetValue("ModDropdown", out _leftDirectiveDropdown))
                    ConfigureDropdownExpansion();
            }

            ConfigureWorkspace();
            ConfigureInspector();
            ApplyDynamicLayoutConstraints(); // Fixes squishing/overflow issues!
        }
    }

    public void SelectCardForInspection(VisualBlockCard card)
    {
        if (_currentlyInspectedCard != null) _currentlyInspectedCard.Deselect();

        _currentlyInspectedCard = card;

        foreach (Transform child in _inspectorContainer)
        {
            Destroy(child.gameObject);
        }

        if (card == null || card.UINode == null) return;

        // Wire up the delete button natively!
        card.UINode.OnDeleteRequested = DeleteSelectedBlock;

        GridReferences refs = uiGenerator.RebuildGrid(_inspectorContainer, card.UINode.GetRowSpecs(), useMargins: false);

        card.UINode.BindUI(_inspectorContainer, refs);
        card.UINode.RestoreState(_inspectorContainer, refs);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_inspectorContainer);
    }

    // --- NEW: Ported from HeroUI to force ScrollViews to fill the screen ---
    private void ApplyDynamicLayoutConstraints()
    {
        if (generatedScreen.ColumnRefs.TryGetValue("Middle_Column", out GridReferences midRefs))
        {
            if (midRefs.ScrollViews.TryGetValue("WorkspaceScrollArea", out ScrollRect midScroll))
            {
                RectTransform scrollRt = midScroll.GetComponent<RectTransform>();
                RectTransform rowRt = scrollRt.parent as RectTransform;

                ConfigureFlexibleLayout(rowRt);
                ConfigureFlexibleLayout(scrollRt);

                // Middle column has a Title row and a Buttons row above it
                float topOffset = (uiGenerator.rowHeight * 2f) + (uiGenerator.rowSpacing * 3f);
                StretchToParent(rowRt, topOffset, 10f);
                StretchToParent(scrollRt, 0f, 0f);
            }
        }

        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences rightRefs))
        {
            if (rightRefs.ScrollViews.TryGetValue("InspectorScrollArea", out ScrollRect rightScroll))
            {
                RectTransform scrollRt = rightScroll.GetComponent<RectTransform>();
                RectTransform rowRt = scrollRt.parent as RectTransform;

                ConfigureFlexibleLayout(rowRt);
                ConfigureFlexibleLayout(scrollRt);

                // Right column only has a Title row above it
                float topOffset = uiGenerator.rowHeight + (uiGenerator.rowSpacing * 2f);
                StretchToParent(rowRt, topOffset, 10f);
                StretchToParent(scrollRt, 0f, 0f);
            }
        }
    }

    private void ConfigureFlexibleLayout(RectTransform target)
    {
        if (target == null) return;
        var layoutElement = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = -1;
        layoutElement.flexibleHeight = 1f;
    }

    private void StretchToParent(RectTransform rt, float topOffset, float bottomOffset)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f, bottomOffset);
        rt.offsetMax = new Vector2(0f, -topOffset);
    }

    private void ConfigureDropdownExpansion()
    {
        if (_leftDirectiveDropdown == null) return;

        RectTransform templateRt = _leftDirectiveDropdown.template;
        if (templateRt != null)
        {
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(0f, 0f);
            templateRt.pivot = new Vector2(0f, 1f);

            float customWidth = 420f;
            float currentHeight = templateRt.sizeDelta.y > 0 ? templateRt.sizeDelta.y : 350f;
            templateRt.sizeDelta = new Vector2(customWidth, currentHeight);
        }
    }

    private void ConfigureWorkspace()
    {
        if (generatedScreen.ColumnRefs.TryGetValue("Middle_Column", out GridReferences refs))
        {
            if (refs.ScrollViews.TryGetValue("WorkspaceScrollArea", out ScrollRect workspaceScroll))
            {
                Transform content = workspaceScroll.content;

                var layout = content.gameObject.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 4f;

                // Add padding (Left, Right, Top, Bottom) to prevent elements from touching the edges
                layout.padding = new RectOffset(12, 12, 12, 12);

                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = content.gameObject.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                _rootWorkspaceZone = content.gameObject.AddComponent<ReorderableZone>();
                _rootWorkspaceZone.SetCanvas(_rootCanvas);
            }
        }
    }

    private void ConfigureInspector()
    {
        if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences refs))
        {
            if (refs.ScrollViews.TryGetValue("InspectorScrollArea", out ScrollRect inspectorScroll))
            {
                _inspectorContainer = inspectorScroll.content;

                var layout = _inspectorContainer.gameObject.GetComponent<VerticalLayoutGroup>() ?? _inspectorContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 8f;

                // Add padding (Left, Right, Top, Bottom) to clear the flush borders
                layout.padding = new RectOffset(12, 12, 12, 12);

                // This MUST be false! Otherwise Unity crushes the manual heights set by FullScreenUIGenerator.
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = _inspectorContainer.gameObject.GetComponent<ContentSizeFitter>() ?? _inspectorContainer.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }

    private void OnDropdownSelected(int index)
    {
        if (index <= 0 || _leftDirectiveDropdown == null) return;
        string selectedOption = _leftDirectiveDropdown.options[index].text;
        _leftDirectiveDropdown.value = 0;
        AddBlockToWorkspace(selectedOption);
    }

    private void DeleteSelectedBlock()
    {
        if (_currentlyInspectedCard == null) return;

        if (_currentlyInspectedCard.CurrentZone != null)
            _currentlyInspectedCard.CurrentZone.RemoveEntrant(_currentlyInspectedCard);

        Destroy(_currentlyInspectedCard.gameObject);
        SelectCardForInspection(null);
    }

    private void CompileBlocks()
    {
        if (_rootWorkspaceZone == null || _rootWorkspaceZone.Entrants.Count == 0) return;

        List<ITextmodNode> rootNodes = CompileZoneRecursive(_rootWorkspaceZone);

        CommaChainBlock rootChain = new CommaChainBlock { Nodes = rootNodes };
        string compiledOutput = rootChain.Compile();

        if (!string.IsNullOrEmpty(compiledOutput))
        {
            GUIUtility.systemCopyBuffer = compiledOutput;
            Debug.Log($"Compiled output copied to clipboard:\n{compiledOutput}");
        }
    }

    private List<ITextmodNode> CompileZoneRecursive(ReorderableZone zone)
    {
        List<ITextmodNode> compiledNodes = new List<ITextmodNode>();

        foreach (var entrant in zone.Entrants)
        {
            var card = entrant as VisualBlockCard;
            if (card == null) continue;

            // If this card has a nested zone, compile its children first (Bottom-Up Compilation)
            if (card.NestedZone != null)
            {
                List<ITextmodNode> childNodes = CompileZoneRecursive(card.NestedZone);

                // 1. Assign to List-based nodes
                if (card.CompilerNode is CommaChainBlock cc) cc.Nodes = childNodes;
                else if (card.CompilerNode is AndChainBlock ac) ac.Nodes = childNodes;
                else if (card.CompilerNode is Phase_LinkedBlock lc) lc.Phases = childNodes;
                else if (card.CompilerNode is Phase_SimpleChoiceBlock sc) sc.Choices = childNodes;
                else if (card.CompilerNode is Reward_ChoiceBlock rc) rc.Options = childNodes;
                else if (card.CompilerNode is Phase_ChoiceBlock cb) cb.Options = childNodes;
                else if (card.CompilerNode is Phase_LevelEndBlock leb) leb.EndScreenData = childNodes;

                // 2. Assign to Single-Payload wrapper nodes
                else if (card.CompilerNode is ContextWrapperBlock cw) cw.Payload = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is MultiplierBlock mb) mb.Payload = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is FloorConditionBlock fc) fc.Payload = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is LevelConstraintBlock lcb) lcb.Payload = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is ReplaceCommandBlock rep) rep.TargetNode = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is Phase_ChallengeBlock chb) chb.RewardPayload = GetSingleOrChain(childNodes);
                else if (card.CompilerNode is Phase_RandomRevealBlock rrb) rrb.RewardData = GetSingleOrChain(childNodes);


            }

            compiledNodes.Add(card.CompilerNode);
        }
        return compiledNodes;
    }

    /// <summary>
    /// Helper to safely handle wrapper nodes (like ch.) that expect a single payload.
    /// If the user drops multiple blocks into it, we implicitly wrap them in a Comma Chain to prevent data loss.
    /// </summary>
    private ITextmodNode GetSingleOrChain(List<ITextmodNode> nodes)
    {
        if (nodes == null || nodes.Count == 0) return null;
        if (nodes.Count == 1) return nodes[0];

        // Auto-wrap multiple children to keep the AST intact
        return new CommaChainBlock { Nodes = nodes };
    }

    private void ClearWorkspace()
    {
        if (_rootWorkspaceZone != null)
        {
            foreach (var entrant in _rootWorkspaceZone.Entrants.ToList()) Destroy(entrant.gameObject);
            _rootWorkspaceZone.Entrants.Clear();
        }
        SelectCardForInspection(null);
    }

    private void LoadModFromClipboard() { /* Hook up to TextmodTranslator later */ }

    private void AddClearPartyBlock()
    {
        if (_rootWorkspaceZone == null) return;

        PoolBlock clearPartyNode = new PoolBlock
        {
            Type = PoolBlock.PoolType.Hero,
            Entities = new List<string> { "Thief" },
            Part = 0,
            CustomEncounterName = "Clear Heroes"
        };

        PoolBlockUI uiNode = new PoolBlockUI(clearPartyNode);

        GameObject cardGo = new GameObject($"Card_ClearParty", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        VisualBlockCard card = cardGo.AddComponent<VisualBlockCard>();

        card.Initialize("Clear Party (Hero Pool)", false);
        card.CompilerNode = clearPartyNode;
        card.UINode = uiNode;

        _rootWorkspaceZone.AddEntrant(card);
        SelectCardForInspection(card);
    }

    private void AddBlockToWorkspace(string optionName)
    {
        if (_rootWorkspaceZone == null) return;

        ITextmodNode compilerNode = TextmodBlockFactory.CreateBlock(optionName);
        if (compilerNode == null) return;

        UIBlockNode uiNode = UIBlockFactory.CreateUIBlock(compilerNode);

        GameObject cardGo = new GameObject($"Card_{optionName}", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        VisualBlockCard card = cardGo.AddComponent<VisualBlockCard>();

        // Check if this block type is designed to hold nested children
        bool canHoldChildren = (
            compilerNode is CommaChainBlock ||
            compilerNode is AndChainBlock ||
            compilerNode is Phase_LinkedBlock ||
            compilerNode is Phase_SimpleChoiceBlock || // ph.!
            compilerNode is Reward_ChoiceBlock ||      // o (Or Tag)
            compilerNode is ContextWrapperBlock ||     // ch., ph., etc.
            compilerNode is MultiplierBlock ||         // xN.
            compilerNode is FloorConditionBlock ||     // lvl.
            compilerNode is LevelConstraintBlock ||    // lvl.
            compilerNode is ReplaceCommandBlock ||       // replace.
            compilerNode is Phase_ChoiceBlock ||       // ph.c
            compilerNode is Phase_LevelEndBlock ||     // ph.2
            compilerNode is Phase_ChallengeBlock ||    // ph.9
            compilerNode is Phase_RandomRevealBlock    // ph.r
        );

        card.Initialize(optionName, canHoldChildren);

        card.CompilerNode = compilerNode;
        card.UINode = uiNode;

        if (card.NestedZone != null) card.NestedZone.SetCanvas(_rootCanvas);

        _rootWorkspaceZone.AddEntrant(card);
        SelectCardForInspection(card);
    }
}
#endregion

#region 2. FACTORIES & DEFINITIONS
public static class CodeBlocks
{
    /*
    public static readonly List<string> _blockSyntaxOptions = new List<string>
    {
    // Wrappers & Flow
    "Chain: Comma (Top Level AND)",
    "Chain: Ampersand (Nested AND)",
    "Wrapper: Multiplier (xN)",
    "Wrapper: Context/Phase Definer", // Consolidates 4 wrappers
    
    // Commands
    "Command: Encounter Setup",       // Consolidates Fight & Party
    "Command: Entity Manipulation",   // Consolidates Replace & Add Pool
    "Command: Game Setting",          // Consolidates Add, Zone, Diff, Lvl, Global

    // Phases (Grouped by purpose)
    "Phase: Simple Choice Screen (!)",
    "Phase: Choice / PointBuy (c)",
    "Phase: Challenge Phase (9)",
    "Phase: Sequence / Story (s)",
    "Phase: Boolean Logic (b/z)",     // Consolidates b and z
    "Phase: Trade / Combine (t/7)",   // Consolidates t and 7
    "Phase: Hero Adjustments (5/8)",  // Consolidates 5 and 8
    "Phase: UI Screens (2/4/r/g)",    // Consolidates End, Message, Reveal, Gen
    "Phase: Static / Event",          // Consolidates Static and Linked

    // Rewards
    "Reward: Grant Entity",           // Consolidates Standard, Random, Enum, Var, Rep, Skip
    "Reward: Choice Options (o)"      // 'Or' tag holds children, so it stays independent
    };
    */

    
    public static readonly List<string> _blockSyntaxOptions = new List<string>
    {
        "Chain: Comma (Top Level AND)",
        "Chain: Ampersand (Nested AND)",
        "Wrapper: Floor Condition",
        "Wrapper: Multiplier (xN)",
        "Command: Add Entity (add.)",
        "Command: Fight Encounter (fight.)",
        "Command: Set Party (party.)",
        "Command: Replace Entity (replace.)",
        "Command: Add to Pool (item/hero/monster)",
        "Command: Grant All Items (allitem/alliteme)",
        "Command: Set Zone (zone.)",
        "Command: Set Difficulty (diff.)",
        "Command: Level Constraint (lvl.)",
        "Global Command",
        "Context: Implied Phase (ph.)",
        "Context: Indexed Game Phase (phi.)",
        "Context: Modifier Pick Phase (phmp.)",
        "Context: Choosable Reward (ch.)",
        "Phase: Simple Choice (!)",
        "Phase: Level End Screen (2)",
        "Phase: Message Popup (4)",
        "Phase: Hero Change Offer (5)",
        "Phase: Item Combine / Smithing (7)",
        "Phase: Position Swap (8)",
        "Phase: Challenge Phase (9)",
        "Phase: Boolean Check 1 (b)",
        "Phase: Choice Screen (c)",
        "Phase: Linked Events (l)",
        "Phase: Random Reveal Popup (r)",
        "Phase: Story Sequence (s)",
        "Phase: Cursed Chest Trade (t)",
        "Phase: Generate Screen (g)",
        "Phase: Boolean Check 2 (z)",
        "Phase: Static (0,1,3,d,6,e)",
        "Reward: Standard (i/m/g/l)",
        "Reward: Random Reward (r/q)",
        "Reward: Random Choice (o)",
        "Reward: Enum Item (e)",
        "Reward: Modify Variable (v)",
        "Reward: Replace Reward (p)",
        "Reward: Skip (s)"
    };
    
}

public static class TextmodBlockFactory
{
    public static ITextmodNode CreateBlock(string optionName)
    {
        switch (optionName)
        {
            case "Chain: Comma (Top Level AND)": return new CommaChainBlock();
            case "Chain: Ampersand (Nested AND)": return new AndChainBlock();
            case "Wrapper: Floor Condition": return new FloorConditionBlock();
            case "Wrapper: Multiplier (xN)": return new MultiplierBlock();
            case "Command: Add Entity (add.)": return new AddEntityBlock();
            case "Command: Fight Encounter (fight.)": return new FightBlock();
            case "Command: Set Party (party.)": return new PartyBlock();
            case "Command: Replace Entity (replace.)": return new ReplaceCommandBlock();
            case "Command: Add to Pool (item/hero/monster)": return new PoolBlock();
            case "Command: Grant All Items (allitem/alliteme)": return new AllItemBlock();
            case "Command: Set Zone (zone.)": return new SimpleValueBlock("zone");
            case "Command: Set Difficulty (diff.)": return new SimpleValueBlock("diff");
            case "Command: Level Constraint (lvl.)": return new LevelConstraintBlock();
            case "Global Command": return new GlobalCommandBlock();
            case "Context: Implied Phase (ph.)": return new ContextWrapperBlock("ph");
            case "Context: Indexed Game Phase (phi.)": return new ContextWrapperBlock("phi");
            case "Context: Modifier Pick Phase (phmp.)": return new ContextWrapperBlock("phmp");
            case "Context: Choosable Reward (ch.)": return new ContextWrapperBlock("ch");
            case "Phase: Simple Choice (!)": return new Phase_SimpleChoiceBlock();
            case "Phase: Level End Screen (2)": return new Phase_LevelEndBlock();
            case "Phase: Message Popup (4)": return new Phase_MessageBlock();
            case "Phase: Hero Change Offer (5)": return new Phase_HeroChangeBlock();
            case "Phase: Item Combine / Smithing (7)": return new Phase_ItemCombineBlock();
            case "Phase: Position Swap (8)": return new Phase_PositionSwapBlock();
            case "Phase: Challenge Phase (9)": return new Phase_ChallengeBlock();
            case "Phase: Boolean Check 1 (b)": return new Phase_Boolean1Block();
            case "Phase: Choice Screen (c)": return new Phase_ChoiceBlock();
            case "Phase: Linked Events (l)": return new Phase_LinkedBlock();
            case "Phase: Random Reveal Popup (r)": return new Phase_RandomRevealBlock();
            case "Phase: Story Sequence (s)": return new Phase_SequenceBlock();
            case "Phase: Cursed Chest Trade (t)": return new Phase_TradeBlock();
            case "Phase: Generate Screen (g)": return new Phase_GenerateScreenBlock();
            case "Phase: Boolean Check 2 (z)": return new Phase_Boolean2Block();
            case "Phase: Static (0,1,3,d,6,e)": return new Phase_StaticBlock();
            case "Reward: Standard (i/m/g/l)": return new Reward_StandardBlock();
            case "Reward: Random Reward (r/q)": return new Reward_RandomBlock();
            case "Reward: Random Choice (o)": return new Reward_ChoiceBlock();
            case "Reward: Enum Item (e)": return new Reward_EnumItemBlock();
            case "Reward: Modify Variable (v)": return new Reward_ValueModifyBlock();
            case "Reward: Replace Reward (p)": return new Reward_ReplaceBlock();
            case "Reward: Skip (s)": return new Reward_SkipBlock();
            default:
                Debug.LogWarning($"Unknown block option selected: {optionName}");
                return null;
        }
    }
}

public static class UIBlockFactory
{
    public static UIBlockNode CreateUIBlock(ITextmodNode compilerNode)
    {
        if (compilerNode is PoolBlock poolNode)
        {
            return new PoolBlockUI(poolNode);
        }

        if (compilerNode is TextmodBlock textmodBase)
        {
            return new DefaultBlockUI(textmodBase);
        }

        if (compilerNode is Phase_MessageBlock msgNode) return new Phase_MessageBlockUI(msgNode);
        if (compilerNode is Phase_HeroChangeBlock hcNode) return new Phase_HeroChangeBlockUI(hcNode);
        if (compilerNode is Phase_ItemCombineBlock icNode) return new Phase_ItemCombineBlockUI(icNode);
        if (compilerNode is Phase_PositionSwapBlock psNode) return new Phase_PositionSwapBlockUI(psNode);
        if (compilerNode is Phase_ChoiceBlock chNode) return new Phase_ChoiceBlockUI(chNode);
        if (compilerNode is Phase_StaticBlock statNode) return new Phase_StaticBlockUI(statNode);
        if (compilerNode is Phase_GenerateScreenBlock genNode) return new Phase_GenerateScreenBlockUI(genNode);
        if (compilerNode is Phase_LinkedBlock lkNode) return new Phase_LinkedBlockUI(lkNode);
        if (compilerNode is Phase_LevelEndBlock leNode) return new Phase_LevelEndBlockUI(leNode);
        if (compilerNode is Phase_ChallengeBlock clNode) return new Phase_ChallengeBlockUI(clNode);

        // 5. Complex Branching Phases (Stopgaps)
        if (compilerNode is Phase_Boolean1Block b1Node) return new Phase_Boolean1BlockUI(b1Node);
        if (compilerNode is Phase_Boolean2Block b2Node) return new Phase_Boolean2BlockUI(b2Node);
        if (compilerNode is Phase_TradeBlock trNode) return new Phase_TradeBlockUI(trNode);
        if (compilerNode is Phase_SequenceBlock seqNode) return new Phase_SequenceBlockUI(seqNode);

        return null;
    }
}
#endregion

#region 3. PHYSICAL WORKSPACE COMPONENTS
public class VisualBlockCard : ReorderableItem, IPointerClickHandler
{
    public ITextmodNode CompilerNode;
    public UIBlockNode UINode;

    public ReorderableZone NestedZone { get; private set; }

    private Image _bgImage;
    private TextMeshProUGUI _label;

    public void Initialize(string blockName, bool canHoldChildren)
    {
        _bgImage = gameObject.AddComponent<Image>();
        _bgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        var sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(transform, false);
        _label = textGo.GetComponent<TextMeshProUGUI>();
        _label.text = blockName;
        _label.fontSize = 14;
        _label.alignment = TextAlignmentOptions.TopLeft;

        if (canHoldChildren)
        {
            GameObject zoneGo = new GameObject("NestedZone", typeof(RectTransform), typeof(Image));
            zoneGo.transform.SetParent(transform, false);

            Image nestedBg = zoneGo.GetComponent<Image>();
            nestedBg.color = new Color(0, 0, 0, 0.3f);

            var nestedLayout = zoneGo.AddComponent<VerticalLayoutGroup>();
            nestedLayout.padding = new RectOffset(16, 4, 8, 8);
            nestedLayout.spacing = 4f;
            nestedLayout.childControlHeight = true;
            nestedLayout.childControlWidth = true;
            nestedLayout.childForceExpandHeight = false;

            zoneGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            NestedZone = zoneGo.AddComponent<ReorderableZone>();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;

        ScratchModUI.Instance.SelectCardForInspection(this);
        _bgImage.color = new Color(0.3f, 0.4f, 0.6f, 1f);
    }

    public void Deselect()
    {
        if (_bgImage != null)
            _bgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
    }
}
#endregion

#region 4. INSPECTOR UI DEFINITIONS
/// <summary>
/// Base class for all Inspector UIs. Handles standard shared properties natively.
/// </summary>
public abstract class UIBlockNode
{
    protected TextmodBlock BaseCompilerNode;

    public System.Action OnDeleteRequested;

    // FIX: Accept ITextmodNode instead of TextmodBlock
    protected UIBlockNode(ITextmodNode baseNode)
    {
        // Safely cast. If it's just an ITextmodNode (like chains/wrappers), 
        // BaseCompilerNode becomes null and the universal UI properties gracefully ignore it.
        BaseCompilerNode = baseNode as TextmodBlock;
    }

    // Subclasses provide their title and bespoke rows
    public abstract string GetBlockTitle();
    protected abstract List<GridRowSpec> GetSpecificRowSpecs();
    protected abstract void BindSpecificUI(RectTransform container, GridReferences refs);
    protected abstract void RestoreSpecificState(RectTransform container, GridReferences refs);

    /// <summary>
    /// Merges the specific block UI rows with the Universal base property rows.
    /// </summary>
    public List<GridRowSpec> GetRowSpecs()
    {
        // Initialize the list and add the standard rows all at once
        List<GridRowSpec> rows = new List<GridRowSpec>
        {
        // --- HEADER ROW (Title + Delete Button) ---
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblTitle", $"<b>{GetBlockTitle()}</b>", 0.70f),
            GridCellSpec.CreateButton("BtnDeleteBlock", "Delete", 0.30f, () => OnDeleteRequested?.Invoke())
        ),

        // --- UNIVERSAL PROPERTIES ---
        new GridRowSpec(GridCellSpec.CreateLabel("LblBaseProps", "General Properties", 1.0f)),

        new GridRowSpec(
            GridCellSpec.CreateLabel("LblEncName", "Modifier Name (.mn):", 0.35f),
            GridCellSpec.CreateInput("InpEncounterName", "", 0.65f, null)
        ),

        new GridRowSpec(GridCellSpec.CreateLabel("LblBaseProps", "Node Specific Properties", 1.0f))
        };

        // --- SPECIFIC PROPERTIES AT THE BOTTOM ---
        rows.AddRange(GetSpecificRowSpecs());

        return rows;
    }

    public void BindUI(RectTransform container, GridReferences refs)
    {
        BindSpecificUI(container, refs);

        if (BaseCompilerNode != null)
        {
            if (refs.Inputs.TryGetValue("InpEncounterName", out var inpEncName))
                inpEncName.onValueChanged.AddListener(v => BaseCompilerNode.CustomEncounterName = v);
        }
    }

    public void RestoreState(RectTransform container, GridReferences refs)
    {
        RestoreSpecificState(container, refs);

        if (BaseCompilerNode != null)
        {
            if (refs.Inputs.TryGetValue("InpEncounterName", out var inpEncName))
                inpEncName.text = BaseCompilerNode.CustomEncounterName;
        }
    }
}

/// <summary>
/// Bespoke Inspector UI specifically for Pool commands (like Clear Party).
/// </summary>
public class PoolBlockUI : UIBlockNode
{
    private PoolBlock _node;

    public PoolBlockUI(PoolBlock node) : base(node)
    {
        _node = node;
    }

    public override string GetBlockTitle() => $"{_node.Type.ToString().ToUpper()} POOL";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblEnt", "Entities (+ split):", 0.35f),
                GridCellSpec.CreateInput("InpEntities", "", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblPart", "Part ID:", 0.35f),
                GridCellSpec.CreateInput("InpPart", "", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpEntities", out var inpEnt))
            inpEnt.onValueChanged.AddListener(v => _node.Entities = v.Split('+').Select(s => s.Trim()).ToList());

        if (refs.Inputs.TryGetValue("InpPart", out var inpPart))
            inpPart.onValueChanged.AddListener(v => { if (int.TryParse(v, out int p)) _node.Part = p; else _node.Part = -1; });
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpEntities", out var inpEnt))
            inpEnt.text = string.Join("+", _node.Entities);

        if (refs.Inputs.TryGetValue("InpPart", out var inpPart))
            inpPart.text = _node.Part.ToString();
    }
}

public class DefaultBlockUI : UIBlockNode
{
    public DefaultBlockUI(TextmodBlock node) : base(node) { }

    public override string GetBlockTitle() => "GENERIC BLOCK";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblWIP", "Specific properties WIP for this block type.", 1f))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs) { }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs) { }
}
#endregion

#region 5. COMPILER AST NODES
public interface ITextmodNode
{
    string Compile();
}

public class RawTextNode : ITextmodNode
{
    public string Text = "";
    public RawTextNode(string text) { Text = text; }
    public string Compile() => Text;
}
public abstract class TextmodBlock : ITextmodNode
{
    public string CustomEncounterName = "";
    public string CustomEntityName = "";
    public string ModTier = "";

    public abstract string CompileCore();

    public string Compile()
    {
        string core = CompileCore();

        if (!string.IsNullOrEmpty(CustomEntityName))
            core += $".n.{CustomEntityName}";
        if (!string.IsNullOrEmpty(CustomEncounterName))
            core += $".mn.{CustomEncounterName}";
        if (!string.IsNullOrEmpty(ModTier))
            core += $".modtier.{ModTier}";

        return core;
    }

    protected string SafeCompile(ITextmodNode node)
    {
        if (node == null) return "";
        string compiled = node.Compile();

        char[] delimiters = { '&', '@', ';', ',', '+', '~', '#' };
        if (compiled.IndexOfAny(delimiters) >= 0 && !(compiled.StartsWith("(") && compiled.EndsWith(")")))
        {
            return $"({compiled})";
        }
        return compiled;
    }
}

public class CommaChainBlock : ITextmodNode
{
    public List<ITextmodNode> Nodes = new List<ITextmodNode>();
    public string Compile() => string.Join(",", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
}

public class AndChainBlock : ITextmodNode
{
    public List<ITextmodNode> Nodes = new List<ITextmodNode>();
    public string Compile() => string.Join("&", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
}

public class FloorConditionBlock : ITextmodNode
{
    public enum ConditionType { Single, Range, EveryX }
    public ConditionType Type = ConditionType.Single;
    public int StartFloor = 1;
    public int EndFloor = 5;
    public int Interval = 2;
    public int Offset = 0;
    public ITextmodNode Payload;

    public string Compile()
    {
        string prefix = "";
        switch (Type)
        {
            case ConditionType.Single: prefix = $"{StartFloor}."; break;
            case ConditionType.Range: prefix = $"{StartFloor}-{EndFloor}."; break;
            case ConditionType.EveryX: prefix = Offset > 0 ? $"e{Interval}.{Offset}." : $"e{Interval}."; break;
        }
        return prefix + Payload?.Compile();
    }
}

public class MultiplierBlock : ITextmodNode
{
    public int Multiplier = 2;
    public ITextmodNode Payload;
    public string Compile() => Multiplier <= 1 ? Payload?.Compile() : $"x{Multiplier}.{Payload?.Compile()}";
}

public class ContextWrapperBlock : ITextmodNode
{
    public string Prefix;
    public string Target = "";
    public ITextmodNode Payload;

    public ContextWrapperBlock(string prefix) { Prefix = prefix; }

    public string Compile()
    {
        if (Payload != null) return $"{Prefix}.{Target}{Payload.Compile()}";
        return $"{Prefix}.{Target}";
    }
}

public class AddEntityBlock : TextmodBlock
{
    public string Entity = "";
    public override string CompileCore() => $"add.{Entity}";
}

public class FightBlock : TextmodBlock
{
    public List<string> Monsters = new List<string>();
    public override string CompileCore() => $"fight.{string.Join("+", Monsters)}";
}

public class PartyBlock : TextmodBlock
{
    public List<string> Heroes = new List<string>();
    public override string CompileCore() => $"party.{string.Join("+", Heroes)}";
}

public class ReplaceCommandBlock : TextmodBlock
{
    public ITextmodNode TargetNode;
    public override string CompileCore() => $"replace.{SafeCompile(TargetNode)}";
}

public class PoolBlock : TextmodBlock
{
    public enum PoolType { Item, Hero, Monster }
    public PoolType Type = PoolType.Hero;
    public List<string> Entities = new List<string>();

    public int Part = -1;

    public override string CompileCore()
    {
        string core = $"{Type.ToString().ToLower()}pool.{string.Join("+", Entities)}";

        if (Part >= 0)
            core += $".part.{Part}";

        return core;
    }
}

public class AllItemBlock : TextmodBlock
{
    public bool Equipped = false;
    public List<string> Pools = new List<string>();
    public override string CompileCore() => (Equipped ? "alliteme." : "allitem.") + string.Join("+", Pools);
}

public class SimpleValueBlock : TextmodBlock
{
    private string Prefix;
    public string Value = "";
    public SimpleValueBlock(string prefix) { Prefix = prefix; }
    public override string CompileCore() => $"{Prefix}.{Value}";
}

public class LevelConstraintBlock : TextmodBlock
{
    public ITextmodNode Payload;
    public override string CompileCore() => $"lvl.{SafeCompile(Payload)}";
}

public class GlobalCommandBlock : TextmodBlock
{
    public enum GlobalType
    {
        Delevel, LevelUp, NoFlee, SkipAll, Skip, Temporary, Wish, ClearParty,
        Missing, Hidden, AddFight, Add10Fights, Add100Fights, MinusFight,
        CursemodeLoopdiff, Horde, DoubleMonsters, SkipRewards
    }
    public GlobalType Type = GlobalType.SkipAll;

    public override string CompileCore()
    {
        switch (Type)
        {
            case GlobalType.LevelUp: return "Level Up";
            case GlobalType.NoFlee: return "No Flee";
            case GlobalType.SkipAll: return "skip all";
            case GlobalType.ClearParty: return "Clear Party";
            case GlobalType.AddFight: return "Add Fight";
            case GlobalType.Add10Fights: return "Add 10 Fights";
            case GlobalType.Add100Fights: return "Add 100 Fights";
            case GlobalType.MinusFight: return "Minus Fight";
            case GlobalType.CursemodeLoopdiff: return "Cursemode Loopdiff";
            case GlobalType.DoubleMonsters: return "double monsters";
            case GlobalType.SkipRewards: return "skip rewards";
            default: return Type.ToString().ToLower();
        }
    }
}

public class Phase_SimpleChoiceBlock : TextmodBlock
{
    public string Title = "";
    public List<ITextmodNode> Choices = new List<ITextmodNode>();
    public override string CompileCore()
    {
        string opts = string.Join("@3", Choices.Select(c => SafeCompile(c)));
        return string.IsNullOrEmpty(Title) ? $"!{opts}" : $"!{Title};{opts}";
    }
}

public class Phase_LevelEndBlock : TextmodBlock
{
    public List<ITextmodNode> EndScreenData = new List<ITextmodNode>();
    public override string CompileCore() => $"2ps:[{string.Join(",", EndScreenData.Select(SafeCompile))}]";
}

public class Phase_MessageBlock : TextmodBlock
{
    public string Message = "";
    public string ButtonText = "Ok";
    public override string CompileCore() => $"4{Message};{ButtonText}";
}

public class Phase_HeroChangeBlock : TextmodBlock
{
    public int HeroPositionIndex = 0;
    public bool IsRandomClass = false;
    public override string CompileCore() => $"5{HeroPositionIndex}{(IsRandomClass ? "0" : "1")}";
}

public class Phase_ItemCombineBlock : TextmodBlock
{
    public enum CombineRule { SecondHighestToTierThrees, ZeroToThreeToSingle }
    public CombineRule Rule = CombineRule.SecondHighestToTierThrees;
    public override string CompileCore() => $"7{Rule}";
}

public class Phase_PositionSwapBlock : TextmodBlock
{
    public int IndexA = 0;
    public int IndexB = 1;
    public override string CompileCore() => $"8{IndexA}{IndexB}";
}

public class Phase_ChallengeBlock : TextmodBlock
{
    public List<string> ExtraMonsters = new List<string>();
    public ITextmodNode RewardPayload;

    public override string CompileCore()
    {
        string monsters = string.Join(",", ExtraMonsters.Select(m => $"\"{m}\""));
        return $"9{{\"extraMonsters\":[{monsters}],\"data\":\"{SafeCompile(RewardPayload)}\"}}";
    }
}

public class Phase_Boolean1Block : TextmodBlock
{
    public string VariableName = "";
    public int Threshold = 1;
    public ITextmodNode TrueBranch;
    public ITextmodNode FalseBranch;

    public override string CompileCore()
        => $"b{VariableName};{Threshold};{SafeCompile(TrueBranch)}@2{SafeCompile(FalseBranch)}";
}

public class Phase_ChoiceBlock : TextmodBlock
{
    public string ChoiceType = "i";
    public int NumChoices = 1;
    public string Title = "";
    public List<ITextmodNode> Options = new List<ITextmodNode>();

    public override string CompileCore()
    {
        string opts = string.Join("@3", Options.Select(SafeCompile));
        string core = $"c{ChoiceType}#{NumChoices};{opts}";
        return string.IsNullOrEmpty(Title) ? core : $"{core};{Title}";
    }
}

public class Phase_LinkedBlock : TextmodBlock
{
    public List<ITextmodNode> Phases = new List<ITextmodNode>();
    public override string CompileCore() => $"l{string.Join("@1", Phases.Select(SafeCompile))}";
}

public class Phase_RandomRevealBlock : TextmodBlock
{
    public ITextmodNode RewardData;
    public override string CompileCore() => $"r{SafeCompile(RewardData)}";
}

public class Phase_SequenceBlock : TextmodBlock
{
    public string SequenceMessage = "";
    public struct SequenceStep { public string ButtonText; public ITextmodNode Action; }
    public List<SequenceStep> Steps = new List<SequenceStep>();

    public override string CompileCore()
    {
        string res = $"s{SequenceMessage}";
        foreach (var step in Steps) res += $"@1{step.ButtonText}@2{SafeCompile(step.Action)}";
        return res;
    }
}

public class Phase_TradeBlock : TextmodBlock
{
    public ITextmodNode Item1;
    public ITextmodNode Item2;
    public override string CompileCore() => $"t{SafeCompile(Item1)}@3{SafeCompile(Item2)}";
}

public class Phase_GenerateScreenBlock : TextmodBlock
{
    public enum ScreenType { LevelUp = 'h', Item = 'i' }
    public ScreenType Type = ScreenType.Item;
    public override string CompileCore() => $"g{(char)Type}";
}

public class Phase_Boolean2Block : TextmodBlock
{
    public string VariableName = "";
    public int Threshold = 1;
    public ITextmodNode TrueBranch;
    public ITextmodNode FalseBranch;

    public override string CompileCore()
        => $"z{VariableName}@6{Threshold}@7{SafeCompile(TrueBranch)}@7{SafeCompile(FalseBranch)}";
}

public class Phase_StaticBlock : TextmodBlock
{
    public enum StaticPhase { PlayerRolling = '0', Targeting = '1', EnemyRolling = '3', Damage = 'd', Reset = '6', RunEnd = 'e' }
    public StaticPhase Phase = StaticPhase.PlayerRolling;
    public override string CompileCore() => $"{(char)Phase}";
}

public class Reward_StandardBlock : TextmodBlock
{
    public enum RewardType { Item = 'i', Modifier = 'm', Hero = 'g', LevelUp = 'l' }
    public RewardType Type = RewardType.Item;
    public string TargetEntity = "";
    public override string CompileCore() => $"{(char)Type}{TargetEntity}";
}

public class Reward_RandomBlock : TextmodBlock
{
    public int MinTier = 1;
    public int MaxTier = 1;
    public int Amount = 1;
    public string RewardTypeFlag = "i";

    public override string CompileCore()
    {
        if (MinTier == MaxTier) return $"r{MinTier}~{Amount}~{RewardTypeFlag}";
        return $"q{MinTier}~{MaxTier}~{Amount}~{RewardTypeFlag}";
    }
}

public class Reward_ChoiceBlock : TextmodBlock
{
    public List<ITextmodNode> Options = new List<ITextmodNode>();
    public override string CompileCore() => $"o{string.Join("@4", Options.Select(SafeCompile))}";
}

public class Reward_EnumItemBlock : TextmodBlock
{
    public string EnumName = "RandoKeywordT1Item";
    public override string CompileCore() => $"e{EnumName}";
}

public class Reward_ValueModifyBlock : TextmodBlock
{
    public string VariableName = "";
    public int ValueToAdd = 1;
    public override string CompileCore() => $"v{VariableName}V{ValueToAdd}";
}

public class Reward_ReplaceBlock : TextmodBlock
{
    public bool IsModifierReplacement = false;
    public string TargetToReplace = "";
    public string NewValue = "";

    public override string CompileCore()
    {
        if (IsModifierReplacement) return $"p m{TargetToReplace}~{NewValue}";
        return $"p{TargetToReplace}";
    }
}

public class Reward_SkipBlock : TextmodBlock
{
    public override string CompileCore() => "s";
}
#endregion

// =====================================================================
// BESPOKE UI NODES (Based on your Mod Structure Context)
// =====================================================================

public class GlobalCommandBlockUI : UIBlockNode
{
    private GlobalCommandBlock _node;
    private readonly string[] _globalOptions = System.Enum.GetNames(typeof(GlobalCommandBlock.GlobalType));

    public GlobalCommandBlockUI(GlobalCommandBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "GLOBAL COMMAND";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblCmd", "Command Type:", 0.35f),
                GridCellSpec.CreateDropdown("DropGlobalCommand", "", 0.65f, _globalOptions, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropGlobalCommand", out var drop))
        {
            drop.onValueChanged.AddListener(val => _node.Type = (GlobalCommandBlock.GlobalType)val);
        }
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropGlobalCommand", out var drop))
        {
            drop.value = (int)_node.Type;
        }
    }
}

public class FightBlockUI : UIBlockNode
{
    private FightBlock _node;

    public FightBlockUI(FightBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "FIGHT ENCOUNTER OVERRIDE";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblMonsters", "Monsters (+ split):", 0.35f),
                GridCellSpec.CreateInput("InpMonsters", "e.g. Goblin+Orc", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMonsters", out var inp))
            inp.onValueChanged.AddListener(v => _node.Monsters = v.Split('+').Select(s => s.Trim()).ToList());
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMonsters", out var inp))
            inp.text = string.Join("+", _node.Monsters);
    }
}

public class PartyBlockUI : UIBlockNode
{
    private PartyBlock _node;

    public PartyBlockUI(PartyBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "SET PARTY OVERRIDE";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblHeroes", "Heroes (+ split):", 0.35f),
                GridCellSpec.CreateInput("InpHeroes", "e.g. Thief+Fighter", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpHeroes", out var inp))
            inp.onValueChanged.AddListener(v => _node.Heroes = v.Split('+').Select(s => s.Trim()).ToList());
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpHeroes", out var inp))
            inp.text = string.Join("+", _node.Heroes);
    }
}

// =====================================================================
// CONTEXT & PHASE WRAPPERS
// =====================================================================

public class ContextWrapperBlockUI : UIBlockNode
{
    private ContextWrapperBlock _node;

    public ContextWrapperBlockUI(ContextWrapperBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle()
    {
        return _node.Prefix switch
        {
            "ch" => "CHOOSABLE REWARD (ch.)",
            "ph" => "IMPLIED PHASE (ph.)",
            "phi" => "INDEXED PHASE (phi.)",
            "phmp" => "MODIFIER PICK PHASE (phmp.)",
            _ => $"CONTEXT WRAPPER ({_node.Prefix}.)"
        };
    }

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        var rows = new List<GridRowSpec>();

        if (_node.Prefix == "phi" || _node.Prefix == "phmp")
        {
            rows.Add(new GridRowSpec(
                GridCellSpec.CreateLabel("LblTarget", "Phase Target / Index:", 0.4f),
                GridCellSpec.CreateInput("InpTarget", "e.g. 2 or +", 0.6f, null)
            ));
        }
        else
        {
            rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop a Reward Tag block inside this wrapper in the workspace.</i>", 1f)));
        }

        return rows;
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpTarget", out var inpTarget))
            inpTarget.onValueChanged.AddListener(v => _node.Target = v);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpTarget", out var inpTarget))
            inpTarget.text = _node.Target;
    }
}

public class Phase_SimpleChoiceBlockUI : UIBlockNode
{
    private Phase_SimpleChoiceBlock _node;

    public Phase_SimpleChoiceBlockUI(Phase_SimpleChoiceBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "SIMPLE CHOICE PHASE (ph.!)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTitleStr", "Screen Title (Optional):", 0.4f),
                GridCellSpec.CreateInput("InpTitle", "e.g. Choose a Curse!", 0.6f, null)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("LblChildInfo", "<i>Drop Reward options inside this block to populate the choice screen (@3).</i>", 1f))
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpTitle", out var inp))
            inp.onValueChanged.AddListener(v => _node.Title = v);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpTitle", out var inp))
            inp.text = _node.Title;
    }
}

// =====================================================================
// REWARD TAGS
// =====================================================================

public class Reward_StandardBlockUI : UIBlockNode
{
    private Reward_StandardBlock _node;
    private readonly string[] _typeOptions = { "Item (i)", "Modifier (m)", "Hero (g)", "LevelUp (l)" };

    public Reward_StandardBlockUI(Reward_StandardBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "STANDARD REWARD TAG";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblType", "Reward Type:", 0.35f),
                GridCellSpec.CreateDropdown("DropRewardType", "", 0.65f, _typeOptions, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTarget", "Entity Name:", 0.35f),
                GridCellSpec.CreateInput("InpTarget", "e.g. Mana Jelly", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropRewardType", out var drop))
        {
            drop.onValueChanged.AddListener(val =>
            {
                if (val == 0) _node.Type = Reward_StandardBlock.RewardType.Item;
                else if (val == 1) _node.Type = Reward_StandardBlock.RewardType.Modifier;
                else if (val == 2) _node.Type = Reward_StandardBlock.RewardType.Hero;
                else if (val == 3) _node.Type = Reward_StandardBlock.RewardType.LevelUp;
            });
        }

        if (refs.Inputs.TryGetValue("InpTarget", out var inp))
            inp.onValueChanged.AddListener(v => _node.TargetEntity = v);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropRewardType", out var drop))
        {
            if (_node.Type == Reward_StandardBlock.RewardType.Item) drop.value = 0;
            else if (_node.Type == Reward_StandardBlock.RewardType.Modifier) drop.value = 1;
            else if (_node.Type == Reward_StandardBlock.RewardType.Hero) drop.value = 2;
            else if (_node.Type == Reward_StandardBlock.RewardType.LevelUp) drop.value = 3;
        }

        if (refs.Inputs.TryGetValue("InpTarget", out var inp))
            inp.text = _node.TargetEntity;
    }
}

public class Reward_RandomBlockUI : UIBlockNode
{
    private Reward_RandomBlock _node;
    private readonly string[] _flagOptions = { "Item (i)", "Modifier (m)", "Hero (g)", "LevelUp (l)" };

    public Reward_RandomBlockUI(Reward_RandomBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "RANDOM REWARD TAG (r/q)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblMin", "Min Tier:", 0.35f),
                GridCellSpec.CreateInput("InpMin", "1", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblMax", "Max Tier:", 0.35f),
                GridCellSpec.CreateInput("InpMax", "1", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblAmt", "Amount:", 0.35f),
                GridCellSpec.CreateInput("InpAmt", "1", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblFlag", "Target Type:", 0.35f),
                GridCellSpec.CreateDropdown("DropFlag", "", 0.65f, _flagOptions, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMin", out var inpMin))
            inpMin.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.MinTier = i; });

        if (refs.Inputs.TryGetValue("InpMax", out var inpMax))
            inpMax.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.MaxTier = i; });

        if (refs.Inputs.TryGetValue("InpAmt", out var inpAmt))
            inpAmt.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Amount = i; });

        if (refs.Dropdowns.TryGetValue("DropFlag", out var drop))
        {
            drop.onValueChanged.AddListener(val =>
            {
                if (val == 0) _node.RewardTypeFlag = "i";
                else if (val == 1) _node.RewardTypeFlag = "m";
                else if (val == 2) _node.RewardTypeFlag = "g";
                else if (val == 3) _node.RewardTypeFlag = "l";
            });
        }
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMin", out var inpMin)) inpMin.text = _node.MinTier.ToString();
        if (refs.Inputs.TryGetValue("InpMax", out var inpMax)) inpMax.text = _node.MaxTier.ToString();
        if (refs.Inputs.TryGetValue("InpAmt", out var inpAmt)) inpAmt.text = _node.Amount.ToString();

        if (refs.Dropdowns.TryGetValue("DropFlag", out var drop))
        {
            if (_node.RewardTypeFlag == "i") drop.value = 0;
            else if (_node.RewardTypeFlag == "m") drop.value = 1;
            else if (_node.RewardTypeFlag == "g") drop.value = 2;
            else if (_node.RewardTypeFlag == "l") drop.value = 3;
        }
    }
}

public class Reward_EnumItemBlockUI : UIBlockNode
{
    private Reward_EnumItemBlock _node;
    private readonly string[] _enumOptions = { "RandoKeywordT1Item", "RandoKeywordT5Item", "RandoKeywordT7Item" };

    public Reward_EnumItemBlockUI(Reward_EnumItemBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "ENUM ITEM TAG (e)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblEnum", "Enum Type:", 0.35f),
                GridCellSpec.CreateDropdown("DropEnum", "", 0.65f, _enumOptions, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropEnum", out var drop))
            drop.onValueChanged.AddListener(val => _node.EnumName = _enumOptions[val]);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropEnum", out var drop))
            drop.value = System.Array.IndexOf(_enumOptions, _node.EnumName);
    }
}

public class Reward_ValueModifyBlockUI : UIBlockNode
{
    private Reward_ValueModifyBlock _node;

    public Reward_ValueModifyBlockUI(Reward_ValueModifyBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "MODIFY VARIABLE TAG (v)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblVar", "Variable Name:", 0.4f),
                GridCellSpec.CreateInput("InpVar", "e.g. Gold", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblVal", "Value Added (V):", 0.4f),
                GridCellSpec.CreateInput("InpVal", "e.g. 50", 0.6f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var inpVar))
            inpVar.onValueChanged.AddListener(v => _node.VariableName = v);

        if (refs.Inputs.TryGetValue("InpVal", out var inpVal))
            inpVal.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.ValueToAdd = i; });
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var inpVar)) inpVar.text = _node.VariableName;
        if (refs.Inputs.TryGetValue("InpVal", out var inpVal)) inpVal.text = _node.ValueToAdd.ToString();
    }
}

public class Reward_ReplaceBlockUI : UIBlockNode
{
    private Reward_ReplaceBlock _node;

    public Reward_ReplaceBlockUI(Reward_ReplaceBlock node) : base(node) { _node = node; }

    public override string GetBlockTitle() => "REPLACE TAG (p)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblModFlag", "Is Modifier Removal (pm):", 0.7f),
                GridCellSpec.CreateToggle("TglModFlag", "", 0.3f, null) // Assuming UI Gen supports Toggle, else use Dropdown
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTarget", "Target to Remove:", 0.35f),
                GridCellSpec.CreateInput("InpTarget", "e.g. Wurst", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblNew", "New Reward (Raw):", 0.35f),
                GridCellSpec.CreateInput("InpNew", "e.g. gPaladin", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        // Fallback to text inputs parsing bools if Toggles aren't in your FullScreenUIGenerator yet
        if (refs.Toggles != null && refs.Toggles.TryGetValue("TglModFlag", out var tgl))
            tgl.onValueChanged.AddListener(v => _node.IsModifierReplacement = v);

        if (refs.Inputs.TryGetValue("InpTarget", out var inpTarget))
            inpTarget.onValueChanged.AddListener(v => _node.TargetToReplace = v);

        if (refs.Inputs.TryGetValue("InpNew", out var inpNew))
            inpNew.onValueChanged.AddListener(v => _node.NewValue = v);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Toggles != null && refs.Toggles.TryGetValue("TglModFlag", out var tgl))
            tgl.isOn = _node.IsModifierReplacement;

        if (refs.Inputs.TryGetValue("InpTarget", out var inpTarget)) inpTarget.text = _node.TargetToReplace;
        if (refs.Inputs.TryGetValue("InpNew", out var inpNew)) inpNew.text = _node.NewValue;
    }
}

public class Reward_SkipBlockUI : UIBlockNode
{
    public Reward_SkipBlockUI(Reward_SkipBlock node) : base(node) { }
    public override string GetBlockTitle() => "SKIP TAG (s)";
    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>The Skip tag has no configuration properties.</i>", 1f))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs) { }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs) { }
}

// =====================================================================
// PHASE BLOCKS
// =====================================================================

public class Phase_MessageBlockUI : UIBlockNode
{
    private Phase_MessageBlock _node;

    public Phase_MessageBlockUI(Phase_MessageBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "MESSAGE PHASE (ph.4)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblMsg", "Message Content:", 0.35f),
                GridCellSpec.CreateInput("InpMsg", "e.g. Hello World", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblBtn", "Button Text:", 0.35f),
                GridCellSpec.CreateInput("InpBtn", "Ok", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMsg", out var inpMsg))
            inpMsg.onValueChanged.AddListener(v => _node.Message = v);
        if (refs.Inputs.TryGetValue("InpBtn", out var inpBtn))
            inpBtn.onValueChanged.AddListener(v => _node.ButtonText = v);
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMsg", out var inpMsg)) inpMsg.text = _node.Message;
        if (refs.Inputs.TryGetValue("InpBtn", out var inpBtn)) inpBtn.text = _node.ButtonText;
    }
}

public class Phase_HeroChangeBlockUI : UIBlockNode
{
    private Phase_HeroChangeBlock _node;
    private readonly string[] _typeOptions = { "Generated Hero (1)", "Random Class (0)" };

    public Phase_HeroChangeBlockUI(Phase_HeroChangeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "HERO CHANGE PHASE (ph.5)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblPos", "Hero Position Index:", 0.5f),
                GridCellSpec.CreateInput("InpPos", "e.g. 0 for top", 0.5f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblType", "Change Type:", 0.5f),
                GridCellSpec.CreateDropdown("DropType", "", 0.5f, _typeOptions, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpPos", out var inp))
            inp.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.HeroPositionIndex = i; });
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.onValueChanged.AddListener(val => _node.IsRandomClass = (val == 1));
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpPos", out var inp)) inp.text = _node.HeroPositionIndex.ToString();
        if (refs.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = _node.IsRandomClass ? 1 : 0;
    }
}

public class Phase_ItemCombineBlockUI : UIBlockNode
{
    private Phase_ItemCombineBlock _node;
    private readonly string[] _rules = { "2nd Highest -> Tier 3s", "Tier 0-3 -> Single Item" };

    public Phase_ItemCombineBlockUI(Phase_ItemCombineBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "ITEM COMBINE PHASE (ph.7)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblRule", "Combine Rule:", 0.4f), GridCellSpec.CreateDropdown("DropRule", "", 0.6f, _rules, null))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropRule", out var drop))
            drop.onValueChanged.AddListener(val => _node.Rule = (Phase_ItemCombineBlock.CombineRule)val);
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropRule", out var drop)) drop.value = (int)_node.Rule;
    }
}

public class Phase_PositionSwapBlockUI : UIBlockNode
{
    private Phase_PositionSwapBlock _node;

    public Phase_PositionSwapBlockUI(Phase_PositionSwapBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "POSITION SWAP PHASE (ph.8)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblA", "Hero Index A:", 0.5f), GridCellSpec.CreateInput("InpA", "0", 0.5f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblB", "Hero Index B:", 0.5f), GridCellSpec.CreateInput("InpB", "1", 0.5f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpA", out var inpA)) inpA.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.IndexA = i; });
        if (refs.Inputs.TryGetValue("InpB", out var inpB)) inpB.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.IndexB = i; });
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpA", out var inpA)) inpA.text = _node.IndexA.ToString();
        if (refs.Inputs.TryGetValue("InpB", out var inpB)) inpB.text = _node.IndexB.ToString();
    }
}

public class Phase_ChoiceBlockUI : UIBlockNode
{
    private Phase_ChoiceBlock _node;
    private readonly string[] _types = { "PointBuy", "Number", "UpToNumber", "Optional" };

    public Phase_ChoiceBlockUI(Phase_ChoiceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "CHOICE PHASE (ph.c)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Choice Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _types, null)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblNum", "Number/Limit:", 0.4f), GridCellSpec.CreateInput("InpNum", "1", 0.6f, null)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblTitle", "Title (Optional):", 0.4f), GridCellSpec.CreateInput("InpTitle", "", 0.6f, null)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Reward tags inside to populate choices (@3)</i>", 1f))
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop)) drop.onValueChanged.AddListener(v => _node.ChoiceType = _types[v]);
        if (refs.Inputs.TryGetValue("InpNum", out var inpNum)) inpNum.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.NumChoices = i; });
        if (refs.Inputs.TryGetValue("InpTitle", out var inpT)) inpT.onValueChanged.AddListener(v => _node.Title = v);
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = System.Array.IndexOf(_types, _node.ChoiceType);
        if (refs.Inputs.TryGetValue("InpNum", out var inpNum)) inpNum.text = _node.NumChoices.ToString();
        if (refs.Inputs.TryGetValue("InpTitle", out var inpT)) inpT.text = _node.Title;
    }
}

public class Phase_StaticBlockUI : UIBlockNode
{
    private Phase_StaticBlock _node;
    private readonly string[] _options = System.Enum.GetNames(typeof(Phase_StaticBlock.StaticPhase));

    public Phase_StaticBlockUI(Phase_StaticBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "STATIC / EVENT PHASE";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Phase Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _options, null))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.onValueChanged.AddListener(v => _node.Phase = (Phase_StaticBlock.StaticPhase)System.Enum.GetValues(typeof(Phase_StaticBlock.StaticPhase)).GetValue(v));
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.value = System.Array.IndexOf(System.Enum.GetValues(typeof(Phase_StaticBlock.StaticPhase)), _node.Phase);
    }
}

public class Phase_GenerateScreenBlockUI : UIBlockNode
{
    private Phase_GenerateScreenBlock _node;
    private readonly string[] _options = { "Item Screen (i)", "LevelUp Screen (h)" };

    public Phase_GenerateScreenBlockUI(Phase_GenerateScreenBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "GENERATE SCREEN (ph.g)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Screen Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _options, null))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.onValueChanged.AddListener(v => _node.Type = v == 0 ? Phase_GenerateScreenBlock.ScreenType.Item : Phase_GenerateScreenBlock.ScreenType.LevelUp);
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.value = _node.Type == Phase_GenerateScreenBlock.ScreenType.Item ? 0 : 1;
    }
}

// Container blocks requiring just info text
public class Phase_LinkedBlockUI : UIBlockNode
{
    public Phase_LinkedBlockUI(Phase_LinkedBlock node) : base(node) { }
    public override string GetBlockTitle() => "LINKED PHASES (ph.l)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Phases inside to link them sequentially (@1)</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class Phase_LevelEndBlockUI : UIBlockNode
{
    public Phase_LevelEndBlockUI(Phase_LevelEndBlock node) : base(node) { }
    public override string GetBlockTitle() => "LEVEL END PHASE (ph.2)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Phases inside to attach them to the end screen</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class Phase_ChallengeBlockUI : UIBlockNode
{
    private Phase_ChallengeBlock _node;
    public Phase_ChallengeBlockUI(Phase_ChallengeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "CHALLENGE PHASE (ph.9)";
    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec> {
            new GridRowSpec(GridCellSpec.CreateLabel("LblMon", "Extra Monsters (+ split):", 0.4f), GridCellSpec.CreateInput("InpMon", "e.g. Militia+Militia", 0.6f, null)),
            new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop the Reward inside this block.</i>", 1f))
        };
    }
    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMon", out var inp)) inp.onValueChanged.AddListener(v => _node.ExtraMonsters = v.Split('+').Select(s => s.Trim()).ToList());
    }
    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMon", out var inp)) inp.text = string.Join("+", _node.ExtraMonsters);
    }
}

// =====================================================================
// COMPLEX BRANCHING PHASES (Stopgap Text Input UIs)
// =====================================================================

public class Phase_Boolean1BlockUI : UIBlockNode
{
    private Phase_Boolean1Block _node;

    public Phase_Boolean1BlockUI(Phase_Boolean1Block node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "BOOLEAN PHASE (ph.b)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblVar", "Variable to Check:", 0.4f),
                GridCellSpec.CreateInput("InpVar", "e.g. Seed", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblThresh", "Threshold (>=):", 0.4f),
                GridCellSpec.CreateInput("InpThresh", "1", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTrue", "True Branch (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpTrue", "e.g. !m1.fight.Boar", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblFalse", "False Branch (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpFalse", "e.g. !s", 0.6f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var iVar)) iVar.onValueChanged.AddListener(v => _node.VariableName = v);
        if (refs.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Threshold = i; });

        if (refs.Inputs.TryGetValue("InpTrue", out var iTrue))
            iTrue.onValueChanged.AddListener(v => _node.TrueBranch = new RawTextNode(v));

        if (refs.Inputs.TryGetValue("InpFalse", out var iFalse))
            iFalse.onValueChanged.AddListener(v => _node.FalseBranch = new RawTextNode(v));
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var iVar)) iVar.text = _node.VariableName;
        if (refs.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.text = _node.Threshold.ToString();
        if (refs.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.text = (_node.TrueBranch as RawTextNode)?.Text ?? "";
        if (refs.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.text = (_node.FalseBranch as RawTextNode)?.Text ?? "";
    }
}

public class Phase_Boolean2BlockUI : UIBlockNode
{
    private Phase_Boolean2Block _node;

    public Phase_Boolean2BlockUI(Phase_Boolean2Block node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "BOOLEAN PHASE 2 (ph.z)";

    // Shares identical layout with Boolean 1, just binds to the Boolean 2 AST node
    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblVar", "Variable to Check:", 0.4f),
                GridCellSpec.CreateInput("InpVar", "e.g. gold", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblThresh", "Threshold (>=):", 0.4f),
                GridCellSpec.CreateInput("InpThresh", "1", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTrue", "True Branch (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpTrue", "e.g. !vgoldV-400", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblFalse", "False Branch (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpFalse", "e.g. 4You can't afford that!", 0.6f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var iVar)) iVar.onValueChanged.AddListener(v => _node.VariableName = v);
        if (refs.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Threshold = i; });
        if (refs.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.onValueChanged.AddListener(v => _node.TrueBranch = new RawTextNode(v));
        if (refs.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.onValueChanged.AddListener(v => _node.FalseBranch = new RawTextNode(v));
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpVar", out var iVar)) iVar.text = _node.VariableName;
        if (refs.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.text = _node.Threshold.ToString();
        if (refs.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.text = (_node.TrueBranch as RawTextNode)?.Text ?? "";
        if (refs.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.text = (_node.FalseBranch as RawTextNode)?.Text ?? "";
    }
}

public class Phase_TradeBlockUI : UIBlockNode
{
    private Phase_TradeBlock _node;

    public Phase_TradeBlockUI(Phase_TradeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "TRADE / CURSED CHEST (ph.t)";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblItem1", "Reward 1 (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpItem1", "e.g. r1~4~i", 0.6f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblItem2", "Reward 2 (Raw):", 0.4f),
                GridCellSpec.CreateInput("InpItem2", "e.g. r-1~1~m", 0.6f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpItem1", out var i1)) i1.onValueChanged.AddListener(v => _node.Item1 = new RawTextNode(v));
        if (refs.Inputs.TryGetValue("InpItem2", out var i2)) i2.onValueChanged.AddListener(v => _node.Item2 = new RawTextNode(v));
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpItem1", out var i1)) i1.text = (_node.Item1 as RawTextNode)?.Text ?? "";
        if (refs.Inputs.TryGetValue("InpItem2", out var i2)) i2.text = (_node.Item2 as RawTextNode)?.Text ?? "";
    }
}

public class Phase_SequenceBlockUI : UIBlockNode
{
    private Phase_SequenceBlock _node;

    public Phase_SequenceBlockUI(Phase_SequenceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "SEQUENCE PHASE (ph.s)";

    // As a stopgap for lists, we'll hardcode inputs for exactly 2 steps.
    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblMsg", "Initial Message:", 0.35f),
                GridCellSpec.CreateInput("InpMsg", "e.g. Choose a Party", 0.65f, null)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("LblS1", "<b>--- Step 1 ---</b>", 1f)),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblBtn1", "Button 1 Text:", 0.35f),
                GridCellSpec.CreateInput("InpBtn1", "e.g. [Scoundrel]...", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblAct1", "Action 1 (Raw):", 0.35f),
                GridCellSpec.CreateInput("InpAct1", "e.g. !mparty.Scoundrel", 0.65f, null)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("LblS2", "<b>--- Step 2 ---</b>", 1f)),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblBtn2", "Button 2 Text:", 0.35f),
                GridCellSpec.CreateInput("InpBtn2", "e.g. [Dabble]...", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblAct2", "Action 2 (Raw):", 0.35f),
                GridCellSpec.CreateInput("InpAct2", "e.g. !mparty.Dabble", 0.65f, null)
            )
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMsg", out var iMsg)) iMsg.onValueChanged.AddListener(v => _node.SequenceMessage = v);

        // We rebuild the list whenever a value changes to keep it synchronized
        UnityEngine.Events.UnityAction rebuildList = () =>
        {
            _node.Steps.Clear();
            string btn1 = refs.Inputs["InpBtn1"].text;
            string act1 = refs.Inputs["InpAct1"].text;
            if (!string.IsNullOrEmpty(btn1))
                _node.Steps.Add(new Phase_SequenceBlock.SequenceStep { ButtonText = btn1, Action = new RawTextNode(act1) });

            string btn2 = refs.Inputs["InpBtn2"].text;
            string act2 = refs.Inputs["InpAct2"].text;
            if (!string.IsNullOrEmpty(btn2))
                _node.Steps.Add(new Phase_SequenceBlock.SequenceStep { ButtonText = btn2, Action = new RawTextNode(act2) });
        };

        if (refs.Inputs.TryGetValue("InpBtn1", out var iB1)) iB1.onValueChanged.AddListener(_ => rebuildList());
        if (refs.Inputs.TryGetValue("InpAct1", out var iA1)) iA1.onValueChanged.AddListener(_ => rebuildList());
        if (refs.Inputs.TryGetValue("InpBtn2", out var iB2)) iB2.onValueChanged.AddListener(_ => rebuildList());
        if (refs.Inputs.TryGetValue("InpAct2", out var iA2)) iA2.onValueChanged.AddListener(_ => rebuildList());
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Inputs.TryGetValue("InpMsg", out var iMsg)) iMsg.text = _node.SequenceMessage;

        if (_node.Steps.Count > 0)
        {
            if (refs.Inputs.TryGetValue("InpBtn1", out var iB1)) iB1.text = _node.Steps[0].ButtonText;
            if (refs.Inputs.TryGetValue("InpAct1", out var iA1)) iA1.text = (_node.Steps[0].Action as RawTextNode)?.Text ?? "";
        }
        if (_node.Steps.Count > 1)
        {
            if (refs.Inputs.TryGetValue("InpBtn2", out var iB2)) iB2.text = _node.Steps[1].ButtonText;
            if (refs.Inputs.TryGetValue("InpAct2", out var iA2)) iA2.text = (_node.Steps[1].Action as RawTextNode)?.Text ?? "";
        }
    }
}