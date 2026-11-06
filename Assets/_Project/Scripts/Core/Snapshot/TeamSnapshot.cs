using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Snapshot
{
    /// <summary>
    /// 팀 스냅샷 = 온라인 계약 (CLAUDE 핵심 규칙 6). 라운드가 끝날 때 올리고, 다른 플레이어가 같은 라운드에서 상대로 받는다.
    /// JSON 이름은 [JsonProperty]로 고정 — C# 이름을 바꿔도 JSON은 바뀌지 않는다. 필드를 바꾸면 <see cref="CurrentFormatVersion"/>을 올리고
    /// <see cref="TeamSnapshotJson"/>에 이전 버전 변환을 추가한다. 몬스터는 ID · 레벨 · 버프만 (능력치는 전투 때 몬스터 표에서 계산, 결정 2026-11-06).
    /// 소유자 ID · 점수 같은 서버 정보는 넣지 않는다 (서버가 감싼다).
    /// </summary>
    public sealed class TeamSnapshot
    {
        public const int CurrentFormatVersion = 1;

        /// <summary>이 JSON 구조의 버전.</summary>
        [JsonProperty("formatVersion", Order = 0)]
        public int FormatVersion = CurrentFormatVersion;

        /// <summary>만들 때의 규칙 · 밸런스 버전. 다르면 매칭에서 제외하는 데 쓴다 (값은 콘텐츠 데이터가 생기면 거기서).</summary>
        [JsonProperty("rulesVersion", Order = 1)]
        public int RulesVersion;

        [JsonProperty("runId", Order = 2)]
        public string RunId;

        [JsonProperty("round", Order = 3)]
        public int Round;

        [JsonProperty("wins", Order = 4)]
        public int Wins;

        [JsonProperty("livesLost", Order = 5)]
        public int LivesLost;

        [JsonProperty("trainerId", Order = 6)]
        public string TrainerId;

        [JsonProperty("trinketIds", Order = 7)]
        public List<string> TrinketIds = new List<string>();

        /// <summary>3×2 보드, 칸 = 줄 × 3 + 열 (<see cref="BattleTeam.SlotOf"/>). 빈칸은 null.</summary>
        [JsonProperty("board", Order = 8)]
        public MonsterSnapshot[] Board = new MonsterSnapshot[BattleTeam.SlotCount];
    }

    public sealed class MonsterSnapshot
    {
        [JsonProperty("speciesId", Order = 0)]
        public string SpeciesId;

        [JsonProperty("level", Order = 1)]
        public int Level = 1;

        /// <summary>영구 버프. 순서가 그대로 저장된다 (딕셔너리 금지 — 순서가 바뀌면 같은 팀이 다른 JSON이 됨).</summary>
        [JsonProperty("buffs", Order = 2)]
        public List<StatBuff> Buffs = new List<StatBuff>();
    }

    public sealed class StatBuff
    {
        /// <summary>능력치 ID (문자열 — 숫자 enum은 순서가 바뀌면 옛 스냅샷이 뒤바뀜).</summary>
        [JsonProperty("stat", Order = 0)]
        public string Stat;

        [JsonProperty("value", Order = 1)]
        public int Value;

        public StatBuff()
        {
        }

        public StatBuff(string stat, int value)
        {
            Stat = stat;
            Value = value;
        }
    }
}
