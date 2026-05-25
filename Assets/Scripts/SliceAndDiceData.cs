using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SDColors;

public static class SDData
{
    public static readonly string[] UnusuablePortraits = { "Glitch", "Error", "Totem" };
}

public enum KeywordColor
{
    Green,
    Purple,
    Grey,
    Yellow,
    Light,
    Red,
    Blue,
    Orange,
    Pink
}

public static class EffectKeywordColors
{
    /*
    // Color definitions
    public static readonly Color Green = new Color(0.3f, 0.8f, 0.3f);     // Readable green
    public static readonly Color Purple = new Color(0.65f, 0.35f, 0.85f);  // Soft purple
    public static readonly Color Grey = new Color(0.65f, 0.65f, 0.65f);    // Medium-light grey
    public static readonly Color Yellow = new Color(0.95f, 0.75f, 0.1f);   // Warm yellow/gold
    public static readonly Color Light = new Color(0.95f, 0.95f, 0.95f);     // Off-white/light blue-grey
    public static readonly Color Red = new Color(0.9f, 0.3f, 0.3f);        // Soft red
    public static readonly Color Blue = new Color(0.3f, 0.6f, 0.95f);      // Clear blue
    public static readonly Color Orange = new Color(0.95f, 0.5f, 0.1f);    // Warm orange
    public static readonly Color Pink = new Color(0.95f, 0.45f, 0.7f);     // Rose pink
    */

    public static readonly Color Green = FromHex("3c8939");     // Readable green
    public static readonly Color Purple = FromHex("6f489c");    // Soft purple
    public static readonly Color Grey = FromHex("62757c");      // Medium-light grey
    public static readonly Color Yellow = FromHex("b09e14");    // Warm yellow/gold
    public static readonly Color Light = FromHex("e9e6b9");     // Off-white/light blue-grey
    public static readonly Color Red = FromHex("c12529");       // Soft red
    public static readonly Color Blue = FromHex("1c8cb5");      // Clear blue
    public static readonly Color Orange = FromHex("ba6022");    // Warm orange
    public static readonly Color Pink = FromHex("d629f7");      // Rose pink

    // Complete dictionary mapping every keyword to its corresponding Unity Color
    public static readonly Dictionary<EffectKeyword, Color> Map = new Dictionary<EffectKeyword, Color>
    {
        { EffectKeyword.Acidic, Green },
        { EffectKeyword.Affected, Purple },
        { EffectKeyword.Annul, Grey },
        { EffectKeyword.Antideathwish, Purple },
        { EffectKeyword.Antidog, Yellow },
        { EffectKeyword.Antiengage, Yellow },
        { EffectKeyword.Antipair, Light },
        { EffectKeyword.Antipristine, Light },
        { EffectKeyword.Armoured, Light },
        { EffectKeyword.Bloodlust, Red },
        { EffectKeyword.Boned, Light },
        { EffectKeyword.Boost, Blue },
        { EffectKeyword.Buffed, Light },
        { EffectKeyword.Bully, Orange },
        { EffectKeyword.Cantrip, Pink },
        { EffectKeyword.Century, Grey },
        { EffectKeyword.Chain, Pink },
        { EffectKeyword.Channel, Green },
        { EffectKeyword.Charged, Blue },
        { EffectKeyword.Cleanse, Light },
        { EffectKeyword.Cleave, Light },
        { EffectKeyword.Cooldown, Orange },
        { EffectKeyword.Copycat, Light },
        { EffectKeyword.Critical, Yellow },
        { EffectKeyword.Cruel, Orange },
        { EffectKeyword.Cruesh, Orange },
        { EffectKeyword.Damage, Orange },
        { EffectKeyword.Death, Light },
        { EffectKeyword.Deathlust, Purple },
        { EffectKeyword.Deathwish, Purple },
        { EffectKeyword.Decay, Purple },
        { EffectKeyword.Defy, Yellow },
        { EffectKeyword.Dejavu, Pink },
        { EffectKeyword.Deplete, Orange },
        { EffectKeyword.Descend, Light },
        { EffectKeyword.Dispel, Pink },
        { EffectKeyword.Dog, Yellow },
        { EffectKeyword.Dogma, Yellow },
        { EffectKeyword.DoubDiff, Yellow },
        { EffectKeyword.Doubled, Blue },
        { EffectKeyword.Doublegrowth, Green },
        { EffectKeyword.DoubleUse, Orange },
        { EffectKeyword.Duegue, Blue },
        { EffectKeyword.Duel, Blue },
        { EffectKeyword.Duplicate, Blue },
        { EffectKeyword.Echo, Blue },
        { EffectKeyword.Ego, Yellow },
        { EffectKeyword.Eliminate, Red },
        { EffectKeyword.Enduring, Grey },
        { EffectKeyword.Engage, Yellow },
        { EffectKeyword.Engarged, Yellow },
        { EffectKeyword.Engine, Yellow },
        { EffectKeyword.Equipped, Grey },
        { EffectKeyword.Era, Blue },
        { EffectKeyword.Evil, Red },
        { EffectKeyword.Exert, Grey },
        { EffectKeyword.Fashionable, Blue },
        { EffectKeyword.Fault, Blue },
        { EffectKeyword.Fierce, Yellow },
        { EffectKeyword.First, Light },
        { EffectKeyword.Fizz, Blue },
        { EffectKeyword.Flesh, Red },
        { EffectKeyword.Fluctuate, Blue },
        { EffectKeyword.Flurry, Orange },
        { EffectKeyword.Focus, Orange },
        { EffectKeyword.Fumble, Grey },
        { EffectKeyword.Future, Blue },
        { EffectKeyword.Generous, Light },
        { EffectKeyword.Groooooowth, Green },
        { EffectKeyword.Groupdecay, Purple },
        { EffectKeyword.Groupexert, Purple },
        { EffectKeyword.Groupgroooooowth, Green },
        { EffectKeyword.Groupgrowth, Green },
        { EffectKeyword.GroupsingleUse, Orange },
        { EffectKeyword.Growth, Green },
        { EffectKeyword.Guilt, Orange },
        { EffectKeyword.Halvedeathwish, Purple },
        { EffectKeyword.Halveduel, Blue },
        { EffectKeyword.Halveengage, Yellow },
        { EffectKeyword.Heal, Red },
        { EffectKeyword.Heavy, Yellow },
        { EffectKeyword.Hoard, Orange },
        { EffectKeyword.Hyena, Yellow },
        { EffectKeyword.Hyperboned, Light },
        { EffectKeyword.Hypergrowth, Green },
        { EffectKeyword.Hyperuse, Orange },
        { EffectKeyword.Hypnotise, Orange },
        { EffectKeyword.Inflictboned, Light },
        { EffectKeyword.Inflictdeath, Light },
        { EffectKeyword.Inflictexert, Purple },
        { EffectKeyword.Inflictinflictdeath, Light },
        { EffectKeyword.Inflictinflictnothing, Grey },
        { EffectKeyword.Inflictnothing, Grey },
        { EffectKeyword.Inflictpain, Red },
        { EffectKeyword.Inflictselfshield, Light },
        { EffectKeyword.InflictsingleUse, Orange },
        { EffectKeyword.Inspired, Green },
        { EffectKeyword.Lead, Yellow },
        { EffectKeyword.Lucky, Red },
        { EffectKeyword.Manacost, Purple },
        { EffectKeyword.ManaGain, Blue },
        { EffectKeyword.Mandatory, Red },
        { EffectKeyword.Minusera, Blue },
        { EffectKeyword.Minusflesh, Red },
        { EffectKeyword.Moxie, Yellow },
        { EffectKeyword.Nothing, Grey },
        { EffectKeyword.Onesie, Grey },
        { EffectKeyword.Overdog, Yellow },
        { EffectKeyword.Pain, Red },
        { EffectKeyword.Pair, Light },
        { EffectKeyword.Patient, Light },
        { EffectKeyword.Paxin, Light },
        { EffectKeyword.PermaBoost, Pink },
        { EffectKeyword.Permissive, Pink },
        { EffectKeyword.Petrify, Yellow },
        { EffectKeyword.Picky, Blue },
        { EffectKeyword.Plague, Green },
        { EffectKeyword.Plus, Grey },
        { EffectKeyword.Poison, Green },
        { EffectKeyword.Possessed, Purple },
        { EffectKeyword.Potion, Purple },
        { EffectKeyword.Pristeel, Light },
        { EffectKeyword.Pristine, Light },
        { EffectKeyword.Priswish, Light },
        { EffectKeyword.QuadUse, Orange },
        { EffectKeyword.Quin, Light },
        { EffectKeyword.Rainbow, Light },
        { EffectKeyword.Rampage, Purple },
        { EffectKeyword.Ranged, Light },
        { EffectKeyword.Reborn, Yellow },
        { EffectKeyword.Regen, Red },
        { EffectKeyword.Removed, Light },
        { EffectKeyword.Repel, Orange },
        { EffectKeyword.Rescue, Yellow },
        { EffectKeyword.Resilient, Orange },
        { EffectKeyword.Resonate, Pink },
        { EffectKeyword.RevDiff, Red },
        { EffectKeyword.Rite, Green },
        { EffectKeyword.Run, Yellow },
        { EffectKeyword.Scared, Purple },
        { EffectKeyword.Selfcleanse, Light },
        { EffectKeyword.Selfheal, Red },
        { EffectKeyword.Selfpetrify, Yellow },
        { EffectKeyword.Selfpoison, Green },
        { EffectKeyword.Selfregen, Red },
        { EffectKeyword.Selfrepel, Orange },
        { EffectKeyword.Selfshield, Light },
        { EffectKeyword.Selfvulnerable, Orange },
        { EffectKeyword.Sept, Light },
        { EffectKeyword.Serrated, Red },
        { EffectKeyword.Share, Light },
        { EffectKeyword.Shield, Grey },
        { EffectKeyword.Shifter, Pink },
        { EffectKeyword.SingleCast, Purple },
        { EffectKeyword.SingleUse, Orange },
        { EffectKeyword.Sixth, Orange },
        { EffectKeyword.Skill, Light },
        { EffectKeyword.Sloth, Light },
        { EffectKeyword.Smith, Light },
        { EffectKeyword.Spellrescue, Yellow },
        { EffectKeyword.Sprint, Orange },
        { EffectKeyword.Spy, Grey },
        { EffectKeyword.Squared, Pink },
        { EffectKeyword.Squish, Yellow },
        { EffectKeyword.Stasis, Blue },
        { EffectKeyword.Steel, Light },
        { EffectKeyword.Step, Grey },
        { EffectKeyword.Sticky, Purple },
        { EffectKeyword.Swapcruel, Orange },
        { EffectKeyword.Swapdeathwish, Purple },
        { EffectKeyword.Swapengage, Yellow },
        { EffectKeyword.Swapterminal, Purple },
        { EffectKeyword.Tactical, Orange },
        { EffectKeyword.Tall, Orange },
        { EffectKeyword.Terminal, Purple },
        { EffectKeyword.Threesy, Orange },
        { EffectKeyword.Treble, Light },
        { EffectKeyword.Trill, Light },
        { EffectKeyword.Trio, Light },
        { EffectKeyword.Underdog, Yellow },
        { EffectKeyword.Undergrowth, Green },
        { EffectKeyword.Underocus, Yellow },
        { EffectKeyword.Unusable, Grey },
        { EffectKeyword.Uppercut, Orange },
        { EffectKeyword.Vigil, Light },
        { EffectKeyword.Vitality, Light },
        { EffectKeyword.Vulnerable, Orange },
        { EffectKeyword.Weaken, Green },
        { EffectKeyword.Wham, Light },
        { EffectKeyword.Wither, Green },
        { EffectKeyword.Zeroed, Grey }
    };
}

