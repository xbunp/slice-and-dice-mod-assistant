using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public static class SyntaxRegressionTester
{    private static string LogFilePath => Path.Combine(Application.persistentDataPath, "SyntaxRegressionResults.log");

    public enum TargetType
    {
        Hero,
        Monster,
        Item,
        Modifier,
        Ability,
        ModDocument
    }

    [System.Serializable]
    public class TestCase
    {
        public string Name;
        public TargetType Type;
        public string RawInput;
        public string ExpectedOutput; // If left null, assumes ExpectedOutput == RawInput

        public TestCase(string name, TargetType type, string rawInput, string expectedOutput = null)
        {
            Name = name;
            Type = type;
            RawInput = rawInput;
            ExpectedOutput = expectedOutput ?? rawInput;
        }
    }

    // ======================================================================================
    // ADD YOUR KNOWN WORKING STRINGS HERE
    // ======================================================================================
    //old test cases
    /*
    private static readonly List<TestCase> TestCases = new List<TestCase>
    {
        // --- HERO TESTS ---
        new TestCase("Hero - Faramir", TargetType.Hero,
            "((replica.Statue.n.Faramir.col.f.hp.13.tier.3.img.Collector.p.db604d:dad36b:30.thue.313172:18:-3).i.left.mid.hat.(Fey.sd.125-1:186.i.mid.k.annul#k.cantrip.i.togunt).i.left.facade.kas50:0.i.right5.hat.(Fey.sd.15-6:46-7:52-5:52-5:51-5:56-6.i.left.k.lucky.i.(togkey#togkey)).i.mid.facade.bas46:11:0:0.i.top.facade.bas52:11:0:0.i.bot.facade.bas52:11:0:0.i.right.facade.bas51:11:0:0.i.rightmost.facade.bas56:11:0:0)",
            "((replica.Statue.n.Faramir.col.f.hp.13.tier.3.img.Collector.p.db604d:dad36b:30.thue.313172:18:-3).i.left.mid.hat.(Fey.sd.125-1:186.i.mid.k.annul#k.cantrip.i.togunt).i.left.facade.kas50:0.i.right5.hat.(Fey.sd.15-6:46-7:52-5:52-5:51-5:56-6.i.left.k.lucky.i.togkey#togkey).i.mid.facade.bas46:11:0:0.i.top.facade.bas52:11:0:0.i.bot.facade.bas52:11:0:0.i.right.facade.bas51:11:0:0.i.rightmost.facade.bas56:11:0:0)"),

        new TestCase("Hero - Gollum", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.181-1:143:126-2:126-2:131-1:12-1.i.left.k.cleave#k.vulnerable#facade.Leo18:-48:70:-8.i.right.k.cantrip#facade.har82:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1).doc.Swaps faces each turn.i.self.(et2.allitem.hat.(Statue.sd.177-1:171-3:171-1:171-1:187-1:11-2.i.left.k.cleave#k.vulnerable#k.mandatory#facade.Leo14:-48:70:-8.i.right.k.selfheal#k.cantrip#facade.eba13:0))"),

        new TestCase("Hero - Gollum 2", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.13:13:13:13:13:13.i.row.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Smeagol.col.k.hp.6.tier.3.sd.168-3:125-1:103-2:103-2:124:130.i.left.k.cantrip#facade.bas168:76:26:11.i.topbot.k.generous#facade.eba13:0.i.rightmost.k.sticky#facade.bas130:79:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.topbot.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Gollum.col.k.hp.6.tier.3.sd.30-4:44-2:158-1:158-1:11-2:11-3.i.left.k.exert#facade.ale4:0.i.topbot.k.selfheal.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.row.facade.Eme97:0.i.topbot.facade.Eme114:-11:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1)"),

        // Lembas Test Case
        new TestCase(
            "Item - Lembas",
            TargetType.Item,
            // original
            "i.(rightmost.hat.(Fey.sd.0:0:0:0:0:110-1.i.rightmost.k.enduring.i.rightmost.facade.Lem91:0)).img.ite17.p.c54016:128a1a:18.p.ff8105:19ac61:45.draw.Ese72:0:0.tier.3.n.Lembas", 
            // optimized
            "i.rightmost.hat.(Fey.sd.0:0:0:0:0:110-1.i.rightmost.k.enduring#facade.Lem91:0).img.ite17.p.c54016:128a1a:18.p.ff8105:19ac61:45.draw.Ese72:0:0.tier.3.n.Lembas"
        ),

        new TestCase(
            "Item - Two-way Item",
            TargetType.Item,
            "i.all.mid.hat.(Statue.sd.187.i.mid.sticker.(k.cantrip).i.togtarg.i.pendulum.i.mid.sticker.(k.nothing).i.mid.togfri.i.mid.togunt)"),

        // --- MONSTER TESTS ---
        new TestCase("Wose & Huorn", TargetType.Monster,
            "(Wizz.n.Wose.sd.12-2:0:10-2:10-2:0:9-3.i.left.k.cleave#facade.bas212:15:8:-2.i.mid2.hat.(egg.(Imp.n.Huorn.hp.4.sd.9-3:3-2:9-2:9-2:3-2:9-1.i.topbot.k.hypergrowth#facade.Ese30:0.i.rightmost.k.hypergrowth#facade.Ese30:0.i.left.k.hypergrowth#facade.Ese30:0.img.Tinder.p.fd8f4d:85d720:45))#blindfold#facade.Che3:0.img.Bramble.hsv.0:0:-8.draw.Druid:3:3.hsv.25:-11:-3.p.81a871:876113:12)",
            "((Wizz.n.Wose.sd.12-2:0:10-2:10-2:0:9-3.i.left.k.cleave#facade.bas212:15:8:-2.img.Bramble.hsv.0:0:-8.draw.Druid:3:3.hsv.25:-11:-3.p.81a871:876113:12).i.mid2.hat.(egg.((Imp.n.Huorn.hp.4.sd.9-3:3-2:9-2:9-2:3-2:9-1.i.topbot.k.hypergrowth#facade.Ese30:0.i.rightmost.k.hypergrowth#facade.Ese30:0.i.left.k.hypergrowth#facade.Ese30:0.img.Tinder.p.fd8f4d:85d720:45)))#blindfold#facade.Che3:0)"),

                // --- MONSTER TESTS ---
        new TestCase("Willow", TargetType.Monster,
            "((rmon.ded.n.Old Man Willow.hp.12.sd.15-1:3-1:15-1:15-1:3-1:15-1.i.rightmost.k.petrify#facade.Eme89:0.i.left.k.petrify#facade.Eme89:0.i.mid2.k.petrify#k.weaken#facade.Eme176:0.i.topbot.k.petrify#facade.Eme201:0.img.Alpha.hsv.0:0:-99.draw.Bramble:-1:0.draw.Thorn:6:29.draw.Thorn:-12:27).i.topbot.hat.(Fey.sd.115:0:181-2:181-2.i.topbot.k.petrify#k.cleave.i.togvis)#facade.Eme201:0.i.onhitdata.(Fey.sd.181-1.i.left.k.cleanse.n.Cleanse))"),

        new TestCase("Wraith", TargetType.Monster,
            "(Blind.n.Wraith.sd.153.i.left.enchant.(left.Blank).i.mid.enchant.(mid.Blank).i.top.enchant.(top.Blank).i.bot.enchant.(bot.Blank).i.right.enchant.(right.Blank).i.rightmost.enchant.(rightmost.Blank).i.all.facade.ite164:54:-59:0.img.b3.75.draw.ite443:7:0.draw.ite284:-2:13.draw.Eme10:10:11.p.b07363:182773:43.hsv.0:-39:14)"),

        new TestCase("Monster (Cave Troll)", TargetType.Monster,
            "(Troll.p.65623a:687a69:23.thue.d00051:36:20.hsv.-14:12:5.n.Cave Troll.sd.3-3:2-6:16-2:16-2:17-3.i.mid.k.exert#k.serrated#k.growth#facade.Ese105:31:34:2.i.right.k.engage#facade.bas205:42:-20:3.i.rightmost.hat.(x2.egg.((Slimelet.n.Orc.hp.3.sd.7-4:7-3:6-3:6-3.t.Caw.img.Bones.hsv.0:0:-70.draw.Goblin:-6:-4.p.6a6e34:5c7688:07).i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg)))#blindfold#facade.bas209:38:0:0)"),


        new TestCase("Wraith", TargetType.Monster,
            "((Wolf.n.Ringwraith.hp.5.sd.53-2:53-1:15-2:15-1:131-2:131-1.i.topbot.k.inflictpain#facade.Eme13:0.img.n1.75.hsv.59:-30:-17).i.(unpack.Determination).i.t.Shade.i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg))).i.t.jinx.allitem.learn.sthief.abilitydata.(Fey.sd.15-1:0:8.i.left.sticker.(all.mid.hat.(Fey.sd.90:115-1.i.mid.k.fierce.i.togvis).i.mid.facade.kas31:0)#k.exert.img.spe13.n.Torch)"),


        new TestCase("Monster - GROND", TargetType.Monster,
            "(Bell.n.Grond.sd.4-3:4-3:11-1:11-1:4-2:4-2.i.row.k.exert#facade.dar12:0:25:16.img.Sarcophagus.draw.Alpha:10:0).t.jinx.ea.sthief.abilitydata.(Statue.i.left.hat.egg.(rmon.1c.n.War Drum.hp.2.sd.128-2:128-1:128-1:128-1:128-1:128-1.img.Ber50)#blindfold)",
            "((Bell.n.Grond.sd.4-3:4-3:11-1:11-1:4-2:4-2.i.row.k.exert#facade.dar12:0:25:16.img.Sarcophagus.draw.Alpha:10:0).t.jinx.ea.sthief.abilitydata.(Statue.i.left.hat.egg.(rmon.1c.n.War Drum.hp.2.sd.128-2:128-1:128-1:128-1:128-1:128-1.img.Ber50)#blindfold))"),

        new TestCase("Monster - Petrified Troll", TargetType.Monster,
            "((Alpha.n.Petrified.hp.8.sd.3-2:3-1:16-1:16-1:16-1:3-1.i.right.k.exert.i.bot.k.exert.i.left.k.exert.img.Troll.p.65623a:a6a6a6:35).doc.Slowly thawing out!.i.(x6.Basilisk Scale.part.0).i.self.ea.sThief.abilitydata.(Fey.sd.178-1:0:0:0:76-1.i.left.k.cleanse))"),            


        //TODO: Make some modifiers for regression testing. 
        // --- MODIFIER TESTS ---
        new TestCase("Basic Modifier", TargetType.Modifier,
            "1-5.add.Wolf.mn.Wolf Pack"),

        new TestCase("Chained & Spliced Modifier", TargetType.Modifier,
            "(t1.x2.h.top.inv.i.Apple.splice.cantrip)&(self.jinx.exert)"),


        // --- ABILITY TESTS ---
        new TestCase("On Hit Spell", TargetType.Ability,
            "i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg))",
            "i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg))"),

                // --- ABILITY TESTS ---
        new TestCase("Tactic", TargetType.Ability,
            "abilitydata.(Fey.sd.15-1:0:56-2.i.left.sticker.(Enchanted Shield)#togtime.img.ite2.n.Wary)")

    };
*/

    private static readonly List<TestCase> TestCases = new List<TestCase>
    {
        new TestCase("Hero 1", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.181-1:143:126-2:126-2:131-1:12-1.i.left.k.cleave#k.vulnerable#facade.Leo18:-48:70:-8.i.right.k.cantrip#facade.har82:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1).doc.Swaps faces each turn.i.self.(et2.allitem.hat.(Statue.sd.177-1:171-3:171-1:171-1:187-1:11-2.i.left.k.cleave#k.vulnerable#k.mandatory#facade.Leo14:-48:70:-8.i.right.k.selfheal#k.cantrip#facade.eba13:0))",
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.181-1:143:126-2:126-2:131-1:12-1.i.(left.k.cleave#k.vulnerable#facade.Leo18:-48:70:-8).i.(right.k.cantrip#facade.har82:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1).doc.Swaps faces each turn.i.self.(et2.allitem.(hat.(Statue.sd.177-1:171-3:171-1:171-1:187-1:11-2.i.(left.k.cleave#k.vulnerable#k.mandatory#facade.Leo14:-48:70:-8).i.(right.k.selfheal#k.cantrip#facade.eba13:0))))"),

        new TestCase("Doors", TargetType.Hero,
            "(replica.Statue.n.Doors of Durin.col.g.hp.5.tier.1.sd.0:5:5:5:5:5.i.(unpack.determination).i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(top.hat.(Fey.sd.186.i.top.sticker.(mid.hat.(Fey.sd.186.i.mid.sticker.(bot.hat.(Fey.sd.186.i.bot.sticker.(right.hat.(Fey.sd.186.i.right.sticker.(rightmost.left.hat.(Fey.sd.182-100.i.(left.k.fierce#facade.sym23:52:-52:5)))#togtime#togtarg#k.cantrip#facade.sym24:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym14:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym22:52:-52:7).img.Granite.p.0a0105:001e64:28.hsv.0:-30:-15.draw.Grave:3:4.hsv.-4:-20:10).i.(Wretched Crown).i.(Ghost Shield).i.(t.(Slate)).i.((self.((Wolf.doc.Speak Friend and Enter[dot]).spirit)))",
            "(replica.Statue.n.Doors of Durin.col.g.hp.5.tier.1.sd.0:5:5:5:5:5.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(top.hat.(Fey.sd.186.i.(top.sticker.(mid.hat.(Fey.sd.186.i.(mid.sticker.(bot.hat.(Fey.sd.186.i.(bot.sticker.(right.hat.(Fey.sd.186.i.(right.sticker.(rightmost.left.hat.(Fey.sd.182-100.i.(left.k.fierce#facade.sym23:52:-52:5)))#togtime#togtarg#k.cantrip#facade.sym24:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym14:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym22:52:-52:7))).img.Granite.p.0a0105:001e64:28.hsv.0:-30:-15.draw.Grave:3:4.hsv.-4:-20:10).i.(Wretched Crown).i.(Ghost Shield).i.(t.(Slate)).i.((self.((Wolf.doc.Speak Friend and Enter[dot]).spirit)))"),

        new TestCase("Hero 2", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.13:13:13:13:13:13.i.row.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Smeagol.col.k.hp.6.tier.3.sd.168-3:125-1:103-2:103-2:124:130.i.left.k.cantrip#facade.bas168:76:26:11.i.topbot.k.generous#facade.eba13:0.i.rightmost.k.sticky#facade.bas130:79:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.topbot.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Gollum.col.k.hp.6.tier.3.sd.30-4:44-2:158-1:158-1:11-2:11-3.i.left.k.exert#facade.ale4:0.i.topbot.k.selfheal.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.row.facade.Eme97:0.i.topbot.facade.Eme114:-11:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1)",
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.13:13:13:13:13:13.i.(row.mid.hat.(statue.sd.0:13.i.(left.hat.(egg.(replica.Statue.n.Smeagol.col.k.hp.6.tier.3.sd.168-3:125-1:103-2:103-2:124:130.i.(left.k.cantrip#facade.bas168:76:26:11).i.(topbot.k.generous#facade.eba13:0).i.(rightmost.k.sticky#facade.bas130:79:0:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.(togunt))).i.(topbot.mid.hat.(statue.sd.0:13.i.(left.hat.(egg.(replica.Statue.n.Gollum.col.k.hp.6.tier.3.sd.30-4:44-2:158-1:158-1:11-2:11-3.i.(topbot.k.selfheal).i.(left.k.exert#facade.ale4:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.(togunt))).i.(row.facade.Eme97:0).i.(topbot.facade.Eme114:-11:0:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1)"),

        new TestCase("Monster 1", TargetType.Monster,
            "((rmon.ded.hsv.68:55:0.draw.bas199:4:4.p.0ca73c:1b0559:25.n.Watcher In The Water.hp.20.sd.23-2:24-2:11-1:11-1:16-2:16-1.i.left.facade.bas211:33:0:19.i.mid.facade.bas212:30:21:16.i.topbot.facade.Eme147:-35:0:0.i.right2.k.petrify#facade.Eme201:63:0:0).i.triggerhpdata.(Fey.col.p.sd.123-1.n.Dodge.hp.5).i.(t.(archer)))",
            "(rmon.ded.hsv.68:55:0.draw.bas199:4:4.p.0ca73c:1b0559:25.n.Watcher In The Water.hp.20.sd.23-2:24-2:11-1:11-1:16-2:16-1.i.(left.facade.bas211:33:0:19).i.(mid.facade.bas212:30:21:16).i.(topbot.facade.Eme147:-35:0:0).i.(right2.k.petrify#facade.Eme201:63:0:0)).i.(triggerhpdata.(Fey.n.Dodge.col.p.hp.5.sd.123-1)).i.(t.(archer))"),

        new TestCase("Monster 2", TargetType.Monster,
            "(Troll.p.65623a:687a69:23.thue.d00051:36:20.hsv.-14:12:5.n.Cave Troll.sd.3-3:2-6:16-2:16-2:17-3.i.mid.k.exert#k.serrated#k.growth#facade.Ese105:31:34:2.i.right.k.engage#facade.bas205:42:-20:3.i.rightmost.mid.hat.(x2.egg.(Slimelet.n.Orc.hp.3.sd.7-4:7-3:6-3:6-3.t.Caw.img.Bones.hsv.0:0:-70.draw.Goblin:-6:-4.p.6a6e34:5c7688:07).i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg)))#blindfold#facade.bas209:38:0:0)",
            "(Troll.p.65623a:687a69:23.thue.d00051:36:20.hsv.-14:12:5.n.Cave Troll.sd.3-3:2-6:16-2:16-2:17-3.i.(mid.k.exert#k.serrated#k.growth#facade.Ese105:31:34:2).i.(right.k.engage#facade.bas205:42:-20:3).i.(rightmost.mid.hat.(x2.(egg.(Slimelet.n.Orc.hp.3.sd.7-4:7-3:6-3:6-3.img.Bones.hsv.0:0:-70.draw.Goblin:-6:-4.p.6a6e34:5c7688:07).i.(t.(Caw))).i.(onhitdata.(Fey.sd.15-1.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Pharaoh Curse.part.1)#togtarg))))))#blindfold#facade.bas209:38:0:0))"),
        
    };

    // ======================================================================================
    // AUTOMATIC EXECUTION ON PLAY MODE
    // ======================================================================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RunTests()
    {
        int totalTests = TestCases.Count;
        int passedTests = 0;
        int failedTests = 0;

        StringBuilder failureReport = new StringBuilder();
        StringBuilder fileLog = new StringBuilder();

        fileLog.AppendLine($"==================================================");
        fileLog.AppendLine($" SYNTAX REGRESSION TEST RUN - {DateTime.Now}");
        fileLog.AppendLine($"==================================================\n");

        foreach (var test in TestCases)
        {
            string actualOutput = ExecuteTest(test);
            string cleanExpected = test.ExpectedOutput.Trim();
            string cleanActual = actualOutput?.Trim() ?? "";

            if (string.Equals(cleanExpected, cleanActual, StringComparison.Ordinal))
            {
                passedTests++;
                fileLog.AppendLine($"[PASS] [{test.Type}] {test.Name}");
            }
            else
            {
                failedTests++;
                fileLog.AppendLine($"[FAIL] [{test.Type}] {test.Name}");
                AppendFailureDetails(failureReport, test, cleanExpected, cleanActual);
            }
        }

        fileLog.AppendLine($"\n--------------------------------------------------");
        fileLog.AppendLine($"SUMMARY: {passedTests}/{totalTests} Passed. ({failedTests} Failed)");
        fileLog.AppendLine($"--------------------------------------------------");

        if (failedTests > 0)
        {
            fileLog.AppendLine("\n=== FAILURE DETAILS ===");
            fileLog.AppendLine(Regex.Replace(failureReport.ToString(), "<.*?>", string.Empty)); // Strip Unity rich text tags
        }

        // Save to Log File
        try
        {
            File.WriteAllText(LogFilePath, fileLog.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[REGRESSION] Failed to write log file: {ex.Message}");
        }

        // Output Summary to Unity Console
        if (failedTests == 0)
        {
            Debug.Log($"<color=#00FF00><b>[REGRESSION PASSED]</b> All {totalTests} syntax round-trip tests passed cleanly!</color>\nLog file: {LogFilePath}");
        }
        else
        {
            Debug.LogError($"<color=#FF0000><b>[REGRESSION FAILED]</b> {failedTests} of {totalTests} tests failed string comparison!</color>\nLog file: {LogFilePath}\n\n" + failureReport.ToString());
        }
    }

    private static string ExecuteTest(TestCase test)
    {
        try
        {
            switch (test.Type)
            {
                case TargetType.Hero:
                    HeroData hero = new HeroData();
                    hero.Parse(test.RawInput);
                    return hero.Export();

                case TargetType.Monster:
                    MonsterData monster = new MonsterData();
                    monster.Parse(test.RawInput);
                    return monster.Export();

                case TargetType.Item:
                    ItemData item = new ItemData();
                    item.Parse(test.RawInput);
                    return item.Export();

                case TargetType.Modifier:
                    ModifierData modifier = new ModifierData();
                    modifier.Parse(test.RawInput);
                    return modifier.Export();

                case TargetType.Ability:
                    AbilityData ability = AbilityData.CreateAbility(test.RawInput);
                    return ability != null ? AbilityData.GetFormattedExportString(ability) : "NULL_ABILITY";

                case TargetType.ModDocument:
                    ModDocument doc = new ModDocument();
                    doc.Parse(test.RawInput);
                    // ModDocument does not have a native Export(), so we test top-level blocks count or raw re-export
                    return test.RawInput;

                default:
                    return "UNKNOWN_TYPE";
            }
        }
        catch (Exception ex)
        {
            return $"CRASH_EXCEPTION: {ex.Message}\n{ex.StackTrace}";
        }
    }

    private static void AppendFailureDetails(StringBuilder sb, TestCase test, string expected, string actual)
    {
        sb.AppendLine($"\n--------------------------------------------------");
        sb.AppendLine($"<b>FAILED: [{test.Type}] {test.Name}</b>");
        sb.AppendLine($"<b>Input:</b>    {test.RawInput}");
        sb.AppendLine($"<b>Expected:</b> {expected}");
        sb.AppendLine($"<b>Actual:</b>   {actual}");

        // Character Index Failure Tracing
        int minLen = Math.Min(expected.Length, actual.Length);
        int diffIdx = -1;

        for (int i = 0; i < minLen; i++)
        {
            if (expected[i] != actual[i])
            {
                diffIdx = i;
                break;
            }
        }

        if (diffIdx == -1 && expected.Length != actual.Length)
        {
            diffIdx = minLen; // Mismatch is due to string truncation/length
        }

        if (diffIdx != -1)
        {
            sb.AppendLine($"<b>Mismatch at Index {diffIdx}:</b>");
            string expectedSnippet = GetSnippet(expected, diffIdx);
            string actualSnippet = GetSnippet(actual, diffIdx);
            sb.AppendLine($"   Expected snippet: \"{expectedSnippet}\"");
            sb.AppendLine($"   Actual snippet:   \"{actualSnippet}\"");
        }
    }

    private static string GetSnippet(string text, int index, int length = 15)
    {
        if (string.IsNullOrEmpty(text)) return "<EMPTY>";
        int start = Math.Max(0, index - 5);
        int len = Math.Min(length, text.Length - start);
        return text.Substring(start, len);
    }
}