using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
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

        [Header("상점 · 골드 (BALANCE 10)")]
        [Tooltip("1라운드 지급")] public int startGold;
        [Tooltip("2라운드 수입")] public int firstIncome;
        [Tooltip("3라운드부터 라운드마다 수입 증가량")] public int incomeStep;
        public int shopSlots;
        public int rerollCost;
        [Tooltip("등급별 가격: 일반 · 고급 · 희귀 · 영웅 · 전설")] public int[] priceByRarity;
        [Tooltip("판매가 = 들인 골드 × %")] public int sellPercent;
        [Tooltip("레벨별 합체 마리 수: [0] Lv1 몇 마리 → Lv2, [1] Lv2 몇 마리 → Lv3")] public int[] mergeCountByLevel;
        public int maxLevel;

        [Header("로비 (BALANCE 13)")]
        [Tooltip("로비 인원 (모자라면 봇)")] public int playerCount;
        [Tooltip("최근 몇 라운드의 상대를 피할지")] public int recentOpponentMemory;

        [Header("시작 (BALANCE 9)")]
        public string startTrainerId;
        public List<StartSlot> startTeam = new List<StartSlot>();

        public RunRules ToRules() => new RunRules
        {
            WinsToChampion = winsToChampion,
            StartLives = startLives,
            LifeLossByRound = (int[])lifeLossByRound.Clone(),
        };

        public ShopRules ToShopRules() => new ShopRules
        {
            StartGold = startGold,
            FirstIncome = firstIncome,
            IncomeStep = incomeStep,
            ShopSlots = shopSlots,
            RerollCost = rerollCost,
            PriceByRarity = (int[])priceByRarity.Clone(),
            SellPercent = sellPercent,
            MergeCountByLevel = (int[])mergeCountByLevel.Clone(),
            MaxLevel = maxLevel,
        };

        public LobbyRules ToLobbyRules() => new LobbyRules { PlayerCount = playerCount, RecentOpponentMemory = recentOpponentMemory };

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
