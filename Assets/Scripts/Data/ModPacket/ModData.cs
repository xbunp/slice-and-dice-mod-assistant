using SliceDiceTextMod;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModData
{
    public event Action OnDataChanged;

    [SerializeField] private List<SliceDiceTextMod.ModDirectiveData> _directives = new List<SliceDiceTextMod.ModDirectiveData>();
    [SerializeField] private List<HeroData> _heroes = new List<HeroData>();
    [SerializeField] private List<MonsterData> _monsters = new List<MonsterData>();
    [SerializeField] private List<AbilityData> _abilities = new List<AbilityData>();
    [SerializeField] private List<ItemData> _items = new List<ItemData>();

    private bool _suppressNotifications = false;
    private bool _notificationWasSuppressed = false;

    public bool SuppressNotifications
    {
        get => _suppressNotifications;
        set
        {
            _suppressNotifications = value;
            // If we are enabling notifications and one was queued up, trigger it now
            if (!_suppressNotifications && _notificationWasSuppressed)
            {
                _notificationWasSuppressed = false;
                NotifyDataChanged();
            }
        }
    }

    private Dictionary<Type, IList> _listMap;

    // --- LIFE CYCLE ---
    public void NewMod()
    {
        _directives.Clear();
        _heroes.Clear();
        _monsters.Clear();
        _abilities.Clear();
        _items.Clear();
        NotifyDataChanged();
    }
    public void LoadFromTextMod(string rawMod)
    {
        NewMod();
        // to do later.
        NotifyDataChanged();
    }
    private void NotifyDataChanged()
    {
        if (SuppressNotifications)
        {
            _notificationWasSuppressed = true;
            return;
        }
        OnDataChanged?.Invoke();
    }

    private IList GetRawList(Type type)
    {
        if (_listMap == null)
        {
            // If you add a new entity type in the future, you ONLY add one line here.
            _listMap = new Dictionary<Type, IList>
            {
                { typeof(HeroData), _heroes },
                { typeof(MonsterData), _monsters },
                { typeof(AbilityData), _abilities },
                { typeof(ItemData), _items }
            };
        }

        _listMap.TryGetValue(type, out var list);
        return list;
    }
    private List<T> GetList<T>() where T : EntityData
    {
        return GetRawList(typeof(T)) as List<T>;
    }

    // --- GENERIC GETTERS ---
    public IReadOnlyList<T> GetAll<T>() where T : EntityData
    {
        return GetList<T>();
    }
    public T Load<T>(int index) where T : EntityData
    {
        var list = GetList<T>();
        if (list != null && index >= 0 && index < list.Count)
        {
            return list[index];
        }
        return null;
    }

    // --- UNTYPED SAVE & DELETE (For runtime Singleton execution) ---
    public void SaveEntity(EntityData original, EntityData updated)
    {
        if (updated == null) return;

        IList list = GetRawList(updated.GetType());
        if (list == null) return;

        int index = list.IndexOf(original);
        if (index >= 0)
        {
            list[index] = updated; // Replace existing
        }
        else
        {
            list.Add(updated); // Add new
        }

        NotifyDataChanged();
    }
    public void DeleteEntity(EntityData target)
    {
        if (target == null) return;

        IList list = GetRawList(target.GetType());
        if (list != null)
        {
            list.Remove(target);
            NotifyDataChanged();
        }
    }

    // --- DIRECTIVE METHODS ---
    public void ClearDirectives()
    {
        _directives.Clear();
        NotifyDataChanged();
    }
    public IReadOnlyList<SliceDiceTextMod.ModDirectiveData> GetDirectives()
    {
        return _directives;
    }
    public SliceDiceTextMod.ModDirectiveData LoadDirective(int index)
    {
        if (index >= 0 && index < _directives.Count)
        {
            return _directives[index];
        }
        return null;
    }
    public void SaveDirective(SliceDiceTextMod.ModDirectiveData original, SliceDiceTextMod.ModDirectiveData updated)
    {
        if (updated == null) return;

        int index = _directives.IndexOf(original);
        if (index >= 0)
        {
            _directives[index] = updated;
        }
        else
        {
            _directives.Add(updated);
        }

        NotifyDataChanged();
    }
    public void DeleteDirective(SliceDiceTextMod.ModDirectiveData target)
    {
        if (target == null) return;

        if (_directives.Remove(target))
        {
            NotifyDataChanged();
        }
    }

    // SERIALIZATION:
    public static string Export(ModData modData)
    {
        if (modData == null) return "";

        /*
        List<string> blocks = new List<string>();

        // 1. Compile active entities
        if (modData.ActiveHero != null && !string.IsNullOrEmpty(modData.ActiveHero.entityName))
        {
            blocks.Add(HeroData.Export(modData.ActiveHero));
        }

        // 2. Compile structural rules
        foreach (var directive in modData.Directives)
        {
            string syntax = SerializeDirective(directive);
            if (!string.IsNullOrWhiteSpace(syntax))
            {
                blocks.Add(syntax);
            }
        }
        */

        return null; //string.Join("&", blocks);
    }
    private static string SerializeDirective(ModDirectiveData data)
    {
        string core = "";

        if (data is PoolDirectiveData pool)
        {
            string prefix = pool is HeroPoolData hp && hp.ReplaceBaseHeroes ? "replace.heropool" :
                            pool is HeroPoolData ? "heropool" :
                            pool is MonsterPoolData ? "monsterpool" :
                            pool is ItemPoolData ? "itempool" :
                            pool is ForcedFightData ? "fight" :
                            pool is SpawnInjectionData ? "add" : "party";

            if (pool.Elements.Count > 0)
            {
                core = $"{prefix}.{string.Join("+", pool.Elements)}";
            }
        }
        else if (data is ConfigDirectiveData config)
        {
            core = $"{config.Prefix}.{config.Payload}";
        }
        else if (data is ToggleDirectiveData toggle)
        {
            core = $"tog{toggle.ToggleName}";
        }
        else if (data is CommandDirectiveData cmd)
        {
            core = cmd.Command;
        }
        else if (data is RawDirectiveData raw)
        {
            core = raw.RawContent;
        }

        if (string.IsNullOrEmpty(core)) return "";

        // Wrap structurally
        if (!string.IsNullOrEmpty(data.FloorSelectorRaw)) core = $"{data.FloorSelectorRaw}.{core}";
        if (data.IsHidden) core = $"!m({core})";

        return core;
    }

}
