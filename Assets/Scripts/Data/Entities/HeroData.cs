using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class HeroDomainRules
{
    public static readonly string[] HeroPropertyKeys =
    {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata"
    };

    public static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "n", "img", "col", "hp", "tier", "hsv", "hsl", "hue",
        "p", "b", "rect", "draw", "thue", "adj", "speech", "doc"
    };
}

[System.Serializable]
public class HeroData : EntityData
{
    [Header("Hero Specific Info")]
    public string baseReplica;
    public string colorClass;
    public int tier;
    public int? adj;

    [Header("Hero Modifiers")]
    public List<string> baseAbilityData;

    [Header("Post-Dice Info")]
    public string speech;

    [SerializeField] public List<SpellData> customSpells;
    [SerializeField] public List<TacticData> customTactics;

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

    public void InitializeAsBlank()
    {
        entityName = null; imageOverride = null; baseReplica = null; colorClass = null;
        hp = 0; h = 0; s = 0; v = 0; tier = 0; hue = 0;
        hsl = null; p = null; b = null; rect = null; draw = null; thue = null; doc = null; speech = null; adj = null;
        items = new List<string>(); customItems = new List<ItemData>(); traits = new List<string>();
        blessings = new List<string>(); curses = new List<string>(); baseAbilityData = new List<string>();
        customSpells = new List<SpellData>(); customTactics = new List<TacticData>();
        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
    }

    public void InitializeAsDefault()
    {
        entityName = "NewEntity"; baseReplica = "Statue"; colorClass = "y"; imageOverride = "None"; hp = 7; tier = 1;
    }

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = ItemData.TopLevelSplit(data.Trim(), '&');
        string heroCore = chunks[0];

        if (heroCore.StartsWith("(") && heroCore.EndsWith(")"))
        {
            heroCore = heroCore.Substring(1, heroCore.Length - 2);
        }

        List<string> tokens = ItemData.TopLevelSplit(heroCore, '.');

        // Check if the very first token is an implicit base replica (e.g. "Statue.sd.187...")
        if (tokens.Count > 0)
        {
            string firstLower = tokens[0].ToLower();
            if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
            {
                baseReplica = tokens[0];
            }
        }

