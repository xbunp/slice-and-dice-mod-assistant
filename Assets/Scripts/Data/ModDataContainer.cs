using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ModDataContainer
{
    public event Action<object> OnDataChanged;
    public List<SliceDiceTextMod.ModDirectiveData> Directives { get; set; } = new List<SliceDiceTextMod.ModDirectiveData>();

    public List<HeroData> Heroes { get; set; } = new List<HeroData>();

    // THIS IS THE SSOT FOR THE UI
    // No strings here! Just pure C# data.
    public HeroData ActiveHero { get; set; } = new HeroData();

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