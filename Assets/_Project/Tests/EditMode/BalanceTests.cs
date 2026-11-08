using System.Linq;
using Moa.Showdown.Game.Data;
using NUnit.Framework;
using UnityEditor;
using E = Moa.Showdown.Core.Content.ElementType;
using F = Moa.Showdown.Core.Battle.AbilityEffect;
using T = Moa.Showdown.Core.Battle.AbilityTrigger;

namespace Moa.Showdown.Tests
{
    /// <summary>데이터 에셋 값 = BALANCE.md 표. 의도해서 바꿨다면 BALANCE와 이 테스트를 같이 고친다.</summary>
    public class BalanceTests
    {
        private const string BattleBalancePath = "Assets/_Project/Data/BattleBalance.asset";

        private static BattleBalance Load()
        {
            var b = AssetDatabase.LoadAssetAtPath<BattleBalance>(BattleBalancePath);
            Assert.That(b, Is.Not.Null, "BattleBalance 에셋 없음 — 메뉴 MoaShowdown/Setup/Battle Balance 실행");
            return b;
        }

        [Test]
        public void Balance1_TicksPerSecond()
        {
            Assert.That(Load().ticksPerSecond, Is.EqualTo(10));
        }

        [Test]
        public void Balance2_TeamHpByRound()
        {
            CollectionAssert.AreEqual(new[]
            {
                300, 500, 800, 1400, 2400,
                3800, 5700, 8300, 11600, 17300,
                24900, 34700, 47300, 63000,
            }, Load().teamHpByRound);
        }

        [Test]
        public void Balance3_SuddenDeath()
        {
            var b = Load();
            Assert.That(b.suddenDeathStartTick, Is.EqualTo(600));
            Assert.That(b.suddenDeathIntervalTicks, Is.EqualTo(10));
            Assert.That(b.suddenDeathBaseDamage, Is.EqualTo(20));
            Assert.That(b.suddenDeathDamageStep, Is.EqualTo(20));
        }

        [Test]
        public void Balance4_MaxTicks()
        {
            Assert.That(Load().maxTicks, Is.EqualTo(3000));
        }

