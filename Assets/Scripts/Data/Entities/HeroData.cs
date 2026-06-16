using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// ==========================================
// HERO DOMAIN DICTIONARY (Factual Rules)
// ==========================================
public static class HeroDomainRules
{
    // These specific modifiers are lists and nested containers that require complex parsing.
    public static readonly HashSet<string> CollectionModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "t", "gift", "learn", "abilitydata", "jinx"
    };

    // Sub-properties that naturally nest under the base "i." prefix logic
    public static readonly HashSet<string> InherentSubKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "t", "jinx", "gift", "learn"
    };
}

[System.Serializable]
public class HeroData : EntityData
{
    // Retained for UI/Reflection backwards compatibility
    public static readonly string[] HeroPropertyKeys =
    {
        "replica", "img", "n", "col", "hp", "tier", "hsv", "hsl", "hue", "sd",
        "speech", "doc", "i", "p", "t", "gift", "abilitydata", "adj", "b", "rect",
        "draw", "thue", "triggerhpdata"
    };

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

    // ==========================================
    // INITIALIZATION STATE MANAGEMENT
    // ==========================================

    public void InitializeAsBlank()
    {
        entityName = null;
        imageOverride = null;
        baseReplica = null;
        colorClass = null;

        hp = 0;
        h = 0;
        s = 0;
        v = 0;
        tier = 0;
        hue = 0;

        hsl = null;
        p = null;
        b = null;
        rect = null;
        draw = null;
        thue = null;
        doc = null;
        speech = null;
        adj = null;

        items = new List<string>();
        customItems = new List<ItemData>();
        traits = new List<string>();
        blessings = new List<string>();
        curses = new List<string>();
        baseAbilityData = new List<string>();
        customSpells = new List<SpellData>();
        customTactics = new List<TacticData>();

        diceSides = new DiceSideData[6];
        for (int i = 0; i < 6; i++)
        {
            diceSides[i] = new DiceSideData { effectID = 0, pips = 0, facadeID = null, keywords = new List<string>() };
        }
    }

    public void InitializeAsDefault()
    {
        entityName = "NewEntity";
        baseReplica = "Statue";
        colorClass = "y";
        imageOverride = "None";
        hp = 7;
        tier = 1;
    }

    // ==========================================
    // AST KNOWLEDGE EXTRACTION (Parse Core)
    // ==========================================

    public virtual void ParseInstance(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        UnityEngine.Debug.Log($"[HeroData] Starting AST Parse on data length: {data.Length}");

        // Hand off to the master internal AST builder (identical to ItemData)
        ASTNode root = new ASTParser(data.Trim()).Parse();
        ExtractKnowledge(ref root, this);

        UnityEngine.Debug.Log($"[HeroData] AST Parse Complete! Name: '{entityName}', Replica: '{baseReplica}'");
    }

