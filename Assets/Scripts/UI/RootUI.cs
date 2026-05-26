using System.Collections;
using System.Collections.Generic;
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
}