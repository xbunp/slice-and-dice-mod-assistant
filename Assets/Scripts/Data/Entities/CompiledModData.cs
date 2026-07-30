using System;
using System.Collections.Generic;
using System.Text;

public class CompiledModData
{
    // Pool Lists
    public List<string> monsterPool = new();
    public List<string> heroPool = new();
    public List<string> itemPool = new();

    // Clear Flags (On by default)
    public bool clearMonsterPool = true;
    public bool clearHeroPool = true;
    public bool clearItemPool = true;

    // Compiled Output
    public string compiledMod = string.Empty;

    // Limits & Dummies
    private const int MaxChunkLength = 4000;
    private const string DummyMonster = "bee";
    private const string DummyHero = "fey";
    private const string DummyItem = "can";

    /// <summary>
    /// Parses bracketed heroes from a raw text block, updates heroPool, compiles, and outputs to Console.
    /// </summary>
    public void ImportHeroes(string heroString)
    {
        heroPool.Clear();

        int depth = 0;
        int startIndex = -1;

        // Parse outer bracketed hero objects, ignoring surrounding junk text
        for (int i = 0; i < heroString.Length; i++)
        {
            char c = heroString[i];

            if (c == '(')
            {
                if (depth == 0) startIndex = i; // Mark start of top-level hero
                depth++;
            }
            else if (c == ')')
            {
                if (depth > 0)
                {
                    depth--;
                    if (depth == 0 && startIndex != -1)
                    {
                        // Extracted complete top-level hero string
                        heroPool.Add(heroString.Substring(startIndex, i - startIndex + 1));
                        startIndex = -1;
                    }
                }
            }
        }

        // Immediately compile and output
        Compile();
        Console.WriteLine(compiledMod);
    }

    /// <summary>
    /// Builds the final Slice & Dice textmod string by combining separate modifier objects.
    /// </summary>
    public void Compile()
    {
        List<string> modObjects = new List<string>();

        // 1. Build the Clear Pools Object
        List<string> clearParts = new List<string>();
        if (clearMonsterPool) clearParts.Add($"(monsterpool.{DummyMonster}.part.0)");
        if (clearHeroPool) clearParts.Add($"(heropool.{DummyHero}.part.0)");
        if (clearItemPool) clearParts.Add($"(itempool.{DummyItem}.part.0)");

        if (clearParts.Count > 0)
        {
            // Chains the clears together with '&' and adds the Hidden/mn tags
            modObjects.Add(string.Join("&", clearParts) + "&Hidden.mn.Clear Pools");
        }

        // 2. Build the Addition (part.1) Objects for each pool
        modObjects.AddRange(GetPoolChunks("monster", monsterPool, "Monsters"));
        modObjects.AddRange(GetPoolChunks("hero", heroPool, "Heroes"));
        modObjects.AddRange(GetPoolChunks("item", itemPool, "Items"));

        // 3. Assemble final string (Always starts with '=', separated by ',')
        if (modObjects.Count > 0)
        {
            compiledMod = "=" + string.Join(",", modObjects) + ",";
        }
        else
        {
            compiledMod = "="; // Fallback if literally nothing is flagged/added
        }
    }

    /// <summary>
    /// Combines items into chunked modifier objects, respecting the 4000 character limit.
    /// </summary>
    private List<string> GetPoolChunks(string poolType, List<string> items, string labelPrefix)
    {
        List<string> chunks = new List<string>();
        if (items.Count == 0) return chunks;

        StringBuilder currentChunk = new StringBuilder();
        int chunkIndex = 1;

        foreach (string item in items)
        {
            string separator = currentChunk.Length == 0 ? "" : "+";

            // If adding this exceeds 4k chars, finalize current chunk and start a new one
            if (currentChunk.Length + separator.Length + item.Length > MaxChunkLength)
            {
                chunks.Add($"{poolType}pool.{currentChunk}.mn.{labelPrefix} {chunkIndex}.part.1");

                currentChunk.Clear();
                chunkIndex++;
                separator = ""; // Reset separator for the first item of the new chunk
            }

            currentChunk.Append(separator).Append(item);
        }

        // Add the final remaining chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add($"{poolType}pool.{currentChunk}.mn.{labelPrefix} {chunkIndex}.part.1");
        }

        return chunks;
    }
}