
// ==========================================
// 3. HERO DATA
// ==========================================

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class HeroDomainRules
{
    // Change from string[] to HashSet<string>
    public static readonly HashSet<string> HeroPropertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata", "orb"
    };

    public static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "replica", "n", "img", "col", "hp", "tier", "hsv", "hsl", "hue",
        "p", "b", "rect", "draw", "thue", "adj", "speech", "doc"
    };

    public static readonly HashSet<string> HeroItemBoundaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "abilitydata", "triggerhpdata", "onhitdata", "sd", "hp", "tier", "col", "orb"
    };
}

[System.Serializable]
public class HeroData : EntityData
{
    public string baseReplica;
    public string colorClass;
    public int tier;
    public int? adj;
    public string speech;

    // Change from [SerializeField] public List<SpellData> customSpells;
    [System.NonSerialized] // Tells Unity's serializer to ignore this field
    [JsonProperty]         // Tells Newtonsoft to keep serializing this field
    public List<SpellData> customSpells;

    [System.NonSerialized]
    [JsonProperty]
    public List<TacticData> customTactics;

    public override IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>(base.customAbilityData); // Safely grabs base orbs
            if (customSpells != null) combined.AddRange(customSpells);
            if (customTactics != null) combined.AddRange(customTactics);
            return combined;
        }
    }

    public void InitializeAsDefault()
    {
        InitializeAsBlank();
        entityName = "NewEntity"; baseReplica = "Statue"; colorClass = "y"; imageOverride = "None"; hp = 7; tier = 1;
    }

    public void InitializeAsBlank()
    {
        entityName = null; imageOverride = null; baseReplica = null; colorClass = null;
        hp = 0; h = 0; s = 0; v = 0; tier = 0; hue = 0;
        p = null; b = null; rect = null; draw = null; thue = null; doc = null; speech = null; adj = null; phue = null;

        items = new List<string>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();
        baseAbilityData = new List<string>();
        customSpells = new List<SpellData>();
        customTactics = new List<TacticData>();
        customOnHits = new List<OnHitData>();
        customTriggerHPs = new List<TriggerHPData>(); 
        customPayloads = new List<CustomPayload>();
        customOrbs = new List<OrbData>();
        thue = new Thue();
        phue = new Phue();

        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
    }

    public override void Parse(string data)
    {
        InitializeAsBlank(); // Ensure we start completely clean
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string heroCore = StaticBranchTracing.StripOuterParens(chunks[0]);

        // FIX: Do NOT split by '#' at the top level. Process as a single chain.
        List<string> tokens = StaticBranchTracing.TopLevelSplit(heroCore, '.');

        if (tokens.Count > 0)
        {
            string firstLower = tokens[0].ToLower();
            if (!HeroDomainRules.MetadataKeys.Contains(firstLower) && firstLower != "i" && firstLower != "sd" && firstLower != "t")
            {
                baseReplica = tokens[0];
            }
        }

        ExtractKnowledge(tokens);
    }

    private void ExtractKnowledge(List<string> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                ProcessRecursiveParentheses(originalToken, ExtractKnowledge);
                continue;
            }

            if (TryProcessCommonMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessEntityMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessHeroSpecificMetadata(tokens, ref i, tokenLower)) continue;
            if (TryProcessDiceSides(tokens, ref i, tokenLower)) continue;
            if (TryProcessTriggerData(tokens, ref i, tokenLower)) continue;
            if (TryProcessOrbData(tokens, ref i, tokenLower)) continue;

            if (tokenLower == "t")
            {
                ProcessTraitToken(tokens, ref i, HeroDomainRules.HeroPropertyKeys);
                continue;
            }

            if (TryProcessCollections(tokens, ref i, tokenLower)) continue;

            // trying to fix some shit 
            /*
            if (tokenLower == "i")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) continue;

                // --- FIX: Intercept Dice Face Modifiers (e.g. i.mid.facade.Aid0:0) ---
                if (startIndex + 2 < tokens.Count)
                {
                    string sideToken = tokens[startIndex].ToLower();
                    int faceIndex = -1;

                    if (sideToken == "left") faceIndex = 0;
                    else if (sideToken == "mid") faceIndex = 1;
                    else if (sideToken == "top") faceIndex = 2;
                    else if (sideToken == "bot") faceIndex = 3;
                    else if (sideToken == "right") faceIndex = 4;
                    else if (sideToken == "rightmost") faceIndex = 5;

                    if (faceIndex >= 0)
                    {
                        string modProp = tokens[startIndex + 1].ToLower();
                        if (modProp == "facade")
                        {
                            string facadeVal = tokens[startIndex + 2];
                            string[] facadeParts = facadeVal.Split(':');
                            diceSides[faceIndex].facadeID = facadeParts[0]; // Captures the short name cleanly ("Aid0")
                            if (facadeParts.Length > 1)
                            {
                                diceSides[faceIndex].facadeColor = string.Join(":", facadeParts.Skip(1));
                            }

                            i = startIndex + 2; // Jump loop tracker forward
                            continue;
                        }
                    }
                }
                // --- END FIX ---

                int endIndex = startIndex;
                while (endIndex < tokens.Count)
                {
                    string peek = tokens[endIndex].ToLower();
                    // Stop collecting if we hit another hero property/item boundary
                    if (HeroDomainRules.HeroPropertyKeys.Contains(peek)) break; // <-- Changed this line
                    endIndex++;
                }

                int count = endIndex - startIndex;
                if (count > 0)
                {
                    List<string> itemTokens = tokens.GetRange(startIndex, count);
                    string itemString = string.Join(".", itemTokens);
                    i = endIndex - 1; // Advance outer loop counter past the parsed item

                    if (itemString.Contains("("))
                    {
                        ItemData customItem = new ItemData();
                        customItem.Parse(StaticBranchTracing.StripOuterParens(itemString));
                        customPayloads.Add(new CustomPayload { Prefix = "i", Data = customItem });
                    }
                    else
                    {
                        items.Add(itemString); // Adds as a single "left2.k.possessed#facade.Yca21:0" item
                    }
                }
                continue;
            }
            */

            if (tokenLower == "i")
            {
                int startIndex = i + 1;
                if (startIndex >= tokens.Count) continue;

                string subToken = tokens[startIndex].ToLower();

                // 1. Find where this entire segment ends (at the next metadata key or end of string)
                int endIndex = startIndex;
                while (endIndex < tokens.Count && !HeroDomainRules.HeroPropertyKeys.Contains(tokens[endIndex].ToLower()))
                {
                    endIndex++;
                }

                int count = endIndex - startIndex;
                if (count > 0)
                {
                    List<string> subTokens = tokens.GetRange(startIndex, count);
                    i = endIndex - 1; // Advance the parser's main loop past this parsed segment

                    // 2. Route Traits / Curses (.i.t.traitName or .i.t.jinx.curseName)
                    if (subToken == "t")
                    {
                        string tPayload = string.Join(".", subTokens.Skip(1));
                        ProcessTraitPayload(tPayload); // Routed through unified processing
                    }
                    // 3. Route Abilities (.i.learn.abilityName)
                    else if (subToken == "learn")
                    {
                        baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(string.Join(".", subTokens.Skip(1)), '#'));
                    }
                    // 4. Route Dice Face Modifiers (Check indices using your helper class)
                    else if (DiceTargetHelper.GetIndicesForTarget(subToken).Count > 0)
                    {
                        List<int> targetFaces = DiceTargetHelper.GetIndicesForTarget(subToken);
                        string modPayload = string.Join(".", subTokens.Skip(1));
                        ApplyDiceModifiers(targetFaces, modPayload);
                    }
                    // 5. Default: Route Items
                    else
                    {
                        string itemString = string.Join(".", subTokens);
                        if (itemString.Contains("("))
                        {
                            ItemData customItem = new ItemData();
                            customItem.Parse(StaticBranchTracing.StripOuterParens(itemString));
                            customPayloads.Add(new CustomPayload { Prefix = "i", Data = customItem });
                        }
                        else
                        {
                            items.Add(itemString);
                        }
                    }
                }
                continue;
            }

        }
    }

    private void ApplyDiceModifiers(List<int> targetFaces, string modPayload)
    {
        string[] chunks = modPayload.Split('#');
        foreach (int faceIdx in targetFaces)
        {
            if (faceIdx < 0 || faceIdx >= diceSides.Length) continue;
            if (diceSides[faceIdx] == null) diceSides[faceIdx] = new DiceSideData();

            foreach (string chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;
                string[] parts = chunk.Split(new char[] { '.' }, 2);
                if (parts.Length < 2) continue;

                string type = parts[0].ToLower();
                string value = parts[1];

                if (type == "k")
                {
                    if (!diceSides[faceIdx].keywords.Contains(value))
                        diceSides[faceIdx].keywords.Add(value);
                }
                else if (type == "facade")
                {
                    string[] facadeParts = value.Split(':');
                    diceSides[faceIdx].facadeID = facadeParts[0];

                    if (facadeParts.Length > 1)
                    {
                        var colorParts = facadeParts.Skip(1)
                                                    .Select(p => string.IsNullOrWhiteSpace(p) ? "0" : p.Trim())
                                                    .ToList();

                        if (colorParts.Count == 0 || colorParts.All(p => p == "0"))
                        {
                            diceSides[faceIdx].facadeColor = null;
                        }
                        else
                        {
                            while (colorParts.Count < 3) colorParts.Add("0");
                            diceSides[faceIdx].facadeColor = $"{colorParts[0]}:{colorParts[1]}:{colorParts[2]}";
                        }
                    }
                    else
                    {
                        diceSides[faceIdx].facadeColor = null;
                    }
                }
            }
        }
    }

    // Separates the hero-specific properties from the shared metadata
    private bool TryProcessHeroSpecificMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        string nextVal = tokens[i + 1];

        switch (tokenLower)
        {
            case "replica": baseReplica = nextVal; break;
            case "col": colorClass = nextVal; break;
            case "tier": if (int.TryParse(nextVal, out int t)) tier = t; break;
            case "adj": if (int.TryParse(nextVal, out int a)) adj = a; break;
            case "speech": speech = nextVal; break;
            default: return false;
        }
        i++;
        return true;
    }

    private bool TryProcessCollections(List<string> tokens, ref int i, string tokenLower)
    {
        if (i + 1 >= tokens.Count) return false;
        if (tokenLower == "t") { traits.AddRange(StaticBranchTracing.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "gift") { blessings.AddRange(StaticBranchTracing.TopLevelSplit(tokens[++i], '#')); return true; }
        if (tokenLower == "abilitydata" || tokenLower == "triggerhpdata" || tokenLower == "onhitdata")
        {
            string payload = tokens[++i];
            if (payload.StartsWith("("))
            {
                AddCustomAbility(AbilityData.CreateAbility(payload));
            }
            else
            {
                baseAbilityData.AddRange(StaticBranchTracing.TopLevelSplit(payload, '#'));
            }
            return true;
        }
        return false;
    }

    public override string Export()
    {
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");
        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) && imageOverride != "None" && imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
        {
            string formattedReplica = FormatSpecialImageName(baseReplica);
            heroSb.Append($"replica.{FormatName(formattedReplica)}");
            if (!hasImageOverride) AppendColorModifier(heroSb);
        }
        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");

        bool skipColor = false;
        string activeVisual = hasImageOverride ? imageOverride : baseReplica;
        // Try parsing the current active visual into a HeroType
        if (!string.IsNullOrEmpty(activeVisual) && Enum.TryParse(activeVisual, true, out HeroType parsedHero))
        {
            // Lookup the inherent natural color for this hero sprite
            if (SDColors.HeroColorMap.TryGetValue(parsedHero, out HeroColorOption defaultColor))
            {
                // If the selected color matches the default, flag it to be omitted
                if (EntityUIHelpers.ReverseLookupColor(colorClass) == defaultColor)
                {
                    skipColor = true;
                }
            }
        }
        // Only append the color modifier if it differs from the inherent default
        if (!skipColor && !string.IsNullOrEmpty(colorClass))
        {
            heroSb.Append($".col.{colorClass}");
        }

        if (hp > 0) heroSb.Append($".hp.{hp}");
        if (tier > 0) heroSb.Append($".tier.{tier}");
        if (!string.IsNullOrEmpty(p)) heroSb.Append($".p.{p}");
        if (adj.HasValue) heroSb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) heroSb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) heroSb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) heroSb.Append($".draw.{draw}");

        AppendDiceSides(heroSb);
        if (!string.IsNullOrEmpty(speech)) heroSb.Append($".speech.{speech}");
        if (!string.IsNullOrEmpty(doc)) heroSb.Append($".doc.{doc}");

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers)) heroSb.Append(faceModifiers);

        if (hasImageOverride)
        {
            string formattedImg = FormatSpecialImageName(imageOverride);
            heroSb.Append($".img.{FormatName(formattedImg)}");
            AppendColorModifier(heroSb);
        }

        StringBuilder thoseSb = new StringBuilder();
        if (traits != null) foreach (var t in traits) if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");
        if (items != null) foreach (var i in items) if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");
        if (blessings != null) foreach (var bl in blessings) if (!string.IsNullOrEmpty(bl)) thoseSb.Append($".gift.{FormatName(bl)}");
        if (curses != null) foreach (var c in curses) if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");
        if (baseAbilityData != null) foreach (var ab in baseAbilityData) if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");

        if (customPayloads != null)
        {
            foreach (var payload in customPayloads)
            {
                string exported = payload.Export();
                if (!string.IsNullOrEmpty(exported)) thoseSb.Append($".{exported}");
            }
        }

        heroSb.Append(thoseSb.ToString());
        heroSb.Append(")");

        string baseHeroString = heroSb.ToString();

        // Append custom abilities on the outside of the base hero structure
        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            StringBuilder abilitiesSb = new StringBuilder();
            foreach (var cab in customAbilityData)
            {
                if (cab != null)
                {
                    if (cab is TriggerHPData)
                        abilitiesSb.Append($".triggerhpdata.({cab.Export()})");
                    else if (cab is OnHitData)
                        abilitiesSb.Append($".onhitdata.({cab.Export()})");
                    else if (cab is OrbData orb)
                        abilitiesSb.Append($".{orb.ExportAsTrait(useITPrefix: true)}");
                    else
                        abilitiesSb.Append($".abilitydata.({cab.Export()})");
                }
            }

            if (abilitiesSb.Length > 0)
            {
                return $"({baseHeroString}{abilitiesSb.ToString()})";
            }
        }

        return baseHeroString;
    }

    public override void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();
        if (customOrbs == null) customOrbs = new List<OrbData>(); // Safety check

        if (ability is SpellData spell) { if (!customSpells.Any(s => s.entityName == spell.entityName)) customSpells.Add(spell); }
        else if (ability is TacticData tactic) { if (!customTactics.Any(t => t.entityName == tactic.entityName)) customTactics.Add(tactic); }
        else if (ability is OrbData orb) { if (!customOrbs.Any(o => o.entityName == orb.entityName && o.hardcodedAbilityName == orb.hardcodedAbilityName)) customOrbs.Add(orb); }
        else base.AddCustomAbility(ability);
    }

    public void DebugContentsToConsoleCompact(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        string displayName = !string.IsNullOrEmpty(entityName) ? entityName : baseReplica;
        if (!string.IsNullOrEmpty(displayName)) sb.AppendLine($"{indent}Name: {displayName}");
        if (baseReplica != null && !string.IsNullOrEmpty(baseReplica.ToString())) sb.AppendLine($"{indent}Base Replica: {baseReplica}");
        if (!string.IsNullOrEmpty(colorClass)) sb.AppendLine($"{indent}Color Class: {colorClass}");
        if (tier != 0) sb.AppendLine($"{indent}Tier: {tier}");
        if (hp != 0) sb.AppendLine($"{indent}HP: {hp}");
        if (!string.IsNullOrEmpty(imageOverride))
        {
            string displayValue = imageOverride.Length > 32 ? "<base64 string img>" : imageOverride;
            sb.AppendLine($"{indent}Image Override: {displayValue}");
        }

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

        if (customAbilityData != null && customAbilityData.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Abilities ({customAbilityData.Count}):");
            foreach (var cab in customAbilityData)
            {
                string abilityType = cab is SpellData ? "Spell" :
                                     cab is TacticData ? "Tactic" :
                                     cab is OnHitData ? "OnHit" :
                                     cab is TriggerHPData ? "TriggerHP" : "Ability";

                sb.AppendLine($"{indent}  [✓ Unpacked {abilityType}: {cab.entityName ?? "Unnamed"}]");
                cab.DebugAbilityCompact();
            }
        }

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
        if (sb.Length > 0) UnityEngine.Debug.Log($"{indent}--- HERO DATA DEBUG (COMPACT) ---\n" + sb.ToString());
    }
}