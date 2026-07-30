using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Linq;

public static class MonsterDomainRules
{
    public static readonly HashSet<string> MonsterPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "triggerhpdata", "onhitdata"
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

    protected override void ParseCore(string data)
    {
        InitializeAsBlank();
        if (string.IsNullOrWhiteSpace(data)) return;
        DetectBracketingState(data);

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string core = StaticBranchTracing.StripOuterParens(chunks[0]);
        List<string> tokens = StaticBranchTracing.TopLevelSplit(core, '.');

        // Unpack outer parens wrapping the core details
        while (tokens.Count > 0 && tokens[0].StartsWith("(") && tokens[0].EndsWith(")"))
        {
            string inner = StaticBranchTracing.StripOuterParens(tokens[0]);
            List<string> innerTokens = StaticBranchTracing.TopLevelSplit(inner, '.');
            tokens.RemoveAt(0);
            tokens.InsertRange(0, innerTokens);
        }

        if (tokens.Count > 0)
        {
            // 1. Extract Base Monster Name / Image
            var tempStream = new TokenStream(tokens);
            if (TryParseSpecialOrNormalImage(tempStream, out string baseName))
            {
                baseMonster = baseName;
                tokens.RemoveRange(0, tempStream.Index);
            }
            else if (!MonsterDomainRules.MonsterPropertyKeys.Contains(tokens[0]))
            {
                baseMonster = tokens[0];
                tokens.RemoveAt(0);
            }

            // 2. Extract Specialized Containers (egg, vase, orb, jinx)
            string firstTokenLower = baseMonster.ToLower();

            /*
            if (firstTokenLower == "egg" || firstTokenLower == "vase" || firstTokenLower == "orb" || firstTokenLower == "jinx" || firstTokenLower == "rmon")
            {
                if (tokens.Count > 0)
                {
                    string rawPayload;
                    if (tokens[0].StartsWith("("))
                    {
                        rawPayload = tokens[0];
                        tokens.RemoveAt(0);
                    }
                    else
                    {
                        rawPayload = string.Join(".", tokens);
                        tokens.Clear();
                    }

                    baseMonster += "." + rawPayload;
                    string corePayload = StaticBranchTracing.StripOuterParens(rawPayload);
            */

            if (firstTokenLower == "egg" || firstTokenLower == "vase" || firstTokenLower == "orb" || firstTokenLower == "jinx" || firstTokenLower == "rmon")
            {
                if (tokens.Count > 0)
                {
                    // Pass everything to the inner payload entity (the Orc) so it owns its abilities
                    string rawPayload = string.Join(".", tokens);
                    tokens.Clear();

                    baseMonster += "." + rawPayload;
                    string corePayload = StaticBranchTracing.StripOuterParens(rawPayload);

                    if (firstTokenLower == "jinx" || firstTokenLower == "vase")
                    {
                        ModifierData mod = new ModifierData();
                        mod.Parse(corePayload);
                        payloadData = mod;
                    }
                    else if (firstTokenLower == "orb")
                    {
                        payloadData = AbilityData.CreateSpellOrTactic(corePayload);
                    }
                    else if (firstTokenLower == "egg")
                    {
                        if (StaticBranchTracing.IsMonsterEntity(corePayload))
                        {
                            MonsterData nestedMonster = new MonsterData();
                            nestedMonster.Parse(corePayload);
                            payloadData = nestedMonster;
                        }
                        else
                        {
                            HeroData nestedHero = new HeroData();
                            nestedHero.Parse(corePayload);
                            payloadData = nestedHero;
                        }
                    }
                }
            }
        }

        // 3. Delegate ALL property, dice, and item parsing to the base TokenStream pipeline in EntityData
        ExtractKnowledge(tokens, _itemPipeline, processTraitsAndCollections: true);
        ExecuteItemPipeline();
        SyncMonsterSize();
    }

