using SliceDiceTextMod;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holding class containing logic ripped out from HeroUI.
/// Recommended locations for each chunk are noted above their declarations.
/// </summary>
public static class EntityUIHelpers
{
    // =====================================================================
    // PORT: ASSET CACHING AND RESOLUTION STATE
    // Recommendation: Relocate to an Asset Management, Addressable or Sprite Registry module.
    // =====================================================================

    public static Sprite[] BaseActionSprites { get; private set; }
    public static Sprite[] AllActionSprites { get; private set; }

    public static void Initialize()
    {
        LoadAllSprites();
    }

    private static void LoadAllSprites()
    {
        Sprite[] baseAtlas = SpriteCache.GetBaseSprites();
        Sprite[] commAtlas = SpriteCache.GetCommunitySprites();

        List<Sprite> allSp = new List<Sprite>();

        if (baseAtlas != null)
        {
            for (int i = 0; i < baseAtlas.Length; i++)
            {
                if (baseAtlas[i] != null && !IsForbidden(baseAtlas[i].name))
                {
                    allSp.Add(baseAtlas[i]);
                }
            }
        }

        if (commAtlas != null)
        {
            for (int i = 0; i < commAtlas.Length; i++)
            {
                if (commAtlas[i] != null && !IsForbidden(commAtlas[i].name))
                {
                    allSp.Add(commAtlas[i]);
                }
            }
        }

        AllActionSprites = allSp.ToArray();

        List<Sprite> basSp = new List<Sprite>();
        for (int i = 0; i < AllActionSprites.Length; i++)
        {
            string sName = AllActionSprites[i].name;
            if (sName.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
            {
                basSp.Add(AllActionSprites[i]);
            }
        }

        BaseActionSprites = basSp.ToArray();
    }

    // =====================================================================
    // PORT: PORTRAIT / SPRITE FILTERING RULES
    // Recommendation: Relocate to Content Validation, Rule Configs or SDData extension.
    // =====================================================================

    private static readonly HashSet<string> AllowedBasePrefixes = new HashSet<string>
    {
        "bas", "ite", "Lem", "eba", "pos", "Ese", "kas", "Eme", "dee", "har",
        "Spi", "Yca", "Ber", "Sef", "Leo", "Col", "OkN", "Mut", "Ric", "dar", "sym", "Sea",
        "Bal", "The", "ale", "Dog", "the", "Can", "Liz", "Che", "Ale", "dan", "PEP", "Aid",
        "Enc", "Ksy", "pow", "Fre", "Med", "Sul"
    };

    public static bool IsSpriteValid(Sprite sprite)
    {
        if (sprite == null || IsForbidden(sprite.name)) return false;

        string spriteName = sprite.name;
        string textureName = sprite.texture != null ? sprite.texture.name : string.Empty;

        bool isBaseAtlas = textureName.Contains("base_atlas");
        if (isBaseAtlas)
        {
            int underscoreIndex = spriteName.IndexOf('_');
            string prefix = underscoreIndex > 0 ? spriteName.Substring(0, underscoreIndex) : string.Empty;
            return AllowedBasePrefixes.Contains(prefix);
        }
        else
        {
            if (spriteName.StartsWith("sma_", StringComparison.OrdinalIgnoreCase) ||
                spriteName.StartsWith("old_", StringComparison.OrdinalIgnoreCase) ||
                spriteName.StartsWith("spe_", StringComparison.OrdinalIgnoreCase) ||
                spriteName.StartsWith("key_", StringComparison.OrdinalIgnoreCase) ||
                spriteName.StartsWith("lap_", StringComparison.OrdinalIgnoreCase) ||
                spriteName.StartsWith("alp_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsForbidden(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        for (int i = 0; i < RandomSDData.UnusuablePortraits.Length; i++)
        {
            if (name.IndexOf(RandomSDData.UnusuablePortraits[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // =====================================================================
    // PORT: SPRITE IDENTIFIER RESOLUTION LOGIC
    // Recommendation: Relocate to a Lookup Engine or static Registry / Database.
    // =====================================================================

    public static Sprite GetSpriteForPortrait(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        Sprite directMatch = Array.Find(AllActionSprites, s => s != null && s.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (directMatch != null) return directMatch;

        string targetSpriteName = null;

        if (Enum.TryParse(name, true, out HeroType hero))
        {
            foreach (var kvp in HeroSpriteDatabase.SpriteToHeroMap)
            {
                if (kvp.Value == hero)
                {
                    targetSpriteName = kvp.Key;
                    break;
                }
            }
        }

        if (targetSpriteName == null && Enum.TryParse(name, true, out MonsterType monster))
        {
            foreach (var kvp in HeroSpriteDatabase.SpriteToMonsterMap)
            {
                if (kvp.Value == monster)
                {
                    targetSpriteName = kvp.Key;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(targetSpriteName))
        {
            return Array.Find(AllActionSprites, s => s != null && s.name.Equals(targetSpriteName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    public static string GetPortraitDisplayName(Sprite sprite)
    {
        if (sprite == null) return string.Empty;
        if (HeroSpriteDatabase.SpriteToHeroMap.TryGetValue(sprite.name, out HeroType hero)) return hero.ToString();
        if (HeroSpriteDatabase.SpriteToMonsterMap.TryGetValue(sprite.name, out MonsterType monster)) return monster.ToString();
        return sprite.name;
    }

    public static Sprite GetBaseSprite(int effectId)
    {
        return Array.Find(BaseActionSprites, s => {
            if (s == null) return false;
            string[] parts = s.name.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedId))
            {
                return parsedId == effectId;
            }
            return false;
        });
    }

    public static Sprite GetFacadeSprite(string facadeId)
    {
        if (string.IsNullOrEmpty(facadeId)) return null;

        return Array.Find(AllActionSprites, s => {
            if (s == null) return false;
            string name = s.name;
            string[] parts = name.Split('_');
            if (parts.Length >= 2)
            {
                string key = $"{parts[0]}{parts[1]}";
                if (key.Equals(facadeId, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return name.Equals(facadeId, StringComparison.OrdinalIgnoreCase);
        });
    }

    public static Sprite GetSpriteForBase(int id)
    {
        string search = $"bas_{id}";
        return Array.Find(BaseActionSprites, s => s != null && (s.name == search || s.name.StartsWith($"{search}_")));
    }

    public static Sprite GetSpriteForFacade(string facadeId)
    {
        if (string.IsNullOrWhiteSpace(facadeId) || facadeId.Length < 4) return null;

        string prefix = facadeId.Substring(0, 3);
        string num = facadeId.Substring(3);
        string search = $"{prefix}_{num}";

        return Array.Find(AllActionSprites, s => s != null && (s.name == search || s.name.StartsWith($"{search}_")));
    }

    // =====================================================================
    // PORT: TOOLTIP EXTRACTION RULES
    // Recommendation: Relocate to Tooltip / UI Metadata Dictionary configuration.
    // =====================================================================

    public static string GetBaseTooltip(Sprite sprite)
    {
        if (sprite == null) return string.Empty;

        if (TryGetBasValue(sprite.name, out int basVal))
        {
            if (basVal >= 0 && basVal < DefaultDiceData.BaseTooltipNames.Length)
            {
                return DefaultDiceData.BaseTooltipNames[basVal];
            }
        }

        return sprite.name;
    }

    private static bool TryGetBasValue(string spriteName, out int basValue)
    {
        basValue = -1;
        if (string.IsNullOrEmpty(spriteName)) return false;

        if (spriteName.StartsWith("bas_", StringComparison.OrdinalIgnoreCase))
        {
            int startIndex = 4;
            int endIndex = startIndex;
            while (endIndex < spriteName.Length && char.IsDigit(spriteName[endIndex]))
            {
                endIndex++;
            }

            if (endIndex > startIndex)
            {
                string numStr = spriteName.Substring(startIndex, endIndex - startIndex);
                return int.TryParse(numStr, out basValue);
            }
        }
        return false;
    }

    // =====================================================================
    // PORT: SYNTAX HIGHLIGHTING / COLOR FORMATTING
    // Recommendation: Relocate to a separate SyntaxHighlighter utility, or TMP overlay module.
    // =====================================================================

    public static string FormatSyntaxHighlighting(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        string result = plainText;

        string[] keys = { "replica", "img", "n", "col", "hp", "tier", "hsv" };
        string lookahead = @"(?=\.n|\.col|\.hp|\.tier|\.img|\.sd|\.i|\.hsv|\)|$)";

        foreach (string key in keys)
        {
            string hexColor = GetFixedColorForTag(key);
            string pattern = $"(?<=^|\\.|\\()({key}\\..*?){lookahead}";
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, $"<color=#{hexColor}>$1</color>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static string GetFixedColorForTag(string tag)
    {
        uint hash = 2166136261;
        foreach (char c in tag)
        {
            hash ^= (uint)c;
            hash *= 16777619;
        }

        float hue = (hash % 1000) / 1000f;
        Color color = Color.HSVToRGB(hue, 0.7f, 0.9f);
        return ColorUtility.ToHtmlStringRGB(color);
    }

    public static string GetColoredKeywordLabel(string keyword)
    {
        if (System.Enum.TryParse(keyword, true, out EffectKeyword parsedKw))
        {
            if (EffectKeywordColors.Map.TryGetValue(parsedKw, out Color colorValue))
            {
                string hex = ColorUtility.ToHtmlStringRGB(colorValue);
                return $"<color=#{hex}>{keyword}</color>";
            }
        }
        return keyword;
    }

    // =====================================================================
    // PORT: DATA SCHEMA / CONSTS / PARSING HELPERS
    // Recommendation: Move into HeroData domain objects, or extension utility methods.
    // =====================================================================

    public static string ExtractHeroName(string rawText)
    {
        HeroData dummy = TextModLexerParser.ParseHero(rawText);
        return string.IsNullOrEmpty(dummy.entityName) ? "Unknown Hero" : dummy.entityName;
    }

    public static string[] GetKeywordOptions()
    {
        var list = new List<string> { "Select Keyword to Add..." };

        foreach (string rawName in Enum.GetNames(typeof(EffectKeyword)))
        {
            list.Add(GetColoredKeywordLabel(rawName));
        }

        return list.ToArray();
    }

    public static HeroColorOption ReverseLookupColor(string code)
    {
        foreach (HeroColorOption opt in Enum.GetValues(typeof(HeroColorOption)))
        {
            if (SDColors.GetColorCode(opt) == code) return opt;
        }
        return HeroColorOption.Yellow;
    }
}