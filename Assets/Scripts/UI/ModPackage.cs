using System;
using System.Collections.Generic;
using UnityEngine;

public class ModPackage : MonoBehaviour
{
    public static ModPackage Instance { get; private set; }
    public event Action<object> OnModDataChanged;

    public bool isModLoaded { get; private set; } = false;
    public readonly ModData loadedMod = new ModData();
    public IReadOnlyList<AbilityData> CustomAbilities => loadedMod.GetAll<AbilityData>();
    public IReadOnlyList<HeroData> Heroes => loadedMod.GetAll<HeroData>();
    public IReadOnlyList<MonsterData> Monsters => loadedMod.GetAll<MonsterData>();
    public IReadOnlyList<ItemData> Items => loadedMod.GetAll<ItemData>();

    // --- CONCURRENT SESSION TRACKING (ENTITIES) ---
    private class EditingSession
    {
        public EntityData Original;
        public EntityData Clone;
    }

    // Single-session tracker per Entity Type (Hero, Monster, etc.)
    private readonly Dictionary<Type, EditingSession> _activeSessions = new Dictionary<Type, EditingSession>();

    // --- MULTI-SESSION TRACKING (DIRECTIVES) ---
    // Key: Original Directive reference | Value: Active Directive reference (direct reference, no clone)
    private readonly Dictionary<SliceDiceTextMod.ModDirectiveData, SliceDiceTextMod.ModDirectiveData> _activeDirectiveSessions =
        new Dictionary<SliceDiceTextMod.ModDirectiveData, SliceDiceTextMod.ModDirectiveData>();

    // --- EVENTS ---
    public event Action<Type, EntityData> OnActiveEntityChanged;
    public event Action OnDirectivesChanged; // Invoked when directives are saved, deleted, or reloaded
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

        // Database-level updates pass null as they don't originate from a specific UI class
        loadedMod.OnDataChanged += () => OnModDataChanged?.Invoke(null);
    }
    private void Start()
    {
        CreateNewMod();
        RootUIFactory.Instance.InitializeEntireUI();
    }

    public void CreateNewMod()
    {
        ClearAllEditingSessions();
        loadedMod.NewMod();
        isModLoaded = true;
        OnModLoaded?.Invoke();
    }
    public void LoadModFromTextmod(string fullMod)
    {
        ClearAllEditingSessions();
        loadedMod.LoadFromTextMod(fullMod);
        isModLoaded = true;
        OnModLoaded?.Invoke();
    }

    // =========================================================================
    // --- ENTITY-SPECIFIC API (SINGLE-SESSION / TYPE-SAFE) ---
    // =========================================================================

    public void LoadEntityForEditing<T>(T originalEntity) where T : EntityData
    {
        Type type = typeof(T);

        if (originalEntity == null)
        {
            UnloadEditingSession<T>();
            return;
        }

        var session = new EditingSession
        {
            Original = originalEntity,
            Clone = Clone(originalEntity)
        };

        _activeSessions[type] = session;
        OnActiveEntityChanged?.Invoke(type, session.Clone);
    }
    public T GetActiveEntity<T>() where T : EntityData
    {
        if (_activeSessions.TryGetValue(typeof(T), out var session))
        {
            return session.Clone as T;
        }
        return null;
    }
    public void UnloadEditingSession<T>() where T : EntityData
    {
        Type type = typeof(T);
        if (_activeSessions.Remove(type))
        {
            OnActiveEntityChanged?.Invoke(type, null);
        }
    }
    public void SaveActiveEntity<T>() where T : EntityData
    {
        Type type = typeof(T);

        if (_activeSessions.TryGetValue(type, out var session))
        {
            loadedMod.SaveEntity(session.Original, session.Clone);
            session.Original = session.Clone;
        }
    }
    public void DeleteEntity<T>(T entityToDelete) where T : EntityData
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

    /// <summary>
    /// Gets the unique editable clone for a specific directive. 
    /// If no editing session exists for this directive instance, one is created automatically.
    /// </summary>
    public SliceDiceTextMod.ModDirectiveData GetOrCreateDirectiveSession(SliceDiceTextMod.ModDirectiveData original)
    {
        if (original == null) return null;

        if (_activeDirectiveSessions.TryGetValue(original, out var activeInstance))
        {
            return activeInstance;
        }

        // We use the direct reference instead of cloning
        _activeDirectiveSessions[original] = original;
        return original;
    }

    public void SaveDirective(SliceDiceTextMod.ModDirectiveData original)
    {
        if (original == null) return;

        if (_activeDirectiveSessions.TryGetValue(original, out var activeInstance))
        {
            loadedMod.SaveDirective(original, activeInstance);
            _activeDirectiveSessions.Remove(original); // Close editing session after saving
            OnDirectivesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Discards any pending local edits made to a directive instance.
    /// </summary>
    public void CancelDirectiveEdit(SliceDiceTextMod.ModDirectiveData original)
    {
        if (original == null) return;
        _activeDirectiveSessions.Remove(original);
    }

    /// <summary>
    /// Deletes a directive configuration completely.
    /// </summary>
    public void DeleteDirective(SliceDiceTextMod.ModDirectiveData original)
    {
        if (original == null) return;

        loadedMod.DeleteDirective(original);
        _activeDirectiveSessions.Remove(original);
        OnDirectivesChanged?.Invoke();
    }

    public void MoveDirective(SliceDiceTextMod.ModDirectiveData directive, int direction)
    {
        loadedMod.MoveDirective(directive, direction);
    }

    // =========================================================================
    // --- LIFE CYCLE CLEANUP & UTILITIES ---
    // =========================================================================

    public void ClearAllEditingSessions()
    {
        // Clean up entity sessions
        List<Type> activeTypes = new List<Type>(_activeSessions.Keys);
        _activeSessions.Clear();

        foreach (var type in activeTypes)
        {
            OnActiveEntityChanged?.Invoke(type, null);
        }

        // Clean up directive sessions
        _activeDirectiveSessions.Clear();
        OnDirectivesChanged?.Invoke();
    }
    private T Clone<T>(T source) where T : class
    {
        if (source == null) return null;
        string json = JsonUtility.ToJson(source);
        return JsonUtility.FromJson<T>(json);
    }

    public void NotifyActiveEntityChanged<T>(object sender) where T : EntityData
    {
        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session))
        {
            OnActiveEntityChanged?.Invoke(type, session.Clone);
        }

        // Trigger the change event and pass the sender along
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

    /// <summary>
    /// Replaces the currently active working clone in an editing session.
    /// Useful for actions like pasting an import or resetting an entity to defaults.
    /// </summary>
    public void UpdateActiveEntityClone<T>(T newClone) where T : EntityData
    {
        Type type = typeof(T);
        if (_activeSessions.TryGetValue(type, out var session))
        {
            session.Clone = newClone;
            OnActiveEntityChanged?.Invoke(type, newClone);
        }
    }
}