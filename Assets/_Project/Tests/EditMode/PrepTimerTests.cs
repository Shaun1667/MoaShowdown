using Moa.Showdown.Core.Lobby;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>L-3 준비 시간: 시간 끝 · 전원 준비 완료로 시작, 봇 · 탈락자는 처음부터 준비.</summary>
    public class PrepTimerTests
    {
        [Test]
        public void CountsDown_StartsWhenTimeRunsOut()
        {
            var t = new PrepTimer();
            t.Start(8, 30, new[] { 1, 2, 3, 4, 5, 6, 7 });
            Assert.That(t.DisplaySeconds, Is.EqualTo(30));
            t.Advance(10.2);
            Assert.That(t.DisplaySeconds, Is.EqualTo(20), "19.8초 남음 → 올림 20");
            Assert.That(t.ShouldStart, Is.False);
            t.Advance(25);
            Assert.That(t.RemainingSeconds, Is.EqualTo(0));
            Assert.That(t.ShouldStart, Is.True, "시간 끝");
        }

        [Test]
        public void AllReady_StartsEarly_BotsAreAutoReady()
        {
            var t = new PrepTimer();
            t.Start(8, 30, new[] { 1, 2, 3, 4, 5, 6, 7 });
            Assert.That(t.IsReady(3), Is.True, "봇");
            Assert.That(t.AllReady, Is.False, "나(0번)는 아직");
            t.SetReady(0);
            Assert.That(t.ShouldStart, Is.True);
        }

        [Test]
        public void TwoHumans_WaitForBoth()
        {
            var t = new PrepTimer();
            t.Start(4, 30, new[] { 2, 3 });
            t.SetReady(0);
            Assert.That(t.ShouldStart, Is.False, "1번 사람이 아직");
            t.SetReady(1);
            t.SetReady(1);
            Assert.That(t.ShouldStart, Is.True);
        }

        [Test]
        public void Stopped_DoesNotStart_OrTick()
        {
            var t = new PrepTimer();
            t.Start(2, 5, new int[0]);
            t.Stop();
            t.Advance(10);
            Assert.That(t.RemainingSeconds, Is.EqualTo(5), "멈추면 시간이 안 흐름");
            Assert.That(t.ShouldStart, Is.False);
            t.Start(2, 5, new int[0]);
            Assert.That(t.IsReady(0), Is.False, "새로 시작하면 준비 상태 초기화");
            t.Advance(-3);
            Assert.That(t.RemainingSeconds, Is.EqualTo(5), "음수는 무시");
        }
    }
}
