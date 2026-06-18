using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

public static class ItemDomainRules
{
    public static readonly HashSet<string> ValidItemProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "k", "learn", "hat", "t", "sidepos", "tier", "n", "ritem", "ritemx", "facade",
        "mrg", "self", "m", "doc", "pertier", "part", "rditem", "unpack", "sidesc",
        "splice", "onhitdata", "triggerhpdata", "sticker", "enchant", "cast", "img",
        "hue", "hsl", "b", "draw", "hsv", "rect", "thue", "p", "summon", "cleardesc",
        "clearicon", "oi", "t1", "t2"
    };

    public static readonly HashSet<string> ValidTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "all", "self", "right5", "right3", "right2", "row", "mid2", "col", "topbot",
        "left2", "rightmost", "right", "bot", "top", "mid", "left", "k", "t"
    };

    public static readonly HashSet<string> ContainerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "triggerhpdata", "onhitdata", "learn", "unpack", "splice", "abilitydata",
        "peritem", "allitem", "alliteme", "sticker", "enchant", "cast", "mrg", "hat"
    };

    public static readonly HashSet<string> TogItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "togtime", "togtarg", "togfri", "togvis", "togeft",
        "togpip", "togkey", "togorf", "togunt",
        "togres", "togresm", "togresa", "togreso", "togresx", "togress", "togresn"
    };

    public static bool IsItemIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (token.StartsWith("ritem", StringComparison.OrdinalIgnoreCase)) return true;
        if (TogItems.Contains(token)) return true;
        return ExternalGameRegistry.IsValidItemName(token);
    }

    public static readonly HashSet<string> MechanicPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "i", "sd", "k", "t",
        "sticker", "enchant", "cast",
        "hat", "onhitdata", "triggerhpdata",
        "facade", "sidesc"
    };

    public static bool IsRepeatPrefix(string token, out int count)
    {
        count = 1;
        if (string.IsNullOrEmpty(token) || char.ToLower(token[0]) != 'x') return false;
        return int.TryParse(token.Substring(1), out count);
    }
}

[System.Serializable]
public class ItemProperty { public string Key { get; set; } public string Value { get; set; } public ItemProperty(string k, string v) { Key = k; Value = v; } }

[System.Serializable]
public class ItemMechanic
{
    public List<string> Targets = new List<string>();
    public string Prefix = "";
    public string PayloadString = "";
    public object PayloadData { get; set; } = null;
    public int Multiplier { get; set; } = 1;
    public string MergedItem { get; set; } = string.Empty;
    public string SplicedItem { get; set; } = string.Empty;
    public List<string> ChainedKeywords { get; set; } = new List<string>();
    public int RepeatTimes { get; set; } = 1;
    public bool PerTier { get; set; }
    public bool Unpack { get; set; }
    public int? PartIndex { get; set; }

    public ItemMechanic AddTarget(string target) { Targets.Add(target); return this; }
    public string Export()
    {
        List<string> parts = new List<string>();
        if (Targets.Count > 0) parts.AddRange(Targets);
        if (RepeatTimes != 1) parts.Add($"x{RepeatTimes}");
        if (PerTier) parts.Add("pertier");
        if (Unpack) parts.Add("unpack");
        if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);
        string corePayload = PayloadString;
        if (ChainedKeywords.Count > 0) corePayload += "#" + string.Join("#", ChainedKeywords);
        if (!string.IsNullOrEmpty(corePayload)) parts.Add(corePayload);
        if (PartIndex.HasValue) parts.Add($"part.{PartIndex.Value}");
        if (Multiplier != 1) parts.Add($"m{Multiplier}");
        if (!string.IsNullOrEmpty(MergedItem)) parts.Add($"mrg.{MergedItem}");
        if (!string.IsNullOrEmpty(SplicedItem)) parts.Add($"splice.{SplicedItem}");
        return string.Join(".", parts);
    }
}

public static class ExternalGameRegistry
{
    public static bool IsValidSprite(string atlasId) => true;
    public static bool IsValidKeyword(string key) => Enum.TryParse<EffectKeyword>(key, true, out _);
    public static bool IsValidAbility(string id) => BaseAbilityDatabase.ValidAbilities.Contains(id);
    public static bool IsValidItemName(string token) => Enum.TryParse<BaseItems>(token.Replace(" ", ""), true, out _);
}

