using System;
using System.Collections.Generic;

namespace Moa.Showdown.Core.Content
{
    /// <summary>스냅샷 버프 · 능력치 ID (문자열 — 스냅샷에 그대로 저장됨, 바꾸면 옛 스냅샷이 깨진다).</summary>
    public static class StatIds
    {
        public const string Attack = "attack";
        public const string Interval = "interval";
    }

    /// <summary>몬스터 한 종의 기본 능력치 (표 한 줄).</summary>
    public sealed class SpeciesDef
    {
        public string Id;
        public string DisplayName;
        public int Attack;
        public int AttackIntervalTicks;
    }

    /// <summary>몬스터 표 조회 한 곳. ID로만 찾는다 (순회 순서에 의존하는 규칙 금지).</summary>
    public sealed class SpeciesCatalog
    {
        private readonly Dictionary<string, SpeciesDef> _byId = new Dictionary<string, SpeciesDef>(StringComparer.Ordinal);

        public SpeciesCatalog(IEnumerable<SpeciesDef> defs)
        {
            foreach (SpeciesDef d in defs)
            {
                if (d == null || string.IsNullOrEmpty(d.Id)) throw new ArgumentException("빈 몬스터 ID");
                if (d.Attack < 0) throw new ArgumentException($"{d.Id}: 공격력 < 0");
                if (d.AttackIntervalTicks < 1) throw new ArgumentException($"{d.Id}: 공격 간격 < 1");
                if (_byId.ContainsKey(d.Id)) throw new ArgumentException($"몬스터 ID 중복: {d.Id}");
                _byId.Add(d.Id, d);
            }
        }

        public int Count => _byId.Count;

        public bool TryGet(string id, out SpeciesDef def)
        {
            def = null;
            return id != null && _byId.TryGetValue(id, out def);
        }
    }

    /// <summary>능력치 계산 수치 (BALANCE 6). 값은 데이터 에셋에서.</summary>
    public sealed class UnitStatRules
    {
        /// <summary>레벨이 1 오를 때마다 공격력에 곱하는 값 (레벨 1 = 기본).</summary>
        public int LevelAttackMultiplier;

        public void Validate()
        {
            if (LevelAttackMultiplier < 1) throw new InvalidOperationException("LevelAttackMultiplier < 1");
        }
    }
}
