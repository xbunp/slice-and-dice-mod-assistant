using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class CompiledModData
{
    // Filename
    public string modFileName = "MyTextMod";

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
    /// Parses bracketed heroes from a raw text block, protecting trailing tags like .i.(...)
    /// by using depth checking combined with smart lookahead token validation.
    /// </summary>
    public void ImportHeroes(string heroString)
    {
        heroPool.Clear();
        if (string.IsNullOrWhiteSpace(heroString)) return;

        int depth = 0;
        bool isCapturing = false;
        StringBuilder currentHero = new StringBuilder();

        for (int i = 0; i < heroString.Length; i++)
        {
            char c = heroString[i];

            if (c == '(')
            {
                if (depth == 0)
                {
                    // We are at root level. Look ahead to see if this begins a NEW valid hero block
                    string nextToken = GetNextToken(heroString, i + 1);
                    if (IsRootHeroIdentifier(nextToken))
                    {
                        // Found a new hero! Flush the previous one if we were capturing
                        if (isCapturing && currentHero.Length > 0)
                        {
                            FlushCurrentHero(currentHero);
                            currentHero.Clear();
                        }
                        isCapturing = true; // Start capturing the new hero
                    }
                }
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth < 0) depth = 0; // Guard against malformed unbalanced strings
            }

            // Only append characters if we have successfully found at least one hero start point.
            if (isCapturing)
            {
                // Strip raw newlines/returns from the string to keep the compiled mod clean
                if (c != '\n' && c != '\r')
                {
                    currentHero.Append(c);
                }
            }
        }

        // Flush whatever is remaining in the buffer at the end of the string
        if (isCapturing && currentHero.Length > 0)
        {
            FlushCurrentHero(currentHero);
        }

        // Immediately compile and output
        Compile();
        OutputMod();
    }

    /// <summary>
    /// Safely packages the hero string, automatically stripping any unbracketed trailing junk text.
    /// </summary>
    private void FlushCurrentHero(StringBuilder sb)
    {
        string rawHero = sb.ToString();

        // Find the absolute last closing bracket in our captured string.
        // This ensures trailing garbage like ", " or "junk text" is severed, 
        // while perfectly preserving trailing chained tags like ".i.(learn.Mend)"
        int lastBracket = rawHero.LastIndexOf(')');

        if (lastBracket >= 0)
        {
            string cleanHero = rawHero.Substring(0, lastBracket + 1).Trim();
            if (!string.IsNullOrEmpty(cleanHero))
            {
                heroPool.Add(cleanHero);
            }
        }
    }

    /// <summary>
    /// Extracts the word immediately following a bracket to evaluate its identity.
    /// </summary>
    private string GetNextToken(string text, int startIndex)
    {
        StringBuilder token = new StringBuilder();
        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];
            // Slice & Dice object headers end at the first period.
            if (c == '.' || c == ')' || c == '(' || char.IsWhiteSpace(c)) break;
            token.Append(c);
        }
        return token.ToString();
    }

    /// <summary>
    /// Evaluates if the token is a valid starting name for a Hero object.
    /// </summary>
    private bool IsRootHeroIdentifier(string token)
    {
        // Custom crafted heroes always start with 'replica'
        if (string.Equals(token, "replica", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // Otherwise, check against your global list of valid heroes
        if (System.Enum.TryParse(token, true, out HeroType result))
        {
            if (result != HeroType.None) return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the final Slice & Dice textmod string by combining separate modifier objects with line breaks.
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

        // 3. Assemble final string (Separated by comma + newline)
        if (modObjects.Count > 0)
        {
            compiledMod = "=" + string.Join(",\n", modObjects) + ",\n";
        }
        else
        {
            compiledMod = "=";
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

    /// <summary>
    /// Logs the raw compiled mod to Unity's console and safely saves it to a unique file.
    /// </summary>
    public void OutputMod()
    {
        // 1. Pure log to Unity Console for easy copy-pasting
        Debug.Log(compiledMod);

        // 2. Safely output to file without overwriting
        try
        {
        #if UNITY_EDITOR
            string targetDirectory = Application.dataPath; // Saves in /Assets
        #else
        string targetDirectory = Application.persistentDataPath;
        #endif

            // Generate safe unique path (e.g. NewTextMod (1).txt)
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, modFileName);

            File.WriteAllText(uniqueFilePath, compiledMod);

        #if UNITY_EDITOR
            // Refresh Unity's file database so it sees the new file immediately
            UnityEditor.AssetDatabase.Refresh();

            // Convert full path to an "Assets/..." relative path
            string relativePath = "Assets" + uniqueFilePath.Substring(Application.dataPath.Length);
            UnityEngine.Object fileContext = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);

            // Passing 'fileContext' as the 2nd parameter makes clicking the console log jump directly to the file!
            Debug.Log($"[CompiledModData] Saved file safely to: {Path.GetFileName(uniqueFilePath)}", fileContext);
        #else
        Debug.Log($"[CompiledModData] Saved file safely to: {Path.GetFileName(uniqueFilePath)}");
        #endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to write mod file: {e.Message}");
        }
    }

    /// <summary>
    /// Ensures a file isn't overwritten by appending (1), (2), etc., if it already exists.
    /// </summary>
    private string GetUniqueFilePath(string directory, string baseFileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        string extension = Path.GetExtension(baseFileName);

        // Default to .txt if no extension was provided in the UI
        if (string.IsNullOrEmpty(extension)) extension = ".txt";

        string filePath = Path.Combine(directory, $"{nameWithoutExt}{extension}");
        int counter = 1;

        // Increment filename if a match is found
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
            counter++;
        }

        return filePath;
    }
}