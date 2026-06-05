using System.Collections.Generic;

public enum MonsterSize
{
    Tiny,
    HeroSized,
    Big,
    Huge
}

public static class MonsterDatabase
{
    // SPECIAL MONSTERS
    /*
        jinx.<curse>.
        Orb.<untargeted spell>.
        vase.<modifier>.
        rmon.0 for normal size
        rmon.3 for big
        rmon.4 for tiny
        rmon.1a for huge 
        egg.<other monster or hero to egg for> 
     */

    // Map MonsterType to MonsterSize
    public static readonly Dictionary<MonsterType, MonsterSize> SizeMapping = new Dictionary<MonsterType, MonsterSize>()
    {
        // Tiny
        { MonsterType.Bones, MonsterSize.Tiny },
        { MonsterType.Slimelet, MonsterSize.Tiny },
        { MonsterType.Illusion, MonsterSize.Tiny },
        { MonsterType.Imp, MonsterSize.Tiny },
        { MonsterType.Sniper, MonsterSize.Tiny },
        { MonsterType.Wisp, MonsterSize.Tiny },
        { MonsterType.Rat, MonsterSize.Tiny },
        { MonsterType.Log, MonsterSize.Tiny },
        { MonsterType.Archer, MonsterSize.Tiny },
        { MonsterType.Thorn, MonsterSize.Tiny },
        { MonsterType.Spider, MonsterSize.Tiny },
        { MonsterType.Grave, MonsterSize.Tiny },
        { MonsterType.CawEgg, MonsterSize.Tiny },
        { MonsterType.Seed, MonsterSize.Tiny },
        { MonsterType.Chest, MonsterSize.Tiny },
        { MonsterType.Bee, MonsterSize.Tiny },
        { MonsterType.Shade, MonsterSize.Tiny },
        { MonsterType.Egg, MonsterSize.Tiny },
        { MonsterType.Vase, MonsterSize.Tiny },
        { MonsterType.Rm_t, MonsterSize.Tiny },    


        // Hero-Sized
        { MonsterType.Quartz, MonsterSize.HeroSized },
        { MonsterType.Wolf, MonsterSize.HeroSized },
        { MonsterType.Grandma, MonsterSize.HeroSized },
        { MonsterType.Snake, MonsterSize.HeroSized },
        { MonsterType.Fanatic, MonsterSize.HeroSized },
        { MonsterType.Gnoll, MonsterSize.HeroSized },
        { MonsterType.Goblin, MonsterSize.HeroSized },
        { MonsterType.Sudul, MonsterSize.HeroSized },
        { MonsterType.Saber, MonsterSize.HeroSized },
        { MonsterType.Warchief, MonsterSize.HeroSized },
        { MonsterType.Bandit, MonsterSize.HeroSized },
        { MonsterType.DragonEgg, MonsterSize.HeroSized },
        { MonsterType.Zombie, MonsterSize.HeroSized },
        { MonsterType.Z0mbie, MonsterSize.HeroSized },
        { MonsterType.Golem, MonsterSize.HeroSized },
        { MonsterType.Blind, MonsterSize.HeroSized },
        { MonsterType.Barrel, MonsterSize.HeroSized },
        { MonsterType.Fountain, MonsterSize.HeroSized },
        { MonsterType.Militia, MonsterSize.HeroSized },
        { MonsterType.Carrier, MonsterSize.HeroSized },
        { MonsterType.PainSigil, MonsterSize.HeroSized },
        { MonsterType.DecaySigil, MonsterSize.HeroSized },
        { MonsterType.DeathSigil, MonsterSize.HeroSized },
        { MonsterType.Rm_n, MonsterSize.HeroSized },


        // Big
        { MonsterType.Boar, MonsterSize.Big },
        { MonsterType.Slimer, MonsterSize.Big },
        { MonsterType.Ogre, MonsterSize.Big },
        { MonsterType.Chomp, MonsterSize.Big },
        { MonsterType.Ghost, MonsterSize.Big },
        { MonsterType.Caw, MonsterSize.Big },
        { MonsterType.Banshee, MonsterSize.Big },
        { MonsterType.Slate, MonsterSize.Big },
        { MonsterType.Wizz, MonsterSize.Big },
        { MonsterType.Basilisk, MonsterSize.Big },
        { MonsterType.Demon, MonsterSize.Big },
        { MonsterType.Spiker, MonsterSize.Big },
        { MonsterType.Cyclops, MonsterSize.Big },
        { MonsterType.Hydra, MonsterSize.Big },
        { MonsterType.Alpha, MonsterSize.Big },
        { MonsterType.Troll, MonsterSize.Big },
        { MonsterType.Bramble, MonsterSize.Big },
        { MonsterType.Agnes, MonsterSize.Big },
        { MonsterType.Gytha, MonsterSize.Big },
        { MonsterType.Magrat, MonsterSize.Big },
        { MonsterType.Jinx, MonsterSize.Big },
        { MonsterType.Rm_b, MonsterSize.Big },

        // Huge
        { MonsterType.SlimeQueen, MonsterSize.Huge },
        { MonsterType.Bell, MonsterSize.Huge },
        { MonsterType.Sarcophagus, MonsterSize.Huge },
        { MonsterType.Lich, MonsterSize.Huge },
        { MonsterType.Rotten, MonsterSize.Huge },
        { MonsterType.Baron, MonsterSize.Huge },
        { MonsterType.Madness, MonsterSize.Huge },
        { MonsterType.TrollKing, MonsterSize.Huge },
        { MonsterType.Tarantus, MonsterSize.Huge },
        { MonsterType.Basalt, MonsterSize.Huge },
        { MonsterType.Dragon, MonsterSize.Huge },
        { MonsterType.Hexia, MonsterSize.Huge },
        { MonsterType.TheHand, MonsterSize.Huge },
        { MonsterType.Inevitable, MonsterSize.Huge },
        { MonsterType.Rm_h, MonsterSize.Huge }

    };

