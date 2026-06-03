using System.Collections.Generic;

namespace SliceDiceTextMod
{
    // Passive data models. No logic, no lexer dependencies.
    /*
    public class ModDataContainer
    {
        public List<ModDirectiveData> Directives { get; set; } = new List<ModDirectiveData>();
        public List<HeroData> Heroes { get; set; } = new List<HeroData>();
    }
    */
    /*
    public abstract class ModDirectiveData
    {
        public bool IsHidden { get; set; }
        public string FloorSelectorRaw { get; set; }
    }
    */

    /*
    public class PoolData : ModDirectiveData
    {
        public bool ReplaceBase { get; set; }
        public List<string> Elements { get; set; } = new List<string>();
    }
    */

    /*
    public class PhaseData : ModDirectiveData
    {
        public string PhaseCode { get; set; }
        public List<string> PhaseActions { get; set; } = new List<string>();
    }
    */

    /*
    public class ChoiceMenuData : ModDirectiveData
    {
        public string ChoiceMarker { get; set; }
        public string Label { get; set; }
        public List<string> Payloads { get; set; } = new List<string>();
    }
    */

    /*
    public class RawDirectiveData : ModDirectiveData
    {
        public string RawContent { get; set; }
    }
    */
    /*
    public class DiceSideData
    {
        public int effectID;
        public int pips;
        public string facadeID;
        public string facadeColor;
        public List<string> keywords = new List<string>();
    }
    */

    /*
    public class HeroData
    {
        public string baseReplica;
        public string heroName;
        public int hp;
        public int tier;
        public string colorClass;
        public string imageOverride;
        public string speech;
        public string doc;
        public int h, s, v;
        public DiceSideData[] diceSides = new DiceSideData[6]
        {
            new DiceSideData(), new DiceSideData(), new DiceSideData(),
            new DiceSideData(), new DiceSideData(), new DiceSideData()
        };
    }
    */
}