public enum EffectKeyword
{
    Acidic,
    Affected,
    Annul,
    Antideathwish,
    Antidog,
    Antiengage,
    Antipair,
    Antipristine,
    Armoured,
    Bloodlust,
    Boned,
    Boost,
    Buffed,
    Bully,
    Cantrip,
    Century,
    Chain,
    Channel,
    Charged,
    Cleanse,
    Cleave,
    Cooldown,
    Copycat,
    Critical,
    Cruel,
    Cruesh,
    Damage,
    Death,
    Deathlust,
    Deathwish,
    Decay,
    Defy,
    Dejavu,
    Deplete,
    Descend,
    Dispel,
    Dog,
    Dogma,
    DoubDiff,
    Doubled,
    Doublegrowth,
    DoubleUse,
    Duegue,
    Duel,
    Duplicate,
    Echo,
    Ego,
    Eliminate,
    Enduring,
    Engage,
    Engarged,
    Engine,
    Equipped,
    Era,
    Evil,
    Exert,
    Fashionable,
    Fault,
    Fierce,
    First,
    Fizz,
    Flesh,
    Fluctuate,
    Flurry,
    Focus,
    Fumble,
    Future,
    Generous,
    Groooooowth,
    Groupdecay,
    Groupexert,
    Groupgroooooowth,
    Groupgrowth,
    GroupsingleUse,
    Growth,
    Guilt,
    Halvedeathwish,
    Halveduel,
    Halveengage,
    Heal,
    Heavy,
    Hoard,
    Hyena,
    Hyperboned,
    Hypergrowth,
    Hyperuse,
    Hypnotise,
    Inflictboned,
    Inflictdeath,
    Inflictexert,
    Inflictinflictdeath,
    Inflictinflictnothing,
    Inflictnothing,
    Inflictpain,
    Inflictselfshield,
    InflictsingleUse,
    Inspired,
    Lead,
    Lucky,
    Manacost,
    ManaGain,
    Mandatory,
    Minusera,
    Minusflesh,
    Moxie,
    Nothing,
    Onesie,
    Overdog,
    Pain,
    Pair,
    Patient,
    Paxin,
    PermaBoost,
    Permissive,
    Petrify,
    Picky,
    Plague,
    Plus,
    Poison,
    Possessed,
    Potion,
    Pristeel,
    Pristine,
    Priswish,
    QuadUse,
    Quin,
    Rainbow,
    Rampage,
    Ranged,
    Reborn,
    Regen,
    Removed,
    Repel,
    Rescue,
    Resilient,
    Resonate,
    RevDiff,
    Rite,
    Run,
    Scared,
    Selfcleanse,
    Selfheal,
    Selfpetrify,
    Selfpoison,
    Selfregen,
    Selfrepel,
    Selfshield,
    Selfvulnerable,
    Sept,
    Serrated,
    Share,
    Shield,
    Shifter,
    SingleCast,
    SingleUse,
    Sixth,
    Skill,
    Sloth,
    Smith,
    Spellrescue,
    Sprint,
    Spy,
    Squared,
    Squish,
    Stasis,
    Steel,
    Step,
    Sticky,
    Swapcruel,
    Swapdeathwish,
    Swapengage,
    Swapterminal,
    Tactical,
    Tall,
    Terminal,
    Threesy,
    Treble,
    Trill,
    Trio,
    Underdog,
    Undergrowth,
    Underocus,
    Unusable,
    Uppercut,
    Vigil,
    Vitality,
    Vulnerable,
    Weaken,
    Wham,
    Wither,
    Zeroed
}
public enum HeroColorOption
{
    Orange, Yellow, Grey, Red, Blue, Green,
    Purple, Cyan, DarkBlue, Black, White, Magenta,
    Pink, Violet, Brown, DarkBrown, Lime, DarkGreen,
    StrongOrange, StrongYellow, LightGrey, StrongRed, StrongGreen,
    WeakGreen, WeakBlue
}

public static class SDColors
{


    public static string[] GetFormattedColorNames()
    {
        // We iterate through the enum to ensure the dropdown order 
        // matches the order defined in ColorOption
        return Enum.GetValues(typeof(HeroColorOption))
                   .Cast<HeroColorOption>()
                   .Select(option => HeroColorNames[GetCode(option)])
                   .ToArray();
    }

