using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class RootUI : MonoBehaviour
{
    protected GeneratedScreen generatedScreen;
    public FullScreenUIGenerator uiGenerator { get; protected set; }

    public virtual void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        if (uiGeneratorRef == null)
        {
            Debug.LogError("No UI Generator defined", this);
            return;
        }
        uiGenerator = uiGeneratorRef;

        // 1. Attempt to build the subclass UI
        BuildUIAndBind();

        // 2. If the subclass is a placeholder (did not assign generatedScreen),
        //    generate a safe fallback placeholder UI.
        if (generatedScreen == null)
        {
            BuildPlaceholderUI();
        }
    }

    protected virtual void BuildUIAndBind()
    {
        // Left empty for subclasses to override
    }

    /// <summary>
    /// Generates a simple text layout indicating the panel is a placeholder.
    /// </summary>
    private void BuildPlaceholderUI()
    {
        string uiName = GetType().Name;

        List<GridRowSpec> rows = new List<GridRowSpec>
        {
            new GridRowSpec(
                GridCellSpec.CreateLabel($"{uiName}_Placeholder_Txt", $"[ {uiName} ] Under Construction", 1.0f)
            )
        };

        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            new ColumnSpec($"{uiName}_Placeholder_Col", 0f, 1.0f, rows)
        };

        generatedScreen = uiGenerator.SetupScreen(columns, false);
    }

    public RectTransform GetRootWrapper()
    {
        if (generatedScreen != null)
        {
            return generatedScreen.RootWrapper;
        }

        // Return a warning instead of a hard error to prevent breaking execution flow
        Debug.LogWarning($"UI component '{GetType().Name}' failed to generate a root wrapper.", this);
        return null;
    }

    // Generic builder for complex objects (like Abilities)
    protected void AppendCollectionSelector<T>(
        List<GridRowSpec> layout,
        string label,
        string uniqueKey,
        IEnumerable<T> availableChoices,
        IEnumerable<string> currentActiveItems,
        Func<T, string> getKey,
        Func<T, string> getDisplay,
        Action<string> onAdd,
        Action<string> onRemove)
    {
        var choicesList = availableChoices?.ToList() ?? new List<T>();

        List<string> displayOptions = new List<string> { "" };
        foreach (var choice in choicesList)
        {
            displayOptions.Add(getDisplay?.Invoke(choice) ?? "");
        }

        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"{uniqueKey}_Lbl", label, 0.30f),
            GridCellSpec.CreateFilteredDropdown($"{uniqueKey}_Drop", "", 0.70f, displayOptions.ToArray(), (selectedIndex) =>
            {
                if (selectedIndex > 0 && selectedIndex <= choicesList.Count)
                {
                    var chosenItem = choicesList[selectedIndex - 1];
                    onAdd?.Invoke(getKey?.Invoke(chosenItem));
                }
            })
        ));

        if (currentActiveItems != null)
        {
            foreach (var item in currentActiveItems)
            {
                string capturedItem = item;
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"Tag_{uniqueKey}_{capturedItem}", capturedItem, 0.80f),
                    GridCellSpec.CreateButton($"Del_{uniqueKey}_{capturedItem}", "[X]", 0.20f, () => onRemove?.Invoke(capturedItem))
                ));
            }
        }
    }

    protected void AppendCollectionSelector(
        List<GridRowSpec> layout,
        string label,
        string uniqueKey,
        IEnumerable<string> availableChoices,
        IEnumerable<string> currentActiveItems,
        Action<string> onAdd,
        Action<string> onRemove)
    {
        AppendCollectionSelector<string>(
            layout,
            label,
            uniqueKey,
            availableChoices,
            currentActiveItems,
            getKey: (val) => val,
            getDisplay: (val) => val,
            onAdd,
            onRemove
        );
    }


}