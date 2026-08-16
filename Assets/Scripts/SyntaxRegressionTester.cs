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

    private static readonly List<TestCase> TestCases = new List<TestCase>
    {

        new TestCase("Hero 1", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.181-1:143:126-2:126-2:131-1:12-1.i.left.k.cleave#k.vulnerable#facade.Leo18:-48:70:-8.i.right.k.cantrip#facade.har82:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1).doc.Swaps faces each turn.i.self.(et2.allitem.hat.(Statue.sd.177-1:171-3:171-1:171-1:187-1:11-2.i.left.k.cleave#k.vulnerable#k.mandatory#facade.Leo14:-48:70:-8.i.right.k.selfheal#k.cantrip#facade.eba13:0))",
            "((replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.181-1:143:126-2:126-2:131-1:12-1.i.(left.k.cleave#k.vulnerable#facade.Leo18:-48:70:-8).i.(right.k.cantrip#facade.har82:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1).i.self.et2.allitem.(hat.(Statue.sd.177-1:171-3:171-1:171-1:187-1:11-2.i.(left.k.cleave#k.vulnerable#k.mandatory#facade.Leo14:-48:70:-8).i.(right.k.selfheal#k.cantrip#facade.eba13:0))).doc.Swaps faces each turn)"),

        new TestCase("Witchking", TargetType.Hero,
            "((replica.Statue.n.Witchking.col.e.hp.8.tier.3.sd.38-2:15-2:182-2:5:131-2:5.speech.Do you not know death when you see it?.i.(unpack.(Determination)).i.(left.k.duel#facade.bas38:58:-25:-4).i.(mid.k.cruesh#k.duel#facade.Che6:0:47:0).i.(right.k.inflictpain#k.singleuse#facade.Eme3:0).i.(top.k.fierce#k.cruel#facade.Eme114:0).img.Valkyrie.thue.3db852:53:-29.hsv.73:24:-18).i.(triggerhpdata.(Fey.n.Flee.col.y.hp.22.sd.187-1.i.(left.k.fierce))).i.(t.(Presence)).i.(t.(Imp)))",
            "((replica.Statue.n.Witchking.col.e.hp.8.tier.3.sd.38-2:15-2:182-2:5:131-2:5.speech.Do you not know death when you see it?.i.(left.k.duel#facade.bas38:58:-25:-4).i.(mid.k.cruesh#k.duel#facade.Che6:0:47:0).i.(top.k.fierce#k.cruel#facade.Eme114:0).i.(right.k.inflictpain#k.singleuse#facade.Eme3:0).img.Valkyrie.thue.3db852:53:-29.hsv.73:24:-18).i.(unpack.(Determination)).i.(triggerhpdata.(Fey.n.Flee.col.y.hp.22.sd.187-1.i.(left.k.fierce))).i.(t.(Presence)).i.(t.(Imp)))"),

        new TestCase("Item - Future Shield", TargetType.Item,
            "(topbot.mid.hat.(Fey.sd.56-6.i.(left.k.stasis).i.(mid.sticker.(Enchanted Shield).m.6#ritemx.dae9#facade.Mut89:0#sidesc.Shield [pips]).i.(togpip#togkey)))"),

        new TestCase("Item - Self Megashield", TargetType.Item,
            "(left.mid.hat.(Fey.sd.186.i.mid.cast.sthief.abilitydata.(Fey.sd.56-999.i.(left.k.damage)).i.togtarg))",
            "(left.mid.hat.(Fey.sd.186.i.(mid.cast.sthief.abilitydata.(Fey.sd.56-999.i.(left.k.damage))).i.(togtarg)))"),

        new TestCase("Item - Palantir", TargetType.Item,
            "((mid.mid.hat.(Fey.sd.0:125-2.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(mid.hat.(Fey.sd.0:12-1.i.(k.mandatory#facade.DgK20:64:57:6)))#togtarg))).i.(mid.k.pain#facade.the30:0#sidesc.Replace the middle side with \"Gain 2 rerolls [violet]cantrip[cu] [red]pain[cu]\"[comma] then replace the face with 1 self damage [violet]cantrip[cu] [red]mandatory[cu] this turn).i.togunt)).img.Void.draw.Col9:0:-1.hsv.17:25:1.p.8bf770:ff3d95:67.tier.4.n.Palantir)",
            "(mid.mid.hat.(Fey.sd.0:125-2.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(mid.hat.(Fey.sd.0:12-1.i.(k.mandatory#facade.DgK20:64:57:6)))#togtarg))).i.(togunt).i.(mid.k.pain#facade.the30:0#sidesc.Replace the middle side with \"Gain 2 rerolls [violet]cantrip[cu] [red]pain[cu]\"[comma] then replace the face with 1 self damage [violet]cantrip[cu] [red]mandatory[cu] this turn)).img.Void.draw.Col9:0:-1.hsv.17:25:1.p.8bf770:ff3d95:67.tier.4.n.Palantir)"),

        new TestCase("Item - SelfVitality Claw", TargetType.Item,
            "(mid.hat.(Fey.sd.0:178-1.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Peanut Shell.part.1)#togtime#togtarg))).i.(mid.k.damage#k.selfheal#k.quaduse#facade.eba3:0).i.togunt))",
            "(mid.hat.(Fey.sd.0:178-1.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Peanut Shell.part.1)#togtime#togtarg))).i.(togunt).i.(mid.k.damage#k.selfheal#k.quaduse#facade.eba3:0)))"),

        new TestCase("Hero - Has Tactick", TargetType.Hero,
            "(replica.Statue.n.Aragorn.col.o.hp.12.tier.3.sd.174-1:117:132-2:133-2:105-4.img.Veteran.p.788490:90563d:34.hsv.0:-4:8.abilitydata.(Fey.n.Herbs.sd.111-2:0:56-2.img.spe29.hsv.31:0:0))"),

        new TestCase("Eoywn", TargetType.Hero,
            "((replica.Statue.n.Eowyn.col.i.hp.5.tier.2.sd.15.i.(left.k.pristeel#facade.Dog25:0).i.(topbot.hat.(Fey.sd.187:0:56-1:56-1:0:56-999.i.(topbot.k.descend#k.pristeel#facade.Che18:0:0:-9#sidesc.Shield [pips][comma] lose all shields [light]descend pristeel[cu]).i.(rightmost.k.damage).i.(rightmost.togtarg#Ballet Shoes#togunt))).i.(mid2.hat.(Fey.sd.187:56:0:0:69-1:56-999.i.(mid.k.engine#k.pristeel#facade.Che19:0:46:-23#sidesc.Shield [pips][comma] lose all shields).i.(right.k.pristeel#facade.eba24:0#sidesc.Shield [pips][comma] lose all shields).i.(rightmost.k.damage))).img.Scoundrel.p.4598a5:e2ab25:29.hsv.0:-7:11).i.(Enchanted Shield#Quicksilver))"),

        new TestCase("Arwen Unpack", TargetType.Hero,
            "((replica.Statue.n.Arwen.col.c.hp.3.tier.2.sd.108-1:0:0:176.i.(left.k.patient#facade.bas108:56:-54:0).i.(mid.sticker.(Sparks.splice.k.inspired)#togtime#k.singleuse#facade.Lem302:38:-49:0).i.(top.sticker.(top.k.defy)#togtime#k.singleuse#facade.the5:0:-23:-8).i.(bot.sticker.(Karma)#togtime#k.singleuse#facade.ite184:0).i.(self.Quick Nap^3.splice.unpack.Determination).img.Herbalist.p.b0827a:c79752:26.thue.11882d:33:24.hsv.-1:-13:-3).doc.[kas116] Immortality ends after turn 3.i.(t.(Log)))",
            "((replica.Statue.n.Arwen.col.c.hp.3.tier.2.sd.108-1:0:0:176.i.(left.k.patient#facade.bas108:56:-54:0).i.(mid.sticker.(Sparks.splice.k.inspired)#togtime#k.singleuse#facade.Lem302:38:-49:0).i.(top.sticker.(top.k.defy)#togtime#k.singleuse#facade.the5:0:-23:-8).i.(bot.sticker.(Karma)#togtime#k.singleuse#facade.ite184:0).img.Herbalist.p.b0827a:c79752:26.thue.11882d:33:24.hsv.-1:-13:-3).i.(self.Quick Nap^3.splice.unpack.Determination).i.(t.(Log)).doc.[kas116] Immortality ends after turn 3)"),

        new TestCase("Hero Saruman", TargetType.Hero,
            "(replica.Statue.n.Saruman.col.j.hp.9.tier.4.sd.0:187-1:122-4:122-4:182-3.i.(left.mid.hat.(Fey.sd.15-3.i.(left.k.damage).i.(mid.sticker.(all.left.hat.(Fey.sd.76.i.k.death#k.flesh.i.all.facade.har53:0))#facade.har53:0).i.((togkey#togpip)))).i.(mid.k.permaboost#facade.ite100:7:78:14).i.(topbot.k.managain#k.evil#facade.dee18:-9:29:-17).i.(right.k.cruel#k.fierce#facade.Leo14:51:99:-7).i.(rightmost.sticker.(Demonic Deal)#facade.ite288:0).img.Banshee.p.b02626:ff003d:02.p.716e5e:46174b:54).doc.[Golden Cup][plus]100 stored mana[comma] [green]withers[cu] 1 each turn.i.(learn.Infinity).i.(Golden Cup.part.1).i.self.ea.sThief.abilitydata.(Fey.sd.178-1:0:0:0:76-1.i.(left.k.wither))",
            "((replica.Statue.n.Saruman.col.j.hp.9.tier.4.sd.0:187-1:122-4:122-4:182-3.i.(left.mid.hat.(Fey.sd.15-3.i.(left.k.damage).i.(mid.sticker.(all.left.hat.(Fey.sd.76.i.(k.death#k.flesh).i.(all.facade.har53:0)))#facade.har53:0).i.(togkey#togpip))).i.(mid.k.permaboost#facade.ite100:7:78:14).i.(topbot.k.managain#k.evil#facade.dee18:-9:29:-17).i.(right.k.cruel#k.fierce#facade.Leo14:51:99:-7).i.(rightmost.sticker.(Demonic Deal)#facade.ite288:0).img.Banshee.p.b02626:ff003d:02.p.716e5e:46174b:54).i.self.ea.sThief.abilitydata.(Fey.sd.178-1:0:0:0:76-1.i.(left.k.wither)).i.(Golden Cup.part.1).i.(learn.Infinity).doc.[Golden Cup][plus]100 stored mana[comma] [green]withers[cu] 1 each turn)"),

        new TestCase("Grima", TargetType.Hero,
            "(replica.Statue.n.Grima.col.k.hp.4.tier.1.sd.181-2:181-2:181-1:181-1:15-1.i.(left.k.inflictsingleuse#k.selfshield#facade.Leo11:-45:95:-10).i.(mid.k.inflictexert#k.selfrepel#facade.Leo14:-70:79:-28).i.(topbot.k.poison#k.selfcleanse#facade.Leo12:-14:99:-21).i.(right.k.possessed#k.generous#k.cantrip#k.selfshield#facade.bas11:-19:0:0).img.Cultist.p.c38675:fdc1d7:02.thue.623f42:01:-30).abilitydata.(Fey.n.Whisper.sd.180-1.i.(left.k.annul).i.(top.cast.DSVarhest#Fly).img.Leo17.hsv.21:76:-18)",
            "(replica.Statue.n.Grima.col.k.hp.4.tier.1.sd.181-2:181-2:181-1:181-1:15-1.i.(left.k.inflictsingleuse#k.selfshield#facade.Leo11:-45:95:-10).i.(mid.k.inflictexert#k.selfrepel#facade.Leo14:-70:79:-28).i.(topbot.k.poison#k.selfcleanse#facade.Leo12:-14:99:-21).i.(right.k.possessed#k.generous#k.cantrip#k.selfshield#facade.bas11:-19:0:0).img.Cultist.p.c38675:fdc1d7:02.thue.623f42:01:-30.abilitydata.(Fey.n.Whisper.sd.180-1.i.(left.k.annul).i.(top.cast.DSVarhest#Fly).img.Leo17.hsv.21:76:-18))"),


        new TestCase("Doors", TargetType.Hero,
            "(replica.Statue.n.Doors of Durin.col.g.hp.5.tier.1.sd.0:5:5:5:5:5.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(top.hat.(Fey.sd.186.i.top.sticker.(mid.hat.(Fey.sd.186.i.mid.sticker.(bot.hat.(Fey.sd.186.i.bot.sticker.(right.hat.(Fey.sd.186.i.right.sticker.(rightmost.left.hat.(Fey.sd.182-100.i.(left.k.fierce#facade.sym23:52:-52:5)))#togtime#togtarg#k.cantrip#facade.sym24:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym14:52:-52:7))#togtime#togtarg#k.cantrip#facade.sym22:52:-52:7).img.Granite.p.0a0105:001e64:28.hsv.0:-30:-15.draw.Grave:3:4.hsv.-4:-20:10).i.(Wretched Crown).i.(Ghost Shield).i.(t.(Slate)).i.((self.((Wolf.doc.Speak Friend and Enter[dot]).spirit)))",
            "((replica.Statue.n.Doors of Durin.col.g.hp.5.tier.1.sd.0:5:5:5:5:5.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(top.hat.(Fey.sd.186.i.(top.sticker.(mid.hat.(Fey.sd.186.i.(mid.sticker.(bot.hat.(Fey.sd.186.i.(bot.sticker.(right.hat.(Fey.sd.186.i.(right.sticker.(rightmost.left.hat.(Fey.sd.182-100.i.(left.k.fierce#facade.sym23:52:-52:5)))#togtime#togtarg#k.cantrip#facade.sym24:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym21:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym14:52:-52:7)))#togtime#togtarg#k.cantrip#facade.sym22:52:-52:7))).img.Granite.p.0a0105:001e64:28.hsv.0:-30:-15.draw.Grave:3:4.hsv.-4:-20:10).i.(Wretched Crown).i.(Ghost Shield).i.(t.(Slate)).i.(self.Wolf.doc.Speak Friend and Enter[dot].spirit))"),

        new TestCase("Mega Galadriel", TargetType.Hero,
            "((replica.Statue.n.Galadriel.col.x.hp.8.tier.2.sd.0:61-1:72-1:72-1.i.(mid.k.engage#facade.ite148:8:9:10).i.(topbot.k.patient#facade.Aid5:0).i.(rightmost.hat.(Fey.sd.186.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Brittle#t.vase.add.((replica.Statue.n.Queen.col.x.hp.10.tier.5.sd.15:0:108-2:131-1:125-1:56-6.i.(left.k.charged#k.enduring#facade.Spi70:0:0:99).i.(top.k.cruel#k.enduring#facade.Ber117:0).i.(bot.k.cleave#k.enduring#facade.ite177:0).i.(right.k.managain#k.flurry#facade.Mut95:0).i.(rightmost.k.tall#k.enduring#facade.the31:-8:3:7).img.Fey.p.316767:835f15:13.p.698b46:911919:11.p.86ad5c:b36969:01.hsv.1:-7:23).i.(learn.Harvest).i.(t.(Housecat)).gift.(allitem.(mid.hat.(Fey.sd.0:103-1.i.(mid.k.mandatory#k.pain#k.wither#facade.pos139:0:0:9#sidesc.All shall love me and despair[comma] heal [pips]))).mn.Queens Love))\r\n)#togtarg#togtime))#facade.ite144:61:95:-28).i.(rightmost.mid.hat.(egg.((replica.Statue.n.Queen.col.x.hp.10.tier.5.sd.15:109-3:108-2:131-1:125-1:56-4.i.(left.k.charged#k.enduring#facade.Spi70:0:0:99).i.(top.k.cruel#k.enduring#facade.Ber117:0).i.(bot.k.cleave#k.enduring#facade.ite177:0).i.(right.k.managain#k.flurry#facade.Mut95:0).i.(rightmost.k.tall#k.enduring#facade.the31:-8:3:7).img.Fey.p.316767:835f15:13.p.698b46:911919:11.p.86ad5c:b36969:01.hsv.1:-7:23).i.(learn.Harvest)))#k.death#facade.ite144:61:95:-16#togunt#sidesc.[wiggle]This act will have consequences[dot][wiggle] Become the [red]Queen[cu] [violet]perma[cu][light]death[cu]))).img.Weaver.p.645141:fbd85a:07.hsv.0:-13:14).i.(learn.Light))",
            "((replica.Statue.n.Galadriel.col.x.hp.8.tier.2.sd.0:61-1:72-1:72-1.i.(mid.k.engage#facade.ite148:8:9:10).i.(topbot.k.patient#facade.Aid5:0).i.(rightmost.hat.(Fey.sd.186.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Brittle#t.vase.add.((replica.Statue.n.Queen.col.x.hp.10.tier.5.sd.15:0:108-2:131-1:125-1:56-6.i.(left.k.charged#k.enduring#facade.Spi70:0:0:99).i.(top.k.cruel#k.enduring#facade.Ber117:0).i.(bot.k.cleave#k.enduring#facade.ite177:0).i.(right.k.managain#k.flurry#facade.Mut95:0).i.(rightmost.k.tall#k.enduring#facade.the31:-8:3:7).img.Fey.p.316767:835f15:13.p.698b46:911919:11.p.86ad5c:b36969:01.hsv.1:-7:23).gift.(allitem.(mid.hat.(Fey.sd.0:103-1.i.(mid.k.mandatory#k.pain#k.wither#facade.pos139:0:0:9#sidesc.All shall love me and despair[comma] heal [pips]))).mn.Queens Love).i.(learn.Harvest).i.(t.(Housecat))))#togtarg#togtime))#facade.ite144:61:95:-28).i.(rightmost.mid.hat.(egg.((replica.Statue.n.Queen.col.x.hp.10.tier.5.sd.15:109-3:108-2:131-1:125-1:56-4.i.(left.k.charged#k.enduring#facade.Spi70:0:0:99).i.(top.k.cruel#k.enduring#facade.Ber117:0).i.(bot.k.cleave#k.enduring#facade.ite177:0).i.(right.k.managain#k.flurry#facade.Mut95:0).i.(rightmost.k.tall#k.enduring#facade.the31:-8:3:7).img.Fey.p.316767:835f15:13.p.698b46:911919:11.p.86ad5c:b36969:01.hsv.1:-7:23).i.(learn.Harvest)))#k.death#facade.ite144:61:95:-16#togunt#sidesc.[wiggle]This act will have consequences[dot][wiggle] Become the [red]Queen[cu] [violet]perma[cu][light]death[cu]))).img.Weaver.p.645141:fbd85a:07.hsv.0:-13:14).i.(learn.Light))"),

        new TestCase("Gandalf Res", TargetType.Hero,
            "(replica.Statue.n.Gandalf.col.g.hp.8.tier.3.sd.18-2:18-2:76-2:76-2:35-1.i.(left2.k.reborn#facade.bas18:-4:0:22).i.(topbot.k.fizz#k.reborn#facade.pos129:0:43:33).i.(right.k.reborn).i.(rightmost.enchant.(t2.ea.sthief.abilitydata.(Fey.sd.15-1:0:0:0:76-1.img.Ber75.i.(left.mid.hat.(egg.(Fey.n.Revive.i.dead crow#brittle#t.(orb.operate)))#blindfold))).mn.Revive#k.singleuse#facade.pos183:0#sidesc.Revive a hero at the start of the third turn).img.Wizard).i.(Bone Charm).i.(learn.Balance)",
            "((replica.Statue.n.Gandalf.col.g.hp.8.tier.3.sd.18-2:18-2:76-2:76-2:35-1.i.(left2.k.reborn#facade.bas18:-4:0:22).i.(topbot.k.fizz#k.reborn#facade.pos129:0:43:33).i.(right.k.reborn).i.(rightmost.enchant.(t2.ea.sthief.abilitydata.(Fey.sd.15-1:0:0:0:76-1.img.Ber75.i.(left.mid.hat.(egg.(Fey.n.Revive.i.dead crow#brittle#t.(orb.operate)))#blindfold))).mn.Revive#k.singleuse#facade.pos183:0#sidesc.Revive a hero at the start of the third turn).img.Wizard).i.(Bone Charm).i.(learn.Balance))"),

        new TestCase("Gandalf Shadowfax", TargetType.Hero,
            "(replica.Statue.n.Gandalf The White.col.w.hp.10.tier.5.sd.45-3:70-3:94-1:77-1:18-3:136-1.i.(left.k.reborn#facade.har92:37:-33:9).i.(mid.k.reborn#facade.bas70:16:-26:23).i.(bot.k.era#facade.har101:0).i.(right.k.reborn#facade.bas18:50:-46:9).i.(rightmost.ritemx.dae9#facade.bas136:61:-31:13).img.Wizard.hsv.0:-26:11.p.59a8e4:fff:42.abilitydata.(Fey.n.Presense.sd.34-1:0:0:0:76-3.i.(left.k.fierce).img.Lem156.hsv.0:-23:21).abilitydata.(Fey.n.Shadowfax.sd.15-1:0:0:0:76-3.i.k.singleuse.i.(left.mid.hat.(egg.(replica.Statue.n.Shadowfax.col.w.hp.6.tier.0.sd.102-1:118-3:71-3:71-3:88-3:136-1.i.(left.k.first#facade.bas102:0:-38:3).i.(mid.k.first#facade.bas118:0:-32:9).i.(topbot.k.first#facade.bas71:0:0:24).i.(right.k.first#facade.ite47:0:-50:0).i.(rightmost.k.first#facade.bas136:0:-16:34).img.Wolf.p.70675c:fff:99).doc.[plus]1 Extra Reroll.i.(t.(jinx.Extra reroll)))#blindfold).img.Lem25))",
            "(replica.Statue.n.Gandalf The White.col.w.hp.10.tier.5.sd.45-3:70-3:94-1:77-1:18-3:136-1.i.(left.k.reborn#facade.har92:37:-33:9).i.(mid.k.reborn#facade.bas70:16:-26:23).i.(bot.k.era#facade.har101:0).i.(right.k.reborn#facade.bas18:50:-46:9).i.(rightmost.ritemx.dae9#facade.bas136:61:-31:13).img.Wizard.hsv.0:-26:11.p.59a8e4:fff:42.abilitydata.(Fey.n.Presense.sd.34-1:0:0:0:76-3.i.(left.k.fierce).img.Lem156.hsv.0:-23:21).abilitydata.(Fey.n.Shadowfax.sd.15-1:0:0:0:76-3.i.(left.mid.hat.(egg.((replica.Statue.n.Shadowfax.col.w.hp.6.tier.0.sd.102-1:118-3:71-3:71-3:88-3:136-1.i.(left.k.first#facade.bas102:0:-38:3).i.(mid.k.first#facade.bas118:0:-32:9).i.(topbot.k.first#facade.bas71:0:0:24).i.(right.k.first#facade.ite47:0:-50:0).i.(rightmost.k.first#facade.bas136:0:-16:34).img.Wolf.p.70675c:fff:99).i.(t.jinx.Extra reroll).doc.[plus]1 Extra Reroll))#blindfold).i.(k.singleuse).img.Lem25))"),

        new TestCase("Summon Gwaihir", TargetType.Hero,
            "(replica.Wizard.n.Gandalf.col.g.hp.7.tier.2.sd.0:186:0:0:66-1.i.(mid.facade.Leo12:0:0:-41).i.(right.k.defy#facade.ite174:7:99:12).i.(mid.hat.(Fey.sd.0:186.i.(left.mid.hat.(egg.(replica.Statue.n.Gwaihir.col.h.hp.10.tier.0.sd.171-2:118-3:15-3:15-3:118-2:176.i.(left.k.rescue#facade.dan8:0).i.(mid2.k.rescue#facade.Che22:7:0:0).i.(topbot.k.rescue#facade.dan15:0).i.(rightmost.sticker.(k.ranged)#facade.ite8:-7:50:-28).img.Caw.p.3ccbb2:ad501a:51).i.(t.(Archer)))#blindfold).i.(mid.hat.(Fey.sd.186.i.(mid.sticker.(self.unpack.Crumbling Castle.part.1)#togtarg))).i.togunt)#facade.Leo12:0))",
            "(replica.Wizard.n.Gandalf.col.g.hp.7.tier.2.sd.0:186:0:0:66-1.i.(mid.facade.Leo12:0:0:-41).i.(right.k.defy#facade.ite174:7:99:12).i.(mid.hat.(Fey.sd.0:186.i.(left.mid.hat.(egg.((replica.Statue.n.Gwaihir.col.h.hp.10.tier.0.sd.171-2:118-3:15-3:15-3:118-2:176.i.(left.k.rescue#facade.dan8:0).i.(mid2.k.rescue#facade.Che22:7:0:0).i.(topbot.k.rescue#facade.dan15:0).i.(rightmost.sticker.(k.ranged)#facade.ite8:-7:50:-28).img.Caw.p.3ccbb2:ad501a:51).i.(t.(Archer))))#blindfold).i.(mid.hat.(Fey.sd.186.i.(mid.sticker.(self.unpack.Crumbling Castle.part.1)#togtarg))).i.(togunt))#facade.Leo12:0))"),

        new TestCase("Golum 2", TargetType.Hero,
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.13:13:13:13:13:13.i.row.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Smeagol.col.k.hp.6.tier.3.sd.168-3:125-1:103-2:103-2:124:130.i.left.k.cantrip#facade.bas168:76:26:11.i.topbot.k.generous#facade.eba13:0.i.rightmost.k.sticky#facade.bas130:79:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.topbot.mid.hat.(statue.sd.0:13.i.left.hat.((egg.(replica.Statue.n.Gollum.col.k.hp.6.tier.3.sd.30-4:44-2:158-1:158-1:11-2:11-3.i.left.k.exert#facade.ale4:0.i.topbot.k.selfheal.img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.togunt).i.row.facade.Eme97:0.i.topbot.facade.Eme114:-11:0:0.img.Alien.thue.c571da:92:8.hsv.0:-57:-1)",
            "(replica.Statue.n.Precious.col.k.hp.6.tier.3.sd.13:13:13:13:13:13.i.(row.mid.hat.(statue.sd.0:13.i.(left.hat.(egg.(replica.Statue.n.Smeagol.col.k.hp.6.tier.3.sd.168-3:125-1:103-2:103-2:124:130.i.(left.k.cantrip#facade.bas168:76:26:11).i.(topbot.k.generous#facade.eba13:0).i.(rightmost.k.sticky#facade.bas130:79:0:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.(togunt))).i.(topbot.mid.hat.(statue.sd.0:13.i.(left.hat.(egg.(replica.Statue.n.Gollum.col.k.hp.6.tier.3.sd.30-4:44-2:158-1:158-1:11-2:11-3.i.(left.k.exert#facade.ale4:0).i.(topbot.k.selfheal).img.Alien.thue.c571da:92:8.hsv.0:-57:-1))).i.(togunt))).i.(row.facade.Eme97:0).i.(topbot.facade.Eme114:-11:0:0).img.Alien.thue.c571da:92:8.hsv.0:-57:-1)"),

        new TestCase("Monster 1", TargetType.Monster,
            "((rmon.ded.hsv.68:55:0.draw.bas199:4:4.p.0ca73c:1b0559:25.n.Watcher In The Water.hp.20.sd.23-2:24-2:11-1:11-1:16-2:16-1.i.left.facade.bas211:33:0:19.i.mid.facade.bas212:30:21:16.i.topbot.facade.Eme147:-35:0:0.i.right2.k.petrify#facade.Eme201:63:0:0).i.triggerhpdata.(Fey.col.p.sd.123-1.n.Dodge.hp.5).i.(t.(archer)))",
            "((rmon.ded.hsv.68:55:0.draw.bas199:4:4.p.0ca73c:1b0559:25.n.Watcher In The Water.hp.20.sd.23-2:24-2:11-1:11-1:16-2:16-1.i.(left.facade.bas211:33:0:19).i.(mid.facade.bas212:30:21:16).i.(topbot.facade.Eme147:-35:0:0).i.(right2.k.petrify#facade.Eme201:63:0:0)).i.(triggerhpdata.(Fey.n.Dodge.col.p.hp.5.sd.123-1)).i.(t.(archer)))"),

        new TestCase("Traitors", TargetType.Monster,
            "((rmon.1c.n.Traitor.hp.6.sd.187-1:65-3:69-1:69-1:65-2:56-1.i.left.k.permaboost#k.singleuse#facade.har69:0.i.rightmost.k.descend#facade.eba28:0.img.Militia.thue.73675c:16:29.hsv.5:27:-10.bal.Militia).t.(jinx.alliteme.Quicksilver))",
            "((rmon.1c.n.Traitor.hp.6.sd.187-1:65-3:69-1:69-1:65-2:56-1.i.(left.k.permaboost#k.singleuse#facade.har69:0).i.(rightmost.k.descend#facade.eba28:0).img.Militia.thue.73675c:16:29.hsv.5:27:-10.bal.Militia).i.(t.jinx.alliteme.(Quicksilver)))"),

        new TestCase("Dunharrow", TargetType.Monster,
            "((rmon.1a.n.Dunharrow.hp.20.sd.24:17-1:0-2:0:19-2:6-3.i.(topbot.mid.hat.(x2.egg.((Carrier.n.Dead Men.hp.8.sd.54-1:21-3:15-2:15-2:15-2:15-1.i.(mid.k.acidic#facade.eba6:0).i.(right2.k.acidic#facade.bas15:29:0:0).i.(topbot.k.acidic#facade.bas15:29:0:0).img.Gladiator.p.f8f8f8:fada49:36.thue.ab4f2b:42:13.hsv.1:85:2).i.(onhitdata.(Fey.n.Selfpoison.sd.187-1.i.(left.k.poison))).i.(t.(Zombie))))#blindfold#facade.bas235:23:0:0).i.(left.k.plague#k.poison#k.onesie#facade.kas96:17:53:5).i.(mid.k.onesie#k.plague#facade.bas237:5:99:-2).i.(right.k.onesie#k.plague#facade.bas239:5:99:6).i.(rightmost.k.eliminate#k.onesie#k.poison#k.plague#facade.Eme47:58:0:-51).img.The Hand.p.452222:cce19d:10.hsv.0:-30:5.draw.Banshee:16:12.p.434340:488820:19).i.(self.vase.ch.i(First Aid Kit.splice.(rightmost.hat.(Fey.i.(rightmost.mid.hat.(egg.((replica.Statue.n.Oathbreaker.col.l.hp.7.tier.0.sd.127-7:131-2:95-1:95-1:131-1:136-2.i.(rightmost.k.death#facade.pos176:0).img.Gladiator.thue.ab4f2b:42:34.hsv.7:83:2).i.(Brittle).i.(t.(Ghost))))#blindfold#k.potion#facade.kas21:31:0:0))).img.Void.draw.kas21:0:-1.hsv.33:0:1.tier.9.n.Hold Oath.doc.Potion).mn.Oathbreakers.spirit))",
            "((rmon.1a.n.Dunharrow.hp.20.sd.24:17-1:0-2:0:19-2:6-3.i.(topbot.mid.hat.(x2.egg.((Carrier.n.Dead Men.hp.8.sd.54-1:21-3:15-2:15-2:15-2:15-1.i.(mid.k.acidic#facade.eba6:0).i.(topbot.k.acidic#facade.bas15:29:0:0).i.(right2.k.acidic#facade.bas15:29:0:0).img.Gladiator.p.f8f8f8:fada49:36.thue.ab4f2b:42:13.hsv.1:85:2).i.(onhitdata.(Fey.n.Selfpoison.sd.187-1.i.(left.k.poison))).i.(t.(Zombie))))#blindfold#facade.bas235:23:0:0).i.(left.k.plague#k.poison#k.onesie#facade.kas96:17:53:5).i.(mid.k.onesie#k.plague#facade.bas237:5:99:-2).i.(right.k.onesie#k.plague#facade.bas239:5:99:6).i.(rightmost.k.eliminate#k.onesie#k.poison#k.plague#facade.Eme47:58:0:-51).img.The Hand.p.452222:cce19d:10.hsv.0:-30:5.draw.Banshee:16:12.p.434340:488820:19).i.(self.vase.ch.i(First Aid Kit.splice.(rightmost.hat.(Fey.i.(rightmost.mid.hat.(egg.((replica.Statue.n.Oathbreaker.col.l.hp.7.tier.0.sd.127-7:131-2:95-1:95-1:131-1:136-2.i.(rightmost.k.death#facade.pos176:0).img.Gladiator.thue.ab4f2b:42:34.hsv.7:83:2).i.(Brittle).i.(t.(Ghost))))#blindfold#k.potion#facade.kas21:31:0:0))).img.Void.draw.kas21:0:-1.hsv.33:0:1.tier.9.n.Hold Oath.doc.Potion).mn.Oathbreakers.spirit))"),

        new TestCase("Troll", TargetType.Monster,
            "(Troll.p.65623a:687a69:23.thue.d00051:36:20.hsv.-14:12:5.n.Cave Troll.sd.3-3:2-6:16-2:16-2:17-3.i.mid.k.exert#k.serrated#k.growth#facade.Ese105:31:34:2.i.right.k.engage#facade.bas205:42:-20:3.i.rightmost.mid.hat.(x2.egg.((Slimelet.n.Orc.hp.3.sd.7-4:7-3:6-3:6-3.t.Caw.img.Bones.hsv.0:0:-70.draw.Goblin:-6:-4.p.6a6e34:5c7688:07).i.onhitdata.(Fey.sd.15-1.i.left.mid.hat.(Fey.sd.186.i.mid.sticker.(Pharaoh Curse.part.1)#togtarg))))#blindfold#facade.bas209:38:0:0)",
            "(Troll.p.65623a:687a69:23.thue.d00051:36:20.hsv.-14:12:5.n.Cave Troll.sd.3-3:2-6:16-2:16-2:17-3.i.(mid.k.exert#k.serrated#k.growth#facade.Ese105:31:34:2).i.(right.k.engage#facade.bas205:42:-20:3).i.(rightmost.mid.hat.(x2.egg.((Slimelet.n.Orc.hp.3.sd.7-4:7-3:6-3:6-3.img.Bones.hsv.0:0:-70.draw.Goblin:-6:-4.p.6a6e34:5c7688:07).i.(onhitdata.(Fey.sd.15-1.i.(left.mid.hat.(Fey.sd.186.i.(mid.sticker.(Pharaoh Curse.part.1)#togtarg))))).i.(t.(Caw))))#blindfold#facade.bas209:38:0:0))"),


        new TestCase("Troll", TargetType.Monster,
            "((Alpha.n.Petrified.hp.8.sd.3-2:3-1:16-1:16-1:16-1:3-1.i.right.k.exert.i.bot.k.exert.i.left.k.exert.img.Troll.p.65623a:a6a6a6:35).doc.Slowly thawing out!.i.(x6.Basilisk Scale.part.0).i.self.ea.sThief.abilitydata.(Fey.sd.178-1:0:0:0:76-1.i.left.k.cleanse))",
            "((Alpha.n.Petrified.hp.8.sd.3-2:3-1:16-1:16-1:16-1:3-1.i.(left.k.exert).i.(bot.k.exert).i.(right.k.exert).img.Troll.p.65623a:a6a6a6:35).i.self.ea.sThief.abilitydata.(Fey.sd.178-1:0:0:0:76-1.i.(left.k.cleanse)).i.(x6.Basilisk Scale.part.0).doc.Slowly thawing out!)"),


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
            string cleanExpected = test.ExpectedOutput.Trim();

            // Pass 1: RawInput -> ExpectedOutput
            string actualOutput = ExecuteTest(test.Type, test.RawInput);
            string cleanActual = actualOutput?.Trim() ?? "";

            if (!string.Equals(cleanExpected, cleanActual, StringComparison.Ordinal))
            {
                failedTests++;
                fileLog.AppendLine($"[FAIL] [{test.Type}] {test.Name} (Raw Input Mismatch)");
                AppendFailureDetails(failureReport, test, cleanExpected, cleanActual);
                continue;
            }

            // Pass 2: ExpectedOutput -> ExpectedOutput (Idempotency check)
            string secondPassOutput = ExecuteTest(test.Type, cleanExpected);
            string cleanSecondPass = secondPassOutput?.Trim() ?? "";

            if (!string.Equals(cleanExpected, cleanSecondPass, StringComparison.Ordinal))
            {
                failedTests++;
                fileLog.AppendLine($"[FAIL] [{test.Type}] {test.Name} (Expected Output Re-export Mismatch)");
                AppendFailureDetails(failureReport, test, cleanExpected, cleanSecondPass);
            }
            else
            {
                passedTests++;
                fileLog.AppendLine($"[PASS] [{test.Type}] {test.Name}");
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
    private static string ExecuteTest(TargetType type, string input)
    {
        try
        {
            switch (type)
            {
                case TargetType.Hero:
                    HeroData hero = new HeroData();
                    hero.Parse(input);
                    return hero.Export();

                case TargetType.Monster:
                    MonsterData monster = new MonsterData();
                    monster.Parse(input);
                    return monster.Export();

                case TargetType.Item:
                    ItemData item = new ItemData();
                    item.Parse(input);
                    return item.Export();

                case TargetType.Modifier:
                    ModifierData modifier = new ModifierData();
                    modifier.Parse(input);
                    return modifier.Export();

                case TargetType.Ability:
                    AbilityData ability = AbilityData.CreateAbility(input);
                    return ability != null ? AbilityData.GetFormattedExportString(ability) : "NULL_ABILITY";

                case TargetType.ModDocument:
                    ModDocument doc = new ModDocument();
                    doc.Parse(input);
                    return input;

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