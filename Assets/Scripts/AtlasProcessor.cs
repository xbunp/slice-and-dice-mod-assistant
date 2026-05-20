using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public class AtlasProcessor : AssetPostprocessor
{
    private readonly string[] TargetFileNames = { "base_atlas_image.png", "community_atlas_image.png" };
    private const int AtlasHeight = 1024;

    // Explicit database mapping definitions to ensure accurate engine IDs
    private static readonly Dictionary<string, Dictionary<string, int>> PrefixCustomIds = new Dictionary<string, Dictionary<string, int>>
    {
        {
            "bas", new Dictionary<string, int>
            {
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
                { "reg/face/special/addKeyword/cleanseselfcleanse", 10 },
                { "reg/face/backstab", 11 },
                { "reg/face/pinkSkull", 12 },
                { "reg/face/special/addKeyword/old/cantripdeath", 13 },
                { "reg/face/dmgSelfMandatory", 14 },
                { "reg/face/sword", 15 },
                { "reg/face/dmgGrowth", 16 },
                { "reg/face/dmgEngage", 17 },
                { "reg/face/swordMagic", 18 },
                { "reg/face/dmgPain", 19 },
                { "reg/face/swordDeathwish", 20 },
                { "reg/face/dmgDeath", 21 },
                { "reg/face/dmgSerrated", 22 },
                { "reg/face/swordExert", 23 },
                { "reg/face/dmgDouble", 24 },
                { "reg/face/swordQuad", 25 },
                { "reg/face/dmgBloodlust", 26 },
                { "reg/face/dmgCopycat", 27 },
                { "reg/face/swordPristine", 28 },
                { "reg/face/dmgGuilt", 29 },
                { "reg/face/kriss", 30 },
                { "reg/face/shadowDagger", 31 },
                { "reg/face/swordFocus", 32 },
                { "reg/face/swordInspire", 33 },
                { "reg/face/swordAll", 34 },
                { "reg/face/resurrectMana", 35 },
                { "reg/face/swordCleave", 36 },
                { "reg/face/swordDescend", 37 },
                { "reg/face/dmgCleaveChain", 38 },
                { "reg/face/hammer", 39 },
                { "reg/face/quartzSlow", 40 },
                { "reg/face/shieldBash", 41 },
                { "reg/face/dmgCharged", 42 },
                { "reg/face/stun", 43 },
                { "reg/face/swordVulnerable", 44 },
                { "reg/face/dmgEra", 45 },
                { "reg/face/arrow", 46 },
                { "reg/face/arrowPoison", 47 },
                { "reg/face/arrowDuplicate", 48 },
                { "reg/face/arrowCleave", 49 },
                { "reg/face/arrowCopycat", 50 },
                { "reg/face/swordShield", 51 },
                { "reg/face/swordHeal", 52 },
                { "reg/face/dmgPoison", 53 },
                { "reg/face/plague", 54 },
                { "reg/face/dmgPoisonDose", 55 },
                { "reg/face/shield", 56 },
                { "reg/face/shieldFlesh", 57 },
                { "reg/face/shieldGrowth", 58 },
                { "reg/face/shieldPrecise", 59 },
                { "reg/face/shieldEnduringDeath", 60 },
                { "reg/face/shieldMagic", 61 },
                { "reg/face/shieldDoubleUse", 62 },
                { "reg/face/shieldSteel", 63 },
                { "reg/face/shieldRescue", 64 },
                { "reg/face/shieldPristine", 65 },
                { "reg/face/shieldCantrip", 66 },
                { "reg/face/shieldCopycat", 67 },
                { "reg/face/shieldFocus", 68 },
                { "reg/face/shieldPlusAdjacent", 69 },
                { "reg/face/shieldCharged", 70 },
                { "reg/face/shieldCure", 71 },
                { "reg/face/wardingChort", 72 },
                { "reg/face/flute", 73 },
                { "reg/face/shieldHeart", 74 },
                { "reg/face/smith", 75 },
                { "reg/face/mana", 76 },
                { "reg/face/manaCantrip", 77 },
                { "reg/face/manaCantripBoned", 78 },
                { "reg/face/manaGrowth", 79 },
                { "reg/face/manaDecay", 80 },
                { "reg/face/manaDeath", 81 },
                { "reg/face/manaPain", 82 },
                { "reg/face/manaBloodlust", 83 },
                { "reg/face/manaPair", 84 },
                { "reg/face/manaTriple", 85 },
                { "reg/face/healShieldMana", 86 },
                { "reg/face/manaDouble", 87 },
                { "reg/face/wandCharged", 88 },
                { "reg/face/wandJinx", 89 },
                { "reg/face/wandFire", 90 },
                { "reg/face/wandPoison", 91 },
                { "reg/face/wandBlood", 92 },
                { "reg/face/wandMana", 93 },
                { "reg/face/wandFightBonus", 94 },
                { "reg/face/wandWeaken", 95 },
                { "reg/face/wandFierce", 96 },
                { "reg/face/wandEcho", 97 },
                { "reg/face/wandDispel", 98 },
                { "reg/face/wandResilient", 99 },
                { "reg/face/wandStun", 100 },
                { "reg/face/wandChaos", 101 },
                { "reg/face/item/sceptre", 102 },
                { "reg/face/heal", 103 },
                { "reg/face/wandHeal", 104 },
                { "reg/face/boon", 105 },
                { "reg/face/healRescue", 106 },
                { "reg/face/healAll", 107 },
                { "reg/face/healBuff", 108 },
                { "reg/face/healCleave", 109 },
                { "reg/face/healRegen", 110 },
                { "reg/face/healUncurse", 111 },
                { "reg/face/healMagic", 112 },
                { "reg/face/healGroooooowth", 113 },
                { "reg/face/healDouble", 114 },
                { "reg/face/stick", 115 },
                { "reg/face/kill", 116 },
                { "reg/face/undying", 117 },
                { "reg/face/taunt", 118 },
                { "reg/face/revenge", 119 },
                { "reg/face/shieldCrescent", 120 },
                { "reg/face/shieldPain", 121 },
                { "reg/face/headshot", 122 },
                { "reg/face/dodge", 123 },
                { "reg/face/dodgeCantrip", 124 },
                { "reg/face/rerollCantrip", 125 },
                { "reg/face/dmgCantrip", 126 },
                { "reg/face/swordRoulette", 127 },
                { "reg/face/flurry", 128 },
                { "reg/face/special/addKeyword/needle", 129 },
                { "reg/face/recharge", 130 },
                { "reg/face/dmgWeaken", 131 },
                { "reg/face/swordDuplicate", 132 },
                { "reg/face/shieldDuplicate", 133 },
                { "reg/face/manaDuplicate", 134 },
                { "reg/face/rangedEngage", 135 },
                { "reg/face/resurrect", 136 },
                { "reg/face/dmgRampage", 137 },
                { "reg/face/special/addKeyword/doubleuse", 138 },
                { "reg/face/special/addKeyword/cantrip", 139 },
                { "reg/face/special/addKeyword/nothing", 140 },
                { "reg/face/special/addKeyword/copycat", 141 },
                { "reg/face/special/addKeyword/addCleaveSingleUse", 142 },
                { "reg/face/special/addKeyword/addCruelDeathwish", 143 },
                { "reg/face/special/addKeyword/addManaGain", 144 },
                { "reg/face/special/addKeyword/addPoison", 145 },
                { "reg/face/special/addKeyword/addSelfShield", 146 },
                { "reg/face/special/addKeyword/addSelfHeal", 147 },
                { "reg/face/special/addKeyword/addSelfHealSelfShield", 148 },
                { "reg/face/special/addKeyword/addManaGainPain", 149 },
                { "reg/face/special/addKeyword/engage", 150 },
                { "reg/face/special/addKeyword/growth", 151 },
                { "reg/face/special/generic/shield", 152 },
                { "reg/face/special/generic/sword", 153 },
                { "reg/face/special/generic/mana", 154 },
                { "reg/face/special/generic/summon", 155 },
                { "reg/face/special/generic/heal", 156 },
                { "reg/face/item/redFlag", 157 },
                { "reg/face/scythe", 158 },
                { "reg/face/item/swordFleshPain", 159 },
                { "reg/face/item/manaBomb", 160 },
                { "reg/face/item/wand-of-wand", 161 },
                { "reg/face/item/demon-horn", 162 },
                { "reg/face/item/charged-hammer", 163 },
                { "reg/face/item/infused-herbs", 164 },
                { "reg/face/item/potion-shard", 165 },
                { "reg/face/item/revive-potion", 166 },
                { "reg/face/item/mana-potion", 167 },
                { "reg/face/dmgEliminate", 168 },
                { "reg/face/poisonFang", 169 },
                { "reg/face/wolfBite", 170 },
                { "reg/face/slash", 171 },
                { "reg/face/hatch", 172 },
                { "reg/face/dmgTrio", 173 },
                { "reg/face/dmgLucky", 174 },
                { "reg/face/dmgCritical", 175 },
                { "reg/face/special/target/justTargetAlly", 176 },
                { "reg/face/special/target/justTargetAllyPips", 177 },
                { "reg/face/special/target/justTargetAllyPipsAll", 178 },
                { "reg/face/special/target/justTargetAllyAll", 179 },
                { "reg/face/special/target/justTargetEnemy", 180 },
                { "reg/face/special/target/justTargetEnemyPips", 181 },
                { "reg/face/special/target/justTargetEnemyPipsAll", 182 },
                { "reg/face/special/target/justTargetEnemyAll", 183 },
                { "reg/face/special/target/justTargetAnyPips", 184 },
                { "reg/face/special/target/justTargetAny", 185 },
                { "reg/face/special/target/justTargetSelf", 186 },
                { "reg/face/special/target/justTargetSelfPips", 187 },

                // Non-Regular categoric assignments
                { "big/face/wolfBite", 215 },
                { "huge/face/summonSaber", 235 },
                { "small/face/boneStrike", 256 }
            }
        }
    };

    private class ParsedSprite
    {
        public string originalPath;
        public string prefix;
        public string leafName;
        public Rect rect;
        public int assignedId = -1;
    }

    void OnPreprocessTexture()
    {
        string fileName = Path.GetFileName(assetPath);

        foreach (string target in TargetFileNames)
        {
            if (fileName == target)
            {
                ProcessAtlas(assetImporter, target);
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

    private void ProcessAtlas(AssetImporter importer, string targetFileName)
    {
        TextureImporter textureImporter = (TextureImporter)importer;
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Multiple;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(textureImporter);
        dataProvider.InitSpriteEditorDataProvider();

        string textPath = assetPath.Replace(".png", ".txt");
        if (!File.Exists(textPath))
        {
            Debug.LogError("Could not find: " + textPath);
            return;
        }

        string[] lines = File.ReadAllLines(textPath);
        var parsedSprites = new List<ParsedSprite>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.Contains(":") || line.EndsWith(".png"))
                continue;

            string originalPath = line;
            string[] pathParts = originalPath.Split('/');
            string spriteSubName = pathParts[pathParts.Length - 1];

            // Assign unified bas prefix to all base faces regardless of size
            string prefix = "Atm";
            if (originalPath.StartsWith("reg/face/") ||
                originalPath.StartsWith("big/face/") ||
                originalPath.StartsWith("huge/face/") ||
                originalPath.StartsWith("small/face/"))
            {
                prefix = "bas";
            }
            else if (pathParts.Length >= 2)
            {
                string folderName = pathParts[pathParts.Length - 2].Trim();
                if (folderName.Length < 3)
                    folderName = folderName.PadRight(3, '_');
                prefix = folderName.Substring(0, 3);
            }

            // Parsing Metadata
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
                    prefix = prefix,
                    leafName = spriteSubName,
                    rect = new Rect(x, AtlasHeight - y - h, w, h)
                });
            }
        }

        // Sort all parsed elements based on size group (reg -> big -> huge -> small) then original relative path
        parsedSprites.Sort((a, b) =>
        {
            int orderA = GetGroupOrder(a.originalPath);
            int orderB = GetGroupOrder(b.originalPath);
            if (orderA != orderB)
            {
                return orderA.CompareTo(orderB);
            }
            return a.originalPath.CompareTo(b.originalPath);
        });

        // Group sprites by prefix to isolate scope counts
        var prefixGroups = new Dictionary<string, List<ParsedSprite>>();
        foreach (var sprite in parsedSprites)
        {
            if (!prefixGroups.ContainsKey(sprite.prefix))
            {
                prefixGroups[sprite.prefix] = new List<ParsedSprite>();
            }
            prefixGroups[sprite.prefix].Add(sprite);
        }

        var spriteRects = new List<SpriteRect>();

        foreach (var group in prefixGroups)
        {
            string prefix = group.Key;
            var list = group.Value;
            var allocatedIds = new HashSet<int>();

            // First Pass: Assign custom exact ID mappings
            if (PrefixCustomIds.ContainsKey(prefix))
            {
                var customMappings = PrefixCustomIds[prefix];
                foreach (var sprite in list)
                {
                    if (customMappings.ContainsKey(sprite.originalPath))
                    {
                        int customId = customMappings[sprite.originalPath];
                        sprite.assignedId = customId;
                        allocatedIds.Add(customId);
                    }
                }
            }

            // Second Pass: Fill remaining sequential indices
            int nextId = 0;
            foreach (var sprite in list)
            {
                if (sprite.assignedId == -1)
                {
                    while (allocatedIds.Contains(nextId))
                    {
                        nextId++;
                    }
                    sprite.assignedId = nextId;
                    allocatedIds.Add(nextId);
                    nextId++;
                }
            }

            // Create SpriteRect configurations
            foreach (var sprite in list)
            {
                string finalName = $"{sprite.prefix}_{sprite.assignedId}_{sprite.leafName}";
                spriteRects.Add(new SpriteRect
                {
                    name = finalName,
                    rect = sprite.rect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();
    }
}
/*
public class AtlasProcessor : AssetPostprocessor
{
    // Add your atlas filenames here
    private readonly string[] TargetFileNames = { "base_atlas_image.png", "community_atlas_image.png" };
    private const int AtlasHeight = 1024;

    void OnPreprocessTexture()
    {
        string fileName = Path.GetFileName(assetPath);

        foreach (string target in TargetFileNames)
        {
            if (fileName == target)
            {
                ProcessAtlas(assetImporter, target);
                break;
            }
        }
    }

    private void ProcessAtlas(AssetImporter importer, string targetFileName)
    {
        TextureImporter textureImporter = (TextureImporter)importer;
        textureImporter.textureType = TextureImporterType.Sprite;
        textureImporter.spriteImportMode = SpriteImportMode.Multiple;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(textureImporter);
        dataProvider.InitSpriteEditorDataProvider();

        string textPath = assetPath.Replace(".png", ".txt");
        if (!File.Exists(textPath))
        {
            Debug.LogError("Could not find: " + textPath);
            return;
        }

        string[] lines = File.ReadAllLines(textPath);
        var spriteRects = new List<SpriteRect>();
        Dictionary<string, int> folderCounters = new Dictionary<string, int>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.Contains(":") || line.EndsWith(".png"))
                continue;

            // Logic to handle naming based on folder path
            string[] pathParts = line.Split('/');
            string folderName = "Atm";
            string spriteSubName = pathParts[pathParts.Length - 1];

            if (pathParts.Length >= 2)
            {
                // If it's in a folder, use the folder name for the prefix
                folderName = pathParts[pathParts.Length - 2];
            }
            else
            {
                // If at root, use the filename itself
                folderName = pathParts[0];
            }

            folderName = folderName.Trim();
            if (folderName.Length < 3) folderName = folderName.PadRight(3, '_');
            string prefix = folderName.Substring(0, 3);

            if (!folderCounters.ContainsKey(prefix)) folderCounters[prefix] = 0;
            string finalName = $"{prefix}_{folderCounters[prefix]}_{spriteSubName}";
            folderCounters[prefix]++;

            // Parsing Metadata
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
                spriteRects.Add(new SpriteRect
                {
                    name = finalName,
                    rect = new Rect(x, AtlasHeight - y - h, w, h),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();
    }
}
*/