        [Test]
        public void Balance5_RunRules()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            Assert.That(r, Is.Not.Null, "RunBalance 에셋 없음 — 메뉴 MoaShowdown/Setup/Run Balance 실행");
            Assert.That(r.winsToChampion, Is.EqualTo(10));
            Assert.That(r.startLives, Is.EqualTo(10));
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 2, 2, 2, 2, 2, 3 }, r.lifeLossByRound);
            Assert.DoesNotThrow(() => r.ToRules().Validate());
        }

        [Test]
        public void Balance9_StartTeam()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            var team = r.ToStartTeam();
            Assert.That(team.Length, Is.EqualTo(6));
            Assert.That(team.Count(m => m != null), Is.EqualTo(0), "2-2부터 시작 팀 없음 — 1라운드 골드로 산다");
        }

        [Test]
        public void Balance10_Shop()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            Assert.That(r.startGold, Is.EqualTo(30));
            Assert.That(r.firstIncome, Is.EqualTo(35));
            Assert.That(r.incomeStep, Is.EqualTo(5));
            Assert.That(r.shopSlots, Is.EqualTo(5));
            Assert.That(r.rerollCost, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 40, 50 }, r.priceByRarity);
            Assert.That(r.sellPercent, Is.EqualTo(75));
            CollectionAssert.AreEqual(new[] { 3, 2 }, r.mergeCountByLevel, "Lv1 × 3 → Lv2, Lv2 × 2 → Lv3");
            Assert.That(r.maxLevel, Is.EqualTo(3));
            Assert.DoesNotThrow(() => r.ToShopRules().Validate());
            var species = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset");
            // 3-2: 일반 8 · 고급 6 · 희귀 3 · 영웅 2 · 전설 1 (BALANCE 7)
            int[] byRarity = Enumerable.Range(0, 5).Select(i => species.rows.Count(x => (int)x.rarity == i)).ToArray();
            CollectionAssert.AreEqual(new[] { 8, 6, 3, 2, 1 }, byRarity, "등급별 몬스터 수");
        }

        [Test]
        public void Balance17_RarityOdds()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            var expected = new[]
            {
                (1, new[] { 100, 0, 0, 0, 0 }),
                (3, new[] { 75, 25, 0, 0, 0 }),
                (5, new[] { 55, 30, 15, 0, 0 }),
                (7, new[] { 40, 33, 22, 5, 0 }),
                (10, new[] { 25, 35, 30, 9, 1 }),
                (13, new[] { 20, 30, 33, 14, 3 }),
                (16, new[] { 15, 25, 35, 20, 5 }),
            };
            Assert.That(r.rarityOdds.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(r.rarityOdds[i].fromRound, Is.EqualTo(expected[i].Item1));
                CollectionAssert.AreEqual(expected[i].Item2, r.rarityOdds[i].percent, $"{expected[i].Item1}라운드부터");
            }
            Assert.DoesNotThrow(() => r.ToShopRules().Validate());
        }

        [Test]
        public void Balance6_LevelMultiplier_RulesVersion()
        {
            var b = Load();
            Assert.That(b.levelAttackMultiplier, Is.EqualTo(2));
            Assert.That(b.rulesVersion, Is.EqualTo(3), "3-1 능력 추가로 1 → 2, 3-2 몬스터 20종 · 새 효과로 2 → 3");
            Assert.DoesNotThrow(() => b.ToStatRules().Validate());
        }

        [Test]
        public void Balance13_Lobby()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            Assert.That(r.playerCount, Is.EqualTo(8));
            Assert.That(r.recentOpponentMemory, Is.EqualTo(3));
            Assert.That(r.prepSeconds, Is.EqualTo(30), "준비 시간 30초 (BALANCE 15)");
            Assert.DoesNotThrow(() => r.ToLobbyRules().Validate());
        }

        [Test]
        public void Balance14_PoolSizes()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            CollectionAssert.AreEqual(new[] { 29, 22, 18, 12, 10 }, r.poolSizeByRarity, "일반 · 고급 · 희귀 · 영웅 · 전설 (TFT 값, 3-2에서 일반 임시 60 → 29)");
        }

        [Test]
        public void Balance16_PveAndCarousel()
        {
            var r = AssetDatabase.LoadAssetAtPath<RunBalance>("Assets/_Project/Data/RunBalance.asset");
            Assert.That((r.pveOpeningRounds, r.pveRepeatStart, r.pveRepeatEvery), Is.EqualTo((2, 8, 6)), "PvE 1 · 2라운드 + 8부터 6마다");
            Assert.That(r.pveLifeLoss, Is.EqualTo(1), "PvE 패배 목숨 1 고정");
            Assert.That(r.pveTeams.Count, Is.EqualTo(5));
            Assert.That(r.pveTeams.Select(t => (t.winGold, t.loseGold)), Is.EqualTo(new[] { (5, 2), (5, 2), (10, 5), (10, 5), (10, 5) }), "초반 +5 · 이후 +10, 지면 절반(내림)");
            Assert.That((r.carouselOpeningRounds, r.carouselRepeatStart, r.carouselRepeatEvery), Is.EqualTo((1, 5, 6)), "캐러셀 1 + 5부터 6마다");
            Assert.That(r.carouselPickSeconds, Is.EqualTo(10));
            Assert.DoesNotThrow(() => r.ToLobbyRules().Validate());
        }

        [Test]
        public void Balance11_DamageOverTime()
        {
            var b = Load();
            Assert.That(b.burnIntervalTicks, Is.EqualTo(10), "화상 1초마다");
            Assert.That(b.poisonIntervalTicks, Is.EqualTo(20), "독 2초마다");
        }

        [Test]
        public void Balance12_SpeciesAbilities()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset");
            void Check(string id, Moa.Showdown.Core.Content.ElementType type, Moa.Showdown.Core.Battle.AbilityTrigger trig, Moa.Showdown.Core.Battle.AbilityEffect eff, int amount, bool perLevel, int everyN)
            {
                var row = t.rows.Find(r => r.id == id);
                Assert.That(row, Is.Not.Null, id);
                Assert.That(row.type, Is.EqualTo(type), id);
                Assert.That(row.ability.trigger, Is.EqualTo(trig), id);
                Assert.That(row.ability.effect, Is.EqualTo(eff), id);
                Assert.That(row.ability.amount, Is.EqualTo(amount), id);
                Assert.That(row.ability.perLevel, Is.EqualTo(perLevel), id);
                Assert.That(row.ability.everyN, Is.EqualTo(everyN), id);
            }
            Check("sprout", Moa.Showdown.Core.Content.ElementType.Grass, Moa.Showdown.Core.Battle.AbilityTrigger.ShopRoundStart, Moa.Showdown.Core.Battle.AbilityEffect.PermAttackSelf, 1, false, 0);
            Check("ember", Moa.Showdown.Core.Content.ElementType.Fire, Moa.Showdown.Core.Battle.AbilityTrigger.OnAttack, Moa.Showdown.Core.Battle.AbilityEffect.Burn, 1, false, 0);
            Check("pebble", Moa.Showdown.Core.Content.ElementType.Rock, Moa.Showdown.Core.Battle.AbilityTrigger.BattleStart, Moa.Showdown.Core.Battle.AbilityEffect.Shield, 20, true, 0);
            Check("zap", Moa.Showdown.Core.Content.ElementType.Electric, Moa.Showdown.Core.Battle.AbilityTrigger.EveryNthAttack, Moa.Showdown.Core.Battle.AbilityEffect.DamageMultiplier, 2, false, 3);
            Check("drip", Moa.Showdown.Core.Content.ElementType.Water, Moa.Showdown.Core.Battle.AbilityTrigger.OnAttack, Moa.Showdown.Core.Battle.AbilityEffect.Heal, 3, true, 0);
            Check("gust", Moa.Showdown.Core.Content.ElementType.Flying, Moa.Showdown.Core.Battle.AbilityTrigger.OnBuy, Moa.Showdown.Core.Battle.AbilityEffect.PermAttackWeakestAlly, 2, false, 0);
            // 3-2
            Check("spore", E.Toxic, T.OnAttack, F.Poison, 1, false, 0);
            Check("chirp", E.Flying, T.ShopRoundStart, F.GainGold, 1, false, 0);
            Check("thornet", E.Bug, T.EveryNthAttack, F.Poison, 2, false, 3);
            Check("cinder", E.Fire, T.OnAttack, F.Burn, 2, false, 0);
            Check("shellby", E.Steel, T.BattleStart, F.Shield, 40, true, 0);
            Check("bubble", E.Water, T.OnAttack, F.Heal, 6, true, 0);
            Check("buzzbolt", E.Electric, T.BattleStart, F.AdjacentHaste, 1, false, 0);
            Check("drum", E.Rock, T.BattleStart, F.AdjacentAttackBonus, 3, true, 0);
            Check("wisp", E.Ghost, T.OnAttack, F.AttackGrowth, 2, false, 0);
            Check("toxifrog", E.Toxic, T.OnAttack, F.Poison, 2, false, 0);
            Check("grovekeeper", E.Grass, T.BattleStart, F.AdjacentAttackBonus, 6, true, 0);
            Check("stormwing", E.Flying, T.EveryNthAttack, F.DamageMultiplier, 3, false, 4);
            Check("magmaw", E.Fire, T.OnAttack, F.Burn, 4, false, 0);
            Check("dracon", E.Dragon, T.OnAttack, F.AttackGrowth, 5, false, 0);
            Assert.DoesNotThrow(() => t.ToCatalog());
        }

        [Test]
        public void Balance7_SpeciesTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset");
            Assert.That(t, Is.Not.Null, "SpeciesTable 에셋 없음 — 메뉴 MoaShowdown/Setup/Species Table 실행");
            // 표 순서 = 상점 · 풀 순서 (새 몬스터는 끝에만)
            var expected = new[]
            {
                ("sprout", 8, 8), ("ember", 14, 12), ("pebble", 20, 18),
                ("zap", 5, 4), ("drip", 10, 10), ("gust", 7, 6),
                ("spore", 9, 10), ("chirp", 6, 5),
                ("thornet", 12, 6), ("cinder", 18, 9), ("shellby", 22, 14), ("bubble", 14, 10), ("buzzbolt", 8, 5), ("drum", 16, 12),
                ("wisp", 20, 10), ("toxifrog", 18, 10), ("grovekeeper", 26, 14),
                ("stormwing", 14, 4), ("magmaw", 40, 15),
                ("dracon", 45, 12),
            };
            Assert.That(t.rows.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(t.rows[i].id, Is.EqualTo(expected[i].Item1));
                Assert.That(t.rows[i].attack, Is.EqualTo(expected[i].Item2), t.rows[i].id);
                Assert.That(t.rows[i].attackIntervalTicks, Is.EqualTo(expected[i].Item3), t.rows[i].id);
            }
            Assert.DoesNotThrow(() => t.ToCatalog());
        }

        [Test]
        public void Asset_ProducesValidRules()
        {
            Assert.DoesNotThrow(() => Load().ToRules().Validate());
        }
    }
}
