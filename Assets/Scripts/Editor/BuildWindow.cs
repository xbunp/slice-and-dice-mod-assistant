using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BuildWindow : EditorWindow
{
    private string targetVersion;
    private bool initialized;

    [MenuItem("Tools/Build Window")]
    public static void ShowWindow()
    {
        BuildWindow window = GetWindow<BuildWindow>("Build Manager");
        window.minSize = new Vector2(350, 250);
        window.Show();
    }

    private void OnEnable()
    {
        // Load the current version from PlayerSettings
        targetVersion = PlayerSettings.bundleVersion;
        if (string.IsNullOrEmpty(targetVersion))
        {
            targetVersion = "0.1.0";
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Build Configurator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Display current settings
        EditorGUILayout.LabelField("Current Project Version:", PlayerSettings.bundleVersion);

        // Editable target version
        targetVersion = EditorGUILayout.TextField("Target Build Version:", targetVersion);

        // Buttons to manually increment version numbers
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Major (1.0.0)"))
        {
            IncrementVersion(major: true);
        }
        if (GUILayout.Button("+ Minor (0.1.0)"))
        {
            IncrementVersion(minor: true);
        }
        if (GUILayout.Button("+ Patch (0.0.1)"))
        {
            IncrementVersion(patch: true);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);

        // Preview the output path
        string buildFolderName = $"{targetVersion} WebGL";
        string relativePath = Path.Combine("Builds", buildFolderName);
        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);

        EditorGUILayout.LabelField("Output Folder:", relativePath, EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space(20);

        // Build button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Perform WebGL Build", GUILayout.Height(40)))
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                bool switchTarget = EditorUtility.DisplayDialog(
                    "Switch Build Target?",
                    "Your active build target is not WebGL. Would you like to switch to WebGL and build?",
                    "Yes, Switch and Build",
                    "Cancel"
                );

                if (switchTarget)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                    ExecuteWebGLBuild(absolutePath, targetVersion);
                }
            }
            else
            {
                ExecuteWebGLBuild(absolutePath, targetVersion);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void IncrementVersion(bool major = false, bool minor = false, bool patch = false)
    {
        string[] parts = targetVersion.Split('.');
        int majorVal = 0, minorVal = 0, patchVal = 0;

        if (parts.Length > 0) int.TryParse(parts[0], out majorVal);
        if (parts.Length > 1) int.TryParse(parts[1], out minorVal);
        if (parts.Length > 2) int.TryParse(parts[2], out patchVal);

        if (major)
        {
            majorVal++;
            minorVal = 0;
            patchVal = 0;
        }
        else if (minor)
        {
            minorVal++;
            patchVal = 0;
        }
        else if (patch)
        {
            patchVal++;
        }

        targetVersion = $"{majorVal}.{minorVal}.{patchVal}";
    }

    private void ExecuteWebGLBuild(string targetDirectory, string version)
    {
        // Update the PlayerSettings version to match the chosen version
        PlayerSettings.bundleVersion = version;

        // Ensure the directory exists
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // Gather active scenes
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("Build failed: No scenes are active in Build Settings.");
            return;
        }

        // Set build parameters
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = targetDirectory, // WebGL takes a folder path, not a file path
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"Starting WebGL Build for version {version} to: {targetDirectory}");
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"WebGL Build succeeded and saved to {targetDirectory}");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
        }
    }
}