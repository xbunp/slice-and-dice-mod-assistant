using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TextPair : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_Text output;

    private bool isProcessing = false;
    private bool update = false;
    private string translatedText;

    public void ReadString(string thisString)
    {
        // If we are already processing an update, ignore incoming events to prevent recursion
        if (isProcessing) return;

        isProcessing = true;

        try
        {
            translatedText = TextmodTranslator.Translate(thisString);
            update = true;
        }
        finally
        {
            isProcessing = false;
        }
    }

    private void Update()
    {
        if (update)
        {
            update = false;
            output.text = translatedText;
            Debug.Log($"{translatedText}");
        }
    }

    public void ProcessClipboardText()
    {
        // Retrieve the text currently held in the system clipboard
        string clipboardText = GUIUtility.systemCopyBuffer;

        if (string.IsNullOrEmpty(clipboardText))
        {
            Debug.LogWarning("Clipboard is empty.");
            return;
        }

        // Optional: Update the input field UI so the user can see what was pasted
        if (inputField != null)
        {
            inputField.text = clipboardText;
        }

        try
        {
            // Translate the text
            string translatedText = TextmodTranslator.Translate(clipboardText);

            // Update the output UI
            if (output != null)
            {
                output.text = translatedText;
            }

            Debug.Log(translatedText);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during translation: {ex.Message}");
        }
    }
}