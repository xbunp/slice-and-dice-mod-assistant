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