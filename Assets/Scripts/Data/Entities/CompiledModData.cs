using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class CompiledModData
{
    public string modFileName = "MyTextMod";
    public bool humanReadable = true;

    public List<string> monsterPool = new();
    public List<string> heroPool = new();
    public List<string> itemPool = new();

    public bool clearMonsterPool = true;
    public bool clearHeroPool = true;
    public bool clearItemPool = true;

    public string compiledMod = string.Empty;

    private const int MaxChunkLength = 4000;
    private const string DummyMonster = "bee";
    private const string DummyHero = "fey";
    private const string DummyItem = "can";

    /// <summary>
    /// Imports items of any format (bracketed, unbracketed, ritemx, keyword-based),
    /// stripping 'i.' prefixes, ignoring plaintext comments/labels, and enclosing them in brackets.
    /// </summary>
    public void ImportItems(string itemString)
    {
        itemPool.Clear();
        if (string.IsNullOrWhiteSpace(itemString)) return;

        string cleanText = StripComments(itemString);
        string[] lines = cleanText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Handle multiple items if pasted on the same line joined by '+'
            List<string> subItems = SplitRootLevel(line, '+');
            foreach (string subItem in subItems)
            {
                ProcessSingleItem(subItem);
            }
        }

        Compile();
        OutputMod();
    }
    private void ProcessSingleItem(string raw)
    {
        string item = raw.Trim();
        if (string.IsNullOrEmpty(item)) return;

        // Strip leading/trailing mod symbols (=, +, ,, etc.)
        item = item.TrimStart('=', ',', '+').Trim();
        if (item.EndsWith(",")) item = item.Substring(0, item.Length - 1).Trim();

        // Validate that this line is actually an item and not a standalone title label
        bool isItem = item.StartsWith("i.", StringComparison.OrdinalIgnoreCase)
                   || item.Contains(".tier.")
                   || item.Contains(".n.")
                   || item.Contains(".img.")
                   || item.Contains(".splice.")
                   || (item.StartsWith("(") && item.Contains("."));

        if (!isItem) return;

        // Strip leading "i." prefix if present
        if (item.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
        {
            item = item.Substring(2).TrimStart();
        }

        // Enforce enclosing in a full pair of brackets if not already fully enclosed
        if (!IsFullyEnclosed(item))
        {
            item = $"({item})";
        }

        itemPool.Add(item);
    }

    /// <summary>
    /// Splits text by a delimiter only when outside of parenthesis depth.
    /// </summary>
    private List<string> SplitRootLevel(string text, char delimiter)
    {
        List<string> results = new List<string>();
        int depth = 0;
        StringBuilder current = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (c == delimiter && depth == 0)
            {
                if (current.Length > 0)
                {
                    results.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            results.Add(current.ToString());
        }

        return results;
    }

    /// <summary>
    /// Captures the item expression and reads past all trailing chained tags 
    /// (.tier.X, .img.X, .n.Name, etc.) until the full item payload completes.
    /// </summary>
    private string ExtractItemEntity(string text, ref int index)
    {
        StringBuilder sb = new StringBuilder();
        int depth = 0;

        // 1. Capture primary bracketed expression
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (c != '\r' && c != '\n')
            {
                sb.Append(c);
            }

            index++;

            if (depth == 0) break;
        }

        // 2. Capture trailing dot/hash tags (e.g. .img.X.tier.Y.n.Item Name)
        while (index < text.Length)
        {
            int peek = index;
            while (peek < text.Length && (text[peek] == ' ' || text[peek] == '\t'))
            {
                peek++;
            }

            if (peek >= text.Length || text[peek] == '\r' || text[peek] == '\n') break;

            // Continue while chained with '.' or '#'
            if (text[peek] == '.' || text[peek] == '#')
            {
                index = peek;

                // Read until next newline or end of segment
                while (index < text.Length && text[index] != '\r' && text[index] != '\n')
                {
                    char c = text[index];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;

                    sb.Append(c);
                    index++;

                    // If depth is 0 and we hit a new line / whitespace followed by a non-chain character, abort
                    if (depth == 0 && index < text.Length && (text[index] == '\r' || text[index] == '\n'))
                    {
                        break;
                    }
                }
            }
            else
            {
                break;
            }
        }

        return sb.ToString().Trim();
    }

    public void ImportMonsters(string monsterString)
    {
        ImportPool(monsterString, monsterPool, IsRootMonsterIdentifier, isItemPool: false);
    }

    public void ImportHeroes(string heroString)
    {
        ImportPool(heroString, heroPool, IsRootHeroIdentifier, isItemPool: false);
    }

    private void ImportPool(string rawText, List<string> targetPool, Func<string, bool> validator, bool isItemPool)
    {
        targetPool.Clear();
        if (string.IsNullOrWhiteSpace(rawText)) return;

        string cleanText = StripComments(rawText);
        int i = 0;

        while (i < cleanText.Length)
        {
            if (char.IsWhiteSpace(cleanText[i]))
            {
                i++;
                continue;
            }

            // Items can optionally start with "i.("
            if (isItemPool && cleanText[i] == 'i' && i + 2 < cleanText.Length && cleanText[i + 1] == '.' && cleanText[i + 2] == '(')
            {
                i += 2; // Advance to '('
            }

            if (cleanText[i] == '(')
            {
                string rootToken = GetLeadingToken(cleanText, i);
                if (validator(rootToken))
                {
                    string fullEntity = ExtractFullEntity(cleanText, ref i);
                    if (!string.IsNullOrEmpty(fullEntity))
                    {
                        // Clean any residual "i." prefix if present
                        if (fullEntity.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
                        {
                            fullEntity = fullEntity.Substring(2).TrimStart();
                        }

                        // Ensure complete enclosing bracket
                        if (!IsFullyEnclosed(fullEntity))
                        {
                            fullEntity = $"({fullEntity})";
                        }

                        targetPool.Add(fullEntity);
                    }
                    continue;
                }
            }

            i++;
        }

        Compile();
        OutputMod();
    }

    private string ExtractFullEntity(string text, ref int index)
    {
        StringBuilder sb = new StringBuilder();
        int depth = 0;

        // 1. Capture primary bracket block
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (c != '\r' && c != '\n')
            {
                sb.Append(c);
            }

            index++;

            if (depth == 0) break;
        }

        // 2. Capture trailing chained modifiers (.tag, #facade, .(nested), etc.)
        while (index < text.Length)
        {
            int peek = index;
            while (peek < text.Length && (text[peek] == ' ' || text[peek] == '\t'))
            {
                peek++;
            }

            if (peek >= text.Length) break;

            // Chain continues if next non-whitespace char is '.' or '#'
            if (text[peek] == '.' || text[peek] == '#')
            {
                index = peek;
                while (index < text.Length)
                {
                    char c = text[index];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;

                    if (c != '\r' && c != '\n')
                    {
                        sb.Append(c);
                    }

                    index++;

                    if (depth == 0 && index < text.Length && char.IsWhiteSpace(text[index]))
                    {
                        break;
                    }
                }
            }
            else
            {
                break;
            }
        }

        return sb.ToString().Trim();
    }

    private string StripComments(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        string noBlock = Regex.Replace(input, @"/\*.*?\*/", "", RegexOptions.Singleline);
        string[] lines = noBlock.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        StringBuilder sb = new StringBuilder();
        foreach (var line in lines)
        {
            int commentIdx = line.IndexOf("//", StringComparison.Ordinal);
            string cleanLine = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
            sb.AppendLine(cleanLine);
        }
        return sb.ToString();
    }

    private bool IsFullyEnclosed(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.StartsWith("(") || !s.EndsWith(")"))
            return false;

        int depth = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0 && i < s.Length - 1)
                    return false;
            }
        }
        return depth == 0;
    }

    private string GetLeadingToken(string text, int startIndex)
    {
        while (startIndex < text.Length && (text[startIndex] == '(' || char.IsWhiteSpace(text[startIndex])))
        {
            startIndex++;
        }

        StringBuilder token = new StringBuilder();
        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '.' || c == ')' || c == '(' || c == ':' || c == '#' || char.IsWhiteSpace(c)) break;
            token.Append(c);
        }
        return token.ToString();
    }

    private bool IsRootMonsterIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        if (string.Equals(token, "replica", StringComparison.OrdinalIgnoreCase)) return true;
        if (token.StartsWith("rmon", StringComparison.OrdinalIgnoreCase)) return true;

        if (Enum.TryParse(token, true, out MonsterType result))
        {
            if (result != MonsterType.None) return true;
        }

        return false;
    }

    private bool IsRootHeroIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        if (string.Equals(token, "replica", StringComparison.OrdinalIgnoreCase)) return true;
        if (token.StartsWith("rhero", StringComparison.OrdinalIgnoreCase)) return true;

        if (Enum.TryParse(token, true, out HeroType result))
        {
            if (result != HeroType.None) return true;
        }

        return false;
    }

    public void Compile()
    {
        List<string> modObjects = new List<string>();

        // 1. Build Pool Clears
        List<string> clearParts = new List<string>();
        if (clearMonsterPool) clearParts.Add($"(monsterpool.{DummyMonster}.part.0)");
        if (clearHeroPool) clearParts.Add($"(heropool.{DummyHero}.part.0)");
        if (clearItemPool) clearParts.Add($"(itempool.{DummyItem}.part.0)");

        if (clearParts.Count > 0)
        {
            string clearObject = string.Join("&", clearParts) + "&Hidden.mn.Clear Pools";
            modObjects.Add(clearObject);
        }

        // 2. Build Addition Pool Chunks
        modObjects.AddRange(GetPoolChunks("monster", monsterPool, "Monster Pool"));
        modObjects.AddRange(GetPoolChunks("hero", heroPool, "Hero Pool"));
        modObjects.AddRange(GetPoolChunks("item", itemPool, "Item Pool"));

        // 3. Assemble Output
        if (modObjects.Count > 0)
        {
            compiledMod = humanReadable
                ? "=" + string.Join(",\n\n", modObjects) + ",\n"
                : "=" + string.Join(",\n", modObjects) + ",\n";
        }
        else
        {
            compiledMod = "=";
        }
    }

    private List<string> GetPoolChunks(string poolType, List<string> items, string labelPrefix)
    {
        List<string> chunks = new List<string>();
        if (items.Count == 0) return chunks;

        StringBuilder currentItemsChunk = new StringBuilder();
        int chunkIndex = 1;

        foreach (string item in items)
        {
            string separator = currentItemsChunk.Length == 0 ? "" : (humanReadable ? "\n+\n" : "+");

            if (currentItemsChunk.Length + separator.Length + item.Length > MaxChunkLength)
            {
                chunks.Add(FormatPoolString(poolType, currentItemsChunk.ToString(), labelPrefix, chunkIndex));
                currentItemsChunk.Clear();
                chunkIndex++;
                separator = "";
            }

            currentItemsChunk.Append(separator).Append(item);
        }

        if (currentItemsChunk.Length > 0)
        {
            chunks.Add(FormatPoolString(poolType, currentItemsChunk.ToString(), labelPrefix, chunkIndex));
        }

        return chunks;
    }

    private string FormatPoolString(string poolType, string content, string labelPrefix, int chunkIndex)
    {
        string label = chunkIndex > 1 ? $"{labelPrefix} {chunkIndex}" : labelPrefix;

        return humanReadable
            ? $"{poolType}pool.\n{content}\n.part.1\n&Hidden.mn.{label}"
            : $"{poolType}pool.{content}.part.1&Hidden.mn.{label}";
    }

    public void OutputMod()
    {
        Debug.Log(compiledMod);

        try
        {
#if UNITY_EDITOR
            string targetDirectory = Application.dataPath;
#else
            string targetDirectory = Application.persistentDataPath;
#endif
            string uniqueFilePath = GetUniqueFilePath(targetDirectory, modFileName);
            File.WriteAllText(uniqueFilePath, compiledMod);

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            string relativePath = "Assets" + uniqueFilePath.Substring(Application.dataPath.Length);
            UnityEngine.Object fileContext = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);
            Debug.Log($"[CompiledModData] Saved file safely to: {Path.GetFileName(uniqueFilePath)}", fileContext);
#else
            Debug.Log($"[CompiledModData] Saved file safely to: {Path.GetFileName(uniqueFilePath)}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to write mod file: {e.Message}");
        }
    }

    private string GetUniqueFilePath(string directory, string baseFileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
        string extension = Path.GetExtension(baseFileName);

        if (string.IsNullOrEmpty(extension)) extension = ".txt";

        string filePath = Path.Combine(directory, $"{nameWithoutExt}{extension}");
        int counter = 1;

        while (File.Exists(filePath))
        {
            filePath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
            counter++;
        }

        return filePath;
    }
}