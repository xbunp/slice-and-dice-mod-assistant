using System;
using System.Collections.Generic;
using System.Text;

public static class SpriteSearchAliases
{
    private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // =========================================================================
    // 1. BI-DIRECTIONAL SYNONYM GROUPS (Clean, non-poisoning synonyms)
    // Members inject each other into BuildExpandedSearchString.
    // Words with dangerous substrings (mallet, firearm, shieldall, swordshield,
    // basefreewin, suengage) are in UniDirectionalTags below so they don't
    // pollute other sprites.
    // =========================================================================
    private static readonly string[][] SynonymGroups = new string[][]
    {
        new[] { "heavy", "axe", "hatchet", "mattock", "hammer" },
        new[] { "damage", "dmg" },
        new[] { "sword", "dagger", "knife", "cutlas", "blade" },
        new[] { "bow", "crossbow", "longbow", "shortbow", "arrow", "phantasm", "sling" },
        new[] { "gun", "sniper", "ammo", "gunshot", "flintlock", "blaster", "musket" },
        new[] { "shield", "block", "defense" },
        new[] { "selfshield", "self-shield", "self shield" },
        new[] { "sickle", "scythe", "razor" },
        new[] { "heal", "healing", "heart" },
        new[] { "vitality", "vita", "vit" },
        new[] { "regen", "regeneration" },
        new[] { "recharge", "reuse" },
        new[] { "revenge", "repel" },
        new[] { "taunt", "redirect", "shush", "distract" },
        new[] { "ite", "item" },
        new[] { "singleuse", "single use", "single-use" },
        new[] { "stick", "wand", "staff", "wood" },
        new[] { "spell", "spe" },
        new[] { "ring", "band" },
        new[] { "spear", "lance", "pole", "javelin" },
        new[] { "fist", "punch" },
        new[] { "undying", "die" },
        new[] { "add", "inflict" },
        new[] { "resurrect", "revive" },
        new[] { "fire", "flame", "burn" },
        new[] { "boom", "blast", "explosion" },
        new[] { "skull", "kill" },
        new[] { "death", "doom" },
        new[] { "chest", "hoard" },
        new[] { "rock", "spike", "stone" },
        new[] { "petrify", "gaze", "sight" },
        new[] { "growth", "grooo", "sprout" },
        new[] { "time", "hourglass", "clock" },
        new[] { "steel", "bash" },
        new[] { "ice", "cold", "snow", "chill", "frost" },
        new[] { "hungry", "cruesh", "eatcruel" },
        new[] { "caltrop", "dmgself" },
        new[] { "foot", "stomp", "boot" },
        new[] { "uparrow", "downarrow", "powerup", "level", "direction" },
        new[] { "target", "snipe", "bullseye" },
        new[] { "trigger", "exclamation" },
        new[] { "inflictpain", "wail" },
        new[] { "lightning", "bolt", "zap", "charge", "charged", "electric", "spark", "flash" },
    };

    // =========================================================================
    // 2. UNI-DIRECTIONAL TAGS (Trigger -> Search words)
    // Lets cryptic filenames AND substring-trapped words (like "mallet" or
    // "firearm") be found by normal searches WITHOUT injecting those strings
    // onto unrelated sprites.
    // =========================================================================
    private static readonly Dictionary<string, string[]> UniDirectionalTags = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        // Substring-safe weapon/item aliases
        { "mallet",      new[] { "heavy", "axe", "hatchet", "mattock", "hammer" } },
        { "firearm",     new[] { "ranged", "gun", "sniper", "ammo", "gunshot", "flintlock", "blaster", "musket" } },
        { "swordshield", new[] { "selfshield", "self-shield", "self shield", "sword", "shield" } },
        { "shieldall",   new[] { "music", "note", "shield", "all" } },
        { "suengage",    new[] { "spear", "lance", "pole", "javelin" } },
        { "picky side",  new[] { "spear", "lance", "pole", "javelin" } },

