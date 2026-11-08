using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Online;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Moa.Showdown.Game.Online
{
    /// <summary>
    /// <see cref="IRoomTransport"/>의 UGS 구현 (O-1): 익명 로그인 → Multiplayer Services 세션(Lobby + Relay)으로 방 만들기 · 코드 참가 · 빠른 참가,
    /// 메시지는 Netcode for GameObjects 사용자 메시지(이름 "moa", JSON 글 한 통). 규칙은 모른다 — 보내고 받기만.
    /// 씬에 NetworkManager(+ UnityTransport)가 있어야 한다 (메뉴 MoaShowdown/Setup/Online).
    /// </summary>
    public sealed class UgsRoomTransport : IRoomTransport
    {
        public const string MessageName = "moa";
        private const string NameProperty = "name";

        private ISession _session;
        /// <summary>메시지 처리기를 등록한 Netcode 사용자 메시지 관리자 (Netcode가 다시 시작되면 새 객체 → 다시 등록).</summary>
        private CustomMessagingManager _registeredOn;
        private string _lastPlayerName;

        public RoomConnectionState State { get; private set; } = RoomConnectionState.Offline;
        public RoomSnapshot Room { get; private set; } = new RoomSnapshot();
        public string LastError { get; private set; }

        public bool NetworkReady
        {
            get
            {
                NetworkManager nm = NetworkManager.Singleton;
                if (_session == null || nm == null || !nm.IsListening) return false;
                RegisterHandler(); // 방장 넘기기로 Netcode가 다시 시작됐으면 처리기를 다시 단다
                return true;
            }
        }

        /// <summary>
        /// 방장 넘기기 데이터 (O-3). 참가자 모두가 마지막 판 상태를 이미 받아 갖고 있으므로 서비스에 올리는 것은 작은 표시뿐이고, 새 방장은 자기 사본으로 이어받는다.
        /// </summary>
        private sealed class MigrationMarker : IMigrationDataHandler
        {
            public byte[] Generate() => System.Text.Encoding.UTF8.GetBytes("moa-showdown");
            public void Apply(byte[] migrationData) { }
        }

        public event Action RoomChanged;
        public event Action<NetMessage> MessageReceived;

        public async Task CreateAsync(string playerName, int maxPlayers)
        {
            await Run(async () =>
            {
                var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = false }.WithRelayNetwork().WithHostMigration(new MigrationMarker());
                SetName(options, playerName);
                return await MultiplayerService.Instance.CreateSessionAsync(options);
            });
        }

        public async Task JoinByCodeAsync(string code, string playerName)
        {
            string normalized = RoomRules.NormalizeCode(code);
            if (normalized == null)
            {
                Fail("참가 코드가 비어 있음");
                return;
            }
            await Run(async () =>
            {
                var options = new JoinSessionOptions();
                SetName(options, playerName);
                return await MultiplayerService.Instance.JoinSessionByCodeAsync(normalized, options);
            });
        }

        public async Task QuickJoinAsync(string playerName, int maxPlayers)
        {
            await Run(async () =>
            {
                var quick = new QuickJoinOptions { CreateSession = true, Timeout = TimeSpan.FromSeconds(5) };
                var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = false }.WithRelayNetwork().WithHostMigration(new MigrationMarker());
                SetName(options, playerName);
                return await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
            });
        }

        public async Task LeaveAsync()
        {
            ISession s = _session;
            Detach();
            if (s != null)
            {
                try
                {
                    await s.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Online] 나가기 실패 (무시): {e.Message}");
                }
            }
            State = RoomConnectionState.Offline;
            Room = new RoomSnapshot();
            RoomChanged?.Invoke();
        }

        public async Task SetLockedAsync(bool locked)
        {
            if (_session == null || !_session.IsHost) return;
            try
            {
                IHostSession host = _session.AsHost();
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] 방 잠금 실패 (무시): {e.Message}");
            }
        }

        public void SendToHost(NetMessage m)
        {
            if (Room.IsLocalHost)
            {
                Deliver(m);
                return;
            }
            Send(m, NetworkManager.ServerClientId);
        }

        public void Broadcast(NetMessage m)
        {
            if (!Room.IsLocalHost) throw new InvalidOperationException("방장만 모두에게 보낼 수 있음");
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                string json = NetMessageJson.Serialize(m);
                using var writer = Writer(json);
                // 방장 자신은 Netcode 메시지를 받지 않으므로 다른 참가자에게만 보내고 나는 바로 전달
                var others = nm.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId).ToList();
                if (others.Count > 0) nm.CustomMessagingManager.SendNamedMessage(MessageName, others, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            Deliver(m);
        }

        // ───────── 내부 ─────────

        /// <summary>로그인 → 세션 열기/들어가기 → 이벤트 연결. 실패는 State = Failed + LastError (예외를 밖으로 던지지 않음).</summary>
        private async Task Run(Func<Task<ISession>> open)
        {
            if (State == RoomConnectionState.Connecting) return;
            if (_session != null) await LeaveAsync();
            State = RoomConnectionState.Connecting;
            LastError = null;
            RoomChanged?.Invoke();
            try
            {
                await EnsureSignedIn();
                ISession s = await open();
                Attach(s);
                State = RoomConnectionState.InRoom;
                Rebuild();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] {e}");
                Fail(e.Message);
            }
        }

        private static async Task EnsureSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var init = new InitializationOptions();