    // Maps Enum to the "code" letter
    public static string GetCode(HeroColorOption option)
    {
        return option switch
        {
            HeroColorOption.Orange => "o",
            HeroColorOption.Yellow => "y",
            HeroColorOption.Grey => "g",
            HeroColorOption.Red => "r",
            HeroColorOption.Blue => "b",
            HeroColorOption.Green => "n",
            HeroColorOption.Purple => "p",
            HeroColorOption.Cyan => "c",
            HeroColorOption.DarkBlue => "s",
            HeroColorOption.Black => "d",
            HeroColorOption.White => "w",
            HeroColorOption.Magenta => "k",
            HeroColorOption.Pink => "u",
            HeroColorOption.Violet => "v",
            HeroColorOption.Brown => "h",
            HeroColorOption.DarkBrown => "m",
            HeroColorOption.Lime => "l",
            HeroColorOption.DarkGreen => "t",
            HeroColorOption.StrongOrange => "z",
            HeroColorOption.StrongYellow => "a",
            HeroColorOption.LightGrey => "i",
            HeroColorOption.StrongRed => "q",
            HeroColorOption.StrongGreen => "x",
            HeroColorOption.WeakGreen => "f",
            HeroColorOption.WeakBlue => "j",
            _ => "w"
        };
    }

    // Maps Enum to the actual Unity Color
    public static Color GetColor(HeroColorOption option)
    {
        string code = GetCode(option);
        // Reuse the dictionary logic from the previous step
        return GetHeroColor(code);
    }

    // Dictionary storing "code" as key and "Nicename" as value
    private static readonly Dictionary<string, string> HeroColorNames = new Dictionary<string, string>
    {
        { "o", "O: Orange" },
        { "y", "Y: Yellow" },
        { "g", "G: Grey" },
        { "r", "R: Red" },
        { "b", "B: Blue" },
        { "n", "N: Green" },
        { "p", "P: Purple" },
        { "c", "C: Cyan" },
        { "s", "S: Dark Blue" },
        { "d", "D: Black" },
        { "w", "W: White" },
        { "k", "K: Magenta" },
        { "u", "U: Pink" },
        { "v", "V: Violet" },
        { "h", "H: Brown" },
        { "m", "M: Dark Brown" },
        { "l", "L: Lime" },
        { "t", "T: Dark Green" },
        { "z", "Z: Strong Orange" },
        { "a", "A: Strong Yellow" },
        { "i", "I: Light Grey" },
        { "q", "Q: Strong Red" },
        { "x", "X: Strong Green" },
        { "f", "F: Weak Green" },
        { "j", "J: Weak Blue" }
    };

    private static readonly Dictionary<string, Color> HeroColorHexMap = new Dictionary<string, Color>
    {
        { "o", FromHex("c45e16") },    // Orange
        { "y", FromHex("b59e09") },    // Yellow
        { "g", FromHex("5a6670") },    // Grey
        { "r", FromHex("ad1f1f") },    // Red
        { "b", FromHex("217b91") },    // Blue
        { "n", FromHex("388044") },    // Green
        { "p", FromHex("6a4484") },    // Purple
        { "c", FromHex("4ed6ec") },    // Cyan
        { "s", FromHex("14397d") },    // Dark Blue (Sea)
        { "d", FromHex("120f17") },    // Black (Dark)
        { "w", FromHex("f1e5b5") },    // White (Light)
        { "k", FromHex("9e78cf") },    // Magenta (Kuish)
        { "u", FromHex("ffc4fc") },    // Pink (Uuish)
        { "v", FromHex("d32be3") },    // Violet (Pink hex)
        { "h", FromHex("a67060") },    // Brown (Huish)
        { "m", FromHex("5e1602") },    // Dark Brown (Mahogany)
        { "l", FromHex("08d008") },    // Lime
        { "t", FromHex("233f23") },    // Dark Green (Tuish)
        { "z", FromHex("f55c0b") },    // Strong Orange (Zuish)
        { "a", FromHex("ffbf00") },    // Strong Yellow (Amber)
        { "i", FromHex("a8a8a8") },    // Light Grey (Iuish)
        { "q", FromHex("ff4343") },    // Strong Red (Quish)
        { "x", FromHex("e8f123") },    // Strong Green (Xuish)
        { "f", FromHex("c8eca1") },    // Weak Green (Fuish)
        { "j", FromHex("def8ff") }     // Weak Blue (Juish)
    };


