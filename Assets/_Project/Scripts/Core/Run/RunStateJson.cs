using System;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Snapshot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Moa.Showdown.Core.Run
{
    /// <summary>판 상태 ↔ JSON 한 곳 (저장 · 이어하기용). 읽기 = 버전 확인 → 단계 변환 → 검사. 형식 오류는 <see cref="SnapshotFormatException"/>.</summary>
    public static class RunStateJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        public static string Serialize(RunState state)
        {
            Validate(state);
            return JsonConvert.SerializeObject(state, Settings);
        }

        public static RunState Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new SnapshotFormatException("빈 JSON");
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new SnapshotFormatException("JSON 파싱 실패", e);
            }

            JToken v = root["formatVersion"];
            if (v == null || v.Type != JTokenType.Integer) throw new SnapshotFormatException("formatVersion 없음");
            int version = v.Value<int>();
            if (version < 1 || version > RunState.CurrentFormatVersion)
                throw new SnapshotFormatException($"판 상태 formatVersion {version}을 읽을 수 없음 (현재 {RunState.CurrentFormatVersion})");
            for (int step = version; step < RunState.CurrentFormatVersion; step++)
                MigrateStep(root, step);
            root["formatVersion"] = RunState.CurrentFormatVersion;

            RunState state;
            try
            {
                state = root.ToObject<RunState>(JsonSerializer.Create(Settings));
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException)
            {
                throw new SnapshotFormatException("필드 형식 오류", e);
            }
            Validate(state);
            return state;
        }

        /// <summary>버전 v → v+1. 버전을 올릴 때 "case N: … break;"를 추가한다.</summary>
        private static void MigrateStep(JObject root, int v)
        {
            switch (v)
            {
                default:
                    throw new SnapshotFormatException($"판 상태 formatVersion {v} → {v + 1} 변환이 없음");
            }
        }

        public static void Validate(RunState s)
        {
            if (s == null) throw new SnapshotFormatException("판 상태가 null");
            if (string.IsNullOrEmpty(s.RunId)) throw new SnapshotFormatException("runId 없음");
            if (s.Round < 1) throw new SnapshotFormatException("round < 1");
            if (s.Wins < 0 || s.LivesLost < 0 || s.Lives < 0) throw new SnapshotFormatException("wins · lives · livesLost 음수");
            if (s.Team == null || s.Team.Length != BattleTeam.SlotCount) throw new SnapshotFormatException("team 칸 수 오류");
            foreach (MonsterSnapshot m in s.Team)
            {
                if (m == null) continue;
                if (string.IsNullOrEmpty(m.SpeciesId) || m.Level < 1 || m.Buffs == null)
                    throw new SnapshotFormatException("team 몬스터 형식 오류");
            }
            if (s.FacedRunIds == null || s.History == null) throw new SnapshotFormatException("facedRunIds · history 없음");
        }
    }
}
