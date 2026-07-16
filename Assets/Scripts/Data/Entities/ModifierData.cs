using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ModifierData : SDData
{
    [Header("Raw Modifier Commands")]
    public List<string> coreCommands = new List<string>();

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        string core = StaticBranchTracing.StripOuterParens(data.Trim());

        List<string> chains = StaticBranchTracing.TopLevelSplit(core, '#');
        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');
            ExtractKnowledge(tokens);
        }
    }

    private void ExtractKnowledge(List<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string originalToken = tokens[i];
            string tokenLower = originalToken.ToLower();

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = StaticBranchTracing.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens);
                continue;
            }

            // =========================================================================
            // NEW: Parse Inline Entities (e.g. Wolf.doc.<desc>) inside a modifier
            // =========================================================================
            if (StaticBranchTracing.IsMonsterEntity(originalToken) || StaticBranchTracing.IsHeroEntity(originalToken))
            {
                int startIndex = i;
                int endIndex = i + 1;

                while (endIndex < tokens.Count)
                {
                    string peek = tokens[endIndex].ToLower();
                    if (EntityDomainRules.CommonMetadataKeys.Contains(peek))
                    {
                        endIndex += 2;
                        continue;
                    }
                    break;
                }

                string entityPayload = string.Join(".", tokens.GetRange(startIndex, endIndex - startIndex));

                if (StaticBranchTracing.IsMonsterEntity(originalToken))
                {
                    MonsterData monster = new MonsterData();
                    // DELETED: monster.entityName = originalToken; (This caused the .n. rename mutation)
                    monster.Parse(entityPayload);
                    customPayloads.Add(new CustomPayload { Prefix = "", Data = monster, Type = PayloadType.Monster });
                }
                else
                {
                    HeroData hero = new HeroData();
                    // DELETED: hero.entityName = originalToken;
                    hero.Parse(entityPayload);
                    customPayloads.Add(new CustomPayload { Prefix = "", Data = hero, Type = PayloadType.Hero });
                }

                i = endIndex - 1;
                continue;
            }

            if (tokenLower == "abilitydata" || tokenLower == "i" || tokenLower == "hat" || tokenLower == "egg" || tokenLower == "rmon" || tokenLower == "add" || tokenLower == "vase" || tokenLower == "jinx")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) break;

                int endIndex = startIndex;
                while (endIndex < tokens.Count)
                {
                    string peek = tokens[endIndex].ToLower();
                    if (peek == "abilitydata" || peek == "i" || peek == "hat" || peek == "egg" || peek == "rmon" || peek == "add" || peek == "vase" || peek == "jinx") break;
                    endIndex++;
                }

                int count = endIndex - startIndex;
                if (count > 0)
                {
                    string payload = string.Join(".", tokens.GetRange(startIndex, count));
                    i = endIndex - 1;

                    if (tokenLower == "abilitydata")
                        customPayloads.Add(new CustomPayload { Prefix = tokenLower, Data = AbilityData.CreateAbility(payload) });
                    else if (tokenLower == "i")
                    {
                        ItemData item = new ItemData(); item.Parse(payload);
                        customPayloads.Add(new CustomPayload { Prefix = tokenLower, Data = item, Type = PayloadType.Item });
                    }
                    else if (tokenLower == "egg" || tokenLower == "rmon")
                    {
                        MonsterData monster = new MonsterData(); monster.Parse(originalToken + "." + payload);
                        customPayloads.Add(new CustomPayload { Prefix = "", Data = monster, Type = PayloadType.Monster });
                    }
                    else if (tokenLower == "hat")
                    {
                        if (StaticBranchTracing.IsMonsterEntity(payload))
                        {
                            MonsterData m = new MonsterData(); m.Parse(payload);
                            customPayloads.Add(new CustomPayload { Prefix = "hat", Data = m, Type = PayloadType.Monster });
                        }
                        else
                        {
                            HeroData h = new HeroData(); h.Parse(payload);
                            customPayloads.Add(new CustomPayload { Prefix = "hat", Data = h, Type = PayloadType.Hero });
                        }
                    }
                    else if (tokenLower == "add")
                    {
                        if (StaticBranchTracing.IsMonsterEntity(payload))
                        {
                            MonsterData m = new MonsterData(); m.Parse(payload);
                            customPayloads.Add(new CustomPayload { Prefix = "add", Data = m, Type = PayloadType.Monster });
                        }
                        else
                        {
                            HeroData h = new HeroData(); h.Parse(payload);
                            customPayloads.Add(new CustomPayload { Prefix = "add", Data = h, Type = PayloadType.Hero });
                        }
                    }
                    else if (tokenLower == "vase" || tokenLower == "jinx")
                    {
                        ModifierData mod = new ModifierData();

                        // FIX: Parse ONLY the inner payload to prevent infinite recursion
                        string corePayload = StaticBranchTracing.StripOuterParens(payload);
                        mod.Parse(corePayload);

                        // Set Prefix to tokenLower so we preserve "vase" or "jinx" during export
                        customPayloads.Add(new CustomPayload { Prefix = tokenLower, Data = mod, Type = PayloadType.Modifier });
                    }
                }
            }
            else
            {
                coreCommands.Add(originalToken);
            }
        }
    }

    public override string Export()
    {
        List<string> parts = new List<string>();

        // 1. Export inline payloads FIRST (e.g. "Wolf.doc.<description>")
        foreach (var cp in customPayloads)
        {
            if (string.IsNullOrEmpty(cp.Prefix))
            {
                string exp = string.Empty;

                // Structurally identify inline spirits instead of relying on a fake prefix string
                if (cp.Type == PayloadType.Monster && cp.Data is MonsterData md && StaticBranchTracing.IsMonsterEntity(md.baseMonster))
                {
                    exp = MonsterData.ExportAsSpirit(md);
                }
                else if (cp.Type == PayloadType.Hero && cp.Data is HeroData hd && StaticBranchTracing.IsHeroEntity(hd.entityName)) //MIGHT BE A BUG, MIGHT BE WRONG?
                {
                    exp = hd.Export(); // Standard export for inline heroes unless they also have a Spirit version
                }
                else
                {
                    exp = cp.Export(); // Fallback for things like 'egg' which also share the empty prefix
                }

                if (!string.IsNullOrEmpty(exp)) parts.Add(exp);
            }
        }

        // 2. Export structural Core Commands (e.g. "spirit", "cantrip")
        if (coreCommands.Count > 0)
        {
            parts.AddRange(coreCommands);
        }

        // 3. Export explicitly prefixed payloads LAST (e.g. "abilitydata.x", "hat.x")
        foreach (var cp in customPayloads)
        {
            if (!string.IsNullOrEmpty(cp.Prefix))
            {
                string exp = cp.Export();
                if (!string.IsNullOrEmpty(exp)) parts.Add(exp);
            }
        }

        return string.Join(".", parts);
    }

    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- MODIFIER DATA DEBUG ---");
        if (coreCommands.Count > 0) sb.AppendLine($"{indent}Core Commands: {string.Join(".", coreCommands)}");

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

        UnityEngine.Debug.Log(sb.ToString());
    }
}