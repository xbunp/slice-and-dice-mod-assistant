using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ModData
{
    public event Action OnDataChanged;

    [SerializeField] private List<SliceDiceTextMod.TextModBlock> _directives = new List<SliceDiceTextMod.TextModBlock>();
    [SerializeField] private List<HeroData> _heroes = new List<HeroData>();
    [SerializeField] private List<MonsterData> _monsters = new List<MonsterData>();
    [SerializeField] private List<SpellData> _spells = new List<SpellData>();
    [SerializeField] private List<TacticData> _tactics = new List<TacticData>();
    [SerializeField] private List<ItemData> _customItems = new List<ItemData>();
    [SerializeField] private List<ModifierData> _customModifiers = new List<ModifierData>();

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
        _customItems.Clear();

        AutoExport_ReplaceBaseHeroes = false;
        AutoExport_HideHeroPool = true;

        NotifyDataChanged();
    }

    public void LoadFromTextMod(string rawMod)
    {
        NewMod();
        // TODO: Implement import logic
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

    // Modernized with pattern matching for cleaner reading
    private System.Collections.IList GetRawList(Type type)
    {
        if (type == null) return null;

        if (typeof(SpellData).IsAssignableFrom(type)) return _spells;
        if (typeof(TacticData).IsAssignableFrom(type)) return _tactics;
        if (typeof(HeroData).IsAssignableFrom(type)) return _heroes;
        if (typeof(MonsterData).IsAssignableFrom(type)) return _monsters;
        if (typeof(ItemData).IsAssignableFrom(type)) return _customItems;
        if (typeof(ModifierData).IsAssignableFrom(type)) return _customModifiers;

        return null;
    }

    private List<T> GetList<T>() where T : SDData => GetRawList(typeof(T)) as List<T>;

    public IReadOnlyList<T> GetAll<T>() where T : SDData
    {
        // Fixed Memory allocation: Uses LINQ Concat to stream data instead of building a new List in memory every call.
        if (typeof(T) == typeof(AbilityData))
        {
            return _spells.Cast<T>().Concat(_tactics.Cast<T>()).ToList();
        }

        return GetList<T>();
    }

    public T Load<T>(int index) where T : SDData
    {
        var list = GetAll<T>();
        if (list != null && index >= 0 && index < list.Count) return list[index];
        return null;
    }

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
            if (index >= 0) newList[index] = updated;
            else newList.Add(updated);
        }

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
        if (target != null && _directives.Remove(target))
        {
            NotifyDataChanged();
        }
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

    // =========================================================================
    // --- SERIALIZATION & AUTOMATION (THE "NOTEPAD" FIX) ---
    // =========================================================================
    public static string Export(ModData modData)
    {
        if (modData == null) return "";

        List<string> blocks = new List<string>();

        // 1. Compile explicit manual directives
        foreach (var directive in modData.GetDirectives())
        {
            string syntax = directive.ToModString();
            if (!string.IsNullOrWhiteSpace(syntax)) blocks.Add(syntax);
        }

        // 2. Automate Custom Heroes
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
                if (!string.IsNullOrWhiteSpace(heroSyntax)) autoHeroPool.Elements.Add(heroSyntax);
            }

            string autoPoolSyntax = autoHeroPool.ToModString();
            if (!string.IsNullOrWhiteSpace(autoPoolSyntax)) blocks.Add(autoPoolSyntax);
        }

        // 3. Automate Custom Items
        var items = modData.GetAll<ItemData>();
        if (items != null)
        {
            foreach (var item in items)
            {
                // Assuming ItemData has an Export() or ToModString() method like HeroData does.
                // If they use an ItemPoolData class similar to Heroes, you can wrap them here just like above.
                string itemSyntax = item.Export();
                if (!string.IsNullOrWhiteSpace(itemSyntax)) blocks.Add(itemSyntax);
            }
        }

        // 4. Automate Custom Monsters
        var monsters = modData.GetAll<MonsterData>();
        if (monsters != null)
        {
            foreach (var monster in monsters)
            {
                string monsterSyntax = monster.Export();
                if (!string.IsNullOrWhiteSpace(monsterSyntax)) blocks.Add(monsterSyntax);
            }
        }

        // 5. Automate Abilities (Spells & Tactics)
        var abilities = modData.GetAll<AbilityData>();
        if (abilities != null)
        {
            foreach (var ability in abilities)
            {
                string abilitySyntax = ability.Export();
                if (!string.IsNullOrWhiteSpace(abilitySyntax)) blocks.Add(abilitySyntax);
            }
        }

        // 6. Automate Modifiers 
        var modifiers = modData.GetAll<ModifierData>();
        if (modifiers != null)
        {
            foreach (var modifier in modifiers)
            {
                string modifierSyntax = modifier.Export();
                if (!string.IsNullOrWhiteSpace(modifierSyntax)) blocks.Add(modifierSyntax);
            }
        }

        // Finally, join absolutely everything together with the ampersand delimiter.
        return string.Join("&", blocks);
    }
}