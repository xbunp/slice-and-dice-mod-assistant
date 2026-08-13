using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

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

            // Pattern 1: Matches "img." followed by valid identifier characters (stops at spaces, dots, or brackets)
            string pattern1 = @"img\.[^\s.\[\]]+";
            string replacement1 = "img.(customImg)";

            // Pattern 2: Matches long payload strings inside square brackets [].
            // Uses [^\]]{20,} to support URL-encoded/percent-encoded characters (like '%'),
            // while ignoring short rich text formatting tags (e.g., [b], [color=#fff]).
            string pattern2 = @"\[(?:data:image\/[a-zA-Z]+;base64,)?[^\]]{20,}\]";
            string replacement2 = "[(customImg)]";

            // Apply both clean-up patterns
            string cleanedContent = Regex.Replace(content, pattern1, replacement1);
            cleanedContent = Regex.Replace(cleanedContent, pattern2, replacement2);

            if (content == cleanedContent)
            {
                Debug.Log("No base64 image strings matching the patterns were found. No changes made.");
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