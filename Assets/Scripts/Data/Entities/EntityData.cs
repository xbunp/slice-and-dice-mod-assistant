using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public abstract class EntityData : SDData, IPayloadContainer
{
    [Header("Core Shared Info")]
    public int hp = 0;

    [Header("Colors (Mutually Exclusive)")]
    public int h = 0, s = 0, v = 0, hue = 0;
    public string hsl;

    [Header("Shared Extended Modifiers")]
    public List<string> items = new List<string>();
    public List<string> traits = new List<string>();
    public List<string> blessings = new List<string>();
    public List<string> curses = new List<string>();
    public List<string> baseAbilityData = new List<string>();

    public string p, b, rect, draw, thue, doc;
    public DiceSideData[] diceSides = new DiceSideData[6];

    // Interface mappings
    public List<string> BaseItems => items;
    public List<string> Traits => traits;
    public List<string> Curses => curses;
    public List<string> Blessings => blessings;
    public List<string> BaseAbilities => baseAbilityData;
    public List<CustomPayload> CustomPayloads => customPayloads;

    protected void InitializeDiceFaces()
    {
        for (int i = 0; i < diceSides.Length; i++) diceSides[i] = new DiceSideData();
    }

    protected void AppendColorModifier(StringBuilder sb)
    {
        if (h != 0 || s != 0 || v != 0) sb.Append($".hsv.{h}:{s}:{v}");
        else if (hue != 0) sb.Append($".hue.{hue}");
        else if (!string.IsNullOrEmpty(hsl)) sb.Append($".hsl.{hsl}");
    }

    protected void AppendDiceSides(StringBuilder sb)
    {
        bool allZero = true;
        for (int i = 0; i < 6; i++)
        {
            if (diceSides[i] != null && diceSides[i].effectID != 0) { allZero = false; break; }
        }

        sb.Append(".sd.");
        if (allZero) { sb.Append("0"); return; }

        for (int i = 0; i < 6; i++)
        {
            var side = diceSides[i];
            if (side == null || side.effectID == 0) sb.Append("0");
            else sb.Append($"{side.effectID}-{side.pips}");
            if (i < 5) sb.Append(":");
        }
    }

    protected string BuildFaceModifiers(bool allowFacade)
    {
        StringBuilder modSb = new StringBuilder();
        var groupedModifiers = new Dictionary<string, int>();

        for (int i = 0; i < 6; i++)
        {
            var face = diceSides[i];
            List<string> chunks = new List<string>();

            foreach (var kw in face.keywords) if (!string.IsNullOrWhiteSpace(kw)) chunks.Add($"k.{kw.Trim().ToLower()}");

            if (allowFacade && !string.IsNullOrWhiteSpace(face.facadeID))
            {
                string facStr = $"facade.{face.facadeID.Trim()}";
                if (!string.IsNullOrWhiteSpace(face.facadeColor)) facStr += $":{face.facadeColor}";
                chunks.Add(facStr);
            }

            if (chunks.Count > 0)
            {
                string modString = string.Join("#", chunks);
                int faceMask = 1 << i;
                if (groupedModifiers.ContainsKey(modString)) groupedModifiers[modString] |= faceMask;
                else groupedModifiers[modString] = faceMask;
            }
        }

        foreach (var kvp in groupedModifiers)
        {
            string modString = kvp.Key;
            List<string> optimalAliases = DiceTargetHelper.GetBestAliasCombination(kvp.Value);
            foreach (string alias in optimalAliases) modSb.Append($".i.{alias}.{modString}");
        }

        return modSb.ToString();
    }

    protected static string FormatName(string name) => name ?? "";
}
