using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public class AtlasProcessor : AssetPostprocessor
{
    private readonly string[] TargetFileNames = { "base_atlas_image.png", "community_atlas_image.png" };
    private const int AtlasHeight = 1024;

    // Explicit database mapping definitions to ensure accurate engine IDs
    // Explicit database mapping definitions to ensure accurate engine IDs
    // Note: All keys are lowercase to ensure matches against lowercased NormalizePath()
    private static readonly Dictionary<string, Dictionary<string, int>> PrefixCustomIds = new Dictionary<string, Dictionary<string, int>>
    {
        {
            "bas", new Dictionary<string, int>
            {
                // [Your 187 Hero-Sized entries here as lowercased paths]
                { "reg/face/blank/basic", 0 },
                { "reg/face/blank/unset", 1 },
                { "reg/face/blank/petrified", 2 },
                { "reg/face/blank/wand", 3 },
                { "reg/face/blank/item", 4 },
                { "reg/face/blank/curse", 5 },
                { "reg/face/blank/stasis", 6 },
                { "reg/face/blank/sticky", 7 },
                { "reg/face/blank/exerted", 8 },
                { "reg/face/blank/fumble", 9 },
                { "reg/face/special/addkeyword/cleanseselfcleanse", 10 },
                { "reg/face/item/backstab", 11 },
                { "reg/face/dmgselfcantrip", 12 },
                { "reg/face/pinkskull", 13 },
                { "reg/face/dmgselfmandatory", 14 },
                { "reg/face/sword", 15 },
                { "reg/face/dmggrowth", 16 },
                { "reg/face/dmgengage", 17 },
                { "reg/face/swordmagic", 18 },
                { "reg/face/dmgpain", 19 },
                { "reg/face/sworddeathwish", 20 },
                { "reg/face/dmgdeath", 21 },
                { "reg/face/dmgserrated", 22 },
                { "reg/face/swordexert", 23 },
                { "reg/face/dmgdouble", 24 },
                { "reg/face/swordquad", 25 },
                { "reg/face/dmgbloodlust", 26 },
                { "reg/face/dmgcopycat", 27 },
                { "reg/face/swordpristine", 28 },
                { "reg/face/dmgguilt", 29 },
                { "reg/face/kriss", 30 },
                { "reg/face/shadowdagger", 31 },
                { "reg/face/swordfocus", 32 },
                { "reg/face/swordinspire", 33 },
                { "reg/face/swordall", 34 },
                { "reg/face/resurrectmana", 35 },
                { "reg/face/swordcleave", 36 },
                { "reg/face/sworddescend", 37 },
                { "reg/face/dmgcleavechain", 38 },
                { "reg/face/hammer", 39 },
                { "reg/face/quartzslow", 40 },
                { "reg/face/shieldbash", 41 },
                { "reg/face/dmgcharged", 42 },
                { "reg/face/stun", 43 },
                { "reg/face/swordvulnerable", 44 },
                { "reg/face/dmgera", 45 },
                { "reg/face/arrow", 46 },
                { "reg/face/arrowpoison", 47 },
                { "reg/face/arrowduplicate", 48 },
                { "reg/face/arrowcleave", 49 },
                { "reg/face/arrowcopycat", 50 },
                { "reg/face/swordshield", 51 },
                { "reg/face/swordheal", 52 },
                { "reg/face/dmgpoison", 53 },
                { "reg/face/plague", 54 },
                { "reg/face/dmgpoisondose", 55 },
                { "reg/face/shield", 56 },
                { "reg/face/shieldflesh", 57 },
                { "reg/face/shieldgrowth", 58 },
                { "reg/face/shieldprecise", 59 },
                { "reg/face/shieldenduringdeath", 60 },
                { "reg/face/shieldmagic", 61 },
                { "reg/face/shielddoubleuse", 62 },
                { "reg/face/shieldsteel", 63 },
                { "reg/face/shieldrescue", 64 },
                { "reg/face/shieldpristine", 65 },
                { "reg/face/shieldcantrip", 66 },
                { "reg/face/shieldcopycat", 67 },
                { "reg/face/shieldfocus", 68 },
                { "reg/face/shieldplusadjacent", 69 },
                { "reg/face/shieldcharged", 70 },
                { "reg/face/shieldcure", 71 },
                { "reg/face/wardingchord", 72 },
                { "reg/face/flute", 73 },
                { "reg/face/shieldheart", 74 },
                { "reg/face/smith", 75 },
                { "reg/face/mana", 76 },
                { "reg/face/manacantrip", 77 },
                { "reg/face/manacantripboned", 78 },
                { "reg/face/managrowth", 79 },
                { "reg/face/manadecay", 80 },
                { "reg/face/manadeath", 81 },
                { "reg/face/manapain", 82 },
                { "reg/face/manabloodlust", 83 },
                { "reg/face/manapair", 84 },
                { "reg/face/manatriple", 85 },
                { "reg/face/healshieldmana", 86 },
                { "reg/face/manadouble", 87 },
                { "reg/face/wandcharged", 88 },
                { "reg/face/wandjinx", 89 },
                { "reg/face/wandfire", 90 },
                { "reg/face/wandpoison", 91 },
                { "reg/face/wandblood", 92 },
                { "reg/face/wandmana", 93 },
                { "reg/face/wandfightbonus", 94 },
                { "reg/face/wandweaken", 95 },
                { "reg/face/wandfierce", 96 },
                { "reg/face/wandecho", 97 },
                { "reg/face/wanddispel", 98 },
                { "reg/face/wandresilient", 99 },
                { "reg/face/wandstun", 100 },
                { "reg/face/wandchaos", 101 },
                { "reg/face/item/sceptre", 102 },
                { "reg/face/heal", 103 },
                { "reg/face/wandheal", 104 },
                { "reg/face/boon", 105 },
                { "reg/face/healrescue", 106 },
                { "reg/face/healall", 107 },
                { "reg/face/healbuff", 108 },
                { "reg/face/healcleave", 109 },
                { "reg/face/healregen", 110 },
                { "reg/face/healuncurse", 111 },
                { "reg/face/healmagic", 112 },
                { "reg/face/healgroooooowth", 113 },
                { "reg/face/healdouble", 114 },
                { "reg/face/stick", 115 },
                { "reg/face/kill", 116 },
                { "reg/face/undying", 117 },
                { "reg/face/taunt", 118 },
                { "reg/face/revenge", 119 },
                { "reg/face/shieldcrescent", 120 },
                { "reg/face/shieldpain", 121 },
                { "reg/face/headshot", 122 },
                { "reg/face/dodge", 123 },
                { "reg/face/dodgecantrip", 124 },
                { "reg/face/rerollcantrip", 125 },
                { "reg/face/dmgcantrip", 126 },
                { "reg/face/swordroulette", 127 },
                { "reg/face/flurry", 128 },
                { "reg/face/needle", 129 },
                { "reg/face/recharge", 130 },
                { "reg/face/dmgweaken", 131 },
                { "reg/face/swordduplicate", 132 },
                { "reg/face/shieldduplicate", 133 },
                { "reg/face/manaduplicate", 134 },
                { "reg/face/rangedengage", 135 },
                { "reg/face/resurrect", 136 },
                { "reg/face/dmgrampage", 137 },
                { "reg/face/special/addkeyword/doubleuse", 138 },
                { "reg/face/special/addkeyword/cantrip", 139 },
                { "reg/face/special/addkeyword/nothing", 140 },
                { "reg/face/special/addkeyword/copycat", 141 },
                { "reg/face/special/addkeyword/cleavesingleuse", 142 },
                { "reg/face/special/addkeyword/crueldeathwish", 143 },
                { "reg/face/special/addkeyword/managain", 144 },
                { "reg/face/special/addkeyword/poison", 145 },
                { "reg/face/special/addkeyword/selfshield", 146 },
                { "reg/face/special/addkeyword/selfheal", 147 },
                { "reg/face/special/addkeyword/selfhealselfshield", 148 },
                { "reg/face/special/addkeyword/managainpain", 149 },
                { "reg/face/special/addkeyword/engage", 150 },
                { "reg/face/special/addkeyword/growth", 151 },
                { "reg/face/special/generic/shield", 152 },
                { "reg/face/special/generic/sword", 153 },
                { "reg/face/special/generic/mana", 154 },
                { "reg/face/special/generic/summon", 155 },
                { "reg/face/special/generic/heal", 156 },
                { "reg/face/item/redflag", 157 },
                { "reg/face/scythe", 158 },
                { "reg/face/item/swordfleshpain", 159 },
                { "reg/face/item/manabomb", 160 },
                { "reg/face/item/wand-of-wand", 161 },
                { "reg/face/item/demon-horn", 162 },
                { "reg/face/item/charged-hammer", 163 },
                { "reg/face/item/infused-herbs", 164 },
                { "reg/face/item/potion-shard", 165 },
                { "reg/face/item/revive-potion", 166 },
                { "reg/face/item/mana-potion", 167 },
                { "reg/face/dmgeliminate", 168 },
                { "reg/face/poisonfang", 169 },
                { "reg/face/wolfbite", 170 },
                { "reg/face/slash", 171 },
                { "reg/face/hatch", 172 },
                { "reg/face/dmgtrio", 173 },
                { "reg/face/dmglucky", 174 },
                { "reg/face/dmgcritical", 175 },
                { "reg/face/special/target/justtargetally", 176 },
                { "reg/face/special/target/justtargetallypips", 177 },
                { "reg/face/special/target/justtargetallypipsall", 178 },
                { "reg/face/special/target/justtargetallyall", 179 },
                { "reg/face/special/target/justtargetenemy", 180 },
                { "reg/face/special/target/justtargetenemypips", 181 },
                { "reg/face/special/target/justtargetenemypipsall", 182 },
                { "reg/face/special/target/justtargetenemyall", 183 },
                { "reg/face/special/target/justtargetanypips", 184 },
                { "reg/face/special/target/justtargetany", 185 },
                { "reg/face/special/target/justtargetself", 186 },
                { "reg/face/special/target/justtargetselfpips", 187 }
            }
        },
        {
            "tin", new Dictionary<string, int>
            {
                { "small/face/blank", 0 },
                { "small/face/blankbug", 1 },
                { "small/face/petrify", 2 },
                { "small/face/selfhealvitality", 3 },
                { "small/face/weaken", 4 },
                { "small/face/sting", 5 },
                { "small/face/arrow", 6 },
                { "small/face/arroweliminate", 7 },
                { "small/face/bonestrike", 8 },
                { "small/face/nip", 9 },
                { "small/face/nippoison", 10 },
                { "small/face/curse", 11 },
                { "small/face/summonbones", 12 },
                { "small/face/summonslimelet", 13 },
                { "small/face/summonhexia", 14 },
                { "small/face/slime", 15 },
                { "small/face/hatch", 16 },
                { "small/face/grow", 17 }
            }
        },
        {
            "big", new Dictionary<string, int>
            {
                { "big/face/blankexerted", 0 },
                { "big/face/blankbug", 1 },
                { "big/face/finger", 2 },
                { "big/face/club", 3 },
                { "big/face/poisonCloud", 4 },
                { "big/face/jinx", 5 },
                { "big/face/poison", 6 },
                { "big/face/poisonapple", 7 },
                { "big/face/broomstick", 8 },
                { "big/face/brew", 9 },
                { "big/face/bats", 10 },
                { "big/face/gaze", 11 },
                { "big/face/chain", 12 },
                { "big/face/rockspray", 13 },
                { "big/face/rockfist", 14 },
                { "big/face/spikespray", 15 },
                { "big/face/stomp", 16 },
                { "big/face/punch", 17 },
                { "big/face/claw", 18 },
                { "big/face/peck", 19 },
                { "big/face/summonskeleton", 20 },
                { "big/face/summonimp", 21 },
                { "big/face/decay", 22 },
                { "big/face/updownblob", 23 },
                { "big/face/threeblobs", 24 },
                { "big/face/summonwolf", 25 },
                { "big/face/maul", 26 },
                { "big/face/wolfbite", 27 },
                { "big/face/sword", 28 },
                { "big/face/boar_bite", 29 },
                { "big/face/boar_gore", 30 },
                { "big/face/blank", 31 }
            }
        },
        {
            "hug", new Dictionary<string, int>
            {
                { "huge/face/blank", 0 },
                { "huge/face/blankbug", 1 },
                { "huge/face/blankexerted", 2 },
                { "huge/face/club", 3 },
                { "huge/face/stomp", 4 },
                { "huge/face/chomp", 5 },
                { "huge/face/devour", 6 },
                { "huge/face/infect", 7 },
                { "huge/face/hexia", 8 },
                { "huge/face/chill", 9 },
                { "huge/face/flame", 10 },
                { "huge/face/summonimp", 11 },
                { "huge/face/summondemon", 12 },
                { "huge/face/summonbones", 13 },
                { "huge/face/summonspider", 14 },
                { "huge/face/summonsaber", 15 },
                { "huge/face/summonslate", 16 },
                { "huge/face/poisonbreath", 17 },
                { "huge/face/staff", 18 },
                { "huge/face/updownblob", 19 },
                { "huge/face/weakenflanking", 20 },
                { "huge/face/deathbeam", 21 },
                { "huge/face/threeblobs", 22 },
                { "huge/face/groupinflictexert", 23 },
                { "huge/face/ear", 24 },
                { "huge/face/handskull", 25 },
                { "huge/face/inevitableskull", 26 },
                { "huge/face/chompselfheal", 27 }
            }
        }
    };

    private class ParsedSprite
    {
        public string originalPath;
        public string normalizedPath;
        public string prefix;
        public string leafName;
        public Rect rect;
        public int assignedId = -1;
    }

    // A stateless helper to strip structural folders, ensuring items match across both configurations
    private static string NormalizePath(string path)
    {
        string normalized = path.Replace('\\', '/').ToLower();

        if (normalized.StartsWith("3dlink/"))
        {
            normalized = normalized.Substring("3dlink/".Length);
        }
        if (normalized.StartsWith("extra/"))
        {
            normalized = normalized.Substring("extra/".Length);
        }

        // Unifies plural differences like "extra/items/poem" vs "item/poem"
        normalized = normalized.Replace("items/", "item/");

        return normalized;
    }

    private static class SpriteRegistry
    {
        private const string RegistryPath = "ProjectSettings/SpriteIdRegistry.json";

        [Serializable]
        private class Entry
        {
            public string prefix;
            public string path;
            public int id;
        }

        [Serializable]
        private class Wrapper
        {
            public List<Entry> entries = new List<Entry>();
        }

        private static readonly Dictionary<string, Dictionary<string, int>> Database = new Dictionary<string, Dictionary<string, int>>();

        public static void Load()
        {
            Database.Clear();
            if (!File.Exists(RegistryPath)) return;

            try
            {
                string json = File.ReadAllText(RegistryPath);
                var wrapper = JsonUtility.FromJson<Wrapper>(json);
                if (wrapper != null && wrapper.entries != null)
                {
                    foreach (var entry in wrapper.entries)
                    {
                        if (!Database.ContainsKey(entry.prefix))
                        {
                            Database[entry.prefix] = new Dictionary<string, int>();
                        }
                        Database[entry.prefix][entry.path] = entry.id;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AtlasProcessor] Failed to load sprite registry: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                var wrapper = new Wrapper();
                foreach (var prefixGroup in Database)
                {
                    foreach (var pathEntry in prefixGroup.Value)
                    {
                        wrapper.entries.Add(new Entry
                        {
                            prefix = prefixGroup.Key,
                            path = pathEntry.Key,
                            id = pathEntry.Value
                        });
                    }
                }
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(RegistryPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AtlasProcessor] Failed to save sprite registry: {ex.Message}");
            }
        }

        public static int GetOrAssignId(string prefix, string normalizedPath, HashSet<int> reservedIds)
        {
            if (!Database.ContainsKey(prefix))
                Database[prefix] = new Dictionary<string, int>();

            if (Database[prefix].TryGetValue(normalizedPath, out int existingId))
            {
                // CHANGE: If an old dynamic ID is now blocked by your hardcoded list, evict it!
                if (!reservedIds.Contains(existingId))
                {
                    return existingId;
                }
                else
                {
                    Database[prefix].Remove(normalizedPath);
                }
            }

            int nextId = 0;
            var assignedInPrefix = new HashSet<int>(Database[prefix].Values);
            while (reservedIds.Contains(nextId) || assignedInPrefix.Contains(nextId))
            {
                nextId++;
            }

            Database[prefix][normalizedPath] = nextId;
            return nextId;
        }
    }

    // --- INSIDE AtlasProcessor.cs ---
    // Replace the OnPreprocessTexture and ProcessAtlas methods with this:

    void OnPreprocessTexture()
    {
        string fileName = Path.GetFileName(assetPath);

        foreach (string target in TargetFileNames)
        {
            if (fileName == target)
            {
                ProcessAtlas(assetImporter, fileName);
                break;
            }
        }
    }

    private int GetGroupOrder(string path)
    {
        if (path.StartsWith("reg/face/")) return 0;
        if (path.StartsWith("big/face/")) return 1;
        if (path.StartsWith("huge/face/")) return 2;
        if (path.StartsWith("small/face/")) return 3;
        return 4;
    }

    private void ProcessAtlas(AssetImporter importer, string fileName)
    {
        TextureImporter textureImporter = (TextureImporter)importer;
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Multiple;

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(textureImporter);
        dataProvider.InitSpriteEditorDataProvider();

        string textPath = assetPath.Replace(".png", ".txt");
        if (!File.Exists(textPath))
        {
            Debug.LogError("Could not find atlas text file: " + textPath);
            return;
        }

        string[] lines = File.ReadAllLines(textPath);
        var parsedSprites = new List<ParsedSprite>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim().Replace('\\', '/');

            if (string.IsNullOrEmpty(line) ||
                line.Contains(":") ||
                line.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string originalPath = line;

            if (originalPath.StartsWith("3dlink/", StringComparison.OrdinalIgnoreCase))
            {
                originalPath = originalPath.Substring("3dlink/".Length);
            }

            string normalizedPath = NormalizePath(originalPath);
            string[] pathParts = originalPath.Split('/');
            string spriteSubName = pathParts[pathParts.Length - 1];

            string prefix = "Atm";
            if (originalPath.StartsWith("reg/face/")) prefix = "bas";
            else if (originalPath.StartsWith("small/face/")) prefix = "tin";
            else if (originalPath.StartsWith("big/face/")) prefix = "big";
            else if (originalPath.StartsWith("huge/face/")) prefix = "hug";
            else if (originalPath.StartsWith("portrait/")) prefix = "prt";
            else if (originalPath.StartsWith("trigger/")) prefix = "trg";
            else if (originalPath.StartsWith("ui/")) prefix = "ui_";
            else if (pathParts.Length >= 2)
            {
                string folderName = pathParts[pathParts.Length - 2].Trim();
                if (folderName.Length < 3) folderName = folderName.PadRight(3, '_');
                prefix = folderName.Substring(0, 3);
            }

            int x = 0, y = 0, w = 0, h = 0;
            bool foundXY = false, foundSize = false;

            for (int j = 1; j <= 6 && (i + j) < lines.Length; j++)
            {
                string metaLine = lines[i + j].Trim();
                if (!metaLine.Contains(":")) break;

                if (metaLine.StartsWith("xy:"))
                {
                    string[] parts = metaLine.Replace("xy:", "").Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out x);
                        int.TryParse(parts[1].Trim(), out y);
                        foundXY = true;
                    }
                }
                else if (metaLine.StartsWith("size:"))
                {
                    string[] parts = metaLine.Replace("size:", "").Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out w);
                        int.TryParse(parts[1].Trim(), out h);
                        foundSize = true;
                    }
                }
            }

            if (foundXY && foundSize)
            {
                parsedSprites.Add(new ParsedSprite
                {
                    originalPath = originalPath,
                    normalizedPath = normalizedPath,
                    prefix = prefix,
                    leafName = spriteSubName,
                    rect = new Rect(x, AtlasHeight - y - h, w, h)
                });
            }
        }

        parsedSprites.Sort((a, b) =>
        {
            int orderA = GetGroupOrder(a.originalPath);
            int orderB = GetGroupOrder(b.originalPath);
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return a.normalizedPath.CompareTo(b.normalizedPath);
        });

        SpriteRegistry.Load();

        var prefixGroups = new Dictionary<string, List<ParsedSprite>>();
        foreach (var sprite in parsedSprites)
        {
            if (!prefixGroups.ContainsKey(sprite.prefix))
                prefixGroups[sprite.prefix] = new List<ParsedSprite>();

            prefixGroups[sprite.prefix].Add(sprite);
        }

        var spriteRects = new List<SpriteRect>();

        foreach (var group in prefixGroups)
        {
            string prefix = group.Key;
            var list = group.Value;
            var reservedIds = new HashSet<int>();

            bool hasCustomMapping = PrefixCustomIds.TryGetValue(prefix, out var customMappings);
            if (hasCustomMapping)
            {
                foreach (var id in customMappings.Values) reservedIds.Add(id);
            }

            var validList = new List<ParsedSprite>();

            foreach (var sprite in list)
            {
                bool matchedHardcode = false;
                if (hasCustomMapping)
                {
                    foreach (var kvp in customMappings)
                    {
                        if (NormalizePath(kvp.Key) == sprite.normalizedPath)
                        {
                            sprite.assignedId = kvp.Value;
                            matchedHardcode = true;
                            break;
                        }
                    }
                }

                // If this is the Base Atlas, and it's a known game face,
                // but wasn't explicitly defined in the dictionary, it's Dev Garbage. Burn it.
                if (fileName == "base_atlas_image.png" && hasCustomMapping && !matchedHardcode)
                {
                    continue; // Discard completely
                }

                validList.Add(sprite);
            }

            for (int i = 0; i < validList.Count; i++)
            {
                var sprite = validList[i];
                if (sprite.assignedId == -1)
                {
                    // Spells strictly use alphabetical indexing and bypass the chronological registry
                    if (sprite.prefix == "spe")
                    {
                        sprite.assignedId = i;
                    }
                    else
                    {
                        sprite.assignedId = SpriteRegistry.GetOrAssignId(sprite.prefix, sprite.normalizedPath, reservedIds);
                    }
                }
            }

            foreach (var sprite in validList)
            {
                int width = Mathf.RoundToInt(sprite.rect.width);
                int height = Mathf.RoundToInt(sprite.rect.height);
                string finalName = $"{sprite.prefix}_{sprite.assignedId}_{sprite.leafName}_{width}x{height}";

                spriteRects.Add(new SpriteRect
                {
                    name = finalName,
                    rect = sprite.rect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        SpriteRegistry.Save();
        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();
    }

}