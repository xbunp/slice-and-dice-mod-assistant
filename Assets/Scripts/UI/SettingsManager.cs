using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    private const string CharsPerFrameKey = "Settings_CharsPerFrame";
    private const string RegexMatchesKey = "Settings_RegexMatchesPerFrame";

    public static SettingsManager Instance { get; private set; }

    // Backing instance fields for persistence
    private int charsPerFrame = 10000;
    private int regexMatchesPerFrame = 150;

    // Static properties wrapper for global access without needing to reference '.Instance'
    public static int CHARS_PER_FRAME
    {
        get => Instance != null ? Instance.charsPerFrame : 10000;
        set
        {
            if (Instance != null)
            {
                Instance.charsPerFrame = value;
            }
        }
    }

    public static int REGEX_MATCHES_PER_FRAME
    {
        get => Instance != null ? Instance.regexMatchesPerFrame : 150;
        set
        {
            if (Instance != null)
            {
                Instance.regexMatchesPerFrame = value;
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSettings()
    {
        charsPerFrame = PlayerPrefs.GetInt(CharsPerFrameKey, 10000);
        regexMatchesPerFrame = PlayerPrefs.GetInt(RegexMatchesKey, 150);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetInt(CharsPerFrameKey, charsPerFrame);
        PlayerPrefs.SetInt(RegexMatchesKey, regexMatchesPerFrame);
        PlayerPrefs.Save();
    }
}