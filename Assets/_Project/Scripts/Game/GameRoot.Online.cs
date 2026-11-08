using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Game.Online;
using Moa.Showdown.Game.Ui;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// Online 모드 (O-1): 대기실만 — 로그인 · 방 만들기/코드 참가/빠른 참가 · 참가자 목록 · 시험 신호 · 나가기.
    /// 네트워크는 <see cref="IRoomTransport"/>(UGS 구현) 뒤, 메시지 규칙은 Core <see cref="RoomProtocol"/>. 실제 판 진행은 O-2.
    /// </summary>
    public sealed partial class GameRoot
    {
        [Header("온라인 (Online 모드, O-1)")]
        [SerializeField] private OnlineView onlineView;
        [Tooltip("방 최대 인원 (로비 인원과 같게)")]
        [SerializeField] private int onlineMaxPlayers = 8;

        private IRoomTransport _room;
        private HashSet<string> _knownMembers = new HashSet<string>();

        /// <summary>지금 방 연결 (확인 · 테스트용).</summary>
        public IRoomTransport Room => _room;
        public OnlineView OnlineViewForCheck => onlineView;

        private void StartOnline()
        {
            _room = new UgsRoomTransport();
            _room.RoomChanged += OnRoomChanged;
            _room.MessageReceived += OnRoomMessage;
            onlineView.Build(string.Format(HudText.OnlineDefaultNameFormat, Random.Range(1000, 10000)));
            onlineView.CreateClicked += n => _ = _room.CreateAsync(CleanName(n), onlineMaxPlayers);
            onlineView.QuickJoinClicked += n => _ = _room.QuickJoinAsync(CleanName(n), onlineMaxPlayers);
            onlineView.JoinClicked += (n, code) => _ = _room.JoinByCodeAsync(code, CleanName(n));
            onlineView.PingClicked += OnPingClicked;
            onlineView.LeaveClicked += () => _ = _room.LeaveAsync();
            onlineView.ShowOutside(HudText.OnlineIdle);
        }

        private string CleanName(string typed) =>
            RoomRules.SanitizeName(typed, string.Format(HudText.OnlineDefaultNameFormat, Random.Range(1000, 10000)));

        private void OnRoomChanged()
        {
            switch (_room.State)
            {
                case RoomConnectionState.Connecting:
                    onlineView.SetBusy(true);
                    onlineView.SetStatus(HudText.OnlineConnecting);
                    break;
                case RoomConnectionState.InRoom:
                    onlineView.SetBusy(false);
                    RoomSnapshot r = _room.Room;
                    LogMemberChanges(r);
                    var lines = new List<string> { string.Format(HudText.OnlineMembersFormat, r.Members.Count, r.MaxPlayers) };
                    lines.AddRange(r.Members.Select(m => string.Format(HudText.OnlineMemberFormat,
                        string.IsNullOrEmpty(m.Name) ? HudText.OnlineUnknownName : m.Name,
                        m.IsHost ? HudText.OnlineHostTag : "",
                        m.PlayerId == r.LocalPlayerId ? HudText.OnlineMeTag : "")));
                    onlineView.ShowRoom(string.Format(HudText.OnlineCodeFormat, r.Code), lines, r.IsLocalHost ? HudText.OnlineInRoomHost : HudText.OnlineInRoomGuest);
                    break;
                case RoomConnectionState.Failed:
                    onlineView.SetBusy(false);
                    _knownMembers.Clear();
                    onlineView.ClearLog();
                    onlineView.ShowOutside(string.Format(HudText.OnlineFailedFormat, _room.LastError));
                    break;
                default:
                    onlineView.SetBusy(false);
                    _knownMembers.Clear();
                    onlineView.ClearLog();
                    onlineView.ShowOutside(HudText.OnlineLeft);
                    break;
            }
        }

        /// <summary>참가 · 퇴장을 기록 줄로 (처음 들어갔을 때는 기록하지 않고 목록만 기억).</summary>
        private void LogMemberChanges(RoomSnapshot r)
        {
            var now = new HashSet<string>(r.Members.Select(m => m.PlayerId));
            if (_knownMembers.Count > 0)
            {
                foreach (RoomMember m in r.Members)
                    if (!_knownMembers.Contains(m.PlayerId)) onlineView.AppendLog(string.Format(HudText.OnlineJoinedFormat, NameOf(r, m.PlayerId)));
                foreach (string gone in _knownMembers.Where(id => !now.Contains(id)))
                    onlineView.AppendLog(string.Format(HudText.OnlineLeftFormat, gone.Length > 6 ? gone.Substring(0, 6) : gone));
            }
            _knownMembers = now;
        }

        private void OnPingClicked()
        {
            if (_room.State != RoomConnectionState.InRoom) return;
            RoomSnapshot r = _room.Room;
            // 기록을 먼저: 방장은 자기 신호를 바로 처리해 Pong 기록이 곧장 붙는다
            onlineView.AppendLog(string.Format(HudText.OnlinePingSentFormat, NameOf(r, r.LocalPlayerId)));
            _room.SendToHost(RoomProtocol.Ping(r.LocalPlayerId));
        }

        /// <summary>받은 메시지: 규칙(Core)이 답을 주면 방장이 모두에게, Pong이면 기록 줄.</summary>
        private void OnRoomMessage(NetMessage m)
        {
            RoomSnapshot r = _room.Room;
            NetMessage reply = RoomProtocol.Handle(m, r);
            if (reply != null) _room.Broadcast(reply);
            string pongFor = RoomProtocol.PongTarget(m, r);
            if (pongFor != null) onlineView.AppendLog(string.Format(HudText.OnlinePongFormat, NameOf(r, pongFor)));
        }

        private static string NameOf(RoomSnapshot r, string playerId)
        {
            RoomMember m = r.Find(playerId);
            return m != null && !string.IsNullOrEmpty(m.Name) ? m.Name : HudText.OnlineUnknownName;
        }

        private async void OnDestroy()
        {
            if (_room != null && _room.State == RoomConnectionState.InRoom) await _room.LeaveAsync();
        }
    }
}
