using System.Collections.Generic;

[System.Serializable]
public class DiceSideData
{
    public int effectID = 0;
    public int pips = 0;
    public string facadeID = "";
    public string facadeColor = "";
    public List<string> keywords = new List<string>();
    public string rawModifications = "";

    public DiceSideData Clone()
    {
        return new DiceSideData
        {
            effectID = this.effectID,
            pips = this.pips,
            facadeID = this.facadeID,
            facadeColor = this.facadeColor,
            keywords = new List<string>(this.keywords),
            rawModifications = this.rawModifications
        };
    }
}