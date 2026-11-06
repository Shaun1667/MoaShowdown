namespace Moa.Showdown.Core.Battle
{
    /// <summary>능력이 언제 발동하는가. 끝에만 추가한다 (데이터 에셋이 숫자로 저장).</summary>
    public enum AbilityTrigger
    {
        None = 0,
        /// <summary>전투 시작 때 한 번.</summary>
        BattleStart = 1,
        /// <summary>이 몬스터가 공격할 때마다.</summary>
        OnAttack = 2,
        /// <summary>이 몬스터의 N번째 공격마다 (N = EveryN).</summary>
        EveryNthAttack = 3,
        /// <summary>상점 단계: 라운드 시작 때 (보드에 있을 때).</summary>
        ShopRoundStart = 4,
        /// <summary>상점 단계: 이 몬스터를 살 때.</summary>
        OnBuy = 5,
    }

    /// <summary>능력이 무엇을 하는가. 끝에만 추가한다.</summary>
    public enum AbilityEffect
    {
        None = 0,
        /// <summary>이번 공격 피해 + 수치.</summary>
        BonusDamage = 1,
        /// <summary>상대 팀에 화상 + 수치 (1초마다 쌓인 만큼 피해, 그 뒤 1 감소).</summary>
        Burn = 2,
        /// <summary>상대 팀에 독 + 수치 (2초마다 쌓인 만큼 피해, 줄지 않음).</summary>
        Poison = 3,
        /// <summary>우리 팀 방어막 + 수치 (모든 피해를 체력보다 먼저 받음).</summary>
        Shield = 4,
        /// <summary>우리 팀 체력 + 수치 (시작 체력을 넘지 않음).</summary>
        Heal = 5,
        /// <summary>이번 공격 피해 × 수치.</summary>
        DamageMultiplier = 6,
        /// <summary>상점: 자기 공격력 영구 + 수치.</summary>
        PermAttackSelf = 7,
        /// <summary>상점: 내 다른 몬스터 중 공격력이 가장 낮은 하나의 공격력 영구 + 수치.</summary>
        PermAttackWeakestAlly = 8,
    }

    /// <summary>
    /// 전투에 들고 들어가는 능력 (수치는 레벨까지 반영된 값). 몬스터 표의 능력을 <c>TeamFactory</c>가 이걸로 바꾼다.
    /// </summary>
    public readonly struct BattleAbility
    {
        public readonly AbilityTrigger Trigger;
        public readonly AbilityEffect Effect;
        public readonly int Amount;
        public readonly int EveryN;

        public BattleAbility(AbilityTrigger trigger, AbilityEffect effect, int amount, int everyN)
        {
            Trigger = trigger;
            Effect = effect;
            Amount = amount;
            EveryN = everyN;
        }

        public bool IsNone => Trigger == AbilityTrigger.None || Effect == AbilityEffect.None;
    }
}
