using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class HeroData : EntityData
{
    [Header("Hero Specific Info")]
    public string baseReplica = "Statue";
    public string colorClass = "y";
    public int tier = 1;

    [Header("Hero Modifiers")]
    public List<string> gifts = new List<string>();
    public List<string> abilityData = new List<string>();
    public int? adj;

    [Header("Post-Dice Info")]
    public string speech;

    public static string Export(HeroData hero)
    {
        if (hero == null) return "()";

        List<string> customAbilities = new List<string>();
        List<string> stockAbilities = new List<string>();

        if (hero.abilityData != null)
        {
            foreach (var ab in hero.abilityData)
            {
                if (string.IsNullOrEmpty(ab)) continue;

                if (IsStockAbility(ab, out string matchedName))
                    stockAbilities.Add(matchedName);
                else
                    customAbilities.Add(ab);
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(hero.imageOverride) &&
                                hero.imageOverride != "None" &&
                                hero.imageOverride != hero.baseReplica;

        sb.Append($"replica.{FormatName(hero.baseReplica)}");
        if (!hasImageOverride) hero.AppendColorModifier(sb);

        sb.Append($".n.{FormatName(hero.entityName)}");

        if (!string.IsNullOrEmpty(hero.colorClass) && !IsDefaultColor(hero.baseReplica, hero.colorClass))
        {
            sb.Append($".col.{hero.colorClass}");
        }

        sb.Append($".hp.{hero.hp}.tier.{hero.tier}");

        hero.AppendListAsChained(sb, "i", hero.items);
        hero.AppendListAsChained(sb, "gift", hero.gifts);
        hero.AppendListAsChained(sb, "t", hero.traits);

        if (customAbilities.Count > 0)
        {
            List<string> formattedAbilities = new List<string>();
            foreach (var ab in customAbilities)
            {
                formattedAbilities.Add(ab.StartsWith("(") && ab.EndsWith(")") ? ab : $"({ab})");
            }
            sb.Append($".abilitydata.{string.Join("#", formattedAbilities)}");
        }

        if (!string.IsNullOrEmpty(hero.p)) sb.Append($".p.{hero.p}");
        if (hero.adj.HasValue) sb.Append($".adj.{hero.adj.Value}");
        if (!string.IsNullOrEmpty(hero.b)) sb.Append($".b.{hero.b}");
        if (!string.IsNullOrEmpty(hero.rect)) sb.Append($".rect.{hero.rect}");
        if (!string.IsNullOrEmpty(hero.draw)) sb.Append($".draw.{hero.draw}");
        if (!string.IsNullOrEmpty(hero.thue)) sb.Append($".thue.{hero.thue}");

        hero.AppendDiceSides(sb);
        if (!string.IsNullOrEmpty(hero.speech)) sb.Append($".speech.{hero.speech}");
        if (!string.IsNullOrEmpty(hero.doc)) sb.Append($".doc.{hero.doc}");

        sb.Append(hero.BuildFaceModifiers(allowFacade: true));

        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(hero.imageOverride)}");
            hero.AppendColorModifier(sb);
        }

        sb.Append(")");

        string result = sb.ToString();

        // Wrap core game abilities: ((<hero>)i.learn.<name>)
        foreach (var stockAbility in stockAbilities)
        {
            result = $"({result}i.learn.{FormatName(stockAbility)})";
        }

        return result;
    }

    public static HeroData Parse(string data)
    {
        HeroData hero = new HeroData();
        if (string.IsNullOrEmpty(data)) return hero;

        data = data.Trim();

        // 1. Unwrap core abilities structured as ((<hero_data>)i.learn.<ability>)
        // We do this from the outside-in to protect the TokenizeString process.
        while (data.StartsWith("(") && data.EndsWith(")"))
        {
            int learnIndex = data.LastIndexOf(")i.learn.", StringComparison.OrdinalIgnoreCase);
            if (learnIndex != -1)
            {
                int nameStart = learnIndex + 9; // Length of ")i.learn."
                int nameLength = data.Length - nameStart - 1; // Exclude the closing ')'

                if (nameLength > 0)
                {
                    string abilityName = data.Substring(nameStart, nameLength);
                    // Insert at 0 so nested wrapped abilities maintain original order
                    hero.abilityData.Insert(0, abilityName);

                    // Strip the outer wrapper to process the inner hero data: "((hero)i.learn.Strike)" -> "(hero)"
                    data = data.Substring(1, learnIndex);
                    continue;
                }
            }
            break; // Break if wrapped in parens but doesn't match the specific i.learn pattern
        }

        // 2. Normal tokenization on the clean inner string
        List<string> tokens = TokenizeString(data);

        for (int i = 0; i < tokens.Count; i++)
        {
            string key = tokens[i].ToLower();
            string value = (i + 1 < tokens.Count) ? tokens[i + 1] : "";
            bool consumeValue = true;

            switch (key)
            {
                case "replica": hero.baseReplica = value; break;
                case "n": hero.entityName = value; break;
                case "img": hero.imageOverride = value; break;
                case "col": hero.colorClass = value; break;
                case "hp": if (int.TryParse(value, out int hp)) hero.hp = hp; break;
                case "tier": if (int.TryParse(value, out int t)) hero.tier = t; break;

                case "hsv":
                    string[] hsvParts = value.Split(':');
                    if (hsvParts.Length == 3)
                    {
                        int.TryParse(hsvParts[0], out hero.h);
                        int.TryParse(hsvParts[1], out hero.s);
                        int.TryParse(hsvParts[2], out hero.v);
                    }
                    break;
                case "hsl": hero.hsl = value; break;
                case "hue": if (int.TryParse(value, out int hVal)) hero.hue = hVal; break;

                case "i":
                    // Fallback to protect against flat/unwrapped "i.learn.<ability>" inside the string
                    if (string.Equals(value, "learn", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        hero.abilityData.Add(tokens[i + 2]);
                        i += 2; // Skip "learn" and the ability name
                    }
                    else
                    {
                        hero.items.AddRange(value.Split('#'));
                    }
                    break;

                case "gift": hero.gifts.AddRange(value.Split('#')); break;
                case "t": hero.traits.AddRange(value.Split('#')); break;
                case "abilitydata": hero.abilityData.AddRange(value.Split('#')); break;

                case "p": hero.p = value; break;
                case "b": hero.b = value; break;
                case "rect": hero.rect = value; break;
                case "draw": hero.draw = value; break;
                case "thue": hero.thue = value; break;
                case "adj": if (int.TryParse(value, out int a)) hero.adj = a; break;

                case "speech": hero.speech = value; break;
                case "doc": hero.doc = value; break;

                case "sd":
                    string[] faces = value.Split(':');
                    for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                    {
                        if (faces[f] == "0") continue;
                        string[] faceParts = faces[f].Split('-');
                        if (faceParts.Length == 2)
                        {
                            int.TryParse(faceParts[0], out hero.diceSides[f].effectID);
                            int.TryParse(faceParts[1], out hero.diceSides[f].pips);
                        }
                    }
                    break;

                default: consumeValue = false; break;
            }

            if (consumeValue) i++;
        }
        return hero;
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

    private static bool IsStockAbility(string abilityName, out string matchedName)
    {
        matchedName = abilityName;
        if (BaseAbilityDatabase.Abilities == null || string.IsNullOrEmpty(abilityName)) return false;

        string cleanedInput = abilityName.Trim();

        foreach (var ability in BaseAbilityDatabase.Abilities)
        {
            if (ability != null && string.Equals(ability.name, cleanedInput, StringComparison.OrdinalIgnoreCase))
            {
                matchedName = ability.name;
                return true;
            }
        }
        return false;
    }
}