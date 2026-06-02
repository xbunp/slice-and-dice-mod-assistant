using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ModDataContainer
{
    // 1. Updated to pass the sender of the modification
    public event Action<object> OnDataChanged;
    public List<SliceDiceTextMod.ModDirectiveData> Directives { get; set; } = new List<SliceDiceTextMod.ModDirectiveData>();

    public List<T> Get<T>() where T : SliceDiceTextMod.ModDirectiveData
    {
        return Directives.OfType<T>().ToList();
    }

    public void LoadFromText(string rawMod)
    {
        Directives.Clear();
        SliceDiceTextMod.ModTextEngine.UnpackIntoContainer(rawMod, this);
        NotifyDataChanged(this); // Sender is the container itself
    }

    public string ExportToText()
    {
        return SliceDiceTextMod.ModTextEngine.Repack(Directives);
    }

    // 2. Accept the sender parameter
    public void NotifyDataChanged(object sender) => OnDataChanged?.Invoke(sender);
}