using System;
using Moa.Showdown.Core.Battle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Moa.Showdown.Core.Snapshot
{
    public sealed class SnapshotFormatException : Exception
    {
        public SnapshotFormatException(string message) : base(message)
        {
        }

        public SnapshotFormatException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// 팀 스냅샷 ↔ JSON 한 곳. 읽을 때: 버전 확인 → 옛 버전이면 단계별 변환 → 객체로 → 구조 검사.
    /// 받은 스냅샷은 다른 사람이 만든 데이터이므로 구조가 틀리면 예외로 거부한다.
    /// </summary>
    public static class TeamSnapshotJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        public static string Serialize(TeamSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Validate(snapshot);
            return JsonConvert.SerializeObject(snapshot, Settings);
        }

        public static TeamSnapshot Deserialize(string json)
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

            JToken versionToken = root["formatVersion"];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                throw new SnapshotFormatException("formatVersion 없음");
            int version = versionToken.Value<int>();
            if (version < 1) throw new SnapshotFormatException($"formatVersion {version} 잘못됨");
            if (version > TeamSnapshot.CurrentFormatVersion)
                throw new SnapshotFormatException($"formatVersion {version}은 이 클라이언트({TeamSnapshot.CurrentFormatVersion})보다 새 버전");

            Migrate(root, version);

            if (root["board"] == null || root["board"].Type != JTokenType.Array)
                throw new SnapshotFormatException("board 없음");

            TeamSnapshot snapshot;
            try
            {
                snapshot = root.ToObject<TeamSnapshot>(JsonSerializer.Create(Settings));
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException)
            {
                throw new SnapshotFormatException("필드 형식 오류", e);
            }

            Validate(snapshot);
            return snapshot;
        }

        /// <summary>옛 버전 → 현재 버전, 한 단계씩.</summary>
        private static void Migrate(JObject root, int fromVersion)
        {
            for (int v = fromVersion; v < TeamSnapshot.CurrentFormatVersion; v++)
                MigrateStep(root, v);
            root["formatVersion"] = TeamSnapshot.CurrentFormatVersion;
        }

        /// <summary>버전 v → v+1. 버전을 올릴 때 여기에 "case N: (N → N+1 변환) break;"를 추가한다.</summary>
        private static void MigrateStep(JObject root, int v)
        {
            switch (v)
            {
                default:
                    throw new SnapshotFormatException($"formatVersion {v} → {v + 1} 변환이 없음");
            }
        }

        public static void Validate(TeamSnapshot s)
        {
            if (s == null) throw new SnapshotFormatException("스냅샷이 null");
            if (s.FormatVersion != TeamSnapshot.CurrentFormatVersion) throw new SnapshotFormatException($"formatVersion {s.FormatVersion} ≠ {TeamSnapshot.CurrentFormatVersion}");
            if (s.Round < 1) throw new SnapshotFormatException($"round {s.Round} < 1");
            if (s.Wins < 0) throw new SnapshotFormatException($"wins {s.Wins} < 0");
            if (s.LivesLost < 0) throw new SnapshotFormatException($"livesLost {s.LivesLost} < 0");
            if (s.TrinketIds == null) throw new SnapshotFormatException("trinketIds 없음");
            foreach (string id in s.TrinketIds)
                if (string.IsNullOrEmpty(id)) throw new SnapshotFormatException("빈 trinketId");
            if (s.Board == null || s.Board.Length != BattleTeam.SlotCount)
                throw new SnapshotFormatException($"board 칸 수가 {BattleTeam.SlotCount}이 아님");

            for (int i = 0; i < s.Board.Length; i++)
            {
                MonsterSnapshot m = s.Board[i];
                if (m == null) continue;
                if (string.IsNullOrEmpty(m.SpeciesId)) throw new SnapshotFormatException($"board[{i}] speciesId 없음");
                if (m.Level < 1) throw new SnapshotFormatException($"board[{i}] level {m.Level} < 1");
                if (m.Buffs == null) throw new SnapshotFormatException($"board[{i}] buffs 없음");
                foreach (StatBuff b in m.Buffs)
                    if (b == null || string.IsNullOrEmpty(b.Stat)) throw new SnapshotFormatException($"board[{i}] 빈 buff");
            }
        }
    }
}
