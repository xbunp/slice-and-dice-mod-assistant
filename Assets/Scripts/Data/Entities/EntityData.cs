using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class EntityDomainRules
{
    // The keys shared by almost ALL entities
    public static readonly HashSet<string> CommonMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "n", "img", "doc", "hp", "hsv", "hsl", "hue",
        "p", "b", "rect", "draw", "thue", "sd"
    };

    // Shared collection routing keys
    public static readonly HashSet<string> CommonCollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "learn", "abilitydata", "triggerhpdata", "onhitdata", "orb" // Added "orb"
    };
}

[System.Serializable]
public abstract class EntityData : SDData, IPayloadContainer
{
    [Header("Core Shared Info")]
    public int hp = 0;

    [Header("Shared Extended Modifiers")]
    public List<string> items = new List<string>();
    public List<string> traits = new List<string>();
    public List<string> blessings = new List<string>();
    public List<string> curses = new List<string>();
    public List<string> baseAbilityData = new List<string>();
    public DiceSideData[] diceSides = new DiceSideData[6];

    // Change from [SerializeField] public List<SpellData> customSpells;
    [System.NonSerialized] // Tells Unity's serializer to ignore this field
    [JsonProperty]         // Tells Newtonsoft to keep serializing this field
    [SerializeField] public List<OnHitData> customOnHits;

    [System.NonSerialized]
    [JsonProperty]
    [SerializeField] public List<TriggerHPData> customTriggerHPs;

    [System.NonSerialized]
    [JsonProperty]
    [SerializeField] public List<OrbData> customOrbs = new List<OrbData>();

    //ADD ORB SUPPORT.

    // Interface mappings
    public List<string> BaseItems => items;
    public List<string> Traits => traits;
    public List<string> Curses => curses;
    public List<string> Blessings => blessings;
    public List<string> BaseAbilities => baseAbilityData;
    public List<CustomPayload> CustomPayloads => customPayloads;

    public virtual IReadOnlyList<AbilityData> customAbilityData
    {
        get
        {
            var combined = new List<AbilityData>();
            if (customOnHits != null) combined.AddRange(customOnHits);
            if (customTriggerHPs != null) combined.AddRange(customTriggerHPs);
            if (customOrbs != null) combined.AddRange(customOrbs); // Added
            return combined;
        }
    }

