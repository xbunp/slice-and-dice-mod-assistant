using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    public static class DirectiveRegistry
    {
        public class Entry
        {
            public string DropdownName;
            public Func<ModDirectiveData> CreateData;
            public Func<ModDirectiveData, FullScreenUIGenerator, Action, Action, DirectiveUI> CreateUI;
            public Func<string, bool, string, ModDirectiveData> TryParse;
        }

        public static List<Entry> Entries { get; private set; } = new List<Entry>();

        static DirectiveRegistry()
        {
            // 1. POOLS
            Entries.Add(new Entry
            {
                DropdownName = "Hero Pool",
                CreateData = () => new HeroPoolData(),
                CreateUI = (d, ui, reb, rem) => new HeroPool((HeroPoolData)d, ui, reb, rem),
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "heropool", true, () => new HeroPoolData())
            });
            Entries.Add(new Entry
            {
                DropdownName = "Monster Pool",
                CreateData = () => new MonsterPoolData(),
                CreateUI = (d, ui, reb, rem) => new MonsterPool((MonsterPoolData)d, ui, reb, rem),
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "monsterpool", false, () => new MonsterPoolData())
            });
            Entries.Add(new Entry
            {
                DropdownName = "Item Pool",
                CreateData = () => new ItemPoolData(),
                CreateUI = (d, ui, reb, rem) => new ItemPool((ItemPoolData)d, ui, reb, rem),
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "itempool", false, () => new ItemPoolData())
            });

            // 2. INJECTIONS & FIGHTS
            Entries.Add(new Entry
            {
                DropdownName = "Forced Fight",
                CreateData = () => new ForcedFightData(),
                CreateUI = null,
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "fight", false, () => new ForcedFightData())
            });
            Entries.Add(new Entry
            {
                DropdownName = "Spawn Injection",
                CreateData = () => new SpawnInjectionData(),
                CreateUI = null,
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "add", false, () => new SpawnInjectionData())
            });
            Entries.Add(new Entry
            {
                DropdownName = "Starting Party",
                CreateData = () => new PartyConfigData(),
                CreateUI = null,
                TryParse = (core, isHidden, floor) => TryParseList(core, isHidden, floor, "party", false, () => new PartyConfigData())
            });

            // 3. CONFIGURATIONS
            Entries.Add(new Entry
            {
                DropdownName = "Difficulty Config",
                CreateData = () => new ConfigDirectiveData { Prefix = "diff" },
                CreateUI = null,
                TryParse = (core, isHidden, floor) => TryParseConfig(core, isHidden, floor, "diff")
            });
            Entries.Add(new Entry
            {
                DropdownName = "Zone Config",
                CreateData = () => new ConfigDirectiveData { Prefix = "zone" },
                CreateUI = null,
                TryParse = (core, isHidden, floor) => TryParseConfig(core, isHidden, floor, "zone")
            });
            Entries.Add(new Entry
            {
                DropdownName = "Item Replacement",
                CreateData = () => new ConfigDirectiveData { Prefix = "ritemx" },
                CreateUI = null,
                TryParse = (core, isHidden, floor) => {
                    var match = Regex.Match(core, @"^(?:i\.)?(ritemx)\.(.*)", RegexOptions.IgnoreCase);
                    if (!match.Success) return null;
                    return new ConfigDirectiveData { Prefix = match.Groups[1].Value, Payload = match.Groups[2].Value, IsHidden = isHidden, FloorSelectorRaw = floor };
                }
            });

            // 4. TOGGLES & GLOBAL COMMANDS
            Entries.Add(new Entry
            {
                DropdownName = "Game Toggle",
                CreateData = () => new ToggleDirectiveData(),
                CreateUI = null,
                TryParse = (core, isHidden, floor) => {
                    var match = Regex.Match(core, @"^tog([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
                    if (!match.Success) return null;
                    return new ToggleDirectiveData { ToggleName = match.Groups[1].Value, IsHidden = isHidden, FloorSelectorRaw = floor };
                }
            });
            Entries.Add(new Entry
            {
                DropdownName = "Global Command",
                CreateData = () => new CommandDirectiveData(),
                CreateUI = null,
                TryParse = (core, isHidden, floor) => {
                    if (Regex.IsMatch(core, @"^(?i)(Delevel|Level Up|No Flee|skip(?: all)?|temporary|Wish|Clear Party|Missing|Hidden|Add(?: 10| 100)? Fights?|Minus Fight|Cursemode Loopdiff)$"))
                        return new CommandDirectiveData { Command = core, IsHidden = isHidden, FloorSelectorRaw = floor };
                    return null;
                }
            });

            // 5. CUSTOM ENTITIES (HeroModManager integration)
            Entries.Add(new Entry
            {
                DropdownName = "Custom Entity / Modifier",
                CreateData = () => new CustomEntityData(),
                CreateUI = null, // TODO: Bind to HeroModManager!
                TryParse = (core, isHidden, floor) => {
                    // If it's a standalone entity definition like "replica.Fighter..." or a keyword definition
                    if (Regex.IsMatch(core, @"^(?i)(replica\.|k\.|allitem\.)"))
                    {
                        return new CustomEntityData { EntitySyntax = core, IsHidden = isHidden, FloorSelectorRaw = floor };
                    }
                    return null;
                }
            });

            // 6. PHASES & CHOOSABLES
            Entries.Add(new Entry
            {
                DropdownName = "Phase Event",
                CreateData = () => new PhaseEventData(),
                CreateUI = null,
                TryParse = TryParsePhase
            });
            Entries.Add(new Entry
            {
                DropdownName = "Reward Option (Choosable)",
                CreateData = () => new RewardOptionData(),
                CreateUI = null,
                TryParse = TryParseChoosable
            });
        }

        // =======================================================================
        // PARSING IMPLEMENTATIONS
        // =======================================================================

        private static ModDirectiveData TryParseList(string core, bool isHidden, string floor, string prefix, bool allowReplace, Func<PoolDirectiveData> factory)
        {
            string pattern = allowReplace ? $@"^(replace\.)?({prefix})\.(.*)" : $@"^({prefix})\.(.*)";
            var match = Regex.Match(core, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            var data = factory();
            data.IsHidden = isHidden;
            data.FloorSelectorRaw = floor;

            if (allowReplace && data is HeroPoolData hp)
                hp.ReplaceBaseHeroes = !string.IsNullOrEmpty(match.Groups[1].Value);

            string elementsRaw = match.Groups[allowReplace ? 3 : 2].Value;
            data.Elements = SplitRespectingParens(elementsRaw, '+').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();

            return data;
        }

        private static ModDirectiveData TryParseConfig(string core, bool isHidden, string floor, string prefix)
        {
            var match = Regex.Match(core, $@"^({prefix})\.(.*)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            return new ConfigDirectiveData
            {
                Prefix = prefix,
                Payload = match.Groups[2].Value,
                IsHidden = isHidden,
                FloorSelectorRaw = floor
            };
        }

        private static ModDirectiveData TryParsePhase(string core, bool isHidden, string floor)
        {
            if (!Regex.IsMatch(core, @"^(?:b?ph\.|phi\.|phmp\.)|^\!", RegexOptions.IgnoreCase))
                return null;

            string pStr = core;
            if (pStr.StartsWith("bph.", StringComparison.OrdinalIgnoreCase))
                pStr = pStr.Substring(1);

            try
            {
                string fullPhaseStr = string.IsNullOrEmpty(floor) ? pStr : $"{floor}.{pStr}";
                Phase parsedPhase = Phase.Parse(fullPhaseStr, isNested: false);

                if (parsedPhase == null) return null;

                return new PhaseEventData
                {
                    PhaseObj = parsedPhase,
                    IsHidden = isHidden,
                    FloorSelectorRaw = floor
                };
            }
            catch { return null; }
        }

        private static ModDirectiveData TryParseChoosable(string core, bool isHidden, string floor)
        {
            var match = Regex.Match(core, @"^ch\.([a-z])(.*)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            string tagPayload = match.Groups[1].Value + match.Groups[2].Value;
            try
            {
                RewardTag parsedTag = RewardTag.Parse(tagPayload);
                if (parsedTag == null || parsedTag is SkipTag && tagPayload != "s") return null;

                return new RewardOptionData
                {
                    RewardObj = parsedTag,
                    IsHidden = isHidden,
                    FloorSelectorRaw = floor
                };
            }
            catch { return null; }
        }

        public static List<string> SplitRespectingParens(string input, char delimiter)
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