    // Tiny faces mapping
    public static readonly Dictionary<int, string> TinyFaceMap = new Dictionary<int, string>()
    {
        { 0, "Blank" },
        { 1, "Blank (Bug)" },
        { 2, "Damage Petrify" },
        { 3, "Self-Heal Vitality" },
        { 4, "Damage Weaken" },
        { 5, "Damage Death" },
        { 6, "Damage (arrow)" },
        { 7, "Damage Eliminate" },
        { 8, "Damage (bone)" },
        { 9, "Damage (bite)" },
        { 10, "Damage Poison" },
        { 11, "Damage InflictPain" },
        { 12, "Summon Bones" },
        { 13, "Summon Slimelet" },
        { 14, "Summon Hexia Death" },
        { 15, "Damage (slime)" },
        { 16, "Summon Caw" },
        { 17, "Summon Thorn" }
    };

    // Big faces mapping
    public static readonly Dictionary<int, string> BigFaceMap = new Dictionary<int, string>()
    {
        { 0, "Blank (Exert)" },
        { 1, "Blank (Bug)" },
        { 2, "Damage Eliminate" },
        { 3, "Damage Cleave (club)" },
        { 4, "Damage Cleave Poison" },
        { 5, "Damage InflictPain" },
        { 6, "Damage Poison" },
        { 7, "Damage Poison (apple)" },
        { 8, "Damage Heavy (broom)" },
        { 9, "Heal to All Allies" },
        { 10, "Damage to All Enemies (bats)" },
        { 11, "Damage Weaken Cleave" },
        { 12, "Damage Weaken" },
        { 13, "Damage to All Enemies (rocks)" },
        { 14, "Damage (rock)" },
        { 15, "Damage to All Enemies (spikes)" },
        { 16, "Damage to All Enemies (stomp)" },
        { 17, "Damage (punch)" },
        { 18, "Damage Cleave (talons)" },
        { 19, "Damage (beak)" },
        { 20, "Summon Bones" },
        { 21, "Summon Imps" },
        { 22, "Damage Descend (curse)" },
        { 23, "Damage to the Top and Bottom Enemies (slime)" },
        { 24, "Damage Cleave (slime)" },
        { 25, "Summon Wolf" },
        { 26, "Damage Cleave (claws)" },
        { 27, "Damage (bite)" },
        { 28, "Damage Cleave (sword)" },
        { 29, "Damage (snout)" },
        { 30, "Damage to the Top and Bottom Enemies (tusks)" },
        { 31, "Blank" }
    };

