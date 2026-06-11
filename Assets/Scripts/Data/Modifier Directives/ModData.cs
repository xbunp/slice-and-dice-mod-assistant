using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

[System.Serializable]
public class ModData
{
    public event Action OnDataChanged;

    [SerializeField] private List<SliceDiceTextMod.TextModBlock> _directives = new List<SliceDiceTextMod.TextModBlock>();
    [SerializeField] private List<HeroData> _heroes = new List<HeroData>();
    [SerializeField] private List<MonsterData> _monsters = new List<MonsterData>();

    // Split concrete lists for Unity Serialization
    [SerializeField] private List<SpellData> _spells = new List<SpellData>();
    [SerializeField] private List<TacticData> _tactics = new List<TacticData>();

    [SerializeField] private List<ItemData> _items = new List<ItemData>();

    // --- AUTOMATION SETTINGS ---
    [SerializeField] public bool AutoExport_ReplaceBaseHeroes = false;
    [SerializeField] public bool AutoExport_HideHeroPool = true;

    private bool _suppressNotifications = false;
    private bool _notificationWasSuppressed = false;

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
        _spells.Clear();
        _tactics.Clear();
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

    // Resolves strictly to the concrete lists
    private System.Collections.IList GetRawList(Type type)
    {
        if (type == null) return null;

        if (typeof(SpellData).IsAssignableFrom(type)) return _spells;
        if (typeof(TacticData).IsAssignableFrom(type)) return _tactics;
        if (typeof(HeroData).IsAssignableFrom(type)) return _heroes;
        if (typeof(MonsterData).IsAssignableFrom(type)) return _monsters;
        if (typeof(ItemData).IsAssignableFrom(type)) return _items;

        return null;
    }

    private List<T> GetList<T>() where T : SDData => GetRawList(typeof(T)) as List<T>;

    public IReadOnlyList<T> GetAll<T>() where T : SDData
    {
        // If the API asks for AbilityData, dynamically combine Spells and Tactics to serve the UI
        if (typeof(T) == typeof(AbilityData))
        {
            var combinedAbilities = new List<AbilityData>();
            combinedAbilities.AddRange(_spells);
            combinedAbilities.AddRange(_tactics);
            return combinedAbilities as IReadOnlyList<T>;
        }

        return GetList<T>();
    }

    public T Load<T>(int index) where T : SDData
    {
        var list = GetAll<T>(); // Route through GetAll to support combined AbilityData resolution safely
        if (list != null && index >= 0 && index < list.Count) return list[index];
        return null;
    }

    /*
    public void SaveEntity(SDData original, SDData updated)
    {
        if (updated == null) return;

        System.Collections.IList oldList = original != null ? GetRawList(original.GetType()) : null;
        System.Collections.IList newList = GetRawList(updated.GetType());

        if (newList == null) return;

        // Safely handle when a user Toggles a Spell into a Tactic (or vice versa)
        if (oldList != null && oldList != newList)
        {
            oldList.Remove(original);
            newList.Add(updated);
        }
        else
        {
            int index = newList.IndexOf(original);
            if (index >= 0)
            {
                newList[index] = updated; // Replace existing
            }
            else
            {
                newList.Add(updated); // Add new
            }
        }

        NotifyDataChanged();
    }
    */

    public void SaveEntity(SDData original, SDData updated)
    {
        if (updated == null) return;

        System.Collections.IList oldList = original != null ? GetRawList(original.GetType()) : null;
        System.Collections.IList newList = GetRawList(updated.GetType());

        Debug.Log($"[DEBUG ModData] Saving Entity: {updated.entityName}. Type: {updated.GetType()}. Found List? {newList != null}");

        if (newList == null) return;

        if (oldList != null && oldList != newList)
        {
            oldList.Remove(original);
            newList.Add(updated);
        }
        else
        {
            int index = newList.IndexOf(original);
            if (index >= 0) newList[index] = updated;
            else newList.Add(updated);
        }

        Debug.Log($"[DEBUG ModData] Save Complete. Current Spell Count: {_spells.Count} | Current Tactic Count: {_tactics.Count}");
        NotifyDataChanged();
    }

    public void DeleteEntity(SDData target)
    {
        if (target == null) return;

        System.Collections.IList list = GetRawList(target.GetType());
        if (list != null && list.Contains(target))
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

    public IReadOnlyList<SliceDiceTextMod.TextModBlock> GetDirectives() => _directives;

    public SliceDiceTextMod.TextModBlock LoadDirective(int index)
    {
        if (index >= 0 && index < _directives.Count) return _directives[index];
        return null;
    }

    public void SaveDirective(SliceDiceTextMod.TextModBlock original, SliceDiceTextMod.TextModBlock updated)
    {
        if (updated == null) return;

        int index = _directives.IndexOf(original);
        if (index >= 0) _directives[index] = updated;
        else _directives.Add(updated);

        NotifyDataChanged();
    }

    public void DeleteDirective(SliceDiceTextMod.TextModBlock target)
    {
        if (target == null) return;
        if (_directives.Remove(target)) NotifyDataChanged();
    }

    public void MoveDirective(SliceDiceTextMod.TextModBlock directive, int direction)
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
                string heroSyntax = hero.Export();
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