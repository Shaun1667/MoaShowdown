using System;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Snapshot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>로비 상태 ↔ JSON 한 곳 (저장 · 방장 넘기기 · 재접속용). 읽기 = 버전 확인 → 검사. 형식 오류는 <see cref="SnapshotFormatException"/>.</summary>
    public static class LobbyStateJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TypeNameHandling = TypeNameHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        public static string Serialize(LobbyState lobby)
        {
            Validate(lobby);
            return JsonConvert.SerializeObject(lobby, Settings);
        }

        public static LobbyState Deserialize(string json)
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
            if (v == null || v.Type != JTokenType.Integer || v.Value<int>() != LobbyState.CurrentFormatVersion)
                throw new SnapshotFormatException("로비 formatVersion을 읽을 수 없음");

            LobbyState lobby;
            try
            {
                lobby = root.ToObject<LobbyState>(JsonSerializer.Create(Settings));
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException)
            {
                throw new SnapshotFormatException("필드 형식 오류", e);
            }
            Validate(lobby);
            return lobby;
        }

        public static void Validate(LobbyState l)
        {
            if (l == null || string.IsNullOrEmpty(l.LobbyId)) throw new SnapshotFormatException("lobbyId 없음");
            if (l.Round < 1) throw new SnapshotFormatException("round < 1");
            if (l.Players == null || l.Players.Count < 2) throw new SnapshotFormatException("플레이어 수 오류");
            for (int i = 0; i < l.Players.Count; i++)
            {
                LobbyPlayer p = l.Players[i];
                if (p == null || p.Index != i) throw new SnapshotFormatException($"players[{i}] 번호 오류");
                if (p.RecentOpponents == null) throw new SnapshotFormatException($"players[{i}] recentOpponents 없음");
                RunStateJson.Validate(p.Run);
            }
            if (l.EliminationOrder == null) throw new SnapshotFormatException("eliminationOrder 없음");
            if (l.Pool == null) throw new SnapshotFormatException("pool 없음");
            foreach (var e in l.Pool)
                if (e == null || string.IsNullOrEmpty(e.SpeciesId) || e.Count < 0) throw new SnapshotFormatException("pool 항목 오류");
        }
    }
}
