using System.Linq;
using Moa.Showdown.Game.Data;
using NUnit.Framework;
using UnityEditor;

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
            Assert.That(team[0].SpeciesId, Is.EqualTo("sprout"));
            Assert.That(team[3].SpeciesId, Is.EqualTo("ember"));
            Assert.That(team.Count(m => m != null), Is.EqualTo(2));
            var species = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset").ToCatalog();
            foreach (var m in team)
                if (m != null) Assert.That(species.TryGet(m.SpeciesId, out _), Is.True, m.SpeciesId);
        }

        [Test]
        public void Balance6_LevelMultiplier_RulesVersion()
        {
            var b = Load();
            Assert.That(b.levelAttackMultiplier, Is.EqualTo(2));
            Assert.That(b.rulesVersion, Is.EqualTo(1));
            Assert.DoesNotThrow(() => b.ToStatRules().Validate());
        }

        [Test]
        public void Balance7_SpeciesTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<SpeciesTable>("Assets/_Project/Data/SpeciesTable.asset");
            Assert.That(t, Is.Not.Null, "SpeciesTable 에셋 없음 — 메뉴 MoaShowdown/Setup/Species Table 실행");
            var expected = new[]
            {
                ("sprout", 8, 8), ("ember", 14, 12), ("pebble", 20, 18),
                ("zap", 5, 4), ("drip", 10, 10), ("gust", 7, 6),
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