    // Handles monster-specific metadata keys during the TokenStream pass
    protected override bool TryProcessSpecificMetadata(TokenStream stream)
    {
        if (stream.Peek().Equals("bal", StringComparison.OrdinalIgnoreCase))
        {
            stream.Consume(); // consume 'bal'
            if (!stream.IsEOF) bal = stream.Consume();
            return true;
        }
        return base.TryProcessSpecificMetadata(stream);
    }
    public static string ExportAsSpirit(MonsterData monster)
    {
        if (monster == null) return string.Empty;
        StringBuilder sb = new StringBuilder();
        sb.Append(FormatName(monster.baseMonster));
        if (!string.IsNullOrEmpty(monster.doc))
        {
            sb.Append($".doc.{monster.doc}");
        }
        return $"({sb.ToString()})";
    }
    private void ExtractKnowledge(List<string> tokens, bool isFirstToken)
    {
        int i = 0;

        if (isFirstToken && tokens.Count > 0)
        {
            string firstToken = tokens[i].ToLower();
            if (firstToken == "rmon" || firstToken == "egg" || firstToken == "vase" || firstToken == "orb" || firstToken == "jinx")
            {
                baseMonster = tokens[i];
                i++;

                if (i < tokens.Count)
                {
                    string rawPayload;
                    if (tokens[i].StartsWith("("))
                    {
                        rawPayload = tokens[i];
                        i++;
                    }
                    else
                    {
                        List<string> remaining = tokens.GetRange(i, tokens.Count - i);
                        rawPayload = string.Join(".", remaining);
                        i = tokens.Count;
                    }

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
                ProcessRecursiveParentheses(originalToken, (innerTokens) => ExtractKnowledge(innerTokens, false));
                continue;
            }

            // TODO: CONVERT TO TOKEN STREAM
            //if (TryProcessCommonMetadata(tokens, ref i, tokenLower)) continue;
            //if (TryProcessEntityMetadata(tokens, ref i, tokenLower)) continue;
            ////if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            //if (TryProcessTriggerData(tokens, ref i, tokenLower)) continue;
            //if (TryProcessAppendedDoc(tokens, ref i, tokenLower)) continue;

            if (tokenLower == "t")
            {
                //ProcessTraitToken(tokens, ref i);
                continue;
            }

            if (tokenLower == "i")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) continue;

                int length = ItemDomainRules.GetItemBlockLength(tokens, startIndex);
                if (length > 0)
                {
                    List<string> itemTokens = tokens.GetRange(startIndex, length);
                    string itemString = string.Join(".", itemTokens);
                    i += length;

                    ItemData parsedItem = new ItemData();
                    parsedItem.Parse(StaticBranchTracing.StripOuterParens(itemString));
                    if (string.IsNullOrEmpty(parsedItem.entityName) && parsedItem.Mechanics.Count == 0)
                        parsedItem.entityName = itemString;

                    _itemPipeline.Add(parsedItem);
                }
                continue;
            }

            switch (tokenLower)
            {
                case "bal": if (i + 1 < tokens.Count) bal = tokens[++i]; break;
            }
        }
    }
    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Determine the correct fallback name
        string displayName = !string.IsNullOrEmpty(entityName) ? entityName : baseMonster;

        if (string.IsNullOrEmpty(entityName) && !string.IsNullOrEmpty(baseMonster))
        {
            int dotIndex = baseMonster.IndexOf('.');
            if (dotIndex > 0)
            {
                string prefix = baseMonster.Substring(0, dotIndex).ToLower();
                if (prefix == "egg" || prefix == "jinx" || prefix == "vase" || prefix == "orb" || prefix == "rmon")
                {
                    // Capitalize the container noun for display (e.g., "egg" -> "Egg")
                    string rawPrefix = baseMonster.Substring(0, dotIndex);
                    displayName = char.ToUpper(rawPrefix[0]) + rawPrefix.Substring(1);
                }
            }
        }

        if (!string.IsNullOrEmpty(displayName)) sb.AppendLine($"{indent}Name: {displayName}");
        if (!string.IsNullOrEmpty(baseMonster) && baseMonster != displayName) sb.AppendLine($"{indent}Base Monster: {baseMonster}");

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
    public void InitializeAsBlank()
    {
        entityName = null;
        imageOverride = null;
        baseMonster = "Wolf";
        bal = null;
        hp = 0; h = 0; s = 0; v = 0; hue = 0;
        doc = null; 
        appendedDoc = null;
        payloadData = null;

        items = new List<string>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();
        baseAbilityData = new List<string>();
        customOnHits = new List<OnHitData>();
        customTriggerHPs = new List<TriggerHPData>();
        customPayloads = new List<CustomPayload>();
        customOrbs = new List<OrbData>();

        visuals.Clear();
        _itemPipeline.Clear();

        // Safely instantiate the array structure
        InitializeDiceFaces();

        // Force clear all existing face data in case we are parsing over an existing object
        for (int i = 0; i < 6; i++)
        {
            diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
        }
    }
    private void SyncMonsterSize()
    {
        if (string.IsNullOrEmpty(baseMonster)) return;

        string cleanName = baseMonster;
        int dotIndex = cleanName.IndexOf('.');
        if (dotIndex != -1)
        {
            cleanName = cleanName.Substring(dotIndex + 1);
            cleanName = StaticBranchTracing.StripOuterParens(cleanName);

            int nextDot = cleanName.IndexOf('.');
            if (nextDot != -1) cleanName = cleanName.Substring(0, nextDot);
        }

        // Map to MonsterType enum to look up size constraints
        if (Enum.TryParse<MonsterType>(cleanName, true, out MonsterType parsedType))
        {
            if (MonsterDatabase.SizeMapping.TryGetValue(parsedType, out MonsterSize mappedSize))
            {
                size = mappedSize;
                return;
            }
        }
        size = MonsterSize.HeroSized; // Default fallback
    }
}