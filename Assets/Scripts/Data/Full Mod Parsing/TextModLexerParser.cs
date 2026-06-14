using System;
using System.Collections.Generic;
using System.Text;

namespace SliceDiceTextMod
{
    public enum TokenType
    {
        Word, Number, Ampersand, Comma, Plus, Minus, Colon, Hash, Dot, Equals,
        Semicolon, Tilde, AtMarker, OpenParen, CloseParen, OpenBracket, CloseBracket,
        OpenBrace, CloseBrace, Caret, Slash, Exclamation, BracketedContent, EOF
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public Token(TokenType type, string value) { Type = type; Value = value; }
        public override string ToString() => $"[{Type}: '{Value}']";
    }

    public static class TextModLexerParser
    {
        // =========================================================================
        // PUBLIC STRING-BASED API (The easy-to-use overloads)
        // =========================================================================

        private static bool IsEntityKeyword(string word)
        {
            string[] keys = { "replica", "img", "n", "col", "hp", "tier", "sd", "speech", "doc", "i", "k", "hsv", "facade", "abilitydata", "gift", "t" };
            foreach (var k in keys) if (word.Equals(k, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Parses a raw, clipboard-pasted Hero string directly into a HeroData object.
        /// </summary>
        public static HeroData ParseHero(string rawHeroString)
        {
            if (string.IsNullOrWhiteSpace(rawHeroString)) return null;

            var tokens = Lex(rawHeroString);
            int pos = 0;
            return ParseHero(tokens, ref pos);
        }

        /// <summary>
        /// Parses a raw, multi-line Mod string directly into a ModDataContainer.
        /// </summary>
        public static void ParseModIntoContainer(string rawModText, ModData container)
        {
            if (string.IsNullOrWhiteSpace(rawModText)) return;

            var tokens = Lex(rawModText);
            int pos = 0;
            ParseModIntoContainer(tokens, ref pos, container);
        }

        // =========================================================================
        // INTERNAL TOKEN-BASED ENGINE
        // =========================================================================

        public static List<Token> Lex(string input)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < input.Length)
            {
                char c = input[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '[')
                {
                    int start = i;
                    int depth = 0;
                    while (i < input.Length)
                    {
                        if (input[i] == '[') depth++;
                        else if (input[i] == ']') depth--;
                        i++;
                        if (depth == 0) break;
                    }
                    string val = input.Substring(start + 1, i - start - 2);
                    tokens.Add(new Token(TokenType.BracketedContent, val));
                    continue;
                }

                if (c == '@' && i + 1 < input.Length && char.IsDigit(input[i + 1]))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(c); i++;
                    while (i < input.Length && char.IsDigit(input[i])) { sb.Append(input[i]); i++; }

                    string[] suffixes = { "lb", "b", "r", "l", "p", "s" };
                    foreach (var sfx in suffixes)
                    {
                        if (i + sfx.Length <= input.Length && input.Substring(i, sfx.Length).Equals(sfx, StringComparison.OrdinalIgnoreCase))
                        {
                            sb.Append(input.Substring(i, sfx.Length));
                            i += sfx.Length;
                            break;
                        }
                    }
                    tokens.Add(new Token(TokenType.AtMarker, sb.ToString()));
                    continue;
                }

                switch (c)
                {
                    case '&': tokens.Add(new Token(TokenType.Ampersand, "&")); i++; continue;
                    case ',': tokens.Add(new Token(TokenType.Comma, ",")); i++; continue;
                    case '+': tokens.Add(new Token(TokenType.Plus, "+")); i++; continue;
                    case '-': tokens.Add(new Token(TokenType.Minus, "-")); i++; continue;
                    case ':': tokens.Add(new Token(TokenType.Colon, ":")); i++; continue;
                    case '#': tokens.Add(new Token(TokenType.Hash, "#")); i++; continue;
                    case '.': tokens.Add(new Token(TokenType.Dot, ".")); i++; continue;
                    case '=': tokens.Add(new Token(TokenType.Equals, "=")); i++; continue;
                    case ';': tokens.Add(new Token(TokenType.Semicolon, ";")); i++; continue;
                    case '~': tokens.Add(new Token(TokenType.Tilde, "~")); i++; continue;
                    case '(': tokens.Add(new Token(TokenType.OpenParen, "(")); i++; continue;
                    case ')': tokens.Add(new Token(TokenType.CloseParen, ")")); i++; continue;
                    case '{': tokens.Add(new Token(TokenType.OpenBrace, "{")); i++; continue;
                    case '}': tokens.Add(new Token(TokenType.CloseBrace, "}")); i++; continue;
                    case '^': tokens.Add(new Token(TokenType.Caret, "^")); i++; continue;
                    case '/': tokens.Add(new Token(TokenType.Slash, "/")); i++; continue;
                    case '!': tokens.Add(new Token(TokenType.Exclamation, "!")); i++; continue;
                }

                if (char.IsDigit(c))
                {
                    StringBuilder num = new StringBuilder();
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
                    {
                        if (input[i] == '.' && (i + 1 >= input.Length || !char.IsDigit(input[i + 1]))) break;
                        num.Append(input[i]);
                        i++;
                    }
                    tokens.Add(new Token(TokenType.Number, num.ToString()));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    StringBuilder word = new StringBuilder();
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '\''))
                    {
                        word.Append(input[i]);
                        i++;
                    }
                    tokens.Add(new Token(TokenType.Word, word.ToString()));
                    continue;
                }

