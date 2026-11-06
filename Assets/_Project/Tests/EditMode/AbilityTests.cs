using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>3-1 능력: 전투(방어막 · 화상 · 독 · 회복 · N번째 공격) + 상점(라운드 시작 · 살 때) + 레벨 반영. 기대값 옆에 계산식.</summary>
    public class AbilityTests
    {
        private static BattleRules Rules() => new BattleRules
        {
            TicksPerSecond = 10,
            TeamHpByRound = new[] { 100 },
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
        private static BattleUnit Dummy(AbilityTrigger t = AbilityTrigger.None, AbilityEffect e = AbilityEffect.None, int amount = 0, int everyN = 0) =>
            U(0, 5000, t, e, amount, everyN);

        private static BattleTeam Team(params BattleUnit[] units)
        {
            var team = new BattleTeam();
            for (int i = 0; i < units.Length; i++) team[i] = units[i];
            return team;
        }

        [Test]
        public void Shield_AbsorbsBeforeHp()
        {
            // B 방어막 25 + 체력 100 = 125, A는 매 틱 10 → 1~2틱 방어막, 3틱 방어막 5 + 체력 5 → 체력 95 → 95 − 10 × 10 ≤ 0 → 13틱
            var r = BattleSimulator.Run(Rules(), Team(U(10, 1)), Team(Dummy(AbilityTrigger.BattleStart, AbilityEffect.Shield, 25)), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.AWins));
            Assert.That(r.EndTick, Is.EqualTo(13));
            var start = r.Events.First(e => e.Type == BattleEventType.ShieldChanged);
            Assert.That((start.Tick, start.Side, start.Value), Is.EqualTo((0, BattleSide.B, 25)));
            var firstHp = r.Events.First(e => e.Type == BattleEventType.TeamHpChanged && e.Side == BattleSide.B);
            Assert.That((firstHp.Tick, firstHp.Value), Is.EqualTo((3, 95)));
        }

        [Test]
        public void Shield_AlsoBlocksSuddenDeath()
        {
            // 서든데스 누적 5 · 10 · 15 · 20 · 25 · 30 = 105 (1050틱) → A(방어막 없음) 0, B는 방어막 30이 먼저 받아 체력 100 − 75 = 25
            var r = BattleSimulator.Run(Rules(), new BattleTeam(), Team(Dummy(AbilityTrigger.BattleStart, AbilityEffect.Shield, 30)), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.BWins));
            Assert.That(r.EndTick, Is.EqualTo(1050));
            Assert.That(r.HpB, Is.EqualTo(25));
        }

        [Test]
        public void Burn_DamagesEverySecond_ThenDecreasesByOne()
        {
            // 전투 시작 화상 5 → 10틱 5, 20틱 4, 30틱 3, 40틱 2, 50틱 1 = 15 → 0
            var r = BattleSimulator.Run(Rules(), Team(Dummy(AbilityTrigger.BattleStart, AbilityEffect.Burn, 5)), Team(Dummy()), 1, 1);
            var dots = r.Events.Where(e => e.Type == BattleEventType.DamageOverTime && e.Side == BattleSide.B && e.Value == 0 && e.Tick <= 100).ToList();
            Assert.That(dots.Select(e => (e.Tick, e.Amount)), Is.EqualTo(new[] { (10, 5), (20, 4), (30, 3), (40, 2), (50, 1) }));
            var hpAfter = r.Events.Last(e => e.Type == BattleEventType.TeamHpChanged && e.Side == BattleSide.B && e.Tick <= 100);
            Assert.That(hpAfter.Value, Is.EqualTo(85));
        }

        [Test]
        public void Poison_DamagesEveryTwoSeconds_DoesNotDecrease()
        {
            var r = BattleSimulator.Run(Rules(), Team(Dummy(AbilityTrigger.BattleStart, AbilityEffect.Poison, 4)), Team(Dummy()), 1, 1);
            var dots = r.Events.Where(e => e.Type == BattleEventType.DamageOverTime && e.Side == BattleSide.B && e.Value == 1).Take(3).ToList();
            Assert.That(dots.Select(e => (e.Tick, e.Amount)), Is.EqualTo(new[] { (20, 4), (40, 4), (60, 4) }));
            // 독 4 × 25번(500틱) = 100 → 500틱에 B 체력 0
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.AWins));
            Assert.That(r.EndTick, Is.EqualTo(500));
        }

        [Test]
        public void OnAttackBurn_StacksEachAttack()
        {
            // 화상 1을 매 틱 → 10틱: 쌓인 10 피해 후 9 → 20틱: 9 + 새로 10 = 19 피해 후 18
            var r = BattleSimulator.Run(Rules(), Team(U(0, 1, AbilityTrigger.OnAttack, AbilityEffect.Burn, 1)), Team(Dummy()), 1, 1);
            var dots = r.Events.Where(e => e.Type == BattleEventType.DamageOverTime).Take(2).Select(e => (e.Tick, e.Amount));
            Assert.That(dots, Is.EqualTo(new[] { (10, 10), (20, 19) }));
        }

        [Test]
        public void Heal_AfterDamage_CappedAtStartHp()
        {
            // B는 5틱마다 회복 3: 5틱은 체력이 가득 → 회복 없음. 10틱: A 피해 10 → 90 → 회복 3 → 93
            var r = BattleSimulator.Run(Rules(), Team(U(10, 10)), Team(U(0, 5, AbilityTrigger.OnAttack, AbilityEffect.Heal, 3)), 1, 1);
            Assert.That(r.Events.Any(e => e.Type == BattleEventType.Heal && e.Tick == 5), Is.False);
            var heal = r.Events.First(e => e.Type == BattleEventType.Heal);
            Assert.That((heal.Tick, heal.Amount, heal.Value), Is.EqualTo((10, 3, 93)));
        }

        [Test]
        public void EveryNthAttack_DoublesThirdHit()
        {
            var r = BattleSimulator.Run(Rules(), Team(U(10, 1, AbilityTrigger.EveryNthAttack, AbilityEffect.DamageMultiplier, 2, 3)), Team(Dummy()), 1, 1);
            var hits = r.Events.Where(e => e.Type == BattleEventType.Attack && e.Side == BattleSide.A).Take(6).Select(e => e.Amount);
            Assert.That(hits, Is.EqualTo(new[] { 10, 10, 20, 10, 10, 20 }));
        }

        [Test]
        public void Abilities_AreDeterministic()
        {
            var a = Team(U(7, 3, AbilityTrigger.OnAttack, AbilityEffect.Burn, 1), U(5, 4, AbilityTrigger.EveryNthAttack, AbilityEffect.DamageMultiplier, 2, 3));
            var b = Team(U(9, 5, AbilityTrigger.OnAttack, AbilityEffect.Heal, 2), Dummy(AbilityTrigger.BattleStart, AbilityEffect.Shield, 40), U(4, 2, AbilityTrigger.OnAttack, AbilityEffect.Poison, 1));
            string Run() => string.Join("|", BattleSimulator.Run(Rules(), a, b, 1, 9).Events.Select(e => e.ToString()));
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        // ───────── 몬스터 표 → 전투 능력 (레벨 반영) ─────────

        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "rock", Attack = 20, AttackIntervalTicks = 18, Ability = new AbilityDef { Trigger = AbilityTrigger.BattleStart, Effect = AbilityEffect.Shield, Amount = 20, PerLevel = true } },
            new SpeciesDef { Id = "grow", Attack = 5, AttackIntervalTicks = 8, Ability = new AbilityDef { Trigger = AbilityTrigger.ShopRoundStart, Effect = AbilityEffect.PermAttackSelf, Amount = 1 } },
            new SpeciesDef { Id = "wind", Attack = 7, AttackIntervalTicks = 6, Ability = new AbilityDef { Trigger = AbilityTrigger.OnBuy, Effect = AbilityEffect.PermAttackWeakestAlly, Amount = 2 } },
            new SpeciesDef { Id = "big", Attack = 50, AttackIntervalTicks = 10 },
        });

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        [TestCase(1, 20)]
        [TestCase(2, 40)]
        [TestCase(3, 60)]
        public void PerLevelAmount_ScalesWithLevel(int level, int shield)
        {
            BattleUnit u = TeamFactory.BuildUnit(new MonsterSnapshot { SpeciesId = "rock", Level = level }, Catalog(), X2);
            Assert.That(u.Ability.Effect, Is.EqualTo(AbilityEffect.Shield));
            Assert.That(u.Ability.Amount, Is.EqualTo(shield), "20 × 레벨");
        }

        private static ShopController Shop() => new ShopController(new ShopRules
        {
            StartGold = 30,
            FirstIncome = 35,
            IncomeStep = 5,
            ShopSlots = 5,
            RerollCost = 3,
            PriceByRarity = new[] { 10, 20, 30, 40, 50 },
            SellPercent = 75,
            MergeCountByLevel = new[] { 3, 2 },
            MaxLevel = 3,
        }, Catalog(), X2);

        private static RunState State() => new RunState { RunId = "r", Seed = 1, Round = 1, Lives = 10 };

        [Test]
        public void RoundStart_BuffsSelf_OnlyOnBoard_OncePerRound()
        {
            var shop = Shop();
            RunState s = State();
            s.Team[0] = new MonsterSnapshot { SpeciesId = "grow", Level = 1 };
            s.Bench[0] = new MonsterSnapshot { SpeciesId = "grow", Level = 1 };
            shop.BeginRound(s);
            shop.BeginRound(s);
            Assert.That(s.Team[0].Buffs.Sum(b => b.Value), Is.EqualTo(1), "보드 · 한 번만");
            Assert.That(s.Bench[0].Buffs, Is.Empty, "대기석은 발동 안 함");
            s.Round = 2;
            shop.BeginRound(s);
            Assert.That(s.Team[0].Buffs.Sum(b => b.Value), Is.EqualTo(2));
        }

        [Test]
        public void OnBuy_BuffsWeakestOtherAlly()
        {
            var shop = Shop();
            RunState s = State();
            s.Gold = 100;
            s.Team[0] = new MonsterSnapshot { SpeciesId = "big", Level = 1 };   // 공격 50
            s.Bench[0] = new MonsterSnapshot { SpeciesId = "grow", Level = 1 }; // 공격 5 ← 가장 약함
            s.Shop.Add("wind");
            Assert.That(shop.Buy(s, 0), Is.EqualTo(ShopError.None));
            Assert.That(s.Bench[0].Buffs.Sum(b => b.Value), Is.EqualTo(2));
            Assert.That(s.Team[0].Buffs, Is.Empty);
            Assert.That(s.Bench[1].SpeciesId, Is.EqualTo("wind"));
            Assert.That(s.Bench[1].Buffs, Is.Empty, "자기 자신은 대상 아님");
        }

        [Test]
        public void OnBuy_NoOtherAlly_DoesNothing()
        {
            var shop = Shop();
            RunState s = State();
            s.Gold = 100;
            s.Shop.Add("wind");
            Assert.That(shop.Buy(s, 0), Is.EqualTo(ShopError.None));
            Assert.That(s.Bench[0].Buffs, Is.Empty);
        }

        [Test]
        public void OnBuy_WithFullBenchMerge_BuffGoesToOtherThenKeeperInherits()
        {
            // 대기석 [wind, wind, big, big] 가득 → wind 사면 바로 합체. 살 때 능력: 합쳐질 자리(대기석 0) 제외 가장 약한 = 대기석 1 wind(7) → +2 → 합체로 keeper가 물려받음
            var shop = Shop();
            RunState s = State();
            s.Gold = 100;
            s.Bench[0] = new MonsterSnapshot { SpeciesId = "wind", Level = 1 };
            s.Bench[1] = new MonsterSnapshot { SpeciesId = "wind", Level = 1 };
            s.Bench[2] = new MonsterSnapshot { SpeciesId = "big", Level = 1 };
            s.Bench[3] = new MonsterSnapshot { SpeciesId = "big", Level = 1 };
            s.Shop.Add("wind");
            Assert.That(shop.Buy(s, 0), Is.EqualTo(ShopError.None));
            Assert.That(s.Bench[0].Level, Is.EqualTo(2));
            Assert.That(s.Bench[0].Buffs.Sum(b => b.Value), Is.EqualTo(2));
            Assert.That(s.Bench[2].Buffs, Is.Empty);
        }
    }
}
