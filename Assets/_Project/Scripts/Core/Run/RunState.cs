using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Snapshot;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Run
{
    public enum RunOutcome
    {
        InProgress = 0,
        Champion = 1,
        Eliminated = 2,
    }

    /// <summary>
    /// 한 판의 상태 전부 = 스냅샷 하나 (CLAUDE 핵심 규칙 5). 저장 · 이어하기 · 되돌리기는 이것만 저장하면 된다.
    /// 새 상태 = 필드 추가 + <see cref="CurrentFormatVersion"/> +1 + <see cref="RunStateJson"/> 변환 + 왕복 테스트.
    /// 난수는 상태로 들고 있지 않고 <see cref="Random.SeedMix"/>로 (판 시드, 용도, 라운드)에서 매번 뽑는다.
    /// </summary>
    public sealed class RunState
    {
        public const int CurrentFormatVersion = 1;

        [JsonProperty("formatVersion", Order = 0)]
        public int FormatVersion = CurrentFormatVersion;

        [JsonProperty("runId", Order = 1)]
        public string RunId;

        /// <summary>판 시드 (JSON 숫자 범위 때문에 long으로 저장, 쓸 때 ulong으로).</summary>
        [JsonProperty("seed", Order = 2)]
        public long Seed;

        /// <summary>지금 치를(또는 마지막으로 치른) 라운드. 1부터.</summary>
        [JsonProperty("round", Order = 3)]
        public int Round = 1;

        [JsonProperty("wins", Order = 4)]
        public int Wins;

        [JsonProperty("lives", Order = 5)]
        public int Lives;

        [JsonProperty("livesLost", Order = 6)]
        public int LivesLost;

        [JsonProperty("trainerId", Order = 7)]
        public string TrainerId;

        [JsonProperty("team", Order = 8)]
        public MonsterSnapshot[] Team = new MonsterSnapshot[BattleTeam.SlotCount];

        /// <summary>만난 상대 판 ID (같은 상대 반복 방지). 순서 = 만난 순서.</summary>
        [JsonProperty("facedRunIds", Order = 9)]
        public List<string> FacedRunIds = new List<string>();

        [JsonProperty("history", Order = 10)]
        public List<RoundRecord> History = new List<RoundRecord>();

        /// <summary>판 결과 (저장한 값이 아니라 승수 · 목숨에서 계산).</summary>
        public RunOutcome GetOutcome(RunRules rules) =>
            Wins >= rules.WinsToChampion ? RunOutcome.Champion
            : Lives <= 0 ? RunOutcome.Eliminated
            : RunOutcome.InProgress;
    }

    /// <summary>라운드 하나의 기록.</summary>
    public sealed class RoundRecord
    {
        [JsonProperty("round", Order = 0)]
        public int Round;

        [JsonProperty("opponentRunId", Order = 1)]
        public string OpponentRunId;

        [JsonProperty("opponentIsBot", Order = 2)]
        public bool OpponentIsBot;

        /// <summary>"win" · "lose" · "draw" · "bye"(상대 없음 → 부전승).</summary>
        [JsonProperty("result", Order = 3)]
        public string Result;

        [JsonProperty("livesLost", Order = 4)]
        public int LivesLost;
    }
}
