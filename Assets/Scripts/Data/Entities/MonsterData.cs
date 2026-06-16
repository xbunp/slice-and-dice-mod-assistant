using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class MonsterDomainRules
{
    public static readonly HashSet<string> MonsterPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "rmon", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "triggerhpdata"
    };
}

[System.Serializable]
public class MonsterData : EntityData
{
    [Header("Monster Semantic Payload")]
    public object payloadData; // Holds parsed Modifiers, Spells, or Nested Monsters safely

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

        if (monster.hp > 0)
        {
            sb.Append($".hp.{monster.hp}");
        }

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

    // ==========================================
    // KNOWLEDGE EXTRACTION (The Perfect Linear Parser)
    // ==========================================

    private string StripOuterParens(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string t = text.Trim();
        while (t.StartsWith("(") && t.EndsWith(")"))
        {
            int depth = 0; bool matching = true;
            for (int k = 0; k < t.Length - 1; k++)
            {
                if (t[k] == '(') depth++; else if (t[k] == ')') depth--;
                if (depth == 0) { matching = false; break; }
            }
            if (matching) t = t.Substring(1, t.Length - 2).Trim();
            else break;
        }
        return t;
    }

    // ==========================================
    // KNOWLEDGE EXTRACTION (The Perfect Linear Parser)
    // ==========================================

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = ItemData.TopLevelSplit(data.Trim(), '&');
        string core = chunks[0];

        core = StripOuterParens(core);

        List<string> tokens = ItemData.TopLevelSplit(core, '.');

