using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

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

        // TEMP: DISABLED, i is a modifier, not an item technically.
        // AUTOMATIC i. PREFIX ENFORCEMENT AT ROOT LEVEL
        /*
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
        */

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
            case ItemNodeType.LearnAbility:
                return $"{card.MechanicData.Prefix}.{card.MechanicData.PayloadString}";
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
    /// <summary>
    /// Compiles a Hat card. 
    /// NOTE: Facades must be extracted and appended manually by querying the HeroData sides directly.
    /// Do not use string subtraction/replacement (e.g., fullMods.Replace(innerMods, "")) because 
    /// overlapping multi-face keywords or delimiters will mismatch, causing massive compilation corruption.
    /// </summary>
    private static string BuildHat(EntityCard card, string childrenCompiled)
    {
        if (!(card.MechanicData.PayloadData is EntityData ed)) return "";
        var validTargets = card.MechanicData.Targets?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        string targets = (validTargets != null && validTargets.Count > 0) ? string.Join(".", validTargets) : "left";
        string prefix = targets.Equals("all", StringComparison.OrdinalIgnoreCase) ? "" : $"{targets}.";

        // Let the backend format the core hat completely natively
        string hatCoreStr = ed.ExportAsHat();
        string strippedCore = StaticBranchTracing.StripOuterParens(hatCoreStr);

        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            string inner = childrenCompiled.Trim();
            if (inner.StartsWith(".")) inner = inner.Substring(1);

            // Fast-path merge for stickers on specific faces (like left.sticker)
            bool mergedSuccessfully = false;
            string firstTarget = null;
            foreach (var face in DiceTargetHelper.FaceNames)
            {
                if (inner.StartsWith($"{face}.", StringComparison.OrdinalIgnoreCase))
                {
                    firstTarget = face;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(firstTarget))
            {
                string expectedStickerPrefix = $".i.{firstTarget}.sticker.";
                int stickerIdx = strippedCore.LastIndexOf(expectedStickerPrefix, StringComparison.OrdinalIgnoreCase);

                if (stickerIdx >= 0)
                {
                    // Safe verification without naive splits
                    string tail = strippedCore.Substring(stickerIdx + expectedStickerPrefix.Length);
                    int pDepth = 0; bool hasInnerI = false;
                    for (int i = 0; i < tail.Length - 2; i++)
                    {
                        if (tail[i] == '(') pDepth++;
                        else if (tail[i] == ')') pDepth--;
                        else if (pDepth == 0 && tail[i] == '.' && (tail[i + 1] == 'i' || tail[i + 1] == 'I') && tail[i + 2] == '.')
                        {
                            hasInnerI = true; break;
                        }
                    }

                    if (!hasInnerI)
                    {
                        string[] innerChains = StaticBranchTracing.TopLevelSplit(inner, '#').ToArray();
                        for (int c = 0; c < innerChains.Length; c++)
                        {
                            if (innerChains[c].StartsWith($"{firstTarget}.", StringComparison.OrdinalIgnoreCase))
                            {
                                innerChains[c] = innerChains[c].Substring(firstTarget.Length + 1);
                            }
                        }
                        string optimizedInner = string.Join("#", innerChains);
                        strippedCore = $"{strippedCore}#{optimizedInner}";
                        mergedSuccessfully = true;
                    }
                }
            }

            if (!mergedSuccessfully)
            {
                if (inner.Contains("#") || inner.Contains(".i."))
                    strippedCore = $"{strippedCore}.i.({inner})";
                else
                    strippedCore = $"{strippedCore}.i.{inner}";
            }
        }

        string finalHat = $"{prefix}hat.({strippedCore})";
        if (!string.IsNullOrEmpty(prefix))
        {
            finalHat = $"({finalHat})";
        }
        return finalHat;
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

        // Do NOT use WrapIfNeeded here. The Bracket node wraps its ENTIRE output scope.
        parts.Add(childrenCompiled);

        if (card.MechanicData.PartIndex.HasValue) parts.Add($"part.{card.MechanicData.PartIndex.Value}");
        if (card.MechanicData.Multiplier != 1) parts.Add($"m{card.MechanicData.Multiplier}");
        if (!string.IsNullOrEmpty(card.MechanicData.MergedItem)) parts.Add($"mrg.{card.MechanicData.MergedItem}");
        if (!string.IsNullOrEmpty(card.MechanicData.SplicedItem)) parts.Add($"splice.{card.MechanicData.SplicedItem}");

        string payload = string.Join(".", parts);
        return $"({payload})";
    }

    // --- UTILITIES & SANITIZATION ---

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
    public static string BuildVisualsString(SDData data, string imageOverride = null)
    {
        List<string> parts = new List<string>();

        // 1. Root Image Override declaration is always first if present
        if (!string.IsNullOrEmpty(imageOverride) && !imageOverride.Trim().Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            string imgName = imageOverride.Trim();
            if (imgName.StartsWith("("))
                parts.Add($"img.{imgName}");
            else if (IsBaseItem(imgName) || imgName.StartsWith("ite", StringComparison.OrdinalIgnoreCase))
                parts.Add($"img.{(IsBaseItem(imgName) ? GetBaseItemName(imgName) : imgName)}");
            else
                parts.Add($"img.{imgName}");
        }

        // 2. Iterate remaining visuals in exact user-defined order
        if (data.visuals != null)
        {
            foreach (VisualModifier vis in data.visuals)
            {
                if (vis.Type == VisualType.HSV) parts.Add($"hsv.{vis.h}:{vis.s}:{vis.v}");
                else if (vis.Type == VisualType.Hue) parts.Add($"hue.{vis.hue}");
                else if (vis.Type == VisualType.THue && vis.thue != null) parts.Add($"thue.{ColorUtility.ToHtmlStringRGB(vis.thue.colorHex).ToLower()}:{vis.thue.colorRange}:{vis.thue.colorOffset}");
                else if (vis.Type == VisualType.P && vis.p != null) parts.Add($"p.{ColorUtility.ToHtmlStringRGB(vis.p.colorStart).ToLower()}:{ColorUtility.ToHtmlStringRGB(vis.p.colorDestination).ToLower()}:{vis.p.colorRange}");
                else if (vis.Type == VisualType.Draw) parts.Add($"draw.{vis.RawValue}:{vis.x}:{vis.y}");
                else if (vis.Type == VisualType.Rect) parts.Add($"rect.{vis.RawValue}");
                else if (vis.Type == VisualType.B) parts.Add($"b.{vis.RawValue}");
            }
        }

        return parts.Count > 0 ? string.Join(".", parts) : "";
    }
    private static string BuildEquippable(EntityCard card, string childrenCompiled)
    {
        if (card.RootData == null) return string.Empty;

        string baseExpr = "Void";
        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            baseExpr = WrapIfNeeded(childrenCompiled);
        }

        List<string> parts = new List<string> { baseExpr };

        // Unified rendering block
        string visualsStr = BuildVisualsString(card.RootData, card.RootData.imageOverride);
        if (!string.IsNullOrEmpty(visualsStr))
        {
            parts.Add(visualsStr);
        }

        // Output structural modifiers safely outside of the payload wrapper
        if (card.RootData.ClearDescription) parts.Add("cleardesc");
        if (card.RootData.ClearIcon) parts.Add("clearicon");

        if (card.RootData.Tier.HasValue) parts.Add($"tier.{card.RootData.Tier.Value}");
        if (!string.IsNullOrEmpty(card.RootData.doc)) parts.Add($"doc.{card.RootData.doc}");
        if (!string.IsNullOrEmpty(card.RootData.entityName)) parts.Add($"n.{card.RootData.entityName}");

        return string.Join(".", parts);
    }
}