        // Cryptic sprite IDs -> human concepts
        { "eba_51_",     new[] { "singleuse", "single use", "single-use", "stick", "wand", "staff", "wood" } },
        { "eme_58_",     new[] { "singleuse", "single use", "single-use", "stick", "wand", "staff", "wood" } },
        { "eba_62",      new[] { "time", "hourglass", "clock" } },
        { "har_106_",    new[] { "caltrop", "dmgself" } },
        { "yca_61_",     new[] { "caltrop", "dmgself" } },
        { "zhgem5",      new[] { "caltrop", "dmgself" } },
        { "zharr",       new[] { "uparrow", "downarrow", "powerup", "level", "direction" } },
        { "eba_54_z",    new[] { "charged", "charge", "lightning" } },
        { "basefreewin", new[] { "death", "doom", "skull", "kill" } },
        { "baseak",      new[] { "gun", "ranged", "ammo", "gunshot" } },
        { "basesniper",  new[] { "gun", "sniper", "ranged", "ammo", "gunshot" } },
        { "eba_72",      new[] { "mace", "lightning", "lighting" } },
    };

    // =========================================================================
    // 3. UNI-DIRECTIONAL CATEGORIES (Umbrella terms)
    // Searching the key finds all matching sprites, without cross-pollination.
    // =========================================================================
    private static readonly Dictionary<string, string[]> CategoryMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "hero",       new[] { "SlimeCleave", "SlimeSlam", "Tusks", "Wail", "WeakenEye", "Bats", "Barrel" } },
        { "damage",     new[] { "eba_52", "OkN_6", "ric_10", "ric_11", "ric_13", "weapons", "pep_0_1", "pep_3_4", "pep_9_a", "sword", "dagger", "knife", "cutlas", "blade", "butcher", "sbane", "backstab" } },
        { "ranged",     new[] { "crossbow", "longbow", "shortbow", "bow", "arrow", "phantasm", "sling", "sniper", "gun", "firearm", "baseak", "basesniper", "ammo", "gunshot", "flintlock", "blaster", "musket" } },
        { "heavy",      new[] { "eba_42", "damagecriticalguilt", "cleaver" } },
        { "singleuse",  new[] { "stick", "wand", "staff", "wood", "eba_51_", "eme_58_" } },
        { "eye",        new[] { "petrify", "gaze", "sight" } },
        { "taunt",      new[] { "bird", "hand" } },

        { "rainbow",    new[] { "keyword-soup", "Spi_38", "Prism", "Lem_282", "Okn_37", "Mut_26", "Med_3", "Lem_125", "Lem_85", "har_100", "har_12", "Fre_1", "Eme_8_ ", "eba_54", "eba_58", "eba_18", "dee_43", "bas_107", "bas_101", "chaos" } },
        { "exert",      new[] { "eba_48" } },
        { "shield",     new[] { "eba_71", "eba_32", "eba_36", "eba_60", "eba_44", "OkN_8", "pep_2_3", "eba_66", "eba_65", "PEP_2_3" } },
        { "mana",       new[] { "magic", "wand", "eba_35", "eba_32", "eba_74", "eba_75", "eba_79", "eba_80", "pep_5_6", "eba_55", "eba_56", "eba_54", "eba_42", "PEP_5_6" } },
        { "heal",       new[] { "eba_37", "eba_38", "eba_58", "eba_67", "PEP_1_2", "selfboost", "fberry" } },
        { "cleave",     new[] { "eba_31", "eba_59", "ric_0", "ric_1", "ric_3", "ric_4", "ric_6" } },
        { "mace",       new[] { "eba_83", "eba_72", "Eme_12_" } },
        { "fire",       new[] { "eba_84", "eba_77" } },
        { "lighting",   new[] { "eba_72" } },
        { "skull",      new[] { "berserk", "status8", "junjun", "pos_100", "pos_101", "basefreewin" } },
        { "kill",       new[] { "pos_100", "pos_101", "baseFreeWin" } },
        { "stun",       new[] { "ric_5", "ric_8", "pep_10_b" } },
        { "music",      new[] { "treble", "note", "sound", "clef", "flat", "natural", "sharp", "bass", "alto", "simile", "har_76", "rest" } },
        { "animal",     new[] { "cat", "dog", "paw", "bunny", "rabbit", "poodle", "skink", "snake", "rat", "fbom", "snail" } },
        { "bomb",       new[] { "SingleUseAll", "tnt", "kamikaze", "blast", "explosion", "grenade" } },
        { "chain",      new[] { "PEP_6_7", "PEP_7_8", "PEP_8_9", "PEP_9_a", "Yca_33" } },
        { "cruel",      new[] { "Yca_10", "Yca_6" } },
        { "scythe",     new[] { "Yca_27" } },
        { "cantrip",    new[] { "eba_54", "Eme_26" } },
        { "selfshield", new[] { "Okn_39", "Eme_112" } },
        { "smith",      new[] { "eba_65", "eba_66", "eba_67", "eba_68" } },
        { "descend",    new[] { "tuningfork" } },
        { "card",       new[] { "ace-of-spades", "pair-of-kings", "three-of-a-kind", "two-of-clubs", "spades", "clubs", "diamond" } },
        { "holy",       new[] { "ankh", "cross", "vigil", "halo" } },
        { "food",       new[] { "cake", "fruit", "nacho", "pizza", "mug", "icecream", "apple", "onigiri", "berry", "steak", "meat", "ite_360", "mushroom", "pumpkin" } },

        // Colors
        { "green",      new[] { "poison", "plague", "acidic", "weaken", "emerald", "grass", "vine", "spore" } },
        { "blue",       new[] { "era", "mana", "future", "water", "splat", "stasis" } },
        { "purple",     new[] { "ite_132", "Ber_54", "shifter", "cantrip", "urple", "Lem_280", "Lem_312", "Lem_314", "Target Any", "shadow", "inflict", "musketeer", "mentalsickness", "demonheart", "hyperuse", "duplicate", "allpips", "gamblish", "unsetted" } },
        { "red",        new[] { "heart", "heal", "pain" } },
        { "pink",       new[] { "zglam", "vigil", "cantrip" } },
        { "grey",       new[] { "space", "pale" } },
        { "yellow",     new[] { "engage", "steel", "93_coin", "brimstone", "gold", "gilded", "heart-of-light", "puzzle-box", "tiara", "twisted-flax", "moxie", "star", "cymbals", "rescue", "tuba", "engine", "alchemyair", "eba_38", "eba_30", "banana", "castzap", "sun", "rampage", "fierce", "holy", "lead" } },
        { "letter",     new[] { "sym" } }
    };

    // Vocabulary used ONLY for splitting smashed lowercase compounds (e.g. "dmgflame", "allguilt", "guiltall").
    // Short English-colliding stems ("ice", "die", "ite", "spe", "vit", "ring") are intentionally excluded here.
    private static readonly HashSet<string> CompoundVocab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static SpriteSearchAliases()
    {
        for (int i = 0; i < SynonymGroups.Length; i++)
        {
            for (int w = 0; w < SynonymGroups[i].Length; w++)
                AddToCompoundVocab(SynonymGroups[i][w]);
        }

        foreach (var kvp in CategoryMap)
        {
            AddToCompoundVocab(kvp.Key);
            for (int i = 0; i < kvp.Value.Length; i++)
                AddToCompoundVocab(kvp.Value[i]);
        }

        string[] extraGameStems = { "all", "self", "guilt", "fumble", "pips", "side", "free", "win", "base", "weaken", "boost", "poison", "heal", "dmg", "flame", "cruel", "slime" };
        for (int i = 0; i < extraGameStems.Length; i++)
            CompoundVocab.Add(extraGameStems[i]);
    }

    private static void AddToCompoundVocab(string term)
    {
        string lower = term.Trim().ToLowerInvariant();
        if (lower.Length < 3 || lower.Contains(" ") || lower.Contains("_") || lower.Contains("-"))
            return;

        // Prevent short stems that match common English words from splitting compounds falsely
        if (lower == "ice" || lower == "die" || lower == "ite" || lower == "spe" || lower == "vit" || lower == "ring" || lower == "eye" || lower == "add")
            return;

        CompoundVocab.Add(lower);
    }

    public static string BuildExpandedSearchString(string rawName, string cleanName, string tooltipText)
    {
        string cacheKey = $"{rawName}::{cleanName}::{tooltipText}";
        if (Cache.TryGetValue(cacheKey, out string cachedResult))
            return cachedResult;

        HashSet<string> terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(rawName)) terms.Add(rawName);
        if (!string.IsNullOrEmpty(cleanName)) terms.Add(cleanName);
        if (!string.IsNullOrEmpty(tooltipText)) terms.Add(tooltipText);

        // 1. Keep original text for exact ID/delimiter matching (e.g. "eba_51_", "pep_0_1", "Target Any", "self-shield")
        string rawCombined = $"{rawName} {cleanName} {tooltipText}";
        string rawLower = rawCombined.ToLowerInvariant();

        // 2. Build CamelCase + compound-split token stream (e.g. "dmgFlame" -> "dmg flame", "allguilt" -> "all guilt")
        List<string> splitTokens = SplitCamelCaseAndDelimiters(rawCombined);
        HashSet<string> extractedTokens = new HashSet<string>(splitTokens, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < splitTokens.Count; i++)
            ExtractLowercaseCompounds(splitTokens[i], extractedTokens);

        foreach (string tok in extractedTokens)
            terms.Add(tok);

        string tokenTextLower = string.Join(" ", extractedTokens).ToLowerInvariant();

        // 3. Process Bi-Directional Synonym Groups
        for (int i = 0; i < SynonymGroups.Length; i++)
        {
            string[] group = SynonymGroups[i];
            bool matched = false;

            for (int w = 0; w < group.Length; w++)
            {
                string wordLower = group[w].ToLowerInvariant();
                if (ContainsDelimited(rawLower, wordLower) || ContainsDelimited(tokenTextLower, wordLower))
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                for (int w = 0; w < group.Length; w++)
                    terms.Add(group[w]);
            }
        }

        // 4. Process Uni-Directional Tags (mallet, firearm, eba_*, etc.)
        foreach (var kvp in UniDirectionalTags)
        {
            string triggerLower = kvp.Key.ToLowerInvariant();
            if (ContainsDelimited(rawLower, triggerLower) || ContainsDelimited(tokenTextLower, triggerLower))
            {
                for (int i = 0; i < kvp.Value.Length; i++)
                    terms.Add(kvp.Value[i]);
            }
        }

        // 5. Process Uni-Directional Categories
        foreach (var kvp in CategoryMap)
        {
            string categoryTag = kvp.Key;
            string[] triggers = kvp.Value;

            for (int t = 0; t < triggers.Length; t++)
            {
                string triggerLower = triggers[t].ToLowerInvariant();
                if (ContainsDelimited(rawLower, triggerLower) || ContainsDelimited(tokenTextLower, triggerLower))
                {
                    terms.Add(categoryTag);
                    break;
                }
            }
        }

        string finalSearchString = string.Join(" ", terms);
        Cache[cacheKey] = finalSearchString;
        return finalSearchString;
    }

    private static bool ContainsDelimited(string textLower, string termLower)
    {
        if (string.IsNullOrEmpty(textLower) || string.IsNullOrEmpty(termLower))
            return false;

        int index = 0;
        while ((index = textLower.IndexOf(termLower, index, StringComparison.Ordinal)) != -1)
        {
            bool startOk = (index == 0) || !char.IsLetterOrDigit(textLower[index - 1]) || !char.IsLetterOrDigit(termLower[0]);
            int end = index + termLower.Length;
            bool endOk = (end == textLower.Length) || !char.IsLetterOrDigit(textLower[end]) || !char.IsLetterOrDigit(termLower[termLower.Length - 1]);

            if (startOk && endOk)
                return true;

            index++;
        }

        return false;
    }

    private static List<string> SplitCamelCaseAndDelimiters(string text)
    {
        List<string> tokens = new List<string>();
        if (string.IsNullOrEmpty(text)) return tokens;

        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (!char.IsLetterOrDigit(c))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString().ToLowerInvariant());
                    sb.Clear();
                }
                continue;
            }

            if (sb.Length > 0 && char.IsUpper(c))
            {
                char prev = text[i - 1];
                bool nextIsLower = (i + 1 < text.Length) && char.IsLower(text[i + 1]);
                if (char.IsLower(prev) || char.IsDigit(prev) || (char.IsUpper(prev) && nextIsLower))
                {
                    tokens.Add(sb.ToString().ToLowerInvariant());
                    sb.Clear();
                }
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString().ToLowerInvariant());

        return tokens;
    }

    private static void ExtractLowercaseCompounds(string token, HashSet<string> destination)
    {
        if (token.Length < 6 || CompoundVocab.Contains(token))
            return;

        // Only split when BOTH sides look valid (prevents "mallet", "small", "practice", etc.)
        for (int split = 3; split <= token.Length - 3; split++)
        {
            string left = token.Substring(0, split);
            string right = token.Substring(split);

            bool leftKnown = CompoundVocab.Contains(left);
            bool rightKnown = CompoundVocab.Contains(right);

            if ((leftKnown && rightKnown) ||
                (leftKnown && right.Length >= 4 && left != "all") ||
                (rightKnown && left.Length >= 4 && right != "all"))
            {
                destination.Add(left);
                destination.Add(right);
            }
        }
    }
}