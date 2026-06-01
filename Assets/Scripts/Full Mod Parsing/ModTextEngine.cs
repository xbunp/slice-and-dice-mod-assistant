using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace SliceDiceTextMod
{
    public static class ModTextEngine
    {
        public static List<ModDirectiveData> Unpack(string fullModString)
        {
            var directives = new List<ModDirectiveData>();
            if (string.IsNullOrWhiteSpace(fullModString)) return directives;

            // Remove formatting, split by top-level delimiter '&'
            string cleanString = fullModString.Replace("\r", "").Replace("\n", "");
            List<string> rawChunks = SplitRespectingParens(cleanString, '&');

            foreach (string chunk in rawChunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;
                directives.Add(ParseChunk(chunk.Trim()));
            }

            return directives;
        }

        public static string Repack(List<ModDirectiveData> directives)
        {
            var validStrings = directives
                .Select(d => d.ToModString())
                .Where(s => !string.IsNullOrWhiteSpace(s));

            return string.Join("&", validStrings);
        }

        private static ModDirectiveData ParseChunk(string chunk)
        {
            bool isHidden = false;
            string core = chunk;

            // Detect and strip hidden wrappers
            if (Regex.IsMatch(core, @"^!?m?\("))
            {
                core = Regex.Replace(core, @"^!?m?\(", "(");
                if (core.StartsWith("(") && core.EndsWith(")"))
                {
                    core = core.Substring(1, core.Length - 2).Trim();
                    isHidden = true;
                }
            }

            // Extract Floor Selector
            core = ParserHelpers.StripFloorSelector(core, out FloorSelector fs);
            string floorRaw = fs?.ToSyntax();

            // Check for Pools
            var poolMatch = Regex.Match(core, @"^(replace\.)?(heropool|monsterpool|itempool)\.(.*)", RegexOptions.IgnoreCase);
            if (poolMatch.Success)
            {
                bool isReplace = !string.IsNullOrEmpty(poolMatch.Groups[1].Value);
                string poolType = poolMatch.Groups[2].Value.ToLower();
                string elementsRaw = poolMatch.Groups[3].Value;

                var elements = SplitRespectingParens(elementsRaw, '+')
                                .Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();

                PoolDirectiveData poolData = poolType switch
                {
                    "heropool" => new HeroPoolData { ReplaceBaseHeroes = isReplace },
                    "monsterpool" => new MonsterPoolData(),
                    "itempool" => new ItemPoolData(),
                    _ => null
                };

                if (poolData != null)
                {
                    poolData.Elements = elements;
                    poolData.IsHidden = isHidden;
                    poolData.FloorSelectorRaw = floorRaw;
                    return poolData;
                }
            }

            // Fallback for Phases, Spawns, Forced Fights, etc.
            return new RawDirectiveData { RawContent = chunk };
        }

        private static List<string> SplitRespectingParens(string input, char delimiter)
        {
            var result = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '(') depth++;
                else if (input[i] == ')') depth--;
                else if (depth == 0 && input[i] == delimiter)
                {
                    result.Add(input.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(input.Substring(start));
            return result;
        }
    }
}