using SliceDiceTextMod;
using System.Collections.Generic;
using System.Linq;

namespace SliceDiceTextMod
{
    public class HeroPoolData : PoolDirectiveData
    {
        protected override string Prefix => ReplaceBaseHeroes ? "replace.heropool" : "heropool";
        public bool ReplaceBaseHeroes { get; set; }

        public override string ToModString()
        {
            if (Elements == null || Elements.Count == 0) return "";

            var activeElements = Elements.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (activeElements.Count == 0) return "";

            List<string> poolBlocks = new List<string>();
            System.Text.StringBuilder currentChunk = new System.Text.StringBuilder();

            for (int i = 0; i < activeElements.Count; i++)
            {
                string element = activeElements[i];

                if (currentChunk.Length == 0)
                {
                    currentChunk.Append(element);
                }
                else
                {
                    // Test if appending the next element violates the 4000-character engine optimization limit
                    string testCore = $"{Prefix}.{currentChunk}+{element},";
                    string wrappedTest = ApplyWrappers(testCore);

                    if (wrappedTest.Length > 4000)
                    {
                        // Commit the current pool (ending with a comma)
                        string committedCore = $"{Prefix}.{currentChunk},";
                        poolBlocks.Add(ApplyWrappers(committedCore));

                        // Start the next pool chunk sequentially
                        currentChunk.Clear();
                        currentChunk.Append(element);
                    }
                    else
                    {
                        currentChunk.Append("+").Append(element);
                    }
                }
            }

            // Commit any remaining elements in the final chunk
            if (currentChunk.Length > 0)
            {
                string finalCore = $"{Prefix}.{currentChunk},";
                poolBlocks.Add(ApplyWrappers(finalCore));
            }

            // Return sequential blocks joined by the block-separator '&'
            return string.Join("&", poolBlocks);
        }
    }
}
