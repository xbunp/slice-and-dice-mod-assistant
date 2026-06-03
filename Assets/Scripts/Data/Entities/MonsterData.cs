using System.Collections.Generic;
using System.Text;
using UnityEngine;

[System.Serializable]
public class MonsterData : EntityData
{
    [Header("Monster Specific Info")]
    public string baseMonsterType = "rmon"; // e.g., rmon, egg, vase, orb
    public string baseMonster = "Rat";      // Counterpart to baseReplica

    [Header("Monster Modifiers")]
    public List<string> jinxs = new List<string>(); // Like traits, can be chained
    public string bal; // String representation, singular

    public static string Export(MonsterData monster)
    {
        if (monster == null) return "()";

        StringBuilder sb = new StringBuilder();
        sb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(monster.imageOverride) &&
                                monster.imageOverride != "None" &&
                                monster.imageOverride != monster.baseMonster;

        // 1. Top-Level Mutually Exclusive Type (rmon, egg, vase, orb)
        string typePrefix = string.IsNullOrEmpty(monster.baseMonsterType) ? "rmon" : monster.baseMonsterType.ToLower();
        sb.Append($"{typePrefix}.{FormatName(monster.baseMonster)}");

        if (!hasImageOverride) monster.AppendColorModifier(sb);

        // 2. Core Stats (Monsters don't track colorClass or Tier here)
        sb.Append($".n.{FormatName(monster.entityName)}");
        sb.Append($".hp.{monster.hp}");

        // 3. Modifiers
        monster.AppendListAsChained(sb, "i", monster.items);
        monster.AppendListAsChained(sb, "t", monster.traits);
        monster.AppendListAsChained(sb, "jinx", monster.jinxs);

        if (!string.IsNullOrEmpty(monster.bal)) sb.Append($".bal.{FormatName(monster.bal)}");
        if (!string.IsNullOrEmpty(monster.p)) sb.Append($".p.{monster.p}");
        if (!string.IsNullOrEmpty(monster.b)) sb.Append($".b.{monster.b}");
        if (!string.IsNullOrEmpty(monster.rect)) sb.Append($".rect.{monster.rect}");
        if (!string.IsNullOrEmpty(monster.draw)) sb.Append($".draw.{monster.draw}");
        if (!string.IsNullOrEmpty(monster.thue)) sb.Append($".thue.{monster.thue}");

        // 4. Dice Sides & Doc
        monster.AppendDiceSides(sb);
        if (!string.IsNullOrEmpty(monster.doc)) sb.Append($".doc.{monster.doc}");

        // 5. Face Modifiers (Monsters do NOT use 'facade' for base images/dice sides)
        sb.Append(monster.BuildFaceModifiers(allowFacade: false));

        // 6. Image Override (using standard 'img' tag)
        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(monster.imageOverride)}");
            monster.AppendColorModifier(sb);
        }

        sb.Append(")");
        return sb.ToString();
    }

    public static MonsterData Parse(string data)
    {
        MonsterData monster = new MonsterData();
        List<string> tokens = TokenizeString(data);

        for (int i = 0; i < tokens.Count; i++)
        {
            string key = tokens[i].ToLower();
            string value = (i + 1 < tokens.Count) ? tokens[i + 1] : "";
            bool consumeValue = true;

            switch (key)
            {
                // Top Level Definitions
                case "rmon":
                case "egg":
                case "vase":
                case "orb":
                    monster.baseMonsterType = key;
                    monster.baseMonster = value;
                    break;

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

                case "i": monster.items.AddRange(value.Split('#')); break;
                case "t": monster.traits.AddRange(value.Split('#')); break;
                case "jinx": monster.jinxs.AddRange(value.Split('#')); break;
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