[System.Serializable]
public struct ItemHsvShift
{
    public int Hue, Saturation, Value;
    public ItemHsvShift(int h, int s, int v) { Hue = Math.Clamp(h, -99, 99); Saturation = Math.Clamp(s, -99, 99); Value = Math.Clamp(v, -99, 99); }
}

[System.Serializable]
public class ItemData : SDData
{
    public List<string> GlobalTags = new List<string>();
    public int? Tier { get; set; }
    public string DocumentedDescription { get; set; } = string.Empty;
    public ItemHsvShift? HsvShift { get; set; }
    public int? SimpleHue { get; set; }
    //public string TargetedHue { get; set; } = string.Empty;
    public string PaletteOverride { get; set; } = string.Empty;
    public string BorderColorCode { get; set; } = string.Empty;
    public string UiDrawInstructions { get; set; } = string.Empty;
    public string UiRectInstructions { get; set; } = string.Empty;
    public List<string> LearnedAbilities { get; set; } = new List<string>();
    public bool ClearDescription { get; set; }
    public bool ClearIcon { get; set; }
    public List<ItemProperty> Containers = new List<ItemProperty>();
    public List<ItemMechanic> Mechanics = new List<ItemMechanic>();

    public bool IsEquippable => !string.IsNullOrEmpty(entityName) || Tier.HasValue;

    public override void Parse(string data)
    {
        GlobalTags.Clear(); PropertiesClear(); Containers.Clear(); Mechanics.Clear();
        if (string.IsNullOrWhiteSpace(data)) return;

        List<string> chunks = StaticBranchTracing.TopLevelSplit(data.Trim(), '&');
        string itemCore = chunks[0];

        for (int c = 1; c < chunks.Count; c++)
        {
            List<string> hiddenTokens = StaticBranchTracing.TopLevelSplit(chunks[c], '.');
            if (hiddenTokens.Count > 0 && (hiddenTokens[0].ToLower() == "hidden" || hiddenTokens[0].ToLower() == "temporary"))
                GlobalTags.Add(hiddenTokens[0]);
        }

        itemCore = StaticBranchTracing.StripOuterParens(itemCore);

        List<string> chains = StaticBranchTracing.TopLevelSplit(itemCore, '#');
        foreach (var chain in chains)
        {
            if (string.IsNullOrWhiteSpace(chain)) continue;
            List<string> tokens = StaticBranchTracing.TopLevelSplit(chain, '.');
            ExtractKnowledge(tokens, this);
        }
    }

    private void PropertiesClear()
    {
        thue = new Thue();
        entityName = string.Empty; imageOverride = string.Empty; Tier = null; DocumentedDescription = string.Empty;
        HsvShift = null; SimpleHue = null; PaletteOverride = string.Empty;
        BorderColorCode = string.Empty; UiDrawInstructions = string.Empty; UiRectInstructions = string.Empty;
        ClearDescription = false; ClearIcon = false; LearnedAbilities.Clear();
    }