                i++;
            }

            tokens.Add(new Token(TokenType.EOF, ""));
            return tokens;
        }

        private static void ParseModIntoContainer(List<Token> tokens, ref int pos, ModData container)
        {
            while (pos < tokens.Count && tokens[pos].Type != TokenType.EOF)
            {
                if (tokens[pos].Type == TokenType.Ampersand || tokens[pos].Type == TokenType.Comma || tokens[pos].Type == TokenType.Equals)
                {
                    pos++;
                    continue;
                }

                if (tokens[pos].Type == TokenType.Word && tokens[pos].Value.ToLower() == "replica")
                {
                    var hero = ParseHero(tokens, ref pos);
                    //container.Heroes.Add(hero);
                }
                else
                {
                    /*
                    var directive = ParseDirective(tokens, ref pos);
                    if (directive != null)
                    {
                        container.Directives.Add(directive);
                    }
                    */
                }
            }
        }

        /*
        private static ModDirectiveData ParseDirective(List<Token> tokens, ref int pos)
        {
            bool isHidden = false;
            string floorRaw = "";
            bool hasOuterParen = false;

            if (tokens[pos].Type == TokenType.OpenParen) { hasOuterParen = true; pos++; }
            if (tokens[pos].Type == TokenType.Exclamation)
            {
                isHidden = true; pos++;
                if (tokens[pos].Type == TokenType.Word && tokens[pos].Value.ToLower().StartsWith("m")) pos++;
                if (tokens[pos].Type == TokenType.OpenParen) pos++;
            }

            if (tokens[pos].Type == TokenType.Number && Peek(tokens, pos, 1).Type == TokenType.Dot) { floorRaw = tokens[pos].Value; pos += 2; }
            else if (tokens[pos].Type == TokenType.Number && Peek(tokens, pos, 1).Type == TokenType.Minus && Peek(tokens, pos, 2).Type == TokenType.Number && Peek(tokens, pos, 3).Type == TokenType.Dot) { floorRaw = $"{tokens[pos].Value}-{tokens[pos + 2].Value}"; pos += 4; }
            else if (tokens[pos].Type == TokenType.Word && tokens[pos].Value.StartsWith("e") && Peek(tokens, pos, 1).Type == TokenType.Dot) { floorRaw = tokens[pos].Value; pos += 2; }

            ModDirectiveData result = null;

            if (tokens[pos].Type == TokenType.Word)
            {
                string cmd = tokens[pos].Value.ToLower();

                if ((cmd == "replace" && Peek(tokens, pos, 1).Type == TokenType.Dot && Peek(tokens, pos, 2).Value.ToLower() == "heropool") ||
                    cmd == "heropool" || cmd == "itempool" || cmd == "monsterpool" || cmd == "fight" || cmd == "add")
                {
                    bool isReplace = false;
                    if (cmd == "replace") { isReplace = true; pos += 2; cmd = tokens[pos].Value.ToLower(); }
                    pos += 2;

                    var pool = new PoolDirectiveData { IsHidden = isHidden, FloorSelectorRaw = floorRaw, ReplaceBase = isReplace };
                    while (pos < tokens.Count && (tokens[pos].Type == TokenType.Word || tokens[pos].Type == TokenType.Number))
                    {
                        pool.Elements.Add(tokens[pos].Value);
                        pos++;
                        if (tokens[pos].Type == TokenType.Plus) pos++; else break;
                    }
                    result = pool;
                }
                else if (cmd.StartsWith("ph") && Peek(tokens, pos, 1).Type == TokenType.Dot)
                {
                    pos += 2;
                    var phase = new PhaseEventData { IsHidden = isHidden, FloorSelectorRaw = floorRaw };
                    phase.PhaseCode = tokens[pos].Value;
                    pos++;

                    while (pos < tokens.Count && tokens[pos].Type != TokenType.Ampersand && tokens[pos].Type != TokenType.Comma && tokens[pos].Type != TokenType.EOF && tokens[pos].Type != TokenType.CloseParen)
                    {
                        if (tokens[pos].Type == TokenType.Semicolon) pos++;
                        phase.PhaseActions.Add(tokens[pos].Value);
                        pos++;
                    }
                    result = phase;
                }
            }
            else if (tokens[pos].Type == TokenType.AtMarker)
            {
                var choice = new ChoiceMenuData { IsHidden = isHidden, FloorSelectorRaw = floorRaw };
                choice.ChoiceMarker = tokens[pos].Value;
                pos++;

                StringBuilder sb = new StringBuilder();
                while (pos < tokens.Count && tokens[pos].Type != TokenType.Ampersand && tokens[pos].Type != TokenType.Comma && tokens[pos].Type != TokenType.AtMarker && tokens[pos].Type != TokenType.EOF && tokens[pos].Type != TokenType.CloseParen)
                {
                    sb.Append(tokens[pos].Value);
                    if (tokens[pos].Type == TokenType.Dot) sb.Append(".");
                    pos++;
                }
                choice.Label = sb.ToString();
                result = choice;
            }

            if (result == null)
            {
                StringBuilder raw = new StringBuilder();
                while (pos < tokens.Count && tokens[pos].Type != TokenType.Ampersand && tokens[pos].Type != TokenType.Comma && tokens[pos].Type != TokenType.EOF && tokens[pos].Type != TokenType.CloseParen)
                {
                    raw.Append(tokens[pos].Value);
                    pos++;
                }
                result = new RawDirectiveData { IsHidden = isHidden, FloorSelectorRaw = floorRaw, RawContent = raw.ToString() };
            }

            if (hasOuterParen && tokens[pos].Type == TokenType.CloseParen) pos++;
            if (isHidden && tokens[pos].Type == TokenType.CloseParen) pos++;

            return result;
        }
        */

        private static HeroData ParseHero(List<Token> tokens, ref int pos)
        {
            HeroData hero = new HeroData();

            while (pos < tokens.Count && tokens[pos].Type != TokenType.EOF && tokens[pos].Type != TokenType.Ampersand && tokens[pos].Type != TokenType.Comma)
            {
                var t = tokens[pos];

                if (t.Type == TokenType.Word)
                {
                    string key = t.Value.ToLower();

                    if (key == "replica" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        hero.baseReplica = Peek(tokens, pos, 2).Value;
                        pos += 3; continue;
                    }
                    if (key == "n" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        hero.entityName = Peek(tokens, pos, 2).Value.Replace("_", " ");
                        pos += 3; continue;
                    }
                    if (key == "hp" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        if (int.TryParse(Peek(tokens, pos, 2).Value, out int hp)) hero.hp = hp;
                        pos += 3; continue;
                    }
                    if (key == "tier" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        if (int.TryParse(Peek(tokens, pos, 2).Value, out int tier)) hero.tier = tier;
                        pos += 3; continue;
                    }
                    if (key == "col" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        hero.colorClass = Peek(tokens, pos, 2).Value;
                        pos += 3; continue;
                    }
                    if (key == "img" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        if (Peek(tokens, pos, 2).Type == TokenType.BracketedContent)
                            hero.imageOverride = $"[{Peek(tokens, pos, 2).Value}]";
                        else
                            hero.imageOverride = Peek(tokens, pos, 2).Value;
                        pos += 3; continue;
                    }
                    if (key == "speech" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        hero.speech = ConsumeStringValue(tokens, ref pos);
                        continue;
                    }
                    if (key == "doc" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        hero.doc = ConsumeStringValue(tokens, ref pos);
                        continue;
                    }
                    if (key == "hsv" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        ParseHsv(tokens, ref pos, out hero.h, out hero.s, out hero.v);
                        continue;
                    }
                    if (key == "sd" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        pos = ParseDiceSides(hero, tokens, pos);
                        continue;
                    }
                    if (key == "i" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        if (tokens[pos].Type == TokenType.OpenParen)
                        {
                            // Custom Item nested handling
                            string itemStr = ExtractBalancedBlock(tokens, ref pos);
                            hero.customItems.Add(SDData.Parse<ItemData>(itemStr));
                        }
                        else
                        {
                            // Check if the word is a face name to determine if this is a Dice Modifier
                            string[] faceNames = { "left", "mid", "top", "bot", "right", "rightmost" };
                            bool isDiceModifier = tokens[pos].Type == TokenType.Word &&
                                                  Array.IndexOf(faceNames, tokens[pos].Value.ToLower()) >= 0;

                            if (isDiceModifier)
                            {
                                ParseDiceModifiers(hero, tokens, ref pos);
                            }
                            else
                            {
                                // Plain Item handling: Consume everything up until the next dot as the item name
                                StringBuilder itemSb = new StringBuilder();
                                bool lastWasWordOrNum = false;

                                while (pos < tokens.Count)
                                {
                                    if (tokens[pos].Type == TokenType.Dot || tokens[pos].Type == TokenType.CloseParen || tokens[pos].Type == TokenType.EOF)
                                        break;

                                    // Reconstruct spaces stripped by the Lexer
                                    if (lastWasWordOrNum && (tokens[pos].Type == TokenType.Word || tokens[pos].Type == TokenType.Number))
                                    {
                                        itemSb.Append(" ");
                                    }

                                    itemSb.Append(tokens[pos].Value);
                                    lastWasWordOrNum = (tokens[pos].Type == TokenType.Word || tokens[pos].Type == TokenType.Number);
                                    pos++;
                                }

                                string itemName = itemSb.ToString().Replace("_", " ");
                                hero.items.Add(itemName);
                            }
                        }
                        continue;
                    }
                    if (key == "abilitydata" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        if (tokens[pos].Type == TokenType.OpenParen)
                        {
                            // Safely extract the custom ability string and pass it to AbilityData.Parse
                            string cabString = ExtractBalancedBlock(tokens, ref pos);
                            hero.AddCustomAbility(AbilityData.Parse(cabString));
                        }
                        else
                        {
                            // Handle standard base abilities
                            string baseAbs = ConsumeStringValue(tokens, ref pos);
                            hero.baseAbilityData.AddRange(baseAbs.Split('#'));
                        }
                        continue;
                    }

                    if (key == "i" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        if (tokens[pos].Type == TokenType.OpenParen)
                        {
                            // Safely extract custom item block so it doesn't break the dice modifier parser
                            string itemStr = ExtractBalancedBlock(tokens, ref pos);
                            hero.customItems.Add(SDData.Parse<ItemData>(itemStr));
                        }
                        else
                        {
                            ParseDiceModifiers(hero, tokens, ref pos);
                        }
                        continue;
                    }

                    if (key == "gift" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        hero.blessings.AddRange(ConsumeStringValue(tokens, ref pos).Split('#'));
                        continue;
                    }

                    if (key == "t" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                    {
                        pos += 2;
                        hero.traits.AddRange(ConsumeStringValue(tokens, ref pos).Split('#'));
                        continue;
                    }
                }
                pos++;
            }

            return hero;
        }

        private static int ParseDiceSides(HeroData hero, List<Token> tokens, int pos)
        {
            int sideIndex = 0;
            while (pos < tokens.Count && sideIndex < 6)
            {
                if (tokens[pos].Type == TokenType.Dot || tokens[pos].Type == TokenType.CloseParen || tokens[pos].Type == TokenType.EOF)
                    break;

                if (tokens[pos].Type == TokenType.Number)
                {
                    int val = int.Parse(tokens[pos].Value);
                    hero.diceSides[sideIndex].effectID = val;
                    hero.diceSides[sideIndex].pips = 0;
                    pos++;

                    if (tokens[pos].Type == TokenType.Minus)
                    {
                        pos++;
                        if (tokens[pos].Type == TokenType.Number)
                        {
                            hero.diceSides[sideIndex].pips = int.Parse(tokens[pos].Value);
                            pos++;
                        }
                    }
                }

                if (tokens[pos].Type == TokenType.Colon) pos++;
                else if (tokens[pos].Type != TokenType.Number) pos++;

                sideIndex++;
            }
            return pos;
        }

        private static void ParseDiceModifiers(HeroData hero, List<Token> tokens, ref int pos)
        {
            string[] faceNames = { "left", "mid", "top", "bot", "right", "rightmost" };

            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokenType.CloseParen || tokens[pos].Type == TokenType.EOF) break;
                if (tokens[pos].Type == TokenType.Dot && IsEntityKeyword(Peek(tokens, pos, 1).Value)) break;

                int targetFace = -1;
                if (tokens[pos].Type == TokenType.Word)
                {
                    targetFace = Array.IndexOf(faceNames, tokens[pos].Value.ToLower());
                }

                if (targetFace >= 0 && Peek(tokens, pos, 1).Type == TokenType.Dot)
                {
                    pos += 2;

                    while (pos < tokens.Count)
                    {
                        if (tokens[pos].Type == TokenType.CloseParen || tokens[pos].Type == TokenType.EOF) return;
                        if (tokens[pos].Type == TokenType.Dot && IsEntityKeyword(Peek(tokens, pos, 1).Value)) return;

                        if (tokens[pos].Type == TokenType.Word && tokens[pos].Value.ToLower() == "k" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                        {
                            hero.diceSides[targetFace].keywords.Add(Peek(tokens, pos, 2).Value);
                            pos += 3;
                        }
                        else if (tokens[pos].Type == TokenType.Word && tokens[pos].Value.ToLower() == "facade" && Peek(tokens, pos, 1).Type == TokenType.Dot)
                        {
                            pos += 2;
                            hero.diceSides[targetFace].facadeID = tokens[pos].Value;
                            pos++;
                            if (tokens[pos].Type == TokenType.Colon)
                            {
                                pos++;
                                ParseHsvString(tokens, ref pos, out string hsvStr);
                                hero.diceSides[targetFace].facadeColor = hsvStr;
                            }
                        }
                        else
                        {
                            pos++;
                        }

                        if (tokens[pos].Type == TokenType.Hash) pos++;
                        else break;
                    }
                }
                else
                {
                    pos++;
                }
            }
        }

        private static void ParseHsv(List<Token> tokens, ref int pos, out int h, out int s, out int v)
        {
            h = 0; s = 0; v = 0;
            if (tokens[pos].Type == TokenType.Minus) { pos++; h = -int.Parse(tokens[pos].Value); } else { h = int.Parse(tokens[pos].Value); }
            pos++;
            if (tokens[pos].Type != TokenType.Colon) return;
            pos++;
            if (tokens[pos].Type == TokenType.Minus) { pos++; s = -int.Parse(tokens[pos].Value); } else { s = int.Parse(tokens[pos].Value); }
            pos++;
            if (tokens[pos].Type != TokenType.Colon) return;
            pos++;
            if (tokens[pos].Type == TokenType.Minus) { pos++; v = -int.Parse(tokens[pos].Value); } else { v = int.Parse(tokens[pos].Value); }
            pos++;
        }

        private static void ParseHsvString(List<Token> tokens, ref int pos, out string hsvString)
        {
            StringBuilder sb = new StringBuilder();
            while (tokens[pos].Type == TokenType.Number || tokens[pos].Type == TokenType.Minus || tokens[pos].Type == TokenType.Colon)
            {
                sb.Append(tokens[pos].Value);
                pos++;
            }
            hsvString = sb.ToString();
        }

        // =========================================================================
        // UTILITIES
        // =========================================================================

        public static Token Peek(List<Token> tokens, int currentPos, int offset)
        {
            if (currentPos + offset >= tokens.Count) return tokens[tokens.Count - 1];
            return tokens[currentPos + offset];
        }

        private static string ConsumeStringValue(List<Token> tokens, ref int pos)
        {
            pos += 2; // Consume key and dot
            StringBuilder sb = new StringBuilder();
            while (pos < tokens.Count)
            {
                if (tokens[pos].Type == TokenType.Dot && IsEntityKeyword(Peek(tokens, pos, 1).Value)) break;
                if (tokens[pos].Type == TokenType.CloseParen) break;
                sb.Append(tokens[pos].Value);
                pos++;
            }
            return sb.ToString();
        }

        private static string ExtractBalancedBlock(List<Token> tokens, ref int pos)
        {
            int depth = 0;
            StringBuilder sb = new StringBuilder();

            while (pos < tokens.Count)
            {
                var t = tokens[pos];
                if (t.Type == TokenType.OpenParen) depth++;
                else if (t.Type == TokenType.CloseParen) depth--;

                sb.Append(t.Value);
                pos++;

                if (depth == 0) break;
            }
            return sb.ToString();
        }
    }
}