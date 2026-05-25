using UnityEngine;

public static class ClipboardHelper
{
    public static void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

    public static string GetFromClipboard()
    {
        return GUIUtility.systemCopyBuffer;
    }
}