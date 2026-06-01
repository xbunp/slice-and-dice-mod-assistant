using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SliceDiceTextMod
{
    public abstract class ModDirectiveData
    {
        public string Id { get; private set; } = System.Guid.NewGuid().ToString();
        public bool IsHidden { get; set; }
        public string FloorSelectorRaw { get; set; } // e.g., "4", "e2.1"

        public abstract string ToModString();

        protected string ApplyWrappers(string coreData)
        {
            string output = coreData;
            if (!string.IsNullOrEmpty(FloorSelectorRaw))
                output = $"{FloorSelectorRaw}.{output}";

            if (IsHidden)
                output = $"!m({output})"; // Standard textmod hidden wrapper

            return output;
        }
    }

    public abstract class PoolDirectiveData : ModDirectiveData
    {
        public List<string> Elements { get; set; } = new List<string>();
        protected abstract string Prefix { get; }

        public override string ToModString()
        {
            if (Elements == null || Elements.Count == 0) return "";
            string joined = string.Join("+", Elements.Where(e => !string.IsNullOrWhiteSpace(e)));
            return ApplyWrappers($"{Prefix}.{joined}");
        }
    }

    public class HeroPoolData : PoolDirectiveData
    {
        protected override string Prefix => ReplaceBaseHeroes ? "replace.heropool" : "heropool";
        public bool ReplaceBaseHeroes { get; set; }
    }

    public class MonsterPoolData : PoolDirectiveData { protected override string Prefix => "monsterpool"; }
    public class ItemPoolData : PoolDirectiveData { protected override string Prefix => "itempool"; }

    // FALLBACK: Preserves exact text for complex phases/rules we haven't built UI for yet
    public class RawDirectiveData : ModDirectiveData
    {
        public string RawContent { get; set; }
        public override string ToModString() => RawContent; // Output exactly as it was
    }
}