        bool isFirstToken = true;
        ExtractKnowledge(tokens, this, ref isFirstToken);
    }

    private void ExtractKnowledge(List<string> tokens, MonsterData monster, ref bool isFirstToken)
    {
        int i = 0;

        // 1. Identify Base Monster / Type Prefix gracefully
        if (isFirstToken && tokens.Count > 0)
        {
            isFirstToken = false;
            string firstToken = tokens[i].ToLower();
            if (firstToken == "rmon" || firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx")
            {
                monster.baseMonster = tokens[i];
                i++;

                // Look ahead. If the next token isn't a property key, it's the payload (e.g. "((ea.sthief...))")
                if (i < tokens.Count && !MonsterDomainRules.MonsterPropertyKeys.Contains(tokens[i].ToLower()))
                {
                    string rawPayload = tokens[i];
                    monster.baseMonster += "." + rawPayload; // Preserve raw string for perfect export symmetry

                    // --- DEEP SEMANTIC COMPREHENSION ---
                    // We parse the payload internally so the compiler fully understands it
                    string corePayload = StripOuterParens(rawPayload);
                    if (firstToken == "jinx" || firstToken == "vase")
                    {
                        ModifierData mod = new ModifierData();
                        mod.Parse(corePayload);
                        monster.payloadData = mod;
                    }
                    else if (firstToken == "orb")
                    {
                        monster.payloadData = AbilityData.CreateSpellOrTactic(corePayload);
                    }
                    else if (firstToken == "egg")
                    {
                        if (monster.IsMonsterEntity(corePayload))
                        {
                            MonsterData nestedMonster = new MonsterData();
                            nestedMonster.Parse(corePayload);
                            monster.payloadData = nestedMonster;
                        }
                        else
                        {
                            HeroData nestedHero = new HeroData();
                            nestedHero.Parse(corePayload);
                            monster.payloadData = nestedHero;
                        }
                    }
                    i++;
                }
            }
            else if (!MonsterDomainRules.MonsterPropertyKeys.Contains(firstToken))
            {
                monster.baseMonster = tokens[i];
                i++;
            }
        }

        // 2. Parse remaining properties
        for (; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = ItemData.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens, monster, ref isFirstToken);
                continue;
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) monster.entityName = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) monster.imageOverride = tokens[++i]; break;
                case "doc": if (i + 1 < tokens.Count) monster.doc = tokens[++i]; break;
                case "bal": if (i + 1 < tokens.Count) monster.bal = tokens[++i]; break;
                case "hp": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hVal)) monster.hp = hVal; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsv = tokens[++i].Split(':');
                        if (hsv.Length == 3 && int.TryParse(hsv[0], out monster.h) && int.TryParse(hsv[1], out monster.s) && int.TryParse(hsv[2], out monster.v)) { }
                    }
                    break;
                case "hsl": if (i + 1 < tokens.Count) monster.hsl = tokens[++i]; break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) monster.hue = hueVal; break;
                case "p": if (i + 1 < tokens.Count) monster.p = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) monster.b = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) monster.rect = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) monster.draw = tokens[++i]; break;
                case "thue": if (i + 1 < tokens.Count) monster.thue = tokens[++i]; break;
                case "sd":
                    if (i + 1 < tokens.Count)
                    {
                        string[] faces = tokens[++i].Split(':');
                        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                        {
                            if (faces[f] == "0" || faces[f] == "0-0") continue;
                            string[] faceParts = faces[f].Split('-');
                            int.TryParse(faceParts[0], out monster.diceSides[f].effectID);
                            if (faceParts.Length > 1) int.TryParse(faceParts[1], out monster.diceSides[f].pips);
                        }
                    }
                    break;
                case "t":
                    if (i + 1 < tokens.Count)
                    {
                        monster.traits.AddRange(ItemData.TopLevelSplit(tokens[++i], '#'));
                    }
                    break;
                case "i":
                    ProcessItemProperty(tokens, ref i, monster);
                    break;
            }
        }
    }

    private void ProcessItemProperty(List<string> tokens, ref int i, MonsterData monster)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            if (endIndex == startIndex && (peek == "t" || peek == "gift" || peek == "learn"))
            {
                endIndex++; continue;
            }
            if (MonsterDomainRules.MonsterPropertyKeys.Contains(peek))
            {
                break;
            }
            endIndex++;
        }

        int count = endIndex - startIndex;
        if (count == 0) return;

        List<string> itemTokens = tokens.GetRange(startIndex, count);
        string payload = string.Join(".", itemTokens);
        i = endIndex - 1;

        string propKey = itemTokens[0].ToLower();
        if (propKey == "t" && itemTokens.Count > 1)
        {
            if (itemTokens[1].ToLower() == "jinx" && itemTokens.Count > 2)
            {
                monster.curses.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(2)), '#'));
                return;
            }
            monster.traits.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(1)), '#'));
            return;
        }

        bool isComplexItemData = false;
        if (payload.StartsWith("(")) isComplexItemData = true;
        else if (payload.Contains("(") || ItemData.TopLevelSplit(payload, '#').Any(p => p.Contains(".")))
        {
            string firstPart = ItemData.TopLevelSplit(payload, '.')[0].ToLower();
            if (firstPart == "k") isComplexItemData = false;
            else isComplexItemData = true;
        }

        if (isComplexItemData)
        {
            ItemData item = new ItemData();
            item.Parse(payload);
            monster.customItems.Add(item);
        }
        else
        {
            monster.items.AddRange(ItemData.TopLevelSplit(payload, '#'));
        }
    }

    private bool IsMonsterEntity(string core)
    {
        if (string.IsNullOrEmpty(core) || core.Contains("replica", StringComparison.OrdinalIgnoreCase)) return false;
        string firstToken = ItemData.TopLevelSplit(core, '.')[0].ToLower();
        if (firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx") return true;
        if (core.Contains(".jinx.", StringComparison.OrdinalIgnoreCase) || core.Contains(".vase.", StringComparison.OrdinalIgnoreCase) || core.Contains(".orb.", StringComparison.OrdinalIgnoreCase) || core.Contains(".rmon.", StringComparison.OrdinalIgnoreCase)) return true;
        foreach (string monsterName in MonsterHelper.FormattedMonsterNames) if (core.Contains(monsterName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (!string.IsNullOrEmpty(baseMonster)) sb.AppendLine($"{indent}Base Monster: {baseMonster}");
        if (hp != 0) sb.AppendLine($"{indent}HP: {hp}");
        if (!string.IsNullOrEmpty(imageOverride)) sb.AppendLine($"{indent}Image Override: {imageOverride}");
        if (!string.IsNullOrEmpty(bal)) sb.AppendLine($"{indent}Balance: {bal}");

        if (payloadData != null)
        {
            sb.AppendLine($"{indent}[✓ Unpacked Monster Payload: {payloadData.GetType().Name}!]");
            if (payloadData is ModifierData md) md.DebugContentsToConsole(indent + "  ");
            else if (payloadData is AbilityData ad) ad.DebugAbilityCompact(indent + "  ");
            else if (payloadData is HeroData hd) hd.DebugContentsToConsoleCompact(indent + "  ");
        }

        if (traits != null && traits.Count > 0) sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (items != null && items.Count > 0) sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");

        if (customItems != null && customItems.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Items ({customItems.Count}):");
            for (int i = 0; i < customItems.Count; i++)
            {
                var ci = customItems[i];
                if (ci != null)
                {
                    sb.AppendLine($"{indent}  [{i}] [✓ Unpacked ItemData]");
                    ci.DebugContentsToConsole(indent + "        ");
                }
            }
        }
        UnityEngine.Debug.Log($"{indent}--- MONSTER DATA DEBUG (COMPACT) ---\n" + sb.ToString());
    }
}
