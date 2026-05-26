using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class ClipboardManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);
#endif

    private static ClipboardReceiver _receiverInstance;
    private const string PermissionPrefKey = "WebGLClipboardPermissionShown";
    /// <summary>
    /// Unified entrypoint for all copy/paste operations across all platforms.
    /// </summary>
    /// <param name="uiGenerator">Reference to generator to show popups. If null, will attempt to find one in the scene.</param>
    /// <param name="onPasteSuccess">Callback invoked with the retrieved clipboard text.</param>
    public static void RequestPaste(FullScreenUIGenerator uiGenerator, Action<string> onPasteSuccess)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        EnsureReceiverExists();
        _receiverInstance.OnClipboardTextReceived = onPasteSuccess;

        // 1. Declare and check the permission flag first
        bool hasShownPrompt = PlayerPrefs.GetInt(PermissionPrefKey, 0) == 1;

        if (!hasShownPrompt)
        {
            if (uiGenerator == null)
            {
                uiGenerator = UnityEngine.Object.FindObjectOfType<FullScreenUIGenerator>();
            }

            if (uiGenerator != null)
            {
                // Rich-text formatted message pointing the user to the browser's native prompt location
                string msg = "<color=#FFCC00><b><size=130%>CLIPBOARD ACCESS REQUIRED</size></b></color>\n\n" +
                             "To paste external data, your browser will now ask for clipboard permission.\n\n" +
                             "Please look at the <b><color=white>TOP-LEFT CORNER</color></b> of your browser " +
                             "(near the address bar) and click <b><color=#00FF00>'Allow'</color></b> to proceed.";
                
                uiGenerator.CreatePopup(msg, true, () =>
                {
                    PlayerPrefs.SetInt(PermissionPrefKey, 1);
                    PlayerPrefs.Save();
                    
                    // Trigger the native browser request synchronously on button dismiss
                    ReadOSClipboard(_receiverInstance.gameObject.name, "OnClipboardDataReceived");
                });
                return;
            }
        }

        // 2. Request directly if already approved
        ReadOSClipboard(_receiverInstance.gameObject.name, "OnClipboardDataReceived");
#else
        // Instant synchronous fallback for Editor and Standalone platforms
        string clipboard = GUIUtility.systemCopyBuffer;
        onPasteSuccess?.Invoke(clipboard);
#endif
    }

    private static void EnsureReceiverExists()
    {
        if (_receiverInstance == null)
        {
            GameObject go = new GameObject("WebGLClipboardReceiver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _receiverInstance = go.AddComponent<ClipboardReceiver>();
        }
    }

    /// <summary>
    /// Internal MonoBehaviour surrogate to receive SendMessage callbacks from browser Javascript.
    /// </summary>
    private class ClipboardReceiver : MonoBehaviour
    {
        public Action<string> OnClipboardTextReceived;

        public void OnClipboardDataReceived(string text)
        {
            OnClipboardTextReceived?.Invoke(text);
        }
    }
}