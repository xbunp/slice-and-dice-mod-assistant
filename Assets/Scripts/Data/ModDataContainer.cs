using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ModDataContainer
{
    public event Action OnDataChanged;

    // The single source of truth. Natively preserves execution order.
    public List<SliceDiceTextMod.ModDirectiveData> Directives { get; set; } = new List<SliceDiceTextMod.ModDirectiveData>();

    // MAGICAL HELPER: GUI elements call this to instantly get a categorized list of exactly what they need.
    // Example: List<HeroPoolData> heroes = loadedMod.Get<HeroPoolData>();
    public List<T> Get<T>() where T : SliceDiceTextMod.ModDirectiveData
    {
        return Directives.OfType<T>().ToList();
    }

    public void LoadFromText(string rawMod)
    {
        Directives.Clear();
        SliceDiceTextMod.ModTextEngine.UnpackIntoContainer(rawMod, this);
        NotifyDataChanged();
    }

    public string ExportToText()
    {
        return SliceDiceTextMod.ModTextEngine.Repack(Directives);
    }

    public void NotifyDataChanged() => OnDataChanged?.Invoke();
}