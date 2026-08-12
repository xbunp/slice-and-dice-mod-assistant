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
        "heropool", "itempool", "monsterpool", "fight"
    };

    public static readonly HashSet<string> ModifierStartTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "self", "jinx", "vase", "enchant", "ch", "ph", "phi", "phmp"
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
        int parenDepth = 0; // Track parenthesis depth to prevent premature breaks
        while (endIndex < tokens.Count)
        {
            string peek = tokens[endIndex].ToLower();
            parenDepth += peek.Count(c => c == '(') - peek.Count(c => c == ')');

            if (ModifierStartTokens.Contains(peek)) depth++;
            else if (ModifierEndTokens.Contains(peek)) depth--;

            endIndex++;

            if (depth == 0) break;

            // If we are at the top level, and we encounter a token that clearly belongs to the entity
            if (parenDepth <= 0 && endIndex < tokens.Count)
            {
                string next = tokens[endIndex].ToLower();
                if (next == "i" || next == "t" || next == "doc" || next == "mn" || next == "bal" || EntityDomainRules.CommonMetadataKeys.Contains(next))
                {
                    break;
                }
            }
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
    InlineHero,     // Bare hero token, e.g. "Thief.doc.description"
    Choosable,      // "ch"
    Phase,          // "ph"
    PhaseIndexed,   // "phi"
    PhaseModPick    // "phmp"
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
    public string Phase;             // "ch", "ph", etc.

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
    public string StringPayload;     // Used for multi-groupings, delivery, or phase payloads

    [Header("Suffixes")]
    public int? PartIndex;           // "part.0"
    public string ModName;           // "mn.Named Modifier"
    public string DocDescription;    // "doc.description text"
    public bool IsSpirit;            // ".spirit"

    protected override void ParseCore(string cleanData)
    {
        if (string.IsNullOrWhiteSpace(cleanData)) return;

        // A. Check for Top-Level Chaining (&)
        var chainParts = StaticBranchTracing.TopLevelSplit(cleanData, '&');
        if (chainParts.Count > 1)
        {
            ParseSingleModifier(chainParts[0]);
            ChainedModifier = new ModifierData();
            ChainedModifier.Parse(string.Join("&", chainParts.Skip(1)));
            return;
        }

        // B. Check for Top-Level Splicing (.splice.)
        var spliceParts = StaticBranchTracing.TopLevelSplit(cleanData, '.');
        int spliceIdx = spliceParts.FindIndex(p => p.Equals("splice", StringComparison.OrdinalIgnoreCase));
        if (spliceIdx != -1)
        {
            ParseSingleModifier(string.Join(".", spliceParts.Take(spliceIdx)));
            SplicedModifier = new ModifierData();
            SplicedModifier.Parse(string.Join(".", spliceParts.Skip(spliceIdx + 1)));
            return;
        }

        // C. Parse Standard Structure
        ParseSingleModifier(cleanData);
    }

    private void ParseSingleModifier(string data)
    {
        string originalInput = data;
        data = StaticBranchTracing.StripOuterParens(data);
        UnityEngine.Debug.Log($"[MODIFIER PARSE START] Raw Input: '{originalInput}' | Stripped: '{data}'");

        List<string> tokens = StaticBranchTracing.TopLevelSplit(data, '.');
        UnityEngine.Debug.Log($"[MODIFIER PARSE TOKENS] Count: {tokens.Count} -> [{string.Join(" | ", tokens)}]");
        if (tokens.Count == 0) return;

        // POP SUFFIXES FIRST (from end to front) to avoid them getting eaten by payloads
        while (tokens.Count > 0)
        {
            string lastLower = tokens.Last().ToLower();
            if (lastLower == "spirit")
            {
                IsSpirit = true;    
                tokens.RemoveAt(tokens.Count - 1);
                continue;
            }

            // Do not pop modifier suffixes if the tokens belong to an inline Monster or Hero entity
            // Avoid falsely triggering on core Modifiers that act as containers (like 'vase' or 'self')
            bool isEntityPayload = false;
            foreach (string t in tokens)
            {
                // Skip over start tokens to check if the payload itself is an entity
                if (ModifierDomainRules.IsModifierStartToken(t)) continue;
                if (StaticBranchTracing.IsMonsterEntity(t) || StaticBranchTracing.IsHeroEntity(t))
                {
                    isEntityPayload = true;
                }
                break;
            }
            if (isEntityPayload)
            {
                break;
            }

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
            if (lower == "ch" || lower == "ph" || lower == "phi" || lower == "phmp" || lower == "fh" || lower == "lh") { Phase = token; continue; }
            if (lower.StartsWith("e") && int.TryParse(lower.Substring(1), out _))
            {
                EveryXFights = token;
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

            if (lower == "ch" || lower == "ph" || lower == "phi" || lower == "phmp")
            {
                ActionType = lower switch
                {
                    "ch" => ModifierActionType.Choosable,
                    "ph" => ModifierActionType.Phase,
                    "phi" => ModifierActionType.PhaseIndexed,
                    "phmp" => ModifierActionType.PhaseModPick,
                    _ => ModifierActionType.Choosable
                };
                StringPayload = remainingPayload;
                break;
            }

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
                string payloadToParse = remainingPayload;
                int abDataIdx = payloadToParse.IndexOf("abilitydata.", StringComparison.OrdinalIgnoreCase);
                if (abDataIdx != -1)
                {
                    payloadToParse = payloadToParse.Substring(abDataIdx + "abilitydata.".Length);
                }
                AbilityPayload = AbilityData.CreateAbility(payloadToParse);
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
                UnityEngine.Debug.Log($"[MODIFIER PARSE CONTAINER] Container Token: '{lower}' | Remaining Nested Payload: '{remainingPayload}'");
                NestedModifierPayload = new ModifierData();
                NestedModifierPayload.Parse(remainingPayload);
                break;
            }

            if (lower == "spirit")
            {
                ActionType = ModifierActionType.MonsterSpirit;
                break;
            }

            // Bare / Inline Entity Parsing
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
                    if (ActionType == 0) ActionType = ModifierActionType.InlineMonster;
                }
                else
                {
                    HeroPayload = new HeroData();
                    HeroPayload.Parse(entityPayload);
                    if (ActionType == 0) ActionType = ModifierActionType.InlineHero;
                }
                i = endIndex - 1;
                continue;
            }

            ActionType = ModifierActionType.CoreModifier;
            CoreEffectName = token;
            break;
        }
    }

    /// <summary>
    /// COMPILER PASS: Validates the author's input against the strict rules of the game engine.
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

        if (SplicedModifier != null && SplicedModifier.ChainedModifier != null)
            throw new InvalidOperationException("COMPILER ERROR: Cannot splice compound modifiers (e.g. mod.splice.(mod&mod) is invalid).");

        if (ActionType == ModifierActionType.CoreModifier && !PartIndex.HasValue)
        {
            bool hasPrefixes = !string.IsNullOrEmpty(Turn) || !string.IsNullOrEmpty(FloorLevel) || InvertTarget || !string.IsNullOrEmpty(HeroPosition);
            if (hasPrefixes)
                UnityEngine.Debug.LogWarning($"COMPILER WARNING: If '{CoreEffectName}' has multiple parts (like Ghoststone), prefixes will cause the parser to crash. You must target parts explicitly.");
        }
    }

    protected override string ExportCore()
    {
        return ExportInternal(isRoot: true);
    }

    public string ExportInternal(bool isRoot)
    {
        Validate(isRoot);
        UnityEngine.Debug.Log($"[MODIFIER EXPORT START] isRoot: {isRoot} | ActionType: {ActionType} | IsSpirit: {IsSpirit}");
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
        if (!string.IsNullOrEmpty(Phase)) parts.Add(Phase);

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
            case ModifierActionType.Choosable:
                parts.Add("ch"); parts.Add(StringPayload); break;
            case ModifierActionType.Phase:
                parts.Add("ph"); parts.Add(StringPayload); break;
            case ModifierActionType.PhaseIndexed:
                parts.Add("phi"); parts.Add(StringPayload); break;
            case ModifierActionType.PhaseModPick:
                parts.Add("phmp"); parts.Add(StringPayload); break;
            case ModifierActionType.AddMonster:
                parts.Add("add"); parts.Add(MonsterPayload?.Export() ?? ""); break;
            case ModifierActionType.AddHero:
                parts.Add("add"); parts.Add(HeroPayload?.Export() ?? ""); break;
            case ModifierActionType.GiveItem:
                parts.Add("i");
                parts.Add(ItemPayload?.Export() ?? "");
                break;
            case ModifierActionType.AllItem:
                parts.Add("allitem");
                parts.Add(ItemPayload?.Export() ?? "");
                break;
            case ModifierActionType.AllItemE:
                parts.Add("alliteme");
                parts.Add(ItemPayload?.Export() ?? "");
                break;
            case ModifierActionType.PerItem:
                parts.Add("peritem");
                parts.Add(ItemPayload?.Export() ?? "()");
                break;
            case ModifierActionType.Delivery:
                parts.Add("delivery"); parts.Add(StringPayload); break;
            case ModifierActionType.RMod:
                parts.Add("rmod"); parts.Add(StringPayload); break;
            case ModifierActionType.PartyHeroes:
                parts.Add("party"); parts.Add(StringPayload); break;
            case ModifierActionType.EndTurnAbility:
                parts.Add("ea");
                if (AbilityPayload != null) parts.Add($"sThief.abilitydata.{AbilityPayload.Export()}");
                break;
            case ModifierActionType.TransformHero:
                parts.Add("b"); parts.Add(HeroPayload?.Export() ?? ""); break;
            case ModifierActionType.Jinx:
                parts.Add("jinx"); parts.Add(NestedModifierPayload?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Vase:
                parts.Add("vase"); parts.Add(NestedModifierPayload?.ExportInternal(false) ?? ""); break;
            case ModifierActionType.Self:
                string nestedExport = NestedModifierPayload?.ExportInternal(false) ?? "";
                UnityEngine.Debug.Log($"[MODIFIER EXPORT SELF] NestedModifierPayload Exported As: '{nestedExport}'");
                parts.Add("self");
                parts.Add(nestedExport);
                break;
            case ModifierActionType.MonsterSpirit:
                if (MonsterPayload != null)
                {
                    string msExport = MonsterData.ExportAsSpirit(MonsterPayload);
                    if (!isRoot && !string.IsNullOrEmpty(msExport) && !msExport.StartsWith("("))
                        msExport = $"({msExport})";
                    parts.Add(msExport);
                }
                parts.Add("spirit"); break;
            case ModifierActionType.InlineMonster:
                if (MonsterPayload != null)
                {
                    string mExport = MonsterData.ExportAsSpirit(MonsterPayload);
                    // Wrap inline payloads in brackets ONLY if they have structural dots (like .doc) to prevent suffix bleeding
                    if (!isRoot && !string.IsNullOrEmpty(mExport) && (mExport.Contains(".") || mExport.Contains("&")) && !mExport.StartsWith("("))
                        mExport = $"({mExport})";
                    parts.Add(mExport);
                }
                break;
            case ModifierActionType.InlineHero:
                if (HeroPayload != null)
                {
                    string hExport = HeroPayload.Export();
                    if (!isRoot && !string.IsNullOrEmpty(hExport) && (hExport.Contains(".") || hExport.Contains("&")) && !hExport.StartsWith("("))
                        hExport = $"({hExport})";
                    parts.Add(hExport);
                }
                break;
            case ModifierActionType.CoreModifier:
                if (!string.IsNullOrEmpty(CoreEffectName)) parts.Add(CoreEffectName);
                break;
        }

        // 8. Local Suffixes 
        if (PartIndex.HasValue) { parts.Add("part"); parts.Add(PartIndex.Value.ToString()); }
        if (!string.IsNullOrEmpty(ModTier)) { parts.Add("modtier"); parts.Add(ModTier); }

        string blockString = string.Join(".", parts.Where(p => !string.IsNullOrEmpty(p)));

        // 9. Process Combinators (Splices first, then Chains)
        if (SplicedModifier != null)
        {
            blockString = $"{blockString}.splice.{SplicedModifier.ExportInternal(false)}";
        }
        if (ChainedModifier != null)
        {
            blockString = $"{blockString}&{ChainedModifier.ExportInternal(false)}";
        }

        // 10. Handle Suffixes & Nested Bracketing
        bool hasSuffixes = !string.IsNullOrEmpty(ModName) || !string.IsNullOrEmpty(DocDescription) || (IsSpirit && ActionType != ModifierActionType.MonsterSpirit);
        UnityEngine.Debug.Log($"[MODIFIER EXPORT FINAL BLOCK] blockString: '{blockString}' | isRoot: {isRoot} | hasSuffixes: {hasSuffixes}");

        if (isRoot)
        {
            if (!string.IsNullOrEmpty(ModName)) blockString += $".mn.{ModName}";
            if (!string.IsNullOrEmpty(DocDescription)) blockString += $".doc.{DocDescription}";
            if (IsSpirit && ActionType != ModifierActionType.MonsterSpirit) blockString += ".spirit";
            UnityEngine.Debug.Log($"[MODIFIER EXPORT RESULT (ROOT)]: '{blockString}'");
            return blockString;
        }
        if (hasSuffixes)
        {
            string suffixedBlock = StaticBranchTracing.SafeBracket(blockString);
            if (!string.IsNullOrEmpty(ModName)) suffixedBlock += $".mn.{ModName}";
            if (!string.IsNullOrEmpty(DocDescription)) suffixedBlock += $".doc.{DocDescription}";
            if (IsSpirit && ActionType != ModifierActionType.MonsterSpirit) suffixedBlock += ".spirit";
            string result = StaticBranchTracing.SafeBracket(suffixedBlock);
            UnityEngine.Debug.Log($"[MODIFIER EXPORT RESULT (NON-ROOT WITH SUFFIXES)]: '{result}'");
            return result;
        }
        string finalResult = StaticBranchTracing.SafeBracket(blockString);
        UnityEngine.Debug.Log($"[MODIFIER EXPORT RESULT (NON-ROOT NO SUFFIXES)]: '{finalResult}'");
        return finalResult;
    }

    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- MODIFIER DATA ---");
        if (Unpack) sb.AppendLine($"{indent}Unpack: True");
        if (!string.IsNullOrEmpty(FloorLevel)) sb.AppendLine($"{indent}Floors: {FloorLevel}");
        if (!string.IsNullOrEmpty(Turn)) sb.AppendLine($"{indent}Turn: {Turn}");
        if (!string.IsNullOrEmpty(EveryXFights)) sb.AppendLine($"{indent}Every {EveryXFights} fights (Offset {EveryXFightsOffset})");
        if (!string.IsNullOrEmpty(Phase)) sb.AppendLine($"{indent}Phase: {Phase}");

        sb.AppendLine($"{indent}Action Type: {ActionType}");
        if (ActionType == ModifierActionType.CoreModifier) sb.AppendLine($"{indent}Core Effect: '{CoreEffectName}'");
        if (ActionType == ModifierActionType.Choosable || ActionType == ModifierActionType.Phase || ActionType == ModifierActionType.PhaseIndexed || ActionType == ModifierActionType.PhaseModPick)
            sb.AppendLine($"{indent}String Payload: '{StringPayload}'");

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