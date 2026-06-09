using ModEditor.Compiler;
using ModEditor.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UIBlockFactory
{
    public static UIBlockNode CreateUIBlock(string optionName, ITextmodNode nodeModel, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
    {
        // Floor conditions (Non-modifier wrappers)
        if (nodeModel is FloorConditionBlock floorModel)
            return new UIFloorConditionBlock(floorModel, generator, onRebuild, onRemove);

        // Commands mapping directly to custom entity selection
        if (nodeModel is AddEntityBlock addEntityModel)
            return new UIAddEntityBlock(addEntityModel, generator, onRebuild, onRemove);

        if (nodeModel is FightBlock fightModel)
            return new UIFightBlock(fightModel, generator, onRebuild, onRemove);

        if (nodeModel is PartyBlock partyModel)
            return new UIPartyBlock(partyModel, generator, onRebuild, onRemove);

        if (nodeModel is PoolBlock poolModel)
            return new UIPoolBlock(poolModel, generator, onRebuild, onRemove);

        // Standard core phases
        if (nodeModel is Phase_MessageBlock msgModel)
            return new UIPhaseMessageBlock(msgModel, generator, onRebuild, onRemove);

        // Default fallback mappings
        if (nodeModel is TextmodBlock textmodModel)
        {
            return new UIGenericTextmodBlock(textmodModel, optionName, generator, onRebuild, onRemove);
        }

        return new UIGenericWrapperBlock(nodeModel, optionName, generator, onRebuild, onRemove);
    }
}

public class UIGenericTextmodBlock : UITextmodBlock<TextmodBlock>
{
    public UIGenericTextmodBlock(TextmodBlock model, string typeName, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
        : base(model, typeName, generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight, GridCellSpec.CreateLabel("Warning", "Specific UI Fields not yet implemented for this block.", 1.0f))
            };
    }
}

public class UIGenericWrapperBlock : UIWrapperBlock<ITextmodNode>
{
    public UIGenericWrapperBlock(ITextmodNode model, string typeName, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
        : base(model, typeName, generator, onRebuild, onRemove) { }

    protected override List<GridRowSpec> GetContentRowSpecs()
    {
        return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight, GridCellSpec.CreateLabel("Warning", "Wrapper fields not yet implemented.", 1.0f))
            };
    }
}

namespace ModEditor.UI
{
    // =========================================================================
    // TIER 0: THE ABSOLUTE BASE (NON-DESTRUCTIVE TOGGLING)
    // =========================================================================
    public abstract class UIBlockNode
    {
        public string Id { get; private set; }
        public string TypeName { get; private set; }
        public ITextmodNode NodeModel { get; protected set; }

        public Action<int> onMoveRequested;
        protected Action onRebuildNeeded; // Used ONLY when adding/removing list items (Fight/Party/Pool)
        protected Action onRemoveRequested;

        protected bool isExpanded = true;
        protected bool isAdvancedExpanded = false;

        protected FullScreenUIGenerator uiGenerator;
        protected float headerHeight => uiGenerator.rowHeight;

        public abstract Color CategoryColor { get; }

        // Cached UI Elements for Non-Destructive Toggling
        private RectTransform _container;
        private GridReferences _refs;
        private List<GameObject> _advancedRows = new List<GameObject>();
        private List<GameObject> _contentRows = new List<GameObject>();

        protected UIBlockNode(ITextmodNode nodeModel, string typeName, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
        {
            Id = Guid.NewGuid().ToString();
            NodeModel = nodeModel;
            TypeName = typeName;
            uiGenerator = generator;
            onRebuildNeeded = onRebuild;
            onRemoveRequested = onRemove;
        }

        public virtual List<GridRowSpec> GetRowSpecs()
        {
            List<GridRowSpec> rows = new List<GridRowSpec>();

            string foldoutSymbol = isExpanded ? "▼" : "▶";
            string gearSymbol = isAdvancedExpanded ? "[X] Props" : "[ ] Props";

            // 0. Header Row (Always Visible)
            rows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateButton($"Foldout_{Id}", foldoutSymbol, 0.08f, ToggleCollapse),
                GridCellSpec.CreateLabel($"Title_{Id}", TypeName.ToUpper(), 0.44f),
                GridCellSpec.CreateButton($"AdvBtn_{Id}", gearSymbol, 0.18f, ToggleAdvanced),
                GridCellSpec.CreateButton($"MoveUp_{Id}", "▲", 0.10f, () => onMoveRequested?.Invoke(-1)),
                GridCellSpec.CreateButton($"MoveDown_{Id}", "▼", 0.10f, () => onMoveRequested?.Invoke(1)),
                GridCellSpec.CreateButton($"RemoveBtn_{Id}", "X", 0.10f, () => onRemoveRequested?.Invoke())
            ));

