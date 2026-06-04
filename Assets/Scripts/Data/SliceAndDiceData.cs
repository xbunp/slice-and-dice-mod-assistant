using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SDColors;
using static UnityEditor.Progress;

public static class RandomSDData
{
    public static readonly string[] UnusuablePortraits = { "Glitch", "Error", "Totem" };
}

public enum FightStage
{
    Fight_1_3,
    Fight_4_Boss,
    Fight_5_7,
    Fight_8_Boss,
    Fight_9_11,
    Fight_12_Boss,
    Fight_13_15,
    Fight_16_Boss,
    Fight_17_19,
    Fight_20_Boss,
    Fight_21_30
}

public static class FightStageExtensions
{
    public static string ToDisplayName(this FightStage stage)
    {
        return stage switch
        {
            FightStage.Fight_1_3 => "Fight 1-3",
            FightStage.Fight_4_Boss => "Fight 4 (Boss)",
            FightStage.Fight_5_7 => "Fight 5-7",
            FightStage.Fight_8_Boss => "Fight 8 (Boss)",
            FightStage.Fight_9_11 => "Fight 9-11",
            FightStage.Fight_12_Boss => "Fight 12 (Boss)",
            FightStage.Fight_13_15 => "Fight 13-15",
            FightStage.Fight_16_Boss => "Fight 16 (Boss)",
            FightStage.Fight_17_19 => "Fight 17-19",
            FightStage.Fight_20_Boss => "Fight 20 (Boss)",
            FightStage.Fight_21_30 => "Fight 21-30",
            _ => stage.ToString()
        };
    }

    public static string ToSyntax(this FightStage stage)
    {
        return stage switch
        {
            FightStage.Fight_1_3 => "1-3",
            FightStage.Fight_4_Boss => "4",
            FightStage.Fight_5_7 => "5-7",
            FightStage.Fight_8_Boss => "8",
            FightStage.Fight_9_11 => "9-11",
            FightStage.Fight_12_Boss => "12",
            FightStage.Fight_13_15 => "13-15",
            FightStage.Fight_16_Boss => "16",
            FightStage.Fight_17_19 => "17-19",
            FightStage.Fight_20_Boss => "20",
            FightStage.Fight_21_30 => "21-30",
            _ => stage.ToString()
        };
    }
}

public enum PoolState
{
    None,
    ItemPool,
    MonsterPool,
    HeroPool
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

public enum BaseItems
{
    DeadCrow,
    Amnesia,
    Backstab,
    Empathy,
    BrokenSpirit,
    Compulsion,
    Conscience,
    CursedBolt,
    Exhaustion,
    Parasite,
    PharaohCurse,
    Slimed,
    SoulLink,
    WretchedCrown,
    Affliction,
    BarrelHoops,
    Brittle,
    BrokenHeart,
    CoiledSnake,
    D4,
    Handcuffs,
    LeadWeight,
    Martyr,
    Mould,
    Tracked,
    Weariness,
    AtlasStone,
    BananaPeel,
    BentFork,
    BentSpoon,
    BentSpork,
    BigFish,
    BondCertificate,
    Brick,
    BurredShield,
    Camomile,
    Can,
    Card,
    Chalk,
    Cholesterol,
    CigaretteEnd,
    CrackedEmerald,
    CrackedPhylactery,
    CyanidePill,
    DeadBranch,
    DullWit,
    DustyEmerald,
    Eggshell,
    ExtraPocket,
    FidgetSpinner,
    Flea,
    Fly,
    Grass,
    HiddenStrength,
    HugeScabbard,
    Knot,
    Lawnmower,
    LearnAid,
    LearnBank,
    LearnBetray,
    LearnBurstic,
    LearnHinder,
    LearnInvoke,
    LearnMana,
    LearnNiche,
    LearnOof,
    LearnTsrub,
    LearnWaste,
    MonsterGrin,
    NameTag,
    OldRoot,
    Orbit,
    Overprepared,
    Paper,
    PeanutShell,
    Pin,
    PotionShard,
    Refactor,
    RorrimTekcop,
    RustyLongsword,
    Scissors,
    ScoundrelStash,
    ShovelBite,
    Shroud,
    SleeperAgent,
    SnakeOil,
    Spanner,
    Splinter,
    Sprinkles,
    Stake,
    StaleBread,
    Stoneskin,
    Taxes,
    TinFoilHat,
    ToySword,
    TrickDeck,
    Twiddle,
    TwoOfClubs,
    Void,
    WoodenArmour,
    Yearn,
    Anchor,
    Arrow,
    AutumnLeaf,
    Balisong,
    BalletShoes,
    Barkskin,
    BasiliskScale,
    BigHeart,
    BigShield,
    BoneCharm,
    Bowl,
    CastorRoot,
    ChangeOfHeart,
    CheatingSleeves,
    Cloak,
    ClumsyShoes,
    Coin,
    Compass,
    CopperRing,
    Corset,
    CouragePotion,
    Doll,
    DoomBlade,
    EmeraldShard,
    Fletching,
    GlassHelm,
    HealingWand,
    InfusedHerbs,
    IronHeart,
    KnifeBag,
    LearnPoultice,
    LeatherVest,
    Memory,
    NecromancerTome,
    Pendulum,
    Pillow,
    Quiver,
    Reagents,
    RevivePotion,
    RustyPlate,
    Scar,
    Seedling,
    SorceryNotes,
    Tankard,
    TatteredRobes,
    Tincture,
    TitanbanePotion,
    Trowel,
    WandOfWand,
    Whey,
    WolfEars,
    AceOfSpades,
    BigHammer,
    BlessedRing,
    BlessedWater,
    BurningBlade,
    CitrineRing,
    Clef,
    Clover,
    ClumsyHammer,
    Duvet,
    FaintHalo,
    FirstAidKit,
    FriendshipBracelet,
    Garnet,
    GoldenThread,
    IceCube,
    LearnRemedy,
    LearnWings,
    LeatherGloves,
    Liqueur,
    Needle,
    Origami,
    PeakedCap,
    PowderedMana,
    PowerStone,
    Quicksilver,
    RainOfArrows,
    RejuvenationWand,
    Sapphire,
    SapphireSkull,
    SilverImp,
    Spinach,
    SquareWheel,
    StaticTome,
    Statuette,
    Syringe,
    TowerShield,
    TwinDaggers,
    Wandify,
    WildSeeds,
    WornArms,
    Abacus,
    Aegis,
    Antivenom,
    Ash,
    BloodChalice,
    Buckler,
    DivingSuit,
    DroopyHat,
    Dynamo,
    Fangs,
    FlickeringBlade,
    Foil,
    FullMoon,
    GoldenCup,
    Incense,
    IronPendant,
    Juice,
    Kilt,
    Ladder,
    LeadBoots,
    LearnFlare,
    LearnHeat,
    LearnSprout,
    LichFinger,
    LightningRod,
    Longbow,
    Magnet,
    ManaBomb,
    Obol,
    PocketPhylactery,
    Poem,
    Polearm,
    PureHeartPendant,
    Relic,
    RitualDagger,
    Scalpel,
    Siphon,
    SmellyManure,
    Terrarium,
    ThreeOfAKind,
    UnholyStrength,
    Viscera,
    Whetstone,
    Alembic,
    Apple,
    Bonesaw,
    Cart,
    Chainmail,
    Chakram,
    ChargedSkull,
    Cocoon,
    Corruption,
    DemonEye,
    DuellingPistol,
    EnchantedShield,
    Eyepatch,
    FaeriePact,
    FlawedDiamond,
    Gizmo,
    GlassBlade,
    GlowingEgg,
    Harpoon,
    HissingRing,
    InkBottle,
    InnerStrength,
    JesterCap,
    LearnBeam,
    LearnHack,
    LearnHex,
    LearnInvest,
    LearnMark,
    LifeBolt,
    ManaJelly,
    Pauldron,
    Pulley,
    RedFlag,
    Shortsword,
    Shuriken,
    SplittingArrows,
    TrollNose,
    Updog,
    Whiskers,
    Wristblade,
    Ambrosia,
    BagOfHolding,
    Bandana,
    Candle,
    Cauldron,
    CrackedPlate,
    CrackedWheel,
    Crystallise,
    Decree,
    DemonHorn,
    Determination,
    Door,
    DragonhideGloves,
    EarlyGrave,
    EnchantedHarp,
    EnhanceWand,
    Erythrocyte,
    Fearless,
    GlyphOfPurity,
    Justice,
    Lens,
    Longsword,
    MagicStaff,
    MiniCrossbow,
    Monocle,
    Natural,
    Nunchaku,
    OrdinaryTriangle,
    PolishedEmerald,
    Poodle,
    Ruby,
    SackOfMana,
    Sceptre,
    ShimmeringHalo,
    ShiningBow,
    Simplicity,
    Sling,
    Soup,
    Tiara,
    TreasureChest,
    WandGrips,
    Water,
    Whiskey,
    Blindfold,
    BlindingBolt,
    BloodAmulet,
    Braids,
    BurningHalo,
    Catnip,
    Conduit,
    CrescentShield,
    DemonClaw,
    DemonicDeal,
    GhostShield,
    Honeycomb,
    InfiniHeal,
    Jump,
    Karma,
    Katar,
    KiteShield,
    LeadenHandle,
    LearnLuck,
    LichEye,
    OcularAmulet,
    Scales,
    SilverPendant,
    Sponge,
    Tie,
    ToothNecklace,
    TrollBlood,
    TwistedFlax,
    Urn,
    WandOfStun,
    WeddingRings,
    WoodenBracelet,
    Wrench,
    Anvil,
    Botany,
    Brimstone,
    Broadsword,
    ChargeLink,
    DragonPipe,
    Duck,
    Eucalyptus,
    Flute,
    GlassHeart,
    Hourglass,
    IchorChalice,
    IronHelm,
    JewelLoupe,
    LearnCharge,
    MetalStuds,
    Mushroom,
    OgreBlood,
    PairOfKings,
    PocketMirror,
    SecondChance,
    SharpWit,
    Sparks,
    Tentacle,
    Thimble,
    TwistedBar,
    TwoReeds,
    Wandcraft,
    Wine,
    BootsOfSpeed,
    Bullseye,
    DeadlyBolt,
    FaerieDust,
    Gauntlet,
    Greatsword,
    HolyBook,
    IronCrown,
    IronbloodPendant,
    LearnSpark,
    Lion,
    MirrorMask,
    OlympianTrident,
    OrnateHilt,
    Prism,
    SapphireRing,
    ScorpionTail,
    Sickle,
    Singularity,
    Standard,
    Sushi,
    TourmalineParaiba,
    Tusk,
    AngelFeather,
    BoarhideBracers,
    ChaosWand,
    ChargedHammer,
    Collar,
    Dumbbell,
    EggBasket,
    EmeraldMirror,
    EyeOfHorus,
    HelmOfPower,
    HornedViper,
    OverflowingChalice,
    PoisonDip,
    SecondHeart,
    Serration,
    ShinyGauntlets,
    SilkCape,
    SpikeStone,
    Timestone,
    TripleShuriken,
    WaxSeal,
    Antlers,
    LearnAbyss,
    MithrilShields,
    PuzzleBox,
    Stream,
    Bismuth,
    DiamondSkull,
    Economancy,
    FullPlate,
    HeartOfLight,
    ManaPotion,
    PoseidonCharm,
    Stilts,
    Telescope,
    Broomstick,
    Coffee,
    ConjuringRings,
    Locket,
    RubyShards,
    ShiningEmerald,
    BronzeBell,
    DemonHeart,
    Illegal,
    TitanbaneAmulet,
    EtherealCloak,
    Fertiliser,
    GoldenD6,
    HugeSword,
    ThirdHeart,
    Whirlwind,
    ArchmageOrb,
    Banned,
    DiamondRing,
    Dolphin,
    EmeraldSatchel,
    LearnInfinity,
    TitanBlade,
    BlueSkink,
    ChocolateBar,
    Farewell,
    Pentagram,
    Whirlpool,
    FaceOfHorus,
    ObsidianEdge,
    Taboo
}

public static class EffectKeywordColors
{
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



public static class SpecialAbilityKeywords
{
    public enum AbilityEffectKeyword
    {
        //Special keywords
        Channel,
        Cooldown,
        Deplete,
        Future,
        SpellRescue,
        SingleCast,
        //Standard keywords
        Boost,
        Charged,
        Cleanse,
        Cleave,
        Cruel,
        Damage,
        Descend,
        Dispel,
        Ego,
        Eliminate,
        Engage,
        Focus,
        Generous,
        Heavy,
        InflictDeath,
        InflictExert,
        InflictInflictDeath,
        InflictInflictNothing,
        InflictPain,
        InflictSingleUse,
        ManaGain,
        Plague,
        Poison,
        Ranged,
        Regen,
        Repel,
        Shield,
        Smith,
        Vitality,
        Vulnerable,
        Weaken,
        Wither,
        Annul,
        Heal,
        Hypnotise,
        InflictBoned,
        InflictNothing,
        InflictSelfShield,
        Removed,
        Serrated
    }

    public const string Channel = "ritemx.302ea5e.part.0";
    public const string Cooldown = "Ritemx.161bf";
    public const string Deplete = "ritemx.539ce9a";
    public const string Future = "Unpack.ritemx.644d";
    public const string SpellRescue = "Ritemx.62e8";
    public const string SingleCast = "Ritemx.132fb.part.1";
}

public enum HeroColorOption
{
    Orange,
    Yellow,
    Grey,
    Red,
    Blue,
    Green,
    Purple,
    Cyan,
    Sea,          // Changed from DarkBlue
    Euish,        // Changed from Black (uses code "e", hex "000000")
    White,
    Kuish,        // Changed from Magenta
    Uuish,        // Changed from Pink
    Violet,
    Huish,        // Changed from Brown
    Mahogany,     // Changed from DarkBrown
    Lime,
    Tuish,        // Changed from DarkGreen
    Zuish,        // Changed from StrongOrange
    Amber,        // Changed from StrongYellow
    Iuish,        // Changed from LightGrey
    Quish,        // Changed from StrongRed
    Xuish,        // Changed from StrongGreen
    Fuish,        // Changed from WeakGreen
    Juish         // Changed from WeakBlue
}

public static class SDColors
{
    private static readonly Dictionary<string, Color> DirectColorMap;
    private static readonly Dictionary<string, string> RichTextFormatMap;
    private static readonly Dictionary<string, string> HeroHexMap;

    static SDColors()
    {
        HeroHexMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (HeroType heroType in Enum.GetValues(typeof(HeroType)))
        {
            if (HeroColorMap.TryGetValue(heroType, out HeroColorOption option))
            {
                string code = GetColorCode(option);
                if (HeroColorHexMap.TryGetValue(code, out Color color))
                {
                    // Convert Color to Hex string once at startup
                    string hex = ColorUtility.ToHtmlStringRGB(color);

                    // Map both the enum name (e.g., "Ace") 
                    HeroHexMap[heroType.ToString()] = hex;
                }
            }
        }
    }

    public static bool TryGetHeroHex(string key, out string hexColor)
    {
        return HeroHexMap.TryGetValue(key, out hexColor);
    }

    public static Color GetHeroColorFast(string input)
    {
        if (string.IsNullOrEmpty(input)) return Color.white;

        // Convert to Span for allocation-free slicing
        ReadOnlySpan<char> span = input.AsSpan();

        // Slice off "col." prefix without creating a new string
        if (span.StartsWith("col.", StringComparison.OrdinalIgnoreCase))
        {
            span = span.Slice(4);
        }

        // Lookup using the sliced span
        // Note: If you are on .NET 9/C# 13, you can look up directly using a span.
        // On older versions of .NET, you convert to string once here, which is still faster than Replace + ToLower.
        string key = span.ToString();

        if (DirectColorMap.TryGetValue(key, out Color color))
        {
            return color;
        }

        return Color.white;
    }

    public static string GetColoredHeroName(string heroName)
    {
        // If the parser passes "col.Ace", clean it up
        ReadOnlySpan<char> span = heroName.AsSpan();
        if (span.StartsWith("col.", StringComparison.OrdinalIgnoreCase))
        {
            span = span.Slice(4);
        }

        string key = span.ToString();

        // Instant O(1) retrieval of the fully formatted string
        if (RichTextFormatMap.TryGetValue(key, out string coloredString))
        {
            return coloredString;
        }

        return heroName; // Fallback to original if not found
    }