    // Huge faces mapping
    public static readonly Dictionary<int, string> HugeFaceMap = new Dictionary<int, string>()
    {
        { 0, "Blank" },
        { 1, "Blank (Bug)" },
        { 2, "Blank (Exert)" },
        { 3, "Damage Cleave (axe)" },
        { 4, "Damage to All Enemies (stomp)" },
        { 5, "Damage Heavy" },
        { 6, "Damage (bite)" },
        { 7, "Damage Weaken Poison" },
        { 8, "Damage Descend InflictPain" },
        { 9, "Damage to All Enemies Weaken" },
        { 10, "Damage to All Enemies (fire)" },
        { 11, "Summon Imp" },
        { 12, "Summon Demon" },
        { 13, "Summon Bones" },
        { 14, "Summon Spider" },
        { 15, "Summon Saber" },
        { 16, "Summon Slate" },
        { 17, "Damage Poison Cleave" },
        { 18, "Damage Petrify" },
        { 19, "Damage to the Top and Bottom Enemies" },
        { 20, "Damage to the Top and Bottom Enemies Weaken" },
        { 21, "Damage InflictDeath" },
        { 22, "Damage Cleave (slime)" },
        { 23, "Damage InflictExert Cleave" },
        { 24, "Damage to All Enemies Exert" },
        { 25, "Kill the Topmost Enemy" },
        { 26, "Damage Bloodlust Eliminate" },
        { 27, "Damage Heavy SelfHeal" }
    };

    public enum TinyEffectType
    {
        Blank = 0,
        BlankBug = 1,
        DamagePetrify = 2,
        SelfHealVitality = 3,
        DamageWeaken = 4,
        DamageDeath = 5,
        DamageArrow = 6,
        DamageEliminate = 7,
        DamageBone = 8,
        DamageBite = 9,
        DamagePoison = 10,
        DamageInflictPain = 11,
        SummonBones = 12,
        SummonSlimelet = 13,
        SummonHexiaDeath = 14,
        DamageSlime = 15,
        SummonCaw = 16,
        SummonThorn = 17
    }

    public enum BigEffectType
    {
        BlankExert = 0,
        BlankBug = 1,
        DamageEliminate = 2,
        DamageCleaveClub = 3,
        DamageCleavePoison = 4,
        DamageInflictPain = 5,
        DamagePoison = 6,
        DamagePoisonApple = 7,
        DamageHeavyBroom = 8,
        HealToAllAllies = 9,
        DamageToAllBats = 10,
        DamageWeakenCleave = 11,
        DamageWeaken = 12,
        DamageToAllRocks = 13,
        DamageRock = 14,
        DamageToAllSpikes = 15,
        DamageToAllStomp = 16,
        DamagePunch = 17,
        DamageCleaveTalons = 18,
        DamageBeak = 19,
        SummonBones = 20,
        SummonImps = 21,
        DamageDescendCurse = 22,
        DamageTopBottomSlime = 23,
        DamageCleaveSlime = 24,
        SummonWolf = 25,
        DamageCleaveClaws = 26,
        DamageBite = 27,
        DamageCleaveSword = 28,
        DamageSnout = 29,
        DamageTopBottomTusks = 30,
        Blank = 31
    }

    public enum HugeEffectType
    {
        Blank = 0,
        BlankBug = 1,
        BlankExert = 2,
        DamageCleaveAxe = 3,
        DamageToAllStomp = 4,
        DamageHeavy = 5,
        DamageBite = 6,
        DamageWeakenPoison = 7,
        DamageDescendInflictPain = 8,
        DamageToAllWeaken = 9,
        DamageToAllFire = 10,
        SummonImp = 11,
        SummonDemon = 12,
        SummonBones = 13,
        SummonSpider = 14,
        SummonSaber = 15,
        SummonSlate = 16,
        DamagePoisonCleave = 17,
        DamagePetrify = 18,
        DamageTopBottom = 19,
        DamageTopBottomWeaken = 20,
        DamageInflictDeath = 21,
        DamageCleaveSlime = 22,
        DamageInflictExertCleave = 23,
        DamageToAllExert = 24,
        KillTopmostEnemy = 25,
        DamageBloodlustEliminate = 26,
        DamageHeavySelfHeal = 27
    }

