using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;

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
    //public List<AbilityData> customAbilityData = new List<AbilityData>();

    [SerializeField] public List<SpellData> customSpells = new List<SpellData>();
    [SerializeField] public List<TacticData> customTactics = new List<TacticData>();
    public IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>();
            if (customSpells != null) combined.AddRange(customSpells);
            if (customTactics != null) combined.AddRange(customTactics);
            return combined;
        }
    }

    public int? adj;

    [Header("Post-Dice Info")]
    public string speech;

    public virtual string Export()
    {
        //if (hero == null) return "()";

        // 1. Build the core <hero> data inside its own balanced parenthesis
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                imageOverride != "None" &&
                                imageOverride != baseReplica;

        heroSb.Append($"replica.{FormatName(baseReplica)}");
        if (!hasImageOverride) AppendColorModifier(heroSb);

        heroSb.Append($".n.{FormatName(entityName)}");

        if (!string.IsNullOrEmpty(colorClass) && !IsDefaultColor(baseReplica, colorClass))
        {
            heroSb.Append($".col.{colorClass}");
        }

        heroSb.Append($".hp.{hp}.tier.{tier}");

        if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");
        if (!string.IsNullOrEmpty(thue)) heroSb.Append($".thue.{thue}");

        AppendDiceSides(heroSb);
        if (!string.IsNullOrEmpty(speech)) heroSb.Append($".speech.{speech}");
        if (!string.IsNullOrEmpty(doc)) heroSb.Append($".doc.{doc}");

        heroSb.Append(BuildFaceModifiers(allowFacade: true));

        if (hasImageOverride)
        {
            heroSb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(heroSb);
        }

        heroSb.Append(")");

        // 2. Build the <those> modifiers outside of the hero parenthesis
        StringBuilder thoseSb = new StringBuilder();

        // Traits: t.<name>
        if (traits != null)
        {
            foreach (var t in traits)
            {
                if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
            }
        }

        // Items: i.<name>
        if (items != null)
        {
            foreach (var i in items)
            {
                if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
            }
        }

        // Custom Items: i.(<custom item>)
        if (customItems != null)
        {
            foreach (var ci in customItems)
            {
                if (ci != null) thoseSb.Append($".i.({ItemData.Export(ci)})");
            }
        }

        // Blessings: i.gift.<name>
        if (blessings != null)
        {
            foreach (var b in blessings)
            {
                if (!string.IsNullOrEmpty(b)) thoseSb.Append($".gift.{FormatName(b)}");
            }
        }

        // Curses: i.t.jinx.<curse>
        if (curses != null)
        {
            foreach (var c in curses)
            {
                if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
            }
        }

        // Base Abilities: i.learn.<ability>
        if (baseAbilityData != null)
        {
            foreach (var ab in baseAbilityData)
            {
                if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");
            }
        }

        // Custom Abilities: abilitydata.(<custom ability>)
        if (customAbilityData != null)
        {
            foreach (var cab in customAbilityData)
            {
                if (cab != null) thoseSb.Append($".abilitydata.({cab.Export()})");
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
                        hero.AddCustomAbility(AbilityData.Parse(value));
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

    // 3. Helper to automatically sort and add incoming custom abilities
    public void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;

        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();

        if (ability is SpellData spell)
        {
            if (!customSpells.Any(s => s.entityName == spell.entityName))
            {
                customSpells.Add(spell);
            }
        }
        else if (ability is TacticData tactic)
        {
            if (!customTactics.Any(t => t.entityName == tactic.entityName))
            {
                customTactics.Add(tactic);
            }
        }
    }

    // 4. Helper to find and remove an ability from whichever underlying list it belongs to
    public bool RemoveCustomAbility(string abilityName)
    {
        bool removed = false;

        if (customSpells != null)
        {
            var target = customSpells.FirstOrDefault(s => s.entityName == abilityName);
            if (target != null) removed = customSpells.Remove(target);
        }

        if (!removed && customTactics != null)
        {
            var target = customTactics.FirstOrDefault(t => t.entityName == abilityName);
            if (target != null) removed = customTactics.Remove(target);
        }

        return removed;
    }
}