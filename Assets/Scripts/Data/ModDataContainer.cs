using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModDataContainer
{
    // Event that subscribers can listen to
    public event Action OnDataChanged;

    private string _fullTextMod;
    public string FullTextMod
    {
        get => _fullTextMod;
        set
        {
            if (_fullTextMod != value)
            {
                _fullTextMod = value;
                NotifyDataChanged();
            }
        }
    }

    private List<ModData> _allHeroes = new List<ModData>();
    public List<ModData> AllHeroes
    {
        get => _allHeroes;
        set
        {
            _allHeroes = value;
            NotifyDataChanged();
        }
    }

    private List<ModData> _allMonsters = new List<ModData>();
    public List<ModData> AllMonsters
    {
        get => _allMonsters;
        set
        {
            _allMonsters = value;
            NotifyDataChanged();
        }
    }

    private List<ModData> _allItems = new List<ModData>();
    public List<ModData> AllItems
    {
        get => _allItems;
        set
        {
            _allItems = value;
            NotifyDataChanged();
        }
    }

    private List<ModData> _allAbilities = new List<ModData>();
    public List<ModData> AllAbilities
    {
        get => _allAbilities;
        set
        {
            _allAbilities = value;
            NotifyDataChanged();
        }
    }

    public void LoadMod(string fullMod)
    {
        // Assigning to the property triggers the notification
        FullTextMod = fullMod;
    }

    // Helper method to safely invoke the event if there are active subscribers
    protected virtual void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}
public class ModData
{
    public string uniqueRef;
    public string data;
}

namespace SliceDiceTextMod
{
    [System.Serializable]
    public class ModData
    {
        public string RawTextMod { get; set; }

        public List<ModData> ItemPools { get; set; } = new List<ModData>();
        public List<ModData> HeroPools { get; set; } = new List<ModData>();
        public List<ModData> MonsterPools { get; set; } = new List<ModData>();
        public List<ModData> SpellPools { get; set; } = new List<ModData>();
        public List<ModData> StartingParties { get; set; } = new List<ModData>();
        public List<ModData> ForcedFights { get; set; } = new List<ModData>();
        public List<ModData> SpawnInjections { get; set; } = new List<ModData>();

        public List<ModData> IndividualItems { get; set; } = new List<ModData>();
        public List<ModData> IndividualHeroes { get; set; } = new List<ModData>();
        public List<ModData> IndividualMonsters { get; set; } = new List<ModData>();
        public List<ModData> IndividualAbilities { get; set; } = new List<ModData>();
        public List<ModData> IndividualSpells { get; set; } = new List<ModData>();
        public List<ModData> IndividualKeywords { get; set; } = new List<ModData>();
        public List<ModData> IndividualCurses { get; set; } = new List<ModData>();
        public List<ModData> IndividualBlessings { get; set; } = new List<ModData>();

        public List<ModData> LogicPhases { get; set; } = new List<ModData>();       // ph.b, ph.z, ph.s, ph.l, phi., phmp.
        public List<ModData> ChoiceRewards { get; set; } = new List<ModData>();     // ch.om, ch.v, ch.p, reward options
        public List<ModData> DifficultyConfigs { get; set; } = new List<ModData>(); // diff.
        public List<ModData> ZoneConfigs { get; set; } = new List<ModData>();       // zone.
        public List<ModData> GlobalRules { get; set; } = new List<ModData>();       // Wurst/5, PointBuy, Rushed, etc.
        public List<ModData> LevelTweaks { get; set; } = new List<ModData>();       // Smith events, boss pool overrides, credits
    }
}