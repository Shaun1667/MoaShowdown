using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using UnityEngine;

namespace Moa.Showdown.Game.Data
{
    /// <summary>
    /// 전투 수치 데이터 에셋 (BALANCE 1~4 · 6번). 값은 Setup 메뉴가 처음 한 번 채우고, 이후에는 인스펙터에서 고친다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaShowdown/Battle Balance", fileName = "BattleBalance")]
    public sealed class BattleBalance : ScriptableObject
    {
        [Tooltip("표현용: 1초에 몇 틱 (BALANCE 1)")]
        public int ticksPerSecond;

        [Tooltip("라운드 1부터 팀 공통 체력. 표보다 뒤 라운드는 마지막 값 (BALANCE 2)")]
        public int[] teamHpByRound;

        [Header("서든데스 (BALANCE 3)")]
        public int suddenDeathStartTick;
        public int suddenDeathIntervalTicks;
        public int suddenDeathBaseDamage;
        public int suddenDeathDamageStep;

        [Tooltip("안전 상한. 여기까지 안 끝나면 무승부 (BALANCE 4)")]
        public int maxTicks;

        [Header("능력치 · 버전 (BALANCE 6)")]
        [Tooltip("레벨이 1 오를 때마다 공격력 배율")]
        public int levelAttackMultiplier;

        [Tooltip("규칙 · 밸런스 · 몬스터 표를 바꾸면 +1. 스냅샷에 저장되고, 다른 버전 스냅샷은 상대로 고르지 않는다")]
        public int rulesVersion;

        public UnitStatRules ToStatRules() => new UnitStatRules { LevelAttackMultiplier = levelAttackMultiplier };

        public BattleRules ToRules()
        {
            return new BattleRules
            {
                TicksPerSecond = ticksPerSecond,
                TeamHpByRound = (int[])teamHpByRound.Clone(),
                SuddenDeathStartTick = suddenDeathStartTick,
                SuddenDeathIntervalTicks = suddenDeathIntervalTicks,
                SuddenDeathBaseDamage = suddenDeathBaseDamage,
                SuddenDeathDamageStep = suddenDeathDamageStep,
                MaxTicks = maxTicks,
            };
        }
    }
}
