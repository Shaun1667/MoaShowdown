using Moa.Showdown.Core.Random;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void KnownFirstValue_SplitMix64Seed0()
        {
            // 알고리즘이 바뀌면 옛 시드의 결과(저장 · 상대 스냅샷 재계산)가 달라진다 — 이 값이 바뀌면 안 된다.
            var rng = new DeterministicRandom(0);
            Assert.That(rng.NextULong(), Is.EqualTo(0xE220A8397B1DCDAFUL));
        }

        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new DeterministicRandom(987654321);
            var b = new DeterministicRandom(987654321);
            for (int i = 0; i < 100; i++) Assert.That(a.NextInt(1000), Is.EqualTo(b.NextInt(1000)));
        }

        [Test]
        public void State_SaveAndRestore_ContinuesSameSequence()
        {
            var a = new DeterministicRandom(42);
            a.NextULong();
            ulong saved = a.State;
            ulong expected = a.NextULong();
            var b = new DeterministicRandom(0) { State = saved };
            Assert.That(b.NextULong(), Is.EqualTo(expected));
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            var rng = new DeterministicRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(3, 10);
                Assert.That(v, Is.InRange(3, 9));
            }
        }
    }
}
