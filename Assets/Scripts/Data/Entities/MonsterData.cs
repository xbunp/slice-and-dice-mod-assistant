
// ==========================================
// 5. MONSTER DATA
// ==========================================

using System;
using System.Collections.Generic;
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
    public object payloadData;

    [Header("Monster Specific Info")]
    public string baseMonster = "Wolf";
    public MonsterSize size = MonsterSize.HeroSized;

    [Header("Monster Modifiers")]
    public string bal;

    public static string Export(MonsterData monster)
    {
        if (monster == null) return string.Empty;
        StringBuilder sb = new StringBuilder();

        bool hasImageOverride = !string.IsNullOrEmpty(monster.imageOverride) && monster.imageOverride != "None" && monster.imageOverride != monster.baseMonster;

        sb.Append($"{FormatName(monster.baseMonster)}");
        if (!hasImageOverride) monster.AppendColorModifier(sb);
        if (!string.IsNullOrEmpty(monster.entityName)) sb.Append($".n.{FormatName(monster.entityName)}");
        if (monster.hp > 0) sb.Append($".hp.{monster.hp}");

        if (!string.IsNullOrEmpty(monster.p)) sb.Append($".p.{monster.p}");
        if (!string.IsNullOrEmpty(monster.b)) sb.Append($".b.{monster.b}");
        if (!string.IsNullOrEmpty(monster.rect)) sb.Append($".rect.{monster.rect}");
        if (!string.IsNullOrEmpty(monster.draw)) sb.Append($".draw.{monster.draw}");
        if (!string.IsNullOrEmpty(monster.thue)) sb.Append($".thue.{monster.thue}");

        monster.AppendDiceSides(sb);

        string faceModifiers = monster.BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            faceModifiers = Regex.Replace(faceModifiers, @"(\.facade\.[^.:\s]+)(?=\.|$)", "$1:0");
            sb.Append(faceModifiers);
        }

        if (!string.IsNullOrEmpty(monster.doc)) sb.Append($".doc.{monster.doc}");
        if (hasImageOverride) { sb.Append($".img.{FormatName(monster.imageOverride)}"); monster.AppendColorModifier(sb); }

        if (monster.traits != null) foreach (var t in monster.traits) if (!string.IsNullOrEmpty(t)) sb.Append($".t.{FormatName(t)}");
        if (monster.items != null) foreach (var i in monster.items) if (!string.IsNullOrEmpty(i)) sb.Append($".i.{FormatName(i)}");

        if (monster.customPayloads != null)
        {
            foreach (var payload in monster.customPayloads)
            {
                string exported = payload.Export();
                if (!string.IsNullOrEmpty(exported)) sb.Append($".{exported}");
            }
        }

        if (monster.curses != null) foreach (var c in monster.curses) if (!string.IsNullOrEmpty(c)) sb.Append($".t.jinx.{FormatName(c)}");
        if (!string.IsNullOrEmpty(monster.bal)) sb.Append($".bal.{FormatName(monster.bal)}");

        return sb.ToString();
    }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string core = StaticBranchTracing.StripOuterParens(chunks[0]);

        List<string> chains = StaticBranchTracing.TopLevelSplit(core, '#');
        bool isFirstChain = true;
        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');

            bool isFirstToken = isFirstChain;
            isFirstChain = false;

            ExtractKnowledge(tokens, ref isFirstToken);
        }
    }

    private void ExtractKnowledge(List<string> tokens, ref bool isFirstToken)
    {
        int i = 0;

        if (isFirstToken && tokens.Count > 0)
        {
            isFirstToken = false;
            string firstToken = tokens[i].ToLower();
            if (firstToken == "rmon" || firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx")
            {
                baseMonster = tokens[i];
                i++;

                if (i < tokens.Count && !MonsterDomainRules.MonsterPropertyKeys.Contains(tokens[i].ToLower()))
                {
                    string rawPayload = tokens[i];
                    baseMonster += "." + rawPayload;

                    string corePayload = StaticBranchTracing.StripOuterParens(rawPayload);
                    if (firstToken == "jinx" || firstToken == "vase")
                    {
                        ModifierData mod = new ModifierData(); mod.Parse(corePayload); payloadData = mod;
                    }
                    else if (firstToken == "orb")
                    {
                        payloadData = AbilityData.CreateSpellOrTactic(corePayload);
                    }
                    else if (firstToken == "egg")
                    {
                        if (StaticBranchTracing.IsMonsterEntity(corePayload))
                        {
                            MonsterData nestedMonster = new MonsterData(); nestedMonster.Parse(corePayload); payloadData = nestedMonster;
                        }
                        else
                        {
                            HeroData nestedHero = new HeroData(); nestedHero.Parse(corePayload); payloadData = nestedHero;
                        }
                    }
                    i++;
                }
            }
            else if (!MonsterDomainRules.MonsterPropertyKeys.Contains(firstToken))
            {
                baseMonster = tokens[i];
                i++;
            }
        }

        for (; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerChains = StaticBranchTracing.TopLevelSplit(inner, '#');
                foreach (var chain in innerChains)
                {
                    if (string.IsNullOrWhiteSpace(chain)) continue;
                    List<string> innerTokens = StaticBranchTracing.TopLevelSplit(chain, '.');
                    ExtractKnowledge(innerTokens, ref isFirstToken);
                }
                continue;
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) entityName = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) imageOverride = tokens[++i]; break;
                case "doc": if (i + 1 < tokens.Count) doc = tokens[++i]; break;
                case "bal": if (i + 1 < tokens.Count) bal = tokens[++i]; break;
                case "hp": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hVal)) hp = hVal; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsvArr = tokens[++i].Split(':');
                        if (hsvArr.Length == 3 && int.TryParse(hsvArr[0], out h) && int.TryParse(hsvArr[1], out s) && int.TryParse(hsvArr[2], out v)) { }
                    }
                    break;
                case "hsl": if (i + 1 < tokens.Count) hsl = tokens[++i]; break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) hue = hueVal; break;
                case "p": if (i + 1 < tokens.Count) p = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) b = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) rect = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) draw = tokens[++i]; break;
                case "thue": if (i + 1 < tokens.Count) thue = tokens[++i]; break;
                case "sd":
                    if (i + 1 < tokens.Count)
                    {
                        string[] faces = tokens[++i].Split(':');
                        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                        {
                            if (faces[f] == "0" || faces[f] == "0-0") continue;
                            string[] faceParts = faces[f].Split('-');
                            int.TryParse(faceParts[0], out diceSides[f].effectID);
                            if (faceParts.Length > 1) int.TryParse(faceParts[1], out diceSides[f].pips);
                        }
                    }
                    break;
                case "i":
                    StaticBranchTracing.ProcessAndRouteProperty(tokens, ref i, MonsterDomainRules.MonsterPropertyKeys, this);
                    break;
                case "t":
                    int startIndex = i + 1;
                    if (startIndex >= tokens.Count) break;

                    int endIndex = startIndex;
                    while (endIndex < tokens.Count)
                    {
                        string peek = tokens[endIndex].ToLower();
                        if (MonsterDomainRules.MonsterPropertyKeys.Contains(peek)) break;
                        endIndex++;
                    }

                    int count = endIndex - startIndex;
                    if (count > 0)
                    {
                        string tPayload = string.Join(".", tokens.GetRange(startIndex, count));
                        i = endIndex - 1;

                        if (tPayload.Contains("("))
                        {
                            ModifierData nestedMod = new ModifierData();
                            nestedMod.Parse(tPayload);
                            customPayloads.Add(new CustomPayload { Prefix = "t", Data = nestedMod });
                        }
                        else traits.AddRange(StaticBranchTracing.TopLevelSplit(tPayload, '#'));
                    }
                    break;
            }
        }
    }

    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (!string.IsNullOrEmpty(baseMonster)) sb.AppendLine($"{indent}Base Monster: {baseMonster}");
        if (hp != 0) sb.AppendLine($"{indent}HP: {hp}");
        if (!string.IsNullOrEmpty(imageOverride))
        {
            string displayValue = imageOverride.Length > 32 ? "<base64 string img>" : imageOverride;
            sb.AppendLine($"{indent}Image Override: {displayValue}");
        }
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

        if (customPayloads != null && customPayloads.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Payloads ({customPayloads.Count}):");
            for (int i = 0; i < customPayloads.Count; i++)
            {
                var cp = customPayloads[i];
                sb.AppendLine($"{indent}  [{i}] Prefix: '{cp.Prefix}' | [✓ Unpacked {cp.Data?.GetType().Name}]");

                if (cp.Data is ItemData id) id.DebugContentsToConsole(indent + "        ");
                else if (cp.Data is HeroData hd) hd.DebugContentsToConsoleCompact(indent + "        ");
                else if (cp.Data is AbilityData ad) ad.DebugAbilityCompact(indent + "        ");
                else if (cp.Data is ModifierData md) md.DebugContentsToConsole(indent + "        ");
                else if (cp.Data is MonsterData mnd) mnd.DebugContentsToConsoleCompact(indent + "        ");
            }
        }
        UnityEngine.Debug.Log($"{indent}--- MONSTER DATA DEBUG (COMPACT) ---\n" + sb.ToString());
    }
}