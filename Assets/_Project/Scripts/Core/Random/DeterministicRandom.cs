using System;

namespace Moa.Showdown.Core.Random
{
    /// <summary>
    /// 시드 난수 (SplitMix64). 같은 시드 = 같은 수열, PC · 서버가 달라도 같다.
    /// 용도별로 인스턴스를 따로 만든다 (상점 ≠ 전투 ≠ 이벤트 ≠ 연출). <see cref="State"/>로 저장 · 복원.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong State
        {
            get => _state;
            set => _state = value;
        }

        public ulong NextULong()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>0 이상 maxExclusive 미만. 치우침 없이 (거부 샘플링).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            ulong bound = (ulong)maxExclusive;
            ulong limit = ulong.MaxValue - ulong.MaxValue % bound;
            ulong r;
            do { r = NextULong(); } while (r >= limit);
            return (int)(r % bound);
        }

        /// <summary>minInclusive 이상 maxExclusive 미만.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }
    }
}
