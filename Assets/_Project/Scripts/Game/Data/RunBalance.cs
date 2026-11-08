using System;
using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("라운드별 등급 확률표 (BALANCE 17) — 이 라운드부터 일반 · 고급 · 희귀 · 영웅 · 전설 %, 합 100. 비우면 등급을 따로 안 뽑음")]
        public List<RarityOddsRow> rarityOdds = new List<RarityOddsRow>();

        [Header("로비 (BALANCE 13)")]
        [Tooltip("로비 인원 (모자라면 봇)")] public int playerCount;
        [Tooltip("최근 몇 라운드의 상대를 피할지")] public int recentOpponentMemory;
        [Tooltip("공유 풀: 등급별 종류당 수량 (일반 · 고급 · 희귀 · 영웅 · 전설, BALANCE 14)")] public int[] poolSizeByRarity;
        [Tooltip("준비(상점) 시간 초 (BALANCE 15)")] public int prepSeconds;

        [Header("PvE · 캐러셀 (BALANCE 16)")]
        [Tooltip("1라운드부터 이만큼 PvE")] public int pveOpeningRounds;
        [Tooltip("이 라운드부터 반복 PvE (0 = 없음)")] public int pveRepeatStart;
        [Tooltip("반복 PvE 간격 (라운드)")] public int pveRepeatEvery;
        [Tooltip("PvE에서 지면 잃는 목숨 (고정)")] public int pveLifeLoss;
        [Tooltip("PvE 팀 — 나오는 순서대로, 표보다 뒤는 마지막 팀")] public List<PveTeamRow> pveTeams = new List<PveTeamRow>();
        [Tooltip("1라운드부터 이만큼 캐러셀")] public int carouselOpeningRounds;
        [Tooltip("이 라운드부터 반복 캐러셀 (0 = 없음)")] public int carouselRepeatStart;
        [Tooltip("반복 캐러셀 간격 (라운드)")] public int carouselRepeatEvery;
        [Tooltip("사람 한 명이 고르는 시간 초")] public int carouselPickSeconds;

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
            RarityOddsByRound = (rarityOdds ?? new List<RarityOddsRow>())
                .Select(o => new RarityOdds { FromRound = o.fromRound, Percent = (int[])(o.percent ?? new int[0]).Clone() })
                .ToArray(),
        };

        public LobbyRules ToLobbyRules() => new LobbyRules
        {
            PlayerCount = playerCount,
            RecentOpponentMemory = recentOpponentMemory,
            PoolSizeByRarity = (int[])poolSizeByRarity.Clone(),
            PrepSeconds = prepSeconds,
            PveOpeningRounds = pveOpeningRounds,
            PveRepeatStart = pveRepeatStart,
            PveRepeatEvery = pveRepeatEvery,
            PveLifeLoss = pveLifeLoss,
            PveTeams = pveTeams.Select(t => t.ToDef()).ToList(),
            CarouselOpeningRounds = carouselOpeningRounds,
            CarouselRepeatStart = carouselRepeatStart,
            CarouselRepeatEvery = carouselRepeatEvery,
            CarouselPickSeconds = carouselPickSeconds,
        };

        public MonsterSnapshot[] ToStartTeam()
        {
            var team = new MonsterSnapshot[BattleTeam.SlotCount];
            foreach (StartSlot s in startTeam)
                team[s.slot] = new MonsterSnapshot { SpeciesId = s.speciesId, Level = s.level };
            return team;
        }
    }

    /// <summary>PvE 팀 한 줄 (BALANCE 16): 몬스터 칸 + 보상.</summary>
    [Serializable]
    public sealed class PveTeamRow
    {
        public string label;
        [Tooltip("이기면 골드")] public int winGold;
        [Tooltip("지거나 비기면 골드")] public int loseGold;
        public List<StartSlot> monsters = new List<StartSlot>();

        public PveTeamDef ToDef()
        {
            var d = new PveTeamDef { WinGold = winGold, LoseGold = loseGold };
            foreach (StartSlot s in monsters)
                d.Board[s.slot] = new MonsterSnapshot { SpeciesId = s.speciesId, Level = s.level };
            return d;
        }
    }

    /// <summary>등급 확률표 한 줄 (BALANCE 17).</summary>
    [Serializable]
    public sealed class RarityOddsRow
    {
        [Tooltip("이 라운드부터")] public int fromRound;
        [Tooltip("일반 · 고급 · 희귀 · 영웅 · 전설 %, 합 100")] public int[] percent;
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
