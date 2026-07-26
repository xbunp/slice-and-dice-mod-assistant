using System;
using System.Collections.Generic;
/*
public class ParseRule
{
    //public string[] Keys { get; init; }
    //public bool IsBoundary { get; init; }
    //public Action<TokenStream, EntityData> Execute { get; init; }
}

public static class RuleRegistry
{
    private static readonly Dictionary<string, ParseRule> Rules = new(StringComparer.OrdinalIgnoreCase);

    static RuleRegistry()
    {
        // -------------------------------------------------------------------
        // SINGLE SOURCE OF TRUTH FOR ALL KEYS
        // Adding a key here sets its boundary rule AND its behavior together.
        // -------------------------------------------------------------------

        // 1. SIMPLE VALUES (Consume key -> parse value)
        BindValue(new[] { "hp" }, isBoundary: true, (target, val) => target.hp = int.Parse(val));
        //BindValue(new[] { "tier" }, isBoundary: false, (target, val) => target.tier = int.Parse(val));
        //BindValue(new[] { "n", "name" }, isBoundary: false, (target, val) => target.name = val);

        // 2. FLAGS / TOGGLES (Just consume key)
        //BindFlag(new[] { "togtime", "togtarg", "togfri" }, isBoundary: false, (target, key) => target.Flags.Add(key));
        //BindFlag(new[] { "cleardesc", "clearicon" }, isBoundary: false, (target, key) => target.ClearVisuals());

        // 3. COMPLEX / PAYLOAD RULES (Inline inline lambda for unique logic)
        Register(new ParseRule
        {
            Keys = new[] { "onhitdata", "triggerhpdata" },
            IsBoundary = true,
            Execute = (stream, target) => {
                string key = stream.Consume();
                string payload = stream.Consume(); // Reads payload
                //target.AddTrigger(key, payload);
            }
        });
    }

    // --- Helpers to eliminate boilerplate ---
    private static void BindValue(string[] keys, bool isBoundary, Action<EntityData, string> assign)
    {
        Register(new ParseRule
        {
            Keys = keys,
            IsBoundary = isBoundary,
            Execute = (stream, target) => { stream.Consume(); assign(target, stream.Consume()); }
        });
    }

    private static void BindFlag(string[] keys, bool isBoundary, Action<EntityData, string> action)
    {
        Register(new ParseRule
        {
            Keys = keys,
            IsBoundary = isBoundary,
            Execute = (stream, target) => action(target, stream.Consume())
        });
    }

    private static void Register(ParseRule rule)
    {
        foreach (var key in rule.Keys) Rules[key] = rule;
    }

    // --- Public API for Parser ---
    public static bool TryGet(string token, out ParseRule rule) => Rules.TryGetValue(token, out rule);
    public static bool IsBoundary(string token) => Rules.TryGetValue(token, out var r) && r.IsBoundary;
}
*/

/*

while (!stream.IsEOF)
{
    string token = stream.Peek();

    if (RuleRegistry.TryGet(token, out ParseRule rule))
    {
        rule.Execute(stream, this);
    }
    else
    {
        stream.Consume(); // Unknown token, skip
    }
}

 * */