            // 1 & 2. Advanced Properties (Only generated if it's a TextmodBlock)
            if (NodeModel is TextmodBlock textmodBlock)
            {
                rows.Add(CreateSeparator());
                rows.Add(new GridRowSpec(headerHeight,
                    GridCellSpec.CreateInput($"EntName_{Id}", "Entity (.n.)", 0.33f, (val) => textmodBlock.CustomEntityName = val),
                    GridCellSpec.CreateInput($"EncName_{Id}", "Encounter (.mn.)", 0.33f, (val) => textmodBlock.CustomEncounterName = val),
                    GridCellSpec.CreateInput($"ModTier_{Id}", "Tier (.modtier.)", 0.34f, (val) => textmodBlock.ModTier = val)
                ));
            }

            // 3. Content Separator
            rows.Add(CreateSeparator());

            // 4+. Specific Block Content
            rows.AddRange(GetContentRowSpecs());

            return rows;
        }

        protected abstract List<GridRowSpec> GetContentRowSpecs();

        /// <summary>
        /// Binds the UI Rows to memory so we can hide/show them seamlessly without destroying data.
        /// </summary>
        public virtual void BindUI(RectTransform container, GridReferences refs)
        {
            _container = container;
            _refs = refs;

            // Apply category color natively to the container background
            Image bg = container.GetComponent<Image>();
            if (bg == null) bg = container.gameObject.AddComponent<Image>();
            bg.color = CategoryColor;

            _advancedRows.Clear();
            _contentRows.Clear();

            int childIndex = 1; // Skip Header [0]

            if (NodeModel is TextmodBlock)
            {
                if (childIndex < container.childCount) _advancedRows.Add(container.GetChild(childIndex++).gameObject); // Separator
                if (childIndex < container.childCount) _advancedRows.Add(container.GetChild(childIndex++).gameObject); // Inputs
            }

            // Separator before content
            if (childIndex < container.childCount) _contentRows.Add(container.GetChild(childIndex++).gameObject);

            // Remaining content rows
            while (childIndex < container.childCount)
            {
                _contentRows.Add(container.GetChild(childIndex++).gameObject);
            }

            UpdateVisibility();
        }

        public virtual void RestoreState(RectTransform container, GridReferences refs)
        {
            if (NodeModel is TextmodBlock textmodBlock)
            {
                RestoreInput(refs, $"EntName_{Id}", textmodBlock.CustomEntityName);
                RestoreInput(refs, $"EncName_{Id}", textmodBlock.CustomEncounterName);
                RestoreInput(refs, $"ModTier_{Id}", textmodBlock.ModTier);
            }
        }

        public virtual void CompileRecursive(GridReferences refs) { }

        private void ToggleCollapse()
        {
            isExpanded = !isExpanded;
            UpdateVisibility();
        }

        private void ToggleAdvanced()
        {
            isAdvancedExpanded = !isAdvancedExpanded;
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            foreach (var row in _advancedRows) row.SetActive(isExpanded && isAdvancedExpanded);
            foreach (var row in _contentRows) row.SetActive(isExpanded);

            if (_refs != null && _refs.Buttons.TryGetValue($"Foldout_{Id}", out Button foldoutBtn))
            {
                foldoutBtn.GetComponentInChildren<TextMeshProUGUI>().text = isExpanded ? "▼" : "▶";
            }
            if (_refs != null && _refs.Buttons.TryGetValue($"AdvBtn_{Id}", out Button advBtn))
            {
                advBtn.GetComponentInChildren<TextMeshProUGUI>().text = isAdvancedExpanded ? "[X] Props" : "[ ] Props";
            }

            if (_container != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
                ScratchModUI workspace = _container.GetComponentInParent<ScratchModUI>();
                if (workspace != null) workspace.RefreshWorkspaceLayout();
            }
        }

