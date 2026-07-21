using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ============================================================================================
// CRITICAL ARCHITECTURAL CONSTRAINT & AI DEVELOPER GUIDELINE - DO NOT REMOVE
// ============================================================================================
// DO NOT SILENTLY ASSUME OR ALTER THE DICE FACE INDEX LAYOUT! 
// THE CODES, ALIASES, AND BITMASKS ARE RIGIDLY TIED TO THE GAME ENGINE AND MUST NEVER BE ASSUMED.
//
// THE INDICES OF THE 6 DICE SIDES ARE DEFINED STRICTLY AS:
//   Index 0: Left
//   Index 1: Middle (mid)
//   Index 2: Top (top)
//   Index 3: Bottom (bot)
//   Index 4: Right (right)
//   Index 5: Rightmost (rightmost)
//
// ANY TRANSLATION BETWEEN SIDE NAMES/ALIASES AND INDICES *MUST* USE 'DiceTargetHelper' METHODS:
//   - DiceTargetHelper.GetIndicesForTarget(target)
//   - DiceTargetHelper.GetBestAliasCombination(mask)
//
// DO NOT hardcode direct translations (e.g. assuming index 4 is 'mid' or index 1 is 'right').
// Doing so violates engine rules and corrupts hero/item properties on export.
// ============================================================================================

