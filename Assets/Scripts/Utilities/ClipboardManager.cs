using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class ClipboardManager
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);

    [DllImport("__Internal")]
    private static extern void InitializeNativePasteListener(string objectName, string methodName);

    [DllImport("__Internal")]
    private static extern bool IsInsideIframe();
#endif

    private static ClipboardReceiver _receiverInstance;
    private const string PermissionPrefKey = "WebGLClipboardPermissionShown";

    /// <summary>
    /// Unified entrypoint for all copy/paste operations across all platforms.
    /// </summary>
    public static void RequestPaste(FullScreenUIGenerator uiGenerator, Action<string> onPasteSuccess)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        EnsureReceiverExists();
        _receiverInstance.OnClipboardTextReceived = onPasteSuccess;

        // 1. Check if we are inside an iframe (e.g. itch.io)
        if (IsInsideIframe())
        {
            if (uiGenerator == null)
            {
                uiGenerator = UnityEngine.Object.FindObjectOfType<FullScreenUIGenerator>();
            }

            if (uiGenerator != null)
            {
                // Inform the user that the button is blocked by the browser, 
                // but they can use Ctrl+V / Cmd+V directly.
                string iframeMsg = "<color=#FFCC00><b><size=130%>DIRECT PASTE RESTRICTED</size></b></color>\n\n" +
                                   "Your browser blocks direct paste buttons inside web frames (like itch.io).\n\n" +
                                   "Please use <b><color=#00FF00>Ctrl + V</color></b> (or <b><color=#00FF00>Cmd + V</color></b> on Mac) on your keyboard to paste.";
                
                uiGenerator.CreatePopup(iframeMsg, true, null);
            }
            return;
        }

        // 2. Normal flow for top-level WebGL pages (Direct URL)
        bool hasShownPrompt = PlayerPrefs.GetInt(PermissionPrefKey, 0) == 1;

        if (!hasShownPrompt)
        {
            if (uiGenerator == null)
            {
                uiGenerator = UnityEngine.Object.FindObjectOfType<FullScreenUIGenerator>();
            }

            if (uiGenerator != null)
            {
                string msg = "<color=#FFCC00><b><size=130%>CLIPBOARD ACCESS REQUIRED</size></b></color>\n\n" +
                             "To paste external data, your browser will now ask for clipboard permission.\n\n" +
                             "Please look at the <b><color=white>TOP-LEFT CORNER</color></b> of your browser " +
                             "(near the address bar) and click <b><color=#00FF00>'Allow'</color></b> to proceed.";
                
                uiGenerator.CreatePopup(msg, true, () =>
                {
                    PlayerPrefs.SetInt(PermissionPrefKey, 1);
                    PlayerPrefs.Save();
                    
                    ReadOSClipboard(_receiverInstance.gameObject.name, "OnClipboardDataReceived");
                });
                return;
            }
        }

        // Request directly if already approved
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

#if UNITY_WEBGL && !UNITY_EDITOR
            // Initialize the global paste listener as soon as the manager is used
            InitializeNativePasteListener(go.name, "OnClipboardDataReceived");
#endif
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
            if (OnClipboardTextReceived != null)
            {
                OnClipboardTextReceived.Invoke(text);

                // Clear the action callback after a successful paste to prevent 
                // accidental triggers from future global Ctrl+V presses.
                OnClipboardTextReceived = null;
            }
        }
    }

    public static void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLCopyAndPaste.WebGLCopyAndPasteAPI.CopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }
}