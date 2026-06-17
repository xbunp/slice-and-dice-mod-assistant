
// ==========================================
// INTERNAL AST (Strictly for parsing string structure)
// ==========================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ASTNode { public abstract string Export(); }

[System.Serializable]
public class ASTParser
{
    private enum TokenType { Identifier, Dot, Hash, LParen, RParen, EOF }
    private class Token { public TokenType Type; public string Value; }

    private readonly string input;
    private int pos;

    public ASTParser(string input) { this.input = input ?? ""; this.pos = 0; }

    private Token NextToken()
    {
        if (pos >= input.Length)
        {
            //UnityEngine.Debug.Log($"[ASTParser.NextToken] EOF at pos {pos}");
            return new Token { Type = TokenType.EOF };
        }

        // --- ADDED DEBUG ---
        // Log the current character at pos before any processing
        char currentChar = input[pos];
        //UnityEngine.Debug.Log($"[ASTParser.NextToken] Processing pos: {pos}, char: '{currentChar}' ({(int)currentChar}) | Remaining: '{input.Substring(pos, Math.Min(30, input.Length - pos))}'");

        if (currentChar == '.') { pos++; } //UnityEngine.Debug.Log($"[ASTParser.NextToken] -> Token: Dot"); return new Token { Type = TokenType.Dot }; }
        if (currentChar == '#') { pos++; }//UnityEngine.Debug.Log($"[ASTParser.NextToken] -> Token: Hash"); return new Token { Type = TokenType.Hash }; }
        if (currentChar == '(') { pos++; }//UnityEngine.Debug.Log($"[ASTParser.NextToken] -> Token: LParen"); return new Token { Type = TokenType.LParen }; }
        if (currentChar == ')') { pos++; }//UnityEngine.Debug.Log($"[ASTParser.NextToken] -> Token: RParen"); return new Token { Type = TokenType.RParen }; }

        int start = pos;
        int bracketDepth = 0, braceDepth = 0;

        while (pos < input.Length)
        {
            char curr = input[pos];
            // --- ADDED DEBUG ---
            // Log depth changes within the identifier loop
            string depthChange = "";
            if (curr == '[') { bracketDepth++; depthChange = " (bracketDepth++)"; }
            else if (curr == ']') { bracketDepth--; depthChange = " (bracketDepth--)"; }
            else if (curr == '{') { braceDepth++; depthChange = " (braceDepth++)"; }
            else if (curr == '}') { braceDepth--; depthChange = " (braceDepth--)"; }

            //UnityEngine.Debug.Log($"[ASTParser.NextToken] Identifier loop: pos {pos}, curr '{curr}', bracketDepth {bracketDepth}, braceDepth {braceDepth}{depthChange}");

            if (bracketDepth == 0 && braceDepth == 0)
            {
                if (curr == '.' || curr == '#' || curr == '(' || curr == ')')
                {
                    // --- ADDED DEBUG ---
                    //UnityEngine.Debug.Log($"[ASTParser.NextToken] Identifier loop BREAK: Detected delimiter '{curr}'");
                    break; // Delimiter found, stop consuming for identifier
                }
            }
            pos++;
        }

        string identifierValue = input.Substring(start, pos - start);

        if (identifierValue.IndexOf("Slimelet", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UnityEngine.Debug.Log($"[ASTParser Debug] Found target identifier: '{identifierValue}' | Char at pos: '{(pos < input.Length ? input[pos].ToString() : "EOF")}'");
        }

        // --- ADDED DEBUG ---
        //UnityEngine.Debug.Log($"[ASTParser.NextToken] -> Token: Identifier, Value: '{identifierValue}' (Length: {identifierValue.Length})");
        return new Token { Type = TokenType.Identifier, Value = identifierValue };
    }

    private List<Token> Tokenize()
    {
        List<Token> tokens = new List<Token>();
        while (true) { var t = NextToken(); tokens.Add(t); if (t.Type == TokenType.EOF) break; }
        return tokens;
    }

    public ASTNode Parse()
    {
        var tokens = Tokenize();
        int index = 0;
        return ParseSequence(tokens, ref index);
    }

    private ASTNode ParseSequence(List<Token> tokens, ref int index)
    {
        SequenceNode seq = new SequenceNode();
        while (index < tokens.Count && tokens[index].Type != TokenType.EOF && tokens[index].Type != TokenType.RParen)
        {
            seq.Items.Add(ParseChain(tokens, ref index));
            if (index < tokens.Count && tokens[index].Type == TokenType.Hash) index++;
        }
        return seq.Items.Count == 1 ? seq.Items[0] : seq;
    }

    private ASTNode ParseChain(List<Token> tokens, ref int index)
    {
        ChainNode chain = new ChainNode();
        while (index < tokens.Count && tokens[index].Type != TokenType.EOF && tokens[index].Type != TokenType.RParen && tokens[index].Type != TokenType.Hash)
        {
            if (tokens[index].Type == TokenType.LParen)
            {
                index++;
                ASTNode content = ParseSequence(tokens, ref index);
                if (index < tokens.Count && tokens[index].Type == TokenType.RParen) index++;
                ASTNode scopeNode = new ScopeNode(content);
                if (index < tokens.Count && tokens[index].Type == TokenType.Identifier)
                {
                    scopeNode = new CompositeNode(scopeNode, tokens[index].Value);
                    index++;
                }
                chain.Elements.Add(scopeNode);
            }
            else if (tokens[index].Type == TokenType.Identifier) { chain.Elements.Add(new StringNode(tokens[index].Value)); index++; }
            else if (tokens[index].Type == TokenType.Dot) index++;
            else index++;
        }
        return chain.Elements.Count == 1 ? chain.Elements[0] : chain;
    }
}

[System.Serializable]
public class StringNode : ASTNode
{
    public string Value { get; set; }
    public StringNode(string value) => Value = value;
    public override string Export() => Value;
}

[System.Serializable]
public class ScopeNode : ASTNode
{
    public ASTNode Content { get; set; }
    public ScopeNode(ASTNode content) => Content = content;
    public override string Export() => Content != null ? $"({Content.Export()})" : "()";
}

[System.Serializable]
public class CompositeNode : ASTNode
{
    public ASTNode Left { get; set; }
    public string Suffix { get; set; }
    public CompositeNode(ASTNode left, string suffix) { Left = left; Suffix = suffix; }
    public override string Export() => $"{Left.Export()}{Suffix}";
}

[System.Serializable]
public class ChainNode : ASTNode
{
    public List<ASTNode> Elements { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join(".", Elements.Select(e => e.Export()));
}

[System.Serializable]
public class SequenceNode : ASTNode
{
    public List<ASTNode> Items { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join("#", Items.Select(e => e.Export()));
}
