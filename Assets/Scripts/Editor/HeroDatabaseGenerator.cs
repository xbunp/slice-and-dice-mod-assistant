#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HeroSpriteDatabaseGenerator
{
    private const string AtlasPath = "base_atlas_image";
    private const string OutputFilePath = "Assets/Scripts/HeroSpriteDatabase.cs";

    // Split raw filename into "Nice Name" and "Suffix" (e.g., "aceR2U1" -> "ace" and "R2U1")
    private static readonly Regex FileNameRegex = new Regex(
        @"^([a-z0-9\-]+)((?:[A-Z]\d+)*)$",
        RegexOptions.Compiled
    );

    private class SpriteMetadata
    {
        public string AtlasPath { get; set; }        // e.g. "portrait/hero/blue/aceR2U1"
        public string UnitySpriteName { get; set; }   // e.g. "prt_aceR2U1"
        public string Category { get; set; }          // "hero" or "monster"
        public string NiceName { get; set; }
        public string Suffix { get; set; }
        public string EnumIdentifier { get; set; }
    }

    [MenuItem("Tools/Generate Sprite Database")]
    public static void GenerateDatabase()
    {
        // 1. Load the sliced sprites from the Unity project
        Sprite[] slicedSprites = Resources.LoadAll<Sprite>(AtlasPath);
        if (slicedSprites == null || slicedSprites.Length == 0)
        {
            Debug.LogError($"[Generator] Failed to load sliced sprites from Resources/{AtlasPath}. Ensure the png file is imported and sliced properly.");
            return;
        }

        // Filter and map ONLY sliced sprites that are prefixed with "prt_" (portraits)
        Dictionary<string, string> unitySpriteMap = new Dictionary<string, string>();
        foreach (Sprite sprite in slicedSprites)
        {
            if (sprite.name.StartsWith("prt_", StringComparison.OrdinalIgnoreCase))
            {
                string key = sprite.name.ToLowerInvariant();
                if (!unitySpriteMap.ContainsKey(key))
                {
                    unitySpriteMap.Add(key, sprite.name);
                }
            }
        }

        // 2. Load the atlas layout text file
        TextAsset atlasText = Resources.Load<TextAsset>(AtlasPath);
        if (atlasText == null)
        {
            Debug.LogError($"[Generator] Failed to load layout text file from Resources/{AtlasPath}. Ensure it is a .txt file inside a Resources folder.");
            return;
        }

        List<SpriteMetadata> parsedSprites = new List<SpriteMetadata>();
        string[] lines = atlasText.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            string spritePath = line.Trim();

            if (spritePath.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            bool isHero = spritePath.StartsWith("portrait/hero/", StringComparison.OrdinalIgnoreCase);
            bool isMonster = spritePath.StartsWith("portrait/monster/", StringComparison.OrdinalIgnoreCase);

            if (isHero || isMonster)
            {
                string category = isHero ? "hero" : "monster";
                string[] pathParts = spritePath.Split('/');
                string fileName = pathParts.Last();

                Match match = FileNameRegex.Match(fileName);
                if (match.Success)
                {
                    string niceName = match.Groups[1].Value;
                    string suffix = match.Groups[2].Value;

                    // Match against Unity's sanitized asset naming scheme (prefixed with "prt_")
                    string targetSearchName = $"prt_{fileName}".ToLowerInvariant();
                    string matchedSpriteName = null;

                    if (unitySpriteMap.TryGetValue(targetSearchName, out string exactName))
                    {
                        matchedSpriteName = exactName;
                    }
                    else
                    {
                        // Fallback partial matching
                        var fallbackKey = unitySpriteMap.Keys.FirstOrDefault(k => k.Contains(fileName.ToLowerInvariant()));
                        if (fallbackKey != null)
                        {
                            matchedSpriteName = unitySpriteMap[fallbackKey];
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedSpriteName))
                    {
                        parsedSprites.Add(new SpriteMetadata
                        {
                            AtlasPath = spritePath,
                            UnitySpriteName = matchedSpriteName,
                            Category = category,
                            NiceName = niceName,
                            Suffix = suffix,
                            EnumIdentifier = ToPascalCase(niceName)
                        });
                    }
                }
            }
        }

        var heroes = parsedSprites.Where(s => s.Category == "hero").ToList();
        var monsters = parsedSprites.Where(s => s.Category == "monster").ToList();

        GenerateDatabaseFile(heroes, monsters);
    }

    private static void GenerateDatabaseFile(List<SpriteMetadata> heroes, List<SpriteMetadata> monsters)
    {
        var uniqueHeroes = GetUniqueEnumMappings(heroes);
        var uniqueMonsters = GetUniqueEnumMappings(monsters);

        StringBuilder fileContent = new StringBuilder();
        fileContent.AppendLine("// ==========================================================================");
        fileContent.AppendLine("// <auto-generated>");
        fileContent.AppendLine("//     This code was generated by a tool.");
        fileContent.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        fileContent.AppendLine("//     the code is regenerated.");
        fileContent.AppendLine("// </auto-generated>");
        fileContent.AppendLine("// ==========================================================================");
        fileContent.AppendLine("using System.Collections.Generic;");
        fileContent.AppendLine();
        fileContent.AppendLine("public static class HeroSpriteDatabase");
        fileContent.AppendLine("{");

        // 1. HeroToSpriteMap
        fileContent.AppendLine("    public static readonly Dictionary<HeroType, string> HeroToSpriteMap = new Dictionary<HeroType, string>");
        fileContent.AppendLine("    {");
        foreach (var kvp in uniqueHeroes.OrderBy(k => k.Key))
        {
            fileContent.AppendLine($"        {{ HeroType.{kvp.Key}, \"{kvp.Value.UnitySpriteName}\" }},");
        }
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 2. SpriteToHeroMap
        fileContent.AppendLine("    public static readonly Dictionary<string, HeroType> SpriteToHeroMap = new Dictionary<string, HeroType>");
        fileContent.AppendLine("    {");
        HashSet<string> writtenHeroSprites = new HashSet<string>();
        foreach (var hero in heroes)
        {
            if (uniqueHeroes.ContainsKey(hero.EnumIdentifier) && !writtenHeroSprites.Contains(hero.UnitySpriteName))
            {
                fileContent.AppendLine($"        {{ \"{hero.UnitySpriteName}\", HeroType.{hero.EnumIdentifier} }},");
                writtenHeroSprites.Add(hero.UnitySpriteName);
            }
        }
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 3. MonsterToSpriteMap
        fileContent.AppendLine("    public static readonly Dictionary<MonsterType, string> MonsterToSpriteMap = new Dictionary<MonsterType, string>");
        fileContent.AppendLine("    {");
        foreach (var kvp in uniqueMonsters.OrderBy(k => k.Key))
        {
            fileContent.AppendLine($"        {{ MonsterType.{kvp.Key}, \"{kvp.Value.UnitySpriteName}\" }},");
        }
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 4. SpriteToMonsterMap
        fileContent.AppendLine("    public static readonly Dictionary<string, MonsterType> SpriteToMonsterMap = new Dictionary<string, MonsterType>");
        fileContent.AppendLine("    {");
        HashSet<string> writtenMonsterSprites = new HashSet<string>();
        foreach (var monster in monsters)
        {
            if (uniqueMonsters.ContainsKey(monster.EnumIdentifier) && !writtenMonsterSprites.Contains(monster.UnitySpriteName))
            {
                fileContent.AppendLine($"        {{ \"{monster.UnitySpriteName}\", MonsterType.{monster.EnumIdentifier} }},");
                writtenMonsterSprites.Add(monster.UnitySpriteName);
            }
        }
        fileContent.AppendLine("    };");

        fileContent.AppendLine("}");

        string directory = Path.GetDirectoryName(OutputFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(OutputFilePath, fileContent.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[Generator] Database script updated at {OutputFilePath}");
    }

    private static Dictionary<string, SpriteMetadata> GetUniqueEnumMappings(List<SpriteMetadata> items)
    {
        var result = new Dictionary<string, SpriteMetadata>();
        var groupedByEnum = items.GroupBy(i => i.EnumIdentifier);

        foreach (var group in groupedByEnum)
        {
            string enumKey = group.Key;

            var sortedCandidates = group.OrderByDescending(i => i.Suffix).ToList();
            var chosenSprite = sortedCandidates.First();

            if (!result.ContainsKey(enumKey))
            {
                result.Add(enumKey, chosenSprite);
            }
        }

        return result;
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "None";

        string[] words = input.Split('-');
        StringBuilder sb = new StringBuilder();

        foreach (string word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpper(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word.Substring(1).ToLowerInvariant());
                }
            }
        }

        string result = sb.ToString();

        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return string.IsNullOrEmpty(result) ? "None" : result;
    }
}
#endif

