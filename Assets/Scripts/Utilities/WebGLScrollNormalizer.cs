#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;

// 1. Ensures the processor is registered in the Editor automatically
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public class WebGLScrollNormalizer : InputProcessor<Vector2>
{
    // WebGL typically reports +/- 1. Desktop reports +/- 120. 
    // We multiply WebGL by 120 so it feels exactly identical to Desktop.
    public float webglMultiplier = 120f;

    // 2. Ensures the processor is registered in the built game automatically
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        // Explicitly naming the processor can prevent mapping issues in the UI
        InputSystem.RegisterProcessor<WebGLScrollNormalizer>("WebGLScrollNormalizer");
    }

    // 3. The actual hardware interception logic
    public override Vector2 Process(Vector2 value, InputControl control)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // If we are in the browser, scale the tiny scroll delta up to match desktop
        return value * webglMultiplier;
#else
        // If we are in the Editor or Desktop build, leave the raw input alone
        return value;
#endif
    }
}