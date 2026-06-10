using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public abstract class AbilityData : HeroData
{
    // Defines top-level metadata tags used within entities.
    // Differentiates actual parameters from raw dot-separated text strings.
    private static readonly HashSet<string> AbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "i", "t", "gift", "abilitydata", "n", "img", "hp", "col", "tier", "hsv", "hsl", "hue", "p", "b", "rect", "draw", "thue", "doc", "adj", "speech"
    };

    // Maps baseReplica natively to your old dummy base property
    public string baseDummyType
    {
        get => baseReplica;
        set => baseReplica = value;
    }

    // Map primary and secondary active visual effects to Left and Middle sides on our dummy hero
    public DiceSideData PrimaryEffect
    {
        get => diceSides[0];
        set => diceSides[0] = value;
    }

    public DiceSideData SecondaryEffect
    {
        get => diceSides[1];
        set => diceSides[1] = value;
    }

    public AbilityData() : base()
    {
        // Abilities share HeroData structures, but heroes natively default to 7 HP.
        // We override and force HP to 0 here on construction so our exported ability data string 
        // doesn't export with a raw `.hp.7` parameter unless it is actually a defined summon.
        hp = 0;
    }

    public abstract string ExportWrapped();

    protected string ExportInner()
    {
        StringBuilder sb = new StringBuilder();

        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                imageOverride != "None" &&
                                imageOverride != baseReplica;

        // 1. Base Replica (Abilities do not use the 'replica.' prefix at the start)
        sb.Append(FormatName(baseReplica));

        if (!hasImageOverride) AppendColorModifier(sb);

        // 2. Metadata (only outputting non-default parameters)
        if (!string.IsNullOrEmpty(colorClass) && colorClass != "y") sb.Append($".col.{colorClass}");
        if (hp > 0) sb.Append($".hp.{hp}");
        if (tier > 1) sb.Append($".tier.{tier}");

        AppendDiceSides(sb);

        // 3. Modifier Arrays
        foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".i.{itm}");
        foreach (var gft in blessings.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".gift.{gft}");
        foreach (var trt in traits.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".t.{trt}");

        if (baseAbilityData != null && baseAbilityData.Count > 0)
        {
            List<string> formattedAbilities = new List<string>();
            foreach (var ab in baseAbilityData)
            {
                if (string.IsNullOrEmpty(ab)) continue;
                formattedAbilities.Add(ab.StartsWith("(") && ab.EndsWith(")") ? ab : $"({ab})");
            }
            if (formattedAbilities.Count > 0)
            {
                sb.Append($".abilitydata.{string.Join("#", formattedAbilities)}");
            }
        }

        sb.Append(BuildFaceModifiers(allowFacade: true));

        // 4. Icon Override (img is compiled BEFORE name)
        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(sb);
        }

        // 5. Aesthetic Modifiers
        if (!string.IsNullOrEmpty(p)) sb.Append($".p.{p}");
        if (adj.HasValue) sb.Append($".adj.{adj.Value}");
        if (!string.IsNullOrEmpty(b)) sb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) sb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) sb.Append($".draw.{draw}");
        if (!string.IsNullOrEmpty(thue)) sb.Append($".thue.{thue}");
        if (!string.IsNullOrEmpty(speech)) sb.Append($".speech.{speech}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");

        // 6. Name is strictly appended at the absolute end
        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity")
        {
            sb.Append($".n.{FormatName(entityName)}");
        }

        return sb.ToString();
    }

    public static new AbilityData Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return new SpellData();

        while (ItemData.IsFullyWrapped(data)) data = data.Substring(1, data.Length - 2);

        List<string> chunks = ItemData.SafeSplit(data, '.');
        if (chunks.Count == 0) return new SpellData();

        // Peek ahead to detect spell vs tactic. Spells MUST have an cost registered in
        // their right slot (Index 4) to compile as spells in-game.
        bool isSpell = false;
        int sdIdx = chunks.FindIndex(x => x.ToLower() == "sd");
        if (sdIdx != -1 && sdIdx + 1 < chunks.Count)
        {
            string[] faces = chunks[sdIdx + 1].Split(':');
            if (faces.Length > 4 && faces[4] != "0" && faces[4] != "0-0")
            {
                isSpell = true;
            }
        }

        AbilityData ability = isSpell ? (AbilityData)new SpellData() : new TacticData();
        ability.baseReplica = chunks[0];

        int i = 1;
        while (i < chunks.Count)
        {
            string key = chunks[i].ToLower();
            if (AbilityKeys.Contains(key))
            {
                // List variables (i, t, gift, abilitydata) are highly unpredictable and can 
                // contain nested dots. We dynamically consume all chunks up until the next 
                // recognized metadata parameter to avoid breaking the parser.
                if ((key == "i" || key == "t" || key == "gift" || key == "abilitydata") && i + 1 < chunks.Count)
                {
                    i++;
                    List<string> subChunks = new List<string>();
                    // Grab everything up until the next structural keyword to preserve nested strings safely
                    while (i < chunks.Count && !AbilityKeys.Contains(chunks[i].ToLower()))
                    {
                        subChunks.Add(chunks[i]);
                        i++;
                    }
                    string joined = string.Join(".", subChunks);
                    if (key == "i") ability.items.Add(joined);
                    else if (key == "t") ability.traits.Add(joined);
                    else if (key == "gift") ability.blessings.Add(joined);
                    else if (key == "abilitydata") ability.baseAbilityData.Add(joined);
                }
                else if (i + 1 < chunks.Count)
                {
                    string val = chunks[i + 1];
                    switch (key)
                    {
                        case "n": ability.entityName = val; break;
                        case "img": ability.imageOverride = val; break;
                        case "doc": ability.doc = val; break;
                        case "col": ability.colorClass = val; break;
                        case "hp": if (int.TryParse(val, out int hVal)) ability.hp = hVal; break;
                        case "tier": if (int.TryParse(val, out int tVal)) ability.tier = tVal; break;
                        case "adj": if (int.TryParse(val, out int aVal)) ability.adj = aVal; break;
                        case "speech": ability.speech = val; break;
                        case "hsv":
                            string[] hsv = val.Split(':');
                            if (hsv.Length == 3) { int.TryParse(hsv[0], out ability.h); int.TryParse(hsv[1], out ability.s); int.TryParse(hsv[2], out ability.v); }
                            break;
                        case "hsl": ability.hsl = val; break;
                        case "hue": if (int.TryParse(val, out int hueVal)) ability.hue = hueVal; break;
                        case "p": ability.p = val; break;
                        case "b": ability.b = val; break;
                        case "rect": ability.rect = val; break;
                        case "draw": ability.draw = val; break;
                        case "thue": ability.thue = val; break;
                        case "sd":
                            string[] faces = val.Split(':');
                            for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                            {
                                if (faces[f] == "0" || faces[f] == "0-0") continue;
                                string[] faceParts = faces[f].Split('-');
                                int.TryParse(faceParts[0], out ability.diceSides[f].effectID);
                                if (faceParts.Length > 1) int.TryParse(faceParts[1], out ability.diceSides[f].pips);
                            }
                            break;
                    }
                    i += 2;
                }
                else i++;
            }
            else i++;
        }

        if (ability is SpellData spell)
        {
            // Sync the explicit manaCost property with the parsed right-side (Index 4) dice pips
            spell.manaCost = spell.diceSides[4].pips;
        }

        return ability;
    }
}