public static class ItemSyntaxCompiler
{
    /// <summary>
    /// Compiles a list of sibling cards, automatically injecting dots or operators where needed.
    /// </summary>
    /// <summary>
    /// Compiles a list of sibling cards, automatically injecting dots or operators where needed.
    /// Guaranteed to prepend 'i.' at the root level if missing.
    /// </summary>
    public static string CompileZone(IEnumerable<EntityCard> cards, bool isRoot = true)
    {
        if (cards == null) return string.Empty;

        StringBuilder sb = new StringBuilder();
        EntityCard prevCard = null;

        foreach (var card in cards)
        {
            string part = CompileCard(card);
            if (string.IsNullOrWhiteSpace(part)) continue;
            part = part.Trim();

            // Handle automatic joining between siblings
            if (prevCard != null)
            {
                var prevDef = NodeRegistry.Get(prevCard.NodeType);
                var currDef = NodeRegistry.Get(card.NodeType);

                bool prevIsOp = prevDef.IsOperator;
                bool currIsOp = currDef.IsOperator;

                // If neither node is an operator, inject a natural dot separator
                if (!prevIsOp && !currIsOp)
                {
                    string currentStr = sb.ToString();
                    if (!currentStr.EndsWith(".") && !currentStr.EndsWith("#") &&
                        !currentStr.EndsWith(".mrg.") && !currentStr.EndsWith(".splice.") && !currentStr.EndsWith(".i.") &&
                        !part.StartsWith(".") && !part.StartsWith("#"))
                    {
                        sb.Append(".");
                    }
                }
            }

            sb.Append(part);
            prevCard = card;
        }

        string compiled = CleanupSyntax(sb.ToString());

        // AUTOMATIC i. PREFIX ENFORCEMENT AT ROOT LEVEL
        if (isRoot && !string.IsNullOrWhiteSpace(compiled))
        {
            if (compiled.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
            {
                return compiled;
            }
            if (compiled.StartsWith("(", StringComparison.Ordinal))
            {
                return $"i.{compiled}";
            }
            return $"i.{compiled}";
        }

        return compiled;
    }

    /// <summary>
    /// Evaluates whether an expression requires enclosing brackets. Simple atomic strings,
    /// standard item names, and clean identifiers bypass unnecessary wrapping.
    /// </summary>
    private static string WrapIfNeeded(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return string.Empty;
        expr = expr.Trim();

        // Already wrapped cleanly
        if (expr.StartsWith("(") && expr.EndsWith(")") && IsBalanced(expr))
            return expr;

        // FIX: Added '.' to complexDelimiters. 
        // This ensures any chained suffixes/prefixes are wrapped in brackets, 
        // but single words (no dots/operators) remain clean.
        char[] complexDelimiters = new char[] { '#', ':', '-', '.' };
        if (expr.IndexOfAny(complexDelimiters) >= 0 || expr.Contains(".mrg.") || expr.Contains(".splice.") || expr.Contains(".i."))
        {
            return $"({expr})";
        }

        return expr;
    }

    private static bool IsBalanced(string input)
    {
        int depth = 0;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(') depth++;
            else if (input[i] == ')') depth--;
            if (depth == 0 && i < input.Length - 1) return false; // Closed too early
        }
        return depth == 0;
    }

    /// <summary>
    /// Compiles a single card and recursively resolves its children.
    /// </summary>
    public static string CompileCard(EntityCard card)
    {
        if (card == null) return string.Empty;

        //Recursively compile children first
        string childrenCompiled = string.Empty;
        if (card.PayloadPort != null && card.PayloadPort.Entrants.Count > 0)
        {
            // Pass false for isRoot when compiling child/payload ports!
            childrenCompiled = CompileZone(card.PayloadPort.Entrants.Cast<EntityCard>(), false);
        }

        // Delegate to specific node formatters
        switch (card.NodeType)
        {
            case ItemNodeType.Equippable:
                return BuildEquippable(card, childrenCompiled);
            case ItemNodeType.Hat:
                return BuildHat(card, childrenCompiled);
            case ItemNodeType.BaseItem:
                return BuildBaseItem(card, childrenCompiled);
            case ItemNodeType.Bracket:
                return BuildBracket(card, childrenCompiled); // Add the 'card' parameter here
            case ItemNodeType.Operator:
            case ItemNodeType.RawString:
                return card.MechanicData.PayloadString ?? "";
            default:
                return "";
        }
    }

    // --- NODE FORMATTERS ---

    private static string BuildEquippable(EntityCard card, string childrenCompiled)
    {
        if (card.RootData == null) return string.Empty;

        string baseExpr = "Void";
        string baseItemName = "Void";

        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            // Only wrap in brackets if the inner structure is complex enough to demand it
            baseExpr = WrapIfNeeded(childrenCompiled);

            string firstToken = childrenCompiled.Split(new char[] { '.', '#', '(', ')', ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            baseItemName = firstToken ?? "Custom";
        }

        bool hasClearModifiers = card.RootData.ClearDescription || card.RootData.ClearIcon;
        if (hasClearModifiers)
        {
            string descMod = card.RootData.ClearDescription ? "#cleardesc" : "";
            string iconMod = card.RootData.ClearIcon ? "#clearicon" : "";
            baseExpr = WrapIfNeeded($"{baseExpr}{descMod}{iconMod}");
        }

        List<string> parts = new List<string> { baseExpr };

        bool hasImage = !string.IsNullOrEmpty(card.RootData.imageOverride) &&
                        !card.RootData.imageOverride.Trim().Equals("None", StringComparison.OrdinalIgnoreCase);

        if (hasImage)
        {
            string imgName = card.RootData.imageOverride.Trim();
            bool isBase = IsBaseItem(imgName);
            bool startsWithIte = imgName.StartsWith("ite", StringComparison.OrdinalIgnoreCase);

            if (imgName.StartsWith("("))
            {
                parts.Add($"img.{imgName}");
                if (card.RootData.HsvShift.HasValue) parts.Add(FormatHsv(card.RootData.HsvShift.Value));
            }
            else if (isBase || startsWithIte)
            {
                parts.Add($"img.{(isBase ? GetBaseItemName(imgName) : imgName)}");
                if (card.RootData.HsvShift.HasValue) parts.Add(FormatHsv(card.RootData.HsvShift.Value));
            }
            else
            {
                string drawOffset = ":-1:-1";
                if (baseItemName.Equals("Void", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add($"draw.{imgName}{drawOffset}");
                    if (card.RootData.HsvShift.HasValue) parts.Add(FormatHsv(card.RootData.HsvShift.Value));
                }
                else
                {
                    if (card.RootData.HsvShift.HasValue)
                        parts.Add($"img.void.draw.(void.img.{imgName}.{FormatHsv(card.RootData.HsvShift.Value)}){drawOffset}");
                    else
                        parts.Add($"img.void.draw.{imgName}{drawOffset}");
                }
            }
        }
        else if (card.RootData.HsvShift.HasValue)
        {
            parts.Add(FormatHsv(card.RootData.HsvShift.Value));
        }

        if (card.RootData.Tier.HasValue) parts.Add($"tier.{card.RootData.Tier.Value}");
        if (!string.IsNullOrEmpty(card.RootData.DocumentedDescription)) parts.Add($"doc.{card.RootData.DocumentedDescription}");
        if (!string.IsNullOrEmpty(card.RootData.entityName)) parts.Add($"n.{card.RootData.entityName}");

        return string.Join(".", parts);
    }

    private static string BuildBaseItem(EntityCard card, string childrenCompiled)
    {
        string internalPayload = card.MechanicData.PayloadString ?? "";
        if (string.IsNullOrWhiteSpace(internalPayload)) return childrenCompiled;

        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            string inner = childrenCompiled;
            string op = "";

            if (inner.StartsWith("#")) { op = "#"; inner = inner.Substring(1); }
            else if (inner.StartsWith(".mrg.")) { op = ".mrg."; inner = inner.Substring(5); }
            else if (inner.StartsWith(".splice.")) { op = ".splice."; inner = inner.Substring(8); }
            else if (inner.StartsWith(".i.")) { op = ".i."; inner = inner.Substring(3); }
            else if (inner.StartsWith(".")) { op = "."; inner = inner.Substring(1); }
            else { op = "."; }

            return $"{WrapIfNeeded(internalPayload)}{op}{WrapIfNeeded(inner)}";
        }

        // FIX: Pass the payload through WrapIfNeeded before returning to guarantee
        // that complex leaf items (like x2.unpack.Candle.part.1.m.2) are bracketed.
        return WrapIfNeeded(internalPayload);
    }

    /// <summary>
    /// Compiles a Hat card. 
    /// NOTE: Facades must be extracted and appended manually by querying the HeroData sides directly.
    /// Do not use string subtraction/replacement (e.g., fullMods.Replace(innerMods, "")) because 
    /// overlapping multi-face keywords or delimiters will mismatch, causing massive compilation corruption.
    /// </summary>
    private static string BuildHat(EntityCard card, string childrenCompiled)
    {
        if (!(card.MechanicData.PayloadData is HeroData heroData)) return "";

        var validTargets = card.MechanicData.Targets?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        string targets = (validTargets != null && validTargets.Count > 0) ? string.Join(".", validTargets) : "left";
        string prefix = targets.Equals("all", StringComparison.OrdinalIgnoreCase) ? "" : $"{targets}.";
        string hatDice = HatNodeDef.GetHatDiceString(heroData);

        // 1. Calculate the target mask of the Hat itself
        int hatMask = 63; // Defaults to 'all'
        if (validTargets != null && validTargets.Count > 0 && !validTargets.Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            hatMask = 0;
            foreach (var t in validTargets)
            {
                var alias = DiceTargetHelper.TargetAliases.FirstOrDefault(a => a.name != null && a.name.Equals(t, StringComparison.OrdinalIgnoreCase));
                if (alias.name != null) hatMask |= alias.mask;
            }
        }

        // 2. Identify which faces have Facades and build their combined mask
        int facadeMask = 0;
        string sharedFacade = null;
        bool multipleDistinctFacades = false;

        for (int i = 0; i < 6; i++)
        {
            string fac = GetFacadeOutput(heroData.diceSides[i]);
            if (!string.IsNullOrEmpty(fac))
            {
                facadeMask |= (1 << i);
                if (sharedFacade == null) sharedFacade = fac;
                else if (sharedFacade != fac) multipleDistinctFacades = true;
            }
        }

        // 3. Intelligently format the Facade string based on mask alignment
        string facadeMods = "";
        if (facadeMask != 0)
        {
            if (facadeMask == hatMask && !multipleDistinctFacades)
            {
                facadeMods = $"#facade.{sharedFacade}";
            }
            else if (facadeMask == 63 && !multipleDistinctFacades)
            {
                // CHANGED: Use '#' to chain full-mask facades to maintain context
                facadeMods = $"#facade.{sharedFacade}";
            }
            else
            {
                List<string> fMods = new List<string>();
                for (int i = 0; i < 6; i++)
                {
                    string fac = GetFacadeOutput(heroData.diceSides[i]);
                    if (!string.IsNullOrEmpty(fac))
                    {
                        fMods.Add($"{DiceTargetHelper.FaceNames[i]}.facade.{fac}");
                    }
                }
                // CHANGED: Joined with '#' instead of '.' to chain multi-facade expressions
                facadeMods = "#" + string.Join("#", fMods);
            }
        }

        string hatCore = "";
        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            string inner = childrenCompiled.Trim();
            if (inner.StartsWith(".")) inner = inner.Substring(1);

            // ====================================================================================
            // CONTEXTUAL MERGE OPTIMIZATION:
            // Prevents strings from bloating into verbose '.i.' boundaries if the inner
            // items can seamlessly inherit the target side of the Hat's native sticker.
            // ====================================================================================
            string rawInner = inner;
            if (rawInner.StartsWith("(") && rawInner.EndsWith(")"))
            {
                rawInner = rawInner.Substring(1, rawInner.Length - 2);
            }

            string firstTarget = null;
            foreach (var face in DiceTargetHelper.FaceNames)
            {
                if (rawInner.StartsWith($"{face}.", StringComparison.OrdinalIgnoreCase))
                {
                    firstTarget = face;
                    break;
                }
            }

            bool mergedSuccessfully = false;

            // If the inner string explicitly targets a face, check if we can chain it to the native sticker
            if (!string.IsNullOrEmpty(firstTarget))
            {
                string expectedStickerPrefix = $".i.{firstTarget}.sticker.";
                int stickerIdx = hatDice.LastIndexOf(expectedStickerPrefix, StringComparison.OrdinalIgnoreCase);

                // Verify the sticker is the last block appended to hatDice so we can safely chain to it
                if (stickerIdx >= 0 && !hatDice.Substring(stickerIdx + expectedStickerPrefix.Length).Contains(".i."))
                {
                    string[] innerChains = rawInner.Split('#');
                    for (int c = 0; c < innerChains.Length; c++)
                    {
                        // Strip redundant targets from subsequent chained items so they inherit properly via '#'
                        if (innerChains[c].StartsWith($"{firstTarget}.", StringComparison.OrdinalIgnoreCase))
                        {
                            innerChains[c] = innerChains[c].Substring(firstTarget.Length + 1);
                        }
                    }

                    string optimizedInner = string.Join("#", innerChains);

                    // Chain them with AND (#) instead of injecting the Boundary (.i.)
                    hatCore = $"{prefix}hat.({hatDice}#{optimizedInner})";
                    mergedSuccessfully = true;
                }
            }

            if (!mergedSuccessfully)
            {
                // Fallback for distinct contexts that genuinely require a boundary reset
                hatCore = $"{prefix}hat.({hatDice}.i.{inner})";
            }
        }
        else
        {
            hatCore = $"{prefix}hat.({hatDice})";
        }

        return $"{hatCore}{facadeMods}";
    }

    private static string GetFacadeOutput(DiceSideData side)
    {
        if (side == null || string.IsNullOrEmpty(side.facadeID)) return null;

        // If the color is null, empty, or a zero-variant, return with the required :0 suffix
        if (string.IsNullOrEmpty(side.facadeColor) ||
            side.facadeColor == "0" ||
            side.facadeColor == "0:0" ||
            side.facadeColor == "0:0:0")
        {
            return $"{side.facadeID}:0";
        }

        // Otherwise, append the custom HSV color values
        return $"{side.facadeID}:{side.facadeColor}";
    }

    private static string BuildBracket(EntityCard card, string childrenCompiled)
    {
        if (string.IsNullOrWhiteSpace(childrenCompiled)) return string.Empty;

        // Perfectly reconstruct the wrapper node matching Engine Export logic
        List<string> parts = new List<string>();
        if (card.MechanicData.Targets != null && card.MechanicData.Targets.Count > 0) parts.AddRange(card.MechanicData.Targets);
        if (card.MechanicData.RepeatTimes != 1) parts.Add($"x{card.MechanicData.RepeatTimes}");
        if (card.MechanicData.PerTier) parts.Add("pertier");
        if (card.MechanicData.Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(card.MechanicData.Prefix)) parts.Add(card.MechanicData.Prefix);

        parts.Add($"({childrenCompiled})");

        if (card.MechanicData.PartIndex.HasValue) parts.Add($"part.{card.MechanicData.PartIndex.Value}");
        if (card.MechanicData.Multiplier != 1) parts.Add($"m{card.MechanicData.Multiplier}");
        if (!string.IsNullOrEmpty(card.MechanicData.MergedItem)) parts.Add($"mrg.{card.MechanicData.MergedItem}");
        if (!string.IsNullOrEmpty(card.MechanicData.SplicedItem)) parts.Add($"splice.{card.MechanicData.SplicedItem}");

        return string.Join(".", parts);
    }

    // --- UTILITIES & SANITIZATION ---

    private static string FormatHsv(ItemHsvShift hsv) => $"hsv.{hsv.Hue}:{hsv.Saturation}:{hsv.Value}";

    private static string CleanupSyntax(string raw)
    {
        string clean = raw;
        int previousLength = 0;

        // Loop replacements until the string stops changing (cleans cascading errors like "...#...")
        while (clean.Length != previousLength)
        {
            previousLength = clean.Length;
            clean = clean.Replace("..", ".")
                         .Replace(".#", "#")
                         .Replace("#.", "#")
                         .Replace(".mrg..", ".mrg.")
                         .Replace("..mrg.", ".mrg.")
                         .Replace(".splice..", ".splice.")
                         .Replace("..splice.", ".splice.")
                         .Replace(".i..", ".i.")
                         .Replace("..i.", ".i.")
                         .Replace("(.i.", "(") // i. shouldn't immediately follow an open bracket
                         .Replace("(.mrg.", "(")
                         .Replace("(.splice.", "(")
                         .Replace("(#", "(");
        }

        // Clean up trailing operators if a node group ended abruptly
        if (clean.EndsWith(".") || clean.EndsWith("#")) clean = clean.Substring(0, clean.Length - 1);

        return clean;
    }

    private static bool IsBaseItem(string imageName)
    {
        if (string.IsNullOrEmpty(imageName)) return false;
        string normalized = imageName.Replace(" ", "").ToLower();
        return Enum.GetNames(typeof(BaseItems)).Any(name => name.ToLower() == normalized);
    }

    private static string GetBaseItemName(string imageName)
    {
        string normalized = imageName.Replace(" ", "").ToLower();
        foreach (var name in Enum.GetNames(typeof(BaseItems)))
        {
            if (name.ToLower() == normalized)
                return Regex.Replace(name, @"(\B[A-Z])", " $1");
        }
        return imageName;
    }
}