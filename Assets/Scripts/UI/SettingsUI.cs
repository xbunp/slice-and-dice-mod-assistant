using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class SettingsUI : RootUI
{
    // Redirect directly to the static properties on the SettingsManager
    public int CHARS_PER_FRAME
    {
        get => SettingsManager.CHARS_PER_FRAME;
        set => SettingsManager.CHARS_PER_FRAME = value;
    }

    public int REGEX_MATCHES_PER_FRAME
    {
        get => SettingsManager.REGEX_MATCHES_PER_FRAME;
        set => SettingsManager.REGEX_MATCHES_PER_FRAME = value;
    }

    protected override void BuildUIAndBind()
    {
        List<GridRowSpec> rows = new List<GridRowSpec>();

        // Header Title
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("SettingsHeader", "APPLICATION CONFIGURATION", 1.0f)
        ));

        // Settings Entry 1: Chars Per Frame
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("CharsLabel", "Text-Parsing Speed (Chars/Frame)", 0.5f),
            GridCellSpec.CreateInput("CharsInput", CHARS_PER_FRAME.ToString(), 0.5f, OnCharsInputChanged)
        ));

        // Settings Entry 2: Regex Matches Per Frame
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateLabel("RegexLabel", "Regex Matches Per Frame", 0.5f),
            GridCellSpec.CreateInput("RegexInput", REGEX_MATCHES_PER_FRAME.ToString(), 0.5f, OnRegexInputChanged)
        ));

        // Control Buttons
        rows.Add(new GridRowSpec(
            GridCellSpec.CreateButton("SaveButton", "Save Changes", 0.5f, SaveAndApply),
            GridCellSpec.CreateButton("ResetButton", "Reset Defaults", 0.5f, ResetToDefaults)
        ));

        // Define a centered layout column taking up 50% of the screen width (from 0.25 to 0.75)
        List<ColumnSpec> columns = new List<ColumnSpec>
        {
            new ColumnSpec("SettingsColumn", 0.25f, 0.75f, rows)
        };

        // Instantiates the elements inside the canvas wrapper
        generatedScreen = uiGenerator.SetupScreen(columns, useMargins: true);

        ConfigureInputValidation();
    }

    /// <summary>
    /// Updates the input fields to restrict formatting to integers.
    /// </summary>
    private void ConfigureInputValidation()
    {
        if (generatedScreen == null) return;

        if (generatedScreen.ColumnRefs.TryGetValue("SettingsColumn", out var refs))
        {
            if (refs.Inputs.TryGetValue("CharsInput", out var charsInput))
            {
                charsInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                charsInput.text = CHARS_PER_FRAME.ToString();
            }

            if (refs.Inputs.TryGetValue("RegexInput", out var regexInput))
            {
                regexInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                regexInput.text = REGEX_MATCHES_PER_FRAME.ToString();
            }
        }
    }

    private void OnCharsInputChanged(string val)
    {
        if (int.TryParse(val, out int parsedVal))
        {
            CHARS_PER_FRAME = parsedVal;
        }
    }

    private void OnRegexInputChanged(string val)
    {
        if (int.TryParse(val, out int parsedVal))
        {
            REGEX_MATCHES_PER_FRAME = parsedVal;
        }
    }

    private void SaveAndApply()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SaveSettings();
            uiGenerator.CreatePopup("Settings saved successfully.", true);
        }
    }

    private void ResetToDefaults()
    {
        CHARS_PER_FRAME = 10000;
        REGEX_MATCHES_PER_FRAME = 150;

        // Synchronize UI representations back to defaults
        if (generatedScreen != null && generatedScreen.ColumnRefs.TryGetValue("SettingsColumn", out var refs))
        {
            if (refs.Inputs.TryGetValue("CharsInput", out var charsInput))
            {
                charsInput.text = CHARS_PER_FRAME.ToString();
            }
            if (refs.Inputs.TryGetValue("RegexInput", out var regexInput))
            {
                regexInput.text = REGEX_MATCHES_PER_FRAME.ToString();
            }
        }
    }
}