using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Playables;

// ==========================================
// INTERNAL AST (Strictly for parsing string structure)
// ==========================================
public abstract class ASTNode
{
    public abstract string Export();
}

public class StringNode : ASTNode
{
    public string Value { get; set; }
    public StringNode(string value) => Value = value;
    public override string Export() => Value;
}

public class ScopeNode : ASTNode
{
    public ASTNode Content { get; set; }
    public ScopeNode(ASTNode content) => Content = content;
    public override string Export() => Content != null ? $"({Content.Export()})" : "()";
}

// Handles cases where text is attached directly to a paren: (memory):5:-1
public class CompositeNode : ASTNode
{
    public ASTNode Left { get; set; }
    public string Suffix { get; set; }
    public CompositeNode(ASTNode left, string suffix) { Left = left; Suffix = suffix; }
    public override string Export() => $"{Left.Export()}{Suffix}";
}

public class ChainNode : ASTNode
{
    public List<ASTNode> Elements { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join(".", Elements.Select(e => e.Export()));
}

public class SequenceNode : ASTNode
{
    public List<ASTNode> Items { get; set; } = new List<ASTNode>();
    public override string Export() => string.Join("#", Items.Select(e => e.Export()));
}

// ==========================================
// LEXER & PARSER
// ==========================================
public class ASTParser
{
    private enum TokenType { Identifier, Dot, Hash, LParen, RParen, EOF }
    private class Token { public TokenType Type; public string Value; }

    private readonly string input;
    private int pos;

    public ASTParser(string input) { this.input = input ?? ""; this.pos = 0; }

    private Token NextToken()
    {
        if (pos >= input.Length) return new Token { Type = TokenType.EOF };

        char c = input[pos];
        if (c == '.') { pos++; return new Token { Type = TokenType.Dot }; }
        if (c == '#') { pos++; return new Token { Type = TokenType.Hash }; }
        if (c == '(') { pos++; return new Token { Type = TokenType.LParen }; }
        if (c == ')') { pos++; return new Token { Type = TokenType.RParen }; }

        int start = pos;
        int bracketDepth = 0, braceDepth = 0;

        while (pos < input.Length)
        {
            char curr = input[pos];
            if (curr == '[') bracketDepth++;
            else if (curr == ']') bracketDepth--;
            else if (curr == '{') braceDepth++;
            else if (curr == '}') braceDepth--;
            else if (bracketDepth == 0 && braceDepth == 0)
            {
                if (curr == '.' || curr == '#' || curr == '(' || curr == ')') break;
            }
            pos++;
        }

        return new Token { Type = TokenType.Identifier, Value = input.Substring(start, pos - start) };
    }

    private List<Token> Tokenize()
    {
        List<Token> tokens = new List<Token>();
        while (true)
        {
            var t = NextToken();
            tokens.Add(t);
            if (t.Type == TokenType.EOF) break;
        }
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

                // Handle direct suffix attachments e.g., "(memory):5:-1"
                if (index < tokens.Count && tokens[index].Type == TokenType.Identifier)
                {
                    scopeNode = new CompositeNode(scopeNode, tokens[index].Value);
                    index++;
                }
                chain.Elements.Add(scopeNode);
            }
            else if (tokens[index].Type == TokenType.Identifier)
            {
                chain.Elements.Add(new StringNode(tokens[index].Value));
                index++;
            }
            else if (tokens[index].Type == TokenType.Dot) index++;
            else index++; // failsafe
        }
        return chain.Elements.Count == 1 ? chain.Elements[0] : chain;
    }
}

// ==========================================
// BASE DATA & STUBS (Strict String Boundaries)
// ==========================================

[System.Serializable]
public class ItemAbility
{
    public string Prefix;
    public string AbilityString; // Storing as pure string currency
}

// ==========================================
// THE MECHANIC ENGINE
// ==========================================
[System.Serializable]
public class ItemMechanic
{
    public string RawString;
    public bool IsWrapped;

    public List<string> Positions = new List<string>();
    public List<string> Operations = new List<string>();

    // Everything is reduced to flat strings for other governing agents to process
    public string BaseItem;
    public string Payload;

