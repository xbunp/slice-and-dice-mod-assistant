using SliceDiceTextMod;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FullModUI : RootUI
{
    
    private TMP_Dropdown directiveDropdown;
    private ScrollRect directiveScrollView;
    private List<DirectiveUI> directiveUIs = new List<DirectiveUI>();
    /*
    protected override void BuildUIAndBind()
    {
        var columns = new List<ColumnSpec>
        {
            new ColumnSpec("Left_Column", 0.0f, 0.49f, new List<GridRowSpec>
            {
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("LoadModBtn", "Load Mod from Clipboard", 1.0f, OnLoadModClicked)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateDropdown("ModDropdown", "", 1.0f, GetDropdownOptions(), OnDropdownSelected))
            }),
            new ColumnSpec("Right_Column", 0.51f, 1.0f, new List<GridRowSpec>
            {
                new GridRowSpec(800f, GridCellSpec.CreateScrollView("ModScrollView", 1.0f)),
                new GridRowSpec(uiGenerator.rowHeight, GridCellSpec.CreateButton("CopyModBtn", "Copy Mod to Clipboard", 1.0f, OnCopyModClicked))
            })
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);

        directiveDropdown = generatedScreen.ColumnRefs["Left_Column"].Dropdowns["ModDropdown"];
        directiveScrollView = generatedScreen.ColumnRefs["Right_Column"].ScrollViews["ModScrollView"];

        // Connect to the Singleton facade rather than directly subscribing to ModData
        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged += OnStateChanged;
            OnStateChanged(null);
        }
    }
    */
    private void OnDestroy()
    {
        if (ModPackage.Instance != null)
        {
            ModPackage.Instance.OnModDataChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(object sender)
    {
        if (object.ReferenceEquals(sender, this)) return;

        directiveUIs.Clear();

        // Fetch raw elements using the read-only getter GetDirectives()
        var directives = ModPackage.Instance.loadedMod.GetDirectives();

        foreach (var directive in directives)
        {
            var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => directive.GetType() == e.CreateData().GetType());
            if (entry != null)
            {
                // Fetch or generate a secure, isolated clone of this directive
                var workingClone = ModPackage.Instance.GetOrCreateDirectiveSession(directive);

                System.Action removeAction = () =>
                {
                    // Safe deletion through the Singleton
                    ModPackage.Instance.DeleteDirective(directive);
                };

                // Pass the safe workingClone to the visual UI builders
                directiveUIs.Add(entry.CreateUI(workingClone, uiGenerator, () =>
                {
                    // Auto-save this specific directive instance to ModData on layout changes
                    ModPackage.Instance.SaveDirective(directive);
                    RebuildScrollView();
                }, removeAction));
            }
        }

        RebuildScrollView();
    }

    private void RebuildScrollView()
    {
        if (directiveScrollView == null || directiveScrollView.content == null) return;

        List<GridRowSpec> masterRows = new List<GridRowSpec>();
        foreach (var ui in directiveUIs)
        {
            masterRows.AddRange(ui.GetRowSpecs());
        }

        var refs = uiGenerator.RebuildGrid(directiveScrollView.content, masterRows, true);
        directiveScrollView.content.sizeDelta = new Vector2(0f, refs.TotalHeight);

        foreach (var ui in directiveUIs)
        {
            ui.RestoreState(refs);
        }
    }

    private void OnLoadModClicked()
    {
        string clipboard = GUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(clipboard)) return;

        // Parse using the loadedMod reference from the Singleton
        ModParser.ParseIntoContainer(clipboard, ModPackage.Instance.loadedMod);

        // Notify that the directive state has completely changed
        ModPackage.Instance.NotifyDirectiveSessionChanged(this);
    }

    private void OnCopyModClicked()
    {
        // Serialize using the clean loadedMod instance
        string output = ModPackage.Instance.ExportModToTextModString();

        if (string.IsNullOrEmpty(output))
        {
            Debug.LogError("No TextMod loaded to export!");
            return;
        }

        // Restore any compressed custom textures safely
        string rawText = ImageUtility.RestoreImages(output);

        GUIUtility.systemCopyBuffer = rawText;
        uiGenerator.CreatePopup("Mod copied to clipboard!", true, null);
    }

    private void OnDropdownSelected(int index)
    {
        if (index <= 0) return;
        string option = directiveDropdown.options[index].text;
        directiveDropdown.value = 0;

        var entry = SliceDiceTextMod.DirectiveRegistry.Entries.FirstOrDefault(e => e.DropdownName == option);
        if (entry != null)
        {
            // Add a new directive safely to the mod container
            ModPackage.Instance.loadedMod.SaveDirective(null, entry.CreateData());
            ModPackage.Instance.NotifyDirectiveSessionChanged(this);
        }
    }

    private string[] GetDropdownOptions()
    {
        var opts = new List<string> { "Add Directive..." };
        opts.AddRange(SliceDiceTextMod.DirectiveRegistry.Entries.Select(e => e.DropdownName));
        return opts.ToArray();
    }
}