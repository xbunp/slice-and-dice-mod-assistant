using System;
using System.Collections.Generic;
using System.Linq;

public abstract class BasePhase
{
    // The internal key (e.g., "ph.4", "ph.b")
    public abstract string Prefix { get; }

    // Converts the object back into the TextMod syntax string
    public abstract string ToSyntax(bool omitPrefix = false);

    // Parses the raw string (prefix already stripped) to populate this object's data
    public abstract void Parse(string rawData);
}

public class MessagePhase : BasePhase
{
    public override string Prefix => "ph.4";
    public string Message { get; set; } = "";
    public string ButtonText { get; set; } = null; // Null means default "ok"

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        return string.IsNullOrEmpty(ButtonText) ? $"{header}{Message}" : $"{header}{Message};{ButtonText}";
    }

    public override void Parse(string rawData)
    {
        var parts = rawData.Split(';');
        Message = parts[0];
        if (parts.Length > 1) ButtonText = parts[1];
    }
}

public class HeroChangePhase : BasePhase
{
    public override string Prefix => "ph.5";
    public int TargetHeroIndex { get; set; }
    public int ChangeType { get; set; } // 0 = random class, 1 = generated hero

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        return $"{header}{TargetHeroIndex}{ChangeType}";
    }

    public override void Parse(string rawData)
    {
        if (rawData.Length >= 2)
        {
            TargetHeroIndex = int.Parse(rawData[0].ToString());
            ChangeType = int.Parse(rawData[1].ToString());
        }
    }
}

public class BooleanPhase : BasePhase
{
    public override string Prefix => "ph.b";
    public string ValueName { get; set; }
    public int Threshold { get; set; }
    public BasePhase TruePhase { get; set; }
    public BasePhase FalsePhase { get; set; }

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        string tPhase = TruePhase?.ToSyntax(true) ?? "";
        string fPhase = FalsePhase?.ToSyntax(true) ?? "";
        return $"{header}{ValueName};{Threshold};{tPhase}@2{fPhase}";
    }

    public override void Parse(string rawData)
    {
        // Example raw: "Doubloon;5;4Excellent@24You need more"
        var parts = rawData.Split(new[] { ';' }, 3);
        if (parts.Length < 3) return;

        ValueName = parts[0];
        Threshold = int.Parse(parts[1]);

        var phaseParts = parts[2].Split(new[] { "@2" }, StringSplitOptions.None);
        TruePhase = PhaseParser.ParseString(phaseParts[0]);
        if (phaseParts.Length > 1) FalsePhase = PhaseParser.ParseString(phaseParts[1]);
    }
}

public class LinkedPhase : BasePhase
{
    public override string Prefix => "ph.l";
    public List<BasePhase> Phases { get; set; } = new List<BasePhase>();

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        var formattedPhases = Phases.Select(p => p.ToSyntax(true)); // Children drop "ph."
        return header + string.Join("@1", formattedPhases);
    }

    public override void Parse(string rawData)
    {
        var phaseStrings = rawData.Split(new[] { "@1" }, StringSplitOptions.None);
        foreach (var pStr in phaseStrings)
        {
            Phases.Add(PhaseParser.ParseString(pStr));
        }
    }
}

public class SeqBranch
{
    public string ButtonText { get; set; }
    public List<BasePhase> Phases { get; set; } = new List<BasePhase>();
}

public class SeqPhase : BasePhase
{
    public override string Prefix => "ph.s";
    public string InitialMessage { get; set; }
    public List<SeqBranch> Branches { get; set; } = new List<SeqBranch>();

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        string result = header + InitialMessage;

        foreach (var branch in Branches)
        {
            result += $"@1{branch.ButtonText}";
            foreach (var phase in branch.Phases)
            {
                result += $"@2{phase.ToSyntax(true)}";
            }
        }
        return result;
    }

    public override void Parse(string rawData)
    {
        // InitialMessage@1Button1@2Phase1@2Phase2@1Button2...
        var branchSplits = rawData.Split(new[] { "@1" }, StringSplitOptions.None);
        InitialMessage = branchSplits[0];

        for (int i = 1; i < branchSplits.Length; i++)
        {
            var phaseSplits = branchSplits[i].Split(new[] { "@2" }, StringSplitOptions.None);
            var newBranch = new SeqBranch { ButtonText = phaseSplits[0] };

            for (int j = 1; j < phaseSplits.Length; j++)
            {
                newBranch.Phases.Add(PhaseParser.ParseString(phaseSplits[j]));
            }
            Branches.Add(newBranch);
        }
    }
}

public class ChoicePhase : BasePhase
{
    public override string Prefix => "ph.c";
    public ChoicePhaseType ChoiceType { get; set; }
    public int Number { get; set; }
    public List<string> Rewards { get; set; } = new List<string>();

    public override string ToSyntax(bool omitPrefix = false)
    {
        string header = omitPrefix ? Prefix.Replace("ph.", "") : Prefix;
        return $"{header}{ChoiceType}#{Number};{string.Join("@3", Rewards)}";
    }

    public override void Parse(string rawData)
    {
        // Example: PointBuy#3;gb0.271@3lDruid
        var headerSplit = rawData.Split(';');
        var typeNumberSplit = headerSplit[0].Split('#');

        ChoiceType = (ChoicePhaseType)Enum.Parse(typeof(ChoicePhaseType), typeNumberSplit[0], true);
        Number = int.Parse(typeNumberSplit[1]);

        if (headerSplit.Length > 1)
        {
            Rewards = headerSplit[1].Split(new[] { "@3" }, StringSplitOptions.None).ToList();
        }
    }
}

// Simple Empty Phases
public class ResetPhase : BasePhase
{
    public override string Prefix => "ph.6";
    public override string ToSyntax(bool omitPrefix = false) => omitPrefix ? "6" : Prefix;
    public override void Parse(string rawData) { } // No data
}

public class RunEndPhase : BasePhase
{
    public override string Prefix => "ph.e";
    public override string ToSyntax(bool omitPrefix = false) => omitPrefix ? "e" : Prefix;
    public override void Parse(string rawData) { } // No data
}