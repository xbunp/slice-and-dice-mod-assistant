using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[System.Serializable]
public class FightEntity
{
    public string MonsterString;
    public int Multiplier = 1;
}

[System.Serializable]
public class FightData
{
    public string bossLabel = "Boss 1";
    public List<FightEntity> entities = new List<FightEntity>();
}

public class CompiledModData
{
    public string modFileName = "MyTextMod";
    public bool humanReadable = true;

    public string floorNumber = "4";
    public string bossPoolNumber = "1";

    public List<string> monsterPool = new List<string>();
    public List<string> heroPool = new List<string>();
    public List<string> itemPool = new List<string>();

    public List<FightData> fights = new List<FightData>() { new FightData() };
    public int selectedFightIndex = 0;

    public bool clearMonsterPool = true;
    public bool clearHeroPool = true;
    public bool clearItemPool = true;

    public string compiledMod = string.Empty;

    private const int MaxChunkLength = 4000;
    private const string DummyMonster = "bee";
    private const string DummyHero = "fey";
    private const string DummyItem = "can";

    public FightData GetActiveFight()
    {
        if (fights.Count == 0) fights.Add(new FightData());
        if (selectedFightIndex < 0 || selectedFightIndex >= fights.Count) selectedFightIndex = 0;
        return fights[selectedFightIndex];
    }

    public void AddNewFight()
    {
        int nextNum = fights.Count + 1;
        fights.Add(new FightData
        {
            bossLabel = $"Boss {nextNum}"
        });
        selectedFightIndex = fights.Count - 1;
        Compile();
    }

    /// <summary>
    /// Unpacks an existing fight string from the clipboard (supporting ch.om and @4m blocks),
    /// extracts multipliers/labels/entities, reverses back to top-down UI order, and adds it.
    /// </summary>
    public bool UnpackAndAddFight(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string text = StripComments(raw).Trim();

        FightData unpacked = new FightData();

        // 1. Extract Boss Label if present
        Match labelMatch = Regex.Match(text, @"&Hidden\.mn\.([^\r\n,&)]+)");
        if (labelMatch.Success) unpacked.bossLabel = labelMatch.Groups[1].Value.Trim();

        // 2. Extract Floor number and all fight/add commands
        Regex cmdPattern = new Regex(@"(?<num>\d+)\.(?:(?<fight>fight)|x(?<mult>\d+)\.add|add)\.", RegexOptions.Compiled);
        MatchCollection matches = cmdPattern.Matches(text);

        if (matches.Count == 0) return false;

        floorNumber = matches[0].Groups["num"].Value;
        List<FightEntity> parsedEntities = new List<FightEntity>();

        foreach (Match m in matches)
        {
            int idx = m.Index + m.Length;
            while (idx < text.Length && char.IsWhiteSpace(text[idx])) idx++;

            if (idx < text.Length && text[idx] == '(')
            {
                string entityStr = ExtractBracketBlock(text, ref idx);
                int count = 1;

                if (m.Groups["mult"].Success && int.TryParse(m.Groups["mult"].Value, out int multVal))
                {
                    count = multVal;
                }

                // If this is the 'add' expansion of the preceding initial '.fight.', sum up the count
                if (parsedEntities.Count > 0 && parsedEntities[parsedEntities.Count - 1].MonsterString == entityStr && m.Groups["mult"].Success)
                {
                    parsedEntities[parsedEntities.Count - 1].Multiplier += count;
                }
                else
                {
                    parsedEntities.Add(new FightEntity
                    {
                        MonsterString = entityStr,
                        Multiplier = count
                    });
                }
            }
        }

        if (parsedEntities.Count == 0) return false;

        // Reverse back into standard UI view order
        parsedEntities.Reverse();
        unpacked.entities = parsedEntities;

        fights.Add(unpacked);
        return true;
    }

