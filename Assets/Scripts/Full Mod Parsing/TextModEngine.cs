using System;
using System.Collections.Generic;
using System.Text;

namespace SliceDiceTextMod
{
    // These are the logical "chunks" your game uses.
    public enum TokenType
    {
        Word,           // e.g., "fight", "Rat", "ph", "ch"
        Number,         // e.g., "4", "0.271", "-1"
        Dot,            // .
        Comma,          // ,
        Ampersand,      // &
        Hash,           // #
        Plus,           // +
        Colon,          // :
        Equals,         // =
        Tilde,          // ~
        AtSymbol,       // @
        OpenParen,      // (
        CloseParen,     // )
        OpenBracket,    // [
        CloseBracket,   // ]
        OpenBrace,      // {
        CloseBrace,     // }
        Caret,          // ^ (for modifier levels)
        Slash,          // /
        Exclamation,    // ! (for hidden modifiers)
        EOF             // End of File / End of String
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public Token(TokenType type, string value) { Type = type; Value = value; }
        public override string ToString() => $"[{Type}: '{Value}']";
    }

    /// <summary>
    /// THE NEW "BLACK BOX" ENGINE.
    /// Provide this class with a raw mod string, and it will lex and parse it into an AST (Abstract Syntax Tree).
    /// </summary>
    public class TextModEngine
    {
        // ====================================================================
        // 1. THE LEXER (Turns a raw string into Tokens)
        // ====================================================================
        private List<Token> Lex(string input)
        {
            var tokens = new List<Token>();
            int pos = 0;

            while (pos < input.Length)
            {
                char current = input[pos];

                // Skip whitespace
                if (char.IsWhiteSpace(current))
                {
                    pos++;
                    continue;
                }

                // Single-character punctuation tokens
                switch (current)
                {
                    case '.': tokens.Add(new Token(TokenType.Dot, ".")); pos++; continue;
                    case ',': tokens.Add(new Token(TokenType.Comma, ",")); pos++; continue;
                    case '&': tokens.Add(new Token(TokenType.Ampersand, "&")); pos++; continue;
                    case '#': tokens.Add(new Token(TokenType.Hash, "#")); pos++; continue;
                    case '+': tokens.Add(new Token(TokenType.Plus, "+")); pos++; continue;
                    case ':': tokens.Add(new Token(TokenType.Colon, ":")); pos++; continue;
                    case '=': tokens.Add(new Token(TokenType.Equals, "=")); pos++; continue;
                    case '~': tokens.Add(new Token(TokenType.Tilde, "~")); pos++; continue;
                    case '@': tokens.Add(new Token(TokenType.AtSymbol, "@")); pos++; continue;
                    case '(': tokens.Add(new Token(TokenType.OpenParen, "(")); pos++; continue;
                    case ')': tokens.Add(new Token(TokenType.CloseParen, ")")); pos++; continue;
                    case '[': tokens.Add(new Token(TokenType.OpenBracket, "[")); pos++; continue;
                    case ']': tokens.Add(new Token(TokenType.CloseBracket, "]")); pos++; continue;
                    case '{': tokens.Add(new Token(TokenType.OpenBrace, "{")); pos++; continue;
                    case '}': tokens.Add(new Token(TokenType.CloseBrace, "}")); pos++; continue;
                    case '^': tokens.Add(new Token(TokenType.Caret, "^")); pos++; continue;
                    case '/': tokens.Add(new Token(TokenType.Slash, "/")); pos++; continue;
                    case '!': tokens.Add(new Token(TokenType.Exclamation, "!")); pos++; continue;
                }

                // Numbers (including negatives and decimals)
                if (char.IsDigit(current) || (current == '-' && pos + 1 < input.Length && char.IsDigit(input[pos + 1])))
                {
                    StringBuilder num = new StringBuilder();
                    if (current == '-')
                    {
                        num.Append('-');
                        pos++;
                    }
                    while (pos < input.Length && (char.IsDigit(input[pos]) || input[pos] == '.'))
                    {
                        // Minor check to prevent eating the structural dots
                        if (input[pos] == '.' && pos + 1 < input.Length && !char.IsDigit(input[pos + 1]))
                            break;

                        num.Append(input[pos]);
                        pos++;
                    }
                    tokens.Add(new Token(TokenType.Number, num.ToString()));
                    continue;
                }

                // Words / Identifiers (e.g., "fight", "heropool", "Rat")
                if (char.IsLetter(current) || current == '_')
                {
                    StringBuilder word = new StringBuilder();
                    while (pos < input.Length && (char.IsLetterOrDigit(input[pos]) || input[pos] == '_' || input[pos] == '-'))
                    {
                        word.Append(input[pos]);
                        pos++;
                    }
                    tokens.Add(new Token(TokenType.Word, word.ToString()));
                    continue;
                }

                // If it's a character we don't recognize, just skip or record it as a word for now
                pos++;
            }

            tokens.Add(new Token(TokenType.EOF, ""));
            return tokens;
        }


        // ====================================================================
        // 2. THE PARSER (Turns Tokens into useful C# Objects)
        // ====================================================================

        private int _tokenIndex = 0;
        private List<Token> _tokens;

        // Helper to get current token
        private Token CurrentToken => _tokenIndex < _tokens.Count ? _tokens[_tokenIndex] : _tokens[_tokens.Count - 1];

        // Helper to advance to next token
        private Token Advance()
        {
            var token = CurrentToken;
            if (_tokenIndex < _tokens.Count - 1) _tokenIndex++;
            return token;
        }

        // Helper to peek ahead without consuming
        private Token Peek(int offset = 1)
        {
            if (_tokenIndex + offset >= _tokens.Count) return _tokens[_tokens.Count - 1];
            return _tokens[_tokenIndex + offset];
        }

        /// <summary>
        /// This is the main entry point for the rest of your codebase!
        /// </summary>
        public ParsedMod UnpackMod(string rawModText)
        {
            // 1. Lex the text into tokens
            _tokens = Lex(rawModText);
            _tokenIndex = 0;

            // 2. Prepare the output object
            var result = new ParsedMod();

            // 3. Parse the tokens
            // (Right now this is just a dummy loop. You will provide code snippets, 
            // and I will build the logic here to parse your specific rules).
            while (CurrentToken.Type != TokenType.EOF)
            {
                // TODO: Identify Top-Level Delimiters, Pools, Phases, Choice Menus, etc.
                Advance();
            }

            return result;
        }
    }

    // ====================================================================
    // 3. YOUR OUTPUT DATA STRUCTURES (The "Useful" stuff)
    // ====================================================================

    // You will use this class in the rest of your app. No more string splitting!
    public class ParsedMod
    {
        public List<string> Modifiers { get; set; } = new List<string>();
        // We will add more properties here as we iteratively build the parser.
    }
}