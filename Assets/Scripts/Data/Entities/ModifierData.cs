using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class ModifierDomainRules
{
    public static readonly HashSet<string> ModLevelTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "heropool", "itempool", "monsterpool", "fight", "ph", "phi", "phmp", "ch"
    };

    public static readonly HashSet<string> ModifierStartTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "self", "jinx", "vase", "enchant"
    };

    public static readonly HashSet<string> ModifierEndTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "spirit"
    };

    public static bool IsModLevelToken(string token) => ModLevelTokens.Contains(token);

    // Determines if a token is a valid face targeting alias 
    public static bool IsTargetAlias(string token)
    {
        string lower = token.ToLower();
        return lower == "all" || DiceTargetHelper.GetIndicesForTarget(lower).Count > 0;
    }

    public static bool IsModifierStartToken(string token) => ModifierStartTokens.Contains(token);

    public static int GetModifierBlockLength(List<string> tokens, int startIndex)
    {
        int endIndex = startIndex;
        int depth = 0;

        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();

            if (ModifierStartTokens.Contains(peek)) depth++;
            else if (ModifierEndTokens.Contains(peek)) depth--;

            endIndex++;

            if (depth == 0) break; // Outer-most modifier scope closed natively
        }

        return endIndex - startIndex;
    }
}

// Enum representing the final "Payload" of the modifier string
public enum ModifierActionType
{
    CoreModifier,     // Base effect, e.g. "cantrip", "Shield Response"
    AddMonster,     // "add.wolf"
    AddHero,        // "add.thief"
    GiveItem,       // "i.item"
    AllItem,        // "allitem.item"
    AllItemE,       // "alliteme.item"
    PerItem,        // "peritem.item"
    Delivery,       // Uses StringPayload for the seed (e.g., "18bfc")
    RMod,           // Uses StringPayload for the seed (e.g., "86b7")
    EndTurnAbility, // "ea.ability"
    TransformHero,  // "b.hero"
    PartyHeroes,    // "party.hero+hero"
    MonsterSpirit,  // "monster.spirit"
    Jinx,           // "jinx.modifier"
    Vase,           // "vase.modifier"
    Self,           // "self.modifier"
    InlineMonster,  // Bare monster token, e.g. "Wolf.doc.description"
    InlineHero      // Bare hero token, e.g. "Thief.doc.description"
}

[System.Serializable]
public class ModifierData : SDData
{
    [Header("Combinators")]
    public ModifierData SplicedModifier; // Handled by .splice.
    public ModifierData ChainedModifier; // Handled by &

    [Header("Timing / Cadence")]
    public string FloorLevel;        // "1" or "1-5"
    public string Turn;              // "t1"
    public string EveryXFights;      // "e2"
    public string EveryXFightsOffset;// ".3" -> e.g. e2.3
    public string EveryXTurns;       // "et3"

    [Header("Stacking / Scaling")]
    public string RepeatTimes;       // "x3"
    public bool PerFightStack;       // "pl"
    public bool PerBossStack;        // "pb"
    public bool PerTurnStack;        // "pt"

    [Header("Game State Rules")]
    public string ModTier;           // "modtier.3"
    public string Difficulty;        // "diff.Hard"

    [Header("Targeting Logic")]
    public bool InvertTarget;        // "inv"
    public string HeroPosition;      // "h.top"
    public bool TargetAllHeroes;     // "hero"
    public bool TargetAllMonsters;   // "monster"
    public string DiceFaceTarget;    // "left2", "row", "all"
    public bool Unpack;              // "unpack"

    [Header("Action Payload")]
    public ModifierActionType ActionType;
    public string CoreEffectName;    // Only used if ActionType == CoreEffect

    // Typed Payloads (Only one of these will generally be populated based on ActionType)
    public MonsterData MonsterPayload;
    public HeroData HeroPayload;
    public ItemData ItemPayload;
    public ModifierData NestedModifierPayload;
    public AbilityData AbilityPayload;
    public string StringPayload;     // Used for multi-groupings like party (hero+hero) or delivery (item+item)

    [Header("Suffixes")]
    public int? PartIndex;           // "part.0"
    public string ModName;           // "mn.Named Modifier"
    public string DocDescription;    // "doc.description text"

    public override void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        data = StaticBranchTracing.StripOuterParens(data.Trim());

