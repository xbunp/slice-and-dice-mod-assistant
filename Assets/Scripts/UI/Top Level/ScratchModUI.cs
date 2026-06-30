using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public VisualBlockCard CurrentlyInspectedCard { get; private set; }

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
            new GridRowSpec(rowHeight, GridCellSpec.CreateButton("PasteModBtn", "Paste (Decompile)", 1.0f, PasteFromClipboard)),
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
            new GridRowSpec(400f, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f))
        };

        List<GridRowSpec> rightRows = new List<GridRowSpec>
        {
            new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("INSPECTOR_TITLE", "INSPECTOR PROPERTIES", 1.0f)),
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
            ApplyDynamicLayoutConstraints();
        }
    }

    public void SelectCardForInspection(VisualBlockCard card)
    {
        if (CurrentlyInspectedCard != null) CurrentlyInspectedCard.Deselect();

        CurrentlyInspectedCard = card;

        foreach (Transform child in _inspectorContainer)
        {
            Destroy(child.gameObject);
        }

        if (card == null || card.UINode == null) return;

        card.UINode.OnDeleteRequested = DeleteSelectedBlock;
        GridReferences refs = uiGenerator.RebuildGrid(_inspectorContainer, card.UINode.GetRowSpecs(), useMargins: false);
        card.UINode.BindUI(_inspectorContainer, refs);
        card.UINode.RestoreState(_inspectorContainer, refs);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_inspectorContainer);
    }

    private void PasteFromClipboard()
    {
        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard)) return;

        ClearWorkspace();

        ITextmodNode decompiled = TextmodDecompiler.Decompile(clipboard);
        if (decompiled == null) return;

        if (decompiled is CommaChainBlock chain)
        {
            foreach (var node in chain.Nodes)
            {
                string name = CodeBlocks.GetOptionNameForNode(node);
                InstantiateCard(node, name, _rootWorkspaceZone);
            }
        }
        else
        {
            string name = CodeBlocks.GetOptionNameForNode(decompiled);
            InstantiateCard(decompiled, name, _rootWorkspaceZone);
        }
    }

    private VisualBlockCard InstantiateCard(ITextmodNode compilerNode, string optionName, ReorderableZone zone)
    {
        UIBlockNode uiNode = BlockRegistry.CreateUI(compilerNode);

        GameObject cardGo = new GameObject($"Card_{optionName}", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        VisualBlockCard card = cardGo.AddComponent<VisualBlockCard>();

        bool canHoldChildren = compilerNode is IBlockContainer || compilerNode is IBlockWrapper;

        card.Initialize(optionName, canHoldChildren);
        card.CompilerNode = compilerNode;
        card.UINode = uiNode;

        if (card.NestedZone != null) card.NestedZone.SetCanvas(_rootCanvas);
        zone.AddEntrant(card);

        PopulateNestedZone(compilerNode, card.NestedZone);
        return card;
    }

    private void PopulateNestedZone(ITextmodNode node, ReorderableZone zone)
    {
        if (zone == null) return;

        if (node is IBlockContainer container && container.ChildNodes != null)
        {
            foreach (var child in container.ChildNodes)
                InstantiateCard(child, BlockRegistry.GetNodeName(child), zone);
        }
        else if (node is IBlockWrapper wrapper && wrapper.PayloadNode != null)
        {
            if (wrapper.PayloadNode is CommaChainBlock cc)
                foreach (var c in cc.Nodes) InstantiateCard(c, BlockRegistry.GetNodeName(c), zone);
            else
                InstantiateCard(wrapper.PayloadNode, BlockRegistry.GetNodeName(wrapper.PayloadNode), zone);
        }
    }

    private List<ITextmodNode> CompileZoneRecursive(ReorderableZone zone)
    {
        List<ITextmodNode> compiledNodes = new List<ITextmodNode>();

        foreach (var entrant in zone.Entrants)
        {
            var card = entrant as VisualBlockCard;
            if (card == null) continue;

            if (card.NestedZone != null)
            {
                List<ITextmodNode> childNodes = CompileZoneRecursive(card.NestedZone);

                if (card.CompilerNode is IBlockContainer container)
                    container.ChildNodes = childNodes;
                else if (card.CompilerNode is IBlockWrapper wrapper)
                    wrapper.PayloadNode = GetSingleOrChain(childNodes);
            }
            compiledNodes.Add(card.CompilerNode);
        }
        return compiledNodes;
    }

    private void AddBlockToWorkspace(string optionName)
    {
        if (_rootWorkspaceZone == null) return;
        ITextmodNode compilerNode = BlockRegistry.CreateNode(optionName);
        if (compilerNode == null) return;

        var card = InstantiateCard(compilerNode, optionName, _rootWorkspaceZone);
        SelectCardForInspection(card);
    }

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
                layout.padding = new RectOffset(12, 12, 12, 12);
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
        if (CurrentlyInspectedCard == null) return;

        if (CurrentlyInspectedCard.CurrentZone != null)
            CurrentlyInspectedCard.CurrentZone.RemoveEntrant(CurrentlyInspectedCard);

        Destroy(CurrentlyInspectedCard.gameObject);
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

    private ITextmodNode GetSingleOrChain(List<ITextmodNode> nodes)
    {
        if (nodes == null || nodes.Count == 0) return null;
        if (nodes.Count == 1) return nodes[0];

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

    private void LoadModFromClipboard() { }

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
}
#endregion

#region 2. FACTORIES & DEFINITIONS
public static class CodeBlocks
{
    private static readonly Dictionary<string, Func<ITextmodNode>> _registry = new Dictionary<string, Func<ITextmodNode>>
    {
        { "Chain: Comma (Top Level AND)", () => new CommaChainBlock() },
        { "Chain: Ampersand (Nested AND)", () => new AndChainBlock() },
        { "Wrapper: Floor Condition", () => new FloorConditionBlock() },
        { "Wrapper: Multiplier (xN)", () => new MultiplierBlock() },
        { "Command: Add Entity (add.)", () => new AddEntityBlock() },
        { "Command: Fight Encounter (fight.)", () => new FightBlock() },
        { "Command: Set Party (party.)", () => new PartyBlock() },
        { "Command: Replace Entity (replace.)", () => new ReplaceCommandBlock() },
        { "Command: Add to Pool (item/hero/monster)", () => new PoolBlock() },
        { "Command: Grant All Items (allitem/alliteme)", () => new AllItemBlock() },
        { "Command: Set Zone (zone.)", () => new ZoneBlock() },
        { "Command: Set Difficulty (diff.)", () => new DifficultyBlock() },
        { "Command: Level Constraint (lvl.)", () => new LevelConstraintBlock() },
        { "Global Command", () => new GlobalCommandBlock() },
        { "Context: Implied Phase (ph.)", () => new PhaseContextBlock() },
        { "Context: Indexed Game Phase (phi.)", () => new IndexedPhaseContextBlock() },
        { "Context: Modifier Pick Phase (phmp.)", () => new ModPickContextBlock() },
        { "Context: Choosable Reward (ch.)", () => new ChoosableContextBlock() },
        { "Phase: Simple Choice (!)", () => new Phase_SimpleChoiceBlock() },
        { "Phase: Level End Screen (2)", () => new Phase_LevelEndBlock() },
        { "Phase: Message Popup (4)", () => new Phase_MessageBlock() },
        { "Phase: Hero Change Offer (5)", () => new Phase_HeroChangeBlock() },
        { "Phase: Item Combine / Smithing (7)", () => new Phase_ItemCombineBlock() },
        { "Phase: Position Swap (8)", () => new Phase_PositionSwapBlock() },
        { "Phase: Challenge Phase (9)", () => new Phase_ChallengeBlock() },
        { "Phase: Boolean Check 1 (b)", () => new Phase_Boolean1Block() },
        { "Phase: Choice Screen (c)", () => new Phase_ChoiceBlock() },
        { "Phase: Linked Events (l)", () => new Phase_LinkedBlock() },
        { "Phase: Random Reveal Popup (r)", () => new Phase_RandomRevealBlock() },
        { "Phase: Story Sequence (s)", () => new Phase_SequenceBlock() },
        { "Phase: Cursed Chest Trade (t)", () => new Phase_TradeBlock() },
        { "Phase: Generate Screen (g)", () => new Phase_GenerateScreenBlock() },
        { "Phase: Boolean Check 2 (z)", () => new Phase_Boolean2Block() },
        { "Phase: Static (0,1,3,d,6,e)", () => new Phase_StaticBlock() },
        { "Reward: Standard (i/m/g/l)", () => new Reward_StandardBlock() },
        { "Reward: Random Reward (r/q)", () => new Reward_RandomBlock() },
        { "Reward: Random Choice (o)", () => new Reward_ChoiceBlock() },
        { "Reward: Enum Item (e)", () => new Reward_EnumItemBlock() },
        { "Reward: Modify Variable (v)", () => new Reward_ValueModifyBlock() },
        { "Reward: Replace Reward (p)", () => new Reward_ReplaceBlock() },
        { "Reward: Skip (s)", () => new Reward_SkipBlock() },
        { "Sequence Option Fork", () => new SequenceOptionBlock() }
    };

    public static readonly List<string> _blockSyntaxOptions = _registry.Keys.ToList();
    public static string GetOptionNameForNode(ITextmodNode node)
    {
        if (node == null) return "GENERIC BLOCK";
        System.Type targetType = node.GetType();

        foreach (var pair in _registry)
        {
            var testInstance = pair.Value();
            if (testInstance != null && testInstance.GetType() == targetType)
            {
                if (node is ContextWrapperBlock actualCw && testInstance is ContextWrapperBlock templateCw)
                {
                    if (actualCw.Prefix != templateCw.Prefix) continue;
                }
                return pair.Key;
            }
        }
        return "GENERIC BLOCK";
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
public abstract class UIBlockNode
{
    protected TextmodBlock BaseCompilerNode;

    public System.Action OnDeleteRequested;

    protected UIBlockNode(ITextmodNode baseNode)
    {
        BaseCompilerNode = baseNode as TextmodBlock;
    }

    public abstract string GetBlockTitle();
    protected abstract List<GridRowSpec> GetSpecificRowSpecs();
    protected abstract void BindSpecificUI(RectTransform container, GridReferences refs);
    protected abstract void RestoreSpecificState(RectTransform container, GridReferences refs);

    protected List<GridRowSpec> GetHeaderRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblTitle", $"<b>{GetBlockTitle()}</b>", 0.70f),
                GridCellSpec.CreateButton("BtnDeleteBlock", "Delete", 0.30f, () => OnDeleteRequested?.Invoke())
            )
        };
    }

    protected List<GridRowSpec> GetUniversalRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(GridCellSpec.CreateLabel("LblBaseProps", "General Properties", 1.0f)),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblEncName", "Modifier Name (.mn):", 0.35f),
                GridCellSpec.CreateInput("InpEncounterName", "", 0.65f, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblHidden", "Hidden:", 0.35f),
                GridCellSpec.CreateToggle("TglHidden", "", 0.15f, null),
                GridCellSpec.CreateLabel("LblTemp", "Temporary:", 0.35f),
                GridCellSpec.CreateToggle("TglTemp", "", 0.15f, null)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("LblSpecificPropsHeader", "Node Specific Properties", 1.0f))
        };
    }

    public List<GridRowSpec> GetRowSpecs()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        rows.AddRange(GetHeaderRowSpecs());

        if (BaseCompilerNode != null)
        {
            rows.AddRange(GetUniversalRowSpecs());
        }

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

            if (refs.Toggles != null)
            {
                if (refs.Toggles.TryGetValue("TglHidden", out var tglH))
                    tglH.onValueChanged.AddListener(v => BaseCompilerNode.IsHidden = v);
                if (refs.Toggles.TryGetValue("TglTemp", out var tglT))
                    tglT.onValueChanged.AddListener(v => BaseCompilerNode.IsTemporary = v);
            }
        }
    }

    public void RestoreState(RectTransform container, GridReferences refs)
    {
        RestoreSpecificState(container, refs);

        if (BaseCompilerNode != null)
        {
            if (refs.Inputs.TryGetValue("InpEncounterName", out var inpEncName))
                inpEncName.text = BaseCompilerNode.CustomEncounterName;

            if (refs.Toggles != null)
            {
                if (refs.Toggles.TryGetValue("TglHidden", out var tglH))
                    tglH.isOn = BaseCompilerNode.IsHidden;
                if (refs.Toggles.TryGetValue("TglTemp", out var tglT))
                    tglT.isOn = BaseCompilerNode.IsTemporary;
            }
        }
    }
}
#endregion

