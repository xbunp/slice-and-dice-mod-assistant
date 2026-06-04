using System;
using System.Collections.Generic;
using System.Text;

namespace SliceDiceTextMod
{
    public static class ModParser
    {
        public static void ParseIntoContainer(string rawModText, ModData container)
        {
            // Suppress event fires during batch parsing
            container.SuppressNotifications = true;

            container.ClearDirectives();

            // Run compression over raw assets first
            string cleanText = ImageUtility.CompressImages(rawModText).Replace("\r", "").Replace("\n", "");
            List<Token> tokens = TextModLexerParser.Lex(cleanText);

            // Split into unique blocks strictly respecting balanced parentheses
            List<List<Token>> blocks = SplitByTopLevelOperators(tokens);

            foreach (var block in blocks)
            {
                var directive = ParseBlockDirective(block);
                if (directive != null)
                {
                    container.SaveDirective(null, directive);
                }
            }

            // Setting this to false automatically triggers a single event notification internally
            container.SuppressNotifications = false;
        }

        private static List<List<Token>> SplitByTopLevelOperators(List<Token> tokens)
        {
            var blocks = new List<List<Token>>();
            var currentBlock = new List<Token>();
            int parenDepth = 0;

            foreach (var t in tokens)
            {
                if (t.Type == TokenType.OpenParen) parenDepth++;
                else if (t.Type == TokenType.CloseParen) parenDepth--;

                if (parenDepth == 0 && t.Type == TokenType.Ampersand)
                {
                    if (currentBlock.Count > 0)
                    {
                        blocks.Add(new List<Token>(currentBlock));
                        currentBlock.Clear();
                    }
                }
                else if (t.Type != TokenType.EOF)
                {
                    currentBlock.Add(t);
                }
            }

            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            return blocks;
        }

        private static ModDirectiveData ParseBlockDirective(List<Token> tokens)
        {
            if (tokens.Count == 0) return null;

            bool isHidden = false;
            string floor = "";

            int startPos = 0;

            // 1. Evaluate Hidden wrappers '!m(...)' or '!'
            if (tokens[startPos].Type == TokenType.Exclamation)
            {
                isHidden = true;
                startPos++;
                if (startPos < tokens.Count && tokens[startPos].Type == TokenType.Word && tokens[startPos].Value == "m")
                {
                    startPos++; // skip 'm'
                }
                if (startPos < tokens.Count && tokens[startPos].Type == TokenType.OpenParen)
                {
                    startPos++; // step over '('
                }
            }

            // Stripping trailing balancing parenthesis if hidden was wrapped
            int endLimit = tokens.Count;
            if (isHidden && tokens[tokens.Count - 1].Type == TokenType.CloseParen)
            {
                endLimit--;
            }

            // 2. Evaluate Floor Selector (e.g. '4.', '1-5.', 'e2.1.')
            if (startPos < endLimit && tokens[startPos].Type == TokenType.Number)
            {
                // Range check or exact
                if (TextModLexerParser.Peek(tokens, startPos, 1).Type == TokenType.Minus && TextModLexerParser.Peek(tokens, startPos, 2).Type == TokenType.Number && TextModLexerParser.Peek(tokens, startPos, 3).Type == TokenType.Dot)
                {
                    floor = $"{tokens[startPos].Value}-{tokens[startPos + 2].Value}";
                    startPos += 4;
                }
                else if (TextModLexerParser.Peek(tokens, startPos, 1).Type == TokenType.Dot)
                {
                    floor = tokens[startPos].Value;
                    startPos += 2;
                }
            }
            else if (startPos < endLimit && tokens[startPos].Type == TokenType.Word && tokens[startPos].Value.StartsWith("e"))
            {
                // Match standard repeat layouts
                StringBuilder fsBuilder = new StringBuilder();
                while (startPos < endLimit && tokens[startPos].Type != TokenType.Dot)
                {
                    fsBuilder.Append(tokens[startPos].Value);
                    startPos++;
                }
                if (startPos < endLimit && tokens[startPos].Type == TokenType.Dot)
                {
                    floor = fsBuilder.ToString();
                    startPos++;
                }
            }

            // 3. Match pure segments
            List<Token> coreTokens = tokens.GetRange(startPos, endLimit - startPos);
            if (coreTokens.Count == 0) return null;

            StringBuilder coreStrBuilder = new StringBuilder();
            foreach (var t in coreTokens)
            {
                coreStrBuilder.Append(t.Value);
            }
            string coreString = coreStrBuilder.ToString();

            // Direct route for exact keywords without loop overhead
            if (coreString.StartsWith("heropool.") || coreString.StartsWith("replace.heropool."))
            {
                var data = new HeroPoolData { IsHidden = isHidden, FloorSelectorRaw = floor };
                data.ReplaceBaseHeroes = coreString.StartsWith("replace");
                string payload = coreString.Substring(coreString.IndexOf("heropool.") + 9);
                data.Elements = new List<string>(payload.Split('+'));
                return data;
            }
            if (coreString.StartsWith("monsterpool."))
            {
                var data = new MonsterPoolData { IsHidden = isHidden, FloorSelectorRaw = floor };
                data.Elements = new List<string>(coreString.Substring(12).Split('+'));
                return data;
            }
            if (coreString.StartsWith("itempool."))
            {
                var data = new ItemPoolData { IsHidden = isHidden, FloorSelectorRaw = floor };
                data.Elements = new List<string>(coreString.Substring(9).Split('+'));
                return data;
            }
            if (coreString.StartsWith("tog"))
            {
                return new ToggleDirectiveData { ToggleName = coreString.Substring(3), IsHidden = isHidden, FloorSelectorRaw = floor };
            }

            // Fallback for custom entities
            return new RawDirectiveData { RawContent = coreString, IsHidden = isHidden, FloorSelectorRaw = floor };
        }
    }
}