    public virtual void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customOnHits == null) customOnHits = new List<OnHitData>();
        if (customTriggerHPs == null) customTriggerHPs = new List<TriggerHPData>();
        if (customOrbs == null) customOrbs = new List<OrbData>();

        if (ability is OrbData orb)
        {
            if (!customOrbs.Any(o => o.entityName == orb.entityName && o.hardcodedAbilityName == orb.hardcodedAbilityName))
                customOrbs.Add(orb);
        }
        else if (ability is OnHitData onHit)
        {
            if (!customOnHits.Any(o => o.entityName == onHit.entityName)) customOnHits.Add(onHit);
        }
        else if (ability is TriggerHPData trig)
        {
            if (!customTriggerHPs.Any(t => t.entityName == trig.entityName)) customTriggerHPs.Add(trig);
        }
    }

    protected void ProcessTraitPayload(string tPayload)
    {
        if (string.IsNullOrWhiteSpace(tPayload)) return;

        List<string> chains = StaticBranchTracing.TopLevelSplit(tPayload, '#');
        foreach (string chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            string trimmed = chain.Trim();

            if (trimmed.StartsWith("orb.", StringComparison.OrdinalIgnoreCase))
            {
                OrbData orb = new OrbData();
                orb.Parse(trimmed);
                AddCustomAbility(orb);
            }
            else if (trimmed.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
            {
                curses.Add(trimmed.Substring(5));
            }
            else if (trimmed.Contains("("))
            {
                ModifierData nestedMod = new ModifierData();
                nestedMod.Parse(trimmed);
                customPayloads.Add(new CustomPayload { Prefix = "t", Data = nestedMod });
            }
            else
            {
                traits.Add(trimmed);
            }
        }
    }

    protected bool TryProcessEntityMetadata(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "hp")
        {
            if (i + 1 < tokens.Count && int.TryParse(tokens[i + 1], out int hpVal))
            {
                hp = hpVal;
                i++; // Consume the value token
            }
            return true;
        }
        return false;
    }

    public string BuildFaceModifiers(bool allowFacade)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            List<string> chunks = new List<string>();

            foreach (var kw in face.keywords)
                if (!string.IsNullOrWhiteSpace(kw)) chunks.Add($"k.{kw.Trim().ToLower()}");

            if (allowFacade && !string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";

                if (!string.IsNullOrWhiteSpace(face.facadeColor))
                {
                    string[] hsv = face.facadeColor.Split(':');
                    List<string> parts = new List<string>();

                    for (int pIdx = 0; pIdx < hsv.Length; pIdx++)
                    {
                        parts.Add(string.IsNullOrWhiteSpace(hsv[pIdx]) ? "0" : hsv[pIdx].Trim());
                    }
                    while (parts.Count < 3) parts.Add("0");

                    // If all elements are zero, fallback to the mandatory ":0"
                    if (parts[0] == "0" && parts[1] == "0" && parts[2] == "0")
                    {
                        facStr += ":0";
                    }
                    else
                    {
                        facStr += $":{parts[0]}:{parts[1]}:{parts[2]}";
                    }
                }
                else
                {
                    facStr += ":0";
                }

                chunks.Add(facStr);
            }

            if (chunks.Count > 0)
            {
                string modString = string.Join("#", chunks);
                int faceMask = 1 << i;
                if (groupedModifiers.ContainsKey(modString)) groupedModifiers[modString] |= faceMask;
                else groupedModifiers[modString] = faceMask;
            }
        }

        foreach (var kvp in groupedModifiers)
        {
            string modString = kvp.Key;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(kvp.Value);
            foreach (string alias in optimalAliases) modSb.Append($".i.{alias}.{modString}");
        }

        return modSb.ToString();
    }

    protected bool TryProcessDiceSides(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower != "sd" || i + 1 >= tokens.Count) return false;

        // Split faces by colon (e.g., "187:76-0")
        string[] faces = tokens[++i].Split(':');

        for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
        {
            if (faces[f] == "0" || faces[f] == "0-0") continue;

            // Ensure the DiceSideData instance exists to prevent NullReferenceException
            if (diceSides[f] == null)
            {
                diceSides[f] = new DiceSideData
                {
                    effectID = 0,
                    pips = 0,
                    facadeID = null,
                    keywords = new List<string>()
                };
            }

            string[] faceParts = faces[f].Split('-');
            if (faceParts.Length > 0)
            {
                // Parse Effect ID (always present if we reached here)
                int.TryParse(faceParts[0], out diceSides[f].effectID);

                // Parse Pips if specified (e.g., "76-2"), otherwise default to 0 (e.g., "187")
                if (faceParts.Length > 1)
                {
                    int.TryParse(faceParts[1], out diceSides[f].pips);
                }
                else
                {
                    diceSides[f].pips = 0;
                }
            }
        }
        return true;
    }

    // Unifies lookahead trait parsing (supports both strings and nested custom modifiers)
    protected void ProcessTraitToken(List<string> tokens, ref int i, HashSet<string> domainPropertyKeys)
    {
        int startIndex = i + 1;
        if (startIndex >= tokens.Count) return;

        int endIndex = startIndex;
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            if (domainPropertyKeys.Contains(peek)) break;
            endIndex++;
        }

        int count = endIndex - startIndex;
        if (count > 0)
        {
            string tPayload = string.Join(".", tokens.GetRange(startIndex, count));
            i = endIndex - 1;
            ProcessTraitPayload(tPayload); // Routed
        }
    }

    protected bool TryProcessOrbData(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "orb")
        {
            if (i + 1 >= tokens.Count) return true;

            int endIndex = i + 1;
            if (OrbData.ValidBaseOrbs.Contains(tokens[endIndex]))
            {
                OrbData orb = new OrbData();
                orb.Parse($"orb.{tokens[endIndex]}");
                AddCustomAbility(orb);
                i = endIndex;
                return true;
            }

            int j = i + 1;
            while (j < tokens.Count)
            {
                if (tokens[j].StartsWith("("))
                {
                    endIndex = j;
                    break;
                }
                if (EntityDomainRules.CommonMetadataKeys.Contains(tokens[j]) || EntityDomainRules.CommonCollectionKeys.Contains(tokens[j]))
                {
                    break;
                }
                j++;
            }

            string payload = string.Join(".", tokens.GetRange(i, endIndex - i + 1));
            OrbData customOrb = new OrbData();
            customOrb.Parse(payload);
            AddCustomAbility(customOrb);
            i = endIndex;
            return true;
        }
        return false;
    }

    protected bool TryProcessTriggerData(List<string> tokens, ref int i, string tokenLower)
    {
        if (tokenLower == "triggerhpdata")
        {
            if (i + 1 < tokens.Count)
            {
                string payload = tokens[++i];
                TriggerHPData thp = new TriggerHPData();
                thp.Parse(StaticBranchTracing.StripOuterParens(payload));
                customPayloads.Add(new CustomPayload { Prefix = "triggerhpdata", Data = thp });
            }
            return true;
        }
        if (tokenLower == "onhitdata")
        {
            if (i + 1 < tokens.Count)
            {
                string payload = tokens[++i];
                OnHitData ohd = new OnHitData();
                ohd.Parse(StaticBranchTracing.StripOuterParens(payload));
                customPayloads.Add(new CustomPayload { Prefix = "onhitdata", Data = ohd });
            }
            return true;
        }
        return false;
    }

    protected void AppendColorModifier(StringBuilder sb)
    {
        if (phue != null && phue.colorRange != 0) // Prevents adding empty payloads if unassigned
        {
            sb.Append($".{PackPHue(phue)}");
        }
        if (thue != null && (thue.colorRange != 0 || thue.colorOffset != 0))
        {
            sb.Append($".{PackTHue(thue)}");
        }

        if (h != 0 || s != 0 || v != 0)
        {
            sb.Append($".hsv.{h}:{s}:{v}");
        }
        else if (hue != 0)
        {
            sb.Append($".hue.{hue}");
        }
    }

    public void InitializeDiceFaces()
    {
        // Ensure the array itself exists
        if (diceSides == null || diceSides.Length != 6)
        {
            diceSides = new DiceSideData[6];
        }

        // ONLY instantiate slots that are completely null, preserving existing data
        for (int i = 0; i < diceSides.Length; i++)
        {
            if (diceSides[i] == null)
            {
                diceSides[i] = new DiceSideData();
                // Safety net: ensure keywords list is never null
                if (diceSides[i].keywords == null) diceSides[i].keywords = new List<string>();
            }
        }
    }

    protected void AppendDiceSides(StringBuilder sb)
    {
        // Find the last modified side so we can truncate trailing zeroes
        int lastActiveIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            // CHANGED: Also check if pips != 0 so we don't drop pip-only modifications
            if (diceSides[i] != null && (diceSides[i].effectID != 0 || diceSides[i].pips != 0))
            {
                lastActiveIndex = i;
            }
        }

        // If no custom sides are defined, omit the .sd block entirely
        if (lastActiveIndex == -1) return;

        sb.Append(".sd.");
        for (int i = 0; i <= lastActiveIndex; i++)
        {
            var side = diceSides[i];
            if (side == null || (side.effectID == 0 && side.pips == 0))
            {
                sb.Append("0");
            }
            else
            {
                // Simplify: if pips are 0, omit the "-0" suffix
                if (side.pips == 0)
                {
                    sb.Append(side.effectID);
                }
                else
                {
                    sb.Append($"{side.effectID}-{side.pips}");
                }
            }

            // Only append separator if there are more customized sides remaining
            if (i < lastActiveIndex) sb.Append(":");
        }
    }
}