        // 1. Check for Top-Level Chaining (&)
        var chainParts = StaticBranchTracing.TopLevelSplit(data, '&');
        if (chainParts.Count > 1)
        {
            ParseCore(chainParts[0]);
            ChainedModifier = new ModifierData();
            ChainedModifier.Parse(string.Join("&", chainParts.Skip(1)));
            return;
        }

        // 2. Check for Top-Level Splicing (.splice.)
        var spliceParts = StaticBranchTracing.TopLevelSplit(data, '.');
        int spliceIdx = spliceParts.FindIndex(p => p.Equals("splice", StringComparison.OrdinalIgnoreCase));
        if (spliceIdx != -1)
        {
            ParseCore(string.Join(".", spliceParts.Take(spliceIdx)));
            SplicedModifier = new ModifierData();
            SplicedModifier.Parse(string.Join(".", spliceParts.Skip(spliceIdx + 1)));
            return;
        }

        // 3. Parse Standard Structure
        ParseCore(data);
    }

    private void ParseCore(string data)
    {
        List<string> tokens = StaticBranchTracing.TopLevelSplit(data, '.');
        if (tokens.Count == 0) return;

        // POP SUFFIXES FIRST (from end to front) to avoid them getting eaten by payloads
        while (tokens.Count > 0)
        {
            string prev = tokens.Count > 1 ? tokens[tokens.Count - 2].ToLower() : "";

            if (prev == "doc")
            {
                DocDescription = tokens.Last();
                tokens.RemoveRange(tokens.Count - 2, 2);
            }
            else if (prev == "mn")
            {
                ModName = tokens.Last();
                tokens.RemoveRange(tokens.Count - 2, 2);
            }
            else if (prev == "part")
            {
                if (int.TryParse(tokens.Last(), out int partVal))
                {
                    PartIndex = partVal;
                    tokens.RemoveRange(tokens.Count - 2, 2);
                }
                else break;
            }
            else
            {
                break;
            }
        }

        // FORWARD PASS
        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            string lower = token.ToLower();

            if (ModifierDomainRules.IsModLevelToken(lower))
            {
                throw new NotImplementedException($"Mod-level structural token '{token}' is not supported in gameplay ModifierData.");
            }

            // Timing / Cadence
            if (Regex.IsMatch(lower, @"^\d+(-\d+)?$")) { FloorLevel = token; continue; }
            if (lower.StartsWith("t") && int.TryParse(lower.Substring(1), out _)) { Turn = token; continue; }
            if (lower.StartsWith("et") && int.TryParse(lower.Substring(2), out _)) { EveryXTurns = token; continue; }
            if (lower.StartsWith("e") && int.TryParse(lower.Substring(1), out _))
            {
                EveryXFights = token;
                // Peek ahead for offset
                if (i + 1 < tokens.Count && int.TryParse(tokens[i + 1], out _))
                {
                    EveryXFightsOffset = tokens[++i];
                }
                continue;
            }

            // Stacking / Scaling
            if (lower.StartsWith("x") && int.TryParse(lower.Substring(1), out _)) { RepeatTimes = token; continue; }
            if (lower == "pl") { PerFightStack = true; continue; }
            if (lower == "pb") { PerBossStack = true; continue; }
            if (lower == "pt") { PerTurnStack = true; continue; }

            // Configurations
            if (lower == "modtier" && i + 1 < tokens.Count) { ModTier = tokens[++i]; continue; }
            if (lower == "diff" && i + 1 < tokens.Count) { Difficulty = tokens[++i]; continue; }
            if (lower == "unpack") { Unpack = true; continue; }

            // Targets
            if (lower == "hero") { TargetAllHeroes = true; continue; }
            if (lower == "monster") { TargetAllMonsters = true; continue; }
            if (lower == "inv") { InvertTarget = true; continue; }
            if (lower == "h" && i + 1 < tokens.Count) { HeroPosition = tokens[++i]; continue; }
            if (ModifierDomainRules.IsTargetAlias(lower)) { DiceFaceTarget = token; continue; }

            // ==== ACTION PAYLOAD ROUTING ==== //
            string remainingPayload = string.Join(".", tokens.Skip(i + 1));

            if (lower == "add")
            {
                if (StaticBranchTracing.IsMonsterEntity(tokens[i + 1]))
                {
                    ActionType = ModifierActionType.AddMonster;
                    MonsterPayload = new MonsterData();
                    MonsterPayload.Parse(remainingPayload);
                }
                else
                {
                    ActionType = ModifierActionType.AddHero;
                    HeroPayload = new HeroData();
                    HeroPayload.Parse(remainingPayload);
                }
                break;
            }
            if (lower == "i" || lower == "allitem" || lower == "alliteme" || lower == "peritem")
            {
                ActionType = lower switch
                {
                    "i" => ModifierActionType.GiveItem,
                    "allitem" => ModifierActionType.AllItem,
                    "alliteme" => ModifierActionType.AllItemE,
                    "peritem" => ModifierActionType.PerItem,
                    _ => ModifierActionType.GiveItem
                };
                ItemPayload = new ItemData();
                ItemPayload.Parse(remainingPayload);
                break;
            }
            if (lower == "ea")
            {
                ActionType = ModifierActionType.EndTurnAbility;
                AbilityPayload = AbilityData.CreateAbility(remainingPayload);
                break;
            }
            if (lower == "b")
            {
                ActionType = ModifierActionType.TransformHero;
                HeroPayload = new HeroData();
                HeroPayload.Parse(remainingPayload);
                break;
            }
            if (lower == "party" || lower == "delivery" || lower == "rmod")
            {
                ActionType = lower switch
                {
                    "party" => ModifierActionType.PartyHeroes,
                    "delivery" => ModifierActionType.Delivery,
                    "rmod" => ModifierActionType.RMod,
                    _ => ModifierActionType.RMod
                };
                StringPayload = remainingPayload;
                break;
            }
            if (lower == "jinx" || lower == "vase" || lower == "self")
            {
                ActionType = lower switch
                {
                    "jinx" => ModifierActionType.Jinx,
                    "vase" => ModifierActionType.Vase,
                    "self" => ModifierActionType.Self,
                    _ => ModifierActionType.Self
                };
                NestedModifierPayload = new ModifierData();
                NestedModifierPayload.Parse(remainingPayload);
                break;
            }
            if (lower == "spirit")
            {
                ActionType = ModifierActionType.MonsterSpirit;
                // 'spirit' acts as the core action flag here
                break;
            }

            // Bare / Inline Entity Parsing (e.g. "Wolf.doc.description" inside a modifier)
            // Crucial: This does NOT break the loop, because trailing tokens like 'spirit' 
            // dictate the final ActionType for this modifier.
            if (StaticBranchTracing.IsMonsterEntity(token) || StaticBranchTracing.IsHeroEntity(token))
            {
                int startIndex = i;
                int endIndex = i + 1;

                while (endIndex < tokens.Count)
                {
                    string peek = tokens[endIndex].ToLower();
                    if (EntityDomainRules.CommonMetadataKeys.Contains(peek))
                    {
                        endIndex += 2;
                        continue;
                    }
                    break;
                }

                string entityPayload = string.Join(".", tokens.GetRange(startIndex, endIndex - startIndex));

                if (StaticBranchTracing.IsMonsterEntity(token))
                {
                    MonsterPayload = new MonsterData();
                    MonsterPayload.Parse(entityPayload);
                    // Set a default action, but allow tokens like 'spirit' further down to overwrite it
                    if (ActionType == 0) ActionType = ModifierActionType.InlineMonster;
                }
                else
                {
                    HeroPayload = new HeroData();
                    HeroPayload.Parse(entityPayload);
                    if (ActionType == 0) ActionType = ModifierActionType.InlineHero;
                }

                i = endIndex - 1;
                continue; // Continue scanning the rest of the modifier to catch suffix actions!
            }

            // If none of the known functional prefixes hit, this token IS the core effect.
            ActionType = ModifierActionType.CoreModifier;
            CoreEffectName = token;
            break;
        }
    }

    /// <summary>
    /// COMPILER PASS: Validates the author's input against the strict rules of the game engine.
    /// Throws an InvalidOperationException if the configuration would cause the engine parser to crash.
    /// </summary>
    public void Validate(bool isRoot = true)
    {
        if (ActionType == ModifierActionType.Jinx && NestedModifierPayload != null)
        {
            if (NestedModifierPayload.ActionType == ModifierActionType.Self)
                throw new InvalidOperationException("COMPILER ERROR: 'jinx.self.<mod>' is invalid. Use 'jinx.i.self.<mod>' instead.");
        }

        if (ActionType == ModifierActionType.AddMonster || ActionType == ModifierActionType.AddHero)
        {
            if (InvertTarget || !string.IsNullOrEmpty(HeroPosition) || TargetAllHeroes || TargetAllMonsters || !string.IsNullOrEmpty(DiceFaceTarget))
                throw new InvalidOperationException($"COMPILER ERROR: '{ActionType}' is targetless. Cannot combine with target scopes.");
        }

        if (!string.IsNullOrEmpty(Difficulty))
            throw new InvalidOperationException("COMPILER ERROR: 'diff' is a Mod-Level setting, not a Modifier string setting.");

        // Splice Rule: Engine evaluates left-to-right. Cannot splice a chained modifier.
        if (SplicedModifier != null && SplicedModifier.ChainedModifier != null)
            throw new InvalidOperationException("COMPILER ERROR: Cannot splice compound modifiers (e.g. mod.splice.(mod&mod) is invalid).");

        // Global Suffix Rule: Names and Docs must be at the top level to avoid UI breaks.
        if (!isRoot && (!string.IsNullOrEmpty(ModName) || !string.IsNullOrEmpty(DocDescription)))
            throw new InvalidOperationException("COMPILER ERROR: ModName and DocDescription apply to the entire package. They must only be set on the root ModifierData, not inside chains or splices.");

        if (ActionType == ModifierActionType.CoreModifier && !PartIndex.HasValue)
        {
            bool hasPrefixes = !string.IsNullOrEmpty(Turn) || !string.IsNullOrEmpty(FloorLevel) || InvertTarget || !string.IsNullOrEmpty(HeroPosition);
            if (hasPrefixes)
                UnityEngine.Debug.LogWarning($"COMPILER WARNING: If '{CoreEffectName}' has multiple parts (like Ghoststone), prefixes will cause the parser to crash. You must target parts explicitly.");
        }
    }


    public override string Export()
    {
        return ExportInternal(isRoot: true);
    }

    private string ExportInternal(bool isRoot)
    {
        Validate(isRoot);

        List<string> parts = new List<string>();

        // 1. Setup (Unpack is local to the specific block)
        if (Unpack) parts.Add("unpack");

        // 2. Timing (Ordered based on engine UI preference)
        if (!string.IsNullOrEmpty(FloorLevel)) parts.Add(FloorLevel);
        if (!string.IsNullOrEmpty(EveryXFights))
        {
            parts.Add(EveryXFights);
            if (!string.IsNullOrEmpty(EveryXFightsOffset)) parts.Add(EveryXFightsOffset);
        }
        if (!string.IsNullOrEmpty(EveryXTurns)) parts.Add(EveryXTurns);
        if (!string.IsNullOrEmpty(Turn)) parts.Add(Turn);

        // 3. Stacking 
        if (!string.IsNullOrEmpty(RepeatTimes)) parts.Add(RepeatTimes);
        if (PerFightStack) parts.Add("pl");
        if (PerBossStack) parts.Add("pb");
        if (PerTurnStack) parts.Add("pt");

        // 4. Entity Targeting (Strict Engine Order: h.pos MUST precede inv)
        if (!string.IsNullOrEmpty(HeroPosition)) { parts.Add("h"); parts.Add(HeroPosition); }
        if (InvertTarget) parts.Add("inv");

        // 5. Dice Face Scopes
        if (!string.IsNullOrEmpty(DiceFaceTarget)) parts.Add(DiceFaceTarget);

        // 6. Traits
        if (TargetAllHeroes) parts.Add("hero");
        if (TargetAllMonsters) parts.Add("monster");

        // 7. Action Payload
        switch (ActionType)
        {
            case ModifierActionType.AddMonster:
                parts.Add("add"); parts.Add(MonsterPayload?.Export() ?? ""); break;
            case ModifierActionType.AddHero:
                parts.Add("add"); parts.Add(HeroPayload?.Export() ?? ""); break;
            case ModifierActionType.GiveItem:
                parts.Add("i"); parts.Add(ItemPayload?.Export() ?? ""); break;
            case ModifierActionType.AllItem:
                parts.Add("allitem"); parts.Add(ItemPayload?.Export() ?? ""); break;
            case ModifierActionType.AllItemE:
                parts.Add("alliteme"); parts.Add(ItemPayload?.Export() ?? ""); break;
            case ModifierActionType.PerItem:
                parts.Add("peritem"); parts.Add(ItemPayload?.Export() ?? ""); break;
            case ModifierActionType.Delivery:
                parts.Add("delivery"); parts.Add(StringPayload); break; // StringPayload is the seed
            case ModifierActionType.RMod:
                parts.Add("rmod"); parts.Add(StringPayload); break; // StringPayload is the seed
            case ModifierActionType.PartyHeroes:
                parts.Add("party"); parts.Add(StringPayload); break;
            case ModifierActionType.EndTurnAbility:
                parts.Add("ea"); /* Requires AbilityPayload Export */ break;
            case ModifierActionType.TransformHero:
                parts.Add("b"); parts.Add(HeroPayload?.Export() ?? ""); break;
            case ModifierActionType.Jinx:
                parts.Add("jinx"); parts.Add(NestedModifierPayload?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Vase:
                parts.Add("vase"); parts.Add(NestedModifierPayload?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Self:
                parts.Add("self"); parts.Add(NestedModifierPayload?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.MonsterSpirit:
                if (MonsterPayload != null) parts.Add(MonsterData.ExportAsSpirit(MonsterPayload));
                parts.Add("spirit"); break;
            case ModifierActionType.InlineMonster:
                if (MonsterPayload != null) parts.Add(MonsterData.ExportAsSpirit(MonsterPayload));
                break;
            case ModifierActionType.InlineHero:
                if (HeroPayload != null) parts.Add(HeroPayload.Export());
                break;
            case ModifierActionType.CoreModifier:
                if (!string.IsNullOrEmpty(CoreEffectName)) parts.Add(CoreEffectName);
                break;
        }

        // 8. Local Suffixes 
        if (PartIndex.HasValue) { parts.Add("part"); parts.Add(PartIndex.Value.ToString()); }
        if (!string.IsNullOrEmpty(ModTier)) { parts.Add("modtier"); parts.Add(ModTier); }

        string blockString = string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p)));

        // 9. Process Combinators (Splices first, then Chains, with strict bracketing)
        if (SplicedModifier != null)
        {
            blockString = $"{blockString}.splice.{SplicedModifier.ExportInternal(false)}";
        }

        if (ChainedModifier != null)
        {
            // By wrapping both sides of the ampersand in parenthesis, we protect prefixes from leaking
            // and prevent the parser from dropping trailing elements.
            blockString = $"({blockString})&({ChainedModifier.ExportInternal(false)})";
        }

        // 10. Global Suffixes (Only applied to the very outermost edge of the package)
        if (isRoot)
        {
            if (!string.IsNullOrEmpty(ModName)) blockString += $".mn.{ModName}";
            if (!string.IsNullOrEmpty(DocDescription)) blockString += $".doc.{DocDescription}";
        }

        return blockString;
    }


    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- MODIFIER DATA ---");

        if (Unpack) sb.AppendLine($"{indent}Unpack: True");
        if (!string.IsNullOrEmpty(FloorLevel)) sb.AppendLine($"{indent}Floors: {FloorLevel}");
        if (!string.IsNullOrEmpty(Turn)) sb.AppendLine($"{indent}Turn: {Turn}");
        if (!string.IsNullOrEmpty(EveryXFights)) sb.AppendLine($"{indent}Every {EveryXFights} fights (Offset {EveryXFightsOffset})");

        sb.AppendLine($"{indent}Action Type: {ActionType}");
        if (ActionType == ModifierActionType.CoreModifier) sb.AppendLine($"{indent}Core Effect: '{CoreEffectName}'");
        if (PartIndex.HasValue) sb.AppendLine($"{indent}Targeted Part: {PartIndex.Value}");

        if (InvertTarget) sb.AppendLine($"{indent}Invert Target: True");
        if (!string.IsNullOrEmpty(HeroPosition)) sb.AppendLine($"{indent}Hero Pos: {HeroPosition}");
        if (!string.IsNullOrEmpty(DiceFaceTarget)) sb.AppendLine($"{indent}Dice Face: {DiceFaceTarget}");

        if (NestedModifierPayload != null)
        {
            sb.AppendLine($"{indent}Nested Modifier Payload:");
            NestedModifierPayload.DebugContentsToConsole(indent + "  ");
        }

        if (ChainedModifier != null)
        {
            sb.AppendLine($"{indent}Chained With (&):");
            ChainedModifier.DebugContentsToConsole(indent + "  ");
        }

        if (SplicedModifier != null)
        {
            sb.AppendLine($"{indent}Spliced With (.splice.):");
            SplicedModifier.DebugContentsToConsole(indent + "  ");
        }

        UnityEngine.Debug.Log(sb.ToString());
    }
}