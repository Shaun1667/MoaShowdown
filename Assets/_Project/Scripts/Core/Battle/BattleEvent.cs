namespace Moa.Showdown.Core.Battle
{
    public enum BattleSide
    {
        A = 0,
        B = 1,
    }

    public enum BattleOutcome
    {
        AWins = 0,
        BWins = 1,
        Draw = 2,
    }

    /// <summary>끝에만 추가한다.</summary>
    public enum BattleEventType
    {
        /// <summary>Side의 Slot 몬스터가 공격. Amount = 피해.</summary>
        Attack = 0,
        /// <summary>서든데스 피해 (양쪽). Amount = 피해, Value = 몇 번째(0부터).</summary>
        SuddenDeath = 1,
        /// <summary>틱이 끝날 때 Side 공통 체력 변화. Amount = 이번 틱 총 피해, Value = 남은 체력(0 이상).</summary>
        TeamHpChanged = 2,
        /// <summary>전투 종료. Value = (int)BattleOutcome.</summary>
        End = 3,
        /// <summary>Side 방어막 변화. Amount = 이번에 흡수한 피해(0이면 늘어남), Value = 남은 방어막.</summary>
        ShieldChanged = 4,
        /// <summary>Side에 쌓인 상태이상 변화. Amount = 화상 스택, Value = 독 스택.</summary>
        StatusChanged = 5,
        /// <summary>Side 체력 회복. Amount = 실제 회복량, Value = 회복 후 체력.</summary>
        Heal = 6,
        /// <summary>Side의 Slot 몬스터 능력 발동. Amount = 수치, Value = (int)AbilityEffect.</summary>
        Ability = 7,
        /// <summary>Side가 지속 피해를 받음. Amount = 피해, Value = 0 화상 · 1 독.</summary>
        DamageOverTime = 8,
    }

    /// <summary>
    /// 전투 기록 한 줄. 화면은 이 목록만 보고 재생한다 (규칙 → 화면은 이벤트로만).
    /// </summary>
    public readonly struct BattleEvent
    {
        public readonly int Tick;
        public readonly BattleEventType Type;
        public readonly BattleSide Side;
        public readonly int Slot;
        public readonly int Amount;
        public readonly int Value;

        public BattleEvent(int tick, BattleEventType type, BattleSide side, int slot, int amount, int value)
        {
            Tick = tick;
            Type = type;
            Side = side;
            Slot = slot;
            Amount = amount;
            Value = value;
        }

        public override string ToString() => $"{Tick}:{Type}:{Side}:{Slot}:{Amount}:{Value}";
    }
}
