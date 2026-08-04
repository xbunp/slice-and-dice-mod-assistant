using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ModPackage : MonoBehaviour
{
    public static ModPackage Instance { get; private set; }
    public event Action<object> OnModDataChanged;

    // Converted to PascalCase for standard C# property naming
    public bool IsModLoaded { get; private set; } = false;
    public readonly ModData loadedMod = new ModData();

    public IReadOnlyList<AbilityData> CustomAbilities => (IReadOnlyList<AbilityData>)loadedMod.GetAll<AbilityData>();
    public IReadOnlyList<HeroData> Heroes => loadedMod.GetAll<HeroData>();
    public IReadOnlyList<MonsterData> Monsters => loadedMod.GetAll<MonsterData>();
    public IReadOnlyList<ItemData> CustomItems => loadedMod.GetAll<ItemData>();
    public IReadOnlyList<ModifierData> CustomModifiers => loadedMod.GetAll<ModifierData>();

    // --- CONCURRENT SESSION TRACKING (ENTITIES) ---
    private class EditingSession
    {
        public SDData Original;
        public SDData Clone;
    }

    private readonly Dictionary<Type, EditingSession> _activeSessions = new Dictionary<Type, EditingSession>();

    // --- MULTI-SESSION TRACKING (DIRECTIVES) ---
    // Changed to a HashSet: Since you edit the direct reference, a Dictionary where Key==Value is redundant.
    private readonly HashSet<SliceDiceTextMod.TextModBlock> _activeDirectiveSessions = new HashSet<SliceDiceTextMod.TextModBlock>();

    // --- EVENTS ---
    public event Action<Type, SDData> OnActiveEntityChanged;
    public event Action OnDirectivesChanged;
    public event Action OnModLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        loadedMod.OnDataChanged += () => OnModDataChanged?.Invoke(null);
    }

    private void Start()
    {
        CreateNewMod();

        // Null coalescing operator simplifies this check
        RootUIFactory.Instance?.InitializeEntireUI();
    }

    public void CreateNewMod()
    {
        ClearAllEditingSessions();
        loadedMod.NewMod();
        IsModLoaded = true;
        OnModLoaded?.Invoke();
    }

    public void LoadModFromTextmod(string fullMod)
    {
        ClearAllEditingSessions();
        loadedMod.LoadFromTextMod(fullMod);
        IsModLoaded = true;
        OnModLoaded?.Invoke();
    }

    // =========================================================================
    // --- ENTITY-SPECIFIC API (SINGLE-SESSION / TYPE-SAFE) ---
    // =========================================================================

    public void LoadEntityForEditing<T>(T originalEntity) where T : SDData
    {
        if (originalEntity == null)
        {
            UnloadEditingSession<T>();
            return;
        }

        Type type = typeof(T);
        var session = new EditingSession
        {
            Original = originalEntity,
            Clone = Clone(originalEntity)
        };

        _activeSessions[type] = session;
        OnActiveEntityChanged?.Invoke(type, session.Clone);
    }

    public T GetActiveEntity<T>() where T : SDData
    {
        return _activeSessions.TryGetValue(typeof(T), out var session) ? session.Clone as T : null;
    }

    public void UnloadEditingSession<T>() where T : SDData
    {
        Type type = typeof(T);
        if (_activeSessions.Remove(type))
        {
            OnActiveEntityChanged?.Invoke(type, null);
        }
    }

    public void SaveActiveEntity<T>() where T : SDData
    {
        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session))
        {
            loadedMod.SaveEntity(session.Original, session.Clone);
            session.Original = session.Clone;
        }
    }

    public void DeleteEntity<T>(T entityToDelete) where T : SDData
    {
        if (entityToDelete == null) return;

        loadedMod.DeleteEntity(entityToDelete);

        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session) && session.Original == entityToDelete)
        {
            UnloadEditingSession<T>();
        }
    }

    // =========================================================================
    // --- DIRECTIVE-SPECIFIC API (MULTI-SESSION / INSTANCE-SAFE) ---
    // =========================================================================

    public SliceDiceTextMod.TextModBlock GetOrCreateDirectiveSession(SliceDiceTextMod.TextModBlock original)
    {
        if (original == null) return null;

        _activeDirectiveSessions.Add(original);
        return original; // Returning direct reference as requested by original design
    }

    public void SaveDirective(SliceDiceTextMod.TextModBlock original)
    {
        if (original != null && _activeDirectiveSessions.Contains(original))
        {
            loadedMod.SaveDirective(original, original); // Passing original twice since we didn't clone
            _activeDirectiveSessions.Remove(original);
            OnDirectivesChanged?.Invoke();
        }
    }

    public void CancelDirectiveEdit(SliceDiceTextMod.TextModBlock original)
    {
        if (original != null) _activeDirectiveSessions.Remove(original);
    }

    public void DeleteDirective(SliceDiceTextMod.TextModBlock original)
    {
        if (original == null) return;

        loadedMod.DeleteDirective(original);
        _activeDirectiveSessions.Remove(original);
        OnDirectivesChanged?.Invoke();
    }

    public void MoveDirective(SliceDiceTextMod.TextModBlock directive, int direction)
    {
        loadedMod.MoveDirective(directive, direction);
    }

    // =========================================================================
    // --- LIFE CYCLE CLEANUP & UTILITIES ---
    // =========================================================================

    public void ClearAllEditingSessions()
    {
        List<Type> activeTypes = new List<Type>(_activeSessions.Keys);
        _activeSessions.Clear();

        foreach (var type in activeTypes)
        {
            OnActiveEntityChanged?.Invoke(type, null);
        }

        _activeDirectiveSessions.Clear();
        OnDirectivesChanged?.Invoke();
    }

    /*
    private T Clone<T>(T source) where T : class
    {
        if (source == null) return null;

        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            ContractResolver = new UnityStructResolver()
        };

        string json = JsonConvert.SerializeObject(source, settings);
        return JsonConvert.DeserializeObject(json, source.GetType(), settings) as T;
    }*/

    private T Clone<T>(T source) where T : class
    {
        if (source == null) return null;

        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            ObjectCreationHandling = ObjectCreationHandling.Replace, // <-- CRITICAL: Prevents array appending/duplication on deserialization
            ContractResolver = new UnityStructResolver()
        };

        string json = JsonConvert.SerializeObject(source, settings);
        return JsonConvert.DeserializeObject(json, source.GetType(), settings) as T;
    }


    public void NotifyActiveEntityChanged<T>(object sender) where T : SDData
    {
        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session))
        {
            OnActiveEntityChanged?.Invoke(type, session.Clone);
        }
        OnModDataChanged?.Invoke(sender);
    }

    public void NotifyDirectiveSessionChanged(object sender)
    {
        OnDirectivesChanged?.Invoke();
        OnModDataChanged?.Invoke(sender);
    }

    public string ExportModToTextModString()
    {
        return ModData.Export(loadedMod);
    }

    public void UpdateActiveEntityClone<T>(T newClone) where T : SDData
    {
        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session))
        {
            session.Clone = newClone;
            OnActiveEntityChanged?.Invoke(type, newClone);
        }
    }
    /*
    public void TryAutoRegisterParsedEntity(SDData entity)
    {
        if (entity == null || !IsModLoaded || loadedMod == null) return;

        string exportStr;
        try
        {
            exportStr = entity.Export();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auto-Register] Failed to export entity for deduplication check: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(exportStr) || exportStr == "()") return;

        // 1. Filter out pure base / unmodified / basic inline entities so we don't clutter the UI
        if (entity is ItemData item)
        {
            string cleanName = item.entityName?.Trim() ?? "";
            bool isVanillaName = ItemDomainRules.TogItems.Contains(cleanName) || ExternalGameRegistry.IsValidItemName(cleanName);
            bool hasExplicitCustomName = !string.IsNullOrEmpty(cleanName) && !isVanillaName;

            bool isComplex = item.Mechanics.Count > 1 || item.visuals.Count > 0 || item.Tier.HasValue ||
                             !string.IsNullOrEmpty(item.doc) || !string.IsNullOrEmpty(item.imageOverride) ||
                             item.LearnedAbilities.Count > 0 || item.Containers.Count > 0;

            if (!hasExplicitCustomName && !isComplex) return;
        }
        else if (entity is OrbData orb)
        {
            if (orb.isHardcoded) return;
        }
        else if (entity is AbilityData ad)
        {
            if (exportStr.Equals(ad.baseReplica, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({ad.baseReplica})", StringComparison.OrdinalIgnoreCase)) return;
        }
        else if (entity is HeroData hd)
        {
            if (exportStr.Equals(hd.baseReplica, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({hd.baseReplica})", StringComparison.OrdinalIgnoreCase)) return;
        }
        else if (entity is MonsterData md)
        {
            if (exportStr.Equals(md.baseMonster, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({md.baseMonster})", StringComparison.OrdinalIgnoreCase)) return;
        }

        // 2. Prevent duplicating entities that have already been extracted or made
        System.Collections.IList list = loadedMod.GetRawList(entity.GetType());
        if (list == null) return;

        foreach (SDData existing in list)
        {
            if (existing == entity) return; // Exact instance already injected
            try
            {
                if (existing.Export() == exportStr) return; // Signature match already exists
            }
            catch { /* Ignore faulty existing items  }
        }

        // 3. Save into the active Mod Data for UI visibility
        loadedMod.SaveEntity(null, entity);
    }
*/

    // Assets/Scripts/Data/ModData/ModPackage.cs

    public void TryAutoRegisterParsedEntity(SDData entity)
    {
        if (entity == null || !IsModLoaded || loadedMod == null) return;
        if (entity.SuppressAutoRegister) return;

        string exportStr;
        try
        {
            exportStr = entity.Export();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Auto-Register] Failed to export entity for deduplication check: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(exportStr) || exportStr == "()") return;

        // 1. Strict Filter: Discard vanilla items and inline atomic modifiers (like 'left.k.cantrip')
        if (entity is ItemData item)
        {
            string cleanName = item.entityName?.Trim() ?? "";
            bool isVanillaName = ItemDomainRules.TogItems.Contains(cleanName) || ExternalGameRegistry.IsValidItemName(cleanName);
            bool hasExplicitCustomName = !string.IsNullOrEmpty(cleanName) && !isVanillaName;

            // Only register unnamed items if they contain major custom architecture (Hats, Spells, OnHits)
            bool hasHat = item.Mechanics.Any(m => m.Prefix != null && m.Prefix.Equals("hat", StringComparison.OrdinalIgnoreCase));
            bool hasCustomAbilities = item.Mechanics.Any(m => m.Prefix != null && (
                m.Prefix.Equals("onhitdata", StringComparison.OrdinalIgnoreCase) ||
                m.Prefix.Equals("triggerhpdata", StringComparison.OrdinalIgnoreCase) ||
                m.Prefix.Equals("cast", StringComparison.OrdinalIgnoreCase) ||
                m.Prefix.Equals("abilitydata", StringComparison.OrdinalIgnoreCase)));

            if (!hasExplicitCustomName && !hasHat && !hasCustomAbilities) return;
        }
        else if (entity is OrbData orb)
        {
            if (orb.isHardcoded) return;
        }
        else if (entity is AbilityData ad)
        {
            if (exportStr.Equals(ad.baseReplica, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({ad.baseReplica})", StringComparison.OrdinalIgnoreCase)) return;
        }
        else if (entity is HeroData hd)
        {
            if (exportStr.Equals(hd.baseReplica, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({hd.baseReplica})", StringComparison.OrdinalIgnoreCase)) return;
        }
        else if (entity is MonsterData md)
        {
            if (exportStr.Equals(md.baseMonster, StringComparison.OrdinalIgnoreCase) ||
                exportStr.Equals($"({md.baseMonster})", StringComparison.OrdinalIgnoreCase)) return;
        }

        // 2. Prevent duplicating entities that have already been extracted or made
        System.Collections.IList list = loadedMod.GetRawList(entity.GetType());
        if (list == null) return;

        foreach (SDData existing in list)
        {
            if (existing == entity) return;
            try
            {
                if (existing.Export() == exportStr) return; // Signature match already exists
            }
            catch { /* Ignore faulty existing items */ }
        }

        // 3. Save into the active Mod Data for UI visibility
        loadedMod.SaveEntity(null, entity);
    }

    public class UnityStructResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            if (property.DeclaringType == typeof(Color) && (property.PropertyName == "linear" || property.PropertyName == "gamma"))
            {
                property.ShouldSerialize = _ => false;
            }
            return property;
        }
    }
}