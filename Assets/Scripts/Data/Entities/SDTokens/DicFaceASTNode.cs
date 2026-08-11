// ============================================================================================
// DICE FACE AST NODE
// ============================================================================================

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// AST node representing an individual dice face side.
/// Assembles intrinsic keywords, payloads (stickers/casts/enchants/eggs), facades, sidesc, and stasis.
/// </summary>
[System.Serializable]
public class DiceFaceNode : SDNode
{
    public int FaceIndex { get; set; } = 0; // 0=Left, 1=Mid, 2=Top, 3=Bot, 4=Right, 5=Rightmost
    public DiceSideData FaceData { get; set; } = new DiceSideData();

    public DiceFaceNode(int faceIndex = 0, DiceSideData faceData = null)
    {
        FaceIndex = faceIndex;
        FaceData = faceData ?? new DiceSideData();
    }

    public List<string> BuildFaceChunks(bool includeInlineFacades = true)
    {
        List<string> chunks = new List<string>();
        if (FaceData == null) return chunks;

        // 1. Permissive keyword
        if (FaceData.keywords.Any(kw => string.Equals(kw?.Trim(), "permissive", StringComparison.OrdinalIgnoreCase)))
            chunks.Add("k.permissive");

        // 2. Face Payload (Sticker, Cast, Enchant, Egg)
        ProcessFacePayload(chunks);

        // 3. Intrinsic Face Keywords
        foreach (var kw in FaceData.keywords)
        {
            if (string.IsNullOrWhiteSpace(kw)) continue;
            string clean = kw.Trim();
            string lower = clean.ToLower();

            if (lower != "permissive" && lower != "stasis")
            {
                if (lower == "future") chunks.Add("ritemx.dae9");
                else if (ExternalGameRegistry.IsValidKeyword(clean)) chunks.Add($"k.{lower}");
                else chunks.Add(clean);
            }
        }

        // 4. Facades
        if (includeInlineFacades && !string.IsNullOrWhiteSpace(FaceData.facadeID))
        {
            string facStr = $"facade.{FaceData.facadeID.Trim()}";
            if (!string.IsNullOrWhiteSpace(FaceData.facadeColor))
            {
                string[] hsv = FaceData.facadeColor.Split(':');
                facStr += (hsv.Length >= 3 && hsv[0] == "0" && hsv[1] == "0" && hsv[2] == "0") ? ":0" : $":{FaceData.facadeColor}";
            }
            else facStr += ":0";
            chunks.Add(facStr);
        }

        // 5. Description (sidesc)
        if (!string.IsNullOrWhiteSpace(FaceData.sidesc))
            chunks.Add($"sidesc.{FaceData.sidesc.Trim()}");

        // 6. Side Items
        if (FaceData.sideItems != null)
        {
            foreach (var item in FaceData.sideItems)
            {
                if (item != null) chunks.Add(item.Export());
            }
        }

        // 7. Stasis keyword (MUST BE LAST)
        if (FaceData.keywords.Any(kw => string.Equals(kw?.Trim(), "stasis", StringComparison.OrdinalIgnoreCase)))
            chunks.Add("k.stasis");

        return chunks;
    }

    private void ProcessFacePayload(List<string> chunks)
    {
        if (FaceData.faceType == DiceSideData.DiceFaceType.Base || string.IsNullOrWhiteSpace(FaceData.payload))
            return;

        string payloadStr = FaceData.payload.Trim();

        if (FaceData.faceType == DiceSideData.DiceFaceType.Egg)
        {
            bool hasBlindfold = payloadStr.EndsWith("#blindfold", StringComparison.OrdinalIgnoreCase);
            string cleanSummon = hasBlindfold ? payloadStr.Substring(0, payloadStr.Length - 10) : payloadStr;

            MonsterEntityNode eggMonster = new MonsterEntityNode { BaseMonster = $"egg.{cleanSummon}" };
            if (FaceData.pips >= 2 && FaceData.pips <= 9) eggMonster.XMultiplier = FaceData.pips;

            chunks.Add($"hat.{eggMonster.Export()}");
            if (hasBlindfold) chunks.Add("blindfold");
        }
        else
        {
            string prefix = FaceData.faceType.ToString().ToLower();
            string innerPayloadStr = $"{prefix}.{payloadStr}";

            if (FaceData.payloadTarget.HasValue)
            {
                chunks.Add(PayloadTargetHelper.FormatTargetedPayload(innerPayloadStr, FaceData.payloadTarget.Value, FaceData.togtime));
            }
            else
            {
                if (FaceData.togtime) innerPayloadStr += "#togtime";
                chunks.Add(innerPayloadStr);
            }
        }
    }

    public override string Export()
    {
        var chunks = BuildFaceChunks(true);
        return chunks.Count > 0 ? string.Join("#", chunks) : "";
    }
}

// ============================================================================================
// FACE MODIFIER CLAUSE
// ============================================================================================

/// <summary>
/// AST Clause representing face targeting modifiers (e.g. left.mid.hat.(...) or top.facade.bas1).
/// </summary>
[System.Serializable]
public class FaceModifierClause : SDClause
{
    public override ClauseCategory Category => ClauseCategory.Mechanics;
    public List<DiceTarget> TargetAliases { get; set; } = new List<DiceTarget>();
    public SDNode Payload { get; set; }

    public FaceModifierClause(DiceTarget target = DiceTarget.Left, SDNode payload = null)
    {
        TargetAliases.Add(target);
        Payload = payload;
    }

    public override string Export()
    {
        if (Payload == null) return "";
        string aliases = string.Join(".", TargetAliases.Select(t => t.ToString().ToLower()));
        string payloadStr = Payload.Export();

        if (payloadStr.Contains("{0}"))
            return string.Format(payloadStr, aliases);

        return $"{aliases}.{payloadStr}";
    }
}