#if UNITY_EDITOR
                // Multiplayer Play Mode의 가상 플레이어(별도 프로세스)끼리 같은 익명 ID가 되지 않게 프로세스마다 프로필을 나눈다
                init.SetProfile("p" + System.Diagnostics.Process.GetCurrentProcess().Id);
#endif
                await UnityServices.InitializeAsync(init);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void SetName(BaseSessionOptions options, string playerName)
        {
            _lastPlayerName = playerName;
            options.PlayerProperties[NameProperty] = new PlayerProperty(playerName, VisibilityPropertyOptions.Member);
        }

        private void Attach(ISession s)
        {
            _session = s;
            s.Changed += Rebuild;
            s.PlayerJoined += OnPlayerEvent;
            s.PlayerHasLeft += OnPlayerEvent;
            s.SessionHostChanged += OnPlayerEvent;
            s.RemovedFromSession += OnLost;
            s.Deleted += OnLost;
            s.SessionMigrated += OnMigrated;
            if (s.Network != null) s.Network.MigrationFailed += OnMigrationFailed;
            RegisterHandler();
        }

        /// <summary>방장 넘기기가 끝남: Netcode가 새로 시작됐으니 처리기를 다시 달고 방 모습을 새로.</summary>
        private void OnMigrated()
        {
            _registeredOn = null;
            Rebuild();
        }

        private void OnMigrationFailed(SessionError error)
        {
            Detach();
            Fail($"방장 넘기기 실패 ({error})");
        }

        private void Detach()
        {
            if (_session != null)
            {
                _session.Changed -= Rebuild;
                _session.PlayerJoined -= OnPlayerEvent;
                _session.PlayerHasLeft -= OnPlayerEvent;
                _session.SessionHostChanged -= OnPlayerEvent;
                _session.RemovedFromSession -= OnLost;
                _session.Deleted -= OnLost;
                _session.SessionMigrated -= OnMigrated;
                if (_session.Network != null) _session.Network.MigrationFailed -= OnMigrationFailed;
            }
            _session = null;
            UnregisterHandler();
        }

        private void OnPlayerEvent(string _) => Rebuild();

        private void OnLost()
        {
            Detach();
            Fail("방이 사라졌거나 방에서 나가졌습니다");
        }

        private void Fail(string message)
        {
            LastError = message;
            State = RoomConnectionState.Failed;
            Room = new RoomSnapshot();
            RoomChanged?.Invoke();
        }

        /// <summary>세션 → 방 모습 (방장 먼저, 나머지는 세션 목록 순서).</summary>
        private void Rebuild()
        {
            if (_session == null) return;
            var room = new RoomSnapshot
            {
                Code = _session.Code,
                MaxPlayers = _session.MaxPlayers,
                LocalPlayerId = AuthenticationService.Instance.PlayerId,
            };
            foreach (IReadOnlyPlayer p in _session.Players)
            {
                string name = p.Properties != null && p.Properties.TryGetValue(NameProperty, out PlayerProperty prop) ? prop.Value : null;
                if (string.IsNullOrEmpty(name) && p.Id == room.LocalPlayerId) name = _lastPlayerName;
                room.Members.Add(new RoomMember { PlayerId = p.Id, Name = name, IsHost = p.Id == _session.Host });
            }
            room.Members = room.Members.OrderByDescending(m => m.IsHost).ToList();
            Room = room;
            RegisterHandler(); // 세션이 Netcode를 늦게 시작하는 경우를 위해 매번 확인
            RoomChanged?.Invoke();
        }

        private void RegisterHandler()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            if (ReferenceEquals(_registeredOn, nm.CustomMessagingManager)) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
            _registeredOn = nm.CustomMessagingManager;
        }

        private void UnregisterHandler()
        {
            if (_registeredOn == null) return;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && ReferenceEquals(nm.CustomMessagingManager, _registeredOn)) nm.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            _registeredOn = null;
        }

        private void OnNamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string json);
            if (!NetMessageJson.TryDeserialize(json, out NetMessage m))
            {
                Debug.LogWarning($"[Online] 형식이 틀린 메시지를 버림 (보낸 연결 {senderClientId})");
                return;
            }
            Deliver(m);
        }

        private void Deliver(NetMessage m) => MessageReceived?.Invoke(m);

        private void Send(NetMessage m, ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                Debug.LogWarning("[Online] 아직 연결 안 됨 — 메시지를 보내지 못함");
                return;
            }
            string json = NetMessageJson.Serialize(m);
            using var writer = Writer(json);
            nm.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        /// <summary>JSON 글 한 통을 담는 쓰기 버퍼 (글자 수 × 2 + 길이 머리, 넉넉히).</summary>
        private static FastBufferWriter Writer(string json)
        {
            int size = json.Length * 2 + 16;
            var writer = new FastBufferWriter(size, Allocator.Temp, NetMessageJson.MaxLength * 2 + 16);
            writer.WriteValueSafe(json);
            return writer;
        }
    }
}
