using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace SliceDiceTextMod
{
    public static class ModTextEngine
    {
        public static void UnpackIntoContainer(string fullModString, ModDataContainer container)
        {
            if (string.IsNullOrWhiteSpace(fullModString)) return;

            string cleanString = fullModString.Replace("\r", "").Replace("\n", "");
            List<string> rawChunks = DirectiveRegistry.SplitRespectingParens(cleanString, '&');

            foreach (string chunk in rawChunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;
                container.Directives.Add(ParseChunk(chunk.Trim()));
            }
        }

        private static ModDirectiveData ParseChunk(string chunk)
        {
            bool isHidden = false;
            string core = chunk;

            if (Regex.IsMatch(core, @"^!?m?\("))
            {
                core = Regex.Replace(core, @"^!?m?\(", "(");
                if (core.StartsWith("(") && core.EndsWith(")"))
                {
                    core = core.Substring(1, core.Length - 2).Trim();
                    isHidden = true;
                }
            }

            core = ParserHelpers.StripFloorSelector(core, out FloorSelector fs);
            string floorRaw = fs?.ToSyntax();

            // Ask the registry if ANY registered directive knows how to parse this chunk
            foreach (var entry in DirectiveRegistry.Entries)
            {
                var parsedData = entry.TryParse(core, isHidden, floorRaw);
                if (parsedData != null) return parsedData;
            }

            // Fallback for unrecognized phases/directives
            return new RawDirectiveData { RawContent = chunk };
        }

        public static string Repack(List<ModDirectiveData> directives)
        {
            var validStrings = directives.Select(d => d.ToModString()).Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join("&", validStrings);
        }
    }
}