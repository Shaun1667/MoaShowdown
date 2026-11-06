using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class BattlePlaybackTests
    {
        private static BattleResult SampleResult()
        {
            var rules = new BattleRules
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
            var a = new BattleTeam { [0] = new BattleUnit(10, 4) };
            var b = new BattleTeam { [3] = new BattleUnit(10, 5) };
            return BattleSimulator.Run(rules, a, b, 1, 1); // A 승, 40틱
        }

        [Test]
        public void Advance_EmitsEventsUpToCurrentTick_InOrder()
        {
            BattleResult r = SampleResult();
            var p = new BattlePlayback(r, 10);
            var got = new List<BattleEvent>();

            p.Advance(1.05f, 1f, got.Add); // 10.5틱 (틱 경계의 float 오차를 피함)
            Assert.That(got.Select(e => e.ToString()), Is.EqualTo(r.Events.Where(e => e.Tick <= 10).Select(e => e.ToString())));
            Assert.That(p.IsFinished, Is.False);
        }

        [Test]
        public void Speed_ScalesTicks()
        {
            BattleResult r = SampleResult();
            var p = new BattlePlayback(r, 10);
            var got = new List<BattleEvent>();
            p.Advance(1.05f, 2f, got.Add); // 21틱
            Assert.That(got.Count, Is.EqualTo(r.Events.Count(e => e.Tick <= 21)));
            Assert.That(p.CurrentTick, Is.EqualTo(21f).Within(0.01f));
        }

        [Test]
        public void LongAdvance_EmitsAll_AndStopsAtEndTick()
        {
            BattleResult r = SampleResult();
            var p = new BattlePlayback(r, 10);
            var got = new List<BattleEvent>();
            p.Advance(100f, 4f, got.Add);
            Assert.That(p.IsFinished, Is.True);
            Assert.That(got.Count, Is.EqualTo(r.Events.Count));
            Assert.That(got.Last().Type, Is.EqualTo(BattleEventType.End));
            Assert.That(p.CurrentTick, Is.EqualTo((float)r.EndTick));
            p.Advance(1f, 1f, got.Add);
            Assert.That(got.Count, Is.EqualTo(r.Events.Count), "끝난 뒤에는 더 내보내지 않음");
        }

        [Test]
        public void DebugTeam_ToBattleTeam_OnlyPresentSlots()
        {
            var t = new DebugTeam { name = "t" };
            for (int i = 0; i < t.slots.Length; i++) t.slots[i] = new DebugUnit();
            t.slots[2] = new DebugUnit { present = true, attack = 7, attackIntervalTicks = 3 };
            t.slots[4] = new DebugUnit { present = false, attack = 9, attackIntervalTicks = 9 };
            BattleTeam bt = t.ToBattleTeam();
            Assert.That(bt[2].Attack, Is.EqualTo(7));
            Assert.That(bt[2].AttackIntervalTicks, Is.EqualTo(3));
            Assert.That(bt[4].IsEmpty, Is.True);
            Assert.That(bt[0].IsEmpty, Is.True);
        }
    }
}
