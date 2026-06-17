using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public static class StringCleanerUtility
{
    [MenuItem("Assets/Clean Base64 Images in Text", false, 100)]
    public static void CleanSelectedTextFile()
    {
        Object selectedObject = Selection.activeObject;
        if (selectedObject == null)
        {
            Debug.LogWarning("Please select a file first.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".txt"))
        {
            Debug.LogWarning("The selected file is not a valid .txt file.");
            return;
        }

        string absolutePath = Path.GetFullPath(assetPath);
        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"File path not found: {absolutePath}");
            return;
        }

        try
        {
            string content = File.ReadAllText(absolutePath);

            // Matches "img." followed by any character that is not a literal dot, 
            // stopping right before the next dot.
            string pattern = @"img\.[^.]+";
            string replacement = "img.(customImg)";

            string cleanedContent = Regex.Replace(content, pattern, replacement);

            if (content == cleanedContent)
            {
                Debug.Log("No base64 image strings matching the pattern were found. No changes made.");
                return;
            }

            // Write changes back to the text file
            File.WriteAllText(absolutePath, cleanedContent);

            // Forces Unity to refresh and serialize the modified file
            AssetDatabase.ImportAsset(assetPath);

            Debug.Log($"File cleaning completed and saved: {assetPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"An error occurred while cleaning the file: {ex.Message}");
        }
    }

    // Validation method to ensure the menu option is only active when a .txt file is selected
    [MenuItem("Assets/Clean Base64 Images in Text", true)]
    public static bool ValidateCleanSelectedTextFile()
    {
        Object selectedObject = Selection.activeObject;
        if (selectedObject == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(selectedObject);
        return !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".txt");
    }
}