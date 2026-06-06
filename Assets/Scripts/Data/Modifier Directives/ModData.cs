using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

[System.Serializable]
public class ModData
{
    public event Action OnDataChanged;

    [SerializeField] private List<SliceDiceTextMod.ModDirectiveData> _directives = new List<SliceDiceTextMod.ModDirectiveData>();
    [SerializeField] private List<HeroData> _heroes = new List<HeroData>();
    [SerializeField] private List<MonsterData> _monsters = new List<MonsterData>();
    [SerializeField] private List<AbilityData> _abilities = new List<AbilityData>();
    [SerializeField] private List<ItemData> _items = new List<ItemData>();

    // --- AUTOMATION SETTINGS ---
    [SerializeField] public bool AutoExport_ReplaceBaseHeroes = false;
    [SerializeField] public bool AutoExport_HideHeroPool = true;

    private bool _suppressNotifications = false;
    private bool _notificationWasSuppressed = false;

    private Dictionary<Type, System.Collections.IList> _listMap;

    public bool SuppressNotifications
    {
        get => _suppressNotifications;
        set
        {
            _suppressNotifications = value;
            if (!_suppressNotifications && _notificationWasSuppressed)
            {
                _notificationWasSuppressed = false;
                NotifyDataChanged();
            }
        }
    }

    public void NewMod()
    {
        _directives.Clear();
        _heroes.Clear();
        _monsters.Clear();
        _abilities.Clear();
        _items.Clear();

        // Reset defaults
        AutoExport_ReplaceBaseHeroes = false;
        AutoExport_HideHeroPool = true;

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

    private System.Collections.IList GetRawList(Type type)
    {
        if (_listMap == null)
        {
            _listMap = new Dictionary<Type, System.Collections.IList>
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

    private List<T> GetList<T>() where T : SDData => GetRawList(typeof(T)) as List<T>;

    public IReadOnlyList<T> GetAll<T>() where T : SDData => GetList<T>();

    public T Load<T>(int index) where T : SDData
    {
        var list = GetList<T>();
        if (list != null && index >= 0 && index < list.Count) return list[index];
        return null;
    }

    public void SaveEntity(SDData original, SDData updated)
    {
        if (updated == null) return;

        System.Collections.IList list = GetRawList(updated.GetType());
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

    public void DeleteEntity(SDData target)
    {
        if (target == null) return;

        System.Collections.IList list = GetRawList(target.GetType());
        if (list != null)
        {
            list.Remove(target);
            NotifyDataChanged();
        }
    }

    public void ClearDirectives()
    {
        _directives.Clear();
        NotifyDataChanged();
    }

    public IReadOnlyList<SliceDiceTextMod.ModDirectiveData> GetDirectives() => _directives;

    public SliceDiceTextMod.ModDirectiveData LoadDirective(int index)
    {
        if (index >= 0 && index < _directives.Count) return _directives[index];
        return null;
    }

    public void SaveDirective(SliceDiceTextMod.ModDirectiveData original, SliceDiceTextMod.ModDirectiveData updated)
    {
        if (updated == null) return;

        int index = _directives.IndexOf(original);
        if (index >= 0) _directives[index] = updated;
        else _directives.Add(updated);

        NotifyDataChanged();
    }

    public void DeleteDirective(SliceDiceTextMod.ModDirectiveData target)
    {
        if (target == null) return;
        if (_directives.Remove(target)) NotifyDataChanged();
    }

    public void MoveDirective(SliceDiceTextMod.ModDirectiveData directive, int direction)
    {
        if (directive == null) return;

        int index = _directives.IndexOf(directive);
        if (index < 0) return;

        int newIndex = index + direction;
        if (newIndex >= 0 && newIndex < _directives.Count)
        {
            _directives.RemoveAt(index);
            _directives.Insert(newIndex, directive);
            NotifyDataChanged();
        }
    }

    // --- SERIALIZATION & AUTOMATION ---
    public static string Export(ModData modData)
    {
        if (modData == null) return "";

        List<string> blocks = new List<string>();

        // 1. Compile explicit manual directives
        foreach (var directive in modData.GetDirectives())
        {
            string syntax = directive.ToModString();
            if (!string.IsNullOrWhiteSpace(syntax))
            {
                blocks.Add(syntax);
            }
        }

        // 2. Automate custom hero exports using an ephemeral HeroPoolData instance
        var heroes = modData.GetAll<HeroData>();
        if (heroes != null && heroes.Count > 0)
        {
            var autoHeroPool = new SliceDiceTextMod.HeroPoolData
            {
                ReplaceBaseHeroes = modData.AutoExport_ReplaceBaseHeroes,
                IsHidden = modData.AutoExport_HideHeroPool
            };

            foreach (var hero in heroes)
            {
                string heroSyntax = HeroData.Export(hero);
                if (!string.IsNullOrWhiteSpace(heroSyntax))
                {
                    autoHeroPool.Elements.Add(heroSyntax);
                }
            }

            // HeroPoolData takes care of formatting, trailing commas, and size-chunking itself
            string autoPoolSyntax = autoHeroPool.ToModString();
            if (!string.IsNullOrWhiteSpace(autoPoolSyntax))
            {
                blocks.Add(autoPoolSyntax);
            }
        }

        return string.Join("&", blocks);
    }
}