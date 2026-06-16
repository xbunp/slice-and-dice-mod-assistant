using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// ==========================================
// ABILITY DOMAIN DICTIONARY (Factual Rules)
// ==========================================
public static class AbilityDomainRules
{
    // Explicit keys recognized strictly by the Ability parser.
    // Anything preceding these in a chain is assumed to be an implicit BaseReplica.
    public static readonly HashSet<string> AbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sd", "i", "t", "gift", "abilitydata", "n", "img", "hp", "col", "tier",
        "hsv", "hsl", "hue", "p", "b", "rect", "draw", "thue", "doc", "adj", "speech"
    };

    // Keys that signal a collection of modifiers that greedy-consume subsequent properties
    public static readonly HashSet<string> CollectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "abilitydata"
    };
}

[System.Serializable]
public abstract class AbilityData : HeroData
{
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

    // Factory creates the exact object needed and tells it to parse itself.
    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        // 1. AST Peek to determine Spell vs Tactic instantiated type
        ASTNode root = new ASTParser(data.Trim()).Parse();

        // Strip outermost structural scopes to get to the core definition
        while (root is ScopeNode scope) root = scope.Content;

        bool isSpell = false;

        // Traverse the top-level chain to inspect the 'sd' token for mana (Face 5)
        if (root is ChainNode chain)
        {
            for (int i = 0; i < chain.Elements.Count; i++)
            {
                if (chain.Elements[i].Export().Equals("sd", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < chain.Elements.Count)
                    {
                        string[] faces = chain.Elements[i + 1].Export().Split(':');
                        // Spell check: Face index 4 (5th face) contains non-zero pips
                        if (faces.Length > 4 && faces[4] != "0" && faces[4] != "0-0")
                        {
                            isSpell = true;
                        }
                    }
                    break;
                }
            }
        }

