using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ==========================================
// BASE COMPONENT (Reference / Template)
// ==========================================
[System.Serializable]
public class BaseComponent
{
    public string TemplateName { get; set; } = string.Empty;
    public int? PartIndex { get; set; }
    public string MergedItem { get; set; } = string.Empty;
    public string SplicedItem { get; set; } = string.Empty;
    public int Multiplier { get; set; } = 1;

    public string Export()
    {
        List<string> parts = new List<string> { TemplateName };
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) parts.Add($"m{Multiplier}");
        if (!string.IsNullOrEmpty(MergedItem)) parts.Add($"mrg.{MergedItem}");
        if (!string.IsNullOrEmpty(SplicedItem)) parts.Add($"splice.{SplicedItem}");
        return string.Join(".", parts);
    }
}