        protected GridRowSpec CreateSeparator() => new GridRowSpec(1.5f, GridCellSpec.CreateImagePanel($"Sep_{Id}", 1.0f));

        protected void RestoreInput(GridReferences refs, string key, string value)
        {
            if (refs.Inputs.TryGetValue(key, out var input)) input.SetTextWithoutNotify(value);
        }

        protected void RestoreDropdown(GridReferences refs, string key, int value)
        {
            if (refs.Dropdowns.TryGetValue(key, out var drop)) drop.SetValueWithoutNotify(value);
        }
    }

    // =========================================================================
    // CATEGORY COLOR-CODED INTERMEDIATE CLASSES (GENERICS ADDED)
    // =========================================================================

    public abstract class UIWrapperBlock<T> : UIBlockNode where T : ITextmodNode
    {
        public override Color CategoryColor => new Color(1.0f, 0.67f, 0.1f, 0.95f);
        protected T Model { get; private set; }

        protected UIWrapperBlock(T model, string typeName, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, typeName, generator, onRebuild, onRemove)
        {
            Model = model;
        }
    }

    public abstract class UITextmodBlock<T> : UIBlockNode where T : TextmodBlock // Enforces TextmodBlock so we get properties
    {
        public override Color CategoryColor => new Color(0.3f, 0.59f, 1.0f, 0.95f);
        protected T Model { get; private set; }

        protected UITextmodBlock(T model, string typeName, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, typeName, generator, onRebuild, onRemove)
        {
            Model = model;
        }
    }

    // =========================================================================
    // TIER 2: CONCRETE IMPLEMENTATIONS (Strongly Typed via Generics)
    // =========================================================================

    public class UIAndChainBlock : UIWrapperBlock<AndChainBlock>
    {
        public UIAndChainBlock(AndChainBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "And Chain ( & )", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight, GridCellSpec.CreateLabel("Desc", "Blocks inside will run concurrently:", 1.0f)),
                new GridRowSpec(40f, GridCellSpec.CreateDropZone($"InnerChain_{Id}", 1.0f)) // Height flexes automatically
            };
        }

        public override void CompileRecursive(GridReferences refs)
        {
            if (refs.DropZones.TryGetValue($"InnerChain_{Id}", out BlockDropZone zone))
            {
                Model.Nodes.Clear();
                foreach (Transform child in zone.transform)
                {
                    VisualBlockComponent visualBlock = child.GetComponent<VisualBlockComponent>();
                    if (visualBlock != null && visualBlock.UI_Node != null)
                    {
                        visualBlock.UI_Node.CompileRecursive(child.GetComponentInChildren<GridReferences>(true));
                        Model.Nodes.Add(visualBlock.UI_Node.NodeModel);
                    }
                }
            }
        }
    }

    public class UIFloorConditionBlock : UIWrapperBlock<FloorConditionBlock>
    {
        public UIFloorConditionBlock(FloorConditionBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Floor Condition Wrap", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight,
                    GridCellSpec.CreateDropdown($"Type_{Id}", "Condition Type", 0.4f, new[] { "Single Floor", "Floor Range", "Every X Floors" },
                        (val) => Model.Type = (FloorConditionBlock.ConditionType)val),
                    GridCellSpec.CreateInput($"Start_{Id}", "Start", 0.3f, (val) => { if(int.TryParse(val, out int v)) Model.StartFloor = v; }),
                    GridCellSpec.CreateInput($"End_{Id}", "End", 0.3f, (val) => { if(int.TryParse(val, out int v)) Model.EndFloor = v; })
                ),
                new GridRowSpec(40f, GridCellSpec.CreateDropZone($"Payload_{Id}", 1.0f))
            };
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreDropdown(refs, $"Type_{Id}", (int)Model.Type);
            RestoreInput(refs, $"Start_{Id}", Model.StartFloor.ToString());
            RestoreInput(refs, $"End_{Id}", Model.EndFloor.ToString());
        }

        public override void CompileRecursive(GridReferences refs)
        {
            if (refs.DropZones.TryGetValue($"Payload_{Id}", out BlockDropZone zone))
            {
                Model.Payload = null;
                foreach (Transform child in zone.transform)
                {
                    VisualBlockComponent vb = child.GetComponent<VisualBlockComponent>();
                    if (vb != null)
                    {
                        vb.UI_Node.CompileRecursive(child.GetComponentInChildren<GridReferences>(true));
                        Model.Payload = vb.UI_Node.NodeModel;
                        break;
                    }
                }
            }
        }
    }

    public class UIAddEntityBlock : UITextmodBlock<AddEntityBlock>
    {
        private int _selectedEntityIndex = 0;

        public UIAddEntityBlock(AddEntityBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Add Entity Command", generator, onRebuild, onRemove)
        {
            string[] entities = ModPackageHelper.GetAllEntityNames();
            int idx = Array.IndexOf(entities, Model.Entity);
            if (idx >= 0) _selectedEntityIndex = idx;
        }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            string[] entities = ModPackageHelper.GetAllEntityNames();
            return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight,
                    GridCellSpec.CreateFilteredDropdown($"EntitySelect_{Id}", "Select Entity Type", 1.0f, entities, (val) => {
                        _selectedEntityIndex = val;
                        if (entities.Length > 0 && val >= 0 && val < entities.Length) Model.Entity = entities[val];
                    })
                )
            };
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreDropdown(refs, $"EntitySelect_{Id}", _selectedEntityIndex);
        }
    }

    public class UIFightBlock : UITextmodBlock<FightBlock>
    {
        private int _selectedMonsterIndex = 0;

        public UIFightBlock(FightBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Fight Encounter (fight.)", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            var rows = new List<GridRowSpec>();
            string[] monsters = ModPackageHelper.GetMonsterNames();

            rows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateFilteredDropdown($"MonsterSelect_{Id}", "Select Monster to Add", 0.7f, monsters, (val) => _selectedMonsterIndex = val),
                GridCellSpec.CreateButton($"AddMonsterBtn_{Id}", "Add", 0.3f, () => {
                    if (monsters.Length > 0 && _selectedMonsterIndex >= 0 && _selectedMonsterIndex < monsters.Length)
                    {
                        Model.Monsters.Add(monsters[_selectedMonsterIndex]);
                        onRebuildNeeded?.Invoke(); // Dynamically rebuild grid because row count changes
                    }
                })
            ));

            for (int i = 0; i < Model.Monsters.Count; i++)
            {
                int index = i;
                rows.Add(new GridRowSpec(headerHeight,
                    GridCellSpec.CreateLabel($"MonsterLabel_{Id}_{index}", Model.Monsters[index], 0.8f),
                    GridCellSpec.CreateButton($"RemoveMonsterBtn_{Id}_{index}", "Remove", 0.2f, () => {
                        Model.Monsters.RemoveAt(index);
                        onRebuildNeeded?.Invoke(); // Rebuild grid to remove the row
                    })
                ));
            }

            return rows;
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreDropdown(refs, $"MonsterSelect_{Id}", _selectedMonsterIndex);
        }
    }

    public class UIPartyBlock : UITextmodBlock<PartyBlock>
    {
        private int _selectedHeroIndex = 0;

        public UIPartyBlock(PartyBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Set Party (party.)", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            var rows = new List<GridRowSpec>();
            string[] heroes = ModPackageHelper.GetHeroNames();

            rows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateFilteredDropdown($"HeroSelect_{Id}", "Select Hero to Add", 0.7f, heroes, (val) => _selectedHeroIndex = val),
                GridCellSpec.CreateButton($"AddHeroBtn_{Id}", "Add", 0.3f, () => {
                    if (heroes.Length > 0 && _selectedHeroIndex >= 0 && _selectedHeroIndex < heroes.Length)
                    {
                        Model.Heroes.Add(heroes[_selectedHeroIndex]);
                        onRebuildNeeded?.Invoke();
                    }
                })
            ));

            for (int i = 0; i < Model.Heroes.Count; i++)
            {
                int index = i;
                rows.Add(new GridRowSpec(headerHeight,
                    GridCellSpec.CreateLabel($"HeroLabel_{Id}_{index}", Model.Heroes[index], 0.8f),
                    GridCellSpec.CreateButton($"RemoveHeroBtn_{Id}_{index}", "Remove", 0.2f, () => {
                        Model.Heroes.RemoveAt(index);
                        onRebuildNeeded?.Invoke();
                    })
                ));
            }

            return rows;
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreDropdown(refs, $"HeroSelect_{Id}", _selectedHeroIndex);
        }
    }

    public class UIPoolBlock : UITextmodBlock<PoolBlock>
    {
        private int _selectedEntityIndex = 0;

        public UIPoolBlock(PoolBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Pool Registration Command", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            var rows = new List<GridRowSpec>();

            string[] poolEntities;
            switch (Model.Type)
            {
                case PoolBlock.PoolType.Hero: poolEntities = ModPackageHelper.GetHeroNames(); break;
                case PoolBlock.PoolType.Monster: poolEntities = ModPackageHelper.GetMonsterNames(); break;
                default: poolEntities = ModPackageHelper.GetItemNames(); break;
            }

            rows.Add(new GridRowSpec(headerHeight,
                GridCellSpec.CreateDropdown($"PoolType_{Id}", "Pool Class Type", 0.3f, new[] { "Item Pool", "Hero Pool", "Monster Pool" }, (val) => {
                    Model.Type = (PoolBlock.PoolType)val;
                    _selectedEntityIndex = 0;
                    onRebuildNeeded?.Invoke();
                }),
                GridCellSpec.CreateFilteredDropdown($"EntitySelect_{Id}", "Select Entity", 0.5f, poolEntities, (val) => _selectedEntityIndex = val),
                GridCellSpec.CreateButton($"AddEntityBtn_{Id}", "Add", 0.2f, () => {
                    if (poolEntities.Length > 0 && _selectedEntityIndex >= 0 && _selectedEntityIndex < poolEntities.Length)
                    {
                        Model.Entities.Add(poolEntities[_selectedEntityIndex]);
                        onRebuildNeeded?.Invoke();
                    }
                })
            ));

            for (int i = 0; i < Model.Entities.Count; i++)
            {
                int index = i;
                rows.Add(new GridRowSpec(headerHeight,
                    GridCellSpec.CreateLabel($"EntityLabel_{Id}_{index}", Model.Entities[index], 0.8f),
                    GridCellSpec.CreateButton($"RemoveEntityBtn_{Id}_{index}", "Remove", 0.2f, () => {
                        Model.Entities.RemoveAt(index);
                        onRebuildNeeded?.Invoke();
                    })
                ));
            }

            return rows;
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreDropdown(refs, $"PoolType_{Id}", (int)Model.Type);
            RestoreDropdown(refs, $"EntitySelect_{Id}", _selectedEntityIndex);
        }
    }

    public class UIPhaseMessageBlock : UITextmodBlock<Phase_MessageBlock>
    {
        public UIPhaseMessageBlock(Phase_MessageBlock model, FullScreenUIGenerator generator, Action onRebuild, Action onRemove)
            : base(model, "Message Popup Phase (4)", generator, onRebuild, onRemove) { }

        protected override List<GridRowSpec> GetContentRowSpecs()
        {
            return new List<GridRowSpec>
            {
                new GridRowSpec(headerHeight,
                    GridCellSpec.CreateInput($"Msg_{Id}", "Message Text", 0.7f, (val) => Model.Message = val),
                    GridCellSpec.CreateInput($"Btn_{Id}", "Button Text", 0.3f, (val) => Model.ButtonText = val)
                )
            };
        }

        public override void RestoreState(RectTransform container, GridReferences refs)
        {
            base.RestoreState(container, refs);
            RestoreInput(refs, $"Msg_{Id}", Model.Message);
            RestoreInput(refs, $"Btn_{Id}", Model.ButtonText);
        }
    }
}