    private static readonly HashSet<string> ValidPositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "all", "mid", "left", "right", "top", "bot", "rightmost", "row", "col", "topbot", "left2", "mid2", "right2", "right3", "right5" };

    private static readonly HashSet<string> TogItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "togtime", "togtarg", "togfri", "togvis", "togeft", "togpip", "togkey", "togorf", "togunt", "togres", "togresm" };

    private static readonly HashSet<string> KnownUnaryOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "i", "hat", "sticker", "cast", "triggerhpdata", "onhitdata", "k", "self", "pertier", "peritem", "allitem", "alliteme", "unpack", "ea", "learn", "t", "replica", "egg", "facade" };

    public static ItemMechanic Parse(string rawData)
    {
        ItemMechanic mech = new ItemMechanic { RawString = rawData };

        ASTNode node = new ASTParser(rawData).Parse();
        if (node is ScopeNode scope)
        {
            mech.IsWrapped = true;
            node = scope.Content;
        }

        List<ASTNode> elements = node is ChainNode chain ? chain.Elements : new List<ASTNode> { node };
        int i = 0;

        while (i < elements.Count && elements[i] is StringNode sn && ValidPositions.Contains(sn.Value.ToLower()))
        {
            mech.Positions.Add(sn.Value.ToLower());
            i++;
        }

        while (i < elements.Count && elements[i] is StringNode sn)
        {
            string t = sn.Value.ToLower();
            if (KnownUnaryOps.Contains(t) || Regex.IsMatch(t, @"^x\d+$") || Regex.IsMatch(t, @"^et\d+$"))
            {
                mech.Operations.Add(sn.Value);
                i++;
            }
            else break;
        }

        int binaryOpIdx = -1;
        for (int j = i; j < elements.Count; j++)
        {
            if (elements[j] is StringNode sn && IsBinaryOp(sn.Value))
            {
                binaryOpIdx = j;
                mech.Operations.Add(sn.Value);
                break;
            }
        }

        if (binaryOpIdx != -1)
        {
            mech.BaseItem = BuildChain(elements.Skip(i).Take(binaryOpIdx - i))?.Export() ?? "";
            mech.Payload = BuildChain(elements.Skip(binaryOpIdx + 1))?.Export() ?? "";
        }
        else
        {
            if (i < elements.Count && elements[i] is StringNode sn && TogItems.Contains(sn.Value.ToLower()))
            {
                mech.Operations.Add(sn.Value);
                mech.Payload = BuildChain(elements.Skip(i + 1))?.Export() ?? "";
            }
            else
            {
                if (mech.Operations.Count > 0) mech.Payload = BuildChain(elements.Skip(i))?.Export() ?? "";
                else mech.BaseItem = BuildChain(elements.Skip(i))?.Export() ?? "";
            }
        }
        return mech;
    }

    private static ASTNode BuildChain(IEnumerable<ASTNode> nodes)
    {
        var list = nodes.ToList();
        if (list.Count == 0) return null;
        return list.Count == 1 ? list[0] : new ChainNode { Elements = list };
    }

    public static bool IsBinaryOp(string op) => new[] { "splice", "mrg", "adj", "m", "part" }.Contains(op.ToLower());

    public string Export() => RawString;
}

