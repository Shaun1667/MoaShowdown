using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Run;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>로비 수치 (BALANCE 13). 값은 데이터 에셋(RunBalance)에서.</summary>
    public sealed class LobbyRules
    {
        public int PlayerCount;

        /// <summary>최근 몇 라운드의 상대를 피할지 (살아남은 사람이 적으면 자동으로 줄임).</summary>
        public int RecentOpponentMemory;

        public void Validate()
        {
            if (PlayerCount < 2) throw new InvalidOperationException("PlayerCount < 2");
            if (RecentOpponentMemory < 0) throw new InvalidOperationException("RecentOpponentMemory < 0");
        }
    }

    /// <summary>
    /// 로비 상태 전부 = 스냅샷 하나 (CLAUDE 핵심 규칙 5). 플레이어마다 자기 판 상태(<see cref="RunState"/>: 골드 · 보드 · 대기석 · 상점 · 목숨)를 들고 있다.
    /// 라운드 번호는 로비가 기준이고 살아 있는 플레이어의 RunState.Round를 맞춘다.
    /// </summary>
    public sealed class LobbyState
    {
        public const int CurrentFormatVersion = 1;

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
