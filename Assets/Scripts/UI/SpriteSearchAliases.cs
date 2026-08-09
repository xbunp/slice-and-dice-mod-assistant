using System;
using System.Collections.Generic;

public static class SpriteSearchAliases
{
    private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // =========================================================================
    // 1. BI-DIRECTIONAL SYNONYM GROUPS (Interchangeable terms)
    // If a sprite matches ANY word in a list, it inherits ALL words in that list!
    // "heavy" <-> "axe" | "fire" <-> "flame" <-> "burn"
    // =========================================================================
    private static readonly string[][] SynonymGroups = new string[][]
    {
        new[] { "heavy", "axe", "hatchet", "mattock", "hammer", "mallet"},
        new[] { "damage", "dmg", "sword", "dagger", "knife", "cutlas", "blade", "butcher", "sbane", "backstab" },
        new[] { "ranged", "crossbow", "longbow", "shortbow", "arrow", "phantasm" },
        new[] { "shield", "block", "defense" },
        new[] { "sickle", "scythe", "razor" },
        new[] { "heal", "heart", "vita", "vit", "boost", "fberry" },
        new[] { "recharge", "reuse" },
        new[] { "revenge", "repel" },
        new[] { "taunt", "redirect", "bird", "self shield", "shush", "distract", "hand" },
        new[] { "ite", "item" },
        new[] { "shieldall", "music", "note" },
        new[] { "singleuse", "single use", "single-use", "stick", "wand", "staff", "wood" },
        new[] { "spell", "spe" },
        new[] { "ring", "band" },
        new[] { "spear", "lance", "pole", "javelin", "picky side", "suengage" },
        new[] { "fist", "punch" },
        new[] { "undying", "die" },
        new[] { "rainbow", "keyword-soup" },
        new[] { "add", "inflict" },
        new[] { "resurrect", "revive" },
        new[] { "boom", "blast", "explosion", "fire", "flame" },
        new[] { "undying", "skull", "kill" },
        new[] { "chest", "hoard" },
        new[] { "rock", "spike", "stone" },
        new[] { "eye", "petrify", "gaze", "sight" },
        new[] { "growth", "grooo", "sprout" },
        new[] { "gun", "baseak", "basesniper", "ammo", "gunshot", "flintlock", "blaster" },
        /*
        new[] { "light", "dagger", "knife", "shiv" },
        new[] { "sword", "blade", "saber", "katana" },
        new[] { "shield", "block", "defense", "armor", "guard" },
        new[] { "heal", "health", "potion", "hp", "cure", "heart" },
        
        new[] { "fire", "flame", "burn", "pyro" },
        new[] { "ice", "frost", "freeze", "cold" },
        new[] { "poison", "toxic", "venom", "acid" },
        new[] { "bow", "arrow", "ranged", "archery" }
        */
    };

