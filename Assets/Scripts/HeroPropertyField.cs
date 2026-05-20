using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HeroPropertyField : MonoBehaviour
{
    public TextMeshProUGUI titleLabel;
    public TMP_InputField textmodInput;  // The editable syntax field (e.g., "n.Gible")

    [Header("Assign ONE of these based on Prefab type")]
    public TMP_InputField guiInput;
    public TMP_Dropdown guiDropdown;

    private bool isSyncing = false;

    // --- SETUP FOR STANDARD TEXT INPUT ---
    public void SetupInput(string title, string initialGuiVal, string initialTextmodVal, Action<string> onGuiChanged, Action<string> onTextmodChanged)
    {
        titleLabel.text = title;
        UpdateInputValuesWithoutNotify(initialGuiVal, initialTextmodVal);

        guiInput.onValueChanged.AddListener(val =>
        {
            if (isSyncing) return;
            onGuiChanged?.Invoke(val); // Let the Manager update the data model and the Textmod string
        });

        textmodInput.onValueChanged.AddListener(val =>
        {
            if (isSyncing) return;
            onTextmodChanged?.Invoke(val); // Let the Manager parse the string, update the model, and update the GUI
        });
    }

    // --- SETUP FOR DROPDOWNS ---
    public void SetupDropdown(string title, List<string> options, int initialIndex, string initialTextmodVal, Action<int> onGuiChanged, Action<string> onTextmodChanged)
    {
        titleLabel.text = title;
        guiDropdown.ClearOptions();
        guiDropdown.AddOptions(options);

        UpdateDropdownValuesWithoutNotify(initialIndex, initialTextmodVal);

        guiDropdown.onValueChanged.AddListener(val =>
        {
            if (isSyncing) return;
            onGuiChanged?.Invoke(val);
        });

        textmodInput.onValueChanged.AddListener(val =>
        {
            if (isSyncing) return;
            onTextmodChanged?.Invoke(val);
        });
    }

    // --- VALUE UPDATERS (Called by Manager when Data Model changes) ---
    public void UpdateInputValuesWithoutNotify(string guiVal, string textmodVal)
    {
        isSyncing = true;
        guiInput.text = guiVal;
        textmodInput.text = textmodVal;
        isSyncing = false;
    }

    public void UpdateDropdownValuesWithoutNotify(int guiIndex, string textmodVal)
    {
        isSyncing = true;
        guiDropdown.value = guiIndex;
        textmodInput.text = textmodVal;
        isSyncing = false;
    }
}