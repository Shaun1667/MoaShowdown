using System;

namespace Moa.Showdown.Core.Battle
{
    /// <summary>
    /// 전투에 들어가는 몬스터 하나의 능력치. <see cref="AttackIntervalTicks"/>가 0이면 빈칸.
    /// </summary>
    public readonly struct BattleUnit
    {
        public readonly int Attack;
        public readonly int AttackIntervalTicks;
        /// <summary>전투 능력 (없으면 IsNone). 상점 단계 능력은 전투에서 무시한다.</summary>
        public readonly BattleAbility Ability;

        public BattleUnit(int attack, int attackIntervalTicks) : this(attack, attackIntervalTicks, default)
        {
        }

        public BattleUnit(int attack, int attackIntervalTicks, BattleAbility ability)
        {
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (attackIntervalTicks < 1) throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks));
            Attack = attack;
            AttackIntervalTicks = attackIntervalTicks;
            Ability = ability;
        }

        public bool IsEmpty => AttackIntervalTicks == 0;

        public static BattleUnit Empty => default;
    }

    /// <summary>
    /// 3×2 보드 한 쪽. 칸 번호 = 줄 × 3 + 열. 줄 = 화면의 위(0) · 아래(1) 줄, 열 = 가운데에서 바깥으로 0 · 1 · 2 (원작 화면, 2026-11-06).
    /// </summary>
    public sealed class BattleTeam
    {
        public const int Columns = 3;
        public const int Rows = 2;
        public const int SlotCount = Columns * Rows;

        private readonly BattleUnit[] _slots = new BattleUnit[SlotCount];

        public BattleUnit this[int slot]
        {
            get => _slots[slot];
            set => _slots[slot] = value;
        }

        public static int SlotOf(int row, int column)
        {
            if (row < 0 || row >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
            if (column < 0 || column >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
            return row * Columns + column;
        }
    }
}
