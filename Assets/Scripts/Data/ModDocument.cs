using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ModDocument
{
    public List<ModBlock> Blocks = new List<ModBlock>();

    public void Parse(string rawModText)
    {
        Blocks.Clear();
        if (string.IsNullOrWhiteSpace(rawModText)) return;

        UnityEngine.Debug.Log($"[ModDocument] Starting Top-Level Parse on length: {rawModText.Length}");

        // 1. Clean formatting noise
        string cleanMod = rawModText.Replace("\r", "").Replace("\n", "").Trim();
        if (cleanMod.StartsWith("=")) cleanMod = cleanMod.Substring(1).Trim();

        // 2. The Ultimate Split: Separates the entire mod into individual directives
        List<string> blockStrings = StaticBranchTracing.TopLevelSplit(cleanMod, ',');

        foreach (string blockStr in blockStrings)
        {
            if (string.IsNullOrWhiteSpace(blockStr)) continue;

            ModBlock block = new ModBlock();
            block.Parse(blockStr);
            Blocks.Add(block);
        }

        UnityEngine.Debug.Log($"[ModDocument] Unrolled {Blocks.Count} Top-Level Blocks.");
    }

    public void DebugDocument()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("==========================================");
        sb.AppendLine("         MOD DOCUMENT UNPACKED            ");
        sb.AppendLine("==========================================");
        UnityEngine.Debug.Log(sb.ToString());

        for (int i = 0; i < Blocks.Count; i++)
        {
            Blocks[i].DebugBlock($"[{i}] ");
        }
    }
}

[System.Serializable]
public class ModBlock
{
    public string Floor = "";
    public List<string> Tags = new List<string>();
    public string BlockType = "";
    public string SubType = ""; // E.g., 'om4' in 'ch.om4'

    public string RawUnparsedData = ""; // Anything the peeler doesn't understand gets saved here

    // Unrolled Payloads (The actual data classes)
    public List<HeroData> Heroes = new List<HeroData>();
    public List<MonsterData> Monsters = new List<MonsterData>();
    public List<ItemData> Items = new List<ItemData>();
    public List<ModifierData> Modifiers = new List<ModifierData>();
    public List<ModBlock> NestedBlocks = new List<ModBlock>(); // For phases/choices that trigger other blocks

    public void Parse(string raw)
    {
        string core = raw.Trim();
        core = StripOuterParens(core);

        // 1. Peel '&' Tags (hidden, temporary)
        List<string> ampSplit = StaticBranchTracing.TopLevelSplit(core, '&');
        core = ampSplit.Last();
        for (int i = 0; i < ampSplit.Count - 1; i++) Tags.Add(ampSplit[i]);

        // 2. Peel Floor Prefix
        List<string> tokens = StaticBranchTracing.TopLevelSplit(core, '.');
        int tIdx = 0;

        if (tokens.Count > 0 && IsFloorToken(tokens[tIdx]))
        {
            Floor = tokens[tIdx];
            tIdx++;
            // Handle fractional floors like "e2.1"
            if (Floor.StartsWith("e", StringComparison.OrdinalIgnoreCase) && tIdx < tokens.Count && int.TryParse(tokens[tIdx], out _))
            {
                Floor += "." + tokens[tIdx];
                tIdx++;
            }
        }

        if (tIdx >= tokens.Count) return;

        // 3. Identify Block Type
        BlockType = tokens[tIdx].ToLower();
        tIdx++;

        // Handle 'replace.heropool' edge case
        if (BlockType == "replace" && tIdx < tokens.Count && tokens[tIdx].ToLower() == "heropool")
        {
            BlockType = "replace.heropool";
            tIdx++;
        }

        // Handle sub-types for Choosables and Phases (e.g., ch.om4, ph.bY)
        if ((BlockType == "ch" || BlockType == "ph" || BlockType == "phi" || BlockType == "phmp" || BlockType.StartsWith("!")) && tIdx < tokens.Count)
        {
            SubType = tokens[tIdx];
            tIdx++;
        }

        string payload = string.Join(".", tokens.Skip(tIdx));

        // 4. Route Payload to the correct TypeData class!
        RoutePayload(payload);
    }

    private void RoutePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;

        // --- POOLS & FIGHTS (+ delimited) ---
        if (BlockType == "heropool" || BlockType == "party" || BlockType == "replace.heropool")
        {
            List<string> elements = StaticBranchTracing.TopLevelSplit(payload, '+');
            foreach (var e in elements) { var h = new HeroData(); h.Parse(e); Heroes.Add(h); }
        }
        else if (BlockType == "monsterpool" || BlockType == "fight" || BlockType == "add")
        {
            List<string> elements = StaticBranchTracing.TopLevelSplit(payload, '+');
            foreach (var e in elements) { var m = new MonsterData(); m.Parse(e); Monsters.Add(m); }
        }
        else if (BlockType == "itempool")
        {
            List<string> elements = StaticBranchTracing.TopLevelSplit(payload, '+');
            foreach (var e in elements) { var i = new ItemData(); i.Parse(e); Items.Add(i); }
        }