// OLD VERSION WHICH REGENERATES CERTAIN ENUM DICTIONARIES.

/*
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HeroDatabaseGenerator
{
    // Make sure this matches your exact file name in the Resources folder (without the .txt extension)
    private const string AtlasPath = "base_atlas_image";
    private const string OutputFilePath = "Assets/Scripts/HeroSpriteDatabase.cs";

    // Regex to split the file name into a "Nice Name" and "Suffix"
    // e.g., "aceR2U1" -> Group 1: "ace", Group 2: "R2U1"
    private static readonly Regex FileNameRegex = new Regex(
        @"^([a-z0-9\-]+)((?:[A-Z]\d+)*)$",
        RegexOptions.Compiled
    );

    public enum HeroColor
    {
        None = 0,
        Orange,
        Yellow,
        Grey,
        Blue,
        Red,
        Green,
        Violet,
        Unknown
    }

    private class SpriteMetadata
    {
        public string FullPath { get; set; }
        public string Category { get; set; } // "hero" or "monster"
        public string NiceName { get; set; }
        public string Suffix { get; set; }
        public string EnumIdentifier { get; set; }
        public HeroColor Color { get; set; }
    }

    [MenuItem("Tools/Generate Sprite Database")]
    public static void GenerateDatabase()
    {
        // Load the atlas as a raw text file
        TextAsset atlasText = Resources.Load<TextAsset>(AtlasPath);

        if (atlasText == null)
        {
            Debug.LogError($"[Generator] Failed to load text file from Resources/{AtlasPath}. Ensure it is a .txt file inside a Resources folder.");
            return;
        }

        List<SpriteMetadata> parsedSprites = new List<SpriteMetadata>();
        string[] lines = atlasText.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            // Atlas block headers (paths) never start with a space.
            // Properties like "  rotate: false" always start with whitespace.
            if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            string spritePath = line.Trim();

            // Skip placeholders and non-character paths
            if (spritePath.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            bool isHero = spritePath.StartsWith("portrait/hero/", StringComparison.OrdinalIgnoreCase);
            bool isMonster = spritePath.StartsWith("portrait/monster/", StringComparison.OrdinalIgnoreCase);

            if (isHero || isMonster)
            {
                string category = isHero ? "hero" : "monster";
                string[] pathParts = spritePath.Split('/');
                string fileName = pathParts.Last();

                Match match = FileNameRegex.Match(fileName);
                if (match.Success)
                {
                    string niceName = match.Groups[1].Value;
                    string suffix = match.Groups[2].Value;

                    HeroColor color = HeroColor.None;
                    if (isHero)
                    {
                        color = InferHeroColor(spritePath, niceName);
                    }

                    parsedSprites.Add(new SpriteMetadata
                    {
                        FullPath = spritePath,
                        Category = category,
                        NiceName = niceName,
                        Suffix = suffix,
                        EnumIdentifier = ToPascalCase(niceName),
                        Color = color
                    });
                }
            }
        }

        var heroes = parsedSprites.Where(s => s.Category == "hero").ToList();
        var monsters = parsedSprites.Where(s => s.Category == "monster").ToList();

        GenerateDatabaseFile(heroes, monsters);
    }

    private static HeroColor InferHeroColor(string fullPath, string niceName)
    {
        string lowerPath = fullPath.ToLowerInvariant();

        if (lowerPath.Contains("/orange/")) return HeroColor.Orange;
        if (lowerPath.Contains("/yellow/")) return HeroColor.Yellow;
        if (lowerPath.Contains("/grey/") || lowerPath.Contains("/gray/")) return HeroColor.Grey;
        if (lowerPath.Contains("/blue/")) return HeroColor.Blue;
        if (lowerPath.Contains("/red/")) return HeroColor.Red;
        if (lowerPath.Contains("/green/")) return HeroColor.Green;
        if (lowerPath.Contains("/violet/")) return HeroColor.Violet;

        if (niceName.Equals("glitch", StringComparison.OrdinalIgnoreCase))
        {
            return HeroColor.Violet;
        }

        if (lowerPath.Contains("/special/generated/"))
        {
            string nameLower = niceName.ToLowerInvariant();
            if (nameLower.StartsWith("y")) return HeroColor.Yellow;
            if (nameLower.StartsWith("b")) return HeroColor.Blue;
            if (nameLower.StartsWith("r")) return HeroColor.Red;
            if (nameLower.StartsWith("o")) return HeroColor.Orange;
            if (nameLower.StartsWith("n")) return HeroColor.Grey; // n for neutral generated
            if (nameLower.StartsWith("gr") || nameLower.StartsWith("gy")) return HeroColor.Grey;
            if (nameLower.StartsWith("g")) return HeroColor.Green;
            if (nameLower.StartsWith("v")) return HeroColor.Violet;
        }

        return HeroColor.Unknown;
    }

    private static void GenerateDatabaseFile(List<SpriteMetadata> heroes, List<SpriteMetadata> monsters)
    {
        var uniqueHeroes = GetUniqueEnumMappings(heroes);
        var uniqueMonsters = GetUniqueEnumMappings(monsters);

        StringBuilder fileContent = new StringBuilder();
        fileContent.AppendLine("// ==========================================================================");
        fileContent.AppendLine("// <auto-generated>");
        fileContent.AppendLine("//     This code was generated by a tool.");
        fileContent.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        fileContent.AppendLine("//     the code is regenerated.");
        fileContent.AppendLine("// </auto-generated>");
        fileContent.AppendLine("// ==========================================================================");
        fileContent.AppendLine("using System.Collections.Generic;");
        fileContent.AppendLine();

        // Generate Hero Enum
        fileContent.AppendLine("public enum HeroType");
        fileContent.AppendLine("{");
        fileContent.AppendLine("    None = 0,");
        foreach (var hero in uniqueHeroes.Keys.OrderBy(k => k)) fileContent.AppendLine($"    {hero},");
        fileContent.AppendLine("}");
        fileContent.AppendLine();

        // Generate Monster Enum
        fileContent.AppendLine("public enum MonsterType");
        fileContent.AppendLine("{");
        fileContent.AppendLine("    None = 0,");
        foreach (var monster in uniqueMonsters.Keys.OrderBy(k => k)) fileContent.AppendLine($"    {monster},");
        fileContent.AppendLine("}");
        fileContent.AppendLine();

        // Generate HeroColor Enum
        fileContent.AppendLine("public enum HeroColor");
        fileContent.AppendLine("{");
        foreach (var colorName in Enum.GetNames(typeof(HeroColor))) fileContent.AppendLine($"    {colorName},");
        fileContent.AppendLine("}");
        fileContent.AppendLine();

        fileContent.AppendLine("public static class HeroSpriteDatabase");
        fileContent.AppendLine("{");

        // 1. HeroToSpriteMap
        fileContent.AppendLine("    public static readonly Dictionary<HeroType, string> HeroToSpriteMap = new Dictionary<HeroType, string>");
        fileContent.AppendLine("    {");
        foreach (var kvp in uniqueHeroes.OrderBy(k => k.Key))
            fileContent.AppendLine($"        {{ HeroType.{kvp.Key}, \"{kvp.Value.FullPath}\" }},");
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 2. SpriteToHeroMap
        fileContent.AppendLine("    public static readonly Dictionary<string, HeroType> SpriteToHeroMap = new Dictionary<string, HeroType>");
        fileContent.AppendLine("    {");
        foreach (var hero in heroes)
        {
            if (uniqueHeroes.ContainsKey(hero.EnumIdentifier))
                fileContent.AppendLine($"        {{ \"{hero.FullPath}\", HeroType.{hero.EnumIdentifier} }},");
        }
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 3. HeroColorMap
        fileContent.AppendLine("    public static readonly Dictionary<HeroType, HeroColor> HeroColorMap = new Dictionary<HeroType, HeroColor>");
        fileContent.AppendLine("    {");
        foreach (var kvp in uniqueHeroes.OrderBy(k => k.Key))
            fileContent.AppendLine($"        {{ HeroType.{kvp.Key}, HeroColor.{kvp.Value.Color} }},");
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 4. MonsterToSpriteMap
        fileContent.AppendLine("    public static readonly Dictionary<MonsterType, string> MonsterToSpriteMap = new Dictionary<MonsterType, string>");
        fileContent.AppendLine("    {");
        foreach (var kvp in uniqueMonsters.OrderBy(k => k.Key))
            fileContent.AppendLine($"        {{ MonsterType.{kvp.Key}, \"{kvp.Value.FullPath}\" }},");
        fileContent.AppendLine("    };");
        fileContent.AppendLine();

        // 5. SpriteToMonsterMap
        fileContent.AppendLine("    public static readonly Dictionary<string, MonsterType> SpriteToMonsterMap = new Dictionary<string, MonsterType>");
        fileContent.AppendLine("    {");
        foreach (var monster in monsters)
        {
            if (uniqueMonsters.ContainsKey(monster.EnumIdentifier))
                fileContent.AppendLine($"        {{ \"{monster.FullPath}\", MonsterType.{monster.EnumIdentifier} }},");
        }
        fileContent.AppendLine("    };");

        fileContent.AppendLine("}");

        string directory = Path.GetDirectoryName(OutputFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(OutputFilePath, fileContent.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[Generator] Found {heroes.Count} Hero variations and {monsters.Count} Monster variations.");
        Debug.Log($"[Generator] Database script generated successfully at {OutputFilePath}");
    }

    private static Dictionary<string, SpriteMetadata> GetUniqueEnumMappings(List<SpriteMetadata> items)
    {
        var result = new Dictionary<string, SpriteMetadata>();
        var groupedByEnum = items.GroupBy(i => i.EnumIdentifier);

        foreach (var group in groupedByEnum)
        {
            string enumKey = group.Key;

            // Order descending to put R3U1 above R1U1, etc.
            var sortedCandidates = group.OrderByDescending(i => i.Suffix).ToList();
            var chosenSprite = sortedCandidates.First();

            if (!result.ContainsKey(enumKey))
            {
                result.Add(enumKey, chosenSprite);
            }
        }

        return result;
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "None";

        string[] words = input.Split('-');
        StringBuilder sb = new StringBuilder();

        foreach (string word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpper(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word.Substring(1).ToLowerInvariant());
                }
            }
        }

        string result = sb.ToString();

        // Enums can't start with numbers (e.g. "0mbie" from z0mbie handles fine, but if the word literally was "1", it would need an underscore)
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return string.IsNullOrEmpty(result) ? "None" : result;
    }
}
#endif
*/