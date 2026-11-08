using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moa.Showdown.Core.Online
{
    /// <summary>방 참가자 한 명.</summary>
    public sealed class RoomMember
    {
        public string PlayerId;
        public string Name;
        public bool IsHost;
    }

    /// <summary>지금 방 모습 (화면 · 진행 코드가 읽기만). 참가자는 들어온 순서, 방장이 맨 앞.</summary>
    public sealed class RoomSnapshot
    {
        /// <summary>참가 코드 (친구에게 알려 주는 짧은 글).</summary>
        public string Code;
        public int MaxPlayers;
        public string LocalPlayerId;
        public List<RoomMember> Members = new List<RoomMember>();

        public bool IsLocalHost
        {
            get
            {
                foreach (RoomMember m in Members)
                    if (m.PlayerId == LocalPlayerId) return m.IsHost;
                return false;
            }
        }

        public RoomMember Find(string playerId)
        {
            foreach (RoomMember m in Members)
                if (m.PlayerId == playerId) return m;
            return null;
        }
    }

    /// <summary>방 연결 상태.</summary>
    public enum RoomConnectionState
    {
        /// <summary>로그인 전 / 방 밖.</summary>
        Offline = 0,
        /// <summary>로그인 · 방 만들기 · 들어가기 중.</summary>
        Connecting = 1,
        /// <summary>방 안.</summary>
        InRoom = 2,
        /// <summary>실패 · 끊김 (메시지는 <see cref="IRoomTransport.LastError"/>).</summary>
        Failed = 3,
    }

    /// <summary>
    /// 방 · 메시지 전송 (CLAUDE 핵심 규칙 7: 네트워크는 인터페이스 뒤). UGS(Multiplayer Services + Netcode) 구현은 Game에,
    /// 규칙(누가 무엇을 보내고 받으면 어떻게 하는지)은 Core(<see cref="RoomProtocol"/>)에. 오프라인 ↔ 온라인 = 이 구현 교체.
    /// 이벤트는 메인 스레드에서 부른다.
    /// </summary>
    public interface IRoomTransport
    {
        RoomConnectionState State { get; }
        RoomSnapshot Room { get; }
        string LastError { get; }

        /// <summary>방 모습이 바뀜 (참가 · 퇴장 · 방장 바뀜 · 연결 상태).</summary>
        event Action RoomChanged;
        /// <summary>메시지 받음 (형식이 틀린 메시지는 전송 계층에서 버린다).</summary>
        event Action<NetMessage> MessageReceived;

        /// <summary>방 만들기 (나 = 방장). 참가 코드는 <see cref="Room"/>.Code.</summary>
        Task CreateAsync(string playerName, int maxPlayers);
        Task JoinByCodeAsync(string code, string playerName);
        /// <summary>빈자리 있는 공개 방에 들어가고, 없으면 새 방을 만든다 (나 = 방장).</summary>
        Task QuickJoinAsync(string playerName, int maxPlayers);
        Task LeaveAsync();

        /// <summary>방 잠금 (방장만, O-2: 판이 시작되면 새로 못 들어오게). 실패는 무시하고 기록만.</summary>
        Task SetLockedAsync(bool locked);

        /// <summary>방장에게 (내가 방장이면 나에게 바로 전달).</summary>
        void SendToHost(NetMessage m);
        /// <summary>모두에게 (방장만, 나 포함).</summary>
        void Broadcast(NetMessage m);
    }

    /// <summary>방 규칙 수치 · 이름 정리 (O-1).</summary>
    public static class RoomRules
    {
        /// <summary>이름 최대 글자 수.</summary>
        public const int MaxNameLength = 12;

        /// <summary>참가 코드 정리: 앞뒤 공백 · 가운데 공백 · 하이픈 제거, 대문자. 비면 null.</summary>
        public static string NormalizeCode(string code)
        {
            if (code == null) return null;
            var chars = new List<char>();
            foreach (char c in code)
                if (!char.IsWhiteSpace(c) && c != '-') chars.Add(char.ToUpperInvariant(c));
            return chars.Count == 0 ? null : new string(chars.ToArray());
        }

        /// <summary>표시 이름 정리: 앞뒤 공백 제거, 제어 문자 제거, 최대 글자 수로 자름. 비면 fallback.</summary>
        public static string SanitizeName(string name, string fallback)
        {
            if (name == null) return fallback;
            var chars = new List<char>();
            foreach (char c in name.Trim())
                if (!char.IsControl(c)) chars.Add(c);
            if (chars.Count == 0) return fallback;
            if (chars.Count > MaxNameLength) chars.RemoveRange(MaxNameLength, chars.Count - MaxNameLength);
            return new string(chars.ToArray()).Trim();
        }
    }

    /// <summary>
    /// 방 메시지 규칙 한 곳 (O-1: 시험 신호). 방장 · 참가자 모두 받은 메시지를 여기에 넘기고, 돌려준 답이 있으면 방장이 모두에게 보낸다.
    /// O-2의 타이머 · 대진 · 스냅샷 메시지도 여기에 종류를 더한다.
    /// </summary>
    public static class RoomProtocol
    {
        /// <summary>시험 신호를 만든다 (참가자 → 방장).</summary>
        public static NetMessage Ping(string from) => new NetMessage { Type = NetMessageTypes.Ping, From = from };

        /// <summary>
        /// 받은 메시지 처리. 방장이 Ping을 받으면 모두에게 보낼 Pong(body = 보낸 사람)을 돌려준다.
        /// 방에 없는 사람이 보낸 메시지 · 방장이 아닌데 받은 Ping은 무시(null).
        /// </summary>
        public static NetMessage Handle(NetMessage m, RoomSnapshot room)
        {
            if (m == null || room == null || room.Find(m.From) == null) return null;
            if (m.Type == NetMessageTypes.Ping && room.IsLocalHost)
                return new NetMessage { Type = NetMessageTypes.Pong, From = room.LocalPlayerId, Body = m.From };
            return null;
        }

        /// <summary>Pong이 온 경우 신호를 보냈던 사람 ID (아니면 null). Pong은 방장만 보낼 수 있다.</summary>
        public static string PongTarget(NetMessage m, RoomSnapshot room)
        {
            if (m == null || room == null || m.Type != NetMessageTypes.Pong) return null;
            RoomMember sender = room.Find(m.From);
            return sender != null && sender.IsHost ? m.Body : null;
        }
    }
}