// ==========================================
// ITEM DATA (Authoring Wrapper)
// ==========================================
[System.Serializable]
public class ItemData : SDData
{
    private static readonly HashSet<string> MetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "n", "tier", "img", "doc", "hsv", "hue", "hsl", "b", "draw", "rect", "thue", "p", "sidesc" };

    public int? tier;
    public bool isHidden = false;
    public string modName;
    public string modDoc;

    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();
    public List<ItemAbility> GrantedAbilities = new List<ItemAbility>();

    public int h = 0, s = 0, v = 0;
    public int? hue;
    public string hsl, p, b, rect, draw, thue, doc, sidesc;

    public static ItemData Parse(string data)
    {
        ItemData item = new ItemData();
        if (string.IsNullOrWhiteSpace(data)) return item;

        // 1. Separate Top-Level Mod Flags first (Outside the structural parentheses)
        List<string> chunks = TopLevelSplit(data.Trim(), '&');
        string itemCore = chunks[0];

        for (int c = 1; c < chunks.Count; c++)
        {
            List<string> hiddenTokens = TopLevelSplit(chunks[c], '.');
            if (hiddenTokens.Count > 0 && hiddenTokens[0].Equals("Hidden", StringComparison.OrdinalIgnoreCase))
            {
                item.isHidden = true;
                for (int j = 1; j < hiddenTokens.Count; j++)
                {
                    if (hiddenTokens[j].ToLower() == "mn" && j + 1 < hiddenTokens.Count) item.modName = hiddenTokens[++j];
                    else if (hiddenTokens[j].ToLower() == "doc" && j + 1 < hiddenTokens.Count) item.modDoc = hiddenTokens[++j];
                }
            }
        }

        // 2. Parse into AST to safely peel metadata from the right side
        ASTNode root = new ASTParser(itemCore).Parse();
        PeelMetadata(ref root, item);

        // 3. Whatever remains is strictly structural payloads (Mechanics)
        if (root == null) return item;

        // Unwrap structural parentheses one last time if they exist
        while (root is ScopeNode scope) root = scope.Content;

        List<ASTNode> mechanics = root is SequenceNode seq ? seq.Items : new List<ASTNode> { root };
        foreach (var mechNode in mechanics)
        {
            // Intercept Ability Strings
            if (mechNode is ChainNode chain)
            {
                int abIdx = chain.Elements.FindIndex(e => e is StringNode sn && sn.Value.ToLower() == "abilitydata");
                if (abIdx != -1)
                {
                    string prefix = string.Join(".", chain.Elements.Take(abIdx).Select(e => e.Export()));
                    string abilityPayload = (abIdx + 1 < chain.Elements.Count) ? chain.Elements[abIdx + 1].Export() : "";

                    item.GrantedAbilities.Add(new ItemAbility { Prefix = prefix, AbilityString = abilityPayload });
                    continue;
                }
            }

            // Route standard mechanics purely as strings
            item.Mechanics.Add(ItemMechanic.Parse(mechNode.Export()));
        }

        return item;
    }

    // Recursively searches the right-side of chains to safely strip `.n.Waning Shield` from `(((A)#B).n.Waning Shield)`
    private static void PeelMetadata(ref ASTNode node, ItemData item)
    {
        while (node != null)
        {
            if (node is ScopeNode scope)
            {
                ASTNode inner = scope.Content;
                PeelMetadata(ref inner, item);
                if (inner == null) { node = null; break; }
                if (inner != scope.Content) { scope.Content = inner; }
                break; // Stop digging; let the loop evaluate the modified scope
            }
            else if (node is ChainNode chain)
            {
                bool strippedAnything = false;
                do
                {
                    strippedAnything = false;
                    if (chain.Elements.Count >= 2)
                    {
                        string key = chain.Elements[chain.Elements.Count - 2].Export().ToLower();
                        string val = chain.Elements[chain.Elements.Count - 1].Export();
                        if (MetadataKeys.Contains(key))
                        {
                            ApplyMetadata(item, key, val);
                            chain.Elements.RemoveRange(chain.Elements.Count - 2, 2);
                            strippedAnything = true;
                        }
                    }
                } while (strippedAnything && chain.Elements.Count > 0);

                if (chain.Elements.Count == 1) { node = chain.Elements[0]; continue; }
                if (chain.Elements.Count == 0) { node = null; break; }
                break;
            }
            else if (node is SequenceNode seq)
            {
                // Unlikely to have metadata on a raw seq without parens, but just in case, strip the last item
                if (seq.Items.Count > 0)
                {
                    ASTNode last = seq.Items[seq.Items.Count - 1];
                    PeelMetadata(ref last, item);
                    if (last == null) seq.Items.RemoveAt(seq.Items.Count - 1);
                    else seq.Items[seq.Items.Count - 1] = last;

                    if (seq.Items.Count == 1) { node = seq.Items[0]; continue; }
                    if (seq.Items.Count == 0) { node = null; break; }
                }
                break;
            }
            else break;
        }
    }

    private static void ApplyMetadata(ItemData item, string key, string value)
    {
        switch (key)
        {
            case "n": item.entityName = value; break;
            case "tier": if (int.TryParse(value, out int t)) item.tier = t; break;
            case "img": item.imageOverride = value; break;
            case "doc": item.doc = value; break;
            case "sidesc": item.sidesc = value; break;
            case "hsv":
                string[] hsv = value.Split(':');
                if (hsv.Length == 3) { int.TryParse(hsv[0], out item.h); int.TryParse(hsv[1], out item.s); int.TryParse(hsv[2], out item.v); }
                break;
            case "hsl": item.hsl = value; break;
            case "hue": if (int.TryParse(value, out int hVal)) item.hue = hVal; break;
            case "p": item.p = value; break;
            case "b": item.b = value; break;
            case "rect": item.rect = value; break;
            case "draw": item.draw = value; break;
            case "thue": item.thue = value; break;
        }
    }

    public string Export()
    {
        StringBuilder sb = new StringBuilder();

        List<string> allMechanics = new List<string>();
        if (Mechanics != null) allMechanics.AddRange(Mechanics.Select(m => m.RawString)); // Use pure string
        if (GrantedAbilities != null)
        {
            foreach (var ab in GrantedAbilities)
            {
                string prefixStr = string.IsNullOrEmpty(ab.Prefix) ? "" : $"{ab.Prefix}.";
                allMechanics.Add($"{prefixStr}abilitydata.{ab.AbilityString}"); // Appends the raw string payload
            }
        }

        if (allMechanics.Count > 0)
        {
            string joinedEffects = string.Join("#", allMechanics);
            if (allMechanics.Count > 1) sb.Append($"({joinedEffects})");
            else sb.Append(joinedEffects);
        }

        if (!string.IsNullOrEmpty(entityName)) sb.Append($".n.{FormatName(entityName)}");
        if (tier.HasValue) sb.Append($".tier.{tier.Value}");
        if (!string.IsNullOrEmpty(doc)) sb.Append($".doc.{doc}");
        if (!string.IsNullOrEmpty(sidesc)) sb.Append($".sidesc.{sidesc}");
        if (!string.IsNullOrEmpty(imageOverride)) sb.Append($".img.{imageOverride}");

        if (h != 0 || s != 0 || v != 0) sb.Append($".hsv.{h}:{s}:{v}");
        else if (hue.HasValue) sb.Append($".hue.{hue.Value}");
        else if (!string.IsNullOrEmpty(hsl)) sb.Append($".hsl.{hsl}");

        if (!string.IsNullOrEmpty(p)) sb.Append($".p.{p}");
        if (!string.IsNullOrEmpty(b)) sb.Append($".b.{b}");
        if (!string.IsNullOrEmpty(rect)) sb.Append($".rect.{rect}");
        if (!string.IsNullOrEmpty(draw)) sb.Append($".draw.{draw}");
        if (!string.IsNullOrEmpty(thue)) sb.Append($".thue.{thue}");

        if (isHidden) sb.Append("&Hidden");
        if (!string.IsNullOrEmpty(modName)) sb.Append($".mn.{modName}");
        if (!string.IsNullOrEmpty(modDoc)) sb.Append($".doc.{modDoc}");

        return sb.ToString();
    }

    private static string FormatName(string name) => string.IsNullOrEmpty(name) ? "" : name.Replace(" ", "_");

    // A depth-aware split that ignores characters inside ( ) [ ] { }
    private static List<string> TopLevelSplit(string input, char separator)
    {
        List<string> result = new List<string>();
        int p = 0, b = 0, br = 0, start = 0;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') p++;
            else if (c == ')') p--;
            else if (c == '[') b++;
            else if (c == ']') b--;
            else if (c == '{') br++;
            else if (c == '}') br--;
            else if (c == separator && p == 0 && b == 0 && br == 0)
            {
                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }
        }
        result.Add(input.Substring(start));
        return result;
    }
}