    private string ExtractBracketBlock(string text, ref int index)
    {
        StringBuilder sb = new StringBuilder();
        int depth = 0;
        while (index < text.Length)
        {
            char c = text[index];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            sb.Append(c);
            index++;
            if (depth == 0) break;
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Constructs the entire defined fight pool string following strict engine rules:
    /// 1st fight: ch.om( {Floor}.fight. ... )&Hidden.mn.Boss 1
    /// Subsequent: @4m( {Floor}.fight. ... )&Hidden.mn.Boss N
    /// Final suffix: &Hidden.mn.Boss Pool {PoolNum}
    /// </summary>
    public string CompileDefinedFights()
    {
        if (fights.Count == 0) return string.Empty;

        List<string> compiledFights = new List<string>();
        string floor = string.IsNullOrWhiteSpace(floorNumber) ? "4" : floorNumber.Trim();

        for (int f = 0; f < fights.Count; f++)
        {
            var fight = fights[f];
            if (fight.entities.Count == 0) continue;

            // 1. REVERSE ORDER: Last monster in UI list goes to the top of the fight definition
            List<FightEntity> reversed = new List<FightEntity>(fight.entities);
            reversed.Reverse();

            List<string> fightCommands = new List<string>();

            for (int i = 0; i < reversed.Count; i++)
            {
                var ent = reversed[i];
                string mon = EnforceSingleOuterBracket(ent.MonsterString);

                if (i == 0)
                {
                    // Erase and set initial monster
                    fightCommands.Add($"{floor}.fight.\n{mon}");

                    // If multiplier > 1, add remaining (Count - 1)
                    if (ent.Multiplier > 1)
                    {
                        int rem = ent.Multiplier - 1;
                        string addStr = rem > 1 ? $"{floor}.x{rem}.add.\n{mon}" : $"{floor}.add.\n{mon}";
                        fightCommands.Add(addStr);
                    }
                }
                else
                {
                    // Subsequent enemies added
                    string addStr = ent.Multiplier > 1 ? $"{floor}.x{ent.Multiplier}.add.\n{mon}" : $"{floor}.add.\n{mon}";
                    fightCommands.Add(addStr);
                }
            }

            string innerBody = string.Join("\n&\n", fightCommands);
            string header = (f == 0) ? "ch.om(" : "@4m(";
            string label = string.IsNullOrWhiteSpace(fight.bossLabel) ? $"Boss {f + 1}" : fight.bossLabel.Trim();

            compiledFights.Add($"{header}{innerBody}\n)&Hidden.mn.{label}");
        }

        if (compiledFights.Count == 0) return string.Empty;

        string poolNum = string.IsNullOrWhiteSpace(bossPoolNumber) ? "1" : bossPoolNumber.Trim();
        string fullFightBlock = string.Join("\n", compiledFights) + $"\n&Hidden.mn.Boss Pool {poolNum}";

        return fullFightBlock;
    }

    public string EnforceSingleOuterBracket(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "()";
        string s = input.Trim();

        while (s.StartsWith("(") && s.EndsWith(")"))
        {
            int depth = 0;
            bool enclosesWholeString = true;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') depth--;

                if (depth == 0 && i < s.Length - 1)
                {
                    enclosesWholeString = false;
                    break;
                }
            }

            if (enclosesWholeString && s.Length >= 2)
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }
            else
            {
                break;
            }
        }

        return $"({s})";
    }

    public void Compile()
    {
        List<string> modObjects = new List<string>();

        // 1. Pool Clears
        List<string> clearParts = new List<string>();
        if (clearMonsterPool) clearParts.Add($"(monsterpool.{DummyMonster}.part.0)");
        if (clearHeroPool) clearParts.Add($"(heropool.{DummyHero}.part.0)");
        if (clearItemPool) clearParts.Add($"(itempool.{DummyItem}.part.0)");

        if (clearParts.Count > 0)
        {
            modObjects.Add(string.Join("&", clearParts) + "&Hidden.mn.Clear Pools");
        }

        // 2. Addition Pool Chunks
        modObjects.AddRange(GetPoolChunks("monster", monsterPool, "Monster Pool"));
        modObjects.AddRange(GetPoolChunks("hero", heroPool, "Hero Pool"));
        modObjects.AddRange(GetPoolChunks("item", itemPool, "Item Pool"));

        // 3. Defined Fights
        string definedFightsBlock = CompileDefinedFights();
        if (!string.IsNullOrEmpty(definedFightsBlock))
        {
            modObjects.Add(definedFightsBlock);
        }

        // 4. Output Mod String
        if (modObjects.Count > 0)
        {
            compiledMod = "=" + string.Join(",\n\n", modObjects) + ",\n";
        }
        else
        {
            compiledMod = "=";
        }
    }

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