#region 5. COMPILER AST BASE STRUCTURES
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

/// <summary>
/// The abstract base class for all Textmod compiler blocks. 
/// </summary>
/// <remarks>
/// <para><b>THE TEXTMOD MODIFIER ENGINE &amp; SYNTAX RULES:</b></para>
/// <para>
/// The engine makes no strict syntactic distinction between what a modifier "is" or "can be." 
/// A modifier can be a massive mod-defining structural element (like a pool definition) or a 
/// simple gameplay mutator (like adding a monster to every fight). Because of this, it is 
/// syntactically valid to write mismatched modifiers (e.g., placing a hero pool inside a jinx monster 
/// payload). While the game will not crash, the mismatched payload simply won't perform any action, 
/// as the game evaluates modifiers based on their intended logical context.
/// </para>
/// 
/// <para><b>UNIVERSAL PARENTHETICAL WRAPPING ():</b></para>
/// <para>
/// Parentheses <c>()</c> can be wrapped around any modifier or group of modifiers at any time to 
/// help the code compile correctly, resolve syntax precedence, and prevent layout collisions in deep nests.
/// </para>
/// 
/// <para><b>GLOBAL MODIFIER UTILITIES:</b></para>
/// <list type="bullet">
/// <item><description><c>.doc.richtext</c>: Appends a custom richtext description to a modifier when viewed in the UI.</description></item>
/// <item><description><c>.mn.name</c>: Overrides the displayed custom mod name in the UI.</description></item>
/// <item><description><c>.part.#</c>: Subsections a modifier payload so only a specific part ID executes.</description></item>
/// </list>
/// 
/// <para><b>COMMON MUTATOR PROPERTIES &amp; TARGETS:</b></para>
/// <list type="bullet">
/// <item><description><c>hero.keyword</c> / <c>monster.keyword</c>: Bestows a specific passive keyword to all heroes or monsters.</description></item>
/// <item><description><c>sideposition.keyword</c>: Applies a keyword only to specified dice faces (see Dice Face Alignments below).</description></item>
/// <item><description><c>i.item</c>: Awards a specific item.</description></item>
/// <item><description><c>add.monster</c>: Spawns a designated monster in every fight.</description></item>
/// <item><description><c>add.hero</c>: Adds a new hero to the current run party.</description></item>
/// <item><description><c>party.hero+hero...</c>: Wipes the active party and inserts the specified set of heroes.</description></item>
/// <item><description><c>allitem.item</c> / <c>alliteme.item</c>: Bestows an item's passive effects to all heroes (allitem) or all monsters (alliteme).</description></item>
/// <item><description><c>peritem.item</c>: Applies a modifier's mutator effect to heroes scaled by how many items they have equipped.</description></item>
/// <item><description><c>monster.spirit</c>: Bestows a monster's passive trait as a constant passive mutator. Format: <c>&lt;MonsterProperName&gt;.spirit</c> (e.g., <c>Demon.spirit</c>, <c>Troll.spirit</c>).</description></item>
/// </list>
/// 
/// <para><b>DICE FACE ALIGNMENTS &amp; TARGETS:</b></para>
/// <para>
/// Sides are designated via text-based aliases corresponding to physical layout positions:
/// </para>
/// <list type="bullet">
/// <item><description><c>left</c>: Index 0 (Bitmask 1)</description></item>
/// <item><description><c>mid</c>: Index 1 (Bitmask 2)</description></item>
/// <item><description><c>top</c>: Index 2 (Bitmask 4)</description></item>
/// <item><description><c>bot</c>: Index 3 (Bitmask 8)</description></item>
/// <item><description><c>right</c>: Index 4 (Bitmask 16)</description></item>
/// <item><description><c>rightmost</c>: Index 5 (Bitmask 32)</description></item>
/// </list>
/// <para>
/// Compound Target Aliases:
/// </para>
/// <list type="bullet">
/// <item><description><c>all</c>: All faces (Bitmask 63)</description></item>
/// <item><description><c>right5</c>: All except left (Bitmask 62)</description></item>
/// <item><description><c>right3</c>: Middle, right, and rightmost (Bitmask 50)</description></item>
/// <item><description><c>right2</c>: Right and rightmost (Bitmask 48)</description></item>
/// <item><description><c>row</c>: Middle Row (Left, Mid, Right - Bitmask 19)</description></item>
/// <item><description><c>mid2</c>: Middle and right (Bitmask 18)</description></item>
/// <item><description><c>col</c>: Column (Mid, Top, Bot - Bitmask 14)</description></item>
/// <item><description><c>topbot</c>: Top and bottom (Bitmask 12)</description></item>
/// <item><description><c>left2</c>: Middle and left (Bitmask 3)</description></item>
/// </list>
/// 
/// <para><b>HERO STACK POSITION TARGETING:</b></para>
/// <list type="bullet">
/// <item><description><c>h.heroposition.modifier</c>: Restricts a mutator effect to specific slots in the hero stack. 
/// <i>Note: This behavior is highly contextual to the source of the modifier and requires active in-game exploration to predict exact stacking layouts.</i></description></item>
/// </list>
/// 
/// <para><b>SCALING, SPLICING &amp; CONDITIONAL SYNTAX:</b></para>
/// <list type="bullet">
/// <item><description><c>modtier.#</c>: Multiplies a gameplay mutator's scaling coefficient directly.</description></item>
/// <item><description><c>x2-9.modifier</c>: Duplicates a modifier's payload X times (functions slightly differently than modtier).</description></item>
/// <item><description><c>modifier&amp;modifier</c>: Packages two modifiers together so they act as a single inseparable payload.</description></item>
/// <item><description><c>modifier.splice.modifier</c>: Combines two modifiers in a combinatorial way to alter conditions/results (e.g., <c>mod1.splice.mod2</c>). Grouping with parentheses is valid. 
/// <i>Warning: Recursive splices (e.g. splicing splices) can be unstable and are generally not recommended.</i></description></item>
/// <item><description><c>modifier.splice.item</c>: Splices a modifier's active mutator behaviors directly into a standard item's properties.</description></item>
/// <item><description><c>unpack.modifier</c>: Forces the modifier to parse by stripping away any conditional wrapper filters.</description></item>
/// <item><description><c>delivery.xxxx</c>: Grants random items. Suffixes with 4 random alphanumeric characters (e.g., <c>delivery.1a90</c>, <c>delivery.87ad</c>). 
/// There is no programatic way to know in advance what items will be granted; these must be discovered experimentally.</description></item>
/// </list>
/// 
/// <para><b>END OF TURN ABILITY TRIGGERS (ea.):</b></para>
/// <para>
/// Casts a designated ability at the end of each turn. Syntax can target base-game abilities or custom internal payloads:
/// </para>
/// <list type="bullet">
/// <item><description>Base Game: <c>ea.Burst</c> or <c>ea.Luck</c></description></item>
/// <item><description>Complex Custom: <c>ea.&lt;s/t&gt;&lt;heroName&gt;.abilitydata.(&lt;internal_syntax&gt;)</c> 
/// (where <c>s</c> or <c>t</c> refers to Spell or Tactic respectively, e.g., <c>ea.sthief.abilitydata.(statue.sd.0-0:0-0:0-0:0-0:76-3:0-0)</c>).</description></item>
/// </list>
/// 
/// <para><b>RUN PROGRESSION STACKING (COMPLETED CHECKS):</b></para>
/// <list type="bullet">
/// <item><description><c>pl.modifier</c>: Evaluates and stacks the mutator an additional time for each standard fight completed during the run.</description></item>
/// <item><description><c>pb.modifier</c>: Stacks the mutator for each boss fight completed (maximum 5 bosses across a 20-floor run).</description></item>
/// <item><description><c>pt.modifier</c>: Stacks the mutator scaled by how many combat turns have elapsed during the active fight.</description></item>
/// </list>
/// </remarks>
public abstract class TextmodBlock : ITextmodNode
{
    public string CustomEncounterName = "";
    public string RichTextDocumentation = "";
    public int Part = -1;
    public bool IsHidden = false;
    public bool IsTemporary = false;

    public abstract string CompileCore();

    public string Compile()
    {
        string core = CompileCore();

        if (!string.IsNullOrEmpty(CustomEncounterName)) core += $".mn.{CustomEncounterName}";
        if (!string.IsNullOrEmpty(RichTextDocumentation)) core += $".doc.{RichTextDocumentation}";
        if (Part != -1) core += $".part.{Part}";

        bool needsWrap = IsHidden || IsTemporary;
        if (needsWrap)
        {
            string wrapped = $"({core}";
            if (IsHidden) wrapped += "&hidden";
            if (IsTemporary) wrapped += "&temporary";
            wrapped += ")";
            return wrapped;
        }

        return core;
    }

    protected string SafeCompile(ITextmodNode node)
    {
        if (node == null) return "";
        string compiled = node.Compile();

        char[] delimiters = { '&', ',', '+' };
        if (compiled.IndexOfAny(delimiters) >= 0 && !(compiled.StartsWith("(") && compiled.EndsWith(")")))
            return $"({compiled})";

        return compiled;
    }
}
#endregion

#region 6. DECOMPILER ENGINE
public static class TextmodDecompiler
{
    public static ITextmodNode Decompile(string input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        input = input.Trim().TrimEnd(',');

        List<string> topTokens = SplitOuter(input, ',');
        if (topTokens.Count > 1)
        {
            CommaChainBlock chain = new CommaChainBlock();
            foreach (var t in topTokens)
            {
                var node = DecompileSingle(t);
                if (node != null) chain.Nodes.Add(node);
            }
            return chain;
        }

        return DecompileSingle(input);
    }

    private static ITextmodNode DecompileSingle(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        token = Unwrap(token.Trim());

        List<string> andTokens = SplitOuter(token, '&');

        bool isHidden = false;
        bool isTemp = false;

        andTokens.RemoveAll(t =>
        {
            string lower = t.ToLower();
            if (lower == "hidden") { isHidden = true; return true; }
            if (lower == "temporary") { isTemp = true; return true; }
            return false;
        });

        if (andTokens.Count > 1)
        {
            AndChainBlock andChain = new AndChainBlock();
            foreach (var t in andTokens)
            {
                var thisNode = DecompileSingle(t);
                if (thisNode != null) andChain.Nodes.Add(thisNode);
            }
            return andChain;
        }

        string core = andTokens.Count == 1 ? andTokens[0] : token;

        string customName = "";
        string doc = "";
        int part = -1;

        int propIdx = -1;
        int depth = 0;
        for (int i = 0; i < core.Length; i++)
        {
            if (core[i] == '(' || core[i] == '[' || core[i] == '{') depth++;
            else if (core[i] == ')' || core[i] == ']' || core[i] == '}') depth--;
            else if (depth == 0 && core[i] == '.')
            {
                string rem = core.Substring(i);
                if (rem.StartsWith(".mn.") || rem.StartsWith(".n.") || rem.StartsWith(".doc.") || rem.StartsWith(".part."))
                {
                    propIdx = i;
                    break;
                }
            }
        }

        if (propIdx != -1)
        {
            string props = core.Substring(propIdx);
            core = core.Substring(0, propIdx);

            customName = ExtractTagValue(ref props, "mn") ?? ExtractTagValue(ref props, "n");
            doc = ExtractTagValue(ref props, "doc");
            string partStr = ExtractTagValue(ref props, "part");
            if (!string.IsNullOrEmpty(partStr)) int.TryParse(partStr, out part);
        }

        if (core.StartsWith("=")) core = core.Substring(1).Trim();

        ITextmodNode node = ParseCore(core);

        if (node is TextmodBlock block)
        {
            if (!string.IsNullOrEmpty(customName)) block.CustomEncounterName = customName;
            if (!string.IsNullOrEmpty(doc)) block.RichTextDocumentation = doc;
            if (part != -1) block.Part = part;
            block.IsHidden = isHidden;
            block.IsTemporary = isTemp;
        }

        return node;
    }

