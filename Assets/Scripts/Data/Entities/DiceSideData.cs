using System.Collections.Generic;

[System.Serializable]
public class DiceSideData
{
    public enum DiceFaceType
    {
        Base,
        Sticker,
        Cast,
        Enchant//,
        //Egg
    }

    // NEW: Your exact target definitions
    public enum PayloadTarget
    {
        None,
        Self,
        Ally,
        Enemy,
        AllAllies,
        AllEnemies,
        Everyone,
    }

    public static string GetInherentDefaultTargetName(DiceSideData.DiceFaceType faceType)
    {
        switch (faceType)
        {
            case DiceSideData.DiceFaceType.Sticker: return "Ally";
            //case DiceSideData.DiceFaceType.Enchant: return "Self";
            //case DiceSideData.DiceFaceType.Egg: return "None";
            //case DiceSideData.DiceFaceType.Cast: return "Spell Default";
            default: return "Inherent";
        }
    }

    public static bool IsTargetInherentDefault(DiceSideData.DiceFaceType faceType, DiceSideData.PayloadTarget target)
    {
        // Define which explicit selections are mathematically identical to "Default"
        if (faceType == DiceSideData.DiceFaceType.Sticker && target == DiceSideData.PayloadTarget.Ally) return true;
        //if (faceType == DiceSideData.DiceFaceType.Enchant && target == DiceSideData.PayloadTarget.Self) return true;
        //if (faceType == DiceSideData.DiceFaceType.Egg && target == DiceSideData.PayloadTarget.Self) return true;

        return false;
    }

    public DiceFaceType faceType = DiceFaceType.Base;

    //Standard side values
    public int effectID = 0;
    public int pips = 0;
    public string facadeID = "";
    public string facadeColor = "";
    public List<string> keywords = new List<string>();
    public string sidesc = "";

    public bool togtime = false;

    // Advanced side values
    public PayloadTarget? payloadTarget = null; // null represents "Default (Inherent)"
    public string payload = "";

    public DiceSideData Clone()
    {
        return new DiceSideData
        {
            effectID = this.effectID,
            pips = this.pips,
            facadeID = this.facadeID,
            facadeColor = this.facadeColor,
            keywords = new List<string>(this.keywords),
            faceType = this.faceType,
            payloadTarget = this.payloadTarget,
            payload = this.payload,
            sidesc = this.sidesc
        };
    }
}