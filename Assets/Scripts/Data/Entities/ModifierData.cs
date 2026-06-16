using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


[System.Serializable]
public class ModifierData : SDData
{
    [Header("Raw Modifier Commands")]
    public List<string> coreCommands = new List<string>();

    [Header("Nested Domain Data")]
    public List<AbilityData> customAbilities = new List<AbilityData>();
    public List<ItemData> customItems = new List<ItemData>();
    public List<MonsterData> customMonsters = new List<MonsterData>();

    // ==========================================
    // KNOWLEDGE EXTRACTION (The Perfect Linear Parser)
    // ==========================================

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        // 1. Split by '#' first to handle multiple chained modifiers in a sequence
        List<string> subModifiers = ItemData.TopLevelSplit(data.Trim(), '#');

        foreach (var subMod in subModifiers)
        {
            if (string.IsNullOrWhiteSpace(subMod)) continue;

            string core = StripOuterParens(subMod);
            List<string> tokens = ItemData.TopLevelSplit(core, '.');

            ExtractKnowledge(tokens);
        }
    }

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

    private void ExtractKnowledge(List<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string originalToken = tokens[i];
            string tokenLower = originalToken.ToLower();

            // Unwrap trapped parens
            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = ItemData.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens);
                continue;
            }

            // Bridge Handoffs: If we hit a known container, gather it and pass it down the pipeline.
            if (tokenLower == "abilitydata" || tokenLower == "i" || tokenLower == "hat" || tokenLower == "egg" || tokenLower == "rmon")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) break;

                int endIndex = startIndex;
                while (endIndex < tokens.Count)
                {
                    string peek = tokens[endIndex].ToLower();
                    // Break if we hit another top-level structural modifier key
                    if (peek == "abilitydata" || peek == "i" || peek == "hat" || peek == "egg") break;
                    endIndex++;
                }

                int count = endIndex - startIndex;
                if (count > 0)
                {
                    string payload = string.Join(".", tokens.GetRange(startIndex, count));
                    i = endIndex - 1; // Advance outer loop

                    if (tokenLower == "abilitydata")
                    {
                        customAbilities.Add(AbilityData.CreateAbility(payload));
                    }
                    else if (tokenLower == "i")
                    {
                        ItemData item = new ItemData();
                        item.Parse(payload);
                        customItems.Add(item);
                    }
                    else if (tokenLower == "hat" || tokenLower == "egg" || tokenLower == "rmon")
                    {
                        // If it's hat/egg, pass the prefix + payload to MonsterData to figure out
                        MonsterData monster = new MonsterData();
                        monster.Parse(originalToken + "." + payload);
                        customMonsters.Add(monster);
                    }
                }
            }
            else
            {
                // It's a raw modifier command like "ea", "sthief", "summon", "temporary". 
                // We just save it safely so it isn't lost.
                coreCommands.Add(originalToken);
            }
        }
    }

    public override string Export()
    {
        List<string> parts = new List<string>(coreCommands);

        foreach (var ab in customAbilities) if (ab != null) parts.Add($"abilitydata.({ab.Export()})");
        foreach (var itm in customItems) if (itm != null) parts.Add($"i.({itm.Export()})");
        foreach (var mon in customMonsters) if (mon != null) parts.Add(mon.Export());

        return string.Join(".", parts);
    }

    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- MODIFIER DATA DEBUG ---");

        if (coreCommands.Count > 0)
            sb.AppendLine($"{indent}Core Commands: {string.Join(".", coreCommands)}");

        if (customAbilities.Count > 0)
        {
            for (int i = 0; i < customAbilities.Count; i++)
            {
                sb.AppendLine($"{indent}  [✓ Unpacked AbilityData!]");
                customAbilities[i]?.DebugAbilityCompact(indent + "    ");
            }
        }

        if (customItems.Count > 0)
        {
            for (int i = 0; i < customItems.Count; i++)
            {
                sb.AppendLine($"{indent}  [✓ Unpacked ItemData!]");
                customItems[i]?.DebugContentsToConsole(indent + "    ");
            }
        }

        if (customMonsters.Count > 0)
        {
            for (int i = 0; i < customMonsters.Count; i++)
            {
                sb.AppendLine($"{indent}  [✓ Unpacked MonsterData!]");
                customMonsters[i]?.DebugContentsToConsoleCompact(indent + "    ");
            }
        }

        UnityEngine.Debug.Log(sb.ToString());
    }
}