        // 2. Allocate the exact subclass required and parse directly into it
        AbilityData ability = isSpell ? (AbilityData)new SpellData() : new TacticData();
        ability.ParseInstance(data);
    }

    // ==========================================
    // AST KNOWLEDGE EXTRACTION (Parse Core)
    // ==========================================

    public override void ParseInstance(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        UnityEngine.Debug.Log($"[AbilityData] Starting AST Parse on data length: {data.Length}");

        ASTNode root = new ASTParser(data.Trim()).Parse();

        // Strip structural wrapper scopes immediately since abilities often wrap `(Burst.sd...)`
        while (root is ScopeNode scope)
        {
            root = scope.Content;
        }

        bool isFirstToken = true;
        ExtractKnowledge(ref root, this, ref isFirstToken);

        // Spell specific resolution
        if (this is SpellData spell)
        {
            spell.manaCost = spell.diceSides[4].pips;
        }

        UnityEngine.Debug.Log($"[AbilityData] AST Parse Complete! Name: '{entityName}', Replica: '{baseReplica}'");
    }

    private void ExtractKnowledge(ref ASTNode node, AbilityData ability, ref bool isFirstToken)
    {
        if (node is ChainNode chain)
        {
            for (int i = 0; i < chain.Elements.Count; i++)
            {
                string token = chain.Elements[i].Export().ToLower();

                // INTELLIGENT REPLICA DETECTION
                // If the very first parsed token of the root is not a known property key, it acts as the implied replica.
                if (isFirstToken)
                {
                    isFirstToken = false;
                    if (!AbilityDomainRules.AbilityKeys.Contains(token))
                    {
                        ability.baseReplica = chain.Elements[i].Export();
                        continue; // Consumed, move to next token in chain
                    }
                }

                switch (token)
                {
                    case "n":
                        if (i + 1 < chain.Elements.Count) ability.entityName = chain.Elements[++i].Export();
                        break;
                    case "img":
                        if (i + 1 < chain.Elements.Count) ability.imageOverride = chain.Elements[++i].Export();
                        break;
                    case "doc":
                        if (i + 1 < chain.Elements.Count) ability.doc = chain.Elements[++i].Export();
                        break;
                    case "col":
                        if (i + 1 < chain.Elements.Count) ability.colorClass = chain.Elements[++i].Export();
                        break;
                    case "hp":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int hVal)) ability.hp = hVal;
                        break;
                    case "tier":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int tVal)) ability.tier = tVal;
                        break;
                    case "adj":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int aVal)) ability.adj = aVal;
                        break;
                    case "speech":
                        if (i + 1 < chain.Elements.Count) ability.speech = chain.Elements[++i].Export();
                        break;
                    case "hsv":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string[] hsv = chain.Elements[++i].Export().Split(':');
                            if (hsv.Length == 3)
                            {
                                int.TryParse(hsv[0], out ability.h);
                                int.TryParse(hsv[1], out ability.s);
                                int.TryParse(hsv[2], out ability.v);
                            }
                        }
                        break;
                    case "hsl":
                        if (i + 1 < chain.Elements.Count) ability.hsl = chain.Elements[++i].Export();
                        break;
                    case "hue":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int hueVal)) ability.hue = hueVal;
                        break;
                    case "p":
                        if (i + 1 < chain.Elements.Count) ability.p = chain.Elements[++i].Export();
                        break;
                    case "b":
                        if (i + 1 < chain.Elements.Count) ability.b = chain.Elements[++i].Export();
                        break;
                    case "rect":
                        if (i + 1 < chain.Elements.Count) ability.rect = chain.Elements[++i].Export();
                        break;
                    case "draw":
                        if (i + 1 < chain.Elements.Count) ability.draw = chain.Elements[++i].Export();
                        break;
                    case "thue":
                        if (i + 1 < chain.Elements.Count) ability.thue = chain.Elements[++i].Export();
                        break;
                    case "sd":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string[] faces = chain.Elements[++i].Export().Split(':');
                            for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                            {
                                if (faces[f] == "0" || faces[f] == "0-0") continue;
                                string[] faceParts = faces[f].Split('-');
                                int.TryParse(faceParts[0], out ability.diceSides[f].effectID);
                                if (faceParts.Length > 1) int.TryParse(faceParts[1], out ability.diceSides[f].pips);
                            }
                        }
                        break;

                    // GREEDY COLLECTION MODIFIERS
                    case "i":
                    case "t":
                    case "gift":
                    case "abilitydata":
                        if (i + 1 < chain.Elements.Count)
                        {
                            i++; // Step onto the first element of the collection payload
                            List<string> subChain = new List<string>();

                            // Greedily consume everything until the next explicit AbilityKey is encountered
                            while (i < chain.Elements.Count && !AbilityDomainRules.AbilityKeys.Contains(chain.Elements[i].Export().ToLower()))
                            {
                                subChain.Add(chain.Elements[i].Export());
                                i++;
                            }

                            string joinedPayload = string.Join(".", subChain);

                            if (token == "i") ability.items.Add(joinedPayload);
                            else if (token == "t") ability.traits.Add(joinedPayload);
                            else if (token == "gift") ability.blessings.Add(joinedPayload);
                            else if (token == "abilitydata") ability.baseAbilityData.Add(joinedPayload);

                            // Backstep by 1 so the outer for-loop increments exactly onto the next valid key
                            i--;
                        }
                        break;

                    default:
                        // Recursively unpack structural scopes if they exist
                        if (chain.Elements[i] is ScopeNode isolatedScope)
                        {
                            ASTNode inner = isolatedScope.Content;
                            ExtractKnowledge(ref inner, ability, ref isFirstToken);
                        }
                        else if (chain.Elements[i] is CompositeNode compNode && compNode.Left is ScopeNode compScope)
                        {
                            ASTNode inner = compScope.Content;
                            ExtractKnowledge(ref inner, ability, ref isFirstToken);
                        }
                        break;
                }
            }
        }
        else if (node is ScopeNode scope)
        {
            ASTNode inner = scope.Content;
            ExtractKnowledge(ref inner, ability, ref isFirstToken);
        }
        else if (node is SequenceNode seq)
        {
            foreach (var itemNode in seq.Items)
            {
                ASTNode temp = itemNode;
                ExtractKnowledge(ref temp, ability, ref isFirstToken);
            }
        }
    }

    // ==========================================
    // EXPORTING (Symmetrical Reconstruction)
    // ==========================================

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

        // Abilities utilize an implicit replica prefix! (e.g., "Burst.sd..." instead of "replica.Burst.sd...")
        if (!string.IsNullOrEmpty(baseReplica))
        {
            sb.Append(FormatName(baseReplica));
        }

        if (!hasImageOverride) AppendColorModifier(sb);

        AppendDiceSides(sb);

        if (items != null)
        {
            foreach (var itm in items.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                sb.Append($".i.{itm}");
            }
        }

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

        string faceModifiers = BuildFaceModifiers(allowFacade: true);
        if (!string.IsNullOrEmpty(faceModifiers))
        {
            sb.Append(faceModifiers);
        }

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

    //me being snide
    public static AbilityData FigureItOut(string data) => CreateAbility(data);
    public static AbilityData WhatAmI(string data) => CreateAbility(data);
    // ==========================================
    // TYPE GUESSER FACTORY
    // ==========================================

    public static AbilityData CreateSpellOrTactic(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        // 1. Parse into a temporary probe object
        ProbeAbilityData probe = new ProbeAbilityData();
        probe.ParseInstance(data);

        // 2. Spell check: index 4 (5th face) has effectID == 76 and non-zero pips
        bool isSpell = false;
        if (probe.diceSides != null && probe.diceSides.Length > 4)
        {
            var face5 = probe.diceSides[4];
            if (face5 != null && face5.effectID == 76 && face5.pips > 0)
            {
                isSpell = true;
            }
        }

        // 3. Instantiate and parse the concrete type
        AbilityData result = isSpell ? (AbilityData)new SpellData() : new TacticData();
        result.ParseInstance(data);
        return result;
    }

    public static AbilityData CreateAbility(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        // 1. Parse into a temporary probe object to extract configuration details
        ProbeAbilityData probe = new ProbeAbilityData();
        probe.ParseInstance(data);

        // 2. Rule 1: Non-zero health means TriggerHPData
        if (probe.hp != 0)
        {
            TriggerHPData triggerHP = new TriggerHPData();
            triggerHP.ParseInstance(data);
            return triggerHP;
        }

        // 3. Rule 2: SpellData check
        // Check if index 4 (5th face) contains effectID 76 and non-zero pips
        bool isSpell = false;
        if (probe.diceSides != null && probe.diceSides.Length > 4)
        {
            var face5 = probe.diceSides[4];
            if (face5 != null && face5.effectID == 76 && face5.pips > 0)
            {
                isSpell = true;
            }
        }

        if (isSpell)
        {
            SpellData spell = new SpellData();
            spell.ParseInstance(data);
            return spell;
        }

        // 4. Rule 3: OnHitData check
        // Check if only the left dice face (index 0), and nothing else, is defined
        bool onlyLeftFace = false;
        if (probe.diceSides != null && probe.diceSides.Length > 0)
        {
            var face1 = probe.diceSides[0];
            bool face1Defined = face1 != null && (face1.effectID != 0 || face1.pips != 0);

            if (face1Defined)
            {
                bool otherFacesDefined = false;
                for (int i = 1; i < probe.diceSides.Length; i++)
                {
                    var face = probe.diceSides[i];
                    if (face != null && (face.effectID != 0 || face.pips != 0))
                    {
                        otherFacesDefined = true;
                        break;
                    }
                }

                if (!otherFacesDefined)
                {
                    // Verify no other attributes like items or traits are defined
                    bool hasExtraData = (probe.items != null && probe.items.Count > 0) ||
                                        (probe.traits != null && probe.traits.Count > 0) ||
                                        (probe.blessings != null && probe.blessings.Count > 0) ||
                                        (probe.baseAbilityData != null && probe.baseAbilityData.Count > 0);

                    if (!hasExtraData)
                    {
                        onlyLeftFace = true;
                    }
                }
            }
        }

        if (onlyLeftFace)
        {
            OnHitData onHit = new OnHitData();
            onHit.ParseInstance(data);
            return onHit;
        }

        // 5. Rule 4: TacticData fallback
        // If index 4 is zero, and none of the other conditions match
        TacticData tactic = new TacticData();
        tactic.ParseInstance(data);
        return tactic;
    }

    // A private, lightweight class used solely to probe data strings safely
    private class ProbeAbilityData : AbilityData
    {
        public ProbeAbilityData()
        {
            // Initialize the array in case the base class doesn't do it eagerly
            if (diceSides == null)
            {
                diceSides = new DiceSideData[6];
                for (int i = 0; i < 6; i++) diceSides[i] = new DiceSideData();
            }
        }

        public override string ExportWrapped() => string.Empty;
    }

    protected void InitializeDefaults()
    {
        baseReplica = "Fey";
    }
}