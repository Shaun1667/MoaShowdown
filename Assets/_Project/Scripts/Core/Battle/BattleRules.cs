using System;

namespace Moa.Showdown.Core.Battle
{
    /// <summary>
    /// 전투 수치. 값은 데이터 에셋(Game의 BattleBalance)에서 채운다 — 여기에 기본값을 넣지 않는다. 표는 BALANCE 1~4번.
    /// </summary>
    public sealed class BattleRules
    {
        /// <summary>표현용: 1초에 몇 틱인지 (규칙 계산에는 쓰지 않음).</summary>
        public int TicksPerSecond;

        /// <summary>라운드 1부터의 팀 공통 체력. 표보다 뒤 라운드는 마지막 값.</summary>
        public int[] TeamHpByRound;

        /// <summary>서든데스가 시작되는 틱.</summary>
        public int SuddenDeathStartTick;

        /// <summary>서든데스 피해 간격(틱).</summary>
        public int SuddenDeathIntervalTicks;

        /// <summary>서든데스 첫 피해.</summary>
        public int SuddenDeathBaseDamage;

        /// <summary>서든데스 피해가 회마다 늘어나는 양.</summary>
        public int SuddenDeathDamageStep;

        /// <summary>안전 상한. 여기까지 안 끝나면 무승부.</summary>
        public int MaxTicks;

        /// <summary>화상 피해 간격(틱): 이 간격마다 쌓인 만큼 피해 후 1 감소 (BALANCE 11).</summary>
        public int BurnIntervalTicks;

        /// <summary>독 피해 간격(틱): 이 간격마다 쌓인 만큼 피해, 줄지 않음 (BALANCE 11).</summary>
        public int PoisonIntervalTicks;

        public int GetTeamHp(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (TeamHpByRound == null || TeamHpByRound.Length == 0) throw new InvalidOperationException("TeamHpByRound가 비어 있음");
            int i = Math.Min(round - 1, TeamHpByRound.Length - 1);
            return TeamHpByRound[i];
        }

        public void Validate()
        {
            if (TeamHpByRound == null || TeamHpByRound.Length == 0) throw new InvalidOperationException("TeamHpByRound가 비어 있음");
            foreach (int hp in TeamHpByRound)
                if (hp <= 0) throw new InvalidOperationException("TeamHpByRound에 0 이하 값");
            if (SuddenDeathStartTick < 1) throw new InvalidOperationException("SuddenDeathStartTick < 1");
            if (SuddenDeathIntervalTicks < 1) throw new InvalidOperationException("SuddenDeathIntervalTicks < 1");
            if (SuddenDeathBaseDamage < 1) throw new InvalidOperationException("SuddenDeathBaseDamage < 1");
            if (SuddenDeathDamageStep < 0) throw new InvalidOperationException("SuddenDeathDamageStep < 0");
            if (MaxTicks <= SuddenDeathStartTick) throw new InvalidOperationException("MaxTicks <= SuddenDeathStartTick");
            if (BurnIntervalTicks < 1) throw new InvalidOperationException("BurnIntervalTicks < 1");
            if (PoisonIntervalTicks < 1) throw new InvalidOperationException("PoisonIntervalTicks < 1");
        }
    }
}
