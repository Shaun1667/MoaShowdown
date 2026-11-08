using System;
using Newtonsoft.Json;

namespace Moa.Showdown.Core.Online
{
    /// <summary>
    /// 방 안에서 주고받는 메시지 한 통 (O-1, CLAUDE 핵심 규칙 6 · 7 — 온라인 계약). 전송은 <see cref="IRoomTransport"/>가, 형식은 <see cref="NetMessageJson"/> 한 곳.
    /// 내용(body)은 종류(type)마다 정한 JSON 문자열 — 팀 스냅샷 등은 O-2에서 기존 JSON 변환(TeamSnapshotJson 등)을 그대로 넣는다.
    /// </summary>
    public sealed class NetMessage
    {
        /// <summary>메시지 형식 버전. 봉투 필드를 바꾸면 +1 (옛 버전 읽기는 <see cref="NetMessageJson"/>).</summary>
        [JsonProperty("v", Order = 0)]
        public int FormatVersion = NetMessageJson.CurrentFormatVersion;

        /// <summary>종류 (<see cref="NetMessageTypes"/>).</summary>
        [JsonProperty("type", Order = 1)]
        public string Type;

        /// <summary>보낸 사람 플레이어 ID (UGS 인증 ID). 받는 쪽은 전송 계층이 아니라 이 값으로 누군지 안다.</summary>
        [JsonProperty("from", Order = 2)]
        public string From;

        /// <summary>내용 (종류마다 정한 형식, 없으면 null).</summary>
        [JsonProperty("body", Order = 3)]
        public string Body;
    }

    /// <summary>메시지 종류 문자열. 온라인 계약이라 한번 정하면 바꾸지 않는다 — 새 종류 = 새 문자열.</summary>
    public static class NetMessageTypes
    {
        /// <summary>시험 신호 (O-1): 참가자 → 방장.</summary>
        public const string Ping = "ping";
        /// <summary>시험 신호 응답 (O-1): 방장 → 모두. body = 신호를 보낸 사람 ID.</summary>
        public const string Pong = "pong";
    }

    public sealed class NetMessageFormatException : Exception
    {
        public NetMessageFormatException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>메시지 JSON 변환 한 곳: 읽기 = 버전 확인(더 새 버전은 거부) → 검사.</summary>
    public static class NetMessageJson
    {
        public const int CurrentFormatVersion = 1;

        /// <summary>한 통의 최대 길이 (글자). 넘으면 보내지도 받지도 않는다 — 안전 상한, 밸런스 값 아님.</summary>
        public const int MaxLength = 64 * 1024;

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static string Serialize(NetMessage m)
        {
            Validate(m);
            string json = JsonConvert.SerializeObject(m, Settings);
            if (json.Length > MaxLength) throw new NetMessageFormatException($"메시지가 너무 김 ({json.Length})");
            return json;
        }

        public static NetMessage Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new NetMessageFormatException("빈 메시지");
            if (json.Length > MaxLength) throw new NetMessageFormatException($"메시지가 너무 김 ({json.Length})");
            NetMessage m;
            try
            {
                m = JsonConvert.DeserializeObject<NetMessage>(json, Settings);
            }
            catch (JsonException e)
            {
                throw new NetMessageFormatException("JSON 아님", e);
            }
            if (m == null) throw new NetMessageFormatException("빈 메시지");
            if (m.FormatVersion > CurrentFormatVersion) throw new NetMessageFormatException($"더 새 메시지 형식 ({m.FormatVersion})");
            if (m.FormatVersion < 1) throw new NetMessageFormatException($"잘못된 메시지 형식 버전 ({m.FormatVersion})");
            Validate(m);
            return m;
        }

        /// <summary>읽기 실패를 예외 대신 false로 (네트워크에서 받은 남의 데이터용).</summary>
        public static bool TryDeserialize(string json, out NetMessage m)
        {
            try
            {
                m = Deserialize(json);
                return true;
            }
            catch (NetMessageFormatException)
            {
                m = null;
                return false;
            }
        }

        private static void Validate(NetMessage m)
        {
            if (m == null) throw new NetMessageFormatException("null 메시지");
            if (string.IsNullOrEmpty(m.Type)) throw new NetMessageFormatException("종류 없음");
            if (string.IsNullOrEmpty(m.From)) throw new NetMessageFormatException("보낸 사람 없음");
        }
    }
}
