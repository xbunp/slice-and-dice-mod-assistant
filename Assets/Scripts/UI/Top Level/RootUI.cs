using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class RootUI : MonoBehaviour
{
    protected GeneratedScreen generatedScreen;
    protected FullScreenUIGenerator uiGenerator;

    // Base implementation handles assignment safely
    public virtual void Initialize(FullScreenUIGenerator uiGeneratorRef)
    {
        if (uiGeneratorRef == null)
        {
            Debug.LogError("No UI Generator defined", this);
            return;
        }
        uiGenerator = uiGeneratorRef;
        BuildUIAndBind();
    }

    protected virtual void BuildUIAndBind()
    {

    }

    public RectTransform GetRootWrapper()
    {
        if (generatedScreen != null)
        {
            return generatedScreen.RootWrapper;
        }
        Debug.LogError("UI has no root wrapper!!", this);
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
        // Cache choices to list to prevent multiple enumerations and allow index lookup
        var choicesList = availableChoices?.ToList() ?? new List<T>();

        // 1. Prepare options with an empty placeholder at index 0
        List<string> displayOptions = new List<string> { "" };
        foreach (var choice in choicesList)
        {
            displayOptions.Add(getDisplay?.Invoke(choice) ?? "");
        }

        // 2. Add the dropdown row
        layout.Add(new GridRowSpec(
            GridCellSpec.CreateLabel($"{uniqueKey}_Lbl", label, 0.30f),
            GridCellSpec.CreateFilteredDropdown($"{uniqueKey}_Drop", "", 0.70f, displayOptions.ToArray(), (selectedIndex) =>
            {
                if (selectedIndex > 0 && selectedIndex <= choicesList.Count)
                {
                    // Map index back to the cached list and extract the data key
                    var chosenItem = choicesList[selectedIndex - 1];
                    onAdd?.Invoke(getKey?.Invoke(chosenItem));
                }
            })
        ));

        // 3. Add rows for each currently active item
        if (currentActiveItems != null)
        {
            foreach (var item in currentActiveItems)
            {
                string capturedItem = item; // Capture for lambda scope
                layout.Add(new GridRowSpec(
                    GridCellSpec.CreateLabel($"Tag_{uniqueKey}_{capturedItem}", capturedItem, 0.80f),
                    GridCellSpec.CreateButton($"Del_{uniqueKey}_{capturedItem}", "[X]", 0.20f, () => onRemove?.Invoke(capturedItem))
                ));
            }
        }
    }

    // Overload for simple string lists (like Keywords) where key and display are identical
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