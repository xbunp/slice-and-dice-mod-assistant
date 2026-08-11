using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class DiceSideData
{
    public enum DiceFaceType { Base, Sticker, Cast, Enchant, Egg }
    public enum PayloadTarget { None, Self, Ally, Enemy, AllAllies, AllEnemies, Everyone }

    public static string GetInherentDefaultTargetName(DiceSideData.DiceFaceType faceType)
    {
        switch (faceType)
        {
            case DiceSideData.DiceFaceType.Sticker: return "Ally";
            default: return "Inherent";
        }
    }

    public static bool IsTargetInherentDefault(DiceSideData.DiceFaceType faceType, DiceSideData.PayloadTarget target)
    {
        if (faceType == DiceSideData.DiceFaceType.Sticker && target == DiceSideData.PayloadTarget.Ally) return true;
        return false;
    }

    public int effectID = 0;
    public int pips = 0;

    [UnityEngine.SerializeField]
    private List<MechanicNode> _sideMechanics = new List<MechanicNode>();
    public List<MechanicNode> sideMechanics
    {
        get => _sideMechanics;
        set => _sideMechanics = value ?? new List<MechanicNode>();
    }
    public List<MechanicNode> sideItems => sideMechanics;

    // --- COPY-ON-WRITE: Flatten legacy payload for this face when edited in UI ---
    public void FlattenLegacyPayloadsForEdit()
    {
        var toRemove = sideMechanics.Where(m => m.LegacyItemPayload != null).ToList();
        if (toRemove.Count == 0) return;

        foreach (var m in toRemove)
        {
            foreach (var leg in m.LegacyItemPayload.Mechanics)
            {
                var flat = new MechanicNode
                {
                    Prefix = leg.Prefix,
                    RawPayloadString = leg.PayloadString,
                    ChainedKeywords = new List<string>(leg.ChainedKeywords),
                    Multiplier = leg.Multiplier,
                    MergedItem = leg.MergedItem,
                    SplicedItem = leg.SplicedItem,
                    PartIndex = leg.PartIndex,
                    RepeatTimes = leg.RepeatTimes,
                    PerTier = leg.PerTier,
                    Unpack = leg.Unpack
                };

                if (leg.PayloadData is SDData sd)
                {
                    if (leg.Prefix == "hat") flat.RawPayloadString = $"egg.{sd.Export()}";
                    else flat.RawPayloadString = sd.Export();
                }

                sideMechanics.Add(flat);
            }
            sideMechanics.Remove(m);
        }
    }

    // --- LEGACY BRIDGE HELPERS ---
    private string GetLegacyFacadeString()
    {
        foreach (var m in sideMechanics)
        {
            if (m.LegacyItemPayload != null)
            {
                foreach (var leg in m.LegacyItemPayload.Mechanics)
                {
                    if (leg.Prefix == "facade" && !string.IsNullOrEmpty(leg.PayloadString))
                        return leg.PayloadString;

                    foreach (var chain in leg.ChainedKeywords)
                    {
                        if (chain.StartsWith("facade.", StringComparison.OrdinalIgnoreCase))
                            return chain.Substring(7);
                        if (chain.StartsWith("facade:", StringComparison.OrdinalIgnoreCase))
                            return chain.Substring(7);
                    }
                }
            }
        }
        return null;
    }

    private ItemMechanic GetLegacyMechanic(Func<ItemMechanic, bool> predicate)
    {
        foreach (var m in sideMechanics)
        {
            if (m.LegacyItemPayload != null)
            {
                var found = m.LegacyItemPayload.Mechanics.FirstOrDefault(predicate);
                if (found != null) return found;
            }
        }
        return null;
    }

    private MechanicNode PrimaryPayloadMechanic => sideMechanics.FirstOrDefault(m =>
        m.Prefix == "sticker" || m.Prefix == "cast" || m.Prefix == "enchant" || m.Prefix == "hat");

    private ItemMechanic LegacyPrimaryPayloadMechanic => GetLegacyMechanic(m =>
        m.Prefix == "sticker" || m.Prefix == "cast" || m.Prefix == "enchant" || m.Prefix == "hat");

    // --- AST POINTER PROPERTIES ---

    public string facadeID
    {
        get
        {
            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "facade");
            if (m != null && !string.IsNullOrEmpty(m.RawPayloadString))
                return m.RawPayloadString.Split(':')[0];

            string legFac = GetLegacyFacadeString();
            if (!string.IsNullOrEmpty(legFac))
                return legFac.Split(':')[0];

            return "";
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "facade");
            if (m != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    string color = facadeColor;
                    if (string.IsNullOrEmpty(color) || color == "0:0:0" || color == "0")
                    {
                        sideMechanics.Remove(m);
                        return;
                    }
                    m.RawPayloadString = $":{color}";
                    return;
                }
                string c = facadeColor;
                m.RawPayloadString = (string.IsNullOrEmpty(c) || c == "0:0:0" || c == "0") ? value : $"{value}:{c}";
                return;
            }

            var leg = GetLegacyMechanic(x => x.Prefix == "facade");
            if (leg != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    string color = facadeColor;
                    if (string.IsNullOrEmpty(color) || color == "0:0:0" || color == "0")
                    {
                        RemoveLegacyMechanic(x => x.Prefix == "facade");
                        return;
                    }
                    leg.PayloadString = $":{color}";
                    return;
                }
                string c = facadeColor;
                leg.PayloadString = (string.IsNullOrEmpty(c) || c == "0:0:0" || c == "0") ? value : $"{value}:{c}";
                return;
            }

            // DO NOT create a node if setting an empty ID and there is no color
            if (string.IsNullOrEmpty(value)) return;

            string curColor = facadeColor;
            sideMechanics.Add(new MechanicNode { Prefix = "facade", RawPayloadString = string.IsNullOrEmpty(curColor) ? value : $"{value}:{curColor}" });
        }
    }

    public string facadeColor
    {
        get
        {
            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "facade");
            if (m != null && !string.IsNullOrEmpty(m.RawPayloadString))
            {
                var parts = m.RawPayloadString.Split(':');
                return parts.Length > 1 ? string.Join(":", parts.Skip(1)) : "";
            }
            string legFac = GetLegacyFacadeString();
            if (!string.IsNullOrEmpty(legFac))
            {
                var parts = legFac.Split(':');
                return parts.Length > 1 ? string.Join(":", parts.Skip(1)) : "";
            }
            return "";
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "facade");
            if (m != null)
            {
                string id = m.RawPayloadString.Split(':')[0];
                if (string.IsNullOrEmpty(id) && (string.IsNullOrEmpty(value) || value == "0:0:0" || value == "0"))
                {
                    sideMechanics.Remove(m);
                    return;
                }
                m.RawPayloadString = (string.IsNullOrEmpty(value) || value == "0:0:0" || value == "0") ? id : $"{id}:{value}";
                return;
            }

            var leg = GetLegacyMechanic(x => x.Prefix == "facade");
            if (leg != null)
            {
                string id = leg.PayloadString.Split(':')[0];
                if (string.IsNullOrEmpty(id) && (string.IsNullOrEmpty(value) || value == "0:0:0" || value == "0"))
                {
                    RemoveLegacyMechanic(x => x.Prefix == "facade");
                    return;
                }
                leg.PayloadString = (string.IsNullOrEmpty(value) || value == "0:0:0" || value == "0") ? id : $"{id}:{value}";
                return;
            }

            // DO NOT create a node if setting an empty/zero color when no facade ID exists
            if (string.IsNullOrEmpty(value) || value == "0:0:0" || value == "0")
                return;

            sideMechanics.Add(new MechanicNode { Prefix = "facade", RawPayloadString = $":{value}" });
        }
    }

    public string sidesc
    {
        get
        {
            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "sidesc");
            if (m != null) return m.RawPayloadString;
            return GetLegacyMechanic(x => x.Prefix == "sidesc")?.PayloadString ?? "";
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            var m = sideMechanics.FirstOrDefault(x => x.Prefix == "sidesc");
            if (m != null)
            {
                if (string.IsNullOrEmpty(value)) sideMechanics.Remove(m);
                else m.RawPayloadString = value;
                return;
            }
            if (!string.IsNullOrEmpty(value)) sideMechanics.Add(new MechanicNode { Prefix = "sidesc", RawPayloadString = value });
        }
    }

    public bool togtime
    {
        get => sideMechanics.Any(m => string.IsNullOrEmpty(m.Prefix) && string.Equals(m.RawPayloadString, "togtime", StringComparison.OrdinalIgnoreCase)) ||
               sideMechanics.Any(m => m.LegacyItemPayload != null && m.LegacyItemPayload.Mechanics.Any(leg => string.Equals(leg.PayloadString, "togtime", StringComparison.OrdinalIgnoreCase) || leg.ChainedKeywords.Contains("togtime", StringComparer.OrdinalIgnoreCase)));
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            if (value)
            {
                if (!togtime) sideMechanics.Add(new MechanicNode { Prefix = "", RawPayloadString = "togtime" });
            }
            else
            {
                sideMechanics.RemoveAll(m => string.IsNullOrEmpty(m.Prefix) && string.Equals(m.RawPayloadString, "togtime", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public List<string> keywords
    {
        get
        {
            var kws = new List<string>();
            foreach (var m in sideMechanics)
            {
                if (m.Prefix == "k" || (string.IsNullOrEmpty(m.Prefix) && ExternalGameRegistry.IsValidKeyword(m.RawPayloadString)))
                {
                    if (!string.IsNullOrEmpty(m.RawPayloadString)) kws.Add(m.RawPayloadString);
                }
                kws.AddRange(m.ChainedKeywords.Where(k => ExternalGameRegistry.IsValidKeyword(k) || k.StartsWith("k.", StringComparison.OrdinalIgnoreCase)));

                if (m.LegacyItemPayload != null)
                {
                    foreach (var legMech in m.LegacyItemPayload.Mechanics)
                    {
                        if (legMech.Prefix == "k" || (string.IsNullOrEmpty(legMech.Prefix) && ExternalGameRegistry.IsValidKeyword(legMech.PayloadString)))
                        {
                            if (!string.IsNullOrEmpty(legMech.PayloadString)) kws.Add(legMech.PayloadString);
                        }
                        kws.AddRange(legMech.ChainedKeywords.Where(k => ExternalGameRegistry.IsValidKeyword(k) || k.StartsWith("k.", StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            return kws.Distinct().ToList();
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            sideMechanics.RemoveAll(m => m.Prefix == "k" || (string.IsNullOrEmpty(m.Prefix) && ExternalGameRegistry.IsValidKeyword(m.RawPayloadString)));
            foreach (var m in sideMechanics) m.ChainedKeywords.RemoveAll(k => ExternalGameRegistry.IsValidKeyword(k) || k.StartsWith("k.", StringComparison.OrdinalIgnoreCase));

            if (value != null)
            {
                foreach (var kw in value)
                {
                    if (string.IsNullOrWhiteSpace(kw)) continue;
                    sideMechanics.Add(new MechanicNode { Prefix = "k", RawPayloadString = kw.Trim() });
                }
            }
        }
    }

    public DiceFaceType faceType
    {
        get
        {
            var m = PrimaryPayloadMechanic;
            if (m != null)
            {
                if (m.Prefix == "hat") return DiceFaceType.Egg;
                if (m.Prefix == "cast") return DiceFaceType.Cast;
                if (m.Prefix == "enchant") return DiceFaceType.Enchant;
                return DiceFaceType.Sticker;
            }
            var leg = LegacyPrimaryPayloadMechanic;
            if (leg != null)
            {
                if (leg.Prefix == "hat") return DiceFaceType.Egg;
                if (leg.Prefix == "cast") return DiceFaceType.Cast;
                if (leg.Prefix == "enchant") return DiceFaceType.Enchant;
                return DiceFaceType.Sticker;
            }
            return DiceFaceType.Base;
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            // Base explicitly clears all payload mechanics
            if (value == DiceFaceType.Base)
            {
                var m = PrimaryPayloadMechanic;
                if (m != null) sideMechanics.Remove(m);
                RemoveLegacyMechanic(x => x.Prefix == "sticker" || x.Prefix == "cast" || x.Prefix == "enchant" || x.Prefix == "hat");
                return;
            }

            string targetPrefix = value switch
            {
                DiceFaceType.Egg => "hat",
                DiceFaceType.Cast => "cast",
                DiceFaceType.Enchant => "enchant",
                _ => "sticker"
            };

            var existing = PrimaryPayloadMechanic;
            if (existing != null)
            {
                existing.Prefix = targetPrefix;

                // Format or strip egg prefixes when transitioning face types
                if (targetPrefix == "hat" && !string.IsNullOrEmpty(existing.RawPayloadString) && !existing.RawPayloadString.StartsWith("egg.", StringComparison.OrdinalIgnoreCase))
                {
                    existing.RawPayloadString = $"egg.{existing.RawPayloadString}";
                }
                else if (targetPrefix != "hat" && !string.IsNullOrEmpty(existing.RawPayloadString))
                {
                    if (existing.RawPayloadString.StartsWith("egg.", StringComparison.OrdinalIgnoreCase))
                        existing.RawPayloadString = existing.RawPayloadString.Substring(4);
                    existing.RawPayloadString = existing.RawPayloadString.Replace("#blindfold", "");
                }
            }
            else
            {
                // Create the active payload node (even with an empty payload string!)
                sideMechanics.Add(new MechanicNode { Prefix = targetPrefix, RawPayloadString = "" });
            }
        }
    }

    public string payload
    {
        get
        {
            var m = PrimaryPayloadMechanic;
            if (m != null)
            {
                if (m.Prefix == "hat" && m.RawPayloadString.StartsWith("egg.", StringComparison.OrdinalIgnoreCase)) return m.RawPayloadString.Substring(4);
                return m.RawPayloadString;
            }
            var leg = LegacyPrimaryPayloadMechanic;
            if (leg != null)
            {
                if (leg.Prefix == "hat" && leg.PayloadString.StartsWith("egg.", StringComparison.OrdinalIgnoreCase)) return leg.PayloadString.Substring(4);
                return leg.PayloadString;
            }
            return "";
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            var m = PrimaryPayloadMechanic;
            if (m == null)
            {
                // If set on Base, default to Sticker
                if (string.IsNullOrEmpty(value)) return;
                m = new MechanicNode { Prefix = "sticker" };
                sideMechanics.Add(m);
            }

            // DO NOT delete m if value is empty! Keep the node alive for the faceType.
            if (m.Prefix == "hat")
            {
                if (!string.IsNullOrEmpty(value) && !value.StartsWith("egg.", StringComparison.OrdinalIgnoreCase))
                    m.RawPayloadString = $"egg.{value}";
                else
                    m.RawPayloadString = value ?? "";
            }
            else
            {
                m.RawPayloadString = value ?? "";
            }
        }
    }

    public PayloadTarget? payloadTarget
    {
        get
        {
            var m = PrimaryPayloadMechanic;
            if (m != null) return m.PayloadTargetOverride;
            return null;
        }
        set
        {
            FlattenLegacyPayloadsForEdit(); // Isolate this face before modifying!

            var m = PrimaryPayloadMechanic;
            if (m != null) m.PayloadTargetOverride = value;
        }
    }

    public void AddKeyword(string kw)
    {
        if (string.IsNullOrWhiteSpace(kw)) return;
        string clean = kw.Trim();
        var kws = keywords;
        if (!kws.Contains(clean, StringComparer.OrdinalIgnoreCase))
        {
            kws.Add(clean);
            keywords = kws;
        }
    }

    public bool RemoveKeyword(string kw)
    {
        if (string.IsNullOrWhiteSpace(kw)) return false;
        string clean = kw.Trim();
        var kws = keywords;
        int countBefore = kws.Count;
        kws.RemoveAll(k => string.Equals(k, clean, StringComparison.OrdinalIgnoreCase));
        if (kws.Count != countBefore)
        {
            keywords = kws;
            return true;
        }
        return false;
    }

    public DiceSideData Clone()
    {
        return new DiceSideData
        {
            effectID = this.effectID,
            pips = this.pips,
            _sideMechanics = this._sideMechanics.Select(m => m.Clone()).ToList()
        };
    }

    private void RemoveLegacyMechanic(Func<ItemMechanic, bool> predicate)
    {
        foreach (var m in sideMechanics.ToList())
        {
            if (m.LegacyItemPayload != null)
            {
                m.LegacyItemPayload.Mechanics.RemoveAll(new Predicate<ItemMechanic>(predicate));
                if (m.LegacyItemPayload.Mechanics.Count == 0)
                    sideMechanics.Remove(m);
            }
        }
    }
}