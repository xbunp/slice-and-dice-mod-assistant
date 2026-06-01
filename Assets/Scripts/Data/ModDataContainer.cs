using System;
using System.Collections.Generic;

[System.Serializable]
public class ModDataContainer
{
    public event Action OnDataChanged;

    private List<SliceDiceTextMod.ModDirectiveData> _directives = new List<SliceDiceTextMod.ModDirectiveData>();
    public List<SliceDiceTextMod.ModDirectiveData> Directives
    {
        get => _directives;
        set { _directives = value; NotifyDataChanged(); }
    }

    public void LoadFromText(string rawMod)
    {
        Directives = SliceDiceTextMod.ModTextEngine.Unpack(rawMod);
    }

    public string ExportToText()
    {
        return SliceDiceTextMod.ModTextEngine.Repack(Directives);
    }

    protected virtual void NotifyDataChanged() => OnDataChanged?.Invoke();
}