        ExtractKnowledge(tokens, this);
    }

    private void ExtractKnowledge(List<string> tokens, HeroData hero)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerTokens = ItemData.TopLevelSplit(inner, '.');
                ExtractKnowledge(innerTokens, hero);
                continue;
            }

            if (TryProcessMetadata(tokens, ref i, tokenLower)) continue;
            else if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            else if (TryProcessCollections(tokens, ref i, tokenLower)) continue;
            else if (tokenLower == "i") ProcessItemProperty(tokens, ref i);
        }
    }

    private bool TryProcessMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (!HeroDomainRules.MetadataKeys.Contains(tokenLower)) return false;
        if (i + 1 >= tokens.Count) return false;

        string nextVal = tokens[++i]; // Pristine casing

        switch (tokenLower)
        {
            case "replica": baseReplica = nextVal; break;
            case "n": entityName = nextVal; break;
            case "img": imageOverride = nextVal; break;
            case "col": colorClass = nextVal; break;
            case "hp": if (int.TryParse(nextVal, out int hpVal)) hp = hpVal; break;
            case "tier": if (int.TryParse(nextVal, out int t)) tier = t; break;
            case "hsv":
                string[] hsvParts = nextVal.Split(':');
                if (hsvParts.Length == 3)
                {
                    int.TryParse(hsvParts[0], out h);
                    int.TryParse(hsvParts[1], out s);
                    int.TryParse(hsvParts[2], out v);
                }
                break;
            case "hsl": hsl = nextVal; break;
            case "hue": if (int.TryParse(nextVal, out int hVal)) hue = hVal; break;
            case "p": p = nextVal; break;
            case "b": b = nextVal; break;
            case "rect": rect = nextVal; break;
            case "draw": draw = nextVal; break;
            case "thue": thue = nextVal; break;
            case "adj": if (int.TryParse(nextVal, out int a)) adj = a; break;
            case "speech": speech = nextVal; break;
            case "doc": doc = nextVal; break;
        }
        return true;
    }

    private bool TryProcessDiceSides(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower != "sd" || i + 1 >= tokens.Count) return false;

        InitializeDiceFaces();

        string[] faces = tokens[++i].Split(':');
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
        return true;
    }

    private bool TryProcessCollections(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;

        if (tokenLower == "t") { traits.AddRange(ItemData.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "gift") { blessings.AddRange(ItemData.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "abilitydata")
        {
            string payload = tokens[++i];
            if (payload.StartsWith("(")) { AddCustomAbility(AbilityData.CreateAbility(payload)); }
            else { baseAbilityData.AddRange(ItemData.TopLevelSplit(payload, '#')); }
            return true;
        }
        return false;
    }

    private void ProcessItemProperty(List<string> tokens, ref int i)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        // --- GATHER LOOP ---
        // Fast forward until we hit a token that belongs to the Hero, not the Item.
        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();

            // "t", "gift", "learn" can follow "i" directly as item subtypes (e.g. i.t.jinx)
            if (endIndex == startIndex && (peek == "t" || peek == "gift" || peek == "learn"))
            {
                endIndex++; continue;
            }

            // Break if we hit a genuine top-level Hero property
            if (HeroDomainRules.MetadataKeys.Contains(peek) || peek == "i" || peek == "sd" || peek == "abilitydata")
            {
                break;
            }
            endIndex++;
        }

        int count = endIndex - startIndex;
        if (count == 0) return;

        List<string> itemTokens = tokens.GetRange(startIndex, count);
        string payload = string.Join(".", itemTokens); // Reconstruct pristine string
        i = endIndex - 1; // Advance the outer loop

        // Sub-collections logic
        string propKey = itemTokens[0].ToLower();
        if (propKey == "t" && itemTokens.Count > 1)
        {
            if (itemTokens[1].ToLower() == "jinx" && itemTokens.Count > 2)
            {
                curses.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(2)), '#'));
                return;
            }
            traits.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(1)), '#'));
            return;
        }
        if (propKey == "gift" && itemTokens.Count > 1) { blessings.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(1)), '#')); return; }
        if (propKey == "learn" && itemTokens.Count > 1) { baseAbilityData.AddRange(ItemData.TopLevelSplit(string.Join(".", itemTokens.Skip(1)), '#')); return; }

        // Topology Check
        bool isComplexItemData = false;
        if (payload.StartsWith("(")) isComplexItemData = true;
        else if (payload.Contains("(") || ItemData.TopLevelSplit(payload, '#').Any(p => p.Contains(".")))
        {
            string firstPart = ItemData.TopLevelSplit(payload, '.')[0].ToLower();
            if (firstPart == "k") isComplexItemData = false; // i.k.keyword exception
            else isComplexItemData = true;
        }

        // Handoff
        if (isComplexItemData)
        {
            ItemData item = new ItemData();
            item.Parse(payload);
            customItems.Add(item);
        }
        else
        {
            items.AddRange(ItemData.TopLevelSplit(payload, '#'));
        }
    }

    public override string Export()
    {
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
        {
            heroSb.Append($"replica.{FormatName(baseReplica)}");
            if (!hasImageOverride) AppendColorModifier(heroSb);
        }

        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");
        if (!string.IsNullOrEmpty(colorClass) && !IsDefaultColor(baseReplica, colorClass)) heroSb.Append($".col.{colorClass}");
        if (hp > 0) heroSb.Append($".hp.{hp}");
        if (tier > 0) heroSb.Append($".tier.{tier}");
        if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");
        if (!string.IsNullOrEmpty(thue)) heroSb.Append($".thue.{thue}");

        AppendDiceSides(heroSb);

        if (!string.IsNullOrEmpty(speech)) heroSb.Append($".speech.{speech}");
        if (!string.IsNullOrEmpty(doc)) heroSb.Append($".doc.{doc}");

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        if (hasImageOverride)
        {
            heroSb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(heroSb);
        }
        heroSb.Append(")");

        StringBuilder thoseSb = new StringBuilder();
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
        if (customItems != null) foreach (var ci in customItems) if (ci != null) thoseSb.Append($".i.({ci.Export()})");
        if (blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) thoseSb.Append($".gift.{FormatName(bl)}");
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
        if (baseAbilityData != null) foreach (var ab in baseAbilityData) if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");
        if (customAbilityData != null) foreach (var cab in customAbilityData) if (cab != null) thoseSb.Append($".abilitydata.({cab.Export()})");

        if (thoseSb.Length == 0) return heroSb.ToString();
        return $"({heroSb.ToString()}{thoseSb.ToString()})";
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

    public void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();
        if (ability is SpellData spell) { if (!customSpells.Any(s => s.entityName == spell.entityName)) customSpells.Add(spell); }
        else if (ability is TacticData tactic) { if (!customTactics.Any(t => t.entityName == tactic.entityName)) customTactics.Add(tactic); }
    }

    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(entityName)) sb.AppendLine($"{indent}Name: {entityName}");
        if (baseReplica != null && !string.IsNullOrEmpty(baseReplica.ToString())) sb.AppendLine($"{indent}Base Replica: {baseReplica}");
        if (!string.IsNullOrEmpty(colorClass)) sb.AppendLine($"{indent}Color Class: {colorClass}");
        if (tier != 0) sb.AppendLine($"{indent}Tier: {tier}");
        if (hp != 0) sb.AppendLine($"{indent}HP: {hp}");
        if (!string.IsNullOrEmpty(imageOverride)) sb.AppendLine($"{indent}Image Override: {imageOverride}");

        if (diceSides != null && diceSides.Length > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                if (side != null && (side.effectID != 0 || side.pips != 0))
                {
                    if (!headerPrinted) { sb.AppendLine($"{indent}Dice Sides:"); headerPrinted = true; }
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0) sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (blessings != null && blessings.Count > 0) sb.AppendLine($"{indent}Blessings: {string.Join(", ", blessings)}");
        if (curses != null && curses.Count > 0) sb.AppendLine($"{indent}Curses: {string.Join(", ", curses)}");
        if (baseAbilityData != null && baseAbilityData.Count > 0) sb.AppendLine($"{indent}Base Abilities: {string.Join(", ", baseAbilityData)}");
        if (items != null && items.Count > 0) sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");

        if (customItems != null && customItems.Count > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < customItems.Count; i++)
            {
                var ci = customItems[i];
                if (ci != null)
                {
                    if (!headerPrinted) { sb.AppendLine($"{indent}Custom Items ({customItems.Count}):"); headerPrinted = true; }
                    sb.AppendLine($"{indent}  [{i}] [✓ Unpacked ItemData]");
                    ci.DebugContentsToConsole(indent + "        ");
                }
            }
        }
        if (sb.Length > 0) UnityEngine.Debug.Log($"{indent}--- HERO DATA DEBUG (COMPACT) ---\n" + sb.ToString());
    }
}