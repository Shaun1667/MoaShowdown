using System.Linq;
using Moa.Showdown.Core.Battle;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>전투 규칙 검사. 수치는 이 테스트 전용 (실제 수치는 BalanceTests).</summary>
    public class BattleSimulatorTests
    {
        private static BattleRules Rules(int maxTicks = 2000) => new BattleRules
        {
            TicksPerSecond = 10,
            TeamHpByRound = new[] { 100, 200 },
            SuddenDeathStartTick = 1000,
            SuddenDeathIntervalTicks = 10,
            SuddenDeathBaseDamage = 5,
            SuddenDeathDamageStep = 5,
            MaxTicks = maxTicks,
        };

        private static BattleTeam Team(params (int slot, int attack, int interval)[] units)
        {
            var t = new BattleTeam();
            foreach (var u in units) t[u.slot] = new BattleUnit(u.attack, u.interval);
            return t;
        }

        [Test]
        public void OneSidedAttacker_WinsWhenTeamHpReachesZero()
        {
            // 10 피해 × 5틱마다 → 100 체력은 10번째 공격(50틱)에 0
            var r = BattleSimulator.Run(Rules(), Team((0, 10, 5)), new BattleTeam(), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.AWins));
            Assert.That(r.EndTick, Is.EqualTo(50));
            Assert.That(r.HpA, Is.EqualTo(100));
            Assert.That(r.HpB, Is.EqualTo(0));
        }

        [Test]
        public void FasterTeam_Wins_AndDamageIsShared()
        {
            // A: 4틱마다 10 → 40틱에 B 0. B: 5틱마다 10 → 40틱까지 8번 = 80 → A 20 남음
            var r = BattleSimulator.Run(Rules(), Team((0, 10, 4)), Team((3, 10, 5)), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.AWins));
            Assert.That(r.EndTick, Is.EqualTo(40));
            Assert.That(r.HpA, Is.EqualTo(20));
        }

        [Test]
        public void MirrorTeams_SameTickZero_IsDraw()
        {
            var a = Team((0, 10, 5), (4, 7, 3));
            var b = Team((0, 10, 5), (4, 7, 3));
            var r = BattleSimulator.Run(Rules(), a, b, 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(r.HpA, Is.EqualTo(0));
            Assert.That(r.HpB, Is.EqualTo(0));
        }

        [Test]
        public void EmptyTeams_EndBySuddenDeath()
        {
            // 서든데스 1000틱부터 10틱마다 5, 10, 15, 20, 25, 30 → 누적 105 ≥ 100 → 1050틱
            var r = BattleSimulator.Run(Rules(), new BattleTeam(), new BattleTeam(), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(r.EndTick, Is.EqualTo(1050));
            Assert.That(r.Events.Count(e => e.Type == BattleEventType.SuddenDeath), Is.EqualTo(6));
        }

        [Test]
        public void MaxTicks_EndsInDraw()
        {
            // 1000틱 5 + 1010틱 10 = 15 → 85 남고 1010틱에서 상한
            var r = BattleSimulator.Run(Rules(maxTicks: 1010), new BattleTeam(), new BattleTeam(), 1, 1);
            Assert.That(r.Outcome, Is.EqualTo(BattleOutcome.Draw));
            Assert.That(r.EndTick, Is.EqualTo(1010));
            Assert.That(r.HpA, Is.EqualTo(85));
            Assert.That(r.HpB, Is.EqualTo(85));
        }

        [Test]
        public void TeamHp_UsesRoundTable_LastValueAfterEnd()
        {
            var rules = Rules();
            Assert.That(rules.GetTeamHp(1), Is.EqualTo(100));
            Assert.That(rules.GetTeamHp(2), Is.EqualTo(200));
            Assert.That(rules.GetTeamHp(5), Is.EqualTo(200));
            var r = BattleSimulator.Run(rules, Team((0, 10, 5)), new BattleTeam(), 2, 1);
            Assert.That(r.StartHp, Is.EqualTo(200));
        }

        [Test]
        public void SameInputs_SameEventLog()
        {
            var a = Team((0, 13, 4), (2, 9, 3), (5, 21, 7));
            var b = Team((1, 11, 5), (3, 17, 6), (4, 5, 2));
            var r1 = BattleSimulator.Run(Rules(), a, b, 2, 12345);
            var r2 = BattleSimulator.Run(Rules(), a, b, 2, 12345);
            CollectionAssert.AreEqual(r1.Events.Select(e => e.ToString()), r2.Events.Select(e => e.ToString()));
            Assert.That(r1.Outcome, Is.EqualTo(r2.Outcome));
        }

        [Test]
        public void EventLog_EndsWithEnd_AndHpEventsMatchResult()
        {
            var r = BattleSimulator.Run(Rules(), Team((0, 10, 4)), Team((3, 10, 5)), 1, 1);
            var last = r.Events[r.Events.Count - 1];
            Assert.That(last.Type, Is.EqualTo(BattleEventType.End));
            Assert.That(last.Value, Is.EqualTo((int)r.Outcome));
            var lastHpB = r.Events.Last(e => e.Type == BattleEventType.TeamHpChanged && e.Side == BattleSide.B);
            Assert.That(lastHpB.Value, Is.EqualTo(r.HpB));
        }

        [Test]
        public void SlotOf_RowMajor()
        {
            Assert.That(BattleTeam.SlotOf(0, 0), Is.EqualTo(0));
            Assert.That(BattleTeam.SlotOf(0, 2), Is.EqualTo(2));
            Assert.That(BattleTeam.SlotOf(1, 0), Is.EqualTo(3));
            Assert.That(BattleTeam.SlotOf(1, 2), Is.EqualTo(5));
        }
    }
}