        // --- CHOOSABLES & PHASES ---
        else if (BlockType == "ch")
        {
            if (SubType.StartsWith("o", StringComparison.OrdinalIgnoreCase))
            {
                // It's an OR list. Split by @4 or @3 and parse each as a nested block.
                string delimiter = payload.Contains("@4") ? "@4" : "@3";
                List<string> options = StaticBranchTracing.TopLevelSplit(payload, delimiter[0]);

                foreach (string opt in options)
                {
                    string cleanOpt = opt;
                    if (cleanOpt.StartsWith("4") || cleanOpt.StartsWith("3")) cleanOpt = cleanOpt.Substring(1); // strip trailing delimiter number
                    ModBlock nested = new ModBlock(); nested.Parse(cleanOpt); NestedBlocks.Add(nested);
                }
            }
            else if (SubType == "m" || SubType == "i" || SubType.StartsWith("r"))
            {
                // Choosable modifiers/items. Hand them to ModifierData.
                ModifierData mod = new ModifierData(); mod.Parse(payload); Modifiers.Add(mod);
            }
            else
            {
                // Fallback for nested fight injections (e.g. ch.om4.fight.(...))
                ModBlock nested = new ModBlock(); nested.Parse(payload); NestedBlocks.Add(nested);
            }
        }
        else if (BlockType.StartsWith("ph"))
        {
            if (SubType.StartsWith("b", StringComparison.OrdinalIgnoreCase))
            {
                // Boolean phases usually have '!m' blocks hidden in their false/true branches
                List<string> branches = StaticBranchTracing.TopLevelSplit(payload, ';');
                foreach (string branch in branches)
                {
                    if (branch.Contains("!m"))
                    {
                        int mIdx = branch.IndexOf("!m");
                        string hiddenPayload = branch.Substring(mIdx + 2); // Strip '!m'
                        ModBlock nested = new ModBlock(); nested.Parse(hiddenPayload); NestedBlocks.Add(nested);
                    }
                }
            }
            RawUnparsedData = payload; // Store the raw phase logic string
        }

        // --- GENERAL MODIFIERS ---
        else
        {
            // If it's anything else (e.g., "allitem.Dead Crow", "delevel", "horde")
            // We pass it to the ModifierData bridge!
            string fullModString = BlockType;
            if (!string.IsNullOrEmpty(SubType)) fullModString += "." + SubType;
            if (!string.IsNullOrEmpty(payload)) fullModString += "." + payload;

            ModifierData mod = new ModifierData();
            mod.Parse(fullModString);
            Modifiers.Add(mod);
        }
    }

    private bool IsFloorToken(string token)
    {
        if (int.TryParse(token, out _)) return true;
        if (token.Contains("-")) return true;
        if (token.StartsWith("e", StringComparison.OrdinalIgnoreCase) && token.Length > 1 && char.IsDigit(token[1])) return true;
        return false;
    }

    private string StripOuterParens(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string t = text.Trim();
        while (t.StartsWith("(") && t.EndsWith(")"))
        {
            int depth = 0; bool matching = true;
            for (int k = 0; k < t.Length - 1; k++)
            {
                if (t[k] == '(') depth++; else if (t[k] == ')') depth--;
                if (depth == 0) { matching = false; break; }
            }
            if (matching) t = t.Substring(1, t.Length - 2).Trim();
            else break;
        }
        return t;
    }

    public void DebugBlock(string prefix = "")
    {
        string floorStr = string.IsNullOrEmpty(Floor) ? "Global" : $"Floor {Floor}";
        string tagStr = Tags.Count > 0 ? $" | Tags: [{string.Join(", ", Tags)}]" : "";
        string subStr = string.IsNullOrEmpty(SubType) ? "" : $".{SubType}";

        UnityEngine.Debug.Log($"{prefix}Type: {BlockType.ToUpper()}{subStr} | {floorStr}{tagStr}");

        if (Heroes.Count > 0)
        {
            UnityEngine.Debug.Log($"    -> Unpacked {Heroes.Count} Heroes:");
            foreach (var h in Heroes) h.DebugContentsToConsoleCompact("        ");
        }
        if (Monsters.Count > 0)
        {
            UnityEngine.Debug.Log($"    -> Unpacked {Monsters.Count} Monsters:");
            foreach (var m in Monsters) m.DebugContentsToConsoleCompact("        ");
        }
        if (Items.Count > 0)
        {
            UnityEngine.Debug.Log($"    -> Unpacked {Items.Count} Items:");
            foreach (var i in Items) i.DebugContentsToConsole("        ");
        }
        if (Modifiers.Count > 0)
        {
            UnityEngine.Debug.Log($"    -> Unpacked {Modifiers.Count} Modifiers:");
            foreach (var m in Modifiers) m.DebugContentsToConsole("        ");
        }
        if (NestedBlocks.Count > 0)
        {
            UnityEngine.Debug.Log($"    -> Unpacked {NestedBlocks.Count} Nested Sub-Blocks:");
            for (int i = 0; i < NestedBlocks.Count; i++) NestedBlocks[i].DebugBlock($"        [{i}] ");
        }
        if (!string.IsNullOrEmpty(RawUnparsedData))
        {
            UnityEngine.Debug.Log($"    -> Raw Phase/Config Data: {RawUnparsedData}");
        }
    }
}