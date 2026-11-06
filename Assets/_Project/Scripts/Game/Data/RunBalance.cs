using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Snapshot;
using UnityEngine;

namespace Moa.Showdown.Game.Data
{
    /// <summary>
    /// 판 진행 수치 · 시작 팀 데이터 에셋 (BALANCE 5 · 9). 값은 Setup 메뉴(MoaShowdown/Setup/Run Balance)가 처음 한 번 채운다.
    /// </summary>
    [CreateAssetMenu(menuName = "MoaShowdown/Run Balance", fileName = "RunBalance")]
    public sealed class RunBalance : ScriptableObject
    {
        [Tooltip("이만큼 이기면 챔피언 (BALANCE 5)")]
        public int winsToChampion;

        [Tooltip("시작 목숨 (BALANCE 5)")]
        public int startLives;

        [Tooltip("라운드 1부터 패배 시 잃는 목숨. 표보다 뒤 라운드는 마지막 값 (BALANCE 5)")]
        public int[] lifeLossByRound;

        [Header("시작 (BALANCE 9)")]
        public string startTrainerId;
        public List<StartSlot> startTeam = new List<StartSlot>();

        public RunRules ToRules() => new RunRules
        {
            WinsToChampion = winsToChampion,
            StartLives = startLives,
            LifeLossByRound = (int[])lifeLossByRound.Clone(),
        };

        public MonsterSnapshot[] ToStartTeam()
        {
            var team = new MonsterSnapshot[BattleTeam.SlotCount];
            foreach (StartSlot s in startTeam)
                team[s.slot] = new MonsterSnapshot { SpeciesId = s.speciesId, Level = s.level };
            return team;
        }
    }

    [Serializable]
    public sealed class StartSlot
    {
        [Tooltip("칸 번호 = 줄 × 3 + 열 (줄 0 = 위, 열 0 = 가운데 쪽)")]
        public int slot;
        public string speciesId;
        public int level = 1;
    }
}
