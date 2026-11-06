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
        public Rarity Rarity;
        public ElementType Type;
        /// <summary>고유 능력 (없으면 Trigger None).</summary>
        public AbilityDef Ability;
    }

    /// <summary>몬스터 속성 (시너지 조건용). 끝에만 추가한다.</summary>
    public enum ElementType
    {
        None = 0,
        Grass = 1,
        Fire = 2,
        Water = 3,
        Electric = 4,
        Rock = 5,
        Flying = 6,
        Toxic = 7,
    }

    /// <summary>몬스터 표의 능력 한 줄: 언제 × 무엇 × 수치 (BALANCE 12).</summary>
    public struct AbilityDef
    {
        public Moa.Showdown.Core.Battle.AbilityTrigger Trigger;
        public Moa.Showdown.Core.Battle.AbilityEffect Effect;
        public int Amount;
        /// <summary>켜면 수치 × 레벨.</summary>
        public bool PerLevel;
        /// <summary>EveryNthAttack일 때 N.</summary>
        public int EveryN;

        public bool IsNone => Trigger == Moa.Showdown.Core.Battle.AbilityTrigger.None || Effect == Moa.Showdown.Core.Battle.AbilityEffect.None;

        /// <summary>레벨까지 반영한 수치.</summary>
        public int AmountAt(int level) => PerLevel ? (int)Math.Min((long)Amount * Math.Max(1, level), int.MaxValue) : Amount;
    }

    /// <summary>몬스터 등급 (상점 가격). 끝에만 추가한다.</summary>
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    /// <summary>몬스터 표 조회 한 곳. 규칙은 ID로 찾는다. <see cref="All"/>은 표 순서 그대로 (상점 뽑기용 — 표 순서를 바꾸면 같은 시드의 상점이 달라짐).</summary>
    public sealed class SpeciesCatalog
    {
        private readonly Dictionary<string, SpeciesDef> _byId = new Dictionary<string, SpeciesDef>(StringComparer.Ordinal);
        private readonly List<SpeciesDef> _all = new List<SpeciesDef>();

        public SpeciesCatalog(IEnumerable<SpeciesDef> defs)
        {
            foreach (SpeciesDef d in defs)
            {
                if (d == null || string.IsNullOrEmpty(d.Id)) throw new ArgumentException("빈 몬스터 ID");
                if (d.Attack < 0) throw new ArgumentException($"{d.Id}: 공격력 < 0");
                if (d.AttackIntervalTicks < 1) throw new ArgumentException($"{d.Id}: 공격 간격 < 1");
                if (!d.Ability.IsNone)
                {
                    if (d.Ability.Trigger == Moa.Showdown.Core.Battle.AbilityTrigger.EveryNthAttack && d.Ability.EveryN < 1) throw new ArgumentException($"{d.Id}: EveryN < 1");
                    if (d.Ability.Amount < 0) throw new ArgumentException($"{d.Id}: 능력 수치 < 0");
                }
                if (_byId.ContainsKey(d.Id)) throw new ArgumentException($"몬스터 ID 중복: {d.Id}");
                _byId.Add(d.Id, d);
                _all.Add(d);
            }
        }

        public int Count => _byId.Count;

        public IReadOnlyList<SpeciesDef> All => _all;

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
