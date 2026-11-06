using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>로비 수치 (BALANCE 13). 값은 데이터 에셋(RunBalance)에서.</summary>
    public sealed class LobbyRules
    {
        public int PlayerCount;

        /// <summary>최근 몇 라운드의 상대를 피할지 (살아남은 사람이 적으면 자동으로 줄임).</summary>
        public int RecentOpponentMemory;

        /// <summary>공유 풀: 등급(Rarity 번호 순)별 종류당 수량 (L-2, TFT 값).</summary>
        public int[] PoolSizeByRarity;

        /// <summary>준비(상점) 시간 (초, L-3). 전원 준비 완료면 더 일찍 시작.</summary>
        public int PrepSeconds;

        // ── PvE (L-4, BALANCE 16): 1 ~ PveOpeningRounds 라운드 + PveRepeatStart부터 PveRepeatEvery마다. 0 = 없음 ──
        public int PveOpeningRounds;
        public int PveRepeatStart;
        public int PveRepeatEvery;
        /// <summary>PvE에서 지면 잃는 목숨 (라운드와 상관없이 고정).</summary>
        public int PveLifeLoss;
        /// <summary>PvE 팀 (나오는 순서대로, 표보다 뒤는 마지막 팀).</summary>
        public List<PveTeamDef> PveTeams = new List<PveTeamDef>();

        // ── 캐러셀 (L-4, BALANCE 16): 1 ~ CarouselOpeningRounds 라운드 + CarouselRepeatStart부터 CarouselRepeatEvery마다. 0 = 없음 ──
        public int CarouselOpeningRounds;
        public int CarouselRepeatStart;
        public int CarouselRepeatEvery;
        /// <summary>사람 한 명이 고르는 시간 (초). 끝나면 봇 규칙으로 대신 고름.</summary>
        public int CarouselPickSeconds;

        public bool IsPveRound(int round) => Scheduled(round, PveOpeningRounds, PveRepeatStart, PveRepeatEvery);
        public bool IsCarouselRound(int round) => Scheduled(round, CarouselOpeningRounds, CarouselRepeatStart, CarouselRepeatEvery);

        /// <summary>이 라운드의 PvE 팀 (몇 번째 PvE인지로 고름, 표보다 뒤는 마지막). PvE 라운드가 아니면 null.</summary>
        public PveTeamDef PveTeamFor(int round)
        {
            if (!IsPveRound(round) || PveTeams.Count == 0) return null;
            int index = 0;
            for (int r = 1; r < round; r++)
                if (IsPveRound(r)) index++;
            return PveTeams[Math.Min(index, PveTeams.Count - 1)];
        }

        private static bool Scheduled(int round, int opening, int repeatStart, int repeatEvery)
        {
            if (round >= 1 && round <= opening) return true;
            return repeatStart > 0 && repeatEvery > 0 && round >= repeatStart && (round - repeatStart) % repeatEvery == 0;
        }

        public void Validate()
        {
            if (PlayerCount < 2) throw new InvalidOperationException("PlayerCount < 2");
            if (RecentOpponentMemory < 0) throw new InvalidOperationException("RecentOpponentMemory < 0");
            if (PrepSeconds < 1) throw new InvalidOperationException("PrepSeconds < 1");
            if (PoolSizeByRarity == null || PoolSizeByRarity.Length == 0) throw new InvalidOperationException("PoolSizeByRarity 없음");
            foreach (int n in PoolSizeByRarity)
                if (n < 0) throw new InvalidOperationException("풀 수량 음수");
            if (PveOpeningRounds < 0 || PveRepeatStart < 0 || PveRepeatEvery < 0 || PveLifeLoss < 0) throw new InvalidOperationException("PvE 수치 음수");
            if (CarouselOpeningRounds < 0 || CarouselRepeatStart < 0 || CarouselRepeatEvery < 0) throw new InvalidOperationException("캐러셀 수치 음수");
            bool hasPve = PveOpeningRounds > 0 || (PveRepeatStart > 0 && PveRepeatEvery > 0);
            if (hasPve && (PveTeams == null || PveTeams.Count == 0)) throw new InvalidOperationException("PvE 라운드가 있는데 PvE 팀이 없음");
            if (PveTeams != null)
                foreach (PveTeamDef t in PveTeams)
                {
                    if (t?.Board == null || t.Board.Length != BattleTeam.SlotCount) throw new InvalidOperationException("PvE 팀 칸 수 오류");
                    if (Array.TrueForAll(t.Board, m => m == null)) throw new InvalidOperationException("PvE 팀에 몬스터가 없음");
                    if (t.WinGold < 0 || t.LoseGold < 0) throw new InvalidOperationException("PvE 보상 음수");
                }
            bool hasCarousel = CarouselOpeningRounds > 0 || (CarouselRepeatStart > 0 && CarouselRepeatEvery > 0);
            if (hasCarousel && CarouselPickSeconds < 1) throw new InvalidOperationException("CarouselPickSeconds < 1");
        }
    }

    /// <summary>PvE 팀 하나 (BALANCE 16): 보드 6칸 + 보상 골드 (이김 · 짐/무승부).</summary>
    public sealed class PveTeamDef
    {
        public MonsterSnapshot[] Board = new MonsterSnapshot[BattleTeam.SlotCount];
        public int WinGold;
        public int LoseGold;
    }

    /// <summary>
    /// 캐러셀 진행 (L-4, TFT 방식): 공유 풀에서 (생존자 + 1)마리를 꺼내 놓고, 목숨이 적은 사람부터(같으면 번호 순) 한 마리씩 고른다.
    /// 모두 고르면 남은 몬스터는 풀로 돌아간다. 고르는 중에 저장 · 방장 넘기기가 되도록 로비 상태에 들어 있다.
    /// </summary>
    public sealed class CarouselState
    {
        [JsonProperty("round", Order = 0)]
        public int Round;

        /// <summary>놓인 몬스터 (몬스터 ID).</summary>
        [JsonProperty("units", Order = 1)]
        public List<string> Units = new List<string>();

        /// <summary>몬스터마다 가져간 플레이어 번호 (-1 = 아직 / 끝나면 풀로 돌아감).</summary>
        [JsonProperty("takenBy", Order = 2)]
        public List<int> TakenBy = new List<int>();

        /// <summary>고르는 순서 (플레이어 번호).</summary>
        [JsonProperty("order", Order = 3)]
        public List<int> Order = new List<int>();

        /// <summary>Order에서 지금 차례 위치. Order.Count면 끝.</summary>
        [JsonProperty("next", Order = 4)]
        public int Next;

        [JsonIgnore]
        public bool IsDone => Next >= Order.Count;

        /// <summary>지금 고를 플레이어 번호 (끝났으면 -1).</summary>
        [JsonIgnore]
        public int CurrentPicker => IsDone ? -1 : Order[Next];
    }

    /// <summary>
    /// 로비 상태 전부 = 스냅샷 하나 (CLAUDE 핵심 규칙 5). 플레이어마다 자기 판 상태(<see cref="RunState"/>: 골드 · 보드 · 대기석 · 상점 · 목숨)를 들고 있다.
    /// 라운드 번호는 로비가 기준이고 살아 있는 플레이어의 RunState.Round를 맞춘다.
    /// </summary>
    public sealed class LobbyState
    {
        /// <summary>1 = L-1 · 2 = L-2 (공유 풀 추가) · 3 = L-4 (캐러셀 추가, 2는 캐러셀 없음으로 변환). 1은 저장된 적이 없어 변환 없이 거부한다.</summary>
        public const int CurrentFormatVersion = 3;

        [JsonProperty("formatVersion", Order = 0)]
        public int FormatVersion = CurrentFormatVersion;

        [JsonProperty("lobbyId", Order = 1)]
        public string LobbyId;

        [JsonProperty("seed", Order = 2)]
        public long Seed;

        [JsonProperty("round", Order = 3)]
        public int Round = 1;

        [JsonProperty("players", Order = 4)]
        public List<LobbyPlayer> Players = new List<LobbyPlayer>();

        /// <summary>탈락한 순서 (플레이어 번호). 마지막 생존자는 들어가지 않는다.</summary>
        [JsonProperty("eliminationOrder", Order = 5)]
        public List<int> EliminationOrder = new List<int>();

        /// <summary>공유 몬스터 풀 (몬스터 표 순서). 진열된 카드 · 산 몬스터는 여기서 빠져 있다.</summary>
        [JsonProperty("pool", Order = 6)]
        public List<PoolEntry> Pool = new List<PoolEntry>();

        /// <summary>마지막 캐러셀 (없으면 null). 고르는 중이면 아직 안 고른 몬스터는 풀에서 빠져 있다.</summary>
        [JsonProperty("carousel", Order = 7)]
        public CarouselState Carousel;

        public bool IsFinished => AliveCount <= 1;

        [JsonIgnore]
        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (LobbyPlayer p in Players) if (p.IsAlive) n++;
                return n;
            }
        }
    }

    public sealed class LobbyPlayer
    {
        [JsonProperty("index", Order = 0)]
        public int Index;

        [JsonProperty("name", Order = 1)]
        public string Name;

        [JsonProperty("isBot", Order = 2)]
        public bool IsBot;

        [JsonProperty("run", Order = 3)]
        public RunState Run;

        /// <summary>최종 순위 (1 = 우승). 0 = 아직 살아 있음.</summary>
        [JsonProperty("placement", Order = 4)]
        public int Placement;

        /// <summary>최근 상대 플레이어 번호 (오래된 것부터, 유령전은 넣지 않음).</summary>
        [JsonProperty("recentOpponents", Order = 5)]
        public List<int> RecentOpponents = new List<int>();

        [JsonIgnore]
        public bool IsAlive => Placement == 0 && Run.Lives > 0;
    }
}
