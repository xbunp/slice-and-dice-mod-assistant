using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using static HeroData;

[System.Serializable]
public class HeroData : EntityData
{
    public enum Hero
    {
        Blank,
        Defaults
    }

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
    public int? adj;

    [Header("Hero Modifiers")]
    public List<string> baseAbilityData = new List<string>();
    //public List<AbilityData> customAbilityData = new List<AbilityData>();

    [Header("Post-Dice Info")]
    public string speech;

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

    public HeroData(Hero state)
    {
        switch (state)
        {
            case Hero.Blank:
                InitializeAsBlank();
                break;
            case Hero.Defaults:
                InitializeWithDefaults();
                break;
        }
    }

    private void InitializeAsBlank()
    {
        // SDData Fields
        entityName = null;
        imageOverride = null;

        // EntityData Fields
        hp = 0;
        h = 0;
        s = 0;
        v = 0;
        hue = null;
        hsl = null;

        items = new List<string>();
        customItems = new List<ItemData>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();

        p = null;
        b = null;
        rect = null;
        draw = null;
        thue = null;
        doc = null;

        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++)
        {
            diceSides[i] = new DiceSideData { effectID = 0, pips = 0 };
        }

        // HeroData Fields
        baseReplica = null;
        colorClass = null;
        tier = 0;
        baseAbilityData = new List<string>();
        customSpells = new List<SpellData>();
        customTactics = new List<TacticData>();
        adj = null;
        speech = null;
    }
    private void InitializeWithDefaults()
    {
        // SDData Fields
        entityName = "NewEntity";
        imageOverride = "None";

        // EntityData Fields
        hp = 7;
        h = 0;
        s = 0;
        v = 0;
        hue = null;
        hsl = null;

        items = new List<string>();
        customItems = new List<ItemData>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();

        p = null;
        b = null;
        rect = null;
        draw = null;
        thue = null;
        doc = null;

        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++)
        {
            diceSides[i] = new DiceSideData();
        }

        // HeroData Fields
        baseReplica = "Statue";
        colorClass = "y";
        tier = 1;
        baseAbilityData = new List<string>();
        customSpells = new List<SpellData>();
        customTactics = new List<TacticData>();
        adj = null;
        speech = null;
    }

    public virtual new string Export()
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
                if (ci != null) thoseSb.Append($".i.({ci.Export()})");
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
    public HeroData(string data)
    {
        InitializeWithDefaults(); // Must be defaults so omitted text properties fall back correctly
        ParseInstance(data);
    }

    public static new HeroData Parse(string data)
    {
        HeroData hero = new HeroData(Hero.Defaults); // Must be defaults
        hero.ParseInstance(data);
        return hero;
    }

    public void ParseInstance(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

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
                case "replica": baseReplica = value; break;
                case "n": entityName = value; break;
                case "img": imageOverride = value; break;
                case "col": colorClass = value; break;
                case "hp": if (int.TryParse(value, out int parsedhp)) hp = parsedhp; break;
                case "tier": if (int.TryParse(value, out int t)) tier = t; break;

                case "hsv":
                    string[] hsvParts = value.Split(':');
                    if (hsvParts.Length == 3)
                    {
                        int.TryParse(hsvParts[0], out h);
                        int.TryParse(hsvParts[1], out s);
                        int.TryParse(hsvParts[2], out v);
                    }
                    break;
                case "hsl": hsl = value; break;
                case "hue": if (int.TryParse(value, out int hVal)) hue = hVal; break;

                case "i":
                    if (string.Equals(value, "t", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < tokens.Count && string.Equals(tokens[i + 2], "jinx", StringComparison.OrdinalIgnoreCase) &&
                        i + 3 < tokens.Count)
                    {
                        curses.AddRange(tokens[i + 3].Split('#'));
                        i += 3;
                    }
                    else if (string.Equals(value, "gift", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        blessings.AddRange(tokens[i + 2].Split('#'));
                        i += 2;
                    }
                    else if (string.Equals(value, "learn", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        baseAbilityData.AddRange(tokens[i + 2].Split('#'));
                        i += 2;
                    }
                    else if (value.StartsWith("("))
                    {
                        customItems.Add(SDData.Parse<ItemData>(value));
                    }
                    else
                    {
                        items.AddRange(value.Split('#'));
                    }
                    break;

                case "t":
                    traits.AddRange(value.Split('#'));
                    break;

                case "abilitydata":
                    if (value.StartsWith("("))
                    {
                        AddCustomAbility(AbilityData.Parse(value));
                    }
                    else
                    {
                        baseAbilityData.AddRange(value.Split('#'));
                    }
                    break;

                case "p": p = value; break;
                case "b": b = value; break;
                case "rect": rect = value; break;
                case "draw": draw = value; break;
                case "thue": thue = value; break;
                case "adj": if (int.TryParse(value, out int a)) adj = a; break;

                case "speech": speech = value; break;
                case "doc": doc = value; break;

                case "sd":
                    string[] faces = value.Split(':');
                    for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                    {
                        if (faces[f] == "0") continue;
                        string[] faceParts = faces[f].Split('-');
                        if (faceParts.Length == 2)
                        {
                            int.TryParse(faceParts[0], out diceSides[f].effectID);
                            int.TryParse(faceParts[1], out diceSides[f].pips);
                        }
                    }
                    break;

                default: consumeValue = false; break;
            }

            if (consumeValue) i++;
        }
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
    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- HERO DATA DEBUG ---");

        sb.AppendLine($"{indent}Name: {entityName}");
        sb.AppendLine($"{indent}Base Replica: {baseReplica}");
        sb.AppendLine($"{indent}Color Class: {colorClass}");
        sb.AppendLine($"{indent}Tier: {tier}");
        sb.AppendLine($"{indent}HP: {hp}");
        sb.AppendLine($"{indent}Image Override: {imageOverride}");

        if (h != 0 || s != 0 || v != 0 || hue != 0 || !string.IsNullOrEmpty(hsl))
        {
            sb.AppendLine($"{indent}HSV: {h}:{s}:{v} | Hue: {hue} | HSL: {hsl}");
        }

        if (adj.HasValue || !string.IsNullOrEmpty(p) || !string.IsNullOrEmpty(b) ||
            !string.IsNullOrEmpty(rect) || !string.IsNullOrEmpty(draw) || !string.IsNullOrEmpty(thue))
        {
            sb.AppendLine($"{indent}Layout/Visuals -> adj: {adj}, p: {p}, b: {b}, rect: {rect}, draw: {draw}, thue: {thue}");
        }

        if (!string.IsNullOrEmpty(speech) || !string.IsNullOrEmpty(doc))
        {
            sb.AppendLine($"{indent}Narrative -> speech: '{speech}', doc: '{doc}'");
        }

        if (diceSides != null)
        {
            sb.AppendLine($"{indent}Dice Sides:");
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                if (side != null)
                {
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0)
            sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (blessings != null && blessings.Count > 0)
            sb.AppendLine($"{indent}Blessings: {string.Join(", ", blessings)}");
        if (curses != null && curses.Count > 0)
            sb.AppendLine($"{indent}Curses: {string.Join(", ", curses)}");
        if (baseAbilityData != null && baseAbilityData.Count > 0)
            sb.AppendLine($"{indent}Base Abilities: {string.Join(", ", baseAbilityData)}");
        if (items != null && items.Count > 0)
            sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");

        if (customItems != null && customItems.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Items ({customItems.Count}):");
            for (int i = 0; i < customItems.Count; i++)
            {
                var ci = customItems[i];
                if (ci != null)
                {
                    sb.AppendLine($"{indent}  [{i}] [✓ Recursively Unpacked ItemData!]");
                    ci.DebugContentsToConsole(indent + "        ");
                }
            }
        }

        if (customSpells != null && customSpells.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Spells ({customSpells.Count}):");
            for (int i = 0; i < customSpells.Count; i++)
            {
                var spell = customSpells[i];
                if (spell != null)
                {
                    sb.AppendLine($"{indent}  [{i}] Spell Name: {spell.entityName}");
                    // If SpellData/AbilityData implements a similar debug method, it can be called recursively:
                    // spell.DebugContentsToConsole(indent + "        ");
                }
            }
        }

        if (customTactics != null && customTactics.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Tactics ({customTactics.Count}):");
            for (int i = 0; i < customTactics.Count; i++)
            {
                var tactic = customTactics[i];
                if (tactic != null)
                {
                    sb.AppendLine($"{indent}  [{i}] Tactic Name: {tactic.entityName}");
                    // If TacticData/AbilityData implements a similar debug method, it can be called recursively:
                    // tactic.DebugContentsToConsole(indent + "        ");
                }
            }
        }

        if (indent == "")
        {
            UnityEngine.Debug.Log(sb.ToString());
        }
        else
        {
            UnityEngine.Debug.Log(sb.ToString());
        }
    }
}