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
                300, 500, 800, 1100, 1400,
                1800, 2200, 2600, 3000, 3400,
                3800, 4200, 4600, 5000, 5400,
                5800, 6200, 6600, 7000, 7400,
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
        public void Asset_ProducesValidRules()
        {
            Assert.DoesNotThrow(() => Load().ToRules().Validate());
        }
    }
}