    public static string[] GetFormattedColorNames()
    {
        // We iterate through the enum to ensure the dropdown order 
        // matches the order defined in ColorOption
        return Enum.GetValues(typeof(HeroColorOption))
                   .Cast<HeroColorOption>()
                   .Select(option => HeroColorNames[GetColorCode(option)])
                   .ToArray();
    }

    // Helper to check if a substring (without allocating a new string) matches a hero
    public static bool TryGetHeroHexSpan(ReadOnlySpan<char> span, out string hexColor)
    {
        // For compatibility across older Unity/C# runtimes, we convert the span to a string here.
        // This only allocates a string *if* we are evaluating a potential match.
        string key = span.ToString();
        return HeroHexMap.TryGetValue(key, out hexColor);
    }

    // Maps Enum to the "code" letter
    public static string GetColorCode(HeroColorOption option)
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
            HeroColorOption.Sea => "s",
            HeroColorOption.Euish => "e",
            HeroColorOption.White => "w",
            HeroColorOption.Kuish => "k",
            HeroColorOption.Uuish => "u",
            HeroColorOption.Violet => "v",
            HeroColorOption.Huish => "h",
            HeroColorOption.Mahogany => "m",
            HeroColorOption.Lime => "l",
            HeroColorOption.Tuish => "t",
            HeroColorOption.Zuish => "z",
            HeroColorOption.Amber => "a",
            HeroColorOption.Iuish => "i",
            HeroColorOption.Quish => "q",
            HeroColorOption.Xuish => "x",
            HeroColorOption.Fuish => "f",
            HeroColorOption.Juish => "j",
            _ => "w"
        };
    }

    // Maps Enum to the actual Unity Color
    public static Color GetColor(HeroColorOption option)
    {
        string code = GetColorCode(option);
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
        { "s", "S: Sea" },
        { "e", "E: Euish" },
        { "w", "W: White" },
        { "k", "K: Kuish" },
        { "u", "U: Uuish" },
        { "v", "V: Violet" },
        { "h", "H: Huish" },
        { "m", "M: Mahogany" },
        { "l", "L: Lime" },
        { "t", "T: Tuish" },
        { "z", "Z: Zuish" },
        { "a", "A: Amber" },
        { "i", "I: Iuish" },
        { "q", "Q: Quish" },
        { "x", "X: Xuish" },
        { "f", "F: Fuish" },
        { "j", "J: Juish" }
    };

    public static readonly Dictionary<string, string> TraitNiceNames = new Dictionary<string, string>
{
    { "Bones", "1 damage to adjacent allies upon death." },
    { "Imp", "On-hit: damage the attacker for 1" },
    { "Sniper", "Starts at the back" },
    { "Wisp", "The 3rd hp: +1 mana" },
    { "Log", "Rolls away if only Logs remain" },
    { "Archer", "Starts at the back" },
    { "Thorn", "Immune to spells\nOn-hit: damage the attacker for 5" },
    { "Grave", "All hp: must be removed individually" },
    { "Chest", "I flee at the end of the turn and drop a t0-2 item if defeated.\nThe 5th hp: must be removed individually" },
    { "Shade", "All hp: become immune to damage this turn" },
    { "Quartz", "The 3rd hp: I die if this hp is removed and no further" },
    { "Gnoll", "Reduce damage taken from abilities and dice by 1" },
    { "Goblin", "Flees if alone" },
    { "Warchief", "All monsters: +1 pip to all sides" },
    { "Bandit", "Flees if an adjacent monster is overkilled by 2 or more" },
    { "Zombie", "I die if I take 4 or more damage in a single attack" },
    { "Z0mbie", "Attacker dies if I take 4 or more damage in a single attack" },
    { "Golem", "Starts with 8 shields, unused shields are retained" },
    { "Blind", "At the end of the turn, if no damage was dealt to any monster, I flee" },
    { "Barrel", "5 damage to adjacent allies upon death" },
    { "Fountain", "All hp: +1 mana" },
    { "Militia", "If an enemy I target gets 5+ shield, I flee" },
    { "Carrier", "Start poisoned for 2" },
    { "Boar", "The inner hp: must be removed individually" },
    { "Slimer", "Every 5th hp (Starts at 5th hp): summon a slimelet" },
    { "Ogre", "Every 5th hp (Starts at 3rd hp): +1 pip to all sides this fight" },
    { "Chomp", "The inner 5 hp: 1 damage to the bottom-most enemy\nThe outer 5 hp: 1 damage to the top-most enemy" },
    { "Ghost", "The 5th hp: become immune to damage this turn" },
    { "Caw", "After taking damage for the first time each turn, move back this turn" },
    { "Banshee", "After the 1st ability each turn is used, 1 damage to all enemies" },
    { "Slate", "All hp: must be removed individually" },
    { "Wizz", "The 4th hp: Stunned this turn\nStarts at the back" },
    { "Basilisk", "On-hit: turn the attacking side to stone" },
    { "Demon", "Every 10th hp (Starts at 1st hp): must be removed by dice damage" },
    { "Spiker", "On-hit: damage the attacker for 2" },
    { "Cyclops", "The middle hp: Stunned this turn" },
    { "Hydra", "I die if I take damage 5 times in a turn" },
    { "Troll", "Regenerate 1 health at the end of each turn" },
    { "Bramble", "All heroes: Add singleUse to all sides" },
    { "Agnes", "The 4th hp: 2 damage to the top-most enemy" },
    { "Gytha", "The 4th hp: 2 damage to the middle enemy" },
    { "Magrat", "The 4th hp: 2 damage to the bottom-most enemy" },
    { "Slime Queen", "Every 5th hp (Starts at 5th hp): summon a slimer" },
    { "Bell", "At the end of each turn, 1 damage to all heroes and 5 damage to me" },
    { "Sarcophagus", "I flee at the end of turn 3 and drop a t3-5 item if defeated.\nThe outer 3 hp: must be removed individually" },
    { "Lich", "Starts at the back\nEvery 5th hp (Starts at 5th hp): summon a bones" },
    { "Rotten", "All heroes get -1 hp at the end of each turn (minimum 1)" },
    { "Baron", "Every 2nd hp (Starts at 2nd hp): +1 mana" },
    { "Madness", "The top non-magic hero: Add possessed & mandatory to all sides" },
    { "Troll King", "Regenerate 2 health at the end of each turn" },
    { "Tarantus", "The 10th hp: kill the top-most enemy" },
    { "Basalt", "The first time I take exactly 1 damage, double it to 2, then increase 1 to 2" },
    { "Hexia", "On-hit: attacker takes equal damage to me\nWhenever you use an ability: damage the bottom hero equal to the cost" },
    { "The Hand", "All heroes: +1 pip to all sides" },
    { "Inevitable", "Every 5th hp (Starts at 3rd hp): become immune to damage this turn\n(Hidden trait) Add era to all sides" },
    { "PainSigil", "All heroes: Add pain to all sides" },
    { "DecaySigil", "All heroes: Add decay to all sides" },
    { "DeathSigil", "All heroes: Add death to all sides" }
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

    private static HashSet<string> monsterNames;
    private static HashSet<string> heroNames;

    public static HashSet<string> GetMonsterNames()
    {
        if (monsterNames == null)
        {
            InitializePoolNames();
        }
        return monsterNames;
    }

    public static HashSet<string> GetHeroNames()
    {
        if (heroNames == null)
        {
            InitializePoolNames();
        }
        return heroNames;
    }

    public static void InitializePoolNames()
    {
        if (monsterNames == null)
        {
            monsterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in System.Enum.GetNames(typeof(MonsterType)))
            {
                monsterNames.Add(name);
            }
        }
        if (heroNames == null)
        {
            heroNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in System.Enum.GetNames(typeof(HeroType)))
            {
                heroNames.Add(name);
            }
        }
    }

    public static string GetNextWord(string text, int startIndex)
    {
        int length = 0;
        while (startIndex + length < text.Length && (char.IsLetterOrDigit(text[startIndex + length]) || text[startIndex + length] == '_'))
        {
            length++;
        }
        return length > 0 ? text.Substring(startIndex, length) : null;
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
}


public static class DiceTargetHelper
{
    // Indices: 0:left, 1:mid, 2:top, 3:bot, 4:right, 5:rightmost
    public static readonly string[] FaceNames = { "left", "mid", "top", "bot", "right", "rightmost" };

    // Bitmask mapping based on indices: Left(1), Mid(2), Top(4), Bot(8), Right(16), Rightmost(32)
    private static readonly (string name, int mask)[] TargetAliases = new (string, int)[]
    {
        ("all", 63),       // 1+2+4+8+16+32 = 63
        ("right5", 62),    // 2+4+8+16+32 = 62
        ("right3", 50),    // 2+16+32 = 50
        ("right2", 48),    // 16+32 = 48
        ("row", 19),       // 1+2+16 = 19
        ("mid2", 18),      // 2+16 = 18
        ("col", 14),       // 2+4+8 = 14
        ("topbot", 12),    // 4+8 = 12
        ("left2", 3),      // 1+2 = 3
        ("rightmost", 32), // 32
        ("right", 16),     // 16
        ("bot", 8),        // 8
        ("top", 4),        // 4
        ("mid", 2),        // 2
        ("left", 1)        // 1
    };

    /// <summary>
    /// Evaluates combinations of available target aliases to find the absolute shortest text representation.
    /// </summary>
    public static List<string> GetBestAliasCombination(int targetMask)
    {
        List<string> bestCombination = null;
        int bestScore = int.MaxValue; // Score calculation: heavily penalize amount of `.i.` segments, then evaluate string length

        void Search(int remainingMask, int currentIndex, List<string> currentCombination, int currentScore)
        {
            if (remainingMask == 0)
            {
                if (currentScore < bestScore)
                {
                    bestScore = currentScore;
                    bestCombination = new List<string>(currentCombination);
                }
                return;
            }

            if (currentIndex >= TargetAliases.Length) return;

            // Option 1: Skip this alias
            Search(remainingMask, currentIndex + 1, currentCombination, currentScore);

            // Option 2: Try appending this alias if it covers faces entirely safely (no overlaps)
            var alias = TargetAliases[currentIndex];
            if ((remainingMask & alias.mask) == alias.mask)
            {
                currentCombination.Add(alias.name);
                // Score = 1000 per grouping penalty + alias name length
                Search(remainingMask & ~alias.mask, currentIndex + 1, currentCombination, currentScore + 1000 + alias.name.Length);
                currentCombination.RemoveAt(currentCombination.Count - 1);
            }
        }

        Search(targetMask, 0, new List<string>(), 0);
        return bestCombination ?? new List<string>();
    }

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
    PainSigil,
    DeathSigil,
    DecaySigil
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

public static class AbilityRelated
{
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

    // NOTE: To use these abilities, they must be formatted this way:
    // ((<hero string>)i.learn.DTDestroy)


    //Items that don’t affect the sides of a hero have no effect when put inside the replica. They only work when outside it. This is because if you take Replica.Mage, you will have a Mage but with no spell
    //Item affecting sides go inside, items not affecting sides go outside

    // Dont ask me why, quirk of s&D

    //This is also how we can get heroes with negative hero tiers, we can't manually set a hero to negative 1, but we can use adj to obtain negative tier heroes, and then use replica to take their tiers
    //replica.(Robot.adj.-1.i.hat.Statue)


    public static readonly Dictionary<string, string> LearnDictionary = new Dictionary<string, string>()
    {
        { "zzz", "i.learn.DTzzz" },
        { "Reshape", "i.learn.DTReshape" },
        { "Destroy", "i.learn.DTDestroy" },
        { "Star", "i.learn.DTStar" },
        { "throw", "i.learn.DTthrow" },
        { "Recycle", "i.learn.DTRecycle" },
        { "Feint", "i.learn.DTFeint" },
        { "comb", "i.learn.DTcomb" },
        { "oiwennn", "i.learn.DToiwennn" },
        { "Lash", "i.learn.DSLash" },
        { "Slammo", "i.learn.DSSlammo" },
        { "Spikes", "i.learn.DSSpikes" },
        { "Resolve", "i.learn.DSResolve" },
        { "Gristle", "i.learn.DSGristle" },
        { "End", "i.learn.DSEnd" },
        { "Gong", "i.learn.DSGong" },
        { "Sting", "i.learn.DSSting" },
        { "Dispell", "i.learn.DSDispell" },
        { "Intervene", "i.learn.DSIntervene" },
        { "Replenish2", "i.learn.DSReplenish2" },
        { "T3blue00", "i.learn.DST3blue00" },
        { "2dmgclv", "i.learn.DS2dmgclv" },
        { "Spotless", "i.learn.DSSpotless" },
        { "Duo", "i.learn.DSDuo" },
        { "Splorb", "i.learn.DSSplorb" },
        { "Touch", "i.learn.DSTouch" },
        { "Shards", "i.learn.DSShards" },
        { "Speed", "i.learn.DSSpeed" },
        { "Tire", "i.learn.DSTire" },
        { "Varhest", "i.learn.DSVarhest" },
        { "Paradox", "i.learn.DSParadox" },
        { "Beep", "i.learn.DSBeep" }
    };
}

public enum AbilityType
{
    Spell,
    Tactic
}

[Serializable]
public class BaseAbility
{
    public string name;
    public AbilityType type;
    public string cost;
    public string effect;

