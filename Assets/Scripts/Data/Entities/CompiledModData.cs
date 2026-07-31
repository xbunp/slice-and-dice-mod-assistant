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
        OutputMod();
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