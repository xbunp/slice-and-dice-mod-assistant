#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PrefabSanitizer : EditorWindow
{
    [MenuItem("Tools/Sanitize UI Prefab")]
    public static void SanitizeSelectedPrefab()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("Sanitizer: Please select the PortraitPanel prefab asset in your Project window first.");
            return;
        }

        // Use AssetDatabase.GetAssetPath to find the file path of the selected prefab
        string prefabPath = AssetDatabase.GetAssetPath(selected);
        GameObject rootToProcess = selected;
        bool isPrefabAsset = !string.IsNullOrEmpty(prefabPath) && Selection.activeTransform == null;

        if (isPrefabAsset)
        {
            // Open and edit the prefab asset directly in memory
            rootToProcess = PrefabUtility.LoadPrefabContents(prefabPath);
        }

        Undo.RegisterCompleteObjectUndo(rootToProcess, "Sanitize UI Prefab");

        RectTransform rootRt = rootToProcess.GetComponent<RectTransform>();
        if (rootRt == null)
        {
            Debug.LogError("Sanitizer: Selected object does not have a RectTransform. It must be a UI element.");
            if (isPrefabAsset) PrefabUtility.UnloadPrefabContents(rootToProcess);
            return;
        }

        // --- STEP 1: Freeze Root Dimensions ---
        // Grab whatever absolute pixel size the UI currently has in the editor
        float currentWidth = rootRt.rect.width;
        float currentHeight = rootRt.rect.height;

        // Fallback to safe defaults if the size is collapsed or uninitialized
        if (currentWidth <= 0.1f) currentWidth = rootRt.sizeDelta.x > 0 ? rootRt.sizeDelta.x : 350f;
        if (currentHeight <= 0.1f) currentHeight = rootRt.sizeDelta.y > 0 ? rootRt.sizeDelta.y : 240f;

        // Force root anchors to Center-Center to completely block parent stretch-scaling
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        // Bake the actual width and height into sizeDelta
        rootRt.sizeDelta = new Vector2(currentWidth, currentHeight);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.localScale = Vector3.one;
        rootRt.localRotation = Quaternion.identity;

        // --- STEP 2: Sanitize Children Recursively ---
        int sanitizedCount = 0;
        SanitizeChildrenRecursive(rootRt, ref sanitizedCount);

        // --- STEP 3: Save Changes ---
        if (isPrefabAsset)
        {
            PrefabUtility.SaveAsPrefabAsset(rootToProcess, prefabPath);
            PrefabUtility.UnloadPrefabContents(rootToProcess);
            Debug.Log($"Sanitizer: Successfully frozen and saved prefab asset: {prefabPath}. Normalized {sanitizedCount} child properties.");
        }
        else
        {
            EditorUtility.SetDirty(rootToProcess);
            Debug.Log($"Sanitizer: Successfully sanitized hierarchy instance: {rootToProcess.name}. Normalized {sanitizedCount} child properties.");
        }
    }

    private static void SanitizeChildrenRecursive(RectTransform parent, ref int count)
    {
        foreach (Transform child in parent)
        {
            RectTransform childRt = child.GetComponent<RectTransform>();
            if (childRt != null)
            {
                Undo.RecordObject(childRt, "Sanitize Child RectTransform");

                // 1. Force scale back to exactly 1 to fix nested distortion
                if (childRt.localScale != Vector3.one)
                {
                    childRt.localScale = Vector3.one;
                    count++;
                }

                // 2. Clear out off-axis Z coordinates that cause clipping or render failures
                if (childRt.localPosition.z != 0f)
                {
                    Vector3 pos = childRt.localPosition;
                    pos.z = 0f;
                    childRt.localPosition = pos;
                    count++;
                }

                // 3. Clear unintentional rotation drift
                if (Quaternion.Angle(childRt.localRotation, Quaternion.identity) > 0f &&
                    Quaternion.Angle(childRt.localRotation, Quaternion.identity) < 1.0f)
                {
                    childRt.localRotation = Quaternion.identity;
                    count++;
                }

                SanitizeChildrenRecursive(childRt, ref count);
            }
        }
    }
}
#endif