    /// <summary>
    /// Safely gets the size of a specific monster type.
    /// </summary>
    public static MonsterSize GetMonsterSize(MonsterType type)
    {
        if (SizeMapping.TryGetValue(type, out MonsterSize size))
        {
            return size;
        }
        return MonsterSize.HeroSized; // Default fallback
    }

    /// <summary>
    /// Retrieves the face name based on size and ID. 
    /// Hero-sized returns a generic string/ID as it utilizes the default hero setup.
    /// </summary>
    public static string GetFaceName(MonsterSize size, int id)
    {
        switch (size)
        {
            case MonsterSize.Tiny:
                return TinyFaceMap.TryGetValue(id, out var tinyFace) ? tinyFace : "Unknown Tiny Face";
            case MonsterSize.Big:
                return BigFaceMap.TryGetValue(id, out var bigFace) ? bigFace : "Unknown Big Face";
            case MonsterSize.Huge:
                return HugeFaceMap.TryGetValue(id, out var hugeFace) ? hugeFace : "Unknown Huge Face";
            case MonsterSize.HeroSized:
            default:
                return $"HeroDefault_{id}"; // Handled by default Hero structures
        }
    }

    // Explicit database mapping definitions to ensure accurate engine IDs
    private static readonly Dictionary<string, Dictionary<string, int>> PrefixCustomIds = new Dictionary<string, Dictionary<string, int>>
    {
        {
            "bas", new Dictionary<string, int>
            {
                // Your 187 Hero-Sized faces go here
                { "reg/face/blank/basic", 0 },
                { "reg/face/blank/unset", 1 },
                { "reg/face/blank/petrified", 2 },
                { "reg/face/blank/wand", 3 },
                { "reg/face/blank/item", 4 },
                { "reg/face/blank/curse", 5 },
                { "reg/face/blank/stasis", 6 },
                { "reg/face/blank/sticky", 7 },
                { "reg/face/blank/exerted", 8 },
                { "reg/face/blank/fumble", 9 },
                { "reg/face/special/addKeyword/cleanseselfcleanse", 10 },
                { "reg/face/item/backstab", 11 },
                { "reg/face/dmgSelfCantrip", 12 },
                { "reg/face/pinkSkull", 13 },
                { "reg/face/dmgSelfMandatory", 14 },
                { "reg/face/sword", 15 },
                { "reg/face/dmgGrowth", 16 },
                { "reg/face/dmgEngage", 17 },
                { "reg/face/swordMagic", 18 },
                { "reg/face/dmgPain", 19 },
                { "reg/face/swordDeathwish", 20 },
                { "reg/face/dmgDeath", 21 },
                { "reg/face/dmgSerrated", 22 },
                { "reg/face/swordExert", 23 },
                { "reg/face/dmgDouble", 24 },
                { "reg/face/swordQuad", 25 },
                { "reg/face/dmgBloodlust", 26 },
                { "reg/face/dmgCopycat", 27 },
                { "reg/face/swordPristine", 28 },
                { "reg/face/dmgGuilt", 29 },
                { "reg/face/kriss", 30 },
                { "reg/face/shadowDagger", 31 },
                { "reg/face/swordFocus", 32 },
                { "reg/face/swordInspire", 33 },
                { "reg/face/swordAll", 34 },
                { "reg/face/resurrectMana", 35 },
                { "reg/face/swordCleave", 36 },
                { "reg/face/swordDescend", 37 },
                { "reg/face/dmgCleaveChain", 38 },
                { "reg/face/hammer", 39 },
                { "reg/face/quartzSlow", 40 },
                { "reg/face/shieldBash", 41 },
                { "reg/face/dmgCharged", 42 },
                { "reg/face/stun", 43 },
                { "reg/face/swordVulnerable", 44 },
                { "reg/face/dmgEra", 45 },
                { "reg/face/arrow", 46 },
                { "reg/face/arrowPoison", 47 },
                { "reg/face/arrowDuplicate", 48 },
                { "reg/face/arrowCleave", 49 },
                { "reg/face/arrowCopycat", 50 },
                { "reg/face/swordShield", 51 },
                { "reg/face/swordHeal", 52 },
                { "reg/face/dmgPoison", 53 },
                { "reg/face/plague", 54 },
                { "reg/face/dmgPoisonDose", 55 },
                { "reg/face/shield", 56 },
                { "reg/face/shieldFlesh", 57 },
                { "reg/face/shieldGrowth", 58 },
                { "reg/face/shieldPrecise", 59 },
                { "reg/face/shieldEnduringDeath", 60 },
                { "reg/face/shieldMagic", 61 },
                { "reg/face/shieldDoubleUse", 62 },
                { "reg/face/shieldSteel", 63 },
                { "reg/face/shieldRescue", 64 },
                { "reg/face/shieldPristine", 65 },
                { "reg/face/shieldCantrip", 66 },
                { "reg/face/shieldCopycat", 67 },
                { "reg/face/shieldFocus", 68 },
                { "reg/face/shieldPlusAdjacent", 69 },
                { "reg/face/shieldCharged", 70 },
                { "reg/face/shieldCure", 71 },
                { "reg/face/wardingChord", 72 },
                { "reg/face/flute", 73 },
                { "reg/face/shieldHeart", 74 },
                { "reg/face/smith", 75 },
                { "reg/face/mana", 76 },
                { "reg/face/manaCantrip", 77 },
                { "reg/face/manaCantripBoned", 78 },
                { "reg/face/manaGrowth", 79 },
                { "reg/face/manaDecay", 80 },
                { "reg/face/manaDeath", 81 },
                { "reg/face/manaPain", 82 },
                { "reg/face/manaBloodlust", 83 },
                { "reg/face/manaPair", 84 },
                { "reg/face/manaTriple", 85 },
                { "reg/face/healShieldMana", 86 },
                { "reg/face/manaDouble", 87 },
                { "reg/face/wandCharged", 88 },
                { "reg/face/wandJinx", 89 },
                { "reg/face/wandFire", 90 },
                { "reg/face/wandPoison", 91 },
                { "reg/face/wandBlood", 92 },
                { "reg/face/wandMana", 93 },
                { "reg/face/wandFightBonus", 94 },
                { "reg/face/wandWeaken", 95 },
                { "reg/face/wandFierce", 96 },
                { "reg/face/wandEcho", 97 },
                { "reg/face/wandDispel", 98 },
                { "reg/face/wandResilient", 99 },
                { "reg/face/wandStun", 100 },
                { "reg/face/wandChaos", 101 },
                { "reg/face/item/sceptre", 102 },
                { "reg/face/heal", 103 },
                { "reg/face/wandHeal", 104 },
                { "reg/face/boon", 105 },
                { "reg/face/healRescue", 106 },
                { "reg/face/healAll", 107 },
                { "reg/face/healBuff", 108 },
                { "reg/face/healCleave", 109 },
                { "reg/face/healRegen", 110 },
                { "reg/face/healUncurse", 111 },
                { "reg/face/healMagic", 112 },
                { "reg/face/healGroooooowth", 113 },
                { "reg/face/healDouble", 114 },
                { "reg/face/stick", 115 },
                { "reg/face/kill", 116 },
                { "reg/face/undying", 117 },
                { "reg/face/taunt", 118 },
                { "reg/face/revenge", 119 },
                { "reg/face/shieldCrescent", 120 },
                { "reg/face/shieldPain", 121 },
                { "reg/face/headshot", 122 },
                { "reg/face/dodge", 123 },
                { "reg/face/dodgeCantrip", 124 },
                { "reg/face/rerollCantrip", 125 },
                { "reg/face/dmgCantrip", 126 },
                { "reg/face/swordRoulette", 127 },
                { "reg/face/flurry", 128 },
                { "reg/face/needle", 129 },
                { "reg/face/recharge", 130 },
                { "reg/face/dmgWeaken", 131 },
                { "reg/face/swordDuplicate", 132 },
                { "reg/face/shieldDuplicate", 133 },
                { "reg/face/manaDuplicate", 134 },
                { "reg/face/rangedEngage", 135 },
                { "reg/face/resurrect", 136 },
                { "reg/face/dmgRampage", 137 },
                { "reg/face/special/addKeyword/doubleuse", 138 },
                { "reg/face/special/addKeyword/cantrip", 139 },
                { "reg/face/special/addKeyword/nothing", 140 },
                { "reg/face/special/addKeyword/copycat", 141 },
                { "reg/face/special/addKeyword/cleavesingleuse", 142 },
                { "reg/face/special/addKeyword/crueldeathwish", 143 },
                { "reg/face/special/addKeyword/managain", 144 },
                { "reg/face/special/addKeyword/poison", 145 },
                { "reg/face/special/addKeyword/selfshield", 146 },
                { "reg/face/special/addKeyword/selfheal", 147 },
                { "reg/face/special/addKeyword/selfhealselfshield", 148 },
                { "reg/face/special/addKeyword/managainpain", 149 },
                { "reg/face/special/addKeyword/engage", 150 },
                { "reg/face/special/addKeyword/growth", 151 },
                { "reg/face/special/generic/shield", 152 },
                { "reg/face/special/generic/sword", 153 },
                { "reg/face/special/generic/mana", 154 },
                { "reg/face/special/generic/summon", 155 },
                { "reg/face/special/generic/heal", 156 },
                { "reg/face/item/redFlag", 157 },
                { "reg/face/scythe", 158 },
                { "reg/face/item/swordFleshPain", 159 },
                { "reg/face/item/manaBomb", 160 },
                { "reg/face/item/wand-of-wand", 161 },
                { "reg/face/item/demon-horn", 162 },
                { "reg/face/item/charged-hammer", 163 },
                { "reg/face/item/infused-herbs", 164 },
                { "reg/face/item/potion-shard", 165 },
                { "reg/face/item/revive-potion", 166 },
                { "reg/face/item/mana-potion", 167 },
                { "reg/face/dmgEliminate", 168 },
                { "reg/face/poisonFang", 169 },
                { "reg/face/wolfBite", 170 },
                { "reg/face/slash", 171 },
                { "reg/face/hatch", 172 },
                { "reg/face/dmgTrio", 173 },
                { "reg/face/dmgLucky", 174 },
                { "reg/face/dmgCritical", 175 },
                { "reg/face/special/target/justTargetAlly", 176 },
                { "reg/face/special/target/justTargetAllyPips", 177 },
                { "reg/face/special/target/justTargetAllyPipsAll", 178 },
                { "reg/face/special/target/justTargetAllyAll", 179 },
                { "reg/face/special/target/justTargetEnemy", 180 },
                { "reg/face/special/target/justTargetEnemyPips", 181 },
                { "reg/face/special/target/justTargetEnemyPipsAll", 182 },
                { "reg/face/special/target/justTargetEnemyAll", 183 },
                { "reg/face/special/target/justTargetAnyPips", 184 },
                { "reg/face/special/target/justTargetAny", 185 },
                { "reg/face/special/target/justTargetSelf", 186 },
                { "reg/face/special/target/justTargetSelfPips", 187 }
            }
        },
        {
            "tin", new Dictionary<string, int>
            {
                { "small/face/blank", 0 },
                { "small/face/blankBug", 1 },
                { "small/face/petrify", 2 },
                { "small/face/selfHealVitality", 3 },
                { "small/face/weaken", 4 },
                { "small/face/curse", 5 },               // Wisp/Shade Curse (Dmg Death)
                { "small/face/arrow", 6 },               // Sniper Arrow
                { "small/face/arrowEliminate", 7 },      // Sniper Arrow (Eliminate)
                { "small/face/boneStrike", 8 },          // Bones Bone Strike
                { "small/face/nip", 9 },                 // Rat Bite/Nip
                { "small/face/nipPoison", 10 },          // Spider Poison Bite
                { "small/face/sting", 11 },              // Bee Sting (Pain)
                { "small/face/summonBones", 12 },
                { "small/face/summonSlimelet", 13 },
                { "small/face/summonHexia", 14 },
                { "small/face/slime", 15 },              // Slimelet Dmg
                { "small/face/hatch", 16 },              // Caw Egg Hatch
                { "small/face/grow", 17 }                // Thorn Grow (Summon Thorn)
            }
        },
        {
            "big", new Dictionary<string, int>
            {
                { "big/face/blankExerted", 0 },
                { "big/face/blankBug", 1 },
                { "big/face/finger", 2 },                // Banshee Pointing Eliminate
                { "big/face/club", 3 },                  // Troll/Ogre Cleave Club
                { "big/face/decay", 4 },                 // Basilisk Cleave Poison
                { "big/face/jinx", 5 },                  // Demon Horn (Dmg Pain)
                { "big/face/poison", 6 },                // Slimer Dmg Poison
                { "big/face/poisonApple", 7 },           // Agnes Poison Apple
                { "big/face/broomstick", 8 },            // Magrat Broomstick
                { "big/face/brew", 9 },                  // Agnes Brew (Heal All Allies)
                { "big/face/bats", 10 },                 // Vampire Bats (Dmg All)
                { "big/face/gaze", 11 },                 // Basilisk Gaze (Weaken Cleave)
                { "big/face/iceBolt", 12 },              // Wizz Ice Bolt (Dmg Weaken)
                { "big/face/rockSpray", 13 },            // Slate Rock Spray (Dmg All)
                { "big/face/rockFist", 14 },             // Slate Rock Fist
                { "big/face/spikeSpray", 15 },           // Spiker Spikes (Dmg All)
                { "big/face/stomp", 16 },                // Ogre Stomp (Dmg All)
                { "big/face/punch", 17 },                // Ogre Punch
                { "big/face/claw", 18 },                 // Caw Claw (Cleave Talons)
                { "big/face/peck", 19 },                 // Caw Beak (Peck)
                { "big/face/summonSkeleton", 20 },
                { "big/face/summonImp", 21 },
                { "big/face/chain", 22 },                // Ghost Chain (Descend Curse)
                { "big/face/upDownBlob", 23 },           // Slimer Top/Bottom Slime
                { "big/face/threeBlobs", 24 },           // Slimer Cleave Slime
                { "big/face/summonWolf", 25 },
                { "big/face/maul", 26 },                 // Alpha Claws Cleave
                { "big/face/wolfBite", 27 },             // Alpha Bite
                { "big/face/sword", 28 },                // Warchief Sword Cleave
                { "big/face/gore", 29 },                 // Boar Snout Gore
                { "big/face/boar_gore", 30 },            // Boar Tusks Top/Bottom
                { "big/face/blank", 31 }
            }
        },
        {
            "hug", new Dictionary<string, int>
            {
                { "huge/face/blank", 0 },
                { "huge/face/blankBug", 1 },
                { "huge/face/blankExerted", 2 },
                { "huge/face/club", 3 },                 // Troll King/Baron Heavy Cleave Axe
                { "huge/face/stomp", 4 },                // Troll King Stomp
                { "huge/face/staff", 5 },                // Rotten Heavy Staff
                { "huge/face/chomp", 6 },                // Tarantus Bite
                { "huge/face/infect", 7 },               // Rotten Weaken Poison
                { "huge/face/deathBeam", 8 },            // Lich Death Beam
                { "huge/face/ear", 9 },                  // Bell Ear Weaken All
                { "huge/face/flame", 10 },               // Dragon Fire All
                { "huge/face/summonImp", 11 },
                { "huge/face/summonDemon", 12 },
                { "huge/face/summonBones", 13 },
                { "huge/face/summonSpider", 14 },
                { "huge/face/summonSaber", 15 },
                { "huge/face/summonSlate", 16 },
                { "huge/face/poisonBreath", 17 },        // Dragon Poison Cleave
                { "huge/face/chill", 18 },               // Basalt Petrify
                { "huge/face/upDownBlob", 19 },          // Slime Queen Top/Bottom
                { "huge/face/weakenFlanking", 20 },      // Madness Top/Bottom Weaken
                { "huge/face/inevitableSkull", 21 },     // Inevitable Death
                { "huge/face/threeBlobs", 22 },          // Slime Queen Cleave
                { "huge/face/groupInflictExert", 23 },   // The Hand Exert Cleave
                { "huge/face/handSkull", 24 },           // The Hand Exert All
                { "huge/face/devour", 25 },              // Devour/Kill Topmost
                { "huge/face/hexia", 26 },               // Hexia Bloodlust Eliminate
                { "huge/face/chompSelfHeal", 27 }        // Tarantus Chomp Heal
            }
        }
    };
}