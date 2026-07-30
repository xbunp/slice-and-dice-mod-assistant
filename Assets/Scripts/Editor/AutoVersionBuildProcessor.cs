#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Automatically increments the last digit of PlayerSettings.bundleVersion 
/// right before creating any Unity build.
/// </summary>
public class AutoVersionBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string currentVersion = PlayerSettings.bundleVersion;

        if (string.IsNullOrEmpty(currentVersion))
        {
            currentVersion = "0.5.6";
        }

        string[] parts = currentVersion.Split('.');

        // Increment the last digit (e.g., 0.5.6 -> 0.5.7)
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int lastNumber))
        {
            parts[parts.Length - 1] = (lastNumber + 1).ToString();
            string newVersion = string.Join(".", parts);

            PlayerSettings.bundleVersion = newVersion;
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=#4A9EFF>[AutoVersion]</color> Automatically incremented build version to <b>v{newVersion}</b>");
        }
        else
        {
            Debug.LogWarning($"[AutoVersion] Could not parse version format '{currentVersion}'. Ensure it ends in a number (e.g., '0.5.6').");
        }
    }
}
#endif