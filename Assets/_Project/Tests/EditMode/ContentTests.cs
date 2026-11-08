using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>3-2 콘텐츠: 새 효과 4개(이웃 공격력 · 이웃 속도 · 골드 · 공격할수록 강해짐) + 라운드별 등급 확률표 뽑기. 기대값 옆에 계산식.</summary>
    public class ContentTests
    {
        // ───────── 전투 ─────────

        private static BattleRules Rules() => new BattleRules
        {
            TicksPerSecond = 10,
            TeamHpByRound = new[] { 100000 },
            SuddenDeathStartTick = 1000,
            SuddenDeathIntervalTicks = 10,
            SuddenDeathBaseDamage = 5,
            SuddenDeathDamageStep = 5,
            MaxTicks = 2000,
            BurnIntervalTicks = 10,
            PoisonIntervalTicks = 20,
        };

        private static BattleUnit U(int attack, int interval, AbilityTrigger t = AbilityTrigger.None, AbilityEffect e = AbilityEffect.None, int amount = 0, int everyN = 0) =>
            new BattleUnit(attack, interval, new BattleAbility(t, e, amount, everyN));

        /// <summary>공격하지 않는 허수아비 (간격 5000틱).</summary>
        private static BattleUnit Dummy(AbilityTrigger t = AbilityTrigger.None, AbilityEffect e = AbilityEffect.None, int amount = 0) => U(0, 5000, t, e, amount);

        private static BattleTeam Team(params (int slot, BattleUnit unit)[] units)
        {
            var team = new BattleTeam();
            foreach (var (slot, unit) in units) team[slot] = unit;
            return team;
        }

        [TestCase(0, new[] { 1, 3 })]
        [TestCase(1, new[] { 0, 2, 4 })]
        [TestCase(2, new[] { 1, 5 })]
        [TestCase(3, new[] { 4, 0 })]
        [TestCase(4, new[] { 3, 5, 1 })]
        [TestCase(5, new[] { 4, 2 })]
        public void Neighbors_SameRowSidesAndOtherRowSameColumn(int slot, int[] expected)
        {
            // 칸 번호 = 줄 × 3 + 열: 위 줄 0 1 2 / 아래 줄 3 4 5
            Assert.That(BattleSimulator.Neighbors(slot).ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void AdjacentAttackBonus_OnlyNeighborsGetBonus()
        {
            // 0번 칸이 이웃 공격력 +5 → 이웃 1 · 3번은 10 + 5 = 15, 이웃 아닌 2번은 10
            var a = Team((0, Dummy(AbilityTrigger.BattleStart, AbilityEffect.AdjacentAttackBonus, 5)), (1, U(10, 10)), (2, U(10, 10)), (3, U(10, 10)));
            var r = BattleSimulator.Run(Rules(), a, Team((0, Dummy())), 1, 1);
            var firstHits = r.Events.Where(e => e.Type == BattleEventType.Attack && e.Side == BattleSide.A && e.Tick == 10)
                .ToDictionary(e => e.Slot, e => e.Amount);
            Assert.That(firstHits[1], Is.EqualTo(15));
            Assert.That(firstHits[2], Is.EqualTo(10));
            Assert.That(firstHits[3], Is.EqualTo(15));
            var ability = r.Events.First(e => e.Type == BattleEventType.Ability);
            Assert.That((ability.Tick, ability.Slot, ability.Amount, ability.Value), Is.EqualTo((0, 0, 5, (int)AbilityEffect.AdjacentAttackBonus)));
        }

        [Test]
        public void AdjacentHaste_ReducesNeighborInterval_MinimumOne()
        {
            // 0번 칸이 이웃 간격 −3 → 1번: 5 − 3 = 2틱마다 / 3번: 2 − 3 → 최소 1틱마다 / 이웃 아닌 4번: 5틱마다
            var a = Team((0, Dummy(AbilityTrigger.BattleStart, AbilityEffect.AdjacentHaste, 3)), (1, U(1, 5)), (3, U(1, 2)), (4, U(1, 5)));
            var r = BattleSimulator.Run(Rules(), a, Team((0, Dummy())), 1, 1);
            int[] Ticks(int slot) => r.Events.Where(e => e.Type == BattleEventType.Attack && e.Side == BattleSide.A && e.Slot == slot).Take(3).Select(e => e.Tick).ToArray();
            Assert.That(Ticks(1), Is.EqualTo(new[] { 2, 4, 6 }));
            Assert.That(Ticks(3), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(Ticks(4), Is.EqualTo(new[] { 5, 10, 15 }));
        }

        [Test]
        public void AttackGrowth_EachAttackStrongerThanLast()
        {
            // 공격 10, 공격할 때마다 +2 (이번 공격 뒤) → 10, 12, 14, 16
            var r = BattleSimulator.Run(Rules(), Team((0, U(10, 1, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 2))), Team((0, Dummy())), 1, 1);
            var hits = r.Events.Where(e => e.Type == BattleEventType.Attack && e.Side == BattleSide.A).Take(4).Select(e => e.Amount);
            Assert.That(hits, Is.EqualTo(new[] { 10, 12, 14, 16 }));
        }

        [Test]
        public void BattleBuffs_DoNotCarryToNextBattle()
        {
            // 같은 팀으로 두 번 싸워도 첫 공격은 똑같이 10 (이번 전투 능력치는 전투마다 새로)
            var team = Team((0, U(10, 1, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 2)));
            int First() => BattleSimulator.Run(Rules(), team, Team((0, Dummy())), 1, 1).Events.First(e => e.Type == BattleEventType.Attack).Amount;
            Assert.That(First(), Is.EqualTo(10));
            Assert.That(First(), Is.EqualTo(10));
        }

        [Test]
        public void NewEffects_AreDeterministic()
        {
            var a = Team((0, U(8, 5, AbilityTrigger.BattleStart, AbilityEffect.AdjacentHaste, 1)), (1, U(16, 12, AbilityTrigger.BattleStart, AbilityEffect.AdjacentAttackBonus, 3)), (3, U(20, 10, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 2)));
            var b = Team((0, U(45, 12, AbilityTrigger.OnAttack, AbilityEffect.AttackGrowth, 5)), (4, U(9, 10, AbilityTrigger.OnAttack, AbilityEffect.Poison, 1)));
            string Run() => string.Join("|", BattleSimulator.Run(Rules(), a, b, 1, 9).Events.Select(e => e.ToString()));
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        // ───────── 상점: 골드 얻기 ─────────

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            Def("c1", Rarity.Common), Def("c2", Rarity.Common),
            Def("u1", Rarity.Uncommon), Def("r1", Rarity.Rare), Def("e1", Rarity.Epic), Def("l1", Rarity.Legendary),
            new SpeciesDef { Id = "coin", Attack = 6, AttackIntervalTicks = 5, Rarity = Rarity.Common, Ability = new AbilityDef { Trigger = AbilityTrigger.ShopRoundStart, Effect = AbilityEffect.GainGold, Amount = 1 } },
        });

        private static SpeciesDef Def(string id, Rarity rarity) => new SpeciesDef { Id = id, Attack = 10, AttackIntervalTicks = 10, Rarity = rarity };

        private static RarityOdds Odds(int from, params int[] p) => new RarityOdds { FromRound = from, Percent = p };

        private static ShopRules ShopRulesWith(params RarityOdds[] odds) => new ShopRules
        {
            StartGold = 30, FirstIncome = 35, IncomeStep = 5, ShopSlots = 5, RerollCost = 3,
            PriceByRarity = new[] { 10, 20, 30, 40, 50 }, SellPercent = 75, MergeCountByLevel = new[] { 3, 2 }, MaxLevel = 3,
            RarityOddsByRound = odds,
        };

        private static readonly RarityOdds[] Table =
        {
            Odds(1, 100, 0, 0, 0, 0),
            Odds(3, 75, 25, 0, 0, 0),
            Odds(16, 15, 25, 35, 20, 5),
        };

        [Test]
        public void GainGold_RoundStart_BoardOnly()
        {
            var shop = new ShopController(ShopRulesWith(Table), Catalog(), X2);
            var s = new RunState { RunId = "r", Seed = 1, Round = 1, Lives = 10 };
            s.Team[0] = new MonsterSnapshot { SpeciesId = "coin", Level = 1 };
            s.Bench[0] = new MonsterSnapshot { SpeciesId = "coin", Level = 1 };
            shop.BeginRound(s);
            Assert.That(s.Gold, Is.EqualTo(31), "시작 30 + 보드 짹짹이 1 (대기석은 발동 안 함)");
            s.Round = 2;
            shop.BeginRound(s);
            Assert.That(s.Gold, Is.EqualTo(67), "31 + 수입 35 + 1");
        }

        // ───────── 등급 확률표 ─────────

        [TestCase(1, 100)]
        [TestCase(2, 100)]
        [TestCase(3, 75)]
        [TestCase(15, 75)]
        [TestCase(16, 15)]
        [TestCase(99, 15)]
        public void OddsFor_PicksRowStartingAtOrBeforeRound(int round, int commonPercent)
        {
            Assert.That(ShopRulesWith(Table).OddsFor(round)[0], Is.EqualTo(commonPercent));
        }

        [Test]
        public void OddsFor_NoTable_IsNull()
        {
            Assert.That(ShopRulesWith().OddsFor(5), Is.Null);
            Assert.That(ShopRulesWith(null).OddsFor(5), Is.Null);
        }

        [Test]
        public void Validate_RejectsBadOddsTables()
        {
            Assert.Throws<InvalidOperationException>(() => ShopRulesWith(Odds(1, 99, 0, 0, 0, 0)).Validate(), "합 99");
            Assert.Throws<InvalidOperationException>(() => ShopRulesWith(Odds(2, 100, 0, 0, 0, 0)).Validate(), "첫 줄이 2라운드");
            Assert.Throws<InvalidOperationException>(() => ShopRulesWith(Odds(1, 100, 0, 0, 0, 0), Odds(1, 50, 50, 0, 0, 0)).Validate(), "라운드 오름차순 아님");
            Assert.Throws<InvalidOperationException>(() => ShopRulesWith(Odds(1, 100, 0, 0, 0)).Validate(), "등급 4칸");
            Assert.Throws<InvalidOperationException>(() => ShopRulesWith(Odds(1, 110, -10, 0, 0, 0)).Validate(), "음수");
            Assert.DoesNotThrow(() => ShopRulesWith(Table).Validate());
        }

        private static List<PoolEntry> PoolOf(SpeciesCatalog catalog, Func<SpeciesDef, int> count) =>
            catalog.All.Select(d => new PoolEntry { SpeciesId = d.Id, Count = count(d) }).ToList();

        private static Rarity RarityOf(SpeciesCatalog c, string id)
        {
            c.TryGet(id, out SpeciesDef d);
            return d.Rarity;
        }

        [Test]
        public void DrawCard_Round1_OnlyCommon_AndTakesFromPool()
        {
            var catalog = Catalog();
            var shop = new ShopController(ShopRulesWith(Table), catalog, X2);
            List<PoolEntry> entries = PoolOf(catalog, d => 10);
            var pool = new UnitPool(entries);
            var rng = new DeterministicRandom(42);
            int before = pool.Total;
            for (int i = 0; i < 25; i++)
            {
                string id = shop.DrawCard(pool, 1, rng);
                Assert.That(RarityOf(catalog, id), Is.EqualTo(Rarity.Common), id);
            }
            Assert.That(pool.Total, Is.EqualTo(before - 25), "한 장 = 풀에서 한 마리");
        }

        [Test]
        public void DrawCard_EmptyRarity_FallsBackLowerThenHigher_ThenNull()
        {
            // 희귀 100%인데 희귀가 0 → 고급(바로 아래) 2 → 일반 2 (c1 · c2 · coin 중) → 영웅 2 → 전설 0 → 없음
            var catalog = Catalog();
            var shop = new ShopController(ShopRulesWith(Odds(1, 0, 0, 100, 0, 0)), catalog, X2);
            var pool = new UnitPool(PoolOf(catalog, d => d.Id == "u1" ? 2 : d.Id == "c1" ? 2 : d.Id == "e1" ? 2 : 0));
            var rng = new DeterministicRandom(7);
            var drawn = Enumerable.Range(0, 7).Select(_ => shop.DrawCard(pool, 1, rng)).ToList();
            Assert.That(drawn, Is.EqualTo(new[] { "u1", "u1", "c1", "c1", "e1", "e1", null }));
        }

        [Test]
        public void DrawCard_NoPool_UsesOddsOverCatalog()
        {
            var catalog = Catalog();
            var shop = new ShopController(ShopRulesWith(Odds(1, 0, 0, 100, 0, 0)), catalog, X2);
            var rng = new DeterministicRandom(3);
            for (int i = 0; i < 10; i++)
                Assert.That(shop.DrawCard(null, 1, rng), Is.EqualTo("r1"), "희귀 100% → 유일한 희귀");
        }

        [Test]
        public void DrawCard_SameSeed_SameCards()
        {
            var catalog = Catalog();
            var shop = new ShopController(ShopRulesWith(Table), catalog, X2);
            string Draw20()
            {
                var pool = new UnitPool(PoolOf(catalog, d => 10));
                var rng = new DeterministicRandom(2026);
                return string.Join(",", Enumerable.Range(0, 20).Select(_ => shop.DrawCard(pool, 16, rng)));
            }
            Assert.That(Draw20(), Is.EqualTo(Draw20()));
        }

        [Test]
        public void BeginRound_Round1_ShopIsAllCommon()
        {
            var catalog = Catalog();
            var shop = new ShopController(ShopRulesWith(Table), catalog, X2);
            var pool = new UnitPool(PoolOf(catalog, d => 10));
            var s = new RunState { RunId = "r", Seed = 5, Round = 1, Lives = 10 };
            shop.BeginRound(s, pool);
            Assert.That(s.Shop.Count, Is.EqualTo(5));
            Assert.That(s.Shop.All(id => RarityOf(catalog, id) == Rarity.Common), Is.True, string.Join(",", s.Shop));
        }
    }
}
