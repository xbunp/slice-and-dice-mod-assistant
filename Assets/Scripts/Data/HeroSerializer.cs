using System;
using System.Collections.Generic;
using System.Text;

public static class HeroSerializer
{
    public static string Export(HeroData hero)
    {
        if (hero == null) return "()";

        StringBuilder sb = new StringBuilder();
        sb.Append("(");

        bool hasHeroFacade = !string.IsNullOrEmpty(hero.imageOverride) &&
                             hero.imageOverride != "None" &&
                             hero.imageOverride != hero.baseReplica;

        bool hasHsvValues = hero.h != 0 || hero.s != 0 || hero.v != 0;

        // 1. Base Replica & HSV
        sb.Append($"replica.{FormatName(hero.baseReplica)}");
        if (!hasHeroFacade && hasHsvValues)
        {
            sb.Append($".hsv.{hero.h}:{hero.s}:{hero.v}");
        }

        // 2. Core Stats
        sb.Append($".n.{FormatName(hero.heroName)}");

        // Prevent crash: Do not append color tag if it matches the replica's default color
        if (!string.IsNullOrEmpty(hero.colorClass) && !IsDefaultColor(hero.baseReplica, hero.colorClass))
        {
            sb.Append($".col.{hero.colorClass}");
        }

        sb.Append($".hp.{hero.hp}.tier.{hero.tier}");

        // 3. Dice Sides (sd)
        sb.Append(".sd.");
        for (int i = 0; i < 6; i++)
        {
            var side = hero.diceSides[i];
            if (side.effectID == 0) sb.Append("0");
            else sb.Append($"{side.effectID}-{side.pips}");

            if (i < 5) sb.Append(":");
        }

        // 4. Speech & Doc
        if (!string.IsNullOrEmpty(hero.speech)) sb.Append($".speech.{hero.speech}");
        if (!string.IsNullOrEmpty(hero.doc)) sb.Append($".doc.{hero.doc}");

        // 5. Inherent Modifiers (Keywords & Facades optimized via DiceTargetHelper)
        string modifiers = BuildModifiers(hero);
        sb.Append(modifiers);

        // 6. Image Override / Facade
        if (hasHeroFacade)
        {
            sb.Append($".img.{FormatName(hero.imageOverride)}");

            if (hasHsvValues)
            {
                sb.Append($".hsv.{hero.h}:{hero.s}:{hero.v}");
            }
        }

        sb.Append(")");
        return sb.ToString();
    }

    private static string BuildModifiers(HeroData hero)
    {
        StringBuilder modSb = new StringBuilder();

        // Map identical modifier strings to their face-index bitmask representation
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = hero.diceSides[i];
            List<string> chunks = new List<string>();

            foreach (var kw in face.keywords)
            {
                if (!string.IsNullOrWhiteSpace(kw)) chunks.Add($"k.{kw.Trim().ToLower()}");
            }

            if (!string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";
                if (!string.IsNullOrWhiteSpace(face.facadeColor)) facStr += $":{face.facadeColor}";
                chunks.Add(facStr);
            }

            if (chunks.Count > 0)
            {
                string modString = string.Join("#", chunks);
                int faceMask = 1 << i; // Shift maps directly to the bitmasks expected by DiceTargetHelper

                if (groupedModifiers.ContainsKey(modString))
                {
                    groupedModifiers[modString] |= faceMask;
                }
                else
                {
                    groupedModifiers[modString] = faceMask;
                }
            }
        }

        // Group the matching modifiers using optimal aliases found by DiceTargetHelper
        foreach (var kvp in groupedModifiers)
        {
            string modString = kvp.Key;
            int mask = kvp.Value;

            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(mask);
            foreach (string alias in optimalAliases)
            {
                modSb.Append($".i.{alias}.{modString}");
            }
        }

        return modSb.ToString();
    }

    private static bool IsDefaultColor(string baseReplica, string colorClass)
    {
        if (string.IsNullOrEmpty(baseReplica) || string.IsNullOrEmpty(colorClass)) return false;

        if (Enum.TryParse(baseReplica, true, out HeroType heroType))
        {
            if (SDColors.HeroColorMap.TryGetValue(heroType, out HeroColorOption defaultColor))
            {
                string defaultCode = SDColors.GetColorCode(defaultColor);
                return string.Equals(defaultCode, colorClass, StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    private static string FormatName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        name = name.Replace(" ", "_");
        return name;
    }
}