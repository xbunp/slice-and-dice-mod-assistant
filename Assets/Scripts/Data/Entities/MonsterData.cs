using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        if (monster == null) return string.Empty;

        StringBuilder sb = new StringBuilder();

        bool hasImageOverride = !string.IsNullOrEmpty(monster.imageOverride) &&
                                monster.imageOverride != "None" &&
                                monster.imageOverride != monster.baseMonster;

        sb.Append($"{FormatName(monster.baseMonster)}");

        if (!hasImageOverride) monster.AppendColorModifier(sb);

        if (!string.IsNullOrEmpty(monster.entityName))
        {
            sb.Append($".n.{FormatName(monster.entityName)}");
        }

        sb.Append($".hp.{monster.hp}");

        if (!string.IsNullOrEmpty(monster.p)) sb.Append($".p.{monster.p}");
        if (!string.IsNullOrEmpty(monster.b)) sb.Append($".b.{monster.b}");
        if (!string.IsNullOrEmpty(monster.rect)) sb.Append($".rect.{monster.rect}");
        if (!string.IsNullOrEmpty(monster.draw)) sb.Append($".draw.{monster.draw}");
        if (!string.IsNullOrEmpty(monster.thue)) sb.Append($".thue.{monster.thue}");

        monster.AppendDiceSides(sb);

        // Retrieve face modifiers and enforce the ":0" suffix on the facade key if no HSV values are present
        string faceModifiers = monster.BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            faceModifiers = Regex.Replace(
                faceModifiers,
                @"(\.facade\.[^.:\s]+)(?=\.|$)",
                "$1:0"
            );
            sb.Append(faceModifiers);
        }

        if (!string.IsNullOrEmpty(monster.doc)) sb.Append($".doc.{monster.doc}");

        // Image Override
        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(monster.imageOverride)}");
            monster.AppendColorModifier(sb);
        }

        // Traits: t.<name>
        if (monster.traits != null)
        {
            foreach (var t in monster.traits)
            {
                if (!string.IsNullOrEmpty(t)) sb.Append($".t.{FormatName(t)}");
            }
        }

        // Items: i.<name>
        if (monster.items != null)
        {
            foreach (var i in monster.items)
            {
                if (!string.IsNullOrEmpty(i)) sb.Append($".i.{FormatName(i)}");
            }
        }

        // Custom Items: i.<custom item>
        if (monster.customItems != null)
        {
            foreach (var ci in monster.customItems)
            {
                if (ci != null) sb.Append($".i.{ci.Export()}");
            }
        }

        // Curses: i.t.jinx.<curse>
        if (monster.curses != null)
        {
            foreach (var c in monster.curses)
            {
                if (!string.IsNullOrEmpty(c)) sb.Append($".t.jinx.{FormatName(c)}");
            }
        }

        // Balance modifier is appended last after all other modifiers
        if (!string.IsNullOrEmpty(monster.bal))
        {
            sb.Append($".bal.{FormatName(monster.bal)}");
        }

        return sb.ToString();
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
        if (tokens.Count > 0)
        {
            string firstToken = tokens[0].ToLower();
            if (firstToken == "rmon" || firstToken == "egg" || firstToken == "vase" || firstToken == "orb")
            {
                tokens.RemoveAt(0); // consume the type

                if (tokens.Count > 0 && !MonsterPropertyKeys.Contains(tokens[0].ToLower()))
                {
                    monster.baseMonster = tokens[0];
                    tokens.RemoveAt(0); // consume the name
                }
            }
            else if (!MonsterPropertyKeys.Contains(firstToken))
            {
                monster.baseMonster = tokens[0];
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
                    else if (value.StartsWith("("))
                    {
                        monster.customItems.Add(SDData.Parse<ItemData>(value));
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