    private void ExtractKnowledge(ref ASTNode node, HeroData hero)
    {
        if (node is ChainNode chain)
        {
            for (int i = 0; i < chain.Elements.Count; i++)
            {
                string token = chain.Elements[i].Export().ToLower();

                switch (token)
                {
                    case "replica":
                        if (i + 1 < chain.Elements.Count) hero.baseReplica = chain.Elements[++i].Export();
                        break;
                    case "n":
                        if (i + 1 < chain.Elements.Count) hero.entityName = chain.Elements[++i].Export();
                        break;
                    case "img":
                        if (i + 1 < chain.Elements.Count) hero.imageOverride = chain.Elements[++i].Export();
                        break;
                    case "col":
                        if (i + 1 < chain.Elements.Count) hero.colorClass = chain.Elements[++i].Export();
                        break;
                    case "hp":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int hpVal)) hero.hp = hpVal;
                        break;
                    case "tier":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int t)) hero.tier = t;
                        break;
                    case "hsv":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string[] hsvParts = chain.Elements[++i].Export().Split(':');
                            if (hsvParts.Length == 3)
                            {
                                int.TryParse(hsvParts[0], out hero.h);
                                int.TryParse(hsvParts[1], out hero.s);
                                int.TryParse(hsvParts[2], out hero.v);
                            }
                        }
                        break;
                    case "hsl":
                        if (i + 1 < chain.Elements.Count) hero.hsl = chain.Elements[++i].Export();
                        break;
                    case "hue":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int hVal)) hero.hue = hVal;
                        break;
                    case "p":
                        if (i + 1 < chain.Elements.Count) hero.p = chain.Elements[++i].Export();
                        break;
                    case "b":
                        if (i + 1 < chain.Elements.Count) hero.b = chain.Elements[++i].Export();
                        break;
                    case "rect":
                        if (i + 1 < chain.Elements.Count) hero.rect = chain.Elements[++i].Export();
                        break;
                    case "draw":
                        if (i + 1 < chain.Elements.Count) hero.draw = chain.Elements[++i].Export();
                        break;
                    case "thue":
                        if (i + 1 < chain.Elements.Count) hero.thue = chain.Elements[++i].Export();
                        break;
                    case "adj":
                        if (i + 1 < chain.Elements.Count && int.TryParse(chain.Elements[++i].Export(), out int a)) hero.adj = a;
                        break;
                    case "speech":
                        if (i + 1 < chain.Elements.Count) hero.speech = chain.Elements[++i].Export();
                        break;
                    case "doc":
                        if (i + 1 < chain.Elements.Count) hero.doc = chain.Elements[++i].Export();
                        break;
                    case "sd":
                        if (i + 1 < chain.Elements.Count)
                        {
                            string[] faces = chain.Elements[++i].Export().Split(':');
                            for (int f = 0; f < Mathf.Min(faces.Length, 6); f++)
                            {
                                if (faces[f] == "0") continue;
                                string[] faceParts = faces[f].Split('-');
                                if (faceParts.Length == 2)
                                {
                                    int.TryParse(faceParts[0], out hero.diceSides[f].effectID);
                                    int.TryParse(faceParts[1], out hero.diceSides[f].pips);
                                }
                            }
                        }
                        break;

                    // --- COLLECTION & NESTED MODIFIERS ---
                    case "t":
                        if (i + 1 < chain.Elements.Count) hero.traits.AddRange(chain.Elements[++i].Export().Split('#'));
                        break;
                    case "gift":
                        if (i + 1 < chain.Elements.Count) hero.blessings.AddRange(chain.Elements[++i].Export().Split('#'));
                        break;
                    case "abilitydata":
                        if (i + 1 < chain.Elements.Count)
                        {
                            ASTNode next = chain.Elements[++i];
                            if (next is ScopeNode abScope)
                            {
                                AbilityData ability = AbilityData.CreateAbility(abScope.Export());
                                hero.AddCustomAbility(ability);

                            }
                            else
                            {
                                hero.baseAbilityData.AddRange(next.Export().Split('#'));
                            }
                        }
                        break;
                    case "i":
                        if (i + 1 < chain.Elements.Count)
                        {
                            ASTNode next = chain.Elements[i + 1];
                            string nextVal = next.Export().ToLower();

                            if (nextVal == "t" && i + 2 < chain.Elements.Count && chain.Elements[i + 2].Export().ToLower() == "jinx" && i + 3 < chain.Elements.Count)
                            {
                                hero.curses.AddRange(chain.Elements[i + 3].Export().Split('#'));
                                i += 3;
                            }
                            else if (nextVal == "gift" && i + 2 < chain.Elements.Count)
                            {
                                hero.blessings.AddRange(chain.Elements[i + 2].Export().Split('#'));
                                i += 2;
                            }
                            else if (nextVal == "learn" && i + 2 < chain.Elements.Count)
                            {
                                hero.baseAbilityData.AddRange(chain.Elements[i + 2].Export().Split('#'));
                                i += 2;
                            }
                            else if (next is ScopeNode customScope)
                            {
                                ItemData item = new ItemData();
                                item.Parse(customScope.Export());
                                hero.customItems.Add(item);
                                i++;
                            }
                            else
                            {
                                hero.items.AddRange(next.Export().Split('#'));
                                i++;
                            }
                        }
                        break;

                    default:
                        // Recursively dive into isolated Scope/Composite wraps to bypass redundant outer parentheses
                        if (chain.Elements[i] is ScopeNode isolatedScope)
                        {
                            ASTNode inner = isolatedScope.Content;
                            ExtractKnowledge(ref inner, hero);
                        }
                        else if (chain.Elements[i] is CompositeNode compNode && compNode.Left is ScopeNode compScope)
                        {
                            ASTNode inner = compScope.Content;
                            ExtractKnowledge(ref inner, hero);
                        }
                        break;
                }
            }
        }
        else if (node is ScopeNode scope)
        {
            ASTNode inner = scope.Content;
            ExtractKnowledge(ref inner, hero);
        }
        else if (node is SequenceNode seq)
        {
            foreach (var itemNode in seq.Items)
            {
                ASTNode temp = itemNode;
                ExtractKnowledge(ref temp, hero);
            }
        }
    }

    // ==========================================
    // EXPORTING (Symmetrical Reconstruction)
    // ==========================================

    public virtual new string Export()
    {
        StringBuilder heroSb = new StringBuilder();
        heroSb.Append("(");

        bool hasImageOverride = !string.IsNullOrEmpty(imageOverride) &&
                                imageOverride != "None" &&
                                imageOverride != baseReplica;

        if (!string.IsNullOrEmpty(baseReplica))
        {
            heroSb.Append($"replica.{FormatName(baseReplica)}");
            if (!hasImageOverride) AppendColorModifier(heroSb);
        }

        if (!string.IsNullOrEmpty(entityName)) heroSb.Append($".n.{FormatName(entityName)}");

        if (!string.IsNullOrEmpty(colorClass) && !IsDefaultColor(baseReplica, colorClass))
            heroSb.Append($".col.{colorClass}");

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

        // Suffix/Modifier Chain Output
        StringBuilder thoseSb = new StringBuilder();

        if (traits != null)
            foreach (var t in traits)
                if (!string.IsNullOrEmpty(t)) thoseSb.Append($".i.t.{FormatName(t)}");

        if (items != null)
            foreach (var i in items)
                if (!string.IsNullOrEmpty(i)) thoseSb.Append($".i.{FormatName(i)}");

        if (customItems != null)
            foreach (var ci in customItems)
                if (ci != null) thoseSb.Append($".i.({ci.Export()})");

        if (blessings != null)
            foreach (var bl in blessings)
                if (!string.IsNullOrEmpty(bl)) thoseSb.Append($".gift.{FormatName(bl)}");

        if (curses != null)
            foreach (var c in curses)
                if (!string.IsNullOrEmpty(c)) thoseSb.Append($".i.t.jinx.{FormatName(c)}");

        if (baseAbilityData != null)
            foreach (var ab in baseAbilityData)
                if (!string.IsNullOrEmpty(ab)) thoseSb.Append($".i.learn.{FormatName(ab)}");

        if (customAbilityData != null)
            foreach (var cab in customAbilityData)
                if (cab != null) thoseSb.Append($".abilitydata.({cab.Export()})");

        if (thoseSb.Length == 0) return heroSb.ToString();

        // Parity Export Wrap - The nested composition inherently unwraps exactly via AST Parsing
        return $"({heroSb.ToString()}{thoseSb.ToString()})";
    }

    // ==========================================
    // UTILITIES
    // ==========================================

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

    public bool AddAbility(string abilityName)
    {
        if (string.IsNullOrEmpty(abilityName)) return false;
        if (!baseAbilityData.Contains(abilityName))
        {
            baseAbilityData.Add(abilityName);
            return true;
        }
        return false;
    }

    public bool RemoveAbility(string abilityName)
    {
        return baseAbilityData.Remove(abilityName);
    }

    public void AddCustomAbility(AbilityData ability)
    {
        if (ability == null) return;
        if (customSpells == null) customSpells = new List<SpellData>();
        if (customTactics == null) customTactics = new List<TacticData>();

        if (ability is SpellData spell)
        {
            if (!customSpells.Any(s => s.entityName == spell.entityName)) customSpells.Add(spell);
        }
        else if (ability is TacticData tactic)
        {
            if (!customTactics.Any(t => t.entityName == tactic.entityName)) customTactics.Add(tactic);
        }
    }

    public bool RemoveCustomAbility(string abilityName)
    {
        bool removed = false;

        if (customSpells != null)
        {
            var target = customSpells.FirstOrDefault(s => s.entityName == abilityName);
            if (target != null) removed = customSpells.Remove(target);
        }

        if (!removed && customTactics != null)
        {
            var target = customTactics.FirstOrDefault(t => t.entityName == abilityName);
            if (target != null) removed = customTactics.Remove(target);
        }

        return removed;
    }

    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- HERO DATA DEBUG ---");

        sb.AppendLine($"{indent}Name: {entityName}");
        sb.AppendLine($"{indent}Base Replica: {baseReplica}");
        sb.AppendLine($"{indent}Color Class: {colorClass}");
        sb.AppendLine($"{indent}Tier: {tier}");
        sb.AppendLine($"{indent}HP: {hp}");
        sb.AppendLine($"{indent}Image Override: {imageOverride}");

        if (h != 0 || s != 0 || v != 0 || hue != 0 || !string.IsNullOrEmpty(hsl))
        {
            sb.AppendLine($"{indent}HSV: {h}:{s}:{v} | Hue: {hue} | HSL: {hsl}");
        }

        if (adj.HasValue || !string.IsNullOrEmpty(p) || !string.IsNullOrEmpty(b) ||
            !string.IsNullOrEmpty(rect) || !string.IsNullOrEmpty(draw) || !string.IsNullOrEmpty(thue))
        {
            sb.AppendLine($"{indent}Layout/Visuals -> adj: {adj}, p: {p}, b: {b}, rect: {rect}, draw: {draw}, thue: {thue}");
        }

        if (!string.IsNullOrEmpty(speech) || !string.IsNullOrEmpty(doc))
        {
            sb.AppendLine($"{indent}Narrative -> speech: '{speech}', doc: '{doc}'");
        }

        if (diceSides != null)
        {
            sb.AppendLine($"{indent}Dice Sides:");
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                if (side != null)
                {
                    sb.AppendLine($"{indent}  [{i}] EffectID: {side.effectID} | Pips: {side.pips}");
                }
            }
        }

        if (traits != null && traits.Count > 0)
            sb.AppendLine($"{indent}Traits: {string.Join(", ", traits)}");
        if (blessings != null && blessings.Count > 0)
            sb.AppendLine($"{indent}Blessings: {string.Join(", ", blessings)}");
        if (curses != null && curses.Count > 0)
            sb.AppendLine($"{indent}Curses: {string.Join(", ", curses)}");
        if (baseAbilityData != null && baseAbilityData.Count > 0)
            sb.AppendLine($"{indent}Base Abilities: {string.Join(", ", baseAbilityData)}");
        if (items != null && items.Count > 0)
            sb.AppendLine($"{indent}Items (Stock): {string.Join(", ", items)}");

        if (customItems != null && customItems.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Items ({customItems.Count}):");
            for (int i = 0; i < customItems.Count; i++)
            {
                var ci = customItems[i];
                if (ci != null)
                {
                    sb.AppendLine($"{indent}  [{i}] [✓ Recursively Unpacked ItemData!]");
                    ci.DebugContentsToConsole(indent + "        ");
                }
            }
        }

        if (customSpells != null && customSpells.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Spells ({customSpells.Count}):");
            for (int i = 0; i < customSpells.Count; i++)
            {
                var spell = customSpells[i];
                if (spell != null)
                {
                    sb.AppendLine($"{indent}  [{i}] Spell Name: {spell.entityName}");
                }
            }
        }

        if (customTactics != null && customTactics.Count > 0)
        {
            sb.AppendLine($"{indent}Custom Tactics ({customTactics.Count}):");
            for (int i = 0; i < customTactics.Count; i++)
            {
                var tactic = customTactics[i];
                if (tactic != null)
                {
                    sb.AppendLine($"{indent}  [{i}] Tactic Name: {tactic.entityName}");
                }
            }
        }

        if (indent == "") UnityEngine.Debug.Log(sb.ToString());
        else UnityEngine.Debug.Log(sb.ToString());
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

        if (h != 0) sb.AppendLine($"{indent}h: {h}");
        if (s != 0) sb.AppendLine($"{indent}s: {s}");
        if (v != 0) sb.AppendLine($"{indent}v: {v}");
        if (hue != 0) sb.AppendLine($"{indent}Hue: {hue}");
        if (!string.IsNullOrEmpty(hsl)) sb.AppendLine($"{indent}HSL: {hsl}");

        if (adj.HasValue) sb.AppendLine($"{indent}adj: {adj.Value}");
        if (!string.IsNullOrEmpty(p)) sb.AppendLine($"{indent}p: {p}");
        if (!string.IsNullOrEmpty(b)) sb.AppendLine($"{indent}b: {b}");
        if (!string.IsNullOrEmpty(rect)) sb.AppendLine($"{indent}rect: {rect}");
        if (!string.IsNullOrEmpty(draw)) sb.AppendLine($"{indent}draw: {draw}");
        if (!string.IsNullOrEmpty(thue)) sb.AppendLine($"{indent}thue: {thue}");

        if (!string.IsNullOrEmpty(speech)) sb.AppendLine($"{indent}Speech: '{speech}'");
        if (!string.IsNullOrEmpty(doc)) sb.AppendLine($"{indent}Doc: '{doc}'");

        if (diceSides != null && diceSides.Length > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < diceSides.Length; i++)
            {
                var side = diceSides[i];
                if (side != null && (side.effectID != 0 || side.pips != 0))
                {
                    if (!headerPrinted)
                    {
                        sb.AppendLine($"{indent}Dice Sides:");
                        headerPrinted = true;
                    }
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
                    if (!headerPrinted)
                    {
                        sb.AppendLine($"{indent}Custom Items ({customItems.Count}):");
                        headerPrinted = true;
                    }
                    sb.AppendLine($"{indent}  [{i}] [✓ Unpacked ItemData]");
                    ci.DebugContentsToConsole(indent + "        ");
                }
            }
        }

        if (customSpells != null && customSpells.Count > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < customSpells.Count; i++)
            {
                var spell = customSpells[i];
                if (spell != null)
                {
                    if (!headerPrinted)
                    {
                        sb.AppendLine($"{indent}Custom Spells ({customSpells.Count}):");
                        headerPrinted = true;
                    }
                    sb.AppendLine($"{indent}  [{i}] Spell Name: {spell.entityName}");
                }
            }
        }

        if (customTactics != null && customTactics.Count > 0)
        {
            bool headerPrinted = false;
            for (int i = 0; i < customTactics.Count; i++)
            {
                var tactic = customTactics[i];
                if (tactic != null)
                {
                    if (!headerPrinted)
                    {
                        sb.AppendLine($"{indent}Custom Tactics ({customTactics.Count}):");
                        headerPrinted = true;
                    }
                    sb.AppendLine($"{indent}  [{i}] Tactic Name: {tactic.entityName}");
                }
            }
        }

        if (sb.Length > 0)
        {
            string header = $"{indent}--- HERO DATA DEBUG (COMPACT) ---\n";
            UnityEngine.Debug.Log(header + sb.ToString());
        }
    }
}