    /*
    private void ExtractKnowledge(List<string> tokens, ItemData item)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                string inner = originalToken.Substring(1, originalToken.Length - 2);
                List<string> innerChains = StaticBranchTracing.TopLevelSplit(inner, '#');
                foreach (var chain in innerChains)
                {
                    if (string.IsNullOrWhiteSpace(chain)) continue;
                    List<string> innerTokens = StaticBranchTracing.TopLevelSplit(chain, '.');
                    ExtractKnowledge(innerTokens, item);
                }
                continue;
            }

            switch (tokenLower)
            {
                case "n": if (i + 1 < tokens.Count) item.entityName = tokens[++i]; break;
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) item.Tier = t; break;
                case "doc":
                case "sidesc": if (i + 1 < tokens.Count) item.DocumentedDescription = tokens[++i]; break;
                case "img": if (i + 1 < tokens.Count) item.imageOverride = tokens[++i]; break;
                case "hsv":
                    if (i + 1 < tokens.Count)
                    {
                        string[] hsvParts = tokens[++i].Split(':');
                        if (hsvParts.Length == 3 && int.TryParse(hsvParts[0], out int h) && int.TryParse(hsvParts[1], out int s) && int.TryParse(hsvParts[2], out int v))
                            item.HsvShift = new ItemHsvShift(h, s, v);
                    }
                    break;
                case "hue": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int hueVal)) item.SimpleHue = hueVal; break;
                case "thue": if (i + 1 < tokens.Count) item.TargetedHue = tokens[++i]; break;
                case "p": if (i + 1 < tokens.Count) item.PaletteOverride = tokens[++i]; break;
                case "b": if (i + 1 < tokens.Count) item.BorderColorCode = tokens[++i]; break;
                case "draw": if (i + 1 < tokens.Count) item.UiDrawInstructions = tokens[++i]; break;
                case "rect": if (i + 1 < tokens.Count) item.UiRectInstructions = tokens[++i]; break;
                case "learn": if (i + 1 < tokens.Count) item.LearnedAbilities.Add(tokens[++i]); break;
                case "cleardesc": item.ClearDescription = true; break;
                case "clearicon": item.ClearIcon = true; break;

                default:
                    if (TryProcessGenericContainer(tokens, ref i, tokenLower, originalToken)) { }
                    else if (IsMechanicTriggerToken(tokenLower)) ProcessMechanicChain(tokens, ref i, originalToken);
                    break;
            }
        }
    }
    */

    private void ExtractKnowledge(List<string> tokens, ItemData item)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            string tokenLower = tokens[i].ToLower();
            string originalToken = tokens[i];

            if (originalToken.StartsWith("(") && originalToken.EndsWith(")"))
            {
                ProcessRecursiveParentheses(originalToken, (innerTokens) => ExtractKnowledge(innerTokens, item));
                continue;
            }

            if (TryProcessCommonMetadata(tokens, ref i, tokenLower))
            {
                // Sync specific ItemData variables with the parsed base class values
                if (tokenLower == "hsv") item.HsvShift = new ItemHsvShift(h, s, v);
                else if (tokenLower == "hue") item.SimpleHue = hue;

                else if (tokenLower == "thue") item.thue = UnpackTHue(tokens[i]);

                else if (tokenLower == "p") item.PaletteOverride = p;
                else if (tokenLower == "b") item.BorderColorCode = b;
                else if (tokenLower == "draw") item.UiDrawInstructions = draw;
                else if (tokenLower == "rect") item.UiRectInstructions = rect;
                else if (tokenLower == "doc") item.DocumentedDescription = doc;
                continue;
            }