            List<string> subItems = SplitRootLevel(line, '+');
            foreach (string subItem in subItems) ProcessSingleItem(subItem);
        }
        Compile();
    }

    private void ProcessSingleItem(string raw)
    {
        string item = raw.Trim().TrimStart('=', ',', '+').Trim();
        if (item.EndsWith(",")) item = item.Substring(0, item.Length - 1).Trim();

        bool isItem = item.StartsWith("i.", StringComparison.OrdinalIgnoreCase)
                   || item.Contains(".tier.")
                   || item.Contains(".n.")
                   || item.Contains(".img.")
                   || item.Contains(".splice.")
                   || (item.StartsWith("(") && item.Contains("."));

        if (!isItem) return;

        if (item.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
            item = item.Substring(2).TrimStart();

        itemPool.Add(EnforceSingleOuterBracket(item));
    }

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
            else current.Append(c);
        }

        if (current.Length > 0) results.Add(current.ToString());
        return results;
    }

    public void ImportMonsters(string monsterString) => ImportPool(monsterString, monsterPool, false);
    public void ImportHeroes(string heroString) => ImportPool(heroString, heroPool, false);

    private void ImportPool(string rawText, List<string> targetPool, bool isItemPool)
    {
        targetPool.Clear();
        if (string.IsNullOrWhiteSpace(rawText)) return;

        string cleanText = StripComments(rawText);
        int i = 0;

        while (i < cleanText.Length)
        {
            if (char.IsWhiteSpace(cleanText[i])) { i++; continue; }

            if (isItemPool && cleanText[i] == 'i' && i + 2 < cleanText.Length && cleanText[i + 1] == '.' && cleanText[i + 2] == '(')
                i += 2;

            if (cleanText[i] == '(')
            {
                string fullEntity = ExtractFullEntity(cleanText, ref i);
                if (!string.IsNullOrEmpty(fullEntity))
                {
                    if (fullEntity.StartsWith("i.", StringComparison.OrdinalIgnoreCase))
                        fullEntity = fullEntity.Substring(2).TrimStart();

                    targetPool.Add(EnforceSingleOuterBracket(fullEntity));
                }
                continue;
            }
            i++;
        }
        Compile();
    }

    private string ExtractFullEntity(string text, ref int index)
    {
        StringBuilder sb = new StringBuilder();
        int depth = 0;

        while (index < text.Length)
        {
            char c = text[index];
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (c != '\r' && c != '\n') sb.Append(c);
            index++;
            if (depth == 0) break;
        }

        while (index < text.Length)
        {
            int peek = index;
            while (peek < text.Length && (text[peek] == ' ' || text[peek] == '\t')) peek++;
            if (peek >= text.Length) break;

            if (text[peek] == '.' || text[peek] == '#')
            {
                index = peek;
                while (index < text.Length)
                {
                    char c = text[index];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;

                    if (c != '\r' && c != '\n') sb.Append(c);
                    index++;

                    if (depth == 0 && index < text.Length && char.IsWhiteSpace(text[index])) break;
                }
            }
            else break;
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

    private List<string> GetPoolChunks(string poolType, List<string> items, string labelPrefix)
    {
        List<string> chunks = new List<string>();
        if (items.Count == 0) return chunks;

        StringBuilder current = new StringBuilder();
        int chunkIndex = 1;

        foreach (string item in items)
        {
            string separator = current.Length == 0 ? "" : (humanReadable ? "\n+\n" : "+");
            if (current.Length + separator.Length + item.Length > MaxChunkLength)
            {
                string label = chunkIndex > 1 ? $"{labelPrefix} {chunkIndex}" : labelPrefix;
                chunks.Add($"{poolType}pool.\n{current}\n.part.1\n&Hidden.mn.{label}");
                current.Clear();
                chunkIndex++;
                separator = "";
            }
            current.Append(separator).Append(item);
        }

        if (current.Length > 0)
        {
            string label = chunkIndex > 1 ? $"{labelPrefix} {chunkIndex}" : labelPrefix;
            chunks.Add($"{poolType}pool.\n{current}\n.part.1\n&Hidden.mn.{label}");
        }

        return chunks;
    }

    public void OutputMod()
    {
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