    public BaseAbility(string name, AbilityType type, string cost, string effect)
    {
        this.name = name;
        this.type = type;
        this.cost = cost;
        this.effect = effect;
    }
}

public static class BaseAbilityDatabase
{
    public static readonly List<BaseAbility> Abilities = new List<BaseAbility>()
    {
        // === SPELLS ===
        new BaseAbility("Burst", AbilityType.Spell, "2 mana", "2 damage or shield 2"),
        new BaseAbility("Mend", AbilityType.Spell, "2 mana", "Set a hero to 5 hp"),
        new BaseAbility("Hemlock", AbilityType.Spell, "2 mana", "1 damage, poison singleCast"),
        new BaseAbility("Restore", AbilityType.Spell, "2 mana", "Heal 1 to all allies"),
        new BaseAbility("Gaze", AbilityType.Spell, "1 mana", "Gain 1 reroll, future cooldown"),
        new BaseAbility("Bandage", AbilityType.Spell, "2 mana", "Heal and shield 1, singleCast cleave"),
        new BaseAbility("Poke", AbilityType.Spell, "1 mana", "1 damage, cooldown"),
        new BaseAbility("Scroch", AbilityType.Spell, "2 mana", "1 damage, cleave singleCast"),
        new BaseAbility("Slice", AbilityType.Spell, "3 mana", "1 damage to all enemies"),
        new BaseAbility("Gather", AbilityType.Spell, "2 mana", "Replace blank sides with \"2 mana\" this turn"),
        new BaseAbility("Cut", AbilityType.Spell, "1 mana", "3 damage to an ally, then 2 damage to the top-most enemy, cooldown"),
        new BaseAbility("Slay", AbilityType.Spell, "3 mana", "Kill an enemy with exactly 3 hp"),
        new BaseAbility("Imbue", AbilityType.Spell, "2 mana", "+1 pip to target's damage sides this turn, singleCast"),
        new BaseAbility("Light", AbilityType.Spell, "1 mana", "Shield 1, cleanse cleave singleCast"),
        new BaseAbility("Balance", AbilityType.Spell, "3 mana", "1 damage to all enemies, then heal 1 to all allies"),
        new BaseAbility("Vine", AbilityType.Spell, "1 mana", "1 damage or heal 1"),
        new BaseAbility("Circle", AbilityType.Spell, "2 mana", "Add selfheal to allied sides this turn, singleCast"),
        new BaseAbility("Glow", AbilityType.Spell, "1 mana", "Replace blank sides with 'Heal and shield 2' this turn"),
        new BaseAbility("Infuse", AbilityType.Spell, "2 mana", "Heal 1 to all allies, terminal"),
        new BaseAbility("Renew", AbilityType.Spell, "1 mana", "Set a hero to 4 hp"),
        new BaseAbility("Pray", AbilityType.Spell, "1 mana", "Heal and shield 1 to all dying allies, deplete"),
        new BaseAbility("Flip", AbilityType.Spell, "2 mana", "Flip each side with its opposite this turn"),
        new BaseAbility("Scald", AbilityType.Spell, "3 mana", "2 damage to all damaged enemies"),
        new BaseAbility("Burn", AbilityType.Spell, "1 mana", "1 damage to all heroes and monsters, cooldown"),
        new BaseAbility("Flick", AbilityType.Spell, "1 mana", "1 damage, engage cooldown"),
        new BaseAbility("Spore", AbilityType.Spell, "1 mana", "Add decay & +1 pip to target's sides this turn, cooldown"),
        new BaseAbility("Zap", AbilityType.Spell, "1 mana", "Kill an enemy with exactly 2 hp, cooldown"),
        new BaseAbility("Chill", AbilityType.Spell, "2 mana", "2 damage, weaken singleCast"),
        new BaseAbility("Foretell", AbilityType.Spell, "3 mana", "+4 mana, future"),
        new BaseAbility("Drop", AbilityType.Spell, "3 mana", "4 damage to the top-most enemy"),
        new BaseAbility("Clink", AbilityType.Spell, "4 mana", "Shield 1 to all allies, boost singleCast"),
        new BaseAbility("Liquor", AbilityType.Spell, "3 mana", "Heal 10, cleanse"),
        new BaseAbility("Strand", AbilityType.Spell, "4 mana", "Heal 2, spellRescue"),
        new BaseAbility("Operate", AbilityType.Spell, "3 mana", "Revive the top-most defeated ally, deplete"),
        new BaseAbility("Salve", AbilityType.Spell, "1 mana", "Heal 2"),
        new BaseAbility("Soothe", AbilityType.Spell, "4 mana", "Heal 1 to all allies, regen"),
        new BaseAbility("Bind", AbilityType.Spell, "3 mana", "Target ally becomes immune to damage this turn, deplete"),
        new BaseAbility("Leech", AbilityType.Spell, "1 mana", "Kill an ally, then heal 5 to all allies, cooldown"),
        new BaseAbility("Ritual", AbilityType.Spell, "3 mana", "Heal 2, cleanse cleave"),
        new BaseAbility("Blades", AbilityType.Spell, "4 mana", "2 damage to all enemies"),
        new BaseAbility("Miasma", AbilityType.Spell, "3 mana", "1 damage, poison cleave"),
        new BaseAbility("Inspire", AbilityType.Spell, "4 mana", "Another hero can use their dice again, cooldown"),
        new BaseAbility("Blaze", AbilityType.Spell, "6 mana", "13 damage"),
        new BaseAbility("Crush", AbilityType.Spell, "3 mana", "3 damage to the top and bottom enemies"),
        new BaseAbility("Tick", AbilityType.Spell, "4 mana", "1 damage, weaken cleave"),
        new BaseAbility("Draw", AbilityType.Spell, "1 mana", "Target ally 1, boost deplete"),
        new BaseAbility("Harvest", AbilityType.Spell, "1 mana", "Kill an enemy with exactly 1 hp, then +3 mana, cooldown"),
        new BaseAbility("Aid", AbilityType.Spell, "1 mana", "Heal 2 to all enemies"),
        new BaseAbility("Bank", AbilityType.Spell, "4 mana", "3 mana, future singleCast"),
        new BaseAbility("Betray", AbilityType.Spell, "2 mana", "Kill an ally"),
        new BaseAbility("Hinder", AbilityType.Spell, "1 mana", "1 damage to an ally, weaken"),
        new BaseAbility("Invoke", AbilityType.Spell, "1 mana", "Summon a demon"),
        new BaseAbility("Mana", AbilityType.Spell, "1 mana", "+1 mana, singleCast"),
        new BaseAbility("Niche", AbilityType.Spell, "2 mana", "Kill an enemy with exactly 46 hp"),
        new BaseAbility("Tsrub", AbilityType.Spell, "2 mana", "Shield 2 to an enemy or 2 damage to an ally"),
        new BaseAbility("Waste", AbilityType.Spell, "1 mana", "Replace blank sides with '1 damage death' this turn"),
        new BaseAbility("Poultice", AbilityType.Spell, "1 mana", "Heal 2, singleCast"),
        new BaseAbility("Remedy", AbilityType.Spell, "1 mana", "Heal 1, cleanse singleCast"),
        new BaseAbility("Wings", AbilityType.Spell, "2 mana", "Heal 3 to the top and bottom allies"),
        new BaseAbility("Flare", AbilityType.Spell, "4 mana", "5 damage"),
        new BaseAbility("Heat", AbilityType.Spell, "3 mana", "Heal 3 to all allies with shields, cleanse"),
        new BaseAbility("Sprout", AbilityType.Spell, "3 mana", "Heal 3, channel"),
        new BaseAbility("Beam", AbilityType.Spell, "5 mana", "7 damage, singleCast ranged"),
        new BaseAbility("Hack", AbilityType.Spell, "2 mana", "Replace blank sides with '4 damage' this turn"),
        new BaseAbility("Hex", AbilityType.Spell, "3 mana", "Kill an enemy with exactly 6 hp, ranged singleCast"),
        new BaseAbility("Invest", AbilityType.Spell, "4 mana", "+6 mana, future cooldown"),
        new BaseAbility("Mark", AbilityType.Spell, "4 mana", "2 damage, vulnerable"),
        new BaseAbility("Luck", AbilityType.Spell, "3 mana", "Gain 8 rerolls, future cooldown"),
        new BaseAbility("Charge", AbilityType.Spell, "2 mana", "Shield 2, boost singleCast"),
        new BaseAbility("Spark", AbilityType.Spell, "4 mana", "Add manaGain to target's sides this turn"),
        new BaseAbility("Abyss", AbilityType.Spell, "5 mana", "Kill an enemy with half or less hp"),
        new BaseAbility("Infinity", AbilityType.Spell, "13 mana", "Kill an enemy"),
        new BaseAbility("Save", AbilityType.Spell, "1 mana", "Heal and shield 5, cleanse singleCast"),
        new BaseAbility("Bolt", AbilityType.Spell, "3 mana", "5 damage"),
        
        // --- Unused Spells ---
        new BaseAbility("DSLash", AbilityType.Spell, "4 mana", "2 damage cruel channel"),
        new BaseAbility("DSSlammo", AbilityType.Spell, "3 mana", "4 damage cooldown"),
        new BaseAbility("DSSpikes", AbilityType.Spell, "3 mana", "Shield 1 cleave repel"),
        new BaseAbility("DSResolve", AbilityType.Spell, "2 mana", "Heal 10 singlecase"),
        new BaseAbility("DSGristle", AbilityType.Spell, "2 mana", "Add cleave & death to target's sides this turn"),
        new BaseAbility("DSEnd", AbilityType.Spell, "1 mana", "3 damage to all enemies, then kill all allies"),
        new BaseAbility("DSGong", AbilityType.Spell, "4 mana", "2 shields to ALL boost"),
        new BaseAbility("DSSting", AbilityType.Spell, "3 mana", "1 damage weaken poison"),
        new BaseAbility("DSDispell", AbilityType.Spell, "3 mana", "1 damage dispel"),
        new BaseAbility("DSIntervene", AbilityType.Spell, "3 mana", "Heal 1 to all dying allies cleanse"),
        new BaseAbility("DSReplenish2", AbilityType.Spell, "3 mana", "Heal and Shield 3"),
        new BaseAbility("DST3blue00", AbilityType.Spell, "1 mana", "Kill all enemies with exactly 3 hp"),
        new BaseAbility("DS2dmgclv", AbilityType.Spell, "4 mana", "2 damage cleave"),
        new BaseAbility("DSSpotless", AbilityType.Spell, "2 mana", "Shield 2 to all undamaged allies"),
        new BaseAbility("DSDuo", AbilityType.Spell, "2 mana", "Add pair to target's sides this turn"),
        new BaseAbility("DSSplorb", AbilityType.Spell, "4 mana", "Heal 2 cleanse regen boost"),
        new BaseAbility("DSTouch", AbilityType.Spell, "2 mana", "Target ally: Cannot die this turn deplete"),
        new BaseAbility("DSShards", AbilityType.Spell, "2 mana", "2 damage to all enemies hyperboned cooldown"),
        new BaseAbility("DSSpeed", AbilityType.Spell, "3 mana", "Add cantrip to target's sides this fight"),
        new BaseAbility("DSTire", AbilityType.Spell, "5 mana", "Add Exert to enemy sides this turn"),
        new BaseAbility("DSVarhest", AbilityType.Spell, "1 mana", "Kill an enemy with exactly 1 hp cooldown threesy manaGain"),
        new BaseAbility("DSParadox", AbilityType.Spell, "3 mana", "1 damage spellRescue"),
        new BaseAbility("DSBeep", AbilityType.Spell, "3 mana", "(2 damage heavy), then (heal 1 to all allies boost)"),

        // === TACTICS ===
        new BaseAbility("Thrike", AbilityType.Tactic, "1x 3-pip side", "3 damage"),
        new BaseAbility("Leaf", AbilityType.Tactic, "1x 1-keyword side & 1x blank side", "Target ally 1, boost"),
        new BaseAbility("Mulch", AbilityType.Tactic, "1x any pip", "Heal 2"),
        new BaseAbility("Parry", AbilityType.Tactic, "2x damage pips", "Shield 3"),
        new BaseAbility("Else", AbilityType.Tactic, "1x blank side", "Shield 1, cleanse"),
        new BaseAbility("Slam", AbilityType.Tactic, "2x 1-keyword sides", "4 damage, heavy"),
        new BaseAbility("Devoid", AbilityType.Tactic, "1x blank side & 2x damage pips", "Kill all enemies with 2 or less hp"),
        new BaseAbility("Formation", AbilityType.Tactic, "3x damage pips & 3x shield pips", "2 damage to all enemies, then shield 2 to all allies"),
        new BaseAbility("Unite", AbilityType.Tactic, "1x damage pip & 1x shield pip & 1x heal pip & 1x blank side", "15 damage"),
        new BaseAbility("Burstic", AbilityType.Tactic, "2x mana pips", "2 damage or shield 2"),
        new BaseAbility("Oof", AbilityType.Tactic, "5x blank sides", "5 damage"),
        
        // --- Unused Tactics ---
        new BaseAbility("DTzzz", AbilityType.Tactic, "4x damage pips", "6 damage"),
        new BaseAbility("DTReshape", AbilityType.Tactic, "1x 4 pip side", "Shield 4 Cleave"),
        new BaseAbility("DTDestroy", AbilityType.Tactic, "20x any pips", "20 damage"),
        new BaseAbility("DTStar", AbilityType.Tactic, "3x blank sides", "5 damage"),
        new BaseAbility("DTthrow", AbilityType.Tactic, "2x damage pips", "2 damage Ranged"),
        new BaseAbility("DTRecycle", AbilityType.Tactic, "3x any pips", "1 damage"),
        new BaseAbility("DTFeint", AbilityType.Tactic, "1x heal pip & 1x shield pip", "2 damage"),
        new BaseAbility("DTcomb", AbilityType.Tactic, "1x heal pip & 1x shield pip", "Heal and shield 1 to all allies"),
        new BaseAbility("DToiwennn", AbilityType.Tactic, "4x shield pips", "Target hero can use their dice again")
    };
}


public static class CurseDataset
{
    public static readonly Dictionary<string, string> Curses = new Dictionary<string, string>
    {
        // 11-20 Fights Additions
        { "11-20.add.Agnes", "During fights 11-20: Add an Agnes" },
        { "11-20.add.Alpha", "During fights 11-20: Add an Alpha" },
        { "11-20.add.Archer", "During fights 11-20: Add an Archer" },
        { "11-20.add.Bandit", "During fights 11-20: Add a Bandit" },
        { "11-20.add.Banshee", "During fights 11-20: Add a Banshee" },
        { "11-20.add.Baron", "During fights 11-20: Add a Baron" },
        { "11-20.add.Basalt", "During fights 11-20: Add a Basalt" },
        { "11-20.add.Basilisk", "During fights 11-20: Add a Basilisk" },
        { "11-20.add.Bee", "During fights 11-20: Add a Bee" },
        { "11-20.add.Bell", "During fights 11-20: Add a Bell" },
        { "11-20.add.Blind", "During fights 11-20: Add a Blind" },
        { "11-20.add.Boar", "During fights 11-20: Add a Boar" },
        { "11-20.add.Bones", "During fights 11-20: Add a Bones" },
        { "11-20.add.Bramble", "During fights 11-20: Add a Bramble" },
        { "11-20.add.Carrier", "During fights 11-20: Add a Carrier" },
        { "11-20.add.Caw", "During fights 11-20: Add a Caw" },
        { "11-20.add.Caw Egg", "During fights 11-20: Add a Caw Egg" },
        { "11-20.add.Chomp", "During fights 11-20: Add a Chomp" },
        { "11-20.add.Cyclops", "During fights 11-20: Add a Cyclops" },
        { "11-20.add.Demon", "During fights 11-20: Add a Demon" },
        { "11-20.add.Dragon", "During fights 11-20: Add a Dragon" },
        { "11-20.add.Dragon Egg", "During fights 11-20: Add a Dragon Egg" },
        { "11-20.add.Fanatic", "During fights 11-20: Add a Fanatic" },
        { "11-20.add.Ghost", "During fights 11-20: Add a Ghost" },
        { "11-20.add.Gnoll", "During fights 11-20: Add a Gnoll" },
        { "11-20.add.Goblin", "During fights 11-20: Add a Goblin" },
        { "11-20.add.Golem", "During fights 11-20: Add a Golem" },
        { "11-20.add.Grandma", "During fights 11-20: Add a Grandma" },
        { "11-20.add.Grave", "During fights 11-20: Add a Grave" },
        { "11-20.add.Gytha", "During fights 11-20: Add a Gytha" },
        { "11-20.add.Hexia", "During fights 11-20: Add a Hexia" },
        { "11-20.add.Hydra", "During fights 11-20: Add a Hydra" },
        { "11-20.add.Illusion", "During fights 11-20: Add an Illusion" },
        { "11-20.add.Imp", "During fights 11-20: Add an Imp" },
        { "11-20.add.Inevitable", "During fights 11-20: Add an Inevitable" },
        { "11-20.add.Lich", "During fights 11-20: Add a Lich" },
        { "11-20.add.Madness", "During fights 11-20: Add a Madness" },
        { "11-20.add.Magrat", "During fights 11-20: Add a Magrat" },
        { "11-20.add.Militia", "During fights 11-20: Add a Militia" },
        { "11-20.add.Ogre", "During fights 11-20: Add an Ogre" },
        { "11-20.add.Quartz", "During fights 11-20: Add a Quartz" },
        { "11-20.add.Rat", "During fights 11-20: Add a Rat" },
        { "11-20.add.Rotten", "During fights 11-20: Add a Rotten" },
        { "11-20.add.Saber", "During fights 11-20: Add a Saber" },
        { "11-20.add.Sarcophagus", "During fights 11-20: Add a Sarcophagus" },
        { "11-20.add.Seed", "During fights 11-20: Add a Seed" },
        { "11-20.add.Shade", "During fights 11-20: Add a Shade" },
        { "11-20.add.Slate", "During fights 11-20: Add a Slate" },
        { "11-20.add.Slime Queen", "During fights 11-20: Add a Slime Queen" },
        { "11-20.add.Slimelet", "During fights 11-20: Add a Slimelet" },
        { "11-20.add.Slimer", "During fights 11-20: Add a Slimer" },
        { "11-20.add.Snake", "During fights 11-20: Add a Snake" },
        { "11-20.add.Sniper", "During fights 11-20: Add a Sniper" },
        { "11-20.add.Spider", "During fights 11-20: Add a Spider" },
        { "11-20.add.Spiker", "During fights 11-20: Add a Spiker" },
        { "11-20.add.Sudul", "During fights 11-20: Add a Sudul" },
        { "11-20.add.Tarantus", "During fights 11-20: Add a Tarantus" },
        { "11-20.add.The Hand", "During fights 11-20: Add a The Hand" },
        { "11-20.add.Thorn", "During fights 11-20: Add a Thorn" },
        { "11-20.add.Troll", "During fights 11-20: Add a Troll" },
        { "11-20.add.Troll King", "During fights 11-20: Add a Troll King" },
        { "11-20.add.Warchief", "During fights 11-20: Add a Warchief" },
        { "11-20.add.Wisp", "During fights 11-20: Add a Wisp" },
        { "11-20.add.Wizz", "During fights 11-20: Add a Wizz" },
        { "11-20.add.Wolf", "During fights 11-20: Add a Wolf" },
        { "11-20.add.Z0mbie", "During fights 11-20: Add a Z0mbie" },
        { "11-20.add.Zombie", "During fights 11-20: Add a Zombie" },

        // Standard Numerical / Pip / Turn Curses
        { "2 Fewer Reroll", "-2 reroll" },
        { "2 less max mana", "-2 max stored mana" },
        { "2nd death", "Every 2nd dice you use each turn gains death" },
        { "2nd exert", "Every 2nd dice you use each turn gains exert" },
        { "2nd pain", "Every 2nd dice you use each turn gains pain" },
        { "2nd singleUse", "Every 2nd dice you use each turn gains singleUse" },
        { "3 less max mana", "-3 max stored mana" },
        { "3 pip pain", "All heroes: Add pain to all sides with exactly 3 pips" },
        { "3rd death", "Every 3rd dice you use each turn gains death" },
        { "3rd pain", "Every 3rd dice you use each turn gains pain" },
        { "3rd singleUse", "Every 3rd dice you use each turn gains singleUse" },

        // 3rd Turn Summon Curses
        { "3rd.Alpha", "Summon an Alpha every 3rd turn" },
        { "3rd.Archer", "Summon an Archer every 3rd turn" },
        { "3rd.Bandit", "Summon a Bandit every 3rd turn" },
        { "3rd.Banshee", "Summon a Banshee every 3rd turn" },
        { "3rd.Basilisk", "Summon a Basilisk every 3rd turn" },
        { "3rd.Bee", "Summon a Bee every 3rd turn" },
        { "3rd.Blind", "Summon a Blind every 3rd turn" },
        { "3rd.Boar", "Summon a Boar every 3rd turn" },
        { "3rd.Bones", "Summon a Bones every 3rd turn" },
        { "3rd.Bramble", "Summon a Bramble every 3rd turn" },
        { "3rd.Carrier", "Summon a Carrier every 3rd turn" },
        { "3rd.Caw", "Summon a Caw every 3rd turn" },
        { "3rd.Caw Egg", "Summon a Caw Egg every 3rd turn" },
        { "3rd.Chomp", "Summon a Chomp every 3rd turn" },
        { "3rd.Cyclops", "Summon a Cyclops every 3rd turn" },
        { "3rd.Demon", "Summon a Demon every 3rd turn" },
        { "3rd.Dragon", "Summon a Dragon every 3rd turn" },
        { "3rd.Dragon Egg", "Summon a Dragon Egg every 3rd turn" },
        { "3rd.Fanatic", "Summon a Fanatic every 3rd turn" },
        { "3rd.Ghost", "Summon a Ghost every 3rd turn" },
        { "3rd.Gnoll", "Summon a Gnoll every 3rd turn" },
        { "3rd.Goblin", "Summon a Goblin every 3rd turn" },
        { "3rd.Golem", "Summon a Golem every 3rd turn" },
        { "3rd.Grandma", "Summon a Grandma every 3rd turn" },
        { "3rd.Grave", "Summon a Grave every 3rd turn" },
        { "3rd.Hydra", "Summon a Hydra every 3rd turn" },
        { "3rd.Illusion", "Summon an Illusion every 3rd turn" },
        { "3rd.Imp", "Summon an Imp every 3rd turn" },
        { "3rd.Lich", "Summon a Lich every 3rd turn" },
        { "3rd.Madness", "Summon a Madness every 3rd turn" },
        { "3rd.Militia", "Summon a Militia every 3rd turn" },
        { "3rd.Ogre", "Summon an Ogre every 3rd turn" },
        { "3rd.Quartz", "Summon a Quartz every 3rd turn" },
        { "3rd.Rat", "Summon a Rat every 3rd turn" },
        { "3rd.Saber", "Summon a Saber every 3rd turn" },
        { "3rd.Sarcophagus", "Summon a Sarcophagus every 3rd turn" },
        { "3rd.Seed", "Summon a Seed every 3rd turn" },
        { "3rd.Shade", "Summon a Shade every 3rd turn" },
        { "3rd.Slate", "Summon a Slate every 3rd turn" },
        { "3rd.Slimelet", "Summon a Slimelet every 3rd turn" },
        { "3rd.Slimer", "Summon a Slimer every 3rd turn" },
        { "3rd.Snake", "Summon a Snake every 3rd turn" },
        { "3rd.Sniper", "Summon a Sniper every 3rd turn" },
        { "3rd.Spider", "Summon a Spider every 3rd turn" },
        { "3rd.Spiker", "Summon a Spiker every 3rd turn" },
        { "3rd.Sudul", "Summon a Sudul every 3rd turn" },
        { "3rd.Thorn", "Summon a Thorn every 3rd turn" },
        { "3rd.Troll", "Summon a Troll every 3rd turn" },
        { "3rd.Warchief", "Summon a Warchief every 3rd turn" },
        { "3rd.Wisp", "Summon a Wisp every 3rd turn" },
        { "3rd.Wizz", "Summon a Wizz every 3rd turn" },
        { "3rd.Wolf", "Summon a Wolf every 3rd turn" },
        { "3rd.Z0mbie", "Summon a Z0mbie every 3rd turn" },
        { "3rd.Zombie", "Summon a Zombie every 3rd turn" },

        { "4hp", "All heroes: Set hp to 4" },
        { "4th exert", "Every 4th dice you use each turn gains exert" },
        { "4th singleUse", "Every 4th dice you use each turn gains singleUse" },
        { "5th pain", "Every 5th dice you use each turn gains pain" },

        // Add to Fight Curses
        { "add.Agnes", "Add an Agnes to each fight" },
        { "add.Alpha", "Add an Alpha to each fight" },
        { "add.Archer", "Add an Archer to each fight" },
        { "add.Bandit", "Add a Bandit to each fight" },
        { "add.Banshee", "Add a Banshee to each fight" },
        { "add.Baron", "Add a Baron to each fight" },
        { "add.Basalt", "Add a Basalt to each fight" },
        { "add.Basilisk", "Add a Basilisk to each fight" },
        { "add.Bee", "Add a Bee to each fight" },
        { "add.Bell", "Add a Bell to each fight" },
        { "add.Blind", "Add a Blind to each fight" },
        { "add.Boar", "Add a Boar to each fight" },
        { "add.Bones", "Add a Bones to each fight" },
        { "add.Bramble", "Add a Bramble to each fight" },
        { "add.Carrier", "Add a Carrier to each fight" },
        { "add.Caw", "Add a Caw to each fight" },
        { "add.Caw Egg", "Add a Caw Egg to each fight" },
        { "add.Chomp", "Add a Chomp to each fight" },
        { "add.Cyclops", "Add a Cyclops to each fight" },
        { "add.Demon", "Add a Demon to each fight" },
        { "add.Dragon", "Add a Dragon to each fight" },
        { "add.Dragon Egg", "Add a Dragon Egg to each fight" },
        { "add.Fanatic", "Add a Fanatic to each fight" },
        { "add.Ghost", "Add a Ghost to each fight" },
        { "add.Gnoll", "Add a Gnoll to each fight" },
        { "add.Goblin", "Add a Goblin to each fight" },
        { "add.Golem", "Add a Golem to each fight" },
        { "add.Grandma", "Add a Grandma to each fight" },
        { "add.Grave", "Add a Grave to each fight" },
        { "add.Gytha", "Add a Gytha to each fight" },
        { "add.Hexia", "Add a Hexia to each fight" },
        { "add.Hydra", "Add a Hydra to each fight" },
        { "add.Illusion", "Add an Illusion to each fight" },
        { "add.Imp", "Add an Imp to each fight" },
        { "add.Inevitable", "Add an Inevitable to each fight" },
        { "add.Lich", "Add a Lich to each fight" },
        { "add.Madness", "Add a Madness to each fight" },
        { "add.Magrat", "Add a Magrat to each fight" },
        { "add.Militia", "Add a Militia to each fight" },
        { "add.Ogre", "Add an Ogre to each fight" },
        { "add.Quartz", "Add a Quartz to each fight" },
        { "add.Rat", "Add a Rat to each fight" },
        { "add.Rotten", "Add a Rotten to each fight" },
        { "add.Saber", "Add a Saber to each fight" },
        { "add.Sarcophagus", "Add a Sarcophagus to each fight" },
        { "add.Seed", "Add a Seed to each fight" },
        { "add.Shade", "Add a Shade to each fight" },
        { "add.Slate", "Add a Slate to each fight" },
        { "add.Slime Queen", "Add a Slime Queen to each fight" },
        { "add.Slimelet", "Add a Slimelet to each fight" },
        { "add.Slimer", "Add a Slimer to each fight" },
        { "add.Snake", "Add a Snake to each fight" },
        { "add.Sniper", "Add a Sniper to each fight" },
        { "add.Spider", "Add a Spider to each fight" },
        { "add.Spiker", "Add a Spiker to each fight" },
        { "add.Sudul", "Add a Sudul to each fight" },
        { "add.Tarantus", "Add a Tarantus to each fight" },
        { "add.The Hand", "Add a The Hand to each fight" },
        { "add.Thorn", "Add a Thorn to each fight" },
        { "add.Troll", "Add a Troll to each fight" },
        { "add.Troll King", "Add a Troll King to each fight" },
        { "add.Warchief", "Add a Warchief to each fight" },
        { "add.Wisp", "Add a Wisp to each fight" },
        { "add.Wizz", "Add a Wizz to each fight" },
        { "add.Wolf", "Add a Wolf to each fight" },
        { "add.Z0mbie", "Add a Z0mbie to each fight" },
        { "add.Zombie", "Add a Zombie to each fight" },

        // "All / Hero" Modification Curses
        { "all.Blank", "All heroes: Replace all sides with 'blank'" },
        { "all.boned", "All heroes: Add boned to all sides" },
        { "all.death", "All heroes: Add death to all sides" },
        { "all.decay", "All heroes: Add decay to all sides" },
        { "all.evil", "All heroes: Add evil to all sides" },
        { "all.exert", "All heroes: Add exert to all sides" },
        { "all.fumble", "All heroes: Add fumble to all sides" },
        { "all.generous", "All heroes: Add generous to all sides" },
        { "all.groupexert", "All heroes: Add groupexert to all sides" },
        { "all.guilt", "All heroes: Add guilt to all sides" },
        { "all.halveduel", "All heroes: Add halveduel to all sides" },
        { "all.halveengage", "All heroes: Add halveengage to all sides" },
        { "all.heavy", "All heroes: Add heavy to all sides" },
        { "all.hyperboned", "All heroes: Add hyperboned to all sides" },
        { "all.inflictboned", "All heroes: Add inflictboned to all sides" },
        { "all.Jammed", "All heroes: Replace all sides with 'blank stasis'" },
        { "all.lucky", "All heroes: Add lucky to all sides" },
        { "all.mandatory", "All heroes: Add mandatory to all sides" },
        { "all.manacost", "All heroes: Add manacost to all sides" },
        { "all.pain", "All heroes: Add pain to all sides" },
        { "all.Stuck", "All heroes: Replace all sides with 'blank sticky'" },
        { "all.unusable", "All heroes: Add unusable to all sides" },

        { "hero.boned", "All heroes: Add boned to all sides" },
        { "hero.death", "All heroes: Add death to all sides" },
        { "hero.decay", "All heroes: Add decay to all sides" },
        { "hero.exert", "All heroes: Add exert to all sides" },
        { "hero.fumble", "All heroes: Add fumble to all sides" },
        { "hero.generous", "All heroes: Add generous to all sides" },
        { "hero.groupexert", "All heroes: Add groupexert to all sides" },
        { "hero.guilt", "All heroes: Add guilt to all sides" },
        { "hero.halveduel", "All heroes: Add halveduel to all sides" },
        { "hero.halveengage", "All heroes: Add halveengage to all sides" },
        { "hero.heavy", "All heroes: Add heavy to all sides" },
        { "hero.hyperboned", "All heroes: Add hyperboned to all sides" },
        { "hero.inflictboned", "All heroes: Add inflictboned to all sides" },
        { "hero.mandatory", "All heroes: Add mandatory to all sides" },
        { "hero.pain", "All heroes: Add pain to all sides" },
        { "hero.singleUse", "All heroes: Add singleUse to all sides" },
        { "hero.unusable", "All heroes: Add unusable to all sides" },

        // Archery / Armour / Arthritis
        { "Archery Training", "Archers & Snipers: +2 pips to all sides" },
        { "Armour^1/2", "At the start of the 2nd turn, shield 1 to all enemies" },
        { "Armour^1/1", "At the start of the 1st turn, shield 1 to all enemies" },
        { "Armour^2/1", "At the start of the 1st turn, shield 2 to all enemies" },
        { "Armour^3/1", "At the start of the 1st turn, shield 3 to all enemies" },
        { "Armour^3/2", "At the start of the 2nd turn, shield 3 to all enemies" },
        { "Arthritis^1", "Tier 3 heroes get -1 hp" },
        { "Arthritis^2", "Tier 3 heroes get -2 hp" },

        // Back to Basics / Barricade / Big Hitter / Blank
        { "Back to Basics", "All heroes: Remove all keywords from all sides" },
        { "Barricade", "All most-damaged enemies: Move back" },
        { "Big Hitter^5", "All monsters: Double the pips of all sides with 5 or more pips" },
        { "Big Hitter^6", "All monsters: Double the pips of all sides with 6 or more pips" },
        { "Big Hitter^7", "All monsters: Double the pips of all sides with 7 or more pips" },
        { "Big Hitter^8", "All monsters: Double the pips of all sides with 8 or more pips" },
        { "Big Hitter^9", "All monsters: Double the pips of all sides with 9 or more pips" },
        { "Big Hitter^11", "All monsters: Double the pips of all sides with 11 or more pips" },
        { "Blank", "All heroes: Replace all sides with 'blank'" },
        { "Boar.spirit", "All monsters: The inner hp: Must be removed individually" },
        { "Bonezone", "Bones: Replace all sides with 'Summon 2 bones'" },
        { "Boss Armour^2/1", "During boss fights: At the start of the first turn, shield 2 to all enemies" },
        { "Boss Armour^4/1", "During boss fights: At the start of the first turn, shield 4 to all enemies" },
        { "Boss Bones", "During boss fights : Add a bones" },
        { "Boss Curses^1", "Before boss fights: choose a tier 1 curse" },
        { "Boss Curses^2", "Before boss fights: choose a tier 2 curse" },
        { "Boss Curses^3", "Before boss fights: choose a tier 3 curse" },
        { "Boss Curses^4", "Before boss fights: choose a tier 4 curse" },
        { "Boss Spirits", "For each defeated boss: Add a ghost to each fight" },
        { "Bossilisk", "During fight 4: Fight 2x basilisk instead" },

        // Bottom Side Modifiers
        { "bot.Blank", "All heroes: Replace the bottom side with 'blank'" },
        { "bot.boned", "All heroes: Add boned to the bottom side" },
        { "bot.death", "All heroes: Add death to the bottom side" },
        { "bot.decay", "All heroes: Add decay to the bottom side" },
        { "bot.exert", "All heroes: Add exert to the bottom side" },
        { "bot.fumble", "All heroes: Add fumble to the bottom side" },
        { "bot.groupexert", "All heroes: Add groupexert to the bottom side" },
        { "bot.guilt", "All heroes: Add guilt to the bottom side" },
        { "bot.halveengage", "All heroes: Add halveengage to the bottom side" },
        { "bot.heavy", "All heroes: Add heavy to the bottom side" },
        { "bot.hyperboned", "All heroes: Add hyperboned to the bottom side" },
        { "bot.inflictboned", "All heroes: Add inflictboned to the bottom side" },
        { "bot.Jammed", "All heroes: Replace the bottom side with 'blank stasis'" },
        { "bot.lucky", "All heroes: Add lucky to the bottom side" },
        { "bot.manacost", "All heroes: Add manacost to the bottom side" },
        { "bot.pain", "All heroes: Add pain to the bottom side" },
        { "bot.singleUse", "All heroes: Add singleUse to the bottom side" },
        { "bot.Stuck", "All heroes: Replace the bottom side with 'blank sticky'" },
        { "bot.unusable", "All heroes: Add unusable to the bottom side" },
        { "Bot Caltrops", "All heroes: Replace the bottom side with '1 self damage cantrip'" },
        { "Bottom 1 hp", "Bottom hero: Set hp to 1" },
        { "Bottom Poison^1/1", "Bottom hero: Start poisoned" },
        { "Bottom Poison^2/1", "Bottom hero: Start poisoned for 2" },
        { "Bottom Poison^3/1", "Bottom hero: Start poisoned for 3" },
        { "Bottom Poison^5/1", "Bottom hero: Start poisoned for 5" },
        { "Bones Bones", "All heroes: 1 damage to adjacent allies upon death" },

        // Column Modifiers
        { "col.Blank", "All heroes: Replace the middle column with 'blank'" },
        { "col.decay", "All heroes: Add decay to the middle column" },
        { "col.boned", "All heroes: Add boned to the middle column" },
        { "col.death", "All heroes: Add death to the middle column" },
        { "col.fumble", "All heroes: Add fumble to the middle column" },
        { "col.exert", "All heroes: Add exert to the middle column" },
        { "col.groupexert", "All heroes: Add groupexert to the middle column" },
        { "col.guilt", "All heroes: Add guilt to the middle column" },
        { "col.halveduel", "All heroes: Add halveduel to the middle column" },
        { "col.halveengage", "All heroes: Add halveengage to the middle column" },
        { "col.heavy", "All heroes: Add heavy to the middle column" },
        { "col.hyperboned", "All heroes: Add hyperboned to the middle column" },
        { "col.inflictboned", "All heroes: Add inflictboned to the middle column" },
        { "col.Jammed", "All heroes: Replace the middle column with 'blank stasis'" },
        { "col.lucky", "All heroes: Add lucky to the middle column" },
        { "col.manacost", "All heroes: Add manacost to the middle column" },
        { "col.pain", "All heroes: Add pain to the middle column" },
        { "col.singleUse", "All heroes: Add singleUse to the middle column" },
        { "col.Stuck", "All heroes: Replace the middle column with 'blank sticky'" },
        { "col.unusable", "All heroes: Add unusable to the middle column" },

        // Miscellaneous Curses
        { "Contagion", "All most-hp allies: Start poisoned" },
        { "Cooldown Spells", "Add cooldown to all spells" },
        { "Creaky Joints", "The 1st turn: All heroes: -1 pip to all sides" },
        { "Curse of Horus", "All heroes: -1 pip to all sides" },
        { "Damage Reduced", "All heroes: -1 pip to all damage sides" },
        { "Death Shield^1", "All monsters: Upon death: shield 1 to all allies" },
        { "Death Shield^2", "All monsters: Upon death: shield 2 to all allies" },
        { "Deaths Door", "All heroes: Set starting hp to 1" },
        { "Delevel", "De-level all heroes" },
        { "Depleted Spells", "Add deplete to all spells" },
        { "Doom^8/1", "At the end of the 1st turn, 8 damage to all heroes" },
        { "Doom^8/2", "At the end of the 2nd turn, 8 damage to all heroes" },
        { "Doom^8/3", "At the end of the 3rd turn, 8 damage to all heroes" },
        { "Doom^8/4", "At the end of the 4th turn, 8 damage to all heroes" },
        { "Doom^8/5", "At the end of the 5th turn, 8 damage to all heroes" },
        { "Doom^8/6", "At the end of the 6th turn, 8 damage to all heroes" },
        { "Doom^8/7", "At the end of the 7th turn, 8 damage to all heroes" },
        { "Double Monster HP", "All monsters: +1 hp for each 1 hp" },
        { "Double Monsters", "Double the monsters in each fight (Also double the space for enemies (before becoming reinforcements))" },
        { "Dread", "Dying heroes: -1 pip to all sides" },
        { "Early Curses^1", "Before fights 1-4: choose a tier 1 curse" },
        { "Early Curses^2", "Before fights 1-4: choose a tier 2 curse" },
        { "Early Curses^3", "Before fights 1-4: choose a tier 3 curse" },
        { "Early Curses^4", "Before fights 1-4: choose a tier 4 curse" },
        { "Exposed", "All heroes: x2 to incoming damage if gained no shields" },
        { "Exposed edges", "Top/bottom heroes: x2 to incoming damage if gained no shields" },
        { "Exposed middle", "Middle hero: x2 to incoming damage if gained no shields" },
        { "Expensive Spells", "Spells cost +1 mana" },
        { "Expensiver Spells", "Spells cost x2 mana" },
        { "Fewer Reroll", "-1 reroll" },
        { "Fight 8 curse", "Before fight 8: Choose a tier 4-5 curse" },
        { "Fight 14 curse", "Before fight 14: Choose a tier 7-8 curse" },
        { "First 10 Curses^1", "Before fights 1-10: choose a tier 1 curse" },
        { "First 10 Curses^2", "Before fights 1-10: choose a tier 2 curse" },
        { "First 10 Curses^3", "Before fights 1-10: choose a tier 3 curse" },
        { "First 10 Curses^4", "Before fights 1-10: choose a tier 4 curse" },
        { "Flighty", "Damaged monsters: Move back" },
        { "Ghost.spirit", "All monsters: The 5th hp: Become immune to damage this turn" },
        { "Ghostly Monsters", "All monsters: Every 5th hp (Starts at 4th hp): Become immune to damage this turn " },
        { "Ghostly Monsters/2", "All monsters: All hp: Become immune to damage this turn " },
        { "Ghoststone", "All monsters: Every 2nd hp (Starts at 1st hp): Must be removed individually\nAll monsters: Every 2nd hp (Starts at 2nd hp): Become immune to damage this turn" },
        { "Grave spam", "All monsters except grave: Upon death: summon a grave" },

        // Hero Target-Specific Delevels & Missing Modifiers
        { "h.bot.delevel", "De-level the bottom hero" },
        { "h.bot.missing", "No bottom hero" },
        { "h.bot2.delevel", "De-level the bottom 2 heroes" },
        { "h.bot2.missing", "No bottom 2 heroes" },
        { "h.bot3.delevel", "De-level the bottom 3 heroes" },
        { "h.bot3.missing", "No bottom 3 heroes" },
        { "h.eo.delevel", "De-level every other hero" },
        { "h.eo.missing", "No every other hero" },
        { "h.mid.delevel", "De-level the middle hero" },
        { "h.mid.missing", "No middle hero" },
        { "h.mid3.delevel", "De-level the middle 3 heroes" },
        { "h.mid3.missing", "No middle 3 heroes" },
        { "h.top.delevel", "De-level the top hero" },
        { "h.top.missing", "No top hero" },
        { "h.top2.delevel", "De-level the top 2 heroes" },
        { "h.top2.missing", "No top 2 heroes" },
        { "h.top3.delevel", "De-level the top 3 heroes" },
        { "h.top3.missing", "No top 3 heroes" },
        { "h.top4.delevel", "De-level the top 4 heroes" },
        { "h.top4.missing", "No top 4 heroes" },

        { "Heal Reduced", "All heroes: -1 pip to all heal/selfheal sides" },
        { "Heavy Dice^1", "Cannot roll more than 1 dice at a time" },
        { "Heavy Dice^2", "Cannot roll more than 2 dice at a time" },
        { "Heavy Dice^3", "Cannot roll more than 3 dice at a time" },
        { "Heavy Weapons", "All heroes: Add heavy to all damage sides" },
        { "Hefty", "The top non-magic hero: Add heavy to all sides" },
        { "Heartless", "All heroes: I die whenever i save a hero" },
        { "Hero Immunity", "All heroes: Immune to abilities" },
        { "Highest pain", "All heroes: Add pain to all highest-pip sides" },
        { "Horde", "+50% space for enemies (before becoming reinforcements)" },
        { "Hurried^1", "-1 levelup choice" },
        { "Hurried^2", "-2 levelup choice" },

        // Item Curses (Pre-equipped Item Side-effects)
        { "i.Affliction", "Gain the 'Affliction' item\n(\"Must be equipped\nAdd pain to the two left sides\")" },
        { "i.Amnesia", "Gain the 'Amnesia' item\n(\"Must be equipped\nReplace all sides with 'blank'\")" },
        { "i.Backstab", "Gain the 'Backstab' item\n(\"Must be equipped\nReplace the middle four sides with '4 damage to an ally mandatory generous stasis'\")" },
        { "i.Barrel Hoops", "Gain the 'Barrel Hoops' item\n(\"Must be equipped\n5 damage to adjacent allies upon death\")" },
        { "i.Brittle", "Gain the 'Brittle' item\n(\"Must be equipped\nDeath is permanent\")" },
        { "i.Broken Heart", "Gain the 'Broken Heart' item\n(\"Must be equipped\nImmune to healing\")" },
        { "i.Broken Spirit", "Gain the 'Broken Spirit' item\n(\"Must be equipped\nImmune to healing\nImmune to shields\")" },
        { "i.Coiled Snake", "Gain the 'Coiled Snake' item\n(\"Must be equipped\nStart poisoned\")" },
        { "i.Compulsion", "Gain the 'Compulsion' item\n(\"Must be equipped\nAdd mandatory and pain to all sides\")" },
        { "i.Conscience", "Gain the 'Conscience' item\n(\"Must be equipped\nWhen an enemy dies, 1 self damage\")" },
        { "i.Cursed Bolt", "Gain the 'Cursed Bolt' item\n(\"Must be equipped\nAfter an ability is used, 2 self damage\")" },
        { "i.D4", "Gain the 'D4' item\n(\"Must be equipped\nReplace the right side with '1 self damage cantrip'\")" },
        { "i.Dead Crow", "Gain the 'Dead Crow' item\n(\"Must be equipped\nStart of turn 1: I die\")" },
        { "i.Empathy", "Gain the 'Empathy' item\n(\"Must be equipped\nWhen an enemy dies, i die\")" },
        { "i.Exhaustion", "Gain the 'Exhaustion' item\n(\"Must be equipped\nAdd exert to all sides\")" },
        { "i.Handcuffs", "Gain the 'Handcuffs' item\n(\"Must be equipped\n-1 item slot\")" },
        { "i.Lead Weight", "Gain the 'Lead Weight' item\n(\"Must be equipped\nReplace the top and bottom sides with 'blank'\")" },
        { "i.Martyr", "Gain the 'Martyr' item\n(\"Must be equipped\nAdd death & +2 pips to the two left sides\")" },
        { "i.Mould", "Gain the 'Mould' item\n(\"Must be equipped\nAdd decay to all sides\")" },
        { "i.Parasite", "Gain the 'Parasite' item\n(\"Must be equipped\nSet starting hp to 1\")" },
        { "i.Pharoah's Curse", "Gain the 'Pharoah's Curse' item\n(\"Must be equipped\n-1 pip to all sides\")" },
        { "i.Slimed", "Gain the 'Slimed' item\n(\"Must be equipped\nAdd 'sticky' to all sides\")" },
        { "i.Soul Link", "Gain the 'Soul Link' item\n(\"Must be equipped\nWhen an ally dies, you die\")" },
        { "i.Tracked", "Gain the 'Tracked' item\n(\"Must be equipped\nUpon death: summon 2 wolves\")" },
        { "i.Weariness", "Gain the 'Weariness' item\n(\"Must be equipped\nAdd exert to the two left sides\")" },
        { "i.Wretched Crown", "Gain the 'Wretched Crown' item\n(\"Must be equipped\nUpon death: kill all allies\")" },

        { "Immune Monsters", "The 1st turn: All monsters: Immune to damage" },
        { "Improvised Armour", "The 1st turn: All monsters: Reduce damage taken from abilities and dice by 1" },
        { "Item Poison", "All heroes: Start poisoned per equipped item" },
        { "Jammed", "All heroes: Replace all sides with 'blank stasis'" },

        // Left Side Modifiers
        { "Left Sticky", "All heroes: Add sticky to the left side" },
        { "Left Weak", "All heroes: -1 pip to the left side" },
        { "Left Caltrops", "All heroes: Replace the left side with '1 self damage cantrip'" },
        { "left.Blank", "All heroes: Replace the left side with 'blank'" },
        { "left.boned", "All heroes: Add boned to the left side" },
        { "left.death", "All heroes: Add death to the left side" },
        { "left.decay", "All heroes: Add decay to the left side" },
        { "left.exert", "All heroes: Add exert to the left side" },
        { "left.fumble", "All heroes: Add fumble to the left side" },
        { "left.groupexert", "All heroes: Add groupexert to the left side" },
        { "left.guilt", "All heroes: Add guilt to the left side" },
        { "left.halveduel", "All heroes: Add halveduel to the left side" },
        { "left.halveengage", "All heroes: Add halveengage to the left side" },
        { "left.heavy", "All heroes: Add heavy to the left side" },
        { "left.hyperboned", "All heroes: Add hyperboned to the left side" },
        { "left.inflictboned", "All heroes: Add inflictboned to the left side" },
        { "left.Jammed", "All heroes: Replace the left side with 'blank stasis'" },
        { "left.lucky", "All heroes: Add lucky to the left side" },
        { "left.manacost", "All heroes: Add manacost to the left side" },
        { "left.pain", "All heroes: Add pain to the left side" },
        { "left.singleUse", "All heroes: Add singleUse to the left side" },
        { "left.Stuck", "All heroes: Replace the left side with 'blank sticky'" },
        { "left.unusable", "All heroes: Add unusable to the left side" },
        { "left2.Blank", "All heroes: Replace the two left sides with 'blank'" },
        { "left2.boned", "All heroes: Add boned to the two left sides" },
        { "left2.death", "All heroes: Add death to the two left sides" },
        { "left2.decay", "All heroes: Add decay to the two left sides" },
        { "left2.exert", "All heroes: Add exert to the two left sides" },
        { "left2.fumble", "All heroes: Add fumble to the two left sides" },
        { "left2.groupexert", "All heroes: Add groupexert to the two left sides" },
        { "left2.guilt", "All heroes: Add guilt to the two left sides" },
        { "left2.halveduel", "All heroes: Add halveduel to the two left sides" },
        { "left2.halveengage", "All heroes: Add halveengage to the two left sides" },
        { "left2.heavy", "All heroes: Add heavy to the two left sides" },
        { "left2.hyperboned", "All heroes: Add hyperboned to the two left sides" },
        { "left2.inflictboned", "All heroes: Add inflictboned to the two left sides" },
        { "left2.Jammed", "All heroes: Replace the two left sides with 'blank stasis'" },
        { "left2.lucky", "All heroes: Add lucky to the two left sides" },
        { "left2.manacost", "All heroes: Add manacost to the two left sides" },
        { "left2.pain", "All heroes: Add pain to the two left sides" },
        { "left2.singleUse", "All heroes: Add singleUse to the two left sides" },
        { "left2.Stuck", "All heroes: Replace the two left sides with 'blank sticky'" },
        { "left2.unusable", "All heroes: Add unusable to the two left sides" },

        { "Lightning^1/3", "At the end of the 3rd turn, 1 damage to all heroes" },
        { "Lightning^2/3", "At the end of the 3rd turn, 2 damage to all heroes" },
        { "Lightning^3/3", "At the end of the 3rd turn, 3 damage to all heroes" },
        { "Lowest Exert", "All heroes: Add exert to all lowest-pip sides" },

        // Mana Debt / Spells Curses
        { "Mana Debt^1/1", "The 1st spell you cast each fight costs +1 mana" },
        { "Mana Debt^1/3", "The 3rd spell you cast each fight costs +1 mana" },
        { "Mana Debt^2/1", "The 1st spell you cast each fight costs +2 mana" },
        { "Mana Debt^3/1", "The 1st spell you cast each fight costs +3 mana" },
        { "Mana Debt^3/3", "The 3rd spell you cast each fight costs +3 mana" },
        { "Mana Debt^4/1", "The 1st spell you cast each fight costs +4 mana" },
        { "Mana Debt^5/2", "The 2nd spell you cast each fight costs +5 mana" },
        { "Mana Reduced", "All heroes: -1 pip to all mana/managain sides" },
        { "Many Curses^1", "Before each fight: choose a tier 1 curse" },
        { "Many Curses^2", "Before each fight: choose a tier 2 curse" },
        { "Many Curses^3", "Before each fight: choose a tier 3 curse" },
        { "Many Curses^4", "Before each fight: choose a tier 4 curse" },

        // Middle Side Modifiers
        { "mid.Blank", "All heroes: Replace the middle side with 'blank'" },
        { "mid.boned", "All heroes: Add boned to the middle side" },
        { "mid.death", "All heroes: Add death to the middle side" },
        { "mid.decay", "All heroes: Add decay to the middle side" },
        { "mid.exert", "All heroes: Add exert to the middle side" },
        { "mid.fumble", "All heroes: Add fumble to the middle side" },
        { "mid.groupexert", "All heroes: Add groupexert to the middle side" },
        { "mid.guilt", "All heroes: Add guilt to the middle side" },
        { "mid.halveengage", "All heroes: Add halveengage to the middle side" },
        { "mid.heavy", "All heroes: Add heavy to the middle side" },
        { "mid.hyperboned", "All heroes: Add hyperboned to the middle side" },
        { "mid.inflictboned", "All heroes: Add inflictboned to the middle side" },
        { "mid.Jammed", "All heroes: Replace the middle side with 'blank stasis'" },
        { "mid.lucky", "All heroes: Add lucky to the middle side" },
        { "mid.manacost", "All heroes: Add manacost to the middle side" },
        { "mid.pain", "All heroes: Add pain to the middle side" },
        { "mid.singleUse", "All heroes: Add singleUse to the middle side" },
        { "mid.Stuck", "All heroes: Replace the middle side with 'blank sticky'" },
        { "mid.unusable", "All heroes: Add unusable to the middle side" },
        { "mid2.Blank", "All heroes: Replace the two middle sides with 'blank'" },
        { "mid2.boned", "All heroes: Add boned to the two middle sides" },
        { "mid2.death", "All heroes: Add death to the two middle sides" },
        { "mid2.decay", "All heroes: Add decay to the two middle sides" },
        { "mid2.exert", "All heroes: Add exert to the two middle sides" },
        { "mid2.fumble", "All heroes: Add fumble to the two middle sides" },
        { "mid2.groupexert", "All heroes: Add groupexert to the two middle sides" },
        { "mid2.guilt", "All heroes: Add guilt to the two middle sides" },
        { "mid2.halveduel", "All heroes: Add halveduel to the two middle sides" },
        { "mid2.halveengage", "All heroes: Add halveengage to the two middle sides" },
        { "mid2.heavy", "All heroes: Add heavy to the two middle sides" },
        { "mid2.hyperboned", "All heroes: Add hyperboned to the two middle sides" },
        { "mid2.inflictboned", "All heroes: Add inflictboned to the two middle sides" },
        { "mid2.Jammed", "All heroes: Replace the two middle sides with 'blank stasis'" },
        { "mid2.lucky", "All heroes: Add lucky to the two middle sides" },
        { "mid2.manacost", "All heroes: Add manacost to the two middle sides" },
        { "mid2.pain", "All heroes: Add pain to the two middle sides" },
        { "mid2.singleUse", "All heroes: Add singleUse to the two middle sides" },
        { "mid2.Stuck", "All heroes: Replace the two middle sides with 'blank sticky'" },
        { "mid2.unusable", "All heroes: Add unusable to the two middle sides" },
        { "mid4.Blank", "All heroes: Replace the four middle sides with 'blank'" },
        { "mid4.boned", "All heroes: Add boned to the four middle sides" },
        { "mid4.death", "All heroes: Add death to the four middle sides" },
        { "mid4.decay", "All heroes: Add decay to the four middle sides" },
        { "mid4.exert", "All heroes: Add exert to the four middle sides" },
        { "mid4.fumble", "All heroes: Add fumble to the four middle sides" },
        { "mid4.groupexert", "All heroes: Add groupexert to the four middle sides" },
        { "mid4.guilt", "All heroes: Add guilt to the four middle sides" },
        { "mid4.halveduel", "All heroes: Add halveduel to the four middle sides" },
        { "mid4.halveengage", "All heroes: Add halveengage to the four middle sides" },
        { "mid4.heavy", "All heroes: Add heavy to the four middle sides" },
        { "mid4.hyperboned", "All heroes: Add hyperboned to the four middle sides" },
        { "mid4.inflictboned", "All heroes: Add inflictboned to the four middle sides" },
        { "mid4.Jammed", "All heroes: Replace the four middle sides with 'blank stasis'" },
        { "mid4.lucky", "All heroes: Add lucky to the four middle sides" },
        { "mid4.manacost", "All heroes: Add manacost to the four middle sides" },
        { "mid4.pain", "All heroes: Add pain to the four middle sides" },
        { "mid4.singleUse", "All heroes: Add singleUse to the four middle sides" },
        { "mid4.Stuck", "All heroes: Replace the four middle sides with 'blank sticky'" },
        { "mid4.unusable", "All heroes: Add unusable to the four middle sides" },
        { "Middle 1 hp", "Middle hero: Set hp to 1" },
        { "Migraine", "All heroes: -1 pip to the two left sides" },

        // Monster Stat Modification Curses
        { "Monster Charged", "All monsters: Add charged to all sides" },
        { "Monster Column^1", "All monsters: +1 pip to the middle column" },
        { "Monster Column^2", "All monsters: +2 pips to the middle column" },
        { "Monster HP Per^1/2", "All monsters: +1 hp for each 2 hp" },
        { "Monster HP Per^1/3", "All monsters: +1 hp for each 3 hp" },
        { "Monster HP Per^1/4", "All monsters: +1 hp for each 4 hp" },
        { "Monster HP Per^1/5", "All monsters: +1 hp for each 5 hp" },
        { "Monster HP Per^1/7", "All monsters: +1 hp for each 7 hp" },
        { "Monster HP Per^2/3", "All monsters: +2 hp for each 3 hp" },
        { "Monster HP Per^2/4", "All monsters: +2 hp for each 4 hp" },
        { "Monster HP Per^2/7", "All monsters: +2 hp for each 7 hp" },
        { "Monster HP Per^3/4", "All monsters: +3 hp for each 4 hp" },
        { "Monster HP Per^3/5", "All monsters: +3 hp for each 5 hp" },
        { "Monster HP Per^3/6", "All monsters: +3 hp for each 6 hp" },
        { "Monster HP^1", "All monsters: +1 hp" },
        { "Monster HP^2", "All monsters: +2 hp" },
        { "Monster HP^3", "All monsters: +3 hp" },
        { "Monster HP^4", "All monsters: +4 hp" },
        { "Monster HP^5", "All monsters: +5 hp" },
        { "Monster Immunity", "All monsters: Immune to abilities" },
        { "Monster Left^1", "All monsters: +1 pip to the left side" },
        { "Monster Left^2", "All monsters: +2 pips to the left side" },
        { "Monster Left^3", "All monsters: +3 pips to the left side" },
        { "Monster Left^4", "All monsters: +4 pips to the left side" },
        { "Monster Left^5", "All monsters: +5 pips to the left side" },
        { "Monster Regen^1", "All monsters: Start with 1 regen" },
        { "Monster Regen^1/5", "All monsters with 5 or less hp: Start with 1 regen" },
        { "Monster Regen^1/10", "All monsters with 10 or less hp: Start with 1 regen" },
        { "Monster Regen^2", "All monsters: Start with 2 regen" },
        { "Monster Regen^2/10", "All monsters with 10 or less hp: Start with 2 regen" },
        { "Monster Right^1", "All monsters: +1 pip to the rightmost side" },
        { "Monster Right^2", "All monsters: +2 pips to the rightmost side" },
        { "Monster Rights^1", "All monsters: +1 pip to the two right sides" },
        { "Monster Rights^2", "All monsters: +2 pips to the two right sides" },
        { "Monster Rights^3", "All monsters: +3 pips to the two right sides" },
        { "Monster Row^1", "All monsters: +1 pip to the middle row" },
        { "Monster Row^2", "All monsters: +2 pips to the middle row" },
        { "Monster Shield^1", "At the start of each turn, shield 1 to all enemies" },
        { "Monster Shield^2", "At the start of each turn, shield 2 to all enemies" },
        { "Monster Shield^3", "At the start of each turn, shield 3 to all enemies" },
        { "Monster Bonus^1", "All monsters: +1 pip to all sides" },
        { "Monster Bonus^2", "All monsters: +2 pips to all sides" },
        { "Monster Bonus^3", "All monsters: +3 pips to all sides" },
        { "monster.growth", "All monsters: Add growth to all sides" },
        { "monster.inflictBoned", "All monsters: Add inflictBoned to all sides" },
        { "monster.inflictDeath", "All monsters: Add inflictDeath to all sides" },
        { "monster.poison", "All monsters: Add poison to all sides" },
        { "monster.pristine", "All monsters: Add pristine to all sides" },
        { "monster.selfheal", "All monsters: Add selfheal to all sides" },
        { "monster.vigil", "All monsters: Add vigil to all sides" },
        { "monster.era", "All monsters: Add era to all sides" },

        // Mortal Limits / Poison / Other Statuses
        { "Mortal^1", "All heroes: Shields, health and pips limited to 1" },
        { "Mortal^2", "All heroes: Shields, health and pips limited to 2" },
        { "Mortal^3", "All heroes: Shields, health and pips limited to 3" },
        { "Mortal^4", "All heroes: Shields, health and pips limited to 4" },
        { "Mortal^5", "All heroes: Shields, health and pips limited to 5" },
        { "Mortal^6", "All heroes: Shields, health and pips limited to 6" },
        { "Mortal^7", "All heroes: Shields, health and pips limited to 7" },
        { "Mortal^8", "All heroes: Shields, health and pips limited to 8" },
        { "Mortal^9", "All heroes: Shields, health and pips limited to 9" },
        { "Mortal^13", "All heroes: Shields, health and pips limited to 13" },
        { "Mundane", "All heroes: Replace mana/mana-gain sides with damage sides, retaining their original pips and other keywords" },
        { "No Spells", "You cannot cast spells" },
        { "Non-Boss Bones", "During non-boss fights : Add a bones" },
        { "Odd single use", "All heroes: Add singleUse to all odd sides" },
        { "Ogre.spirit", "All monsters: Every 5th hp (Starts at 3rd hp): +1 pip to all sides this fight" },
        { "One Kill", "All monsters: Upon death: all allies become immune to damage this turn" },
        { "Ouroboros", "Imp: Replace the two right sides with 'Summon a hexia death'" },
        { "Permadeath", "All heroes: Death is permanent" },
        { "Poisoned Tendrils", "All heroes: Start poisoned per hero level" },

        // Poison Statuses for All Heroes
        { "All Poisoned^1", "All heroes: Start poisoned" },
        { "All Poisoned^2", "All heroes: Start poisoned for 2" },
        { "All Poisoned^3", "All heroes: Start poisoned for 3" },
        { "All Poisoned^4", "All heroes: Start poisoned for 4" },
        { "All Poisoned^5", "All heroes: Start poisoned for 5" },

        // Quick Nap / Reroll
        { "Quick Nap^1", "First turn: All heroes: Replace all sides with 'blank'" },
        { "Quick Nap^2", "First 2 turns: All heroes: Replace all sides with 'blank'" },
        { "Quick Nap^3", "First 3 turns: All heroes: Replace all sides with 'blank'" },

        // Right Side Modifiers
        { "right.Blank", "All heroes: Replace the right side with 'blank'" },
        { "right.boned", "All heroes: Add boned to the right side" },
        { "right.death", "All heroes: Add death to the right side" },
        { "right.fumble", "All heroes: Add fumble to the right side" },
        { "right.groupexert", "All heroes: Add groupexert to the right side" },
        { "right.heavy", "All heroes: Add heavy to the right side" },
        { "right.hyperboned", "All heroes: Add hyperboned to the right side" },
        { "right.Jammed", "All heroes: Replace the right side with 'blank stasis'" },
        { "right.lucky", "All heroes: Add lucky to the right side" },
        { "right.manacost", "All heroes: Add manacost to the right side" },
        { "right.pain", "All heroes: Add pain to the right side" },
        { "right.Stuck", "All heroes: Replace the right side with 'blank sticky'" },
        { "right.unusable", "All heroes: Add unusable to the right side" },
        { "right2.Blank", "All heroes: Replace the two right sides with 'blank'" },
        { "right2.boned", "All heroes: Add boned to the two right sides" },
        { "right2.decay", "All heroes: Add decay to the two right sides" },
        { "right2.death", "All heroes: Add death to the two right sides" },
        { "right2.exert", "All heroes: Add exert to the two right sides" },
        { "right2.fumble", "All heroes: Add fumble to the two right sides" },
        { "right2.groupexert", "All heroes: Add groupexert to the two right sides" },
        { "right2.halveengage", "All heroes: Add halveengage to the two right sides" },
        { "right2.heavy", "All heroes: Add heavy to the two right sides" },
        { "right2.hyperboned", "All heroes: Add hyperboned to the two right sides" },
        { "right2.inflictboned", "All heroes: Add inflictboned to the two right sides" },
        { "right2.Jammed", "All heroes: Replace the two right sides with 'blank stasis'" },
        { "right2.lucky", "All heroes: Add lucky to the two right sides" },
        { "right2.manacost", "All heroes: Add manacost to the two right sides" },
        { "right2.pain", "All heroes: Add pain to the two right sides" },
        { "right2.Stuck", "All heroes: Replace the two right sides with 'blank sticky'" },
        { "right2.unusable", "All heroes: Add unusable to the two right sides" },
        { "right3.Blank", "All heroes: Replace the three right sides with 'blank'" },
        { "right3.boned", "All heroes: Add boned to the three right sides" },
        { "right3.decay", "All heroes: Add decay to the three right sides" },
        { "right3.exert", "All heroes: Add exert to the three right sides" },
        { "right3.fumble", "All heroes: Add fumble to the three right sides" },
        { "right3.groupexert", "All heroes: Add groupexert to the three right sides" },
        { "right3.guilt", "All heroes: Add guilt to the three right sides" },
        { "right3.halveduel", "All heroes: Add halveduel to the three right sides" },
        { "right3.halveengage", "All heroes: Add halveengage to the three right sides" },
        { "right3.heavy", "All heroes: Add heavy to the three right sides" },
        { "right3.hyperboned", "All heroes: Add hyperboned to the three right sides" },
        { "right3.inflictboned", "All heroes: Add inflictboned to the three right sides" },
        { "right3.Jammed", "All heroes: Replace the three right sides with 'blank stasis'" },
        { "right3.lucky", "All heroes: Add lucky to the three right sides" },
        { "right3.pain", "All heroes: Add pain to the three right sides" },
        { "right3.singleUse", "All heroes: Add singleUse to the three right sides" },
        { "right3.Stuck", "All heroes: Replace the three right sides with 'blank sticky'" },
        { "right3.unusable", "All heroes: Add unusable to the three right sides" },
        { "right5.Blank", "All heroes: Replace the five right sides with 'blank'" },
        { "right5.boned", "All heroes: Add boned to the five right sides" },
        { "right5.decay", "All heroes: Add decay to the five right sides" },
        { "right5.death", "All heroes: Add death to the five right sides" },
        { "right5.exert", "All heroes: Add exert to the five right sides" },
        { "right5.fumble", "All heroes: Add fumble to the five right sides" },
        { "right5.groupexert", "All heroes: Add groupexert to the five right sides" },
        { "right5.guilt", "All heroes: Add guilt to the five right sides" },
        { "right5.halveduel", "All heroes: Add halveduel to the five right sides" },
        { "right5.halveengage", "All heroes: Add halveengage to the five right sides" },
        { "right5.heavy", "All heroes: Add heavy to the five right sides" },
        { "right5.hyperboned", "All heroes: Add hyperboned to the five right sides" },
        { "right5.inflictboned", "All heroes: Add inflictboned to the five right sides" },
        { "right5.Jammed", "All heroes: Replace the five right sides with 'blank stasis'" },
        { "right5.lucky", "All heroes: Add lucky to the five right sides" },
        { "right5.manacost", "All heroes: Add manacost to the five right sides" },
        { "right5.pain", "All heroes: Add pain to the five right sides" },
        { "right5.singleUse", "All heroes: Add singleUse to the five right sides" },
        { "right5.Stuck", "All heroes: Replace the five right sides with 'blank sticky'" },
        { "right5.unusable", "All heroes: Add unusable to the five right sides" },

        // Rightmost Specific Modifiers
        { "rightmost.Blank", "All heroes: Replace the rightmost side with 'blank'" },
        { "rightmost.death", "All heroes: Add death to the rightmost side" },
        { "rightmost.exert", "All heroes: Add exert to the rightmost side" },
        { "rightmost.hyperboned", "All heroes: Add hyperboned to the rightmost side" },
        { "rightmost.Jammed", "All heroes: Replace the rightmost side with 'blank stasis'" },
        { "rightmost.manacost", "All heroes: Add manacost to the rightmost side" },
        { "rightmost.Stuck", "All heroes: Replace the rightmost side with 'blank sticky'" },
        { "rightmost.unusable", "All heroes: Add unusable to the rightmost side" },
        { "RightTwo Caltrops", "All heroes: Replace the two right sides with '1 self damage cantrip'" },
        { "RightMost Caltrops", "All heroes: Replace the rightmost side with '1 self damage cantrip'" },

        // Row Modifiers
        { "row.Blank", "All heroes: Replace the middle row with 'blank'" },
        { "row.boned", "All heroes: Add boned to the middle row" },
        { "row.death", "All heroes: Add death to the middle row" },
        { "row.decay", "All heroes: Add decay to the middle row" },
        { "row.exert", "All heroes: Add exert to the middle row" },
        { "row.fumble", "All heroes: Add fumble to the middle row" },
        { "row.groupexert", "All heroes: Add groupexert to the middle row" },
        { "row.guilt", "All heroes: Add guilt to the middle row" },
        { "row.halveduel", "All heroes: Add halveduel to the middle row" },
        { "row.halveengage", "All heroes: Add halveengage to the middle row" },
        { "row.heavy", "All heroes: Add heavy to the middle row" },
        { "row.hyperboned", "All heroes: Add hyperboned to the middle row" },
        { "row.inflictboned", "All heroes: Add inflictboned to the middle row" },
        { "row.Jammed", "All heroes: Replace the middle row with 'blank stasis'" },
        { "row.lucky", "All heroes: Add lucky to the middle row" },
        { "row.manacost", "All heroes: Add manacost to the middle row" },
        { "row.pain", "All heroes: Add pain to the middle row" },
        { "row.singleUse", "All heroes: Add singleUse to the middle row" },
        { "row.Stuck", "All heroes: Replace the middle row with 'blank sticky'" },
        { "row.unusable", "All heroes: Add unusable to the middle row" },

        { "Reanimated Bosses", "For each defeated boss: Add a bones to each fight" },
        { "Restless Bones", "All heroes: Upon death: summon 2 bones" },
        { "Rise", "All monsters except bones: Upon death: summon a bones" },
        { "RNG Nest", "During fight 3: Fight 3x dragon egg instead" },
        { "Rude Awakening", "During fight 1: Fight alpha and wolf instead" },
        { "Rushed^1", "-1 offered item" },
        { "Rushed^2", "-2 offered item" },

        // Sandstorm / Sickly / Shields / Spells
        { "Sandstorm^1", "At the end of each turn, 1 damage to all heroes" },
        { "Sandstorm^2", "At the end of each turn, 2 damage to all heroes" },
        { "Sandstorm^3", "At the end of each turn, 3 damage to all heroes" },
        { "Shield Reduced", "All heroes: -1 pip to all shield/selfshield sides" },
        { "Shield Response", "All monsters: After taking damage, self-shield 1" },
        { "Shield Response/2", "All monsters: After taking damage, self-shield 2" },
        { "Sickly^1", "All heroes with 1 or less hp: Add pain to all sides" },
        { "Sickly^2", "All heroes with 2 or less hp: Add pain to all sides" },
        { "Sickly^3", "All heroes with 3 or less hp: Add pain to all sides" },
        { "Single Spells", "Add singleCast to all spells" },
        { "Skip rewards", "Skips rewards" },
        { "Skip rewards 4", "Before fight 4: Skips rewards" },
        { "Skip rewards end", "Before fights 19-20: Skips rewards" },
        { "Skip rewards later", "Before fights 11-20: Skips rewards" },
        { "Skulk^1", "During the 1st turn, +1 pip to enemy sides" },
        { "Skulk^2", "During the 2nd turn, +1 pip to enemy sides" },
        { "Skulk^3", "During the 3rd turn, +1 pip to enemy sides" },
        { "Slide Horde", "Add 10x Slimelet to each fight" },
        { "Slimedemic", "All monsters except slimelet: The 3rd hp: Summon a slimelet " },
        { "Slimer.spirit", "All monsters: Every 5th hp (Starts at 5th hp): summon a slimelet " },
        { "Slippery Dice^0", "You can't lock dice" },
        { "Slippery Dice^1", "Cannot lock more than 1 dice at a time" },
        { "Slippery Dice^2", "Cannot lock more than 2 dice at a time" },
        { "Slippery Dice^3", "Cannot lock more than 3 dice at a time" },
        { "Slow Spells^1", "Maximum of 1 spell cast per turn" },
        { "Slow Spells^2", "Maximum of 2 spells cast per turn" },
        { "Slow Spells^3", "Maximum of 3 spells cast per turn" },
        { "Slow Spells^4", "Maximum of 4 spells cast per turn" },
        { "Small Bonus^1", "All tiny enemies: +1 pip to all sides" },
        { "Small Bonus^2", "All tiny enemies: +2 pips to all sides" },
        { "Small Bonus^3", "All tiny enemies: +3 pips to all sides" },
        { "Sparkly Monsters", "All monsters: The 3rd hp: Must be removed by spell damage" },
        { "Spider Soul", "The 1st turn: All monsters: Upon death: kill the top-most enemy" },
        { "Spiky Monsters^1", "All monsters: On-hit: damage the attacker for 1" },
        { "Spiky Monsters^2", "All monsters: On-hit: damage the attacker for 2" },
        { "Spiky Monsters^5", "All monsters: On-hit: damage the attacker for 5" },
        { "Spiky Monsters^1/5", "All monsters with 5 or more hp: On-hit: damage the attacker for 1" },
        { "Spiky Monsters^1/8", "All monsters with 8 or more hp: On-hit: damage the attacker for 1" },
        { "Spiky Monsters^1/20", "All monsters with 20 or more hp: On-hit: damage the attacker for 1" },
        { "Start Damaged^1/3", "All heroes: 1 of every 3 hp starts empty" },
        { "Start Damaged^1/4", "All heroes: 1 of every 4 hp starts empty" },
        { "Start Damaged^1/6", "All heroes: 1 of every 6 hp starts empty" },
        { "Start Damaged^2/3", "All heroes: 2 of every 3 hp starts empty" },
        { "Start Damaged^2/4", "All heroes: 2 of every 4 hp starts empty" },
        { "Start Damaged^2/6", "All heroes: 2 of every 6 hp starts empty" },
        { "Start Damaged^3/4", "All heroes: 3 of every 4 hp starts empty" },
        { "Start Damaged^1/2", "All heroes: 1 of every 2 hp starts empty" },
        { "Static Blanks", "All heroes: Add stasis to all blank sides" },
        { "Sticky Blanks", "All heroes: Add sticky to all blank sides" },
        { "Sticky Fingers", "All heroes: Add sticky to the top and bottom sides" },
        { "Stone first", "The 1st turn: All monsters: All hp: must be removed individually " },
        { "Stone monsters", "All monsters: All hp: must be removed individually " },
        { "Stone Rain", "At the end of each turn, 1 damage to all heroes petrify" },
        { "Stony Grasp", "All heroes: Start petrified for 1" },
        { "Stony Grasp/2", "All heroes: Start petrified for 6" },
        { "Stuck", "All heroes: Replace all sides with 'blank sticky'" },

        // Summon Curses
        { "Summon.Alpha", "Summon an Alpha every turn" },
        { "Summon.Archer", "Summon an Archer every turn" },
        { "Summon.Bandit", "Summon a Bandit every turn" },
        { "Summon.Banshee", "Summon a Banshee every turn" },
        { "Summon.Basilisk", "Summon a Basilisk every turn" },
        { "Summon.Bee", "Summon a Bee every turn" },
        { "Summon.Blind", "Summon a Blind every turn" },
        { "Summon.Boar", "Summon a Boar every turn" },
        { "Summon.Bones", "Summon a Bones every turn" },
        { "Summon.Bramble", "Summon a Bramble every turn" },
        { "Summon.Carrier", "Summon a Carrier every turn" },
        { "Summon.Caw", "Summon a Caw every turn" },
        { "Summon.Caw Egg", "Summon a Caw Egg every turn" },
        { "Summon.Chomp", "Summon a Chomp every turn" },
        { "Summon.Cyclops", "Summon a Cyclops every turn" },
        { "Summon.Demon", "Summon a Demon every turn" },
        { "Summon.Dragon", "Summon a Dragon every turn" },
        { "Summon.Dragon Egg", "Summon a Dragon Egg every turn" },
        { "Summon.Fanatic", "Summon a Fanatic every turn" },
        { "Summon.Ghost", "Summon a Ghost every turn" },
        { "Summon.Gnoll", "Summon a Gnoll every turn" },
        { "Summon.Goblin", "Summon a Goblin every turn" },
        { "Summon.Golem", "Summon a Golem every turn" },
        { "Summon.Grandma", "Summon a Grandma every turn" },
        { "Summon.Grave", "Summon a Grave every turn" },
        { "Summon.Hydra", "Summon a Hydra every turn" },
        { "Summon.Illusion", "Summon an Illusion every turn" },
        { "Summon.Imp", "Summon an Imp every turn" },
        { "Summon.Lich", "Summon a Lich every turn" },
        { "Summon.Log", "Summon a Log every turn" },
        { "Summon.Madness", "Summon a Madness every turn" },
        { "Summon.Militia", "Summon a Militia every turn" },
        { "Summon.Ogre", "Summon an Ogre every turn" },
        { "Summon.Quartz", "Summon a Quartz every turn" },
        { "Summon.Rat", "Summon a Rat every turn" },
        { "Summon.Saber", "Summon a Saber every turn" },
        { "Summon.Sarcophagus", "Summon a Sarcophagus every turn" },
        { "Summon.Seed", "Summon a Seed every turn" },
        { "Summon.Shade", "Summon a Shade every turn" },
        { "Summon.Slate", "Summon a Slate every turn" },
        { "Summon.Slimelet", "Summon a Slimelet every turn" },
        { "Summon.Slimer", "Summon a Slimer every turn" },
        { "Summon.Snake", "Summon a Snake every turn" },
        { "Summon.Sniper", "Summon a Sniper every turn" },
        { "Summon.Spider", "Summon a Spider every turn" },
        { "Summon.Spiker", "Summon a Spiker every turn" },
        { "Summon.Sudul", "Summon a Sudul every turn" },
        { "Summon.Thorn", "Summon a Thorn every turn" },
        { "Summon.Troll", "Summon a Troll every turn" },
        { "Summon.Warchief", "Summon a Warchief every turn" },
        { "Summon.Wisp", "Summon a Wisp every turn" },
        { "Summon.Wizz", "Summon a Wizz every turn" },
        { "Summon.Wolf", "Summon a Wolf every turn" },
        { "Summon.Z0mbie", "Summon a Z0mbie every turn" },
        { "Summon.Zombie", "Summon a Zombie every turn" },
        { "Summoning Circle", "All monsters: +1 pip to all summon sides" },

        // Top Side Modifiers
        { "top.Blank", "All heroes: Replace the top side with 'blank'" },
        { "top.boned", "All heroes: Add boned to the top side" },
        { "top.death", "All heroes: Add death to the top side" },
        { "top.decay", "All heroes: Add decay to the top side" },
        { "top.exert", "All heroes: Add exert to the top side" },
        { "top.fumble", "All heroes: Add fumble to the top side" },
        { "top.groupexert", "All heroes: Add groupexert to the top side" },
        { "top.guilt", "All heroes: Add guilt to the top side" },
        { "top.halveengage", "All heroes: Add halveengage to the top side" },
        { "top.heavy", "All heroes: Add heavy to the top side" },
        { "top.hyperboned", "All heroes: Add hyperboned to the top side" },
        { "top.inflictboned", "All heroes: Add inflictboned to the top side" },
        { "top.Jammed", "All heroes: Replace the top side with 'blank stasis'" },
        { "top.lucky", "All heroes: Add lucky to the top side" },
        { "top.manacost", "All heroes: Add manacost to the top side" },
        { "top.pain", "All heroes: Add pain to the top side" },
        { "top.singleUse", "All heroes: Add singleUse to the top side" },
        { "top.Stuck", "All heroes: Replace the top side with 'blank sticky'" },
        { "top.unusable", "All heroes: Add unusable to the top side" },
        { "Top 1 hp", "Top hero: Set hp to 1" },

        // Top and Bottom Combination Modifiers
        { "topbot.Blank", "All heroes: Replace top and bottom sides with 'blank'" },
        { "topbot.boned", "All heroes: Add boned to top and bottom sides" },
        { "topbot.decay", "All heroes: Add decay to top and bottom sides" },
        { "topbot.death", "All heroes: Add death to top and bottom sides" },
        { "topbot.exert", "All heroes: Add exert to top and bottom sides" },
        { "topbot.fumble", "All heroes: Add fumble to top and bottom sides" },
        { "topbot.groupexert", "All heroes: Add groupexert to top and bottom sides" },
        { "topbot.guilt", "All heroes: Add guilt to top and bottom sides" },
        { "topbot.halveduel", "All heroes: Add halveduel to top and bottom sides" },
        { "topbot.halveengage", "All heroes: Add halveengage to top and bottom sides" },
        { "topbot.heavy", "All heroes: Add heavy to top and bottom sides" },
        { "topbot.hyperboned", "All heroes: Add hyperboned to top and bottom sides" },
        { "topbot.inflictboned", "All heroes: Add inflictboned to top and bottom sides" },
        { "topbot.Jammed", "All heroes: Replace top and bottom sides with 'blank stasis'" },
        { "topbot.lucky", "All heroes: Add lucky to top and bottom sides" },
        { "topbot.manacost", "All heroes: Add manacost to top and bottom sides" },
        { "topbot.pain", "All heroes: Add pain to top and bottom sides" },
        { "topbot.singleUse", "All heroes: Add singleUse to top and bottom sides" },
        { "topbot.Stuck", "All heroes: Replace top and bottom sides with 'blank sticky'" },
        { "topbot.unusable", "All heroes: Add unusable to top and bottom sides" },

        // Tower / Tunnel Vision / Spells / Wurst
        { "Tower^3", "All huge enemies: +3 hp" },
        { "Tower^7", "All huge enemies: +7 hp" },
        { "Tower^12", "All huge enemies: +12 hp" },
        { "Tower^17", "All huge enemies: +17 hp" },
        { "Training", "All hero-sized enemies: Replace the left side with '6 damage'" },
        { "Tunnel Vision^1", "-1 levelup choice\n-1 offered item" },
        { "Tunnel Vision^2", "-2 levelup choice\n-2 offered item" },
        { "Tough Hp^1", "All monsters: The inner hp: Takes 2 damage at once to remove " },
        { "Tough Hp^2", "All monsters: The inner 2 hp: Takes 2 damage at once to remove" },
        { "Tough Hp^3", "All monsters: The inner 3 hp: Takes 2 damage at once to remove" },
        { "Tough Hp^4", "All monsters: The inner 4 hp: Takes 2 damage at once to remove" },
        { "Turn 2 Immune", "The 2nd turn: All monsters: Immune to damage" },
        { "Turn 3 Death", "The 3rd turn: All heroes: Add death to all sides" },
        { "Undying Monsters", "The 1st turn: All monsters: Cannot die" },
        { "Undying Monsters/2", "First 2 turns: All monsters: Cannot die" },
        { "Uhh", "All monsters: Set all sides to 3" },
        { "Wanded", "All heroes: Add singleUse to the two left sides" },
        { "Worse Items^1", "-1 item quality" },
        { "Worse Items^2", "-2 item quality" },
        { "Worse Items^3", "-3 item quality" },
        { "Worse Items^4", "-4 item quality" },
        { "Wurst", "The 2nd turn: No burst" },
        { "Wurst/2", "Add cooldown to burst" },
        { "Wurst/3", "Add singleCast to burst" },
        { "Wurst/4", "First 2 turns: No burst" },
        { "Wurst/5", "No burst" }
    };
}

public static class BlessingDataset
{
    public static readonly Dictionary<string, string> Blessings = new Dictionary<string, string>
    {
        // Standard Blessings
        { "5 max mana", "+5 max stored mana" },
        { "5th growth", "Every 5th dice you use each turn gains growth" },
        { "5th selfHeal", "Every 5th dice you use each turn gains selfheal" },
        { "L19 Blessing", "Before fight 19: choose a tier 4 blessing" },
        { "Latent", "All heroes: Start with 3 regen\\nAll heroes: Start with 3 poison" },
        { "Level 12 loot", "Before fight 12: standardloot phase" },
        { "Level 16 loot", "Before fight 16: standardloot phase" },
        { "Level 17 levelup", "Before fight 17: levelup phase" },
        { "Level 8 loot", "Before fight 8: standardloot phase" },
        { "Mana Spring", "The 6th spell you cast each fight costs -2 mana" },
        { "Perceptive^1", "+1 offered item" },
        { "Pipe dream", "All heroes: Add sprint to the middle two sides" },
        { "Rest", "During fight 19: All heroes: +5 pips to all sides" },
        { "Versatile^1", "+1 levelup choice" },
        { "3rd selfHeal", "Every 3rd dice you use each turn gains selfheal" },
        { "5th selfShield", "Every 5th dice you use each turn gains selfshield" },
        { "Better Items^1", "+1 item quality" },
        { "Challenge each fight", "Before each fight: standard challenge" },
        { "Deep Pockets", "All heroes: +1 item slot (max 4)" },
        { "Divine^1", "All heroes: Side pips minimum 1" },
        { "Growth fan", "Gain the items 'Seedling' and 'Glowing Egg'" },
        { "Keep Rolls", "Keep unused rerolls" },
        { "Level 7 levelup", "Before fight 7: levelup phase" },
        { "Leyline^8", "The 8th spell you cast each fight is free." },
        { "Perceptive^2", "+2 offered items" },
        { "Underworld Deal", "All heroes: No hp penalty when defeated" },
        { "Versatile^2", "+2 levelup choices" },
        { "2nd selfHeal", "Every 2nd dice you use each turn gains selfheal" },
        { "3rd deathwish", "Every 3rd dice you use each turn gains deathwish" },
        { "3rd selfShield", "Every 3rd dice you use each turn gains selfshield" },
        { "5th copycat", "Every 5th dice you use each turn gains copycat" },
        { "Boss Smash^1", "During boss levels: All heroes: +1 pip to all sides" },
        { "Cataclysm", "At the end of the 7th turn, kill all enemies (except reinforcements)" },
        { "Essence Capture^1", "All heroes: Upon death: +1 mana" },
        { "Keep unused rerolls", "Keep unused rerolls" },
        { "Essence Thief", "All monsters: Upon death: +1 mana" },
        { "Extra reroll", "+1 reroll" },
        { "Fizzing", "At the start of the first turn, +2 mana" },
        { "Greased Dice", "The first turn: +1 reroll\\nDuring boss levels: +1 reroll" },
        { "Healest", "All heroes: +1 pip to all heal/selfheal sides" },
        { "Jewelled Chalice", "At the start of the first 4 turns, +1 mana" },
        { "L9 Blessing", "Before fight 9: choose a tier 4 blessing" },
        { "Level 3 levelup", "Before fight 3: levelup phase" },
        { "Leyline^5", "The 5th spell you cast each fight is free." },
        { "Lucky Start", "The 1st turn: +2 Rerolls" },
        { "Mana per boss", "For each defeated boss: At the start of the 1st turn, +1 mana" },
        { "Middle Shield", "All heroes: Add selfshield to the middle side" },
        { "Perceptive^4", "+4 offered items" },
        { "Poison Immunity", "All heroes: Immune to poison" },
        { "Preparation", "At the start of the first turn, shield 2 to all heroes" },
        { "Save Spell", "Learn Spell 'Save' (Cost: 1 mana - Heal and shield 5 cleanse, singleCast)" },
        { "Stun Specialist", "Gain the items 'Fearless' and 'Wand of Stun')" },
        { "Survive", "All heroes: +1 to incoming healing\\nAll heroes: +2 empty hp" },
        { "Threee", "At the start of every 3rd turn, +3 mana" },
        { "Turn 3 Heal", "At the start of the 3rd turn, heal and shield 3 to all heroes" },
        { "Unsummon", "All monsters: -1 pip on all summon sides" },
        { "Versatile^4", "+4 levelup choices" },
        { "Youth", "Tier 1 heroes: +4 hp\\nTier 2 heroes: +2 hp" },
        { "2 hero hp", "All heroes: +2 hp" },
        { "Better Items^2", "+2 item quality" },
        { "Bolt Spell", "Learn Spell 'Bolt' (Cost: 3 mana - 5 damage)" },
        { "Gym", "+1 hp per hero level" },
        { "Me First", "All heroes: Add first to the left side" },
        { "Monster Right Pain", "All monsters: Add pain to the right side" },
        { "Perceptive^6", "+6 offered items" },
        { "Shield Plus", "All heroes: +1 pip to all shield/selfshield sides" },
        { "Treasure Seeker", "All heroes: +1 item slot (max 4)\\n+1 item quality" },
        { "Versatile^6", "+6 levelup choices" },
        { "2nd selfShield", "Every 2nd dice you use each turn gains selfshield" },
        { "Bone Math", "All monsters: 1 damage to adjacent allies upon death" },
        { "Display Case", "If there is exactly 1 item in the inventory: All heroes: Copy all unequipped items" },
        { "Fumes", "All most-hp enemies: Start poisoned for 2" },
        { "Leyline^1", "The 1st spell you cast each fight is free." },
        { "Nicknack Knapsack", "Before each fight: choose a tier 1 item\\nAll heroes: +1 item slot (max 4)" },
        { "Perceptive^8", "+8 offered items" },
        { "Versatile^8", "+8 levelup choices" },
        { "2 Extra rerolls", "+2 reroll" },
        { "Better Items^3", "+3 item quality" },
        { "Boss Smash^3", "During boss levels: All heroes: +3 pips to all sides" },
        { "Crackling", "At the start of the 1st turn, +4 mana" },
        { "Hamstring", "All monsters: -1 pip to the two right sides" },
        { "Hero Regen^1", "All heroes: Start with 1 regen" },
        { "Hunt^1", "All monsters: Start vulnerable for 1" },
        { "Infinite Chalice", "At the start of each turn, +1 mana\\nAt the start of every 4th turn, +2 mana" },
        { "L9 Blessing^10", "Before fight 9: choose a tier 10 blessing" },
        { "Monster Blank", "All monsters: Replace the left side with 'blank'" },
        { "Monster Hp Down^1", "All monsters: -1 hp" },
        { "Perceptive^11", "+11 offered items" },
        { "Reroll per boss", "For each defeated boss: +1 reroll" },
        { "Survive/2", "All heroes: +1 to incoming healing and shields\\nAll heroes: +4 empty hp" },
        { "Versatile^11", "+11 levelup choices" },
        { "Damaged Monsters", "All monsters: 1 of every 4 hp starts empty" },
        { "Warmup", "The first turn: All monsters: -1 pip to all sides" },
        { "Essence Capture^2", "All heroes: Upon death: +2 mana" },
        { "Free turn", "The 1st turn: All monsters: -1 pip to all sides" },
        { "Great Start", "The 1st turn: All heroes: +1 pip to all sides" },
        { "Perceptive^13", "+13 offered items" },
        { "Versatile^13", "+13 levelup choices" },
        { "4 hero hp", "2x [All heroes: +2 hp]" },
        { "Better Items^4", "+4 item quality" },
        { "Divine^2", "All heroes: Side pips minimum 2" },
        { "Double Loot", "Before every 2nd fight: standardloot phase" },
        { "Reliable", "All heroes: Replace blank sides with my middle side" },
        { "Boss Smash^99", "During boss levels: All heroes: +99 pips to all sides" },
        { "Absorb bosses", "For each defeated boss: All heroes: +1 pip to all sides" },
        { "Better Items^5", "+5 item quality" },
        { "Essence Capture^3", "All heroes: Upon death: +3 mana" },
        { "Hero Regen^2", "All heroes: Start with 2 regen" },
        { "Monster Left Pain", "All monsters: Add pain to the left side" },
        { "Hunt^2", "All monsters: Start vulnerable for 2" },
        { "Monster Hp Down^2", "All monsters: -2 hp" },
        { "Better Items^6", "+6 item quality" },
        { "Better Items^7", "+7 item quality" },
        { "Essence Capture^4", "All heroes: Upon death: +4 mana" },
        { "Hero Regen^3", "All heroes: Start with 3 regen" },
        { "Double XP", "Before every 2nd fight: levelup phase" },
        { "Favour of Horus", "All heroes: +1 pip to all sides" },
        { "Level up", "Level-up all heroes by 1" },
        { "Monster Hp Down^3", "All monsters: -3 hp" },
        { "Better Items^8", "+8 item quality" },
        { "Hunt^3", "All monsters: Start vulnerable for 3" },
        { "Essence Capture^5", "All heroes: Upon death: +5 mana" },
        { "Better Items^9", "+9 item quality" },
        { "Monster Hp Down^4", "All monsters: -4 hp" },
        { "Better Items^10", "+10 item quality" },
        { "Divine^3", "All heroes: Side pips minimum 3" },
        { "Essence Capture^6", "All heroes: Upon death: +6 mana" },
        { "Monster Hp Down^5", "All monsters: -5 hp" },
        { "Barrel Time", "All monsters: 5 damage to adjacent allies upon death" },
        { "MonsterMidDeath", "All monsters: Add death to the middle side" },
        { "Double Pips", "All heroes: Double the pips of all sides" },
        { "Divine^4", "All heroes: Side pips minimum 4" },
        { "Triple Pips", "All heroes: Triple the pips of all sides" },
        { "Ascend", "Level-up all heroes by 3" },
        { "Peace", "All monsters: -5 pips to all sides" },

        // Generated blessings - Add monster blessings
        { "add.Barrel", "Add a Barrel to each fight" },
        { "add.Fountain", "Add a Fountain to each fight" },

        // Level up & Ascend blessings
        { "h.top.Level up", "Level-up the top hero by 1" },
        { "h.mid.Level up", "Level-up the middle hero by 1" },
        { "h.bot.Level up", "Level-up the bottom hero by 1" },
        { "h.top2.Level up", "Level-up the top two heroes by 1" },
        { "h.bot2.Level up", "Level-up the bottom two heroes by 1" },
        { "h.top3.Level up", "Level-up the top three heroes by 1" },
        { "h.eo.Level up", "Level-up every other hero by 1" },
        { "h.mid3.Level up", "Level-up the middle three heroes by 1" },
        { "h.bot3.Level up", "Level-up the bottom three heroes by 1" },
        { "h.top4.Level up", "Level-up the top four heroes by 1" },
        { "h.top.Ascend", "Level-up the top hero by 3" },
        { "h.mid.Ascend", "Level-up the middle hero by 3" },
        { "h.bot.Ascend", "Level-up the bottom hero by 3" },
        { "h.top2.Ascend", "Level-up the top two heroes by 3" },
        { "h.bot2.Ascend", "Level-up the bottom two heroes by 3" },
        { "h.top3.Ascend", "Level-up the top three heroes by 3" },
        { "h.eo.Ascend", "Level-up every other hero by 3" },
        { "h.mid3.Ascend", "Level-up the middle three heroes by 3" },
        { "h.bot3.Ascend", "Level-up the bottom three heroes by 3" },
        { "h.top4.Ascend", "Level-up the top four heroes by 3" },

        // Hero keyword blessings
        { "hero.reborn", "All heroes: Add reborn to all sides" },
        { "hero.ego", "All heroes: Add ego to all sides" },
        { "hero.patient", "All heroes: Add patient to all sides" },
        { "hero.terminal", "All heroes: Add terminal to all sides" },
        { "hero.ranged", "All heroes: Add ranged to all sides" },
        { "hero.sixth", "All heroes: Add sixth to all sides" },
        { "hero.dispel", "All heroes: Add dispel to all sides" },
        { "hero.duel", "All heroes: Add duel to all sides" },
        { "hero.enduring", "All heroes: Add enduring to all sides" },
        { "hero.focus", "All heroes: Add focus to all sides" },
        { "hero.undergrowth", "All heroes: Add undergrowth to all sides" },
        { "hero.cruel", "All heroes: Add cruel to all sides" },
        { "hero.fierce", "All heroes: Add fierce to all sides" },
        { "hero.pair", "All heroes: Add pair to all sides" },
        { "hero.selfheal", "All heroes: Add selfheal to all sides" },
        { "hero.trio", "All heroes: Add trio to all sides" },
        { "hero.critical", "All heroes: Add critical to all sides" },
        { "hero.growth", "All heroes: Add growth to all sides" },
        { "hero.overdog", "All heroes: Add overdog to all sides" },
        { "hero.rescue", "All heroes: Add rescue to all sides" },
        { "hero.underdog", "All heroes: Add underdog to all sides" },
        { "hero.deathwish", "All heroes: Add deathwish to all sides" },
        { "hero.duplicate", "All heroes: Add duplicate to all sides" },
        { "hero.selfshield", "All heroes: Add selfshield to all sides" },
        { "hero.vigil", "All heroes: Add vigil to all sides" },
        { "hero.engage", "All heroes: Add engage to all sides" },
        { "hero.steel", "All heroes: Add steel to all sides" },
        { "hero.repel", "All heroes: Add repel to all sides" },
        { "hero.inspired", "All heroes: Add inspired to all sides" },
        { "hero.pristine", "All heroes: Add pristine to all sides" },
        { "hero.copycat", "All heroes: Add copycat to all sides" },
        { "hero.era", "All heroes: Add era to all sides" },
        { "hero.bloodlust", "All heroes: Add bloodlust to all sides" },
        { "hero.fluctuate", "All heroes: Add fluctuate to all sides" },
        { "hero.echo", "All heroes: Add echo to all sides" },
        { "hero.groupgrowth", "All heroes: Add groupgrowth to all sides" },
        { "hero.groooooowth", "All heroes: Add groooooowth to all sides" },
        { "hero.cantrip", "All heroes: Add cantrip to all sides" },
        { "hero.manaGain", "All heroes: Add manaGain to all sides" },
        { "hero.charged", "All heroes: Add charged to all sides" },
        { "hero.flesh", "All heroes: Add flesh to all sides" },

        // Monster keyword blessings
        { "monster.decay", "All monsters: Add decay to all sides" },
        { "monster.singleUse", "All monsters: Add singleUse to all sides" },
        { "monster.exert", "All monsters: Add exert to all sides" },

        // Monster spirit blessings
        { "Basalt.spirit", "All monsters: The first time I take exactly 1 damage, double it to 2, then increase 1 to 2" },
        { "Quartz.spirit", "All monsters: The 3rd hp: i die if this is removed and no further" },
        { "Cyclops.spirit", "All monsters: The middle hp: Stunned this turn" },
        { "Bones.spirit", "All monsters: 1 damage to adjacent allies upon death" },
        { "Hydra.spirit", "All monsters: I die if i take damage 5 times in a turn" },
        { "Militia.spirit", "All monsters: If an enemy i target gets 5+ shields, i flee" },
        { "Blind.spirit", "All monsters: At the end of the turn, if no damage was dealt to any monster, i flee" },
        { "Goblin.spirit", "All monsters: Flee if alone" },
        { "Wisp.spirit", "All monsters: The third hp: +1 mana" },
        { "Bandit.spirit", "All monsters: Flee if an adjacent monster is overkilled by 2 or more" },
        { "Zombie.spirit", "All monsters: i die if i take 4 or more damage in a single attack" },
        { "Baron.spirit", "All monsters: Every 2nd hp: +1 mana" },
        { "Carrier.spirit", "All monsters: Start poisoned for 2" },

        // Add hero blessings
        { "add.[Tier 1 hero]", "Add a [Tier 1 hero] to your party" },
        { "add.[Tier 2 hero]", "Add a [Tier 2 hero] to your party" },
        { "add.[Tier 3 hero]", "Add a [Tier 3 hero] to your party" },

        // Item blessings
        { "i.[Tier 2 item]", "Gain the [Tier 2 item]" },
        { "i.[Tier 3 item]", "Gain the [Tier 3 item]" },
        { "i.[Tier 4 item]", "Gain the [Tier 4 item]" },
        { "i.[Tier 5 item]", "Gain the [Tier 5 item]" },
        { "i.[Tier 6 item]", "Gain the [Tier 6 item]" },
        { "i.[Tier 7 item]", "Gain the [Tier 7 item]" },
        { "i.[Tier 8 item]", "Gain the [Tier 8 item]" },
        { "i.[Tier 9 item]", "Gain the [Tier 9 item]" },
        { "i.[Tier 10 item]", "Gain the [Tier 10 item]" },
        { "i.[Tier 11 item]", "Gain the [Tier 11 item]" },
        { "i.[Tier 12 item]", "Gain the [Tier 12 item]" },
        { "i.[Tier 13 item]", "Gain the [Tier 13 item]" },
        { "i.[Tier 14 item]", "Gain the [Tier 14 item]" },
        { "i.[Tier 15 item]", "Gain the [Tier 15 item]" },
        { "i.[Tier 16 item]", "Gain the [Tier 16 item]" },
        { "i.[Tier 17 item]", "Gain the [Tier 17 item]" }
    };
}