            switch (tokenLower)
            {
                case "tier": if (i + 1 < tokens.Count && int.TryParse(tokens[++i], out int t)) item.Tier = t; break;
                case "sidesc": if (i + 1 < tokens.Count) item.DocumentedDescription = tokens[++i]; break;
                case "learn": if (i + 1 < tokens.Count) item.LearnedAbilities.Add(tokens[++i]); break;
                case "cleardesc": item.ClearDescription = true; break;
                case "clearicon": item.ClearIcon = true; break;

                default:
                    if (TryProcessGenericContainer(tokens, ref i, tokenLower, originalToken)) { }
                    else if (IsMechanicTriggerToken(tokenLower)) ProcessMechanicChain(tokens, ref i, originalToken);
                    break;
            }
        }
    }

    /*
    private void ProcessMechanicChain(List<string> tokens, ref int i, string initialToken)
    {
        ItemMechanic mech = new ItemMechanic();

        while (i < tokens.Count)
        {
            string originalToken = tokens[i];
            string tLower = originalToken.ToLower();

            if (ItemDomainRules.MechanicPrefixes.Contains(tLower))
            {
                mech.Prefix = tLower;
                i++;

                List<string> payloadTokens = new List<string>();
                while (i < tokens.Count)
                {
                    string peek = tokens[i].ToLower();
                    if (peek == "part" || (peek.StartsWith("m") && int.TryParse(peek.Substring(1), out _)) || peek == "mrg" || peek == "splice")
                        break;
                    payloadTokens.Add(tokens[i]);
                    i++;
                }
                mech.PayloadString = string.Join(".", payloadTokens);
                i--;
                break;
            }
            else if (ItemDomainRules.ValidTargets.Contains(tLower))
            {
                mech.AddTarget(originalToken);
            }
            else if (ItemDomainRules.IsRepeatPrefix(tLower, out int reps))
            {
                mech.RepeatTimes = reps;
            }
            else if (tLower == "pertier") mech.PerTier = true;
            else if (tLower == "unpack") mech.Unpack = true;
            else
            {
                List<string> payloadTokens = new List<string>();
                payloadTokens.Add(originalToken);
                i++;

                while (i < tokens.Count)
                {
                    string peek = tokens[i].ToLower();
                    if (peek == "part" || (peek.StartsWith("m") && int.TryParse(peek.Substring(1), out _)) || peek == "mrg" || peek == "splice")
                        break;
                    payloadTokens.Add(tokens[i]);
                    i++;
                }
                mech.PayloadString = string.Join(".", payloadTokens);
                i--;
                break;
            }
            i++;
        }

        while (i + 1 < tokens.Count)
        {
            string nextTokenLower = tokens[i + 1].ToLower();

            if (nextTokenLower == "part" && i + 2 < tokens.Count)
            {
                if (int.TryParse(tokens[i + 2], out int pIdx)) { mech.PartIndex = pIdx; i += 2; }
                else break;
            }
            else if (nextTokenLower.StartsWith("m") && nextTokenLower.Length > 1 && int.TryParse(nextTokenLower.Substring(1), out int mult))
            {
                mech.Multiplier = mult; i++;
            }
            else if (nextTokenLower == "mrg" && i + 2 < tokens.Count) { mech.MergedItem = tokens[i + 2]; i += 2; }
            else if (nextTokenLower == "splice" && i + 2 < tokens.Count) { mech.SplicedItem = tokens[i + 2]; i += 2; }
            else break;
        }

        AssignDomainPayload(mech);
        Mechanics.Add(mech);
    }
    */

    private bool TryProcessGenericContainer(List<string> tokens, ref int i, string tokenLower, string originalToken)
    {
        if (ItemDomainRules.ContainerKeys.Contains(tokenLower) && !ItemDomainRules.MechanicPrefixes.Contains(tokenLower))
        {
            if (i + 1 < tokens.Count)
            {
                Containers.Add(new ItemProperty(originalToken, tokens[++i]));
                return true;
            }
        }
        return false;
    }

    private bool IsMechanicTriggerToken(string token)
    {
        return ItemDomainRules.MechanicPrefixes.Contains(token) || token == "pertier" || token == "unpack" ||
               ItemDomainRules.ValidTargets.Contains(token) || ItemDomainRules.IsItemIdentifier(token) ||
               ItemDomainRules.IsRepeatPrefix(token, out _);
    }

    private void AssignDomainPayload(ItemMechanic mech)
    {
        if (string.IsNullOrEmpty(mech.PayloadString)) return;
        string core = StaticBranchTracing.StripOuterParens(mech.PayloadString);

        if (mech.Prefix == "hat")
        {
            if (StaticBranchTracing.IsMonsterEntity(core)) { MonsterData monster = new MonsterData(); monster.Parse(core); mech.PayloadData = monster; }
            else { HeroData hero = new HeroData(); hero.Parse(core); mech.PayloadData = hero; }
        }
        else if (mech.Prefix == "onhitdata" || mech.Prefix == "triggerhpdata") { TriggerHPData thp = new TriggerHPData(); thp.Parse(core); mech.PayloadData = thp; }
        else if (mech.Prefix == "enchant" || mech.Prefix == "self") { ModifierData mod = new ModifierData(); mod.Parse(core); mech.PayloadData = mod; }
        else if (mech.Prefix == "cast" || mech.Prefix == "abilitydata") { mech.PayloadData = AbilityData.CreateSpellOrTactic(core); }
        else if (mech.Prefix == "sticker") { ItemData item = new ItemData(); item.Parse(core); mech.PayloadData = item; }
        else if (mech.Prefix == "t")
        {
            if (StaticBranchTracing.IsMonsterEntity(core))
            {
                MonsterData monster = new MonsterData(); monster.Parse(core); mech.PayloadData = monster;
            }
            else if (core.StartsWith("jinx.", StringComparison.OrdinalIgnoreCase))
            {
                string modifierCore = StaticBranchTracing.StripOuterParens(core.Substring(5).Trim());
                ModifierData mod = new ModifierData(); mod.Parse(modifierCore); mech.PayloadData = mod;
            }
            else
            {
                HeroData hero = new HeroData(); hero.Parse(core); mech.PayloadData = hero;
            }
        }
        else if (mech.Prefix == "i" || string.IsNullOrEmpty(mech.Prefix))
        {
            if (mech.PayloadString.StartsWith("(")) { ItemData item = new ItemData(); item.Parse(core); mech.PayloadData = item; }
        }
    }

    public override string Export()
    {
        List<string> chainParts = new List<string>();
        if (!string.IsNullOrEmpty(entityName)) chainParts.Add($"n.{entityName}");
        if (Tier.HasValue) chainParts.Add($"tier.{Tier.Value}");
        if (!string.IsNullOrEmpty(DocumentedDescription)) chainParts.Add($"doc.{DocumentedDescription}");
        if (!string.IsNullOrEmpty(imageOverride) && imageOverride != "None") chainParts.Add($"img.{imageOverride}");
        if (HsvShift.HasValue) chainParts.Add($"hsv.{HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");
        if (SimpleHue.HasValue) chainParts.Add($"hue.{SimpleHue.Value}");

        //if (!string.IsNullOrEmpty(TargetedHue)) chainParts.Add($"thue.{TargetedHue}");
        if (this.thue != null && this.thue.colorOffset != 0) chainParts.Add($".{PackTHue(this.thue)}");

        if (!string.IsNullOrEmpty(PaletteOverride)) chainParts.Add($"p.{PaletteOverride}");
        if (!string.IsNullOrEmpty(BorderColorCode)) chainParts.Add($"b.{BorderColorCode}");
        if (!string.IsNullOrEmpty(UiDrawInstructions)) chainParts.Add($"draw.{UiDrawInstructions}");
        if (!string.IsNullOrEmpty(UiRectInstructions)) chainParts.Add($"rect.{UiRectInstructions}");
        if (ClearDescription) chainParts.Add("cleardesc");
        if (ClearIcon) chainParts.Add("clearicon");

        foreach (var cont in Containers) chainParts.Add($"{cont.Key}.({cont.Value})");
        foreach (var mech in Mechanics) chainParts.Add(mech.Export());

        StringBuilder sb = new StringBuilder(string.Join(".", chainParts));
        foreach (var tag in GlobalTags) sb.Append($"&{tag}");
        return sb.ToString();
    }

    public void DebugContentsToConsole(string indent = "")
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"{indent}--- ITEM DATA DEBUG ---");
        sb.AppendLine($"{indent}Name: {entityName}");
        sb.AppendLine($"{indent}Tier: {Tier}");
        string displayValue = !string.IsNullOrEmpty(imageOverride) && imageOverride.Length > 32 ? "<base64 string img>" : imageOverride;
        sb.AppendLine($"{indent}ImageRef: {displayValue}");
        if (HsvShift.HasValue) sb.AppendLine($"{indent}HsvShift: {HsvShift.Value.Hue}:{HsvShift.Value.Saturation}:{HsvShift.Value.Value}");

        sb.AppendLine($"{indent}\n{indent}Mechanics ({Mechanics.Count}):");
        for (int i = 0; i < Mechanics.Count; i++)
        {
            var m = Mechanics[i];
            sb.AppendLine($"{indent}  [{i}] Targets: [{string.Join(", ", m.Targets)}] | Prefix: '{m.Prefix}'");
            sb.AppendLine($"{indent}      Payload: '{m.PayloadString}'");

            if (m.PayloadData is ItemData nestedItem)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked ItemData!]");
                nestedItem.DebugContentsToConsole(indent + "        ");
            }
            else if (m.PayloadData is AbilityData ad)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked AbilityData!]");
                ad.DebugAbilityCompact(indent + "        ");
            }
            else if (m.PayloadData is HeroData hd)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked HeroData!]");
                hd.DebugContentsToConsoleCompact(indent + "        ");
            }
            else if (m.PayloadData is MonsterData md)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked MonsterData!]");
                md.DebugContentsToConsoleCompact(indent + "        ");
            }
            else if (m.PayloadData is ModifierData mod)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked ModifierData!]");
                mod.DebugContentsToConsole(indent + "        ");
            }
            else if (m.PayloadData != null)
            {
                sb.AppendLine($"{indent}      [✓ Unpacked {m.PayloadData.GetType().Name}!]");
            }

            if (m.Multiplier != 1 || !string.IsNullOrEmpty(m.MergedItem) || !string.IsNullOrEmpty(m.SplicedItem) || m.PartIndex.HasValue)
                sb.AppendLine($"{indent}      Suffixes -> m:{m.Multiplier}, mrg:{m.MergedItem}, splice:{m.SplicedItem}, part:{m.PartIndex}");
        }
        UnityEngine.Debug.Log(sb.ToString());
    }

    // Helper method to safely collect forward payload tokens without suffix collisions
    private string BuildPayloadString(List<string> tokens, ref int i)
    {
        List<string> payloadTokens = new List<string>();
        while (i < tokens.Count)
        {
            string peek = tokens[i].ToLower();
            if (peek == "part" || (peek.StartsWith("m") && int.TryParse(peek.Substring(1), out _)) || peek == "mrg" || peek == "splice")
                break;
            payloadTokens.Add(tokens[i]);
            i++;
        }
        i--; // Backtrack so that the suffix parser can evaluate the boundary token
        return string.Join(".", payloadTokens);
    }

    private void ProcessMechanicChain(List<string> tokens, ref int i, string initialToken)
    {
        ItemMechanic mech = new ItemMechanic();

        while (i < tokens.Count)
        {
            string originalToken = tokens[i];
            string tLower = originalToken.ToLower();

            if (ItemDomainRules.MechanicPrefixes.Contains(tLower))
            {
                mech.Prefix = tLower;
                i++; // Move past the prefix
                mech.PayloadString = BuildPayloadString(tokens, ref i);
                break; // Exits the mechanic loop immediately once the payload is assigned
            }
            else if (ItemDomainRules.ValidTargets.Contains(tLower))
            {
                mech.AddTarget(originalToken);
            }
            else if (ItemDomainRules.IsRepeatPrefix(tLower, out int reps))
            {
                mech.RepeatTimes = reps;
            }
            else if (tLower == "pertier") mech.PerTier = true;
            else if (tLower == "unpack") mech.Unpack = true;
            else
            {
                // The current token is the start of the payload
                List<string> payloadTokens = new List<string> { originalToken };
                i++; // Move past the first payload token

                string subsequent = BuildPayloadString(tokens, ref i);
                if (!string.IsNullOrEmpty(subsequent))
                {
                    payloadTokens.Add(subsequent);
                }

                mech.PayloadString = string.Join(".", payloadTokens);
                break; // Exits the mechanic loop immediately once the payload is assigned
            }
            i++;
        }

        // Process trailing suffixes (part, multiplier, mrg, splice)
        while (i + 1 < tokens.Count)
        {
            string nextTokenLower = tokens[i + 1].ToLower();

            if (nextTokenLower == "part" && i + 2 < tokens.Count)
            {
                if (int.TryParse(tokens[i + 2], out int pIdx)) { mech.PartIndex = pIdx; i += 2; }
                else break;
            }
            else if (nextTokenLower.StartsWith("m") && nextTokenLower.Length > 1 && int.TryParse(nextTokenLower.Substring(1), out int mult))
            {
                mech.Multiplier = mult; i++;
            }
            else if (nextTokenLower == "mrg" && i + 2 < tokens.Count) { mech.MergedItem = tokens[i + 2]; i += 2; }
            else if (nextTokenLower == "splice" && i + 2 < tokens.Count) { mech.SplicedItem = tokens[i + 2]; i += 2; }
            else break;
        }

        AssignDomainPayload(mech);
        Mechanics.Add(mech);
    }
}