using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class HeroData : EntityData
{
    public static readonly string[] HeroPropertyKeys =
{
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata"
        };

    [Header("Hero Specific Info")]
    public string baseReplica = "Statue";
    public string colorClass = "y";
    public int tier = 1;

    [Header("Hero Modifiers")]
    public List<string> baseAbilityData = new List<string>();
    public List<AbilityData> customAbilityData = new List<AbilityData>();
    public int? adj;

    [Header("Post-Dice Info")]
    public string speech;

    public static string Export(HeroData hero)
    {
        if (hero == null) return "()";

        // 1. Build the core <hero> data inside its own balanced parenthesis
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(hero.imageOverride) &&
                                hero.imageOverride != "None" &&
                                hero.imageOverride != hero.baseReplica;

        heroSb.Append($"replica.{FormatName(hero.baseReplica)}");
        if (!hasImageOverride) hero.AppendColorModifier(heroSb);

        heroSb.Append($".n.{FormatName(hero.entityName)}");

        if (!string.IsNullOrEmpty(hero.colorClass) && !IsDefaultColor(hero.baseReplica, hero.colorClass))
        {
            heroSb.Append($".col.{hero.colorClass}");
        }

        heroSb.Append($".hp.{hero.hp}.tier.{hero.tier}");

        if (!string.IsNullOrEmpty(hero.p)) heroSb.Append($".p.{hero.p}");
        if (hero.adj.HasValue) heroSb.Append($".adj.{hero.adj.Value}");
        if (!string.IsNullOrEmpty(hero.b)) heroSb.Append($".b.{hero.b}");
        if (!string.IsNullOrEmpty(hero.rect)) heroSb.Append($".rect.{hero.rect}");
        if (!string.IsNullOrEmpty(hero.draw)) heroSb.Append($".draw.{hero.draw}");
        if (!string.IsNullOrEmpty(hero.thue)) heroSb.Append($".thue.{hero.thue}");

        hero.AppendDiceSides(heroSb);
        if (!string.IsNullOrEmpty(hero.speech)) heroSb.Append($".speech.{hero.speech}");
        if (!string.IsNullOrEmpty(hero.doc)) heroSb.Append($".doc.{hero.doc}");

        heroSb.Append(hero.BuildFaceModifiers(allowFacade: true));

        if (hasImageOverride)
        {
            heroSb.Append($".img.{FormatName(hero.imageOverride)}");
            hero.AppendColorModifier(heroSb);
        }

        heroSb.Append(")");

        // 2. Build the <those> modifiers outside of the hero parenthesis
        StringBuilder thoseSb = new StringBuilder();

        // Traits: t.<name>
        if (hero.traits != null)
        {
            foreach (var t in hero.traits)
            {
                if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
            }
        }

        // Items: i.<name>
        if (hero.items != null)
        {
            foreach (var i in hero.items)
            {
                if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
            }
        }

        // Custom Items: i.(<custom item>)
        if (hero.customItems != null)
        {
            foreach (var ci in hero.customItems)
            {
                if (ci != null) thoseSb.Append($".i.({ItemData.Export(ci)})");
            }
        }

        // Blessings: i.gift.<name>
        if (hero.blessings != null)
        {
            foreach (var b in hero.blessings)
            {
                if (!string.IsNullOrEmpty(b)) thoseSb.Append($".gift.{FormatName(b)}");
            }
        }

        // Curses: i.t.jinx.<curse>
        if (hero.curses != null)
        {
            foreach (var c in hero.curses)
            {
                if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
            }
        }

        // Base Abilities: i.learn.<ability>
        if (hero.baseAbilityData != null)
        {
            foreach (var ab in hero.baseAbilityData)
            {
                if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");
            }
        }

        // Custom Abilities: abilitydata.(<custom ability>)
        if (hero.customAbilityData != null)
        {
            foreach (var cab in hero.customAbilityData)
            {
                if (cab != null) thoseSb.Append($".abilitydata.({AbilityData.Export(cab)})");
            }
        }

        // Combine into outer wrapper: ((<hero>)<those>)
        if (thoseSb.Length == 0)
        {
            return heroSb.ToString();
        }

        return $"({heroSb.ToString()}{thoseSb.ToString()})";
    }

    public static HeroData Parse(string data)
    {
        HeroData hero = new HeroData();
        if (string.IsNullOrEmpty(data)) return hero;

        data = data.Trim();
        List<string> tokens = new List<string>();

        // Safely split nested double parentheses ((<hero>)<those>) without corrupting dots
        if (data.StartsWith("((") && data.EndsWith(")"))
        {
            int depth = 1;
            int innerEndIndex = -1;
            for (int idx = 2; idx < data.Length; idx++)
            {
                if (data[idx] == '(') depth++;
                else if (data[idx] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        innerEndIndex = idx;
                        break;
                    }
                }
            }

            if (innerEndIndex != -1 && innerEndIndex < data.Length - 1)
            {
                string innerHeroStr = data.Substring(1, innerEndIndex);
                string trailingModifiersStr = data.Substring(innerEndIndex + 1, data.Length - innerEndIndex - 2);

                tokens.AddRange(TokenizeString(innerHeroStr));
                if (!string.IsNullOrEmpty(trailingModifiersStr))
                {
                    tokens.AddRange(TokenizeString(trailingModifiersStr));
                }
            }
            else
            {
                tokens = TokenizeString(data);
            }
        }
        else
        {
            tokens = TokenizeString(data);
        }

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
                    if (string.Equals(value, "t", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < tokens.Count && string.Equals(tokens[i + 2], "jinx", StringComparison.OrdinalIgnoreCase) &&
                        i + 3 < tokens.Count)
                    {
                        hero.curses.AddRange(tokens[i + 3].Split('#'));
                        i += 3;
                    }
                    else if (string.Equals(value, "gift", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        hero.blessings.AddRange(tokens[i + 2].Split('#'));
                        i += 2;
                    }
                    else if (string.Equals(value, "learn", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        hero.baseAbilityData.AddRange(tokens[i + 2].Split('#'));
                        i += 2;
                    }
                    else if (value.StartsWith("("))
                    {
                        hero.customItems.Add(ItemData.Parse(value));
                    }
                    else
                    {
                        hero.items.AddRange(value.Split('#'));
                    }
                    break;

                case "t":
                    hero.traits.AddRange(value.Split('#'));
                    break;

                case "abilitydata":
                    if (value.StartsWith("("))
                    {
                        hero.customAbilityData.Add(AbilityData.Parse(value));
                    }
                    else
                    {
                        hero.baseAbilityData.AddRange(value.Split('#'));
                    }
                    break;

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
    public bool AddAbility(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return false;
        if (!baseAbilityData.Contains(abilityName))
        {
            baseAbilityData.Add(abilityName);
            return true;
        }
        return false;
    }
    public bool RemoveAbility(string abilityName)
    {
        return baseAbilityData.Remove(abilityName);
    }
}