    public static readonly Dictionary<HeroType, HeroColorOption> HeroColorMap = new Dictionary<HeroType, HeroColorOption>
    {
        { HeroType.Ace, HeroColorOption.Blue },
        { HeroType.Acolyte, HeroColorOption.Red },
        { HeroType.Agent, HeroColorOption.Orange },
        { HeroType.Alien, HeroColorOption.Green },
        { HeroType.Alloy, HeroColorOption.Grey },
        { HeroType.Armorer, HeroColorOption.Grey },
        { HeroType.Artificer, HeroColorOption.Blue },
        { HeroType.Assassin, HeroColorOption.Orange },
        { HeroType.B1, HeroColorOption.Blue },
        { HeroType.B2, HeroColorOption.Blue },
        { HeroType.B3, HeroColorOption.Blue },
        { HeroType.Barbarian, HeroColorOption.Yellow },
        { HeroType.Bard, HeroColorOption.Grey },
        { HeroType.Bash, HeroColorOption.Yellow },
        { HeroType.Berserker, HeroColorOption.Yellow },
        { HeroType.Brawler, HeroColorOption.Yellow },
        { HeroType.Brigand, HeroColorOption.Yellow },
        { HeroType.Brute, HeroColorOption.Yellow },
        { HeroType.Buckle, HeroColorOption.Grey },
        { HeroType.Caldera, HeroColorOption.Blue },
        { HeroType.Captain, HeroColorOption.Yellow },
        { HeroType.Chronos, HeroColorOption.Blue },
        { HeroType.Cleric, HeroColorOption.Grey },
        { HeroType.Clumsy, HeroColorOption.Orange },
        { HeroType.Coffin, HeroColorOption.Green },
        { HeroType.Collector, HeroColorOption.Yellow },
        { HeroType.Cultist, HeroColorOption.Blue },
        { HeroType.Curator, HeroColorOption.Yellow },
        { HeroType.Dabble, HeroColorOption.Orange },
        { HeroType.Dabbler, HeroColorOption.Orange },
        { HeroType.Dabblest, HeroColorOption.Orange },
        { HeroType.Dancer, HeroColorOption.Orange },
        { HeroType.Defender, HeroColorOption.Grey },
        { HeroType.Dice, HeroColorOption.Green },
        { HeroType.Disciple, HeroColorOption.Red },
        { HeroType.Doctor, HeroColorOption.Red },
        { HeroType.Druid, HeroColorOption.Red },
        { HeroType.Eccentric, HeroColorOption.Yellow },
        { HeroType.Enchanter, HeroColorOption.Red },
        { HeroType.Evoker, HeroColorOption.Blue },
        { HeroType.Fate, HeroColorOption.Red },
        { HeroType.Fencer, HeroColorOption.Orange },
        { HeroType.Fey, HeroColorOption.Red },
        { HeroType.Fiend, HeroColorOption.Blue },
        { HeroType.Fighter, HeroColorOption.Yellow },
        { HeroType.Forsaken, HeroColorOption.Red },
        { HeroType.G1, HeroColorOption.Green },
        { HeroType.G2, HeroColorOption.Green },
        { HeroType.G3, HeroColorOption.Green },
        { HeroType.Gambler, HeroColorOption.Orange },
        { HeroType.Gardener, HeroColorOption.Red },
        { HeroType.Ghast, HeroColorOption.Blue },
        { HeroType.Glacia, HeroColorOption.Blue },
        { HeroType.Gladiator, HeroColorOption.Yellow },
        { HeroType.Glitch, HeroColorOption.Violet },
        { HeroType.Granite, HeroColorOption.Green },
        { HeroType.Guardian, HeroColorOption.Grey },
        { HeroType.Healer, HeroColorOption.Red },
        { HeroType.Herbalist, HeroColorOption.Red },
        { HeroType.Hoarder, HeroColorOption.Yellow },
        { HeroType.Housecat, HeroColorOption.Green },
        { HeroType.Initiate, HeroColorOption.Blue },
        { HeroType.Jester, HeroColorOption.Blue },
        { HeroType.Juggler, HeroColorOption.Orange },
        { HeroType.Jumble, HeroColorOption.Green },
        { HeroType.Keeper, HeroColorOption.Grey },
        { HeroType.Knight, HeroColorOption.Grey },
        { HeroType.Lazy, HeroColorOption.Yellow },
        { HeroType.Leader, HeroColorOption.Yellow },
        { HeroType.Lost, HeroColorOption.Orange },
        { HeroType.Ludus, HeroColorOption.Orange },
        { HeroType.Luggage, HeroColorOption.Green },
        { HeroType.Mage, HeroColorOption.Blue },
        { HeroType.Meddler, HeroColorOption.Blue },
        { HeroType.Medic, HeroColorOption.Red },
        { HeroType.Mimic, HeroColorOption.Green },
        { HeroType.Monk, HeroColorOption.Grey },
        { HeroType.Myco, HeroColorOption.Blue },
        { HeroType.Mystic, HeroColorOption.Red },
        { HeroType.N1, HeroColorOption.Grey },
        { HeroType.N2, HeroColorOption.Grey },
        { HeroType.N3, HeroColorOption.Grey },
        { HeroType.Ninja, HeroColorOption.Orange },
        { HeroType.O1, HeroColorOption.Orange },
        { HeroType.O2, HeroColorOption.Orange },
        { HeroType.O3, HeroColorOption.Orange },
        { HeroType.Paladin, HeroColorOption.Grey },
        { HeroType.Pilgrim, HeroColorOption.Grey },
        { HeroType.Pockets, HeroColorOption.Green },
        { HeroType.Poet, HeroColorOption.Grey },
        { HeroType.Presence, HeroColorOption.Green },
        { HeroType.Priestess, HeroColorOption.Red },
        { HeroType.Primrose, HeroColorOption.Green },
        { HeroType.Prince, HeroColorOption.Grey },
        { HeroType.Prodigy, HeroColorOption.Blue },
        { HeroType.Prophet, HeroColorOption.Red },
        { HeroType.R1, HeroColorOption.Red },
        { HeroType.R2, HeroColorOption.Red },
        { HeroType.R3, HeroColorOption.Red },
        { HeroType.Ranger, HeroColorOption.Orange },
        { HeroType.Reflection, HeroColorOption.Green },
        { HeroType.Robot, HeroColorOption.Green },
        { HeroType.Rogue, HeroColorOption.Orange },
        { HeroType.Roulette, HeroColorOption.Orange },
        { HeroType.Ruffian, HeroColorOption.Yellow },
        { HeroType.Scoundrel, HeroColorOption.Orange },
        { HeroType.Scrapper, HeroColorOption.Yellow },
        { HeroType.Seer, HeroColorOption.Blue },
        { HeroType.Shaman, HeroColorOption.Red },
        { HeroType.Sharpshot, HeroColorOption.Orange },
        { HeroType.Sinew, HeroColorOption.Yellow },
        { HeroType.Soldier, HeroColorOption.Yellow },
        { HeroType.Sorcerer, HeroColorOption.Blue },
        { HeroType.Spade, HeroColorOption.Green },
        { HeroType.Sparky, HeroColorOption.Blue },
        { HeroType.Spellblade, HeroColorOption.Orange },
        { HeroType.Sphere, HeroColorOption.Green },
        { HeroType.Spine, HeroColorOption.Green },
        { HeroType.Splint, HeroColorOption.Red },
        { HeroType.Squire, HeroColorOption.Grey },
        { HeroType.Stalwart, HeroColorOption.Grey },
        { HeroType.Statue, HeroColorOption.Green },
        { HeroType.Stoic, HeroColorOption.Grey },
        { HeroType.Student, HeroColorOption.Blue },
        { HeroType.Surgeon, HeroColorOption.Red },
        { HeroType.Tainted, HeroColorOption.Green },
        { HeroType.Thief, HeroColorOption.Orange },
        { HeroType.Tinder, HeroColorOption.Green },
        { HeroType.Trapper, HeroColorOption.Orange },
        { HeroType.Tw1n, HeroColorOption.Green },
        { HeroType.Twin, HeroColorOption.Green },
        { HeroType.Valkyrie, HeroColorOption.Grey },
        { HeroType.Vampire, HeroColorOption.Red },
        { HeroType.Venom, HeroColorOption.Orange },
        { HeroType.Vessel, HeroColorOption.Green },
        { HeroType.Veteran, HeroColorOption.Yellow },
        { HeroType.Wallop, HeroColorOption.Grey },
        { HeroType.Wanderer, HeroColorOption.Yellow },
        { HeroType.Warden, HeroColorOption.Grey },
        { HeroType.Warlock, HeroColorOption.Blue },
        { HeroType.Weaver, HeroColorOption.Blue },
        { HeroType.Whirl, HeroColorOption.Yellow },
        { HeroType.Witch, HeroColorOption.Red },
        { HeroType.Wizard, HeroColorOption.Blue },
        { HeroType.Wraith, HeroColorOption.Red },
        { HeroType.Y1, HeroColorOption.Yellow },
        { HeroType.Y2, HeroColorOption.Yellow },
        { HeroType.Y3, HeroColorOption.Yellow },
    };

    public static string GetHeroColorName(string code)
    {
        // Remove the "col." prefix if it exists
        string key = code.Replace("col.", "").ToLower();

        if (HeroColorNames.TryGetValue(key, out string name))
        {
            return name;
        }
        return "Unknown Color";
    }

    public static Color GetHeroColor(string code)
    {
        string key = code.Replace("col.", "").ToLower();
        if (HeroColorHexMap.TryGetValue(key, out Color color))
        {
            return color;
        }
        return Color.white; // Default fallback
    }

    public static Color FromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
        {
            return color;
        }
        return Color.white;
    }
}

