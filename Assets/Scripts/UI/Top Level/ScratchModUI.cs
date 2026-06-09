using ModEditor.Compiler;
using ModEditor.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModEditor
{
    public class ScratchModUI : RootUI
    {
        public static ScratchModUI Instance { get; private set; }

        // Tracks the connection between spawned UI containers and their logical compiler/UI models
        private readonly List<BlockInstance> _activeUIBlocks = new List<BlockInstance>();

        private ScrollRect _workspaceScroll;
        private Transform _workspaceContent;
        private TMP_Dropdown _leftDirectiveDropdown;
        private BlockDropZone _rootWorkspaceZone;

        protected override void BuildUIAndBind()
        {
            float totalHeight = 600f;
            if (uiGenerator != null && uiGenerator.canvas != null)
            {
                RectTransform canvasRt = uiGenerator.canvas.GetComponent<RectTransform>();
                if (canvasRt != null) totalHeight = canvasRt.rect.height;
            }

            float rowHeight = uiGenerator != null ? uiGenerator.rowHeight : 40f;
            float spacing = uiGenerator != null ? uiGenerator.rowSpacing : 8f;
            float dynamicScrollHeight = Mathf.Max(300f, totalHeight - (rowHeight * 2f) - (spacing * 3f) - 20f);

            // Left column (Load controls and Add dropdown using the unified backend)
            List<string> dropdownOptions = new List<string> { "Quick Add Block..." };
            dropdownOptions.AddRange(CodeBlocks._blockSyntaxOptions);

            List<GridRowSpec> leftRows = new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("LeftTitle", "CONFIGURATION & UTILITIES", 1.0f)),
                new GridRowSpec(rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, LoadModFromClipboard)),
                new GridRowSpec(rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, dropdownOptions.ToArray(), OnDropdownSelected))
            };

            // Right column (Dynamic IDE Workspace)
            List<GridRowSpec> rightRows = new List<GridRowSpec>
            {
                new GridRowSpec(rowHeight, GridCellSpec.CreateLabel("WORKSPACE_TITLE_TXT", "MOD AUTHORING WORKSPACE (Right-Click empty area to add blocks)", 1.0f)),
                new GridRowSpec(dynamicScrollHeight, GridCellSpec.CreateScrollView("WorkspaceScrollArea", 1.0f)),
                new GridRowSpec(rowHeight,
                    GridCellSpec.CreateButton("BtnCompileWorkspace", "Compile Layout / Copy", 0.5f, CompileBlocks),
                    GridCellSpec.CreateButton("BtnClearWorkspace", "Clear Workspace", 0.5f, ClearWorkspace)
                )
            };

            List<ColumnSpec> columns = new List<ColumnSpec>
            {
                new ColumnSpec("Left_Column", 0.0f, 0.32f, leftRows),
                new ColumnSpec("Right_Column", 0.33f, 1.0f, rightRows)
            };

            generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

            if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("Left_Column", out GridReferences leftRefs))
            {
                leftRefs.Dropdowns.TryGetValue("ModDropdown", out _leftDirectiveDropdown);
            }

            ConfigureWorkspace();
        }

        private void ConfigureWorkspace()
        {
            if (generatedScreen == null) return;

            if (generatedScreen.ColumnRefs.TryGetValue("Right_Column", out GridReferences refs))
            {
                if (refs.ScrollViews.TryGetValue("WorkspaceScrollArea", out _workspaceScroll))
                {
                    _workspaceContent = _workspaceScroll.content;

                    // Configure the Scroll Content to automatically stack the generated blocks
                    VerticalLayoutGroup layout = _workspaceContent.gameObject.GetComponent<VerticalLayoutGroup>();
                    if (layout == null) layout = _workspaceContent.gameObject.AddComponent<VerticalLayoutGroup>();

                    layout.spacing = 8f;
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childControlHeight = true; // Let layout group control heights based on LayoutElement
                    layout.childControlWidth = true;
                    layout.childForceExpandHeight = false;
                    layout.childForceExpandWidth = true;

                    ContentSizeFitter fitter = _workspaceContent.gameObject.GetComponent<ContentSizeFitter>();
                    if (fitter == null) fitter = _workspaceContent.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Context Menu Trigger Initialization
                    RectTransform viewportRt = _workspaceScroll.viewport;
                    if (viewportRt != null)
                    {
                        Image viewportImage = viewportRt.GetComponent<Image>();
                        if (viewportImage == null)
                        {
                            viewportImage = viewportRt.gameObject.AddComponent<Image>();
                            viewportImage.color = Color.clear;
                        }

                        ContextMenuTrigger trigger = viewportRt.gameObject.GetComponent<ContextMenuTrigger>();
                        if (trigger == null) trigger = viewportRt.gameObject.AddComponent<ContextMenuTrigger>();

                        FilteredDropdown dropdownPrefab = uiGenerator.filteredDropdown != null
                            ? uiGenerator.filteredDropdown.GetComponent<FilteredDropdown>() : null;

                        trigger.Initialize(
                            generatedScreen.RootWrapper,
                            dropdownPrefab,
                            CodeBlocks._blockSyntaxOptions,
                            AddBlockToWorkspace
                        );
                    }
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

        private void AddBlockToWorkspace(string optionName)
        {
            if (_workspaceContent == null) return;

            // 1. Create the backend compilation node
            ITextmodNode compilerNode;
            try
            {
                compilerNode = TextmodBlockFactory.CreateBlock(optionName);
                if (compilerNode == null) return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not instantiate compiler node logic: {ex.Message}");
                return;
            }

            // 2. Create the empty container GameObject for the UI grid
            GameObject containerGo = new GameObject($"Block_{optionName}", typeof(RectTransform), typeof(LayoutElement));
            containerGo.transform.SetParent(_workspaceContent, false);

            // 3. Define the callbacks needed by the UIBlockNode
            Action onRebuild = null;
            Action onRemove = null;

            // 4. Create the UI Block Node
            UIBlockNode uiNode = UIBlockFactory.CreateUIBlock(optionName, compilerNode, uiGenerator,
                () => onRebuild?.Invoke(),
                () => onRemove?.Invoke()
            );

            // 5. Configure the visual workspace components
            containerGo.AddComponent<CanvasGroup>();

            DragReorderItem dragItem = containerGo.AddComponent<DragReorderItem>();
            dragItem.OnDragEnded = () => RefreshWorkspaceLayout();

            VisualBlockComponent vb = containerGo.AddComponent<VisualBlockComponent>();
            vb.UI_Node = uiNode;

            // 6. Setup UI Block Node execution callbacks
            uiNode.onMoveRequested = (dir) => MoveBlock(containerGo, dir);

            onRebuild = () => RefreshWorkspaceLayout();

            onRemove = () =>
            {
                _activeUIBlocks.RemoveAll(b => b.UINode == uiNode);
                Destroy(containerGo);
                RefreshWorkspaceLayout(); // Refresh layout to fix nesting heights after deletion
            };

            // 7. Store in active list
            _activeUIBlocks.Add(new BlockInstance(containerGo, compilerNode, uiNode));

            // 8. Perform Initial Render
            RebuildBlockUI(containerGo, uiNode);

            // Scroll to newly added block
            Canvas.ForceUpdateCanvases();
            if (_workspaceScroll != null) _workspaceScroll.verticalNormalizedPosition = 0f;
        }

        private void RebuildBlockUI(GameObject container, UIBlockNode uiNode)
        {
            RectTransform rt = container.GetComponent<RectTransform>();

            // Rebuild visual panels
            GridReferences refs = uiGenerator.RebuildGrid(rt, uiNode.GetRowSpecs(), useMargins: false);

            LayoutElement layoutEl = container.GetComponent<LayoutElement>();
            layoutEl.minHeight = refs.TotalHeight;
            layoutEl.preferredHeight = refs.TotalHeight;

            // Updated parameter pass: passes visual container context for theme execution
            uiNode.RestoreState(rt, refs);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_workspaceContent.GetComponent<RectTransform>());
        }

        public void RefreshWorkspaceLayout()
        {
            if (_activeUIBlocks.Count == 0) return;

            // 1. Sort by hierarchy depth descending (deepest block first)
            var sortedBlocks = _activeUIBlocks
                .Where(b => b.ContainerGO != null)
                .OrderByDescending(b => GetTransformDepth(b.ContainerGO.transform))
                .ToList();

            // 2. Perform sequential bottom-up visual rendering
            foreach (var block in sortedBlocks)
            {
                RebuildSingleBlockUI(block.ContainerGO, block.UINode);
            }

            // 3. Re-evaluate root workspace layout bounds
            LayoutRebuilder.ForceRebuildLayoutImmediate(_workspaceContent.GetComponent<RectTransform>());
        }

        private int GetTransformDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        private void RebuildSingleBlockUI(GameObject container, UIBlockNode uiNode)
        {
            RectTransform rt = container.GetComponent<RectTransform>();

            // 1. RESCUE: Save nested blocks so they survive RebuildGrid clearing the container
            var oldDropZones = rt.GetComponentsInChildren<BlockDropZone>(true);
            Dictionary<string, List<Transform>> rescuedBlocks = new Dictionary<string, List<Transform>>();

            foreach (var zone in oldDropZones)
            {
                string zoneKey = zone.gameObject.name;
                List<Transform> blocksToSave = new List<Transform>();

                // Only grab direct child blocks
                foreach (Transform child in zone.transform)
                {
                    if (child.GetComponent<VisualBlockComponent>() != null)
                    {
                        blocksToSave.Add(child);
                    }
                }

                // Park them safely at the root workspace content
                foreach (var b in blocksToSave)
                {
                    b.SetParent(_workspaceContent, false);
                }

                if (blocksToSave.Count > 0)
                {
                    rescuedBlocks[zoneKey] = blocksToSave;
                }
            }

            // 2. REBUILD: Generates new layout, destroys old UI (rescued blocks are safe)
            GridReferences refs = uiGenerator.RebuildGrid(rt, uiNode.GetRowSpecs(), useMargins: false);

            // 3. RESTORE: Put the rescued blocks back into the newly generated DropZones
            foreach (var kvp in rescuedBlocks)
            {
                if (refs.DropZones.TryGetValue(kvp.Key, out BlockDropZone newZone))
                {
                    foreach (var b in kvp.Value)
                    {
                        b.SetParent(newZone.transform, false);
                    }
                }
                else
                {
                    // Fallback dump to main workspace if drop zone was removed
                    foreach (var b in kvp.Value)
                    {
                        b.SetParent(_workspaceContent, false);
                    }
                }
            }

            // 4. BIND & RESTORE: Re-apply background themes, cache row visibilities, and restore variables
            uiNode.BindUI(rt, refs);
            uiNode.RestoreState(rt, refs);
        }

        private void MoveBlock(GameObject containerGo, int direction)
        {
            int currentIndex = containerGo.transform.GetSiblingIndex();
            int newIndex = Mathf.Clamp(currentIndex + direction, 0, _workspaceContent.childCount - 1);
            containerGo.transform.SetSiblingIndex(newIndex);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_workspaceContent.GetComponent<RectTransform>());
        }

        private void LoadModFromClipboard()
        {
            string clipboardContent = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboardContent))
            {
                Debug.LogWarning("System clipboard is empty.");
                return;
            }
            Debug.Log("Decompilation to Blocks logic goes here.");
        }

        private void ClearWorkspace()
        {
            if (_workspaceContent == null) return;

            foreach (var instance in _activeUIBlocks)
            {
                if (instance.ContainerGO != null) Destroy(instance.ContainerGO);
            }
            _activeUIBlocks.Clear();
        }

        private void CompileBlocks()
        {
            if (_rootWorkspaceZone == null || _rootWorkspaceZone.transform.childCount == 0)
            {
                Debug.Log("Workspace is empty.");
                return;
            }

            List<ITextmodNode> rootNodes = new List<ITextmodNode>();

            // 1. Crawl the visual UI hierarchy top-down
            foreach (Transform child in _rootWorkspaceZone.transform)
            {
                VisualBlockComponent visualBlock = child.GetComponent<VisualBlockComponent>();
                if (visualBlock != null && visualBlock.UI_Node != null)
                {
                    // Tell the node to read its visual children and update its internal compiler model
                    GridReferences refs = child.GetComponentInChildren<GridReferences>(true); // Assuming refs are stored or fetchable
                    visualBlock.UI_Node.CompileRecursive(refs);

                    rootNodes.Add(visualBlock.UI_Node.NodeModel);
                }
            }

            // 2. Bundle all root level blocks into a global Comma Chain (Top-level textmod standard)
            CommaChainBlock rootChain = new CommaChainBlock { Nodes = rootNodes };
            string compiledOutput = rootChain.Compile();

            if (!string.IsNullOrEmpty(compiledOutput))
            {
                GUIUtility.systemCopyBuffer = compiledOutput;
                Debug.Log($"Compiled output copied to clipboard:\n{compiledOutput}");
            }
        }

        private struct BlockInstance
        {
            public GameObject ContainerGO;
            public ITextmodNode CompilerNode;
            public UIBlockNode UINode;

            public BlockInstance(GameObject container, ITextmodNode compilerNode, UIBlockNode uiNode)
            {
                ContainerGO = container;
                CompilerNode = compilerNode;
                UINode = uiNode;
            }
        }
    }
}