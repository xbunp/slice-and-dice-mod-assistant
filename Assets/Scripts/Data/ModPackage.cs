using UnityEngine;

public class ModPackage : MonoBehaviour
{
    public static ModPackage Instance { get; private set; }

    public bool isModLoaded = false;
    public ModDataContainer loadedMod = new ModDataContainer();

    private void Awake()
    {
        // Set up the singleton instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void LoadMod(string fullMod)
    {
        isModLoaded = true;
        loadedMod.LoadMod(fullMod);
    }
}