public static class DefaultDiceData
{
    public enum EffectType
    {
        Blank = 0,
        BlankUnset = 1,
        BlankPetrified = 2,
        BlankUsed = 3,
        BlankItem = 4,
        BlankCurse = 5,
        BlankStasis = 6,
        BlankSticky = 7,
        BlankExert = 8,
        BlankFumble = 9,
        AddCleanseAndSelfCleanse = 10,
        DamageToAllyMandatoryGenerousStasis = 11,
        SelfDamageCantrip = 12,
        IDieCantrip = 13,
        SelfDamageMandatory = 14,
        Damage = 15,
        DamageGrowth = 16,
        DamageEngage = 17,
        DamageManagain = 18,
        DamagePain = 19,
        DamageDeathwish = 20,
        DamageDeath = 21,
        DamageSerrated = 22,
        DamageExert = 23,
        DamageDoubleUse = 24,
        DamageQuadUse = 25,
        DamageBloodlust = 26,
        DamageCopycat = 27,
        DamagePristine = 28,
        DamageGuilt = 29,
        DamageCruel = 30,
        DamageShifter = 31,
        DamageFocus = 32,
        DamageInspired = 33,
        DamageToAll = 34,
        ReviveManagain = 35,
        DamageCleave = 36,
        DamageDescend = 37,
        DamageCleaveChain = 38,
        DamageHeavy = 39,
        DamageInflictSingleUse = 40,
        DamageSteel = 41,
        DamageCharged = 42,
        StunBully = 43,
        DamageVulnerable = 44,
        DamageEra = 45,
        DamageRanged = 46,
        DamageRangedPoison = 47,
        DamageRangedDuplicate = 48,
        DamageRangedCleave = 49,
        DamageRangedCopycat = 50,
        DamageSelfShield = 51,
        DamageSelfHeal = 52,
        DamagePoison = 53,
        DamageToAllPoison = 54,
        DamagePoisonPlague = 55,
        Shield = 56,
        ShieldFlesh = 57,
        ShieldGrowth = 58,
        ShieldEngage = 59,
        ShieldEnduringDeath = 60,
        ShieldManaGain = 61,
        ShieldDoubleUse = 62,
        ShieldSteel = 63,
        ShieldRescue = 64,
        ShieldPristine = 65,
        ShieldCantrip = 66,
        ShieldCopycat = 67,
        ShieldFocus = 68,
        ShieldCleave = 69,
        ShieldCharged = 70,
        ShieldCleanse = 71,
        ShieldToAll = 72,
        ShieldToAllCantrip = 73,
        ShieldAndHeal = 74,
        ShieldSmith = 75,
        Mana = 76,
        ManaCantrip = 77,
        ManaCantripBoned = 78,
        ManaGrowth = 79,
        ManaDecay = 80,
        ManaDeath = 81,
        ManaPain = 82,
        ManaBloodlust = 83,
        ManaPair = 84,
        ManaTrio = 85,
        HealShieldManaGain = 86,
        ManaCharged = 87,
        DamageSingleUseCharged = 88,
        DamageSingleUseInflictPain = 89,
        DamageSingleUseCruel = 90,
        DamageSingleUsePoison = 91,
        DamageSingleUseSelfHeal = 92,
        ManaSingleUse = 93,
        ShieldSingleUsePermaBoost = 94,
        DamageSingleUseWeaken = 95,
        DamageSingleUseFierce = 96,
        DamageSingleUseEcho = 97,
        DamageSingleUseDispel = 98,
        DamageSingleUseInflictExert = 99,
        StunSingleUse = 100,
        DamageSingleUseWeakenVulnerableCleaveEngageSelfheal = 101, // Chaos Wand
        DamageLead = 102,
        Heal = 103,
        HealSingleUse = 104,
        HealVitality = 105,
        HealRescue = 106,
        HealAll = 107,
        HealBoost = 108,
        HealCleave = 109,
        HealRegen = 110,
        HealCleanse = 111,
        HealManaGain = 112,
        HealGroooooowth = 113,
        HealDoubleUse = 114,
        DamageSingleUse = 115,
        KillIfLessThanHp = 116,
        Undying = 117,
        RedirectSelfShield = 118,
        ShieldRepel = 119,
        ShieldRepelRampageRescue = 120,
        ShieldPain = 121,
        KillIfLessThanHpRanged = 122,
        Dodge = 123,
        DodgeCantrip = 124,
        RerollCantrip = 125,
        DamageCantrip = 126,
        DamageStickyMandatoryDeath = 127,
        DamageToAllRampagePain = 128,
        DamageChargedRampagePain = 129,
        Reuse = 130,
        DamageWeaken = 131,
        DamageDuplicate = 132,
        ShieldDuplicate = 133,
        ManaDuplicate = 134,
        DamageRangedEngage = 135,
        Revive = 136,
        DamageRampage = 137,
        AddDoubleUse = 138,
        AddCantrip = 139,
        AddNothing = 140,
        AddCopycat = 141,
        AddCleaveSingleUse = 142,
        AddCruelDeathwish = 143,
        AddManaGain = 144,
        AddPoison = 145,
        AddSelfShield = 146,
        AddSelfHeal = 147,
        AddSelfHealSelfShield = 148,
        AddPainManaGain = 149,
        AddEngage = 150,
        AddGrowth = 151,
        BlankShield = 152,
        BlankDamage = 153,
        BlankMana = 154,
        BlankSummon = 155,
        BlankHeal = 156,
        RedirectCleave = 157, // Red Flag
        DamageToAllRampage = 158, // Spinning Scythe
        DamageFleshPain = 159, // Viscera
        DamageToAllChargedManaCost = 160, // Mana Bomb
        DamageSingleUseInflictSingleUse = 161, // Wand of Wand
        HealBoostInflictPain = 162, // Demon Horn
        DamageHeavyCharged = 163, // Charged Hammer
        HealRegenCleanseManaCost = 164, // Infused Herbs
        DamagePainDrink = 165, // Potion Shard
        ReviveDrink = 166, // Revive Potion
        ManaDrink = 167, // Mana Potion
        DamageEliminate = 168,
        DamagePoisonEnemy = 169,
        DamageEnemy = 170,
        DamageCleaveEnemy = 171,
        SummonDragonsDeath = 172,
        DamageCleaveTrio = 173,
        DamageDefy = 174,
        DamageCritical = 175,
        TargetAlly = 176,
        TargetAllyPips = 177,
        AllAlliesPips = 178,
        AllAllies = 179,
        TargetEnemy = 180,
        TargetEnemyPips = 181,
        AllEnemiesPips = 182,
        AllEnemies = 183,
        TargetAllPips = 184,
        TargetAll = 185,
        TargetSelf = 186,
        TargetSelfPips = 187
    }

    public static readonly string[] BaseTooltipNames = new string[]
{
    "Blank", // 0
    "Blank (Unset)", // 1
    "Blank (Petrified)", // 2
    "Blank (Used)", // 3
    "Blank (Item)", // 4
    "Blank (Curse)", // 5
    "Blank (Stasis)", // 6
    "Blank (Sticky)", // 7
    "Blank (Exert)", // 8
    "Blank (Fumble)", // 9
    "Add Cleanse and SelfCleanse", // 10
    "Damage to Ally Mandatory Generous Stasis", // 11
    "Self damage Cantrip", // 12
    "I Die Cantrip", // 13
    "Self damage Mandatory", // 14
    "Damage", // 15
    "Damage Growth", // 16
    "Damage Engage", // 17
    "Damage Managain", // 18
    "Damage Pain", // 19
    "Damage Deathwish", // 20
    "Damage Death", // 21
    "Damage serrated", // 22
    "Damage Exert", // 23
    "Damage DoubleUse", // 24
    "Damage QuadUse", // 25
    "Damage Bloodlust", // 26
    "Damage Copycat", // 27
    "Damage Pristine", // 28
    "Damage Guilt", // 29
    "Damage Cruel", // 30
    "Damage Shifter", // 31
    "Damage Focus", // 32
    "Damage Inspired", // 33
    "Damage to all", // 34
    "Revive Managain", // 35
    "Damage Cleave", // 36
    "Damage Descend", // 37
    "Damage Cleave Chain", // 38
    "Damage Heavy", // 39
    "Damage InflictSingleUse", // 40
    "Damage Steel", // 41
    "Damage Charged", // 42
    "Stun Bully", // 43
    "Damage Vulnerable", // 44
    "Damage Era", // 45
    "Damage Ranged", // 46
    "Damage Ranged Poison", // 47
    "Damage Ranged Duplicate", // 48
    "Damage Ranged Cleave", // 49
    "Damage Ranged Copycat", // 50
    "Damage SelfShield", // 51
    "Damage SelfHeal", // 52
    "Damage Poison", // 53
    "Damage to ALL Poison", // 54
    "Damage Poison Plague", // 55
    "Shield", // 56
    "Shield flesh", // 57
    "Shield Growth", // 58
    "Shield Engage", // 59
    "Shield Enduring Death", // 60
    "Shield ManaGain", // 61
    "Shield DoubleUse", // 62
    "Shield Steel", // 63
    "Shield Rescue", // 64
    "Shield Pristine", // 65
    "Shield Cantrip", // 66
    "Shield Copycat", // 67
    "Shield Focus", // 68
    "Shield Cleave", // 69
    "Shield Charged", // 70
    "Shield Cleanse", // 71
    "Shield to all", // 72
    "Shield to all Cantrip", // 73
    "Shield and Heal", // 74
    "Shield Smith", // 75
    "Mana", // 76
    "Mana Cantrip", // 77
    "Mana Cantrip Boned", // 78
    "Mana Growth", // 79
    "Mana Decay", // 80
    "Mana Death", // 81
    "Mana Pain", // 82
    "Mana Bloodlust", // 83
    "Mana Pair", // 84
    "Mana trio", // 85
    "Heal Shield ManaGain", // 86
    "Mana Charged", // 87
    "Damage Single-use Charged", // 88
    "Damage SIngle-use InflictPain", // 89
    "Damage SIngle-use Cruel", // 90
    "Damage Single-use Poison", // 91
    "Damage Single-use SelfHeal", // 92
    "Mana Single-use", // 93
    "Shield Single-use PermaBoost", // 94
    "Damage Single-use Weaken", // 95
    "Damage Single-use Fierce", // 96
    "Damage Single-use Echo", // 97
    "Damage Single-use Dispel", // 98
    "Damage Single-use InflictExert", // 99
    "Stun Single-use", // 100
    "Damage Singleuse Weaken Vulnerable Cleave Engage Selfheal - [Chaos Wand]", // 101
    "Damage Lead", // 102
    "Heal", // 103
    "Heal Single-use", // 104
    "Heal Vitality", // 105
    "Heal Rescue", // 106
    "Heal All", // 107
    "Heal Boost", // 108
    "Heal Cleave", // 109
    "Heal Regen", // 110
    "Heal Cleanse", // 111
    "Heal ManaGain", // 112
    "Heal Groooooowth", // 113
    "Heal DoubleUse", // 114
    "Damage Single-use", // 115
    "Kill if less than hp ", // 116
    "Undying", // 117
    "Redirect SelfShield", // 118
    "Shield Repel", // 119
    "Shield Repel Rampage Rescue", // 120
    "Shield Pain", // 121
    "Kill if less than hp Ranged", // 122
    "Dodge", // 123
    "Dodge Cantrip", // 124
    "Reroll Cantrip", // 125
    "Damage Cantrip", // 126
    "Damage Sticky Mandatory Death", // 127
    "Damage to all Rampage Pain", // 128
    "Damage Charged Rampage Pain", // 129
    "Reuse", // 130
    "Damage Weaken", // 131
    "Damage Duplicate", // 132
    "Shield Duplicate", // 133
    "Mana Duplicate", // 134
    "Damage Ranged Engage", // 135
    "Revive", // 136
    "Damage Rampage", // 137
    "Add DoubleUse", // 138
    "Add Cantrip", // 139
    "Add Nothing", // 140
    "Add Copycat", // 141
    "Add Cleave Single-use", // 142
    "Add Cruel Deathwish", // 143
    "Add ManaGain", // 144
    "Add Poison", // 145
    "Add SelfShield", // 146
    "Add SeflHeal", // 147
    "Add SelfHeal SelfShield", // 148
    "Add Pain ManaGain", // 149
    "Add Engage", // 150
    "Add Growth", // 151
    "Blank (Shield)", // 152
    "Blank (Damage)", // 153
    "Blank (Mana)", // 154
    "Blank (Summon)", // 155
    "Blank (Heal)", // 156
    "Redirect Cleave - [Red Flag]", // 157
    "Damage to ALL Rampage - [Spinning Scythe]", // 158
    "Damage Flesh Pain - [Viscera]", // 159
    "Damage to ALL Charged ManaCost - [Mana Bomb]", // 160
    "Damage SingleUse InflictSingleUse - [Wand of Wand]", // 161
    "Heal Boost InflictPain - [Demon Horn]", // 162
    "Damage Heavy Charged - [Charged Hammer]", // 163
    "Heal Regen Cleanse ManaCost - [Infused Herbs]", // 164
    "Damage Pain Drink - [Potion Shard]", // 165
    "Revive Drink - [Revive Potion]", // 166
    "Mana Drink - [Mana Potion]", // 167
    "Damage Eliminate", // 168
    "Damage Poison (Enemy)", // 169
    "Damage (Enemy)", // 170
    "Damage Cleave (Enemy)", // 171
    "Summon Dragons Death", // 172
    "Damage Cleave Trio", // 173
    "Damage Defy", // 174
    "Damage Critical", // 175
    "Target Ally", // 176
    "Target Ally (Pips)", // 177
    "All Allies (Pips)", // 178
    "All Allies ", // 179
    "Target Enemy", // 180
    "Target Enemy (Pips)", // 181
    "All Enemies (Pips)", // 182
    "All Enemies", // 183
    "Target ALL (Pips)", // 184
    "Target ALL", // 185
    "Target Self", // 186
    "Target Self (Pips)" // 187
};