    private static ITextmodNode ParseCore(string core)
    {
        if (string.IsNullOrEmpty(core)) return null;

        Match multiMatch = Regex.Match(core, @"^x(\d+)\.(.*)");
        if (multiMatch.Success)
            return new MultiplierBlock { Multiplier = int.Parse(multiMatch.Groups[1].Value), Payload = DecompileSingle(multiMatch.Groups[2].Value) };

        Match floorMatch = Regex.Match(core, @"^((?:e\d+(?:\.\d+)?|\-?\d+(?:-\-?\d+)?))\.(.*)");
        if (floorMatch.Success)
        {
            string prefix = floorMatch.Groups[1].Value;
            string payload = floorMatch.Groups[2].Value;
            FloorConditionBlock fc = new FloorConditionBlock();

            if (prefix.Contains("-"))
            {
                fc.Type = FloorConditionBlock.ConditionType.Range;
                var p = prefix.Split('-');
                int.TryParse(p[0], out fc.StartFloor);
                int.TryParse(p[1], out fc.EndFloor);
            }
            else if (prefix.StartsWith("e"))
            {
                fc.Type = FloorConditionBlock.ConditionType.EveryX;
                var p = prefix.Substring(1).Split('.');
                int.TryParse(p[0], out fc.Interval);
                if (p.Length > 1) int.TryParse(p[1], out fc.Offset);
            }
            else
            {
                fc.Type = FloorConditionBlock.ConditionType.Single;
                int.TryParse(prefix, out fc.StartFloor);
            }
            fc.Payload = DecompileSingle(payload);
            return fc;
        }

        if (core.StartsWith("lvl.")) return new LevelConstraintBlock { Payload = DecompileSingle(core.Substring(4)) };

        if (core.StartsWith("ch.")) return new ChoosableContextBlock { Payload = DecompileSingle(core.Substring(3)) };
        if (core.StartsWith("ph.")) return new PhaseContextBlock { Payload = DecompileSingle(core.Substring(3)) };
        if (core.StartsWith("phi."))
        {
            int nextDot = core.IndexOf('.', 4);
            string target = nextDot > 4 ? core.Substring(4, nextDot - 4) : "";
            string payload = nextDot > 4 ? core.Substring(nextDot + 1) : "";
            return new IndexedPhaseContextBlock { Target = target, Payload = DecompileSingle(payload) };
        }
        if (core.StartsWith("phmp.")) return new ModPickContextBlock { Payload = DecompileSingle(core.Substring(5)) };

        string lowerCore = core.ToLower();
        foreach (GlobalCommandBlock.GlobalType gType in Enum.GetValues(typeof(GlobalCommandBlock.GlobalType)))
        {
            string expectedStr = gType switch
            {
                GlobalCommandBlock.GlobalType.LevelUp => "level up",
                GlobalCommandBlock.GlobalType.NoFlee => "no flee",
                GlobalCommandBlock.GlobalType.SkipAll => "skip all",
                GlobalCommandBlock.GlobalType.ClearParty => "clear party",
                GlobalCommandBlock.GlobalType.AddFight => "add fight",
                GlobalCommandBlock.GlobalType.Add10Fights => "add 10 fights",
                GlobalCommandBlock.GlobalType.Add100Fights => "add 100 fights",
                GlobalCommandBlock.GlobalType.MinusFight => "minus fight",
                GlobalCommandBlock.GlobalType.CursemodeLoopdiff => "cursemode loopdiff",
                GlobalCommandBlock.GlobalType.DoubleMonsters => "double monsters",
                GlobalCommandBlock.GlobalType.SkipRewards => "skip rewards",
                _ => gType.ToString().ToLower()
            };
            if (lowerCore == expectedStr) return new GlobalCommandBlock { Type = gType };
        }

        if (core.StartsWith("add.")) return new AddEntityBlock { Entity = core.Substring(4) };
        if (core.StartsWith("fight.")) return new FightBlock { Monsters = SplitOuter(core.Substring(6), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("party.")) return new PartyBlock { Heroes = SplitOuter(core.Substring(6), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("itempool.")) return new PoolBlock { Type = PoolBlock.PoolType.Item, Entities = SplitOuter(core.Substring(9), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("heropool.")) return new PoolBlock { Type = PoolBlock.PoolType.Hero, Entities = SplitOuter(core.Substring(9), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("monsterpool.")) return new PoolBlock { Type = PoolBlock.PoolType.Monster, Entities = SplitOuter(core.Substring(12), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("allitem.")) return new AllItemBlock { Equipped = false, Pools = SplitOuter(core.Substring(8), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("alliteme.")) return new AllItemBlock { Equipped = true, Pools = SplitOuter(core.Substring(9), '+').Select(s => s.Trim()).ToList() };
        if (core.StartsWith("zone.")) return new ZoneBlock { Value = core.Substring(5) };
        if (core.StartsWith("diff.")) return new DifficultyBlock { Value = core.Substring(5) };
        if (core.StartsWith("replace.")) return new ReplaceCommandBlock { TargetNode = DecompileSingle(core.Substring(8)) };

        if (core.StartsWith("!"))
        {
            var sc = new Phase_SimpleChoiceBlock();
            string data = core.Substring(1);
            int semiIdx = data.IndexOf(';');
            if (semiIdx >= 0)
            {
                sc.Title = data.Substring(0, semiIdx);
                data = data.Substring(semiIdx + 1);
            }
            var opts = SplitOuter(data, '@');
            foreach (var opt in opts)
            {
                string cleanOpt = opt.StartsWith("3") ? opt.Substring(1) : opt;
                sc.Choices.Add(DecompileSingle(cleanOpt));
            }
            return sc;
        }

        if (core.StartsWith("s"))
        {
            var seq = new Phase_SequenceBlock();
            var parts = SplitOuter(core.Substring(1), '@');

            if (parts.Count > 0) seq.SequenceMessage = parts[0];

            SequenceOptionBlock currentOpt = null;
            bool hasOpt = false;

            for (int i = 1; i < parts.Count; i++)
            {
                string p = parts[i];
                if (p.StartsWith("1"))
                {
                    if (hasOpt) seq.Options.Add(currentOpt);
                    currentOpt = new SequenceOptionBlock
                    {
                        ButtonText = p.Substring(1),
                        Actions = new List<ITextmodNode>()
                    };
                    hasOpt = true;
                }
                else if (p.StartsWith("2") && hasOpt)
                {
                    currentOpt.Actions.Add(DecompileSingle(p.Substring(1)));
                }
            }
            if (hasOpt) seq.Options.Add(currentOpt);
            return seq;
        }

        if (core.StartsWith("b"))
        {
            var bp = new Phase_Boolean1Block();
            var parts = SplitOuter(core.Substring(1), ';');
            if (parts.Count >= 3)
            {
                bp.VariableName = parts[0];
                int.TryParse(parts[1], out bp.Threshold);

                var branches = SplitOuter(parts[2], '@');
                if (branches.Count > 0)
                    bp.TrueBranch = DecompileSingle(branches[0].StartsWith("2") ? branches[0].Substring(1) : branches[0]);

                if (branches.Count > 1)
                    bp.FalseBranch = DecompileSingle(branches[1].StartsWith("2") ? branches[1].Substring(1) : branches[1]);
            }
            return bp;
        }

        if (core.StartsWith("z"))
        {
            var b2 = new Phase_Boolean2Block();
            var parts = SplitOuter(core.Substring(1), '@');

            if (parts.Count > 0) b2.VariableName = parts[0];

            if (parts.Count > 1 && parts[1].StartsWith("6"))
                int.TryParse(parts[1].Substring(1), out b2.Threshold);

            if (parts.Count > 2 && parts[2].StartsWith("7"))
                b2.TrueBranch = DecompileSingle(parts[2].Substring(1));

            if (parts.Count > 3 && parts[3].StartsWith("7"))
                b2.FalseBranch = DecompileSingle(parts[3].Substring(1));

            return b2;
        }

        if (core.StartsWith("c"))
        {
            var c = new Phase_ChoiceBlock();
            string data = core.Substring(1);
            int semiIdx = data.IndexOf(';');
            if (semiIdx >= 0)
            {
                var configParts = data.Substring(0, semiIdx).Split('#');
                c.ChoiceType = configParts[0];
                if (configParts.Length > 1) int.TryParse(configParts[1], out c.NumChoices);
                data = data.Substring(semiIdx + 1);
            }
            var opts = SplitOuter(data, '@');

            if (opts.Count > 0)
            {
                var lastOptParts = SplitOuter(opts[opts.Count - 1], ';');
                if (lastOptParts.Count > 1)
                {
                    c.Title = lastOptParts[lastOptParts.Count - 1];
                    lastOptParts.RemoveAt(lastOptParts.Count - 1);
                    opts[opts.Count - 1] = string.Join(";", lastOptParts);
                }
            }

            foreach (var opt in opts)
            {
                string cleanOpt = opt.StartsWith("3") ? opt.Substring(1) : opt;
                c.Options.Add(DecompileSingle(cleanOpt));
            }
            return c;
        }

        if (core.StartsWith("t"))
        {
            var tp = new Phase_TradeBlock();
            var parts = SplitOuter(core.Substring(1), '@');
            if (parts.Count > 0) tp.Item1 = DecompileSingle(parts[0]);
            if (parts.Count > 1) tp.Item2 = DecompileSingle(parts[1].StartsWith("3") ? parts[1].Substring(1) : parts[1]);
            return tp;
        }

        if (core.StartsWith("l"))
        {
            var lp = new Phase_LinkedBlock();
            var parts = SplitOuter(core.Substring(1), '@');
            foreach (var p in parts)
            {
                string clean = p.StartsWith("1") ? p.Substring(1) : p;
                lp.Phases.Add(DecompileSingle(clean));
            }
            return lp;
        }

        if (core.StartsWith("4"))
        {
            var msg = new Phase_MessageBlock();
            var parts = SplitOuter(core.Substring(1), ';');
            if (parts.Count > 0) msg.Message = parts[0];
            if (parts.Count > 1) msg.ButtonText = parts[1];
            return msg;
        }

        if (core.StartsWith("2") && core.Contains("ps:["))
        {
            var lep = new Phase_LevelEndBlock();
            int start = core.IndexOf("ps:[") + 4;
            int end = core.LastIndexOf("]");
            if (start > 3 && end > start)
            {
                string inner = core.Substring(start, end - start);
                var innerPhases = SplitOuter(inner, ',');
                foreach (var p in innerPhases) lep.EndScreenData.Add(DecompileSingle(p));
            }
            return lep;
        }

        if (core.StartsWith("9"))
        {
            var chp = new Phase_ChallengeBlock();
            Match mMonsters = Regex.Match(core, @"\""extraMonsters\""\s*:\s*\[(.*?)\]");
            if (mMonsters.Success)
            {
                foreach (var m in mMonsters.Groups[1].Value.Split(','))
                {
                    string clean = m.Trim().Trim('"', '\\');
                    if (!string.IsNullOrEmpty(clean)) chp.ExtraMonsters.Add(clean);
                }
            }
            Match mData = Regex.Match(core, @"\""data\""\s*:\s*\""(.*?)\""");
            if (mData.Success) chp.RewardPayload = DecompileSingle(mData.Groups[1].Value);
            return chp;
        }

        if (core.StartsWith("5") && core.Length >= 2) return new Phase_HeroChangeBlock { HeroPositionIndex = core[1] - '0', IsRandomClass = (core.Length > 2 && core[2] == '0') };
        if (core.StartsWith("7")) return new Phase_ItemCombineBlock { Rule = core.Substring(1) == "ZeroToThreeToSingle" ? Phase_ItemCombineBlock.CombineRule.ZeroToThreeToSingle : Phase_ItemCombineBlock.CombineRule.SecondHighestToTierThrees };
        if (core.StartsWith("8") && core.Length >= 3) return new Phase_PositionSwapBlock { IndexA = core[1] - '0', IndexB = core[2] - '0' };
        if (core.StartsWith("g")) return new Phase_GenerateScreenBlock { Type = (core.Length > 1 && core[1] == 'h') ? Phase_GenerateScreenBlock.ScreenType.LevelUp : Phase_GenerateScreenBlock.ScreenType.Item };
        if (core.Length == 1 && "013d6e".Contains(core[0])) return new Phase_StaticBlock { Phase = (Phase_StaticBlock.StaticPhase)core[0] };
        if (core.StartsWith("r") && !core.Contains("~")) return new Phase_RandomRevealBlock { RewardData = DecompileSingle(core.Substring(1)) };

        if ((core.StartsWith("r") || core.StartsWith("q")) && core.Contains("~"))
        {
            var rb = new Reward_RandomBlock();
            var parts = SplitOuter(core.Substring(1), '~');

            if (core.StartsWith("r") && parts.Count >= 3)
            {
                int.TryParse(parts[0], out rb.MinTier); rb.MaxTier = rb.MinTier;
                int.TryParse(parts[1], out rb.Amount); rb.RewardTypeFlag = parts[2];
            }
            else if (core.StartsWith("q") && parts.Count >= 4)
            {
                int.TryParse(parts[0], out rb.MinTier); int.TryParse(parts[1], out rb.MaxTier);
                int.TryParse(parts[2], out rb.Amount); rb.RewardTypeFlag = parts[3];
            }
            return rb;
        }

        if (core.StartsWith("o") && core.Contains("@4"))
        {
            var ob = new Reward_ChoiceBlock();
            foreach (var p in SplitOuter(core.Substring(1), '@'))
            {
                string clean = p.StartsWith("4") ? p.Substring(1) : p;
                ob.Options.Add(DecompileSingle(clean));
            }
            return ob;
        }

        if (core.StartsWith("v"))
        {
            var vb = new Reward_ValueModifyBlock();
            string data = core.Substring(1);
            int vIdx = data.LastIndexOf('V');
            if (vIdx >= 1)
            {
                vb.VariableName = data.Substring(0, vIdx);
                int.TryParse(data.Substring(vIdx + 1), out vb.ValueToAdd);
            }
            else vb.VariableName = data;

            return vb;
        }

        if (core.StartsWith("p"))
        {
            var rep = new Reward_ReplaceBlock();
            string data = core.Substring(1);
            if (data.StartsWith(" m") || data.StartsWith("m"))
            {
                rep.IsModifierReplacement = true;
                var parts = SplitOuter(data.StartsWith(" m") ? data.Substring(2) : data.Substring(1), '~');
                if (parts.Count > 0) rep.TargetToReplace = parts[0];
                if (parts.Count > 1) rep.NewValue = parts[1];
            }
            else rep.TargetToReplace = data;

            return rep;
        }

        if (core.StartsWith("e") && core.Contains("RandoKeyword")) return new Reward_EnumItemBlock { EnumName = core.Substring(1) };
        if (core == "s") return new Reward_SkipBlock();

        if (Regex.IsMatch(core, @"^[imgl]"))
            return new Reward_StandardBlock { Type = (Reward_StandardBlock.RewardType)core[0], TargetEntity = core.Substring(1) };

        return new RawTextNode(core);
    }

    private static string ExtractTagValue(ref string properties, string tag)
    {
        string search = "." + tag + ".";
        int start = properties.IndexOf(search);
        if (start == -1) return null;

        int depth = 0;
        int end = start + search.Length;
        while (end < properties.Length)
        {
            char c = properties[end];
            if (c == '(' || c == '[' || c == '{') depth++;
            else if (c == ')' || c == ']' || c == '}') depth--;
            else if (depth == 0 && c == '.') break;
            end++;
        }

        string value = properties.Substring(start + search.Length, end - (start + search.Length));
        properties = properties.Remove(start, end - start);
        return value;
    }

    private static string Unwrap(string text)
    {
        text = text.Trim();
        while (text.StartsWith("(") && text.EndsWith(")"))
        {
            int depth = 0;
            bool matching = true;
            for (int i = 0; i < text.Length - 1; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') depth--;
                if (depth == 0) { matching = false; break; }
            }
            if (matching && depth == 1) text = text.Substring(1, text.Length - 2).Trim();
            else break;
        }
        return text;
    }

    private static List<string> SplitOuter(string str, char delimiter)
    {
        List<string> result = new List<string>();
        int paren = 0, brk = 0, brc = 0, start = 0;
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (c == '(') paren++;
            else if (c == ')') paren--;
            else if (c == '[') brk++;
            else if (c == ']') brk--;
            else if (c == '{') brc++;
            else if (c == '}') brc--;
            else if (c == delimiter && paren == 0 && brk == 0 && brc == 0)
            {
                result.Add(str.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (start <= str.Length) result.Add(str.Substring(start));
        return result;
    }
}
#endregion

#region 7. SYSTEM ATTRIBUTES & INTERFACES
[AttributeUsage(AttributeTargets.Class)]
public class BlockMetaAttribute : Attribute
{
    public string MenuName { get; }
    public Type UIType { get; }

    public BlockMetaAttribute(string menuName, Type uiType)
    {
        MenuName = menuName;
        UIType = uiType;
    }
}

public interface IBlockContainer : ITextmodNode
{
    List<ITextmodNode> ChildNodes { get; set; }
}

public interface IBlockWrapper : ITextmodNode
{
    ITextmodNode PayloadNode { get; set; }
}

public static class BlockRegistry
{
    public static readonly List<string> MenuOptions = new List<string>();

    private static readonly Dictionary<string, Type> _nameToNode = new Dictionary<string, Type>();
    private static readonly Dictionary<Type, string> _nodeToName = new Dictionary<Type, string>();
    private static readonly Dictionary<Type, Type> _nodeToUI = new Dictionary<Type, Type>();

    static BlockRegistry()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITextmodNode).IsAssignableFrom(t));

        foreach (var t in types)
        {
            var attr = (BlockMetaAttribute)Attribute.GetCustomAttribute(t, typeof(BlockMetaAttribute));
            if (attr != null)
            {
                if (!string.IsNullOrEmpty(attr.MenuName))
                {
                    _nameToNode[attr.MenuName] = t;
                    _nodeToName[t] = attr.MenuName;
                    MenuOptions.Add(attr.MenuName);
                }
                _nodeToUI[t] = attr.UIType;
            }
        }
    }

    public static ITextmodNode CreateNode(string name) =>
        _nameToNode.TryGetValue(name, out var type) ? (ITextmodNode)Activator.CreateInstance(type) : null;

    public static string GetNodeName(ITextmodNode node) =>
        node != null && _nodeToName.TryGetValue(node.GetType(), out var name) ? name : "GENERIC BLOCK";

    public static UIBlockNode CreateUI(ITextmodNode node) =>
        node != null && _nodeToUI.TryGetValue(node.GetType(), out var uiType)
            ? (UIBlockNode)Activator.CreateInstance(uiType, node)
            : new DefaultBlockUI(node);
}
#endregion

#region Node Definitions

[BlockMeta("Chain: Comma (Top Level AND)", typeof(DefaultBlockUI))]
public class CommaChainBlock : IBlockContainer
{
    public List<ITextmodNode> Nodes = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Nodes; set => Nodes = value; }

    public string Compile() => string.Join(",", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
}

[BlockMeta("Chain: Ampersand (Nested AND)", typeof(DefaultBlockUI))]
public class AndChainBlock : IBlockContainer
{
    public List<ITextmodNode> Nodes = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Nodes; set => Nodes = value; }

    public string Compile() => string.Join("&", Nodes.Select(n => n?.Compile()).Where(s => !string.IsNullOrEmpty(s)));
}

/// <summary>
/// Wraps a modifier in a conditional timeline constraint.
/// </summary>
/// <remarks>
/// <para><b>TIMELINE CONSTRAINTS:</b></para>
/// <list type="bullet">
/// <item><description><c>lvl.modifier</c>: Constrains the modifier to occur only during a specific floor.</description></item>
/// <item><description><c>lvl-lvl.modifier</c>: Constrains the modifier to apply across a designated range of floors (e.g. <c>8-16.Monster Blank</c> applies "Monster Blank" during floors 8 to 16).</description></item>
/// <item><description><c>t#.modifier</c>: Activates the mutator only during a designated combat turn index of a floor.</description></item>
/// <item><description><c>e###.modifier</c>: Fires the mutator payload every X completed fights.</description></item>
/// <item><description><c>et#.modifier</c>: Fires the mutator payload on every X turns of a fight.</description></item>
/// </list>
/// </remarks>
[BlockMeta("Wrapper: Floor Condition", typeof(FloorConditionBlockUI))]
public class FloorConditionBlock : IBlockWrapper
{
    public enum ConditionType { Single, Range, EveryX }
    public ConditionType Type = ConditionType.Single;
    public int StartFloor = 1;
    public int EndFloor = 5;
    public int Interval = 2;
    public int Offset = 0;

    public ITextmodNode Payload;
    public ITextmodNode PayloadNode { get => Payload; set => Payload = value; }

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

[BlockMeta("Wrapper: Multiplier (xN)", typeof(DefaultBlockUI))]
public class MultiplierBlock : IBlockWrapper
{
    public int Multiplier = 2;

    public ITextmodNode Payload;
    public ITextmodNode PayloadNode { get => Payload; set => Payload = value; }

    public string Compile() => Multiplier <= 1 ? Payload?.Compile() : $"x{Multiplier}.{Payload?.Compile()}";
}

[BlockMeta("Command: Level Constraint (lvl.)", typeof(DefaultBlockUI))]
public class LevelConstraintBlock : TextmodBlock, IBlockWrapper
{
    public ITextmodNode Payload;
    public ITextmodNode PayloadNode { get => Payload; set => Payload = value; }

    public override string CompileCore() => $"lvl.{SafeCompile(Payload)}";
}

/// <summary>
/// Wraps a reward tag in a context. 
/// "ch." (Choosable): Directly grants a reward silently. More efficient, but less flexible. 
/// "ph." (Implied Phase): Often used with ! (SimpleChoicePhase) to pop up a screen.
/// "phi." (Indexed Phase): Generates specific base-game phases (0=Levelup, 1=Loot, 9=Cursed Chest, etc).
/// "phmp." (Mod Pick Phase): Generates a modifier selection screen.
/// </summary>
public abstract class ContextWrapperBlock : ITextmodNode, IBlockWrapper
{
    public string Prefix;
    public string Target = "";

    public ITextmodNode Payload;
    public ITextmodNode PayloadNode { get => Payload; set => Payload = value; }

    protected ContextWrapperBlock(string prefix) { Prefix = prefix; }

    public string Compile()
    {
        if (Payload != null) return $"{Prefix}.{Target}{Payload.Compile()}";
        return $"{Prefix}.{Target}";
    }
}

[BlockMeta("Context: Implied Phase (ph.)", typeof(ContextWrapperBlockUI))]
public class PhaseContextBlock : ContextWrapperBlock
{
    public PhaseContextBlock() : base("ph") { }
}

[BlockMeta("Context: Indexed Game Phase (phi.)", typeof(ContextWrapperBlockUI))]
public class IndexedPhaseContextBlock : ContextWrapperBlock
{
    public IndexedPhaseContextBlock() : base("phi") { }
}

[BlockMeta("Context: Modifier Pick Phase (phmp.)", typeof(ContextWrapperBlockUI))]
public class ModPickContextBlock : ContextWrapperBlock
{
    public ModPickContextBlock() : base("phmp") { }
}

[BlockMeta("Context: Choosable Reward (ch.)", typeof(ContextWrapperBlockUI))]
public class ChoosableContextBlock : ContextWrapperBlock
{
    public ChoosableContextBlock() : base("ch") { }
}

[BlockMeta("Command: Add Entity (add.)", typeof(DefaultBlockUI))]
public class AddEntityBlock : TextmodBlock
{
    public string Entity = "";
    public override string CompileCore() => $"add.{Entity}";
}

/// <summary>
/// Overrides a floor's standard procedural monster configuration with a hardcoded list of monsters.
/// </summary>
/// <remarks>
/// Combine this with floor constraints (<c>lvl.fight.Monster+Monster...</c>) to design bespoke boss fights 
/// or hand-crafted level encounters.
/// </remarks>
[BlockMeta("Command: Fight Encounter (fight.)", typeof(FightBlockUI))]
public class FightBlock : TextmodBlock
{
    public List<string> Monsters = new List<string>();
    public override string CompileCore() => $"fight.{string.Join("+", Monsters)}";
}

[BlockMeta("Command: Set Party (party.)", typeof(PartyBlockUI))]
public class PartyBlock : TextmodBlock
{
    public List<string> Heroes = new List<string>();
    public override string CompileCore() => $"party.{string.Join("+", Heroes)}";
}

[BlockMeta("Command: Replace Entity (replace.)", typeof(DefaultBlockUI))]
public class ReplaceCommandBlock : TextmodBlock, IBlockWrapper
{
    public ITextmodNode TargetNode;
    public ITextmodNode PayloadNode { get => TargetNode; set => TargetNode = value; }

    public override string CompileCore() => $"replace.{SafeCompile(TargetNode)}";
}

/// <summary>
/// Defines a gameplay pool override for Heroes, Items, or Monsters.
/// </summary>
/// <remarks>
/// Pools dictate what entities are eligible to spawn as reward drops or tier-upgrade options during a run.
/// <para><b>POOL SYNTAX CONSTRAINTS:</b></para>
/// <list type="bullet">
/// <item><description>Defined as comma-separated lists prefixed by pool type (e.g., <c>heropool.Hero+Hero...</c>).</description></item>
/// <item><description>There is a character limit of approximately <b>5000 characters</b> per defined pool string.</description></item>
/// <item><description>Multiple pools of the same type can be declared together to bypass limit structures.</description></item>
/// </list>
/// </remarks>
[BlockMeta("Command: Add to Pool (item/hero/monster)", typeof(PoolBlockUI))]
public class PoolBlock : TextmodBlock
{
    public enum PoolType { Item, Hero, Monster }
    public PoolType Type = PoolType.Hero;
    public List<string> Entities = new List<string>();

    public override string CompileCore()
    {
        string core = $"{Type.ToString().ToLower()}pool.{string.Join("+", Entities)}";
        if (Part >= 0) core += $".part.{Part}";
        return core;
    }
}

[BlockMeta("Command: Grant All Items (allitem/alliteme)", typeof(DefaultBlockUI))]
public class AllItemBlock : TextmodBlock
{
    public bool Equipped = false;
    public List<string> Pools = new List<string>();
    public override string CompileCore() => (Equipped ? "alliteme." : "allitem.") + string.Join("+", Pools);
}

/// <summary>
/// Custom Mode Difficulty and Value Parameters.
/// </summary>
/// <remarks>
/// Dictates starting variables and modifier rules. Valid difficulties accepted by the engine are:
/// <c>Heaven, Easy, Normal, Hard, Unfair, Brutal, Hell</c>.
/// </remarks>
public abstract class SimpleValueBlock : TextmodBlock
{
    public string Prefix { get; }
    public string Value = "";

    protected SimpleValueBlock(string prefix) { Prefix = prefix; }
    public override string CompileCore() => $"{Prefix}.{Value}";
}

[BlockMeta("Command: Set Zone (zone.)", typeof(DefaultBlockUI))]
public class ZoneBlock : SimpleValueBlock
{
    public ZoneBlock() : base("zone") { }
}

[BlockMeta("Command: Set Difficulty (diff.)", typeof(DefaultBlockUI))]
public class DifficultyBlock : SimpleValueBlock
{
    public DifficultyBlock() : base("diff") { }
}

/// <summary>
/// Hidden global modifiers added in v3.1.
/// Useful for custom modes (e.g. Skip All removes all events, Clear Party removes the team).
/// Cursemode Loopdiff causes level 21 and level 1 to have the same enemy balance.
/// </summary>
[BlockMeta("Global Command", typeof(GlobalCommandBlockUI))]
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

/// <summary>
/// Simple Choice Phase (!): Pops up a screen allowing the player to pick a reward.
/// Delimiter: '@3'. 
/// Unlike Choosables, SCPhase can be constrained by 'lvl.' and creates a visual UI.
/// </summary>
/// <example>ph.!Example Title;iCorset@3iBallet Shoes@3s</example>
[BlockMeta("Phase: Simple Choice (!)", typeof(Phase_SimpleChoiceBlockUI))]
public class Phase_SimpleChoiceBlock : TextmodBlock, IBlockContainer
{
    public string Title = "";
    public List<ITextmodNode> Choices = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Choices; set => Choices = value; }

    public override string CompileCore()
    {
        string opts = string.Join("@3", Choices.Select(c => SafeCompile(c)));
        return string.IsNullOrEmpty(Title) ? $"!{opts}" : $"!{Title};{opts}";
    }
}

/// <summary>
/// Level End Phase (2): Appends phases to the between-levels screen. 
/// As of v3.1, custom modes usually restrict this to 1 phase, but Paste mode allows multiple.
/// </summary>
/// <example>ph.2{ps:[tr-1~1~m@3r1~4~i,!lPriestess@3lSparky]}</example>
[BlockMeta("Phase: Level End Screen (2)", typeof(Phase_LevelEndBlockUI))]
public class Phase_LevelEndBlock : TextmodBlock, IBlockContainer
{
    public List<ITextmodNode> EndScreenData = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => EndScreenData; set => EndScreenData = value; }

    public override string CompileCore() => $"2ps:[{string.Join(",", EndScreenData.Select(SafeCompile))}]";
}

/// <summary>
/// Message Phase (4): Sends a message with custom contents. 
/// Colors can be changed using bracket tags (e.g. [orange]), images using entity names (e.g. [Thief]), 
/// and tracked Values using [val(variable)].
/// </summary>
/// <example>ph.4You currently have [valgold] gold.;Ok</example>
[BlockMeta("Phase: Message Popup (4)", typeof(Phase_MessageBlockUI))]
public class Phase_MessageBlock : TextmodBlock
{
    public string Message = "";
    public string ButtonText = "Ok";
    public override string CompileCore() => (string.IsNullOrEmpty(ButtonText) || ButtonText == "Ok") ? $"4{Message}" : $"4{Message};{ButtonText}";
}

/// <summary>
/// Hero Change Phase (5): Rerolls a hero based on a top-down zero-indexed position (0-4).
/// Type 0 = Random Class, Type 1 = Generated Hero.
/// </summary>
/// <example>ph.501 (Replace the top hero with a generated one)</example>
[BlockMeta("Phase: Hero Change Offer (5)", typeof(Phase_HeroChangeBlockUI))]
public class Phase_HeroChangeBlock : TextmodBlock
{
    public int HeroPositionIndex = 0;
    public bool IsRandomClass = false;
    public override string CompileCore() => $"5{HeroPositionIndex}{(IsRandomClass ? "0" : "1")}";
}

/// <summary>
/// Item Combine Phase (7): 
/// SecondHighestToTierThrees: Smashes the 2nd highest tier item into multiple tier 3s.
/// ZeroToThreeToSingle: Combines all tier 0-3 items into a single higher tier item.
/// </summary>
[BlockMeta("Phase: Item Combine / Smithing (7)", typeof(Phase_ItemCombineBlockUI))]
public class Phase_ItemCombineBlock : TextmodBlock
{
    public enum CombineRule { SecondHighestToTierThrees, ZeroToThreeToSingle }
    public CombineRule Rule = CombineRule.SecondHighestToTierThrees;
    public override string CompileCore() => $"7{Rule}";
}

/// <summary>
/// Position Swap Phase (8): Swaps two heroes based on top-down zero-indexed positions (0-4).
/// </summary>
/// <example>ph.801 (Swaps top hero and the one below it)</example>
[BlockMeta("Phase: Position Swap (8)", typeof(Phase_PositionSwapBlockUI))]
public class Phase_PositionSwapBlock : TextmodBlock
{
    public int IndexA = 0;
    public int IndexB = 1;
    public override string CompileCore() => $"8{IndexA}{IndexB}";
}

/// <summary>
/// Challenge Phase (9): Offers extra monsters in exchange for rewards.
/// Requires strict internal JSON formatting.
/// </summary>
/// <example>ph.9{"reward":{"data":"iMonocle"},"type":{"extraMonsters":["Militia"]}}</example>
[BlockMeta("Phase: Challenge Phase (9)", typeof(Phase_ChallengeBlockUI))]
public class Phase_ChallengeBlock : TextmodBlock, IBlockWrapper
{
    public List<string> ExtraMonsters = new List<string>();
    public ITextmodNode RewardPayload;
    public ITextmodNode PayloadNode { get => RewardPayload; set => RewardPayload = value; }

    public override string CompileCore()
    {
        string monsters = string.Join(",", ExtraMonsters.Select(m => $"\"{m}\""));
        return $"9{{\"extraMonsters\":[{monsters}],\"data\":\"{SafeCompile(RewardPayload)}\"}}";
    }
}

/// <summary>
/// Boolean Phase (b): Checks a previously set Value (via 'v' tag) and chooses between two branches.
/// Delimiters: ';' and '@2'. Cannot be nested directly in the middle due to collisions.
/// </summary>
/// <example>ph.bSeed;3;!m1.fight.Boar@2!m1.fight.Goblin</example>
[BlockMeta("Phase: Boolean Check 1 (b)", typeof(Phase_Boolean1BlockUI))]
public class Phase_Boolean1Block : TextmodBlock
{
    public string VariableName = "";
    public int Threshold = 1;
    public ITextmodNode TrueBranch;
    public ITextmodNode FalseBranch;
    public override string CompileCore() => $"b{VariableName};{Threshold};{SafeCompile(TrueBranch)}@2{SafeCompile(FalseBranch)}";
}

/// <summary>
/// Choice Phase (c): Similar to SimpleChoicePhase but accepts 4 unique rule types.
/// PointBuy: uses modifier/item/hero tiers to add to total. Number: Exact amount.
/// UpToNumber: Flexible amount. Optional: Take all or nothing.
/// Delimiters: ';' and '@3'.
/// </summary>
/// <example>ph.cUpToNumber#2;iPowdered Mana@3iCan</example>
[BlockMeta("Phase: Choice Screen (c)", typeof(Phase_ChoiceBlockUI))]
public class Phase_ChoiceBlock : TextmodBlock, IBlockContainer
{
    public string ChoiceType = "i";
    public int NumChoices = 1;
    public string Title = "";
    public List<ITextmodNode> Options = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Options; set => Options = value; }

    public override string CompileCore()
    {
        string opts = string.Join("@3", Options.Select(SafeCompile));
        string core = $"c{ChoiceType}#{NumChoices};{opts}";
        return string.IsNullOrEmpty(Title) ? core : $"{core};{Title}";
    }
}

/// <summary>
/// Linked Phase (l): Forces multiple phases to take place one after another without logic collisions.
/// Delimiter: '@1'.
/// </summary>
/// <example>ph.l4Message1@1l4Message2@14Message3</example>
[BlockMeta("Phase: Linked Events (l)", typeof(Phase_LinkedBlockUI))]
public class Phase_LinkedBlock : TextmodBlock, IBlockContainer
{
    public List<ITextmodNode> Phases = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Phases; set => Phases = value; }
    public override string CompileCore() => $"l{string.Join("@1", Phases.Select(SafeCompile))}";
}

/// <summary>
/// Random Reveal Phase (r): Shows a popup stating "Gained: X" but doesn't actually grant the reward.
/// Great for flavor text or notifying players of hidden Choosable grants.
/// </summary>
[BlockMeta("Phase: Random Reveal Popup (r)", typeof(Phase_RandomRevealBlockUI))]
public class Phase_RandomRevealBlock : TextmodBlock, IBlockWrapper
{
    public ITextmodNode RewardData;
    public ITextmodNode PayloadNode { get => RewardData; set => RewardData = value; }
    public override string CompileCore() => $"r{SafeCompile(RewardData)}";
}

/// <summary>
/// Sequence Phase (s): Creates complex dialogue/choice trees.
/// Delimiters: '@1' separates options, '@2' separates the phases that follow the option.
/// </summary>
/// <example>ph.sMessage@1Btn1@2Act1@1Btn2@2Act2</example>
[BlockMeta("Phase: Story Sequence (s)", typeof(Phase_SequenceBlockUI))]
public class Phase_SequenceBlock : TextmodBlock, IBlockContainer
{
    public string SequenceMessage = "";
    public List<ITextmodNode> Options = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Options; set => Options = value; }

    public override string CompileCore() => $"s{SequenceMessage}{string.Join("", Options.Select(o => o?.Compile() ?? ""))}";
}

[BlockMeta("Sequence Option Fork", typeof(SequenceOptionBlockUI))]
public class SequenceOptionBlock : TextmodBlock, IBlockContainer
{
    public string ButtonText = "Option";
    public List<ITextmodNode> Actions = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Actions; set => Actions = value; }

    public override string CompileCore()
    {
        string acts = string.Join("", Actions.Select(a => $"@2{SafeCompile(a)}"));
        return $"@1{ButtonText}{acts}";
    }
}

/// <summary>
/// Trade Phase (t): Functions identically to a cursed chest.
/// Offers a trade where both rewards are accepted or declined together. Delimiter: '@3'.
/// </summary>
/// <example>ph.tr1~4~i@3r-1~1~m (4 random T1 items for 1 random T-1 curse)</example>
[BlockMeta("Phase: Cursed Chest Trade (t)", typeof(Phase_TradeBlockUI))]
public class Phase_TradeBlock : TextmodBlock
{
    public ITextmodNode Item1;
    public ITextmodNode Item2;
    public override string CompileCore() => $"t{SafeCompile(Item1)}@3{SafeCompile(Item2)}";
}

/// <summary>
/// Phase Generator Transform Phase (g): Quickly generates base-game choice screens.
/// 'gh' = Hero Levelup, 'gi' = Item screen. Can be nested inside Sequence or Linked phases.
/// </summary>
[BlockMeta("Phase: Generate Screen (g)", typeof(Phase_GenerateScreenBlockUI))]
public class Phase_GenerateScreenBlock : TextmodBlock
{
    public enum ScreenType { LevelUp = 'h', Item = 'i' }
    public ScreenType Type = ScreenType.Item;
    public override string CompileCore() => $"g{(char)Type}";
}

/// <summary>
/// Boolean Phase 2 (z): Identical logic to Boolean 1, but uses different delimiters 
/// allowing it to be chained alongside SeqPhase or Boolean 1 without collision.
/// Delimiters: '@6' for threshold, '@7' for branches.
/// </summary>
[BlockMeta("Phase: Boolean Check 2 (z)", typeof(Phase_Boolean2BlockUI))]
public class Phase_Boolean2Block : TextmodBlock
{
    public string VariableName = "";
    public int Threshold = 1;
    public ITextmodNode TrueBranch;
    public ITextmodNode FalseBranch;
    public override string CompileCore() => $"z{VariableName}@6{Threshold}@7{SafeCompile(TrueBranch)}@7{SafeCompile(FalseBranch)}";
}

/// <summary>
/// Static / Event Phases (0,1,3,d,6,e).
/// Includes combat phases (Rolling, Targeting, Damage) which can produce strange results if modified.
/// Notably includes '6' (ResetPhase) which resets Cursed mode (de-levels heroes, removes items),
/// and 'e' (RunEndPhase) which immediately ends the run.
/// </summary>
[BlockMeta("Phase: Static (0,1,3,d,6,e)", typeof(Phase_StaticBlockUI))]
public class Phase_StaticBlock : TextmodBlock
{
    public enum StaticPhase { PlayerRolling = '0', Targeting = '1', EnemyRolling = '3', Damage = 'd', Reset = '6', RunEnd = 'e' }
    public StaticPhase Phase = StaticPhase.PlayerRolling;
    public override string CompileCore() => $"{(char)Phase}";
}

/// <summary>
/// Standard Reward Tags: Grants a specific entity directly.
/// 'i' = Item, 'm' = Modifier, 'g' = Add Hero, 'l' = Levelup Hero.
/// Note: 'l' targets an existing hero (defaults to topmost if no match), 'g' generates a new one.
/// </summary>
/// <example>ch.mBone Math (Adds Bone Math mod), ph.!gRuffian (Adds Ruffian hero)</example>
[BlockMeta("Reward: Standard (i/m/g/l)", typeof(Reward_StandardBlockUI))]
public class Reward_StandardBlock : TextmodBlock
{
    public enum RewardType { Item = 'i', Modifier = 'm', Hero = 'g', LevelUp = 'l' }
    public RewardType Type = RewardType.Item;
    public string TargetEntity = "";
    public override string CompileCore() => $"{(char)Type}{TargetEntity}";
}

/// <summary>
/// Random Reward Tags (r / q).
/// 'r' selects a random reward from a specific tier. Syntax: r[Tier]~[Amount]~[Tag].
/// 'q' selects a random reward from a tier range. Syntax: q[Min]~[Max]~[Amount]~[Tag].
/// Note: Heroes and Levelups usually force Amount to 1.
/// </summary>
/// <example>ch.r1~2~m (2 random Tier 1 modifiers)</example>
[BlockMeta("Reward: Random Reward (r/q)", typeof(Reward_RandomBlockUI))]
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

/// <summary>
/// Or Tag (o): Grants a random reward chosen from a custom list.
/// Extremely useful for custom modes requiring controlled randomness. Delimiter: '@4'.
/// </summary>
/// <example>ch.omadd.Bones@4mWurst</example>
[BlockMeta("Reward: Random Choice (o)", typeof(Reward_ChoiceBlock))]
public class Reward_ChoiceBlock : TextmodBlock, IBlockContainer
{
    public List<ITextmodNode> Options = new List<ITextmodNode>();
    public List<ITextmodNode> ChildNodes { get => Options; set => Options = value; }
    public override string CompileCore() => $"o{string.Join("@4", Options.Select(SafeCompile))}";
}

/// <summary>
/// Enum Tag (e): Grants "random keyword on X sides" items.
/// RandoKeywordT1Item = Rightmost.
/// RandoKeywordT5Item = Left, Top/Bot, or Right 3.
/// RandoKeywordT7Item = All sides.
/// </summary>
[BlockMeta("Reward: Enum Item (e)", typeof(Reward_EnumItemBlockUI))]
public class Reward_EnumItemBlock : TextmodBlock
{
    public string EnumName = "RandoKeywordT1Item";
    public override string CompileCore() => $"e{EnumName}";
}

/// <summary>
/// Value Tag (v): Adds or subtracts from a custom hidden variable.
/// Can be viewed later using [val(VariableName)] in a Message phase, 
/// or evaluated using Boolean phases (ph.b / ph.z).
/// </summary>
/// <example>ch.vGoldV50 (Adds 50 Gold)</example>
[BlockMeta("Reward: Modify Variable (v)", typeof(Reward_ValueModifyBlockUI))]
public class Reward_ValueModifyBlock : TextmodBlock
{
    public string VariableName = "";
    public int ValueToAdd = 1;
    public override string CompileCore() => $"v{VariableName}V{ValueToAdd}";
}

/// <summary>
/// Replace Tag (p/pm): Only works for replacing Modifiers!
/// Removes the targeted modifier (if the player has it) and grants a new reward.
/// Used commonly in Curse modes to "upgrade" curses.
/// </summary>
/// <example>ph.!pmWurst~gPaladin (Removes Wurst curse, grants Paladin hero)</example>
[BlockMeta("Reward: Replace Reward (p)", typeof(Reward_ReplaceBlockUI))]
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

/// <summary>
/// Skip Tag (s): Adds the option to do nothing and dismiss a prompt.
/// Highly useful for populating "Decline" or "Leave" options in Choice phases (@3).
/// </summary>
[BlockMeta("Reward: Skip (s)", typeof(Reward_SkipBlockUI))]
public class Reward_SkipBlock : TextmodBlock
{
    public override string CompileCore() => "s";
}

#endregion

#region UI Definitions

// =====================================================================
// 1. DEFAULT & WRAPPER UIs
// =====================================================================
public class DefaultBlockUI : UIBlockNode
{
    private ITextmodNode _rawNode;
    public DefaultBlockUI(ITextmodNode node) : base(node) { _rawNode = node; }

    public override string GetBlockTitle()
    {
        if (_rawNode == null) return "GENERIC BLOCK";
        string typeName = _rawNode.GetType().Name;
        if (typeName.EndsWith("Block")) typeName = typeName.Substring(0, typeName.Length - 5);
        return typeName.ToUpper();
    }

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblWIP", "<i>Specific properties managed via nested workspace drops.</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class FloorConditionBlockUI : UIBlockNode
{
    private FloorConditionBlock _node;
    private readonly string[] _typeOptions = { "Single Floor", "Range", "Every X Floors" };

    public FloorConditionBlockUI(FloorConditionBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "FLOOR CONDITION WRAPPER";

    protected override List<GridRowSpec> GetSpecificRowSpecs()
    {
        return new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblType", "Condition Type:", 0.4f),
                GridCellSpec.CreateDropdown("DropType", "", 0.6f, _typeOptions, null)
            ),
            new GridRowSpec(
                GridCellSpec.CreateLabel("LblStart", "Start/Floor:", 0.25f),
                GridCellSpec.CreateInput("InpStart", "1", 0.25f, null),
                GridCellSpec.CreateLabel("LblEnd", "End/Interval:", 0.25f),
                GridCellSpec.CreateInput("InpEnd", "5", 0.25f, null)
            ),
            new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop a modifier or phase inside this wrapper.</i>", 1f))
        };
    }

    protected override void BindSpecificUI(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop))
            drop.onValueChanged.AddListener(val => _node.Type = (FloorConditionBlock.ConditionType)val);

        if (refs.Inputs.TryGetValue("InpStart", out var iStart))
            iStart.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.StartFloor = i; });

        if (refs.Inputs.TryGetValue("InpEnd", out var iEnd))
            iEnd.onValueChanged.AddListener(v => {
                if (int.TryParse(v, out int i)) { _node.EndFloor = i; _node.Interval = i; }
            });
    }

    protected override void RestoreSpecificState(RectTransform container, GridReferences refs)
    {
        if (refs.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = (int)_node.Type;
        if (refs.Inputs.TryGetValue("InpStart", out var iStart)) iStart.text = _node.StartFloor.ToString();
        if (refs.Inputs.TryGetValue("InpEnd", out var iEnd)) iEnd.text = _node.Type == FloorConditionBlock.ConditionType.EveryX ? _node.Interval.ToString() : _node.EndFloor.ToString();
    }
}

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
            rows.Add(new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop a Reward Tag block inside this wrapper.</i>", 1f)));
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

// =====================================================================
// 2. COMMANDS & OVERRIDES UIs
// =====================================================================

public class GlobalCommandBlockUI : UIBlockNode
{
    private GlobalCommandBlock _node;
    private readonly string[] _globalOptions = Enum.GetNames(typeof(GlobalCommandBlock.GlobalType));

    public GlobalCommandBlockUI(GlobalCommandBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "GLOBAL COMMAND";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblCmd", "Command Type:", 0.35f),
            GridCellSpec.CreateDropdown("DropGlobalCommand", "", 0.65f, _globalOptions, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropGlobalCommand", out var drop)) drop.onValueChanged.AddListener(val => _node.Type = (GlobalCommandBlock.GlobalType)val);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropGlobalCommand", out var drop)) drop.value = (int)_node.Type;
    }
}

public class FightBlockUI : UIBlockNode
{
    private FightBlock _node;
    public FightBlockUI(FightBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "FIGHT ENCOUNTER OVERRIDE";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblMonsters", "Monsters (+ split):", 0.35f),
            GridCellSpec.CreateInput("InpMonsters", "e.g. Goblin+Orc", 0.65f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMonsters", out var inp)) inp.onValueChanged.AddListener(v => _node.Monsters = v.Split('+').Select(s => s.Trim()).ToList());
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMonsters", out var inp)) inp.text = string.Join("+", _node.Monsters);
    }
}

public class PartyBlockUI : UIBlockNode
{
    private PartyBlock _node;
    public PartyBlockUI(PartyBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "SET PARTY OVERRIDE";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblHeroes", "Heroes (+ split):", 0.35f),
            GridCellSpec.CreateInput("InpHeroes", "e.g. Thief+Fighter", 0.65f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpHeroes", out var inp)) inp.onValueChanged.AddListener(v => _node.Heroes = v.Split('+').Select(s => s.Trim()).ToList());
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpHeroes", out var inp)) inp.text = string.Join("+", _node.Heroes);
    }
}

public class PoolBlockUI : UIBlockNode
{
    private PoolBlock _node;
    public PoolBlockUI(PoolBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => $"{_node.Type.ToString().ToUpper()} POOL";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblEnt", "Entities (+ split):", 0.35f),
            GridCellSpec.CreateInput("InpEntities", "", 0.65f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpEntities", out var inpEnt)) inpEnt.onValueChanged.AddListener(v => _node.Entities = v.Split('+').Select(s => s.Trim()).ToList());
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpEntities", out var inpEnt)) inpEnt.text = string.Join("+", _node.Entities);
    }
}

// =====================================================================
// 3. PHASE UIs
// =====================================================================

public class Phase_SimpleChoiceBlockUI : UIBlockNode
{
    private Phase_SimpleChoiceBlock _node;
    public Phase_SimpleChoiceBlockUI(Phase_SimpleChoiceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "SIMPLE CHOICE PHASE (!)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblTitleStr", "Screen Title (Optional):", 0.4f),
            GridCellSpec.CreateInput("InpTitle", "e.g. Choose a Curse!", 0.6f, null)
        ),
        new GridRowSpec(GridCellSpec.CreateLabel("LblChildInfo", "<i>Drop Reward options inside to populate choices (@3).</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpTitle", out var inp)) inp.onValueChanged.AddListener(v => _node.Title = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpTitle", out var inp)) inp.text = _node.Title;
    }
}

public class Phase_MessageBlockUI : UIBlockNode
{
    private Phase_MessageBlock _node;
    public Phase_MessageBlockUI(Phase_MessageBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "MESSAGE PHASE (4)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblMsg", "Message Content:", 0.35f),
            GridCellSpec.CreateInput("InpMsg", "e.g. Hello World", 0.65f, null)
        ),
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblBtn", "Button Text:", 0.35f),
            GridCellSpec.CreateInput("InpBtn", "Ok", 0.65f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMsg", out var inpMsg)) inpMsg.onValueChanged.AddListener(v => _node.Message = v);
        if (r.Inputs.TryGetValue("InpBtn", out var inpBtn)) inpBtn.onValueChanged.AddListener(v => _node.ButtonText = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMsg", out var inpMsg)) inpMsg.text = _node.Message;
        if (r.Inputs.TryGetValue("InpBtn", out var inpBtn)) inpBtn.text = _node.ButtonText;
    }
}

public class Phase_HeroChangeBlockUI : UIBlockNode
{
    private Phase_HeroChangeBlock _node;
    private readonly string[] _typeOptions = { "Generated Hero (1)", "Random Class (0)" };
    public Phase_HeroChangeBlockUI(Phase_HeroChangeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "HERO CHANGE PHASE (5)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblPos", "Hero Position Index:", 0.5f),
            GridCellSpec.CreateInput("InpPos", "e.g. 0 for top", 0.5f, null)
        ),
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblType", "Change Type:", 0.5f),
            GridCellSpec.CreateDropdown("DropType", "", 0.5f, _typeOptions, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpPos", out var inp)) inp.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.HeroPositionIndex = i; });
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.onValueChanged.AddListener(val => _node.IsRandomClass = (val == 1));
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpPos", out var inp)) inp.text = _node.HeroPositionIndex.ToString();
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = _node.IsRandomClass ? 1 : 0;
    }
}

public class Phase_ItemCombineBlockUI : UIBlockNode
{
    private Phase_ItemCombineBlock _node;
    private readonly string[] _rules = { "2nd Highest -> Tier 3s", "Tier 0-3 -> Single Item" };
    public Phase_ItemCombineBlockUI(Phase_ItemCombineBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "ITEM COMBINE PHASE (7)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblRule", "Combine Rule:", 0.4f), GridCellSpec.CreateDropdown("DropRule", "", 0.6f, _rules, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropRule", out var drop)) drop.onValueChanged.AddListener(val => _node.Rule = (Phase_ItemCombineBlock.CombineRule)val);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropRule", out var drop)) drop.value = (int)_node.Rule;
    }
}

public class Phase_PositionSwapBlockUI : UIBlockNode
{
    private Phase_PositionSwapBlock _node;
    public Phase_PositionSwapBlockUI(Phase_PositionSwapBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "POSITION SWAP PHASE (8)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblA", "Hero Index A:", 0.5f), GridCellSpec.CreateInput("InpA", "0", 0.5f, null)
        ),
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblB", "Hero Index B:", 0.5f), GridCellSpec.CreateInput("InpB", "1", 0.5f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpA", out var inpA)) inpA.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.IndexA = i; });
        if (r.Inputs.TryGetValue("InpB", out var inpB)) inpB.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.IndexB = i; });
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpA", out var inpA)) inpA.text = _node.IndexA.ToString();
        if (r.Inputs.TryGetValue("InpB", out var inpB)) inpB.text = _node.IndexB.ToString();
    }
}

public class Phase_ChallengeBlockUI : UIBlockNode
{
    private Phase_ChallengeBlock _node;
    public Phase_ChallengeBlockUI(Phase_ChallengeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "CHALLENGE PHASE (9)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblMon", "Extra Monsters (+ split):", 0.4f), GridCellSpec.CreateInput("InpMon", "e.g. Militia+Militia", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop the Reward inside this block.</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMon", out var inp)) inp.onValueChanged.AddListener(v => _node.ExtraMonsters = v.Split('+').Select(s => s.Trim()).ToList());
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMon", out var inp)) inp.text = string.Join("+", _node.ExtraMonsters);
    }
}

public class Phase_Boolean1BlockUI : UIBlockNode
{
    private Phase_Boolean1Block _node;
    public Phase_Boolean1BlockUI(Phase_Boolean1Block node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "BOOLEAN PHASE (b)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
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
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var iVar)) iVar.onValueChanged.AddListener(v => _node.VariableName = v);
        if (r.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Threshold = i; });
        if (r.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.onValueChanged.AddListener(v => _node.TrueBranch = new RawTextNode(v));
        if (r.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.onValueChanged.AddListener(v => _node.FalseBranch = new RawTextNode(v));
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var iVar)) iVar.text = _node.VariableName;
        if (r.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.text = _node.Threshold.ToString();
        if (r.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.text = (_node.TrueBranch as RawTextNode)?.Text ?? "";
        if (r.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.text = (_node.FalseBranch as RawTextNode)?.Text ?? "";
    }
}

public class Phase_Boolean2BlockUI : UIBlockNode
{
    private Phase_Boolean2Block _node;
    public Phase_Boolean2BlockUI(Phase_Boolean2Block node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "BOOLEAN PHASE 2 (z)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblVar", "Variable to Check:", 0.4f), GridCellSpec.CreateInput("InpVar", "e.g. gold", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblThresh", "Threshold (>=):", 0.4f), GridCellSpec.CreateInput("InpThresh", "1", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblTrue", "True Branch (Raw):", 0.4f), GridCellSpec.CreateInput("InpTrue", "e.g. !vgoldV-400", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblFalse", "False Branch (Raw):", 0.4f), GridCellSpec.CreateInput("InpFalse", "e.g. 4You can't afford that!", 0.6f, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var iVar)) iVar.onValueChanged.AddListener(v => _node.VariableName = v);
        if (r.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Threshold = i; });
        if (r.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.onValueChanged.AddListener(v => _node.TrueBranch = new RawTextNode(v));
        if (r.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.onValueChanged.AddListener(v => _node.FalseBranch = new RawTextNode(v));
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var iVar)) iVar.text = _node.VariableName;
        if (r.Inputs.TryGetValue("InpThresh", out var iThr)) iThr.text = _node.Threshold.ToString();
        if (r.Inputs.TryGetValue("InpTrue", out var iTrue)) iTrue.text = (_node.TrueBranch as RawTextNode)?.Text ?? "";
        if (r.Inputs.TryGetValue("InpFalse", out var iFalse)) iFalse.text = (_node.FalseBranch as RawTextNode)?.Text ?? "";
    }
}

public class Phase_ChoiceBlockUI : UIBlockNode
{
    private Phase_ChoiceBlock _node;
    private readonly string[] _types = { "PointBuy", "Number", "UpToNumber", "Optional" };
    public Phase_ChoiceBlockUI(Phase_ChoiceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "CHOICE PHASE (c)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Choice Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _types, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblNum", "Number/Limit:", 0.4f), GridCellSpec.CreateInput("InpNum", "1", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblTitle", "Title (Optional):", 0.4f), GridCellSpec.CreateInput("InpTitle", "", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Reward tags inside to populate choices (@3)</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.onValueChanged.AddListener(v => _node.ChoiceType = _types[v]);
        if (r.Inputs.TryGetValue("InpNum", out var inpNum)) inpNum.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.NumChoices = i; });
        if (r.Inputs.TryGetValue("InpTitle", out var inpT)) inpT.onValueChanged.AddListener(v => _node.Title = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = Array.IndexOf(_types, _node.ChoiceType);
        if (r.Inputs.TryGetValue("InpNum", out var inpNum)) inpNum.text = _node.NumChoices.ToString();
        if (r.Inputs.TryGetValue("InpTitle", out var inpT)) inpT.text = _node.Title;
    }
}

public class Phase_TradeBlockUI : UIBlockNode
{
    private Phase_TradeBlock _node;
    public Phase_TradeBlockUI(Phase_TradeBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "TRADE / CURSED CHEST (t)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblItem1", "Reward 1 (Raw):", 0.4f), GridCellSpec.CreateInput("InpItem1", "e.g. r1~4~i", 0.6f, null)),
        new GridRowSpec(GridCellSpec.CreateLabel("LblItem2", "Reward 2 (Raw):", 0.4f), GridCellSpec.CreateInput("InpItem2", "e.g. r-1~1~m", 0.6f, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpItem1", out var i1)) i1.onValueChanged.AddListener(v => _node.Item1 = new RawTextNode(v));
        if (r.Inputs.TryGetValue("InpItem2", out var i2)) i2.onValueChanged.AddListener(v => _node.Item2 = new RawTextNode(v));
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpItem1", out var i1)) i1.text = (_node.Item1 as RawTextNode)?.Text ?? "";
        if (r.Inputs.TryGetValue("InpItem2", out var i2)) i2.text = (_node.Item2 as RawTextNode)?.Text ?? "";
    }
}

public class Phase_GenerateScreenBlockUI : UIBlockNode
{
    private Phase_GenerateScreenBlock _node;
    private readonly string[] _options = { "Item Screen (i)", "LevelUp Screen (h)" };
    public Phase_GenerateScreenBlockUI(Phase_GenerateScreenBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "GENERATE SCREEN (g)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Screen Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _options, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.onValueChanged.AddListener(v => _node.Type = v == 0 ? Phase_GenerateScreenBlock.ScreenType.Item : Phase_GenerateScreenBlock.ScreenType.LevelUp);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = _node.Type == Phase_GenerateScreenBlock.ScreenType.Item ? 0 : 1;
    }
}

public class Phase_StaticBlockUI : UIBlockNode
{
    private Phase_StaticBlock _node;
    private readonly string[] _options = Enum.GetNames(typeof(Phase_StaticBlock.StaticPhase));
    public Phase_StaticBlockUI(Phase_StaticBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "STATIC / EVENT PHASE";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblType", "Phase Type:", 0.4f), GridCellSpec.CreateDropdown("DropType", "", 0.6f, _options, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.onValueChanged.AddListener(v => _node.Phase = (Phase_StaticBlock.StaticPhase)Enum.GetValues(typeof(Phase_StaticBlock.StaticPhase)).GetValue(v));
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropType", out var drop)) drop.value = Array.IndexOf(Enum.GetValues(typeof(Phase_StaticBlock.StaticPhase)), _node.Phase);
    }
}

public class Phase_SequenceBlockUI : UIBlockNode
{
    private Phase_SequenceBlock _node;
    public Phase_SequenceBlockUI(Phase_SequenceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "STORY SEQUENCE PHASE (s)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblMsg", "Initial Message:", 0.35f),
            GridCellSpec.CreateInput("InpMsg", _node.SequenceMessage, 0.65f, null)
        ),
        new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop 'Sequence Option Fork' blocks below to add buttons.</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMsg", out var inp)) inp.onValueChanged.AddListener(v => _node.SequenceMessage = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMsg", out var inp)) inp.text = _node.SequenceMessage;
    }
}

public class SequenceOptionBlockUI : UIBlockNode
{
    private SequenceOptionBlock _node;
    public SequenceOptionBlockUI(SequenceOptionBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "SEQUENCE OPTION FORK";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblBtn", "Button Text:", 0.35f),
            GridCellSpec.CreateInput("InpBtn", _node.ButtonText, 0.65f, null)
        ),
        new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Actions for this button into this block.</i>", 1f))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpBtn", out var inp)) inp.onValueChanged.AddListener(v => _node.ButtonText = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpBtn", out var inp)) inp.text = _node.ButtonText;
    }
}

// Container blocks requiring just info text
public class Phase_LinkedBlockUI : UIBlockNode
{
    public Phase_LinkedBlockUI(Phase_LinkedBlock node) : base(node) { }
    public override string GetBlockTitle() => "LINKED PHASES (l)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Phases inside to link them sequentially (@1)</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class Phase_LevelEndBlockUI : UIBlockNode
{
    public Phase_LevelEndBlockUI(Phase_LevelEndBlock node) : base(node) { }
    public override string GetBlockTitle() => "LEVEL END PHASE (2)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Phases inside to attach them to the end screen</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class Phase_RandomRevealBlockUI : UIBlockNode
{
    public Phase_RandomRevealBlockUI(Phase_RandomRevealBlock node) : base(node) { }
    public override string GetBlockTitle() => "RANDOM REVEAL POPUP (r)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop a Reward Tag inside this block.</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

// =====================================================================
// 4. REWARD TAG UIs
// =====================================================================

public class Reward_StandardBlockUI : UIBlockNode
{
    private Reward_StandardBlock _node;
    private readonly string[] _typeOptions = { "Item (i)", "Modifier (m)", "Hero (g)", "LevelUp (l)" };
    public Reward_StandardBlockUI(Reward_StandardBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "STANDARD REWARD TAG";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblType", "Reward Type:", 0.35f),
            GridCellSpec.CreateDropdown("DropRewardType", "", 0.65f, _typeOptions, null)
        ),
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblTarget", "Entity Name:", 0.35f),
            GridCellSpec.CreateInput("InpTarget", "e.g. Mana Jelly", 0.65f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropRewardType", out var drop))
        {
            drop.onValueChanged.AddListener(val => {
                if (val == 0) _node.Type = Reward_StandardBlock.RewardType.Item;
                else if (val == 1) _node.Type = Reward_StandardBlock.RewardType.Modifier;
                else if (val == 2) _node.Type = Reward_StandardBlock.RewardType.Hero;
                else if (val == 3) _node.Type = Reward_StandardBlock.RewardType.LevelUp;
            });
        }
        if (r.Inputs.TryGetValue("InpTarget", out var inp)) inp.onValueChanged.AddListener(v => _node.TargetEntity = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropRewardType", out var drop))
        {
            if (_node.Type == Reward_StandardBlock.RewardType.Item) drop.value = 0;
            else if (_node.Type == Reward_StandardBlock.RewardType.Modifier) drop.value = 1;
            else if (_node.Type == Reward_StandardBlock.RewardType.Hero) drop.value = 2;
            else if (_node.Type == Reward_StandardBlock.RewardType.LevelUp) drop.value = 3;
        }
        if (r.Inputs.TryGetValue("InpTarget", out var inp)) inp.text = _node.TargetEntity;
    }
}

public class Reward_RandomBlockUI : UIBlockNode
{
    private Reward_RandomBlock _node;
    private readonly string[] _flagOptions = { "Item (i)", "Modifier (m)", "Hero (g)", "LevelUp (l)" };
    public Reward_RandomBlockUI(Reward_RandomBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "RANDOM REWARD TAG (r/q)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
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
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMin", out var inpMin)) inpMin.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.MinTier = i; });
        if (r.Inputs.TryGetValue("InpMax", out var inpMax)) inpMax.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.MaxTier = i; });
        if (r.Inputs.TryGetValue("InpAmt", out var inpAmt)) inpAmt.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.Amount = i; });
        if (r.Dropdowns.TryGetValue("DropFlag", out var drop))
        {
            drop.onValueChanged.AddListener(val => {
                if (val == 0) _node.RewardTypeFlag = "i";
                else if (val == 1) _node.RewardTypeFlag = "m";
                else if (val == 2) _node.RewardTypeFlag = "g";
                else if (val == 3) _node.RewardTypeFlag = "l";
            });
        }
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpMin", out var inpMin)) inpMin.text = _node.MinTier.ToString();
        if (r.Inputs.TryGetValue("InpMax", out var inpMax)) inpMax.text = _node.MaxTier.ToString();
        if (r.Inputs.TryGetValue("InpAmt", out var inpAmt)) inpAmt.text = _node.Amount.ToString();
        if (r.Dropdowns.TryGetValue("DropFlag", out var drop))
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

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(GridCellSpec.CreateLabel("LblEnum", "Enum Type:", 0.35f), GridCellSpec.CreateDropdown("DropEnum", "", 0.65f, _enumOptions, null))
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropEnum", out var drop)) drop.onValueChanged.AddListener(val => _node.EnumName = _enumOptions[val]);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Dropdowns.TryGetValue("DropEnum", out var drop)) drop.value = Array.IndexOf(_enumOptions, _node.EnumName);
    }
}

public class Reward_ValueModifyBlockUI : UIBlockNode
{
    private Reward_ValueModifyBlock _node;
    public Reward_ValueModifyBlockUI(Reward_ValueModifyBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "MODIFY VARIABLE TAG (v)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblVar", "Variable Name:", 0.4f),
            GridCellSpec.CreateInput("InpVar", "e.g. Gold", 0.6f, null)
        ),
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblVal", "Value Added (V):", 0.4f),
            GridCellSpec.CreateInput("InpVal", "e.g. 50", 0.6f, null)
        )
    };
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var inpVar)) inpVar.onValueChanged.AddListener(v => _node.VariableName = v);
        if (r.Inputs.TryGetValue("InpVal", out var inpVal)) inpVal.onValueChanged.AddListener(v => { if (int.TryParse(v, out int i)) _node.ValueToAdd = i; });
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Inputs.TryGetValue("InpVar", out var inpVar)) inpVar.text = _node.VariableName;
        if (r.Inputs.TryGetValue("InpVal", out var inpVal)) inpVal.text = _node.ValueToAdd.ToString();
    }
}

public class Reward_ReplaceBlockUI : UIBlockNode
{
    private Reward_ReplaceBlock _node;
    public Reward_ReplaceBlockUI(Reward_ReplaceBlock node) : base(node) { _node = node; }
    public override string GetBlockTitle() => "REPLACE TAG (p)";

    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> {
        new GridRowSpec(
            GridCellSpec.CreateLabel("LblModFlag", "Is Modifier Removal (pm):", 0.7f),
            GridCellSpec.CreateToggle("TglModFlag", "", 0.3f, null)
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
    protected override void BindSpecificUI(RectTransform c, GridReferences r)
    {
        if (r.Toggles != null && r.Toggles.TryGetValue("TglModFlag", out var tgl)) tgl.onValueChanged.AddListener(v => _node.IsModifierReplacement = v);
        if (r.Inputs.TryGetValue("InpTarget", out var inpTarget)) inpTarget.onValueChanged.AddListener(v => _node.TargetToReplace = v);
        if (r.Inputs.TryGetValue("InpNew", out var inpNew)) inpNew.onValueChanged.AddListener(v => _node.NewValue = v);
    }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r)
    {
        if (r.Toggles != null && r.Toggles.TryGetValue("TglModFlag", out var tgl)) tgl.isOn = _node.IsModifierReplacement;
        if (r.Inputs.TryGetValue("InpTarget", out var inpTarget)) inpTarget.text = _node.TargetToReplace;
        if (r.Inputs.TryGetValue("InpNew", out var inpNew)) inpNew.text = _node.NewValue;
    }
}

public class Reward_ChoiceBlockUI : UIBlockNode
{
    public Reward_ChoiceBlockUI(Reward_ChoiceBlock node) : base(node) { }
    public override string GetBlockTitle() => "RANDOM CHOICE (o)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>Drop Reward options inside this block (@4).</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

public class Reward_SkipBlockUI : UIBlockNode
{
    public Reward_SkipBlockUI(Reward_SkipBlock node) : base(node) { }
    public override string GetBlockTitle() => "SKIP TAG (s)";
    protected override List<GridRowSpec> GetSpecificRowSpecs() => new List<GridRowSpec> { new GridRowSpec(GridCellSpec.CreateLabel("LblInfo", "<i>The Skip tag has no configuration properties.</i>", 1f)) };
    protected override void BindSpecificUI(RectTransform c, GridReferences r) { }
    protected override void RestoreSpecificState(RectTransform c, GridReferences r) { }
}

#endregion