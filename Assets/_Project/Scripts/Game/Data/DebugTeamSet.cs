using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using UnityEngine;

namespace Moa.Showdown.Game.Data
{
    /// <summary>
    /// 시험 전투용 팀 목록 (몬스터 표가 생기기 전 임시). 칸 번호 = 줄 × 3 + 열 (<see cref="BattleTeam.SlotOf"/>).
    /// 기본 팀은 Setup 메뉴(MoaShowdown/Setup/Debug Teams)가 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaShowdown/Debug Team Set", fileName = "DebugTeams")]
    public sealed class DebugTeamSet : ScriptableObject
    {
        public List<DebugTeam> teams = new List<DebugTeam>();
    }

    [Serializable]
    public sealed class DebugTeam
    {
        public string name;
        public DebugUnit[] slots = new DebugUnit[BattleTeam.SlotCount];

        public BattleTeam ToBattleTeam()
        {
            var team = new BattleTeam();
            for (int i = 0; i < BattleTeam.SlotCount && i < slots.Length; i++)
            {
                DebugUnit u = slots[i];
                if (u != null && u.present) team[i] = new BattleUnit(u.attack, u.attackIntervalTicks);
            }
            return team;
        }
    }

    [Serializable]
    public sealed class DebugUnit
    {
        public bool present;
        public int attack;
        [Tooltip("공격 간격(틱). 10틱 = 1초 (BALANCE 1)")]
        public int attackIntervalTicks;
    }
}
