using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Shop;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// Online 모드 판 진행 (O-2). 방장 = Core <see cref="HostSession"/>이 판을 돌리고 바뀔 때마다 <see cref="LobbySync"/>를 모두에게 보냄.
    /// 참가자(방장 자신 포함)는 받은 상태를 그대로 그리기만 하고, 누른 것은 <see cref="LobbyAction"/>으로 방장에게 보낸다.
    /// 전투는 각자 기록(대진 · 팀 · 시드)으로 다시 계산해 재생하고 방장 결과와 다르면 경고 (CLAUDE 핵심 규칙 2).
    /// 화면 그리기는 오프라인 로비(<c>GameRoot.Lobby.cs</c>)의 것을 그대로 쓴다.
    /// </summary>
    public sealed partial class GameRoot
    {
        [Header("온라인 판 (O-2, BALANCE 18)")]
        [Tooltip("캐러셀이 끝난 뒤 상점으로 넘어가기 전 보여 주는 시간 (초)")]
        [SerializeField] private float onlineCarouselDoneSeconds = 3f;
        [Tooltip("결과 단계 최대 시간 = 가장 긴 전투(1배속) + 이 값 (초)")]
        [SerializeField] private float onlineResultGraceSeconds = 20f;

        private HostSession _host;
        private LobbySync _lastSync;
        private int _actionSeq;
        private bool _inOnlineGame;
        private OnlinePhase _shownPhase = OnlinePhase.Waiting;
        private int _shownRound;
        private float _onlineLeft;
        private readonly List<int> _mismatches = new List<int>();

        /// <summary>방장일 때 판 진행 (확인 · 테스트용, 아니면 null).</summary>
        public HostSession HostForCheck => _host;
        /// <summary>마지막으로 받은 판 상태 (확인 · 테스트용).</summary>
        public LobbySync LastSync => _lastSync;
        public bool InOnlineGame => _inOnlineGame;
        public IReadOnlyList<int> LastMismatches => _mismatches;

        private int OnlineSecondsLeft => Mathf.CeilToInt(Mathf.Max(0f, _onlineLeft));

        // ───────── 방장 ─────────

        private void OnStartGameClicked()
        {
            if (_room == null || _room.State != RoomConnectionState.InRoom || !_room.Room.IsLocalHost || _inOnlineGame) return;
            RoomSnapshot r = _room.Room;
            var humans = r.Members.Take(_lobby.Rules.PlayerCount)
                .Select(m => (m.PlayerId, string.IsNullOrEmpty(m.Name) ? HudText.OnlineUnknownName : m.Name))
                .ToList();
            _host = new HostSession(_lobby, new HostSessionRules
            {
                CarouselDoneSeconds = onlineCarouselDoneSeconds,
                ResultGraceSeconds = onlineResultGraceSeconds,
                TicksPerSecond = _rules.TicksPerSecond,
            }, balance.rulesVersion);
            ulong seed = runSeed != 0 ? (ulong)runSeed : (ulong)DateTime.UtcNow.Ticks;
            _host.Start($"online_{r.Code}_{DateTime.UtcNow:yyyyMMddHHmmss}", seed, humans);
            _ = _room.SetLockedAsync(true);
            BroadcastSync();
        }

        private void BroadcastSync()
        {
            if (_host == null || _room == null || !_room.Room.IsLocalHost) return;
            LobbySync s = _host.MakeSync();
            _room.Broadcast(new NetMessage { Type = NetMessageTypes.Sync, From = _room.Room.LocalPlayerId, Body = OnlineLobbyJson.SerializeSync(s) });
        }

        /// <summary>매 프레임: 방장이면 판 시간을 흘리고 바뀌었으면 보냄, 모두 남은 시간 표시.</summary>
        private void TickOnline()
        {
            if (mode != Mode.Online) return;
            if (_host != null && _host.Phase != OnlinePhase.Waiting && _host.Phase != OnlinePhase.Finished)
            {
                _host.Tick(Time.deltaTime);
                if (_host.Dirty) BroadcastSync();
            }
            if (!_inOnlineGame) return;
            _onlineLeft -= Time.deltaTime;
            bool warn = OnlineSecondsLeft <= prepWarnSeconds;
            if (_shownPhase == OnlinePhase.Shop)
            {
                if (shopView.IsVisible) shopView.SetTimer(OnlineSecondsLeft, warn);
                if (_scout >= 0 && _scoutFromShop) lobbyView.SetScoutTimer(OnlineSecondsLeft, warn);
            }
            else if (_shownPhase == OnlinePhase.Carousel && carouselView.IsVisible)
            {
                bool myTurn = Lobby?.Carousel != null && Lobby.Carousel.CurrentPicker == MeIndex;
                carouselView.SetTimer(myTurn ? OnlineSecondsLeft : -1, warn);
            }
        }

        // ───────── 메시지 ─────────

        private void OnGameMessage(NetMessage m, RoomSnapshot r)
        {
            if (m.Type == NetMessageTypes.Action)
            {
                if (_host == null || !r.IsLocalHost) return;
                LobbyAction a;
                try
                {
                    a = OnlineLobbyJson.DeserializeAction(m.Body);
                }
                catch (NetMessageFormatException e)
                {
                    Debug.LogWarning($"[Online] 잘못된 행동을 버림: {e.Message}");
                    return;
                }
                _host.Apply(m.From, a);
                if (_host.Dirty) BroadcastSync();
                return;
            }
            // Sync: 방장이 보낸 것만
            RoomMember sender = r.Find(m.From);
            if (sender == null || !sender.IsHost) return;
            LobbySync s;
            try
            {
                s = OnlineLobbyJson.DeserializeSync(m.Body);
            }
            catch (NetMessageFormatException e)
            {
                Debug.LogWarning($"[Online] 잘못된 판 상태를 버림: {e.Message}");
                return;
            }
            ApplySync(s);
        }

        private void SendOnlineAction(LobbyAction a)
        {
            if (_room == null || _room.State != RoomConnectionState.InRoom || !_inOnlineGame) return;
            a.Seq = ++_actionSeq;
            _room.SendToHost(new NetMessage { Type = NetMessageTypes.Action, From = _room.Room.LocalPlayerId, Body = OnlineLobbyJson.SerializeAction(a) });
        }

        // ───────── 받은 상태 그리기 ─────────

        private void ApplySync(LobbySync s)
        {
            if (_lastSync != null && s.Version <= _lastSync.Version) return; // 늦게 온 옛 상태
            if (s.RulesVersion != balance.rulesVersion)
            {
                onlineView.SetStatus(string.Format(HudText.OnlineVersionMismatchFormat, s.RulesVersion, balance.rulesVersion));
                return;
            }
            int seat = s.SeatOf(_room.Room.LocalPlayerId);
            if (seat < 0) return; // 판에 자리가 없음 (구경꾼은 아직 없음)

            bool first = !_inOnlineGame;
            int livesBefore = !first && State != null ? State.Lives : 0;
            _lastSync = s;
            MeIndex = seat;
            Lobby = s.Lobby;
            State = Lobby.Players[seat].Run;
            _onlineLeft = s.PhaseMs / 1000f;
            ReadAcks(s, seat);

            if (first)
            {
                _inOnlineGame = true;
                _shownPhase = OnlinePhase.Waiting;
                onlineView.SetVisible(false);
                LastLobbyReport = null;
                if (livesBefore == 0) livesBefore = State.Lives;
            }

            bool changed = s.Phase != _shownPhase || Lobby.Round != _shownRound;
            _shownPhase = s.Phase;
            _shownRound = Lobby.Round;
            switch (s.Phase)
            {
                case OnlinePhase.Carousel:
                    if (changed) OnlineShowCarousel();
                    else RefreshCarousel();
                    break;
                case OnlinePhase.Shop:
                    if (changed) OnlineShowShop();
                    else if (_scout < 0 && shopView.IsVisible) RefreshShop();
                    break;
                case OnlinePhase.Battle:
                    if (changed) OnlineShowBattle(s.Report, livesBefore);
                    break;
                case OnlinePhase.Finished:
                    if (changed) OnlineShowFinished();
                    break;
            }
            if (s.Phase != OnlinePhase.Carousel) RefreshStandings();
        }

        /// <summary>내 행동 결과를 안내 글로 (오프라인과 같은 문구).</summary>
        private void ReadAcks(LobbySync s, int seat)
        {
            foreach (ActionAck a in s.Acks.Where(x => x.Player == seat))
            {
                switch (a.Kind)
                {
                    case LobbyActionKind.Buy:
                        _shopMessage = !a.Ok ? ErrorText(a.ShopError)
                            : a.LevelAfter > a.LevelBefore && a.LevelAfter > 1 ? string.Format(HudText.MergedFormat, NameOf(a.SpeciesId), a.LevelAfter)
                            : string.Format(HudText.BoughtFormat, NameOf(a.SpeciesId));
                        break;
                    case LobbyActionKind.Reroll:
                        _shopMessage = a.Ok ? "" : ErrorText(a.ShopError);
                        break;
                    case LobbyActionKind.Sell:
                        if (a.Ok) _shopMessage = string.Format(HudText.SoldFormat, NameOf(a.SpeciesId), a.Gold);
                        break;
                    case LobbyActionKind.Ready:
                        if (a.Ok) _shopMessage = HudText.WaitingOthers;
                        break;
                    case LobbyActionKind.CarouselPick:
                        if (!a.Ok) break;
                        int mine = Lobby.Carousel?.TakenBy.IndexOf(seat) ?? -1;
                        string id = mine >= 0 ? Lobby.Carousel.Units[mine] : a.SpeciesId;
                        string msg = id == null ? "" : a.Carousel == CarouselPickResult.PickedAsGold
                            ? string.Format(HudText.CarouselPickedAsGoldFormat, NameOf(id), _shop.PriceOf(id))
                            : string.Format(HudText.CarouselPickedFormat, NameOf(id));
                        _carouselMessage = a.Seq < 0 ? HudText.CarouselAutoPicked + "  ·  " + msg : msg;
                        break;
                }
            }
        }

        private void OnlineShowCarousel()
        {
            EndScout();
            _showOpponents = false;
            _carouselMessage = "";
            _playback = null;
            _roundResultPending = false;
            shopView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            hud.ShowStatus("");
            hud.SetSideLabels(HudText.MySideLabel, "");
            hud.ShowShopHeader(Lobby.Round, "", _rules.GetTeamHp(Lobby.Round));
            ShowRunInfo(State.Wins, State.Lives);
            lobbyView.SetListVisible(false); // 캐러셀 판과 겹침 — 순서 줄이 대신
            RefreshCarousel();
        }

        private void OnlineShowShop()
        {
            carouselView.Hide();
            EndScout();
            _showOpponents = false;
            lobbyView.SetListVisible(true);
            _selected = null;
            _shopMessage = "";
            _emptyBoardWarned = false;
            _playback = null;
            _roundResultPending = false;
            tooltip.HideAll();
            hud.SetSideLabels(HudText.MySideLabel, "");
            hud.HideRoundResult();
            bool pve = _lobby.IsPveRound(Lobby);
            hud.ShowShopHeader(Lobby.Round, pve ? HudText.ShopHeaderPve : HudText.ShopHeader, _rules.GetTeamHp(Lobby.Round));
            ShowRunInfo(State.Wins, State.Lives);
            if (Me.IsAlive)
            {
                hud.ShowStatus("");
                shopView.SetStartLabel(HudText.ReadyButton);
                RefreshShop();
                shopView.SetTimer(OnlineSecondsLeft, OnlineSecondsLeft <= prepWarnSeconds);
            }
            else
            {
                // 탈락: 상점 없이 목록 · 구경만
                shopView.Hide();
                hud.ShowStatus(HudText.OnlineSpectating);
                stage.Build(new BattleTeam(), new BattleTeam(), unchecked((ulong)State.Seed));
                stage.Tick(0f, 0f, true);
            }
        }

        private void OnlineShowBattle(RoundReportRecord record, int livesBefore)
        {
            shopView.Hide();
            carouselView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            EndScout();
            lobbyView.SetListVisible(true);
            _mismatches.Clear();
            if (record == null)
            {
                hud.ShowStatus("");
                return;
            }
            LastLobbyReport = record.Rebuild(_rules, _catalog, _statRules, _mismatches);
            ShowRunInfo(State.Wins, livesBefore);
            _showOpponents = true;
            string warning = _mismatches.Count > 0 ? string.Format(HudText.OnlineMismatchFormat, _mismatches.Count) : "";
            if (_mismatches.Count > 0) Debug.LogWarning($"[Online] 방장과 다른 전투 결과: 대전 {string.Join(", ", _mismatches)}");

            LobbyMatch m = LastLobbyReport.MatchOf(MeIndex);
            if (m == null)
            {
                // 이미 탈락해 내 대전이 없음
                _playback = null;
                hud.ShowStatus(warning);
                ShowLobbyRoundResult();
                return;
            }
            _lastSignature = Signature(m.Battle);
            hud.ShowStatus(warning);
            PlayMyMatch(m, m.Battle);
            _roundResultPending = true;
        }

        private void OnlineShowFinished()
        {
            if (_host != null) _ = _room.SetLockedAsync(false);
            // 결과 패널을 이미 넘겼으면 마지막 순위를 다시 보여 줌
            if (_playback == null || _playback.IsFinished)
            {
                _roundResultPending = false;
                if (LastLobbyReport != null) ShowLobbyRoundResult();
                else hud.ShowRoundResult(string.Format(HudText.OnlineFinishedFormat, Me.Placement), HudText.OnlineBackToRoom);
            }
        }

        /// <summary>결과 패널 버튼: 판이 끝났으면 대기실로, 아니면 "다음 라운드"(다른 사람을 기다림).</summary>
        private void OnlineContinue()
        {
            if (!_inOnlineGame || _lastSync == null) return;
            if (_lastSync.Phase == OnlinePhase.Finished || Lobby.IsFinished)
            {
                OnlineEndGame();
                return;
            }
            // 화면을 먼저 바꾸고 보낸다: 마지막으로 누른 사람이면 방장이 곧바로 다음 라운드로 넘긴다
            hud.HideRoundResult();
            hud.ShowStatus(HudText.WaitingOthers);
            SendOnlineAction(LobbyAction.Of(LobbyActionKind.Continue));
        }

        /// <summary>판 화면을 닫고 대기실로 (방은 그대로).</summary>
        private void OnlineEndGame()
        {
            _inOnlineGame = false;
            _lastSync = null;
            _shownPhase = OnlinePhase.Waiting;
            _shownRound = 0;
            if (_host != null && _room != null && _room.State == RoomConnectionState.InRoom) _ = _room.SetLockedAsync(false);
            _host = null;
            EndScout();
            _playback = null;
            _roundResultPending = false;
            shopView.Hide();
            carouselView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            hud.ShowStatus("");
            lobbyView.SetListVisible(false);
            stage.Build(new BattleTeam(), new BattleTeam(), 0);
            stage.Tick(0f, 0f, true);
            onlineView.SetVisible(true);
        }
    }
}
