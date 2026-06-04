using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public class MonsterData : EntityData
{
    public static readonly string[] MonsterPropertyKeys = { "i", "rmon", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "triggerhpdata" };

    [Header("Monster Specific Info")]
    public string baseMonster = "Wolf";     
    public MonsterSize size = MonsterSize.HeroSized;

    [Header("Monster Modifiers")]
    public string bal; // String representation, singular

    public static string Export(MonsterData monster)
    {
        if (monster == null) return "()";

        // 1. Build the core <monster> data inside its own balanced parenthesis
        StringBuilder monsterSb = new StringBuilder();
        monsterSb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(monster.imageOverride) &&
                                monster.imageOverride != "None" &&
                                monster.imageOverride != monster.baseMonster;

        /*
        // Monsters start directly with their name, or their type prefix followed by name
        if (!string.IsNullOrEmpty(monster.baseMonsterType) &&
            monster.baseMonsterType.ToLower() != "rmon" &&
            monster.baseMonsterType.ToLower() != "none")
        {
            monsterSb.Append($"{monster.baseMonsterType.ToLower()}.{FormatName(monster.baseMonster)}");
        }
        else
        {
            // Standard monster, e.g., "Wolf.hp.4.n.Biyomon"
            monsterSb.Append($"{FormatName(monster.baseMonster)}");
        }
        */

        // Standard monster, e.g., "Wolf.hp.4.n.Biyomon"
        monsterSb.Append($"{FormatName(monster.baseMonster)}");

        if (!hasImageOverride) monster.AppendColorModifier(monsterSb);

        if (!string.IsNullOrEmpty(monster.entityName))
        {
            monsterSb.Append($".n.{FormatName(monster.entityName)}");
        }

        monsterSb.Append($".hp.{monster.hp}");

        if (!string.IsNullOrEmpty(monster.bal)) monsterSb.Append($".bal.{FormatName(monster.bal)}");
        if (!string.IsNullOrEmpty(monster.p)) monsterSb.Append($".p.{monster.p}");
        if (!string.IsNullOrEmpty(monster.b)) monsterSb.Append($".b.{monster.b}");
        if (!string.IsNullOrEmpty(monster.rect)) monsterSb.Append($".rect.{monster.rect}");
        if (!string.IsNullOrEmpty(monster.draw)) monsterSb.Append($".draw.{monster.draw}");
        if (!string.IsNullOrEmpty(monster.thue)) monsterSb.Append($".thue.{monster.thue}");

        monster.AppendDiceSides(monsterSb);

        if (!string.IsNullOrEmpty(monster.doc)) monsterSb.Append($".doc.{monster.doc}");

        // Face Modifiers (Monsters strictly do NOT use 'facade' keywords)
        monsterSb.Append(monster.BuildFaceModifiers(allowFacade: false));

        // Image Override
        if (hasImageOverride)
        {
            monsterSb.Append($".img.{FormatName(monster.imageOverride)}");
            monster.AppendColorModifier(monsterSb);
        }

        monsterSb.Append(")");

        // 2. Build the <those> modifiers outside of the monster parenthesis
        StringBuilder thoseSb = new StringBuilder();

        // Traits: t.<name>
        if (monster.traits != null)
        {
            foreach (var t in monster.traits)
            {
                if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
            }
        }

        // Items: i.<name>
        if (monster.items != null)
        {
            foreach (var i in monster.items)
            {
                if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
            }
        }

        // Custom Items: i.(<custom item>)
        if (monster.customItems != null)
        {
            foreach (var ci in monster.customItems)
            {
                if (ci != null) thoseSb.Append($".i.({ItemData.Export(ci)})");
            }
        }

        // Blessings: gift.<name>
        if (monster.blessings != null)
        {
            foreach (var b in monster.blessings)
            {
                if (!string.IsNullOrEmpty(b)) thoseSb.Append($".gift.{FormatName(b)}");
            }
        }

        // Curses: i.t.jinx.<curse>
        if (monster.curses != null)
        {
            foreach (var c in monster.curses)
            {
                if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
            }
        }

        // Combine into outer wrapper: ((<monster>)<those>)
        if (thoseSb.Length == 0)
        {
            return monsterSb.ToString();
        }

        return $"({monsterSb.ToString()}{thoseSb.ToString()})";
    }

    public static MonsterData Parse(string data)
    {
        MonsterData monster = new MonsterData();
        if (string.IsNullOrEmpty(data)) return monster;

        data = data.Trim();
        List<string> tokens = new List<string>();

        // Safely split nested double parentheses ((<monster>)<those>) without corrupting dots
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
                string innerMonsterStr = data.Substring(1, innerEndIndex);
                string trailingModifiersStr = data.Substring(innerEndIndex + 1, data.Length - innerEndIndex - 2);

                tokens.AddRange(TokenizeString(innerMonsterStr));
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

        // 1. Identify Base Monster / Type Prefix gracefully
        // Unlike heroes, monsters don't use a "replica." tag. It's normally the very first token.
        if (tokens.Count > 0)
        {
            string firstToken = tokens[0].ToLower();
            if (firstToken == "rmon" || firstToken == "egg" || firstToken == "vase" || firstToken == "orb")
            {
                //monster.baseMonsterType = firstToken;
                tokens.RemoveAt(0); // consume the type

                // If the next token isn't a recognized property key, it's the specific monster name
                if (tokens.Count > 0 && !MonsterPropertyKeys.Contains(tokens[0].ToLower()))
                {
                    monster.baseMonster = tokens[0];
                    tokens.RemoveAt(0); // consume the name
                }
            }
            else if (!MonsterPropertyKeys.Contains(firstToken))
            {
                // No explicit type modifier, the very first token is just the name (e.g., "Wolf")
                monster.baseMonster = tokens[0];
                //monster.baseMonsterType = "rmon"; // Default fallback type
                tokens.RemoveAt(0); // consume the name
            }
        }

        // 2. Parse remaining properties
        for (int i = 0; i < tokens.Count; i++)
        {
            string key = tokens[i].ToLower();
            string value = (i + 1 < tokens.Count) ? tokens[i + 1] : "";
            bool consumeValue = true;

            switch (key)
            {
                case "n": monster.entityName = value; break;
                case "img": monster.imageOverride = value; break;
                case "hp": if (int.TryParse(value, out int hp)) monster.hp = hp; break;

                case "hsv":
                    string[] hsvParts = value.Split(':');
                    if (hsvParts.Length == 3)
                    {
                        int.TryParse(hsvParts[0], out monster.h);
                        int.TryParse(hsvParts[1], out monster.s);
                        int.TryParse(hsvParts[2], out monster.v);
                    }
                    break;
                case "hsl": monster.hsl = value; break;
                case "hue": if (int.TryParse(value, out int hVal)) monster.hue = hVal; break;

                case "i":
                    if (string.Equals(value, "t", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < tokens.Count && string.Equals(tokens[i + 2], "jinx", StringComparison.OrdinalIgnoreCase) &&
                        i + 3 < tokens.Count)
                    {
                        monster.curses.AddRange(tokens[i + 3].Split('#'));
                        i += 3;
                    }
                    else if (string.Equals(value, "gift", StringComparison.OrdinalIgnoreCase) && i + 2 < tokens.Count)
                    {
                        monster.blessings.AddRange(tokens[i + 2].Split('#'));
                        i += 2;
                    }
                    else if (value.StartsWith("("))
                    {
                        monster.customItems.Add(ItemData.Parse(value));
                    }
                    else
                    {
                        monster.items.AddRange(value.Split('#'));
                    }
                    break;

                case "t":
                    monster.traits.AddRange(value.Split('#'));
                    break;

                case "bal": monster.bal = value; break;

                case "p": monster.p = value; break;
                case "b": monster.b = value; break;
                case "rect": monster.rect = value; break;
                case "draw": monster.draw = value; break;
                case "thue": monster.thue = value; break;
                case "doc": monster.doc = value; break;

                case "sd":
                    string[] faces = value.Split(':');
                    for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                    {
                        if (faces[f] == "0") continue;
                        string[] faceParts = faces[f].Split('-');
                        if (faceParts.Length == 2)
                        {
                            int.TryParse(faceParts[0], out monster.diceSides[f].effectID);
                            int.TryParse(faceParts[1], out monster.diceSides[f].pips);
                        }
                    }
                    break;

                default: consumeValue = false; break;
            }

            if (consumeValue) i++;
        }
        return monster;
    }
}