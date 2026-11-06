using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Random;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Shop
{
    /// <summary>공유 풀의 한 줄 (몬스터 종류 · 남은 수).</summary>
    public sealed class PoolEntry
    {
        [JsonProperty("speciesId", Order = 0)]
        public string SpeciesId;

        [JsonProperty("count", Order = 1)]
        public int Count;
    }

    /// <summary>
    /// 공유 몬스터 풀 (L-2, TFT 방식). 상태는 로비 스냅샷의 목록(<see cref="PoolEntry"/>)에 그대로 있고, 이 클래스는 그 목록을 다루기만 한다.
    /// 뽑기 = 남은 수에 비례 (목록 순서 = 몬스터 표 순서로 고정 → 같은 시드 = 같은 결과). 0이면 안 나옴.
    /// </summary>
    public sealed class UnitPool
    {
        private readonly List<PoolEntry> _entries;

        public UnitPool(List<PoolEntry> entries)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public int Total
        {
            get
            {
                int n = 0;
                foreach (PoolEntry e in _entries) n += e.Count;
                return n;
            }
        }

        public int CountOf(string speciesId)
        {
            PoolEntry e = Find(speciesId);
            return e?.Count ?? 0;
        }

        /// <summary>남은 수에 비례해 하나 뽑고 풀에서 뺀다. 풀이 비었으면 null.</summary>
        public string Draw(DeterministicRandom rng)
        {
            int total = Total;
            if (total <= 0) return null;
            int r = rng.NextInt(total);
            foreach (PoolEntry e in _entries)
            {
                if (r < e.Count)
                {
                    e.Count--;
                    return e.SpeciesId;
                }
                r -= e.Count;
            }
            throw new InvalidOperationException("풀 뽑기 실패"); // r < total이므로 오지 않음
        }

        /// <summary>풀로 되돌린다 (안 산 상점 카드 · 판 몬스터 · 탈락자 몬스터). 모르는 몬스터면 예외.</summary>
        public void Return(string speciesId, int count)
        {
            if (speciesId == null || count <= 0) return;
            PoolEntry e = Find(speciesId) ?? throw new ArgumentException($"풀에 없는 몬스터 '{speciesId}'");
            e.Count = (int)Math.Min((long)e.Count + count, int.MaxValue);
        }

        private PoolEntry Find(string speciesId)
        {
            foreach (PoolEntry e in _entries)
                if (e.SpeciesId == speciesId) return e;
            return null;
        }
    }
}
