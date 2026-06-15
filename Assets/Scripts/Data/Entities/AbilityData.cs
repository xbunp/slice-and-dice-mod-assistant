using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public abstract class AbilityData : HeroData
{
    private static readonly HashSet<string> AbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "i", "t", "gift", "abilitydata", "n", "img", "hp", "col", "tier", "hsv", "hsl", "hue", "p", "b", "rect", "draw", "thue", "doc", "adj", "speech"
    };

    public string baseDummyType
    {
        get => baseReplica;
        set => baseReplica = value;
    }

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

    // ==========================================
    // CONSTRUCTORS 
    // ==========================================

    // The base constructor guarantees baseReplica and entityName are safely initialized to "Fey".
    public AbilityData() : base(Hero.Spell) { }

    // Memory is allocated, defaults ("Fey") are set, and the instance parses itself perfectly.
    public AbilityData(string data) : base(Hero.Spell)
    {
        ParseInstance(data);
    }

    // Factory creates the exact object needed and tells it to parse itself.
    public static new AbilityData Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return new SpellData();

        // 1. AST Peek to determine Spell vs Tactic instantiated type
        ASTNode root = new ASTParser(data).Parse();
        while (root is ScopeNode scope) root = scope.Content;

        List<string> chunks = new List<string>();
        if (root is ChainNode chain) chunks.AddRange(chain.Elements.Select(e => e.Export()));
        else if (root != null) chunks.Add(root.Export());

        bool isSpell = false;
        int sdIdx = chunks.FindIndex(x => x.Equals("sd", StringComparison.OrdinalIgnoreCase));
        if (sdIdx != -1 && sdIdx + 1 < chunks.Count)
        {
            string[] faces = chunks[sdIdx + 1].Split(':');
            if (faces.Length > 4 && faces[4] != "0" && faces[4] != "0-0")
            {
                isSpell = true;
            }
        }

        // 2. Allocate the exact subclass required and parse directly into it
        AbilityData ability = isSpell ? (AbilityData)new SpellData() : new TacticData();
        ability.ParseInstance(data);
        return ability;
    }

    // ==========================================
    // PARSING & EXPORTING
    // ==========================================

    public override void ParseInstance(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        ASTNode root = new ASTParser(data).Parse();

        while (root is ScopeNode scope)
        {
            root = scope.Content;
        }

        List<string> chunks = new List<string>();
        if (root is ChainNode chain)
        {
            chunks.AddRange(chain.Elements.Select(e => e.Export()));
        }
        else if (root != null)
        {
            chunks.Add(root.Export());
        }

        if (chunks.Count == 0) return;

        int i = 0;
        string firstKey = chunks[0].ToLower();

        // INTELLIGENT REPLICA DETECTION:
        // Ability strings can omit the base replica. If the first chunk is a known key (like "sd"),
        // leave the constructor's default "Fey" baseReplica alone and start parsing keys immediately.
        if (!AbilityKeys.Contains(firstKey))
        {
            this.baseReplica = chunks[0];
            i = 1;
        }

        while (i < chunks.Count)
        {
            string key = chunks[i].ToLower();
            if (AbilityKeys.Contains(key))
            {
                if ((key == "i" || key == "t" || key == "gift" || key == "abilitydata") && i + 1 < chunks.Count)
                {
                    i++;
                    List<string> subChunks = new List<string>();
                    while (i < chunks.Count && !AbilityKeys.Contains(chunks[i].ToLower()))
                    {
                        subChunks.Add(chunks[i]);
                        i++;
                    }
                    string joined = string.Join(".", subChunks);
                    if (key == "i") this.items.Add(joined);
                    else if (key == "t") this.traits.Add(joined);
                    else if (key == "gift") this.blessings.Add(joined);
                    else if (key == "abilitydata") this.baseAbilityData.Add(joined);
                }
                else if (i + 1 < chunks.Count)
                {
                    string val = chunks[i + 1];
                    switch (key)
                    {
                        case "n": this.entityName = val; break;
                        case "img": this.imageOverride = val; break;
                        case "doc": this.doc = val; break;
                        case "col": this.colorClass = val; break;
                        case "hp": if (int.TryParse(val, out int hVal)) this.hp = hVal; break;
                        case "tier": if (int.TryParse(val, out int tVal)) this.tier = tVal; break;
                        case "adj": if (int.TryParse(val, out int aVal)) this.adj = aVal; break;
                        case "speech": this.speech = val; break;
                        case "hsv":
                            string[] hsv = val.Split(':');
                            if (hsv.Length == 3) { int.TryParse(hsv[0], out this.h); int.TryParse(hsv[1], out this.s); int.TryParse(hsv[2], out this.v); }
                            break;
                        case "hsl": this.hsl = val; break;
                        case "hue": if (int.TryParse(val, out int hueVal)) this.hue = hueVal; break;
                        case "p": this.p = val; break;
                        case "b": this.b = val; break;
                        case "rect": this.rect = val; break;
                        case "draw": this.draw = val; break;
                        case "thue": this.thue = val; break;
                        case "sd":
                            string[] faces = val.Split(':');
                            for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                            {
                                if (faces[f] == "0" || faces[f] == "0-0") continue;
                                string[] faceParts = faces[f].Split('-');
                                int.TryParse(faceParts[0], out this.diceSides[f].effectID);
                                if (faceParts.Length > 1) int.TryParse(faceParts[1], out this.diceSides[f].pips);
                            }
                            break;
                    }
                    i += 2;
                }
                else i++;
            }
            else i++;
        }

        if (this is SpellData spell)
        {
            spell.manaCost = spell.diceSides[4].pips;
        }
    }

    public override string Export()
    {
        return ExportWrapped();
    }

    public abstract string ExportWrapped();

    protected string ExportInner()
    {
        StringBuilder sb = new StringBuilder();

        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                imageOverride != "None" &&
                                imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
            sb.Append(FormatName(baseReplica));

        if (!hasImageOverride) AppendColorModifier(sb);

        AppendDiceSides(sb);

        if (items != null)
            foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x))) sb.Append($".i.{itm}");

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

        if (hasImageOverride)
        {
            sb.Append($".img.{FormatName(imageOverride)}");
            AppendColorModifier(sb);
        }

        if (!string.IsNullOrEmpty(thue)) sb.Append($".thue.{thue}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");

        if (!string.IsNullOrEmpty(entityName) && entityName != "NewEntity" && entityName != "Fey")
        {
            sb.Append($".n.{FormatName(entityName)}");
        }

        return sb.ToString();
    }

    internal static ISyntaxPayload Create(string payloadString)
    {
        throw new NotImplementedException();
    }
}