using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class ItemSyntaxCompiler
{
    /// <summary>
    /// Compiles a list of sibling cards, automatically injecting dots or operators where needed.
    /// </summary>
    public static string CompileZone(IEnumerable<EntityCard> cards)
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

                // If neither node is an operator, and the string doesn't already have a natural joiner, inject a dot.
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

        return CleanupSyntax(sb.ToString());
    }

    /// <summary>
    /// Compiles a single card and recursively resolves its children.
    /// </summary>
    public static string CompileCard(EntityCard card)
    {
        if (card == null) return string.Empty;

        // Recursively compile children first
        string childrenCompiled = string.Empty;
        if (card.PayloadPort != null && card.PayloadPort.Entrants.Count > 0)
        {
            childrenCompiled = CompileZone(card.PayloadPort.Entrants.Cast<EntityCard>());
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
                return BuildBracket(childrenCompiled);
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
            // Unconditionally wrap the entire inner payload in parentheses
            baseExpr = $"({childrenCompiled})";

            string firstToken = childrenCompiled.Split(new char[] { '.', '#', '(', ')', ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            baseItemName = firstToken ?? "Custom";
        }

        bool hasClearModifiers = card.RootData.ClearDescription || card.RootData.ClearIcon;
        if (hasClearModifiers)
        {
            string descMod = card.RootData.ClearDescription ? "#cleardesc" : "";
            string iconMod = card.RootData.ClearIcon ? "#clearicon" : "";
            baseExpr = $"({baseExpr}{descMod}{iconMod})";
        }

        List<string> parts = new List<string> { baseExpr };

        // FIX: Ensure "None" (case-insensitive) is ignored as a valid image override
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
            // Fallback to only printing HSV if an HSV shift exists with no image override
            parts.Add(FormatHsv(card.RootData.HsvShift.Value));
        }

        if (card.RootData.Tier.HasValue) parts.Add($"tier.{card.RootData.Tier.Value}");
        if (!string.IsNullOrEmpty(card.RootData.DocumentedDescription)) parts.Add($"doc.{card.RootData.DocumentedDescription}");
        if (!string.IsNullOrEmpty(card.RootData.entityName)) parts.Add($"n.{card.RootData.entityName}");

        return string.Join(".", parts);
    }

    private static string BuildHat(EntityCard card, string childrenCompiled)
    {
        if (!(card.MechanicData.PayloadData is HeroData heroData)) return "";

        // FIX 2: Safely filter out empty strings in the targets list to guarantee fallback to 'left'
        var validTargets = card.MechanicData.Targets?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        string targets = (validTargets != null && validTargets.Count > 0) ? string.Join(".", validTargets) : "left";

        string prefix = targets.Equals("all", StringComparison.OrdinalIgnoreCase) ? "" : $"{targets}.";

        string hatCore = $"hat.({HatNodeDef.GetHatDiceString(heroData)})";

        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            string inner = childrenCompiled;
            if (inner.StartsWith(".")) inner = inner.Substring(1);

            // Already explicitly bracketed by .i.() rule
            return $"{prefix}{hatCore}.i.({inner})";
        }

        return $"{prefix}{hatCore}";
    }

    private static string BuildBaseItem(EntityCard card, string childrenCompiled)
    {
        string internalPayload = card.MechanicData.PayloadString ?? "";
        if (string.IsNullOrWhiteSpace(internalPayload)) return childrenCompiled;

        if (!string.IsNullOrWhiteSpace(childrenCompiled))
        {
            string inner = childrenCompiled;
            string op = "";

            // FIX 3: If a child begins with an operator, it must sit OUTSIDE the bracket 
            // e.g., turning "Sword" + "#Shield" into (Sword)#(Shield) instead of (Sword).(#Shield)
            if (inner.StartsWith("#")) { op = "#"; inner = inner.Substring(1); }
            else if (inner.StartsWith(".mrg.")) { op = ".mrg."; inner = inner.Substring(5); }
            else if (inner.StartsWith(".splice.")) { op = ".splice."; inner = inner.Substring(8); }
            else if (inner.StartsWith(".i.")) { op = ".i."; inner = inner.Substring(3); }
            else if (inner.StartsWith(".")) { op = "."; inner = inner.Substring(1); }
            else { op = "."; } // Fallback generic attachment

            // Bracket the internal payload, attach the operator, then bracket the inner payload
            return $"({internalPayload}){op}({inner})";
        }

        return internalPayload;
    }

    private static string BuildBracket(string childrenCompiled)
    {
        if (string.IsNullOrWhiteSpace(childrenCompiled)) return string.Empty;
        return $"({childrenCompiled})";
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