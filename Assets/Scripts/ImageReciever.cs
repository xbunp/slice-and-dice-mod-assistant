using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class ImageReceiver : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void TriggerFileOpen();

    [Header("UI References")]
    //public RawImage originalImagePreview;
    public RawImage reducedImagePreview;
    public TMPro.TMP_Dropdown compressionDropdown;
    public TMPro.TMP_InputField outputStringField;

    private Texture2D _uploadedTexture;
    private Texture2D _generatedPreviewTexture;

    private void Start()
    {
        // 1. Auto-populate the dropdown
        if (compressionDropdown != null)
        {
            compressionDropdown.ClearOptions();
            var enumNames = new System.Collections.Generic.List<string>(Enum.GetNames(typeof(ImageColorExtractor.CompressionFactor)));
            compressionDropdown.AddOptions(enumNames);

            // 2. Attach listener to refresh the image when the user changes the dropdown
            compressionDropdown.onValueChanged.AddListener(delegate { ProcessCurrentTexture(); });
        }
        else
        {
            Debug.LogError("[Unity C#] Dropdown reference is missing!");
        }
    }

    public void OnSelectFileClicked()
    {
        Debug.Log("[Unity C#] Requesting File Dialog...");
#if UNITY_WEBGL && !UNITY_EDITOR
        TriggerFileOpen();
#else
        Debug.LogWarning("[Unity C#] File dialog is only supported in WebGL builds. Mocking a response not available here.");
#endif
    }

    // Called automatically by the ImageUploader.jslib
    public void OnImageLoaded(string base64String)
    {
        Debug.Log($"[Unity C#] SUCCESS: OnImageLoaded reached! Base64 Length: {base64String.Length}");

        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);

            if (_uploadedTexture != null) Destroy(_uploadedTexture);

            _uploadedTexture = new Texture2D(2, 2);
            if (_uploadedTexture.LoadImage(imageBytes))
            {
                Debug.Log($"[Unity C#] Texture created successfully. Size: {_uploadedTexture.width}x{_uploadedTexture.height}");

                /*
                if (originalImagePreview != null)
                    originalImagePreview.texture = _uploadedTexture;
                */

                ProcessCurrentTexture();
            }
            else
            {
                Debug.LogError("[Unity C#] Failed to convert byte array into Unity Texture2D.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Unity C#] Fatal Error decoding base64 image: " + ex.Message);
        }
    }

    private void ProcessCurrentTexture()
    {
        if (_uploadedTexture == null) return;

        Debug.Log("[Unity C#] Processing texture with extractor...");

        if (_generatedPreviewTexture != null)
        {
            Destroy(_generatedPreviewTexture);
            _generatedPreviewTexture = null;
        }

        ImageColorExtractor.CompressionFactor selectedCompression =
            (ImageColorExtractor.CompressionFactor)Enum.Parse(typeof(ImageColorExtractor.CompressionFactor), compressionDropdown.options[compressionDropdown.value].text);

        string encodedString = ImageColorExtractor.ExtractColors(_uploadedTexture, selectedCompression, out _generatedPreviewTexture);

        if (reducedImagePreview != null) reducedImagePreview.texture = _generatedPreviewTexture;
        if (outputStringField != null) outputStringField.text = encodedString;

        Debug.Log("[Unity C#] Processing complete!");
    }
}