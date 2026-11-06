using System.Linq;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class ShopControllerTests
    {
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "a", Attack = 1, AttackIntervalTicks = 1, Rarity = Rarity.Common },
            new SpeciesDef { Id = "b", Attack = 1, AttackIntervalTicks = 1, Rarity = Rarity.Common },
            new SpeciesDef { Id = "c", Attack = 1, AttackIntervalTicks = 1, Rarity = Rarity.Rare },
        });

        private static ShopRules Rules(int maxLevel = 3) => new ShopRules
        {
            StartGold = 30,
            FirstIncome = 35,
            IncomeStep = 5,
            ShopSlots = 5,
            RerollCost = 3,
            PriceByRarity = new[] { 10, 20, 30, 40, 50 },
            SellPercent = 75,
            MergeCount = 3,
            MaxLevel = maxLevel,
        };

        private static ShopController Shop(int maxLevel = 3) => new ShopController(Rules(maxLevel), Catalog());

        private static RunState State(long seed = 1) => new RunState { RunId = "r", Seed = seed, Round = 1, Lives = 10 };

        private static RunState WithShop(int gold, params string[] shop)
        {
            RunState s = State();
            s.Gold = gold;
            s.Shop.AddRange(shop);
            return s;
        }

        private static MonsterSnapshot M(string id, int level = 1) => new MonsterSnapshot { SpeciesId = id, Level = level };

        [TestCase(1, 30)]
        [TestCase(2, 35)]
        [TestCase(5, 50)]
        [TestCase(7, 60)]
        public void Income_StartThenPlusFive(int round, int expected)
        {
            Assert.That(Rules().IncomeFor(round), Is.EqualTo(expected));
        }

        [Test]
        public void BeginRound_GivesIncomeAndRollsOnce()
        {
            var shop = Shop();
            RunState s = State();
            shop.BeginRound(s);
            Assert.That(s.Gold, Is.EqualTo(30));
            Assert.That(s.Shop.Count, Is.EqualTo(5));
            Assert.That(s.Shop.All(id => id == "a" || id == "b" || id == "c"), Is.True);
            var first = s.Shop.ToList();
            shop.BeginRound(s);
            Assert.That(s.Gold, Is.EqualTo(30), "같은 라운드에 다시 불러도 수입 없음");
            Assert.That(s.Shop, Is.EqualTo(first));

            s.Gold = 4;
            s.Round = 2;
            shop.BeginRound(s);
            Assert.That(s.Gold, Is.EqualTo(4 + 35), "남은 골드는 이월");
        }

        [Test]
        public void Roll_SameSeedSameShop_RerollIsDeterministic()
        {
            var shop = Shop();
            RunState a = State(42), b = State(42);
            shop.BeginRound(a);
            shop.BeginRound(b);
            Assert.That(a.Shop, Is.EqualTo(b.Shop));
            Assert.That(shop.Reroll(a), Is.EqualTo(ShopError.None));
            Assert.That(shop.Reroll(b), Is.EqualTo(ShopError.None));
            Assert.That(a.Shop, Is.EqualTo(b.Shop));
            Assert.That(a.Gold, Is.EqualTo(24));
            Assert.That(a.ShopRollIndex, Is.EqualTo(1));
        }

        [Test]
        public void Reroll_NotEnoughGold_ChangesNothing()
        {
            var shop = Shop();
            RunState s = WithShop(2, "a", "b");
            Assert.That(shop.Reroll(s), Is.EqualTo(ShopError.NotEnoughGold));
            Assert.That(s.Gold, Is.EqualTo(2));
            Assert.That(s.Shop, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Buy_PutsOnBench_PaysByRarity_EmptiesCard()
        {
            var shop = Shop();
            RunState s = WithShop(100, "a", "c");
            Assert.That(shop.Buy(s, 1), Is.EqualTo(ShopError.None));
            Assert.That(s.Gold, Is.EqualTo(70), "희귀 = 30");
            Assert.That(s.Bench[0].SpeciesId, Is.EqualTo("c"));
            Assert.That(s.Shop[1], Is.Null);
            Assert.That(shop.Buy(s, 1), Is.EqualTo(ShopError.Empty));
            Assert.That(shop.Buy(s, 9), Is.EqualTo(ShopError.Invalid));
        }

        [Test]
        public void Buy_NotEnoughGold()
        {
            var shop = Shop();
            RunState s = WithShop(9, "a");
            Assert.That(shop.Buy(s, 0), Is.EqualTo(ShopError.NotEnoughGold));
            Assert.That(s.Bench.All(m => m == null), Is.True);
        }

        [Test]
        public void ThreeSame_MergeToLevel2_KeepsBoardPositionFirst()
        {
            var shop = Shop();
            RunState s = WithShop(100, "a", "a", "a");
            s.Team[2] = M("a");
            s.Gold = 100;
            s.Shop.RemoveAt(2);
            shop.Buy(s, 0);
            shop.Buy(s, 1);
            Assert.That(s.Team[2].Level, Is.EqualTo(2), "보드에 있던 자리로 합쳐짐");
            Assert.That(s.Bench.All(m => m == null), Is.True);
            Assert.That(s.Gold, Is.EqualTo(80));
        }

        [Test]
        public void ChainMerge_ToLevel3()
        {
            var shop = Shop();
            RunState s = WithShop(100, "a");
            s.Team[0] = M("a", 2);
            s.Team[1] = M("a", 2);
            s.Bench[0] = M("a");
            s.Bench[1] = M("a");
            shop.Buy(s, 0);
            Assert.That(s.Team[0].Level, Is.EqualTo(3));
            Assert.That(s.Team[1], Is.Null);
            Assert.That(s.Bench.All(m => m == null), Is.True);
        }

        [Test]
        public void MaxLevel_DoesNotMerge()
        {
            var shop = Shop(maxLevel: 2);
            RunState s = WithShop(100, "a");
            s.Team[0] = M("a", 2);
            s.Team[1] = M("a", 2);
            s.Bench[0] = M("a");
            s.Bench[1] = M("a");
            shop.Buy(s, 0);
            Assert.That(s.Team[0].Level, Is.EqualTo(2));
            Assert.That(s.Team[1].Level, Is.EqualTo(2));
            Assert.That(s.Bench.Count(m => m != null && m.Level == 2), Is.EqualTo(1), "Lv1 셋은 Lv2 하나로");
        }

        [Test]
        public void BenchFull_BuyThatMerges_IsAllowed_OtherwiseBenchFull()
        {
            var shop = Shop();
            RunState s = WithShop(100, "c", "a");
            s.Bench[0] = M("a");
            s.Bench[1] = M("a");
            s.Bench[2] = M("b");
            s.Bench[3] = M("b");
            Assert.That(shop.Buy(s, 0), Is.EqualTo(ShopError.BenchFull));
            Assert.That(s.Gold, Is.EqualTo(100));
            Assert.That(shop.Buy(s, 1), Is.EqualTo(ShopError.None));
            Assert.That(s.Bench[0].Level, Is.EqualTo(2));
            Assert.That(s.Bench[1], Is.Null);
            Assert.That(s.Gold, Is.EqualTo(90));
        }

        [Test]
        public void Merge_CombinesBuffs()
        {
            var shop = Shop();
            RunState s = WithShop(100, "a");
            s.Bench[0] = M("a");
            s.Bench[0].Buffs.Add(new StatBuff("attack", 2));
            s.Bench[1] = M("a");
            s.Bench[1].Buffs.Add(new StatBuff("attack", 3));
            shop.Buy(s, 0);
            Assert.That(s.Bench[0].Buffs.Sum(b => b.Value), Is.EqualTo(5));
        }

        [Test]
        public void SellValue_75Percent_Rounded()
        {
            var shop = Shop();
            Assert.That(shop.SellValue(M("a")), Is.EqualTo(8), "10 × 75% = 7.5 → 8");
            Assert.That(shop.SellValue(M("a", 2)), Is.EqualTo(23), "30 × 75% = 22.5 → 23 (원작 공략 Lv2 = $23)");
            Assert.That(shop.SellValue(M("c")), Is.EqualTo(23), "30 × 75%");
            Assert.That(shop.SellValue(M("a", 3)), Is.EqualTo(68), "90 × 75% = 67.5 → 68");
        }

        [Test]
        public void Sell_GivesGold_ClearsSlot()
        {
            var shop = Shop();
            RunState s = State();
            s.Team[4] = M("a", 2);
            Assert.That(shop.Sell(s, Location.Board(4)), Is.EqualTo(ShopError.None));
            Assert.That(s.Gold, Is.EqualTo(23));
            Assert.That(s.Team[4], Is.Null);
            Assert.That(shop.Sell(s, Location.Board(4)), Is.EqualTo(ShopError.Empty));
        }

        [Test]
        public void Move_SwapsAndMovesToEmpty()
        {
            var shop = Shop();
            RunState s = State();
            s.Bench[0] = M("a");
            s.Team[1] = M("b");
            Assert.That(shop.Move(s, Location.Bench(0), Location.Board(1)), Is.EqualTo(ShopError.None));
            Assert.That(s.Team[1].SpeciesId, Is.EqualTo("a"));
            Assert.That(s.Bench[0].SpeciesId, Is.EqualTo("b"));
            Assert.That(shop.Move(s, Location.Board(1), Location.Board(5)), Is.EqualTo(ShopError.None));
            Assert.That(s.Team[1], Is.Null);
            Assert.That(s.Team[5].SpeciesId, Is.EqualTo("a"));
            Assert.That(shop.Move(s, Location.Board(0), Location.Board(2)), Is.EqualTo(ShopError.Empty));
            Assert.That(shop.Move(s, Location.Bench(4), Location.Board(2)), Is.EqualTo(ShopError.Invalid));
        }

        [Test]
        public void RunState_V1Json_MigratesToV2()
        {
            const string v1 = "{\"formatVersion\":1,\"runId\":\"r\",\"seed\":5,\"round\":3,\"wins\":2,\"lives\":9,\"livesLost\":1,\"trainerId\":\"t\"," +
                              "\"team\":[{\"speciesId\":\"a\",\"level\":1,\"buffs\":[]},null,null,null,null,null],\"facedRunIds\":[\"x\"],\"history\":[]}";
            RunState s = RunStateJson.Deserialize(v1);
            Assert.That(s.FormatVersion, Is.EqualTo(2));
            Assert.That(s.Gold, Is.EqualTo(0));
            Assert.That(s.Bench.Length, Is.EqualTo(RunState.BenchSlotCount));
            Assert.That(s.Shop, Is.Empty);
            Assert.That(s.Wins, Is.EqualTo(2));
            Assert.That(s.Team[0].SpeciesId, Is.EqualTo("a"));
        }

        [Test]
        public void RunState_WithShop_RoundTrip()
        {
            var shop = Shop();
            RunState s = State(9);
            shop.BeginRound(s);
            shop.Buy(s, 0);
            string once = RunStateJson.Serialize(s);
            Assert.That(RunStateJson.Serialize(RunStateJson.Deserialize(once)), Is.EqualTo(once));
        }
    }
}
