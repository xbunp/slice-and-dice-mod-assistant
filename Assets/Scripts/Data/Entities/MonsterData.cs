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
        "i", "rmon", "n", "hp", "egg", "sd", "doc", "jinx", "vase", "orb", "t", "bal", "img", "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "triggerhpdata", "onhitdata"
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

    [System.NonSerialized]
    private List<ItemData> _itemPipeline = new List<ItemData>();

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        customTriggerHPs = new List<TriggerHPData>();
        customOnHits = new List<OnHitData>();
        customPayloads = new List<CustomPayload>();
        _itemPipeline.Clear();

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string core = StaticBranchTracing.StripOuterParens(chunks[0]);
        List<string> tokens = StaticBranchTracing.TopLevelSplit(core, '.');

        bool isFirstToken = true;
        ExtractKnowledge(tokens, isFirstToken);
        ExecuteItemPipeline();

    }

    protected override int GetEndOfBlockIndex(List<string> tokens, int startIndex)
    {
        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();

            if (peek == "i" || peek == "t" || peek == "triggerhpdata" || peek == "onhitdata" || peek == "orb" || peek == "sd")
            {
                break;
            }

            if (MonsterDomainRules.MonsterPropertyKeys.Contains(peek))
            {
                if (peek == "hp" && endIndex + 1 < tokens.Count)
                {
                    if (int.TryParse(tokens[endIndex + 1], out _)) break;
                }
                else if (peek == "n" || peek == "img" || peek == "doc" || peek == "bal" ||
                         peek == "hsv" || peek == "hue" || peek == "thue" || peek == "p" ||
                         peek == "b" || peek == "draw" || peek == "rect")
                {
                    break;
                }
            }
            endIndex++;
        }
        return endIndex;
    }
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

        if (monster.thue != null && monster.thue.colorOffset != 0) sb.Append($".{PackTHue(monster.thue)}");

        monster.AppendDiceSides(sb);

        string faceModifiers = monster.BuildFaceModifiers(includeInlineFacades: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            faceModifiers = Regex.Replace(faceModifiers, @"(\.facade\.[^.:\s]+)(?=\.|$)", "$1:0");
            sb.Append(faceModifiers);
        }

        if (!string.IsNullOrEmpty(monster.doc)) sb.Append($".doc.{monster.doc}");
        if (hasImageOverride) { sb.Append($".img.{FormatName(monster.imageOverride)}"); monster.AppendColorModifier(sb); }

        if (monster.traits != null) foreach (var t in monster.traits) if (!string.IsNullOrEmpty(t)) sb.Append($".t.{FormatName(t)}");
        if (monster.customOrbs != null) foreach (var orb in monster.customOrbs) if (orb != null) sb.Append($".{orb.ExportAsTrait(useITPrefix: false)}"); // Added
        if (monster.items != null) foreach (var i in monster.items) if (!string.IsNullOrEmpty(i)) sb.Append($".i.{FormatName(i)}");

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

            if (TryProcessCommonMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessEntityMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            if (TryProcessTriggerData(tokens, ref i, tokenLower)) continue;

            if (tokenLower == "triggerhpdata" || tokenLower == "onhitdata")
            {
                if (i + 1 < tokens.Count)
                {
                    string payload = tokens[++i];
                    if (payload.StartsWith("("))
                    {
                        var parsedAbility = AbilityData.CreateAbility(payload);
                        if (parsedAbility is TriggerHPData trigHP)
                        {
                            if (customTriggerHPs == null) customTriggerHPs = new List<TriggerHPData>();
                            customTriggerHPs.Add(trigHP);
                        }
                        else if (parsedAbility is OnHitData onHit)
                        {
                            if (customOnHits == null) customOnHits = new List<OnHitData>();
                            customOnHits.Add(onHit);
                        }
                    }
                    else
                    {
                        baseAbilityData.Add(payload);
                    }
                }
                continue;
            }

            if (tokenLower == "t")
            {
                ProcessTraitToken(tokens, ref i, MonsterDomainRules.MonsterPropertyKeys);
                continue;
            }

            if (tokenLower == "i")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) continue;

                int endIndex = GetEndOfBlockIndex(tokens, startIndex);
                int count = endIndex - startIndex;

                if (count > 0)
                {
                    List<string> itemTokens = tokens.GetRange(startIndex, count);
                    string itemString = string.Join(".", itemTokens);
                    i = endIndex - 1;

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


    /*
         public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        customTriggerHPs = new List<TriggerHPData>();
        customOnHits = new List<OnHitData>();
        customPayloads = new List<CustomPayload>();

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string core = StaticBranchTracing.StripOuterParens(chunks[0]);

        // FIX: Do NOT split by '#' at the top level. Process as a single chain.
        List<string> tokens = StaticBranchTracing.TopLevelSplit(core, '.');

        bool isFirstToken = true;
        ExtractKnowledge(tokens, isFirstToken);
    }

    */
}