    // =========================================================================
    // 2. UNI-DIRECTIONAL CATEGORIES (Umbrella terms)
    // Categories that apply to many items, but DON'T make items synonymous.
    // Searching "damage" finds both "sword" and "fireball", but
    // searching "fireball" will NOT find "sword".
    // =========================================================================
    private static readonly Dictionary<string, string[]> CategoryMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "damage",  new[] { "eba_52", "OkN_6", "ric_10", "ric_11", "ric_13", "weapons", "pep_0_1", "pep_3_4", "pep_9_a" } },
        { "exert",  new[] { "eba_48" } },
        { "shield",  new[] { "eba_71", "eba_32", "eba_36", "eba_60", "eba_44", "OkN_8", "pep_2_3" } },
        { "mana",  new[] { "magic", "wand", "eba_35", "eba_32", "eba_74", "eba_75", "eba_79", "eba_80", "pep_5_6", "eba_55", "eba_56", "eba_54" } },
        { "heal",  new[] { "eba_37", "eba_38", "eba_58" } },
        { "heavy",  new[] { "eba_42", "damagecriticalguilt", "cleaver" } },
        { "cleave",  new[] { "eba_31", "eba_59", "ric_0", "ric_1", "ric_3", "ric_4", "ric_6" } },
        { "mace",  new[] { "eba_83" } },
        { "fire",  new[] { "eba_84", "eba_77" } },
        { "lighting",  new[] { "eba_72" } },
        { "skull",  new[] { "berserk", "status8", "junjun" } },
        { "stun",  new[] { "ric_5", "ric_8", "pep_10_b" } },
        { "music",  new[] { "treble", "note", "sound", "clef", "flat", "natural", "sharp", "bass", "alto", "simile" } },
        { "animal",  new[] { "cat", "dog", "paw", "bunny", "rabbit", "poodle", "skink", "snake", "rat", "fbom", "snail" } },
        { "bomb",  new[] { "SingleUseAll", "tnt", "kamikaze", "blast", "explosion", "grenade" } },

        { "food",  new[] { "cake", "fruit", "nacho", "pizza", "mug", "icecream", "apple", "onigiri", "berry", "steak", "meat", "ite_360", "mushroom"  } },

        //colors
        { "green",  new[] { "poison", "plague", "acidic", "weaken", "emerald", "grass", "vine", "spore" } },
        { "blue",  new[] { "era", "mana", "future", "water", "splat", "stasis" } },
        { "red",  new[] { "heart", "heal", "pain"  } },
        { "pink",  new[] { "zglam", "vigil", "cantrip" } },
        { "grey",  new[] { "space", "pale" } },
        { "yellow",  new[] { "engage", "steel", "93_coin", "brimstone", "gold", "heart-of-light", "puzzle-box", "tiara", "twisted-flax", "moxie", "star", "cymbals", "rescue", "tuba", "engine", "alchemyair", "eba_38", "eba_30", "banana", "castzap", "sun", "rampage", "fierce", "holy", "lead" } },
        { "letter",  new[] { "sym" } }
    };

    /// <summary>
    /// Builds a master search string containing filenames, tooltips, bi-directional synonyms, and categories.
    /// </summary>
    public static string BuildExpandedSearchString(string rawName, string cleanName, string tooltipText)
    {
        // ------------------------------------------------=================
        // NEW: CACHE LOOKUP (Returns instantly if this sprite was processed before)
        // ------------------------------------------------=================
        string cacheKey = $"{rawName}::{tooltipText}";
        if (Cache.TryGetValue(cacheKey, out string cachedResult))
        {
            return cachedResult;
        }

        // ------------------------------------------------=================
        // HEAVY CALCULATIONS (Only runs ONCE per sprite)
        // ------------------------------------------------=================
        HashSet<string> terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(rawName)) terms.Add(rawName);
        if (!string.IsNullOrEmpty(cleanName)) terms.Add(cleanName);
        if (!string.IsNullOrEmpty(tooltipText)) terms.Add(tooltipText);

        // OPTIMIZATION: Lowercase once here instead of 100,000 IndexOf calls below
        string fullTextLower = $"{rawName} {cleanName} {tooltipText}".ToLowerInvariant();

        // A. Process Bi-Directional Synonym Groups
        for (int i = 0; i < SynonymGroups.Length; i++)
        {
            string[] group = SynonymGroups[i];
            bool matchesGroup = false;

            for (int w = 0; w < group.Length; w++)
            {
                // CHANGED: Uses fast pre-lowercased Contains check
                if (fullTextLower.Contains(group[w].ToLowerInvariant()))
                {
                    matchesGroup = true;
                    break;
                }
            }

            if (matchesGroup)
            {
                for (int w = 0; w < group.Length; w++) terms.Add(group[w]);
            }
        }

        // B. Process Uni-Directional Categories
        foreach (var kvp in CategoryMap)
        {
            string categoryTag = kvp.Key;
            string[] triggers = kvp.Value;

            for (int t = 0; t < triggers.Length; t++)
            {
                // CHANGED: Uses fast pre-lowercased Contains check
                if (fullTextLower.Contains(triggers[t].ToLowerInvariant()))
                {
                    terms.Add(categoryTag);
                    break;
                }
            }
        }

        // ------------------------------------------------=================
        // NEW: SAVE TO CACHE
        // ------------------------------------------------=================
        string finalSearchString = string.Join(" ", terms);
        Cache[cacheKey] = finalSearchString;

        return finalSearchString;
    }
}