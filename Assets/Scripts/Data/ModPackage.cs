using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


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
    public void LoadModFromText(string fullMod)
    {
        isModLoaded = true;
        loadedMod.LoadFromText(fullMod);
    }

    private void Start()
    {
        LoadModFromText("");
    }
}


[System.Serializable]
public class ModDataContainer
{
    public event Action<object> OnDataChanged;
    public List<SliceDiceTextMod.ModDirectiveData> Directives { get; set; } = new List<SliceDiceTextMod.ModDirectiveData>();

    // Guard flag to temporarily block event propagation
    public bool SuppressNotifications { get; set; } = false;

    public List<T> Get<T>() where T : SliceDiceTextMod.ModDirectiveData
    {
        return Directives.OfType<T>().ToList();
    }

    public void LoadFromText(string rawMod)
    {
        Directives.Clear();
        SliceDiceTextMod.ModTextEngine.UnpackIntoContainer(rawMod, this);
        NotifyDataChanged(this);
    }

    public string ExportToText()
    {
        return SliceDiceTextMod.ModTextEngine.Repack(Directives);
    }

    public void NotifyDataChanged(object sender)
    {
        if (SuppressNotifications) return;
        OnDataChanged?.Invoke(sender);
    }
}