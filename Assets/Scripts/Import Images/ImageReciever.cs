using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public class ImageReceiver : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void TriggerFileOpen();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReadOSClipboard(string objectName, string methodName);
#endif

    public Action<string, Texture2D> OnImageGenerated;

    [Header("UI References")]
    public RawImage reducedImagePreview;
    public TMPro.TMP_Dropdown compressionDropdown;
    public TMPro.TMP_InputField outputStringField;
    public Button copyString, pasteString, clearString;
    public Button decodeExport; 

    private Texture2D _uploadedTexture;
    private Texture2D _generatedPreviewTexture;

    public string EncodedString => outputStringField != null ? outputStringField.text : string.Empty;
    public Texture2D GeneratedPreviewTexture => _generatedPreviewTexture;
    public Texture2D UploadedTexture => _uploadedTexture;

    private void Awake()
    {

    }

    private void Start()
    {
        if (compressionDropdown != null)
        {
            compressionDropdown.ClearOptions();
            var enumNames = new System.Collections.Generic.List<string>(Enum.GetNames(typeof(ImageColorExtractor.CompressionFactor)));
            compressionDropdown.AddOptions(enumNames);
            compressionDropdown.onValueChanged.AddListener(delegate { ProcessCurrentTexture(); });
        }
        else
        {
            Debug.LogError("<b><color=red>[SCREAM-RECEIVER] ERROR: Dropdown reference is MISSING in inspector!</color></b>", this);
        }

        if (copyString != null)
        {
            copyString.onClick.AddListener(() => {
                ClipboardManager.CopyToClipboard(EncodedString);
            });
        }
        else Debug.LogError("<b><color=red>[SCREAM-RECEIVER] ERROR: copyString button is MISSING in inspector!</color></b>", this);

        if (pasteString != null)
        {
            pasteString.onClick.AddListener(() =>
            {
                ClipboardManager.RequestPaste(null, (clipboardText) => {
                    ProcessPastedText(clipboardText);
                });
            });
        }
        else Debug.LogError("<b><color=red>[SCREAM-RECEIVER] ERROR: pasteString button is MISSING in inspector!</color></b>", this);

        if (decodeExport != null)
        {
            decodeExport.onClick.AddListener(DecodeCurrentInputString);
        }
        else Debug.LogError("<b><color=red>[SCREAM-RECEIVER] ERROR: DECODE button is MISSING in inspector!</color></b>", this);


        if (clearString != null)
        {
            clearString.onClick.AddListener(ClearData);
        }
    }

    /// <summary>
    /// Evaluates the clipboard text string and handles image decoding or raw text assignment.
    /// </summary>
    private void ProcessPastedText(string pastedText)
    {
        if (string.IsNullOrEmpty(pastedText)) return;

        if (outputStringField != null)
        {
            outputStringField.text = pastedText.Trim();

            // If the pasted string starts with our raw format versions, decode it immediately
            if (pastedText.StartsWith("2") || pastedText.StartsWith("3"))
            {
                DecodeCurrentInputString();
            }
        }
    }

    /// <summary>
    /// Invoked automatically by the browser with the native clipboard payload.
    /// </summary>
    public void OnClipboardDataReceived(string clipboardText)
    {
        ProcessPastedText(clipboardText);
    }

    public void RestoreState(Texture2D uploadedTex, Texture2D generatedTex, string encodedStr)
    {
        _uploadedTexture = uploadedTex;
        _generatedPreviewTexture = generatedTex;
        if (reducedImagePreview != null) reducedImagePreview.texture = _generatedPreviewTexture;
        if (outputStringField != null) outputStringField.text = encodedStr;
    }

    public void OnSelectFileClicked()
    {
    #if UNITY_WEBGL && !UNITY_EDITOR
        TriggerFileOpen();
    #else
    #endif
    }

    public void OnImageLoaded(string base64String)
    {
        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64String);
            if (_uploadedTexture != null) Destroy(_uploadedTexture);

            _uploadedTexture = new Texture2D(2, 2);
            if (_uploadedTexture.LoadImage(imageBytes))
            {
                ProcessCurrentTexture();
            }
            else
            {
                Debug.LogError("<b><color=red>[SCREAM-RECEIVER] ERROR: LoadImage failed on byte array!</color></b>", this);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"<b><color=red>[SCREAM-RECEIVER] FATAL EXCEPTION: {ex.Message}</color></b>", this);
        }
    }

    private void ProcessCurrentTexture()
    {
        if (_uploadedTexture == null)
        {
            Debug.Log("No image set to compress.", this);
            return;
        }

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

        int listenerCount = OnImageGenerated?.GetInvocationList().Length ?? 0;

        OnImageGenerated?.Invoke(encodedString, _generatedPreviewTexture);
    }

    /// <summary>
    /// Destroys cached textures, resets UI components, and updates the image override string to empty.
    /// </summary>
    public void ClearData()
    {
        if (_uploadedTexture != null)
        {
            Destroy(_uploadedTexture);
            _uploadedTexture = null;
        }


        if (_generatedPreviewTexture != null)
        {
            Destroy(_generatedPreviewTexture);
            _generatedPreviewTexture = null;
        }

        if (reducedImagePreview != null)
        {
            reducedImagePreview.texture = null;
        }

        if (outputStringField != null)
        {
            outputStringField.text = string.Empty;
        }

        // Notify HeroModManager to clear the image override reference
        OnImageGenerated?.Invoke(string.Empty, null);
    }

    /////////////// DECODING /////////////////////
    ///

    /// <summary>
    /// Decodes the custom string currently in the input field and updates the preview.
    /// </summary>
    public void DecodeCurrentInputString()
    {
        if (outputStringField == null || string.IsNullOrWhiteSpace(outputStringField.text)) return;

        string currentText = outputStringField.text;
        Texture2D decodedTex = ImageColorExtractor.DecodeTexture(currentText);

        if (decodedTex != null)
        {
            if (_generatedPreviewTexture != null) Destroy(_generatedPreviewTexture);
            _generatedPreviewTexture = decodedTex;

            if (reducedImagePreview != null) reducedImagePreview.texture = _generatedPreviewTexture;

            OnImageGenerated?.Invoke(currentText, _generatedPreviewTexture);
        }
    }

    /// <summary>
    /// Converts the current preview texture into a PNG file and saves/downloads it.
    /// </summary>
    public void ExportImageToPNG()
    {
        if (_generatedPreviewTexture == null)
        {
            Debug.LogWarning("[ImageReceiver] No image available to export.");
            return;
        }

        // Convert the Texture2D into raw PNG image bytes
        byte[] pngBytes = _generatedPreviewTexture.EncodeToPNG();
        string fileName = "decoded_hero.png";

        #if UNITY_WEBGL && !UNITY_EDITOR
        // In WebGL, send Base64 string to the browser to trigger a download prompt
        string base64 = Convert.ToBase64String(pngBytes);
        DownloadFileWebGL(fileName, base64);
        #else
        // In Standalone or Editor, save directly to Application.persistentDataPath
        string savePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        System.IO.File.WriteAllBytes(savePath, pngBytes);
        Debug.Log($"[ImageReceiver] Saved image to: {savePath}");
        #endif
    }
}