    /*
    public static readonly Dictionary<string, EffectType> EffectMap = new Dictionary<string, EffectType>
    {
        { "Blank", EffectType.Blank },
        { "Blank (Unset)", EffectType.BlankUnset },
        { "Blank (Petrified)", EffectType.BlankPetrified },
        { "Blank (Used)", EffectType.BlankUsed },
        { "Blank (Item)", EffectType.BlankItem },
        { "Blank (Curse)", EffectType.BlankCurse },
        { "Blank (Stasis)", EffectType.BlankStasis },
        { "Blank (Sticky)", EffectType.BlankSticky },
        { "Blank (Exert)", EffectType.BlankExert },
        { "Blank (Fumble)", EffectType.BlankFumble },
        { "Add Cleanse and SelfCleanse", EffectType.AddCleanseAndSelfCleanse },
        { "Damage to Ally Mandatory Generous Stasis", EffectType.DamageToAllyMandatoryGenerousStasis },
        { "Self damage Cantrip", EffectType.SelfDamageCantrip },
        { "I Die Cantrip", EffectType.IDieCantrip },
        { "Self damage Mandatory", EffectType.SelfDamageMandatory },
        { "Damage", EffectType.Damage },
        { "Damage Growth", EffectType.DamageGrowth },
        { "Damage Engage", EffectType.DamageEngage },
        { "Damage Managain", EffectType.DamageManagain },
        { "Damage Pain", EffectType.DamagePain },
        { "Damage Deathwish", EffectType.DamageDeathwish },
        { "Damage Death", EffectType.DamageDeath },
        { "Damage serrated", EffectType.DamageSerrated },
        { "Damage Exert", EffectType.DamageExert },
        { "Damage DoubleUse", EffectType.DamageDoubleUse },
        { "Damage QuadUse", EffectType.DamageQuadUse },
        { "Damage Bloodlust", EffectType.DamageBloodlust },
        { "Damage Copycat", EffectType.DamageCopycat },
        { "Damage Pristine", EffectType.DamagePristine },
        { "Damage Guilt", EffectType.DamageGuilt },
        { "Damage Cruel", EffectType.DamageCruel },
        { "Damage Shifter", EffectType.DamageShifter },
        { "Damage Focus", EffectType.DamageFocus },
        { "Damage Inspired", EffectType.DamageInspired },
        { "Damage to all", EffectType.DamageToAll },
        { "Revive Managain", EffectType.ReviveManagain },
        { "Damage Cleave", EffectType.DamageCleave },
        { "Damage Descend", EffectType.DamageDescend },
        { "Damage Cleave Chain", EffectType.DamageCleaveChain },
        { "Damage Heavy", EffectType.DamageHeavy },
        { "Damage InflictSingleUse", EffectType.DamageInflictSingleUse },
        { "Damage Steel", EffectType.DamageSteel },
        { "Damage Charged", EffectType.DamageCharged },
        { "Stun Bully", EffectType.StunBully },
        { "Damage Vulnerable", EffectType.DamageVulnerable },
        { "Damage Era", EffectType.DamageEra },
        { "Damage Ranged", EffectType.DamageRanged },
        { "Damage Ranged Poison", EffectType.DamageRangedPoison },
        { "Damage Ranged Duplicate", EffectType.DamageRangedDuplicate },
        { "Damage Ranged Cleave", EffectType.DamageRangedCleave },
        { "Damage Ranged Copycat", EffectType.DamageRangedCopycat },
        { "Damage SelfShield", EffectType.DamageSelfShield },
        { "Damage SelfHeal", EffectType.DamageSelfHeal },
        { "Damage Poison", EffectType.DamagePoison },
        { "Damage to ALL Poison", EffectType.DamageToAllPoison },
        { "Damage Poison Plague", EffectType.DamagePoisonPlague },
        { "Shield", EffectType.Shield },
        { "Shield flesh", EffectType.ShieldFlesh },
        { "Shield Growth", EffectType.ShieldGrowth },
        { "Shield Engage", EffectType.ShieldEngage },
        { "Shield Enduring Death", EffectType.ShieldEnduringDeath },
        { "Shield ManaGain", EffectType.ShieldManaGain },
        { "Shield DoubleUse", EffectType.ShieldDoubleUse },
        { "Shield Steel", EffectType.ShieldSteel },
        { "Shield Rescue", EffectType.ShieldRescue },
        { "Shield Pristine", EffectType.ShieldPristine },
        { "Shield Cantrip", EffectType.ShieldCantrip },
        { "Shield Copycat", EffectType.ShieldCopycat },
        { "Shield Focus", EffectType.ShieldFocus },
        { "Shield Cleave", EffectType.ShieldCleave },
        { "Shield Charged", EffectType.ShieldCharged },
        { "Shield Cleanse", EffectType.ShieldCleanse },
        { "Shield to all", EffectType.ShieldToAll },
        { "Shield to all Cantrip", EffectType.ShieldToAllCantrip },
        { "Shield and Heal", EffectType.ShieldAndHeal },
        { "Shield Smith", EffectType.ShieldSmith },
        { "Mana", EffectType.Mana },
        { "Mana Cantrip", EffectType.ManaCantrip },
        { "Mana Cantrip Boned", EffectType.ManaCantripBoned },
        { "Mana Growth", EffectType.ManaGrowth },
        { "Mana Decay", EffectType.ManaDecay },
        { "Mana Death", EffectType.ManaDeath },
        { "Mana Pain", EffectType.ManaPain },
        { "Mana Bloodlust", EffectType.ManaBloodlust },
        { "Mana Pair", EffectType.ManaPair },
        { "Mana trio", EffectType.ManaTrio },
        { "Heal Shield ManaGain", EffectType.HealShieldManaGain },
        { "Mana Charged", EffectType.ManaCharged },
        { "Damage Single-use Charged", EffectType.DamageSingleUseCharged },
        { "Damage SIngle-use InflictPain", EffectType.DamageSingleUseInflictPain },
        { "Damage SIngle-use Cruel", EffectType.DamageSingleUseCruel },
        { "Damage Single-use Poison", EffectType.DamageSingleUsePoison },
        { "Damage Single-use SelfHeal", EffectType.DamageSingleUseSelfHeal },
        { "Mana Single-use", EffectType.ManaSingleUse },
        { "Shield Single-use PermaBoost", EffectType.ShieldSingleUsePermaBoost },
        { "Damage Single-use Weaken", EffectType.DamageSingleUseWeaken },
        { "Damage Single-use Fierce", EffectType.DamageSingleUseFierce },
        { "Damage Single-use Echo", EffectType.DamageSingleUseEcho },
        { "Damage Single-use Dispel", EffectType.DamageSingleUseDispel },
        { "Damage Single-use InflictExert", EffectType.DamageSingleUseInflictExert },
        { "Stun Single-use", EffectType.StunSingleUse },
        { "Damage Singleuse Weaken Vulnerable Cleave Engage Selfheal - [Chaos Wand]", EffectType.DamageSingleUseWeakenVulnerableCleaveEngageSelfheal },
        { "Damage Lead", EffectType.DamageLead },
        { "Heal", EffectType.Heal },
        { "Heal Single-use", EffectType.HealSingleUse },
        { "Heal Vitality", EffectType.HealVitality },
        { "Heal Rescue", EffectType.HealRescue },
        { "Heal All", EffectType.HealAll },
        { "Heal Boost", EffectType.HealBoost },
        { "Heal Cleave", EffectType.HealCleave },
        { "Heal Regen", EffectType.HealRegen },
        { "Heal Cleanse", EffectType.HealCleanse },
        { "Heal ManaGain", EffectType.HealManaGain },
        { "Heal Groooooowth", EffectType.HealGroooooowth },
        { "Heal DoubleUse", EffectType.HealDoubleUse },
        { "Damage Single-use", EffectType.DamageSingleUse },
        { "Kill if less than hp ", EffectType.KillIfLessThanHp },
        { "Undying", EffectType.Undying },
        { "Redirect SelfShield", EffectType.RedirectSelfShield },
        { "Shield Repel", EffectType.ShieldRepel },
        { "Shield Repel Rampage Rescue", EffectType.ShieldRepelRampageRescue },
        { "Shield Pain", EffectType.ShieldPain },
        { "Kill if less than hp Ranged", EffectType.KillIfLessThanHpRanged },
        { "Dodge", EffectType.Dodge },
        { "Dodge Cantrip", EffectType.DodgeCantrip },
        { "Reroll Cantrip", EffectType.RerollCantrip },
        { "Damage Cantrip", EffectType.DamageCantrip },
        { "Damage Sticky Mandatory Death", EffectType.DamageStickyMandatoryDeath },
        { "Damage to all Rampage Pain", EffectType.DamageToAllRampagePain },
        { "Damage Charged Rampage Pain", EffectType.DamageChargedRampagePain },
        { "Reuse", EffectType.Reuse },
        { "Damage Weaken", EffectType.DamageWeaken },
        { "Damage Duplicate", EffectType.DamageDuplicate },
        { "Shield Duplicate", EffectType.ShieldDuplicate },
        { "Mana Duplicate", EffectType.ManaDuplicate },
        { "Damage Ranged Engage", EffectType.DamageRangedEngage },
        { "Revive", EffectType.Revive },
        { "Damage Rampage", EffectType.DamageRampage },
        { "Add DoubleUse", EffectType.AddDoubleUse },
        { "Add Cantrip", EffectType.AddCantrip },
        { "Add Nothing", EffectType.AddNothing },
        { "Add Copycat", EffectType.AddCopycat },
        { "Add Cleave Single-use", EffectType.AddCleaveSingleUse },
        { "Add Cruel Deathwish", EffectType.AddCruelDeathwish },
        { "Add ManaGain", EffectType.AddManaGain },
        { "Add Poison", EffectType.AddPoison },
        { "Add SelfShield", EffectType.AddSelfShield },
        { "Add SeflHeal", EffectType.AddSelfHeal },
        { "Add SelfHeal SelfShield", EffectType.AddSelfHealSelfShield },
        { "Add Pain ManaGain", EffectType.AddPainManaGain },
        { "Add Engage", EffectType.AddEngage },
        { "Add Growth", EffectType.AddGrowth },
        { "Blank (Shield)", EffectType.BlankShield },
        { "Blank (Damage)", EffectType.BlankDamage },
        { "Blank (Mana)", EffectType.BlankMana },
        { "Blank (Summon)", EffectType.BlankSummon },
        { "Blank (Heal)", EffectType.BlankHeal },
        { "Redirect Cleave - [Red Flag]", EffectType.RedirectCleave },
        { "Damage to ALL Rampage - [Spinning Scythe]", EffectType.DamageToAllRampage },
        { "Damage Flesh Pain - [Viscera]", EffectType.DamageFleshPain },
        { "Damage to ALL Charged ManaCost - [Mana Bomb]", EffectType.DamageToAllChargedManaCost },
        { "Damage SingleUse InflictSingleUse - [Wand of Wand]", EffectType.DamageSingleUseInflictSingleUse },
        { "Heal Boost InflictPain - [Demon Horn]", EffectType.HealBoostInflictPain },
        { "Damage Heavy Charged - [Charged Hammer]", EffectType.DamageHeavyCharged },
        { "Heal Regen Cleanse ManaCost - [Infused Herbs]", EffectType.HealRegenCleanseManaCost },
        { "Damage Pain Drink - [Potion Shard]", EffectType.DamagePainDrink },
        { "Revive Drink - [Revive Potion]", EffectType.ReviveDrink },
        { "Mana Drink - [Mana Potion]", EffectType.ManaDrink },
        { "Damage Eliminate", EffectType.DamageEliminate },
        { "Damage Poison (Enemy)", EffectType.DamagePoisonEnemy },
        { "Damage (Enemy)", EffectType.DamageEnemy },
        { "Damage Cleave (Enemy)", EffectType.DamageCleaveEnemy },
        { "Summon Dragons Death", EffectType.SummonDragonsDeath },
        { "Damage Cleave Trio", EffectType.DamageCleaveTrio },
        { "Damage Defy", EffectType.DamageDefy },
        { "Damage Critical", EffectType.DamageCritical },
        { "Target Ally", EffectType.TargetAlly },
        { "Target Ally (Pips)", EffectType.TargetAllyPips },
        { "All Allies (Pips)", EffectType.AllAlliesPips },
        { "All Allies ", EffectType.AllAllies },
        { "Target Enemy", EffectType.TargetEnemy },
        { "Target Enemy (Pips)", EffectType.TargetEnemyPips },
        { "All Enemies (Pips)", EffectType.AllEnemiesPips },
        { "All Enemies", EffectType.AllEnemies },
        { "Target ALL (Pips)", EffectType.TargetAllPips },
        { "Target ALL", EffectType.TargetAll },
        { "Target Self", EffectType.TargetSelf },
        { "Target Self (Pips)", EffectType.TargetSelfPips }
    };
    */

}

[Serializable]
public class DiceSideData
{
    public int effectID = 0;
    public int pips = 0;

    public List<string> keywords = new List<string>();
    public string facadeID = "";
    public string facadeColor = "";
}

public static class DiceTargetHelper
{
    // Indices: 0:left, 1:mid, 2:top, 3:bot, 4:right, 5:rightmost
    public static readonly string[] FaceNames = { "left", "mid", "top", "bot", "right", "rightmost" };

    public static List<int> GetIndicesForTarget(string target)
    {
        target = target?.ToLower() ?? "";

        return target switch
        {
            "left" => new List<int> { 0 },
            "mid" => new List<int> { 1 },
            "top" => new List<int> { 2 },
            "bot" => new List<int> { 3 },
            "right" => new List<int> { 4 },
            "rightmost" => new List<int> { 5 },

            // Logic definitions
            "all" => new List<int> { 0, 1, 2, 3, 4, 5 },
            "row" => new List<int> { 0, 1, 4 },      // Middle Row: Left, Mid, Right
            "col" => new List<int> { 1, 2, 3 },      // Column: Mid, Top, Bot

            // Combinations
            "topbot" => new List<int> { 2, 3 },         // Top and Bottom
            "left2" => new List<int> { 0, 1 },         // Middle and left
            "mid2" => new List<int> { 1, 4 },         // Middle and right
            "right2" => new List<int> { 4, 5 },         // Right and rightmost
            "right3" => new List<int> { 1, 4, 5 },      // Middle, right and rightmost
            "right5" => new List<int> { 1, 2, 3, 4, 5 },// All except left

            _ => new List<int>()
        };
    }
}

public enum TargetType
{
    left,       // index 0
    left2,      // index 1
    top,        // index 2
    bot,        // index 3
    topbot,     // indices 2 & 3
    right2,     // index 4
    right,      // index 5
    rightmost,  // Also index 5? Sometimes used differently in UI
    mid,        // Middle column?
    mid2,
    row,        // Middle row
    all,        // All sides
    col,        // Color wide
    self        // Passive/Hero wide
}
public enum HeroType
{
    None = 0,
    Ace,
    Acolyte,
    Agent,
    Alien,
    Alloy,
    Armorer,
    Artificer,
    Assassin,
    B1,
    B2,
    B3,
    Barbarian,
    Bard,
    Bash,
    Berserker,
    Brawler,
    Brigand,
    Brute,
    Buckle,
    Caldera,
    Captain,
    Chronos,
    Cleric,
    Clumsy,
    Coffin,
    Collector,
    Cultist,
    Curator,
    Dabble,
    Dabbler,
    Dabblest,
    Dancer,
    Defender,
    Dice,
    Disciple,
    Doctor,
    Druid,
    Eccentric,
    Enchanter,
    Evoker,
    Fate,
    Fencer,
    Fey,
    Fiend,
    Fighter,
    Forsaken,
    G1,
    G2,
    G3,
    Gambler,
    Gardener,
    Ghast,
    Glacia,
    Gladiator,
    Glitch,
    Granite,
    Guardian,
    Healer,
    Herbalist,
    Hoarder,
    Housecat,
    Initiate,
    Jester,
    Juggler,
    Jumble,
    Keeper,
    Knight,
    Lazy,
    Leader,
    Lost,
    Ludus,
    Luggage,
    Mage,
    Meddler,
    Medic,
    Mimic,
    Monk,
    Myco,
    Mystic,
    N1,
    N2,
    N3,
    Ninja,
    O1,
    O2,
    O3,
    Paladin,
    Pilgrim,
    Pockets,
    Poet,
    Presence,
    Priestess,
    Primrose,
    Prince,
    Prodigy,
    Prophet,
    R1,
    R2,
    R3,
    Ranger,
    Reflection,
    Robot,
    Rogue,
    Roulette,
    Ruffian,
    Scoundrel,
    Scrapper,
    Seer,
    Shaman,
    Sharpshot,
    Sinew,
    Soldier,
    Sorcerer,
    Spade,
    Sparky,
    Spellblade,
    Sphere,
    Spine,
    Splint,
    Squire,
    Stalwart,
    Statue,
    Stoic,
    Student,
    Surgeon,
    Tainted,
    Thief,
    Tinder,
    Trapper,
    Tw1n,
    Twin,
    Valkyrie,
    Vampire,
    Venom,
    Vessel,
    Veteran,
    Wallop,
    Wanderer,
    Warden,
    Warlock,
    Weaver,
    Whirl,
    Witch,
    Wizard,
    Wraith,
    Y1,
    Y2,
    Y3,
}
public enum MonsterType
{
    None = 0,
    Agnes,
    Alpha,
    Archer,
    Bandit,
    Banshee,
    Baron,
    Barrel,
    Basalt,
    Basilisk,
    Bee,
    Bell,
    Blind,
    Boar,
    Bones,
    Bramble,
    Carrier,
    Caw,
    CawEgg,
    Chest,
    Chomp,
    Cyclops,
    Demon,
    Dragon,
    DragonEgg,
    Egg,
    Error,
    Fanatic,
    Fountain,
    Ghost,
    Gnoll,
    Goblin,
    Golem,
    Grandma,
    Grave,
    Gremline,
    Gytha,
    Hexia,
    Hydra,
    Illusion,
    Imp,
    Inevitable,
    Jinx,
    Lich,
    Log,
    Madness,
    Magrat,
    Militia,
    Ogre,
    Orb,
    Quartz,
    Rat,
    Rm_t,
    Rm_n,
    Rm_b,
    Rm_h,
    Rotten,
    Saber,
    Sarcophagus,
    Seed,
    Shade,
    Slate,
    Slimelet,
    SlimeQueen,
    Slimer,
    Snake,
    Sniper,
    Spider,
    Spiker,
    Sudul,
    Tarantus,
    TheHand,
    Thorn,
    Totem,
    Totemdeath,
    Totemdecay,
    Totempain,
    Troll,
    TrollKing,
    Vase,
    Warchief,
    Wisp,
    Wizz,
    Wolf,
    Z0mbie,
    Zombie,
}

public static class NameFixes
{
    public static readonly Dictionary<string, string> SpecialNameOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "o1", "o1.75" },
        { "o2", "o2.75" },
        { "o3", "o3.75" },
        { "n1", "n1.75" },
        { "n2", "n2.75" },
        { "n3", "n3.75" },
        { "g1", "g1.75" },
        { "g2", "g2.75" },
        { "g3", "g3.75" },
        { "y1", "y1.75" },
        { "y2", "y2.75" },
        { "y3", "y3.75" },
        { "r1", "r1.75" },
        { "r2", "r2.75" },
        { "r3", "r3.75" },
        { "b1", "b1.75" },
        { "b2", "b2.75" },
        { "b3", "b3.75" },
        { "jinx", "jinx.uhh" },
        { "orb", "orb.Slice" },
        { "egg", "egg.Bee" },
        { "dragonegg", "dragon egg" },
        { "cawegg", "caw egg" },
        { "vase", "vase.uhh" },
        { "totemdeath", "deathSigil" },
        { "totemdecay", "decaySigil" },
        { "totempain", "painSigil" },
        { "rm_n", "rmon.0" },
        { "rm_b", "rmon.3" },
        { "rm_t", "rmon.4" },
        { "rm_h", "rmon.1a" },
    };
}


public enum SDAbilities
{
    Abyss,
    Aid,
    Balance,
    Bandage,
    Bank,
    Beam,
    Betray,
    Bind,
    Blades,
    Blaze,
    Bolt,
    Burn,
    Burstic,
    Charge,
    Chill,
    Circle,
    Clink,
    Crush,
    Cut,
    Devoid,
    Draw,
    Drop,
    @Else,
    Flare,
    Flick,
    Flip,
    Foretell,
    Formation,
    Gather,
    Gaze,
    Glow,
    Hack,
    Harvest,
    Heat,
    Hemlock,
    Hex,
    Hinder,
    Imbue,
    Infinity,
    Infuse,
    Inspire,
    Invest,
    Invoke,
    Leaf,
    Leech,
    Light,
    Liquor,
    Luck,
    Mana,
    Mark,
    Mend,
    Miasma,
    Mulch,
    Niche,
    Oof,
    Operate,
    Parry,
    Poke,
    Poultice,
    Pray,
    Remedy,
    Renew,
    Restore,
    Ritual,
    Salve,
    Save,
    Scald,
    Scorch,
    Slam,
    Slay,
    Slice,
    Soothe,
    Spark,
    Spore,
    Sprout,
    Strand,
    Thrike,
    Tick,
    Tsrub,
    Unite,
    Vine,
    Waste,
    Wings,
    Zap
}

