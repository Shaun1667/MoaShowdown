using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Snapshot;
using Moa.Showdown.Game.Shop;
using Moa.Showdown.Game.Ui;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// Lobby 모드 (L-1, TFT 흐름): 나(0번) + 봇 7명. 진행 규칙은 Core의 <see cref="LobbyController"/>, 여기서는 화면 연결만.
    /// 라운드: EnterShop(모두 수입 · 상점, 봇 행동) → 나는 상점 · 배치 → 전투 시작 = ResolveRound(모든 대진 계산) → 내 대진만 재생 → 결과 패널.
    /// 내가 기록상 B쪽이면 화면에서는 뒤집어 왼쪽에 나를 그린다.
    /// </summary>
    public sealed partial class GameRoot
    {
        /// <summary>내 자리 번호 (오프라인 = 0, 온라인 = 방장이 정한 자리).</summary>
        public int MeIndex { get; private set; }

        private LobbyController _lobby;

        /// <summary>지금 로비 상태 (Lobby 모드).</summary>
        public LobbyState Lobby { get; private set; }
        public LobbyRoundReport LastLobbyReport { get; private set; }
        public LobbyController LobbyCtl => _lobby;

        private LobbyPlayer Me => Lobby.Players[MeIndex];

        /// <summary>내 상점 조작에 쓰는 공유 풀 (Lobby 모드만, 아니면 null = 무한 상점).</summary>
        /// <remarks>Online에서는 방장이 보낸 상태의 풀 — 보기만 한다 (조작은 방장이).</remarks>
        private Moa.Showdown.Core.Shop.UnitPool CurrentPool => LobbyLike && Lobby != null ? LobbyController.PoolOf(Lobby) : null;

        public void StartNewLobby()
        {
            ulong s = runSeed != 0 ? (ulong)runSeed : (ulong)DateTime.UtcNow.Ticks;
            string id = $"lobby_{DateTime.UtcNow:yyyyMMddHHmmss}_{s & 0xFFFF:x4}";
            Lobby = _lobby.NewLobby(id, s, new[] { HudText.MyName }, n => string.Format(HudText.BotNameFormat, n));
            State = Me.Run;
            LastLobbyReport = null;
            _lastSignature = null;
            ShowRunInfo(State.Wins, State.Lives);
            RefreshStandings();
        }

        // ───────── 준비 시간 (L-3) ─────────

        private readonly PrepTimer _prep = new PrepTimer();

        /// <summary>지금 준비 시간 (확인 · 테스트용).</summary>
        public PrepTimer Prep => _prep;

        /// <summary>상점 시작 때: 봇 · 탈락자는 처음부터 준비 완료. 타이머를 끈 경우 버튼은 "전투 시작".</summary>
        private void StartPrepTimer()
        {
            if (!usePrepTimer)
            {
                _prep.Stop();
                shopView.SetTimer(-1, false);
                shopView.SetStartLabel(HudText.StartBattle);
                return;
            }
            var autoReady = Lobby.Players.Where(p => p.IsBot || !p.IsAlive).Select(p => p.Index);
            _prep.Start(Lobby.Players.Count, _lobby.PrepSeconds, autoReady);
            shopView.SetStartLabel(HudText.ReadyButton);
            shopView.SetTimer(_prep.DisplaySeconds, _prep.DisplaySeconds <= prepWarnSeconds);
        }

        private void StopPrepTimer()
        {
            _prep.Stop();
            shopView.SetTimer(-1, false);
        }

        /// <summary>매 프레임: 상점 단계(상점 화면 · 상점에서 들어간 구경)에만 시간이 흐르고, 시작 조건이 되면 전투 (시간 끝 = 보드 그대로, 경고 없이).</summary>
        private void TickPrepTimer()
        {
            bool shopPhase = shopView.IsVisible || (_scout >= 0 && _scoutFromShop);
            if (mode != Mode.Lobby || !_prep.IsRunning || !shopPhase) return;
            _prep.Advance(Time.deltaTime);
            bool warn = _prep.DisplaySeconds <= prepWarnSeconds;
            shopView.SetTimer(_prep.DisplaySeconds, warn);
            lobbyView.SetScoutTimer(_scout >= 0 ? _prep.DisplaySeconds : -1, warn);
            if (_prep.ShouldStart)
            {
                EndScout();
                StartBattleNow(force: true);
            }
        }

        /// <summary>준비 완료 버튼: 나를 준비 완료로. 모두 준비되면 다음 프레임에 시작 (오프라인은 봇이 이미 준비 → 바로).</summary>
        private void LobbyReady()
        {
            _prep.SetReady(MeIndex);
            if (_prep.ShouldStart) StartBattleNow(force: true);
            else
            {
                _shopMessage = HudText.WaitingOthers;
                RefreshShop();
            }
        }

        /// <summary>결과 패널 계속: 내가 살아 있고 로비가 안 끝났으면 다음 라운드(캐러셀 → 상점), 아니면 새 로비.</summary>
        private void LobbyContinue()
        {
            if (Lobby.IsFinished || !Me.IsAlive) StartNewLobby();
            EnterLobbyRound();
            RefreshStandings();
        }

        /// <summary>라운드 시작: 캐러셀 라운드면 캐러셀부터, 아니면 바로 상점.</summary>
        private void EnterLobbyRound()
        {
            if (_lobby.CarouselPending(Lobby)) EnterCarousel();
            else EnterShop();
        }

        // ───────── 캐러셀 (L-4) ─────────

        private readonly PrepTimer _carouselTimer = new PrepTimer();
        private string _carouselMessage = "";

        /// <summary>지금 캐러셀 고르기 시간 (확인 · 테스트용).</summary>
        public PrepTimer CarouselTimer => _carouselTimer;

        /// <summary>캐러셀 열기: 내 앞 순서의 봇은 바로 고르고, 내 차례면 시간이 흐른다.</summary>
        private void EnterCarousel()
        {
            _lobby.StartCarousel(Lobby);
            _lobby.AdvanceCarouselBots(Lobby);
            EndScout();
            _showOpponents = false;
            _carouselMessage = "";
            _playback = null;
            _roundResultPending = false;
            StopPrepTimer();
            shopView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            hud.ShowStatus("");
            hud.SetSideLabels(HudText.MySideLabel, "");
            hud.ShowShopHeader(State.Round, "", _rules.GetTeamHp(State.Round));
            ShowRunInfo(State.Wins, State.Lives);
            RefreshStandings();
            lobbyView.SetListVisible(false); // 캐러셀 판과 겹침 — 순서 줄이 대신 보여 줌
            StartCarouselTimerIfMyTurn();
            RefreshCarousel();
        }

        private void StartCarouselTimerIfMyTurn()
        {
            _carouselTimer.Stop();
            carouselView.SetTimer(-1, false);
            if (!usePrepTimer || Lobby.Carousel.CurrentPicker != MeIndex) return;
            _carouselTimer.Start(1, _lobby.Rules.CarouselPickSeconds, Array.Empty<int>());
            carouselView.SetTimer(_carouselTimer.DisplaySeconds, _carouselTimer.DisplaySeconds <= prepWarnSeconds);
        }

        /// <summary>매 프레임: 내 차례 시간이 끝나면 봇 규칙으로 대신 고른다.</summary>
        private void TickCarouselTimer()
        {
            if (mode != Mode.Lobby || !_carouselTimer.IsRunning || !carouselView.IsVisible) return;
            _carouselTimer.Advance(Time.deltaTime);
            carouselView.SetTimer(_carouselTimer.DisplaySeconds, _carouselTimer.DisplaySeconds <= prepWarnSeconds);
            if (!_carouselTimer.ShouldStart) return;
            _carouselTimer.Stop();
            CarouselPickResult r = _lobby.AutoPickCarousel(Lobby);
            AfterMyCarouselPick(r, auto: true);
        }

        private void OnCarouselCard(int index)
        {
            if (Lobby?.Carousel == null || Lobby.Carousel.CurrentPicker != MeIndex) return;
            if (mode == Mode.Online)
            {
                SendOnlineAction(Moa.Showdown.Core.Online.LobbyAction.Of(Moa.Showdown.Core.Online.LobbyActionKind.CarouselPick, index));
                return;
            }
            CarouselPickResult r = _lobby.PickCarousel(Lobby, MeIndex, index);
            if (r != CarouselPickResult.Picked && r != CarouselPickResult.PickedAsGold) return;
            _carouselTimer.Stop();
            AfterMyCarouselPick(r, auto: false);
        }

        /// <summary>내가 고른 뒤: 메시지 → 남은 봇이 고름 → 다시 그림.</summary>
        private void AfterMyCarouselPick(CarouselPickResult r, bool auto)
        {
            int mine = MyCarouselPick();
            string id = mine >= 0 ? Lobby.Carousel.Units[mine] : null;
            string msg = id == null ? "" : r == CarouselPickResult.PickedAsGold
                ? string.Format(HudText.CarouselPickedAsGoldFormat, NameOf(id), _shop.PriceOf(id))
                : string.Format(HudText.CarouselPickedFormat, NameOf(id));
            _carouselMessage = auto ? HudText.CarouselAutoPicked + "  ·  " + msg : msg;
            carouselView.SetTimer(-1, false);
            _lobby.AdvanceCarouselBots(Lobby);
            RefreshCarousel();
        }

        private int MyCarouselPick() => Lobby.Carousel.TakenBy.IndexOf(MeIndex);

        private void OnCarouselContinue()
        {
            if (Lobby?.Carousel == null || !Lobby.Carousel.IsDone) return;
            if (mode == Mode.Online) return; // 방장이 잠깐 뒤 상점으로 넘김
            carouselView.Hide();
            EnterShop();
        }

        private void RefreshCarousel()
        {
            CarouselState c = Lobby.Carousel;
            int current = c.CurrentPicker;
            var d = new CarouselViewData
            {
                Order = string.Format(HudText.CarouselOrderFormat, string.Join(HudText.CarouselOrderSeparator,
                    c.Order.Select(i => i == current ? $"[{Lobby.Players[i].Name}]" : Lobby.Players[i].Name))),
                Status = c.IsDone ? HudText.CarouselDone
                    : current == MeIndex ? HudText.CarouselMyTurn
                    : string.Format(HudText.CarouselWaitingFormat, Lobby.Players[current].Name),
                Message = _carouselMessage,
                CanPick = !c.IsDone && current == MeIndex,
                Done = c.IsDone,
                Cards = c.Units.Select((id, i) => new CarouselCard
                {
                    Text = c.TakenBy[i] >= 0
                        ? string.Format(HudText.CarouselTakenFormat, NameOf(id), Lobby.Players[c.TakenBy[i]].Name)
                        : string.Format(HudText.CarouselCardFormat, NameOf(id), RarityName(id)),
                    Taken = c.TakenBy[i] >= 0,
                    Mine = c.TakenBy[i] == MeIndex,
                }).ToArray(),
            };
            carouselView.Show(d);

            // 보드 미리 보기: 내 팀만
            ShowBoardPreview(State);
        }

        /// <summary>캐러셀 카드 정보 창: 레벨 1 기준 능력치 · 이미 가진 수.</summary>
        private (string, string)? CarouselCardInfo(int index)
        {
            CarouselState c = Lobby?.Carousel;
            if (c == null || index < 0 || index >= c.Units.Count) return null;
            return MonsterInfoText(new MonsterSnapshot { SpeciesId = c.Units[index], Level = 1 }, enemy: false, price: null, mine: false, mergePreview: true);
        }

        /// <summary>전투 시작: 로비 전체 라운드를 계산하고 내 대진을 재생.</summary>
        private void PlayLobbyRound()
        {
            shopView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            int livesBefore = State.Lives;
            EndScout();
            LobbyRoundReport report = _lobby.ResolveRound(Lobby);
            LastLobbyReport = report;
            ShowRunInfo(State.Wins, livesBefore);
            _showOpponents = true; // 전투 · 결과 화면: 목록에 이번 상대

            LobbyMatch m = report.MatchOf(MeIndex);
            if (m == null)
            {
                _playback = null;
                ShowLobbyRoundResult();
                return;
            }
            _lastSignature = Signature(m.Battle);
            hud.ShowStatus("");
            PlayMyMatch(m, m.Battle);
            _roundResultPending = true;
            RefreshStandings();
        }

        private void PlayMyMatch(LobbyMatch m, BattleResult battle)
        {
            bool flip = m.PlayerB == MeIndex;
            BattleTeam mine = flip ? m.TeamB : m.TeamA;
            BattleTeam theirs = flip ? m.TeamA : m.TeamB;
            PlayBattle(mine, theirs, battle, LastLobbyReport.Round, HudText.MyTeam, OpponentName(m), m.Seed, flip,
                SpeciesIds((flip ? m.SnapshotB : m.SnapshotA)?.Board), SpeciesIds((flip ? m.SnapshotA : m.SnapshotB)?.Board));
        }

        private void ReplayLobbyMatch()
        {
            LobbyMatch m = LastLobbyReport?.MatchOf(MeIndex);
            if (m == null) return;
            BattleResult again = BattleSimulator.Run(_rules, m.TeamA, m.TeamB, LastLobbyReport.Round, m.Seed);
            hud.ShowStatus(Signature(again) == Signature(m.Battle) ? HudText.SameResult : HudText.DifferentResult);
            hud.HideRoundResult();
            PlayMyMatch(m, again);
            _roundResultPending = true;
            RefreshStandings();
        }

        private void ShowLobbyRoundResult()
        {
            LobbyRoundReport r = LastLobbyReport;
            ShowRunInfo(State.Wins, State.Lives);
            RefreshStandings();
            LobbyMatch m = r.MatchOf(MeIndex);

            var lines = new List<string>();
            if (m != null)
            {
                bool flip = m.PlayerB == MeIndex;
                BattleOutcome o = m.Battle.Outcome;
                bool won = flip ? o == BattleOutcome.BWins : o == BattleOutcome.AWins;
                bool lost = flip ? o == BattleOutcome.AWins : o == BattleOutcome.BWins;
                int myLoss = flip ? m.LivesLostB : m.LivesLostA;
                if (m.IsPve)
                    lines.Add(won ? string.Format(HudText.PveWinFormat, m.GoldGainedA)
                        : lost ? string.Format(HudText.PveLoseFormat, myLoss, State.Lives, m.GoldGainedA)
                        : string.Format(HudText.PveDrawFormat, m.GoldGainedA));
                else lines.Add(won ? string.Format(HudText.LobbyWinFormat, OpponentName(m))
                    : lost ? string.Format(HudText.RoundLoseFormat, myLoss, State.Lives)
                    : HudText.RoundDraw);
            }
            foreach (int idx in r.Eliminated)
            {
                LobbyPlayer p = Lobby.Players[idx];
                if (idx != MeIndex) lines.Add(string.Format(HudText.LobbyEliminatedFormat, p.Name, p.Placement));
            }

            // 온라인: 판이 끝났으면 대기실로, 나만 탈락했으면 관전 계속 (오프라인은 새 판)
            string endLabel = mode != Mode.Online ? HudText.NewRun : Lobby.IsFinished ? HudText.OnlineBackToRoom : HudText.OnlineKeepWatching;
            if (Me.Placement == 1)
            {
                hud.ShowBanner(HudText.LobbyFirst);
                hud.ShowRoundResult(string.Format(HudText.LobbyFirstFormat, r.Round), endLabel);
            }
            else if (Me.Placement > 1)
            {
                hud.ShowBanner(string.Format(HudText.PlacementFormat, Me.Placement));
                hud.ShowRoundResult(string.Format(HudText.LobbyOutFormat, r.Round, Me.Placement), endLabel);
            }
            else
            {
                hud.ShowRoundResult(string.Join("\n", lines.Take(3)), HudText.NextRound);
            }
        }

        private string OpponentName(LobbyMatch m)
        {
            bool flip = m.PlayerB == MeIndex;
            if (m.IsPve) return HudText.PveName;
            if (m.IsGhost) return string.Format(HudText.GhostFormat, Lobby.Players[m.GhostOf].Name);
            return Lobby.Players[flip ? m.PlayerA : m.PlayerB].Name;
        }

        // ───────── 로비 목록 · 구경 (L-5) ─────────

        /// <summary>목록에 이번 라운드 상대를 보일지 (전투 · 결과 화면만).</summary>
        private bool _showOpponents;
        /// <summary>구경 중인 플레이어 번호 (-1 = 안 함).</summary>
        private int _scout = -1;
        /// <summary>상점에서 구경을 시작했는지 (그동안 준비 시간은 계속 흐름, 돌아가면 상점).</summary>
        private bool _scoutFromShop;

        /// <summary>지금 구경 중인 플레이어 (확인 · 테스트용, -1 = 안 함).</summary>
        public int ScoutIndex => _scout;

        /// <summary>오른쪽 위 목록 (순서 · 이번 상대는 Core <see cref="LobbyStandings"/>).</summary>
        private void RefreshStandings()
        {
            if (Lobby == null) return;
            // 전투 재생 중(결과 패널 전)에는 이번 라운드 결과를 숨긴다
            List<StandingRow> rows = LobbyStandings.Build(Lobby, _showOpponents ? LastLobbyReport : null, beforeResults: _roundResultPending);
            lobbyView.ShowPlayers(rows.Select(r => new LobbyRowView
            {
                PlayerIndex = r.Index,
                Name = r.Name,
                Right = (r.IsAlive ? string.Format(HudText.RowLivesFormat, r.Lives) : string.Format(HudText.RowPlacementFormat, r.Placement)) + VsText(r),
                Me = r.Index == MeIndex,
                Out = !r.IsAlive,
                Scouted = r.Index == _scout,
            }).ToList());
        }

        private string VsText(StandingRow r)
        {
            switch (r.Opponent)
            {
                case OpponentKind.Player: return string.Format(HudText.RowVsFormat, Lobby.Players[r.OpponentIndex].Name);
                case OpponentKind.Ghost: return string.Format(HudText.RowVsGhostFormat, Lobby.Players[r.OpponentIndex].Name);
                case OpponentKind.Pve: return HudText.RowVsPve;
                default: return "";
            }
        }

        /// <summary>목록 클릭: 다른 사람이면 그 사람 보드 구경, 나 · 지금 구경 중인 사람이면 내 보드로. 전투 재생 · 캐러셀 중에는 안 됨.</summary>
        private void OnLobbyPlayerClicked(int index)
        {
            if (index == MeIndex || index == _scout)
            {
                StopScout();
                return;
            }
            bool battlePlaying = _playback != null && !_playback.IsFinished;
            if (battlePlaying || carouselView.IsVisible || Lobby == null) return;
            if (_scout < 0) _scoutFromShop = shopView.IsVisible;
            _scout = index;
            shopView.Hide();
            hud.HideRoundResult();
            tooltip.HideAll();
            LobbyPlayer p = Lobby.Players[index];
            hud.SetSideLabels(string.Format(HudText.ScoutSideLabelFormat, p.Name), "");
            lobbyView.ShowScout(string.Format(HudText.ScoutTitleFormat, p.Name), p.Run.Bench.Select(UnitText).ToList());
            if (mode == Mode.Online) lobbyView.SetScoutTimer(_scoutFromShop ? OnlineSecondsLeft : -1, OnlineSecondsLeft <= prepWarnSeconds);
            else lobbyView.SetScoutTimer(_scoutFromShop && _prep.IsRunning ? _prep.DisplaySeconds : -1, _prep.DisplaySeconds <= prepWarnSeconds);
            ShowBoardPreview(p.Run);
            RefreshStandings();
        }

        /// <summary>구경 끝 → 원래 화면 (상점 또는 결과 패널 + 내 보드).</summary>
        private void StopScout()
        {
            if (_scout < 0) return;
            bool fromShop = _scoutFromShop;
            EndScout();
            hud.SetSideLabels(HudText.MySideLabel, "");
            tooltip.HideAll();
            if (fromShop) RefreshShop();
            else
            {
                ShowBoardPreview(State);
                if (LastLobbyReport != null)
                {
                    LobbyMatch mm = LastLobbyReport.MatchOf(MeIndex);
                    hud.SetSideLabels(HudText.MySideLabel, mm == null ? "" : string.Format(HudText.OpponentTitleFormat, OpponentName(mm)));
                    ShowLobbyRoundResult();
                }
            }
            RefreshStandings();
        }

        /// <summary>구경 상태만 지운다 (화면 전환이 따로 그릴 때).</summary>
        private void EndScout()
        {
            if (_scout < 0) return;
            _scout = -1;
            _scoutFromShop = false;
            lobbyView.HideScout();
            hud.SetSideLabels(HudText.MySideLabel, "");
        }

        /// <summary>구경 중 대기석 칸 정보 창.</summary>
        private (string, string)? ScoutBenchInfo(int bench)
        {
            if (_scout < 0) return null;
            MonsterSnapshot m = Lobby.Players[_scout].Run.Bench[bench];
            return m == null ? ((string, string)?)null : MonsterInfoText(m, enemy: true, price: null, mine: false);
        }

        private (string, string)? LobbyBattleUnitInfo(BattleSide side, int slot)
        {
            if (_scout >= 0)
            {
                MonsterSnapshot s = side == BattleSide.A ? Lobby.Players[_scout].Run.Team[slot] : null;
                return s == null ? ((string, string)?)null : MonsterInfoText(s, enemy: true, price: null, mine: false);
            }
            LobbyMatch m = LastLobbyReport?.MatchOf(MeIndex);
            if (m == null) return null;
            bool flip = m.PlayerB == MeIndex;
            TeamSnapshot mine = flip ? m.SnapshotB : m.SnapshotA;
            TeamSnapshot theirs = flip ? m.SnapshotA : m.SnapshotB;
            MonsterSnapshot mon = (side == BattleSide.A ? mine : theirs)?.Board[slot];
            return mon == null ? ((string, string)?)null : MonsterInfoText(mon, enemy: side == BattleSide.B, price: null, mine: false);
        }

        private (string, string)? LobbyMyInfo()
        {
            if (Lobby == null) return null;
            if (_scout >= 0)
            {
                LobbyPlayer p = Lobby.Players[_scout];
                return (p.Name, string.Join("\n",
                    p.IsBot ? HudText.InfoBotPlayer : HudText.InfoPlayer,
                    p.IsAlive ? string.Format(HudText.InfoLivesFormat, p.Run.Lives, _run.Rules.StartLives) : string.Format(HudText.PlacementFormat, p.Placement),
                    string.Format(HudText.InfoMonsterCountFormat, p.Run.Team.Count(x => x != null))));
            }
            return (HudText.MyTitle, InfoText.MyBody(HudText.MyName, State.Wins, 0, State.Lives, _run.Rules.StartLives, State.Gold, Lobby.Round)
                + "\n" + string.Format(HudText.LobbyAliveFormat, Lobby.AliveCount, Lobby.Players.Count));
        }

        private (string, string)? LobbyOpponentInfo()
        {
            LobbyMatch m = LastLobbyReport?.MatchOf(MeIndex);
            if (m == null || shopView.IsVisible) return null;
            bool flip = m.PlayerB == MeIndex;
            TeamSnapshot theirs = flip ? m.SnapshotA : m.SnapshotB;
            if (m.IsPve)
            {
                PveTeamDef pve = _lobby.Rules.PveTeamFor(LastLobbyReport.Round);
                string pveBody = HudText.InfoPve
                    + (pve == null ? "" : "\n" + string.Format(HudText.InfoPveRewardFormat, pve.WinGold, _lobby.Rules.PveLifeLoss, pve.LoseGold))
                    + "\n" + string.Format(HudText.InfoMonsterCountFormat, theirs.Board.Count(x => x != null));
                return (string.Format(HudText.OpponentTitleFormat, HudText.PveName), pveBody);
            }
            int owner = m.IsGhost ? m.GhostOf : (flip ? m.PlayerA : m.PlayerB);
            LobbyPlayer p = Lobby.Players[owner];
            string body = string.Join("\n", new[]
            {
                p.IsBot ? HudText.InfoBotPlayer : HudText.InfoPlayer,
                m.IsGhost ? HudText.InfoGhost : "",
                string.Format(HudText.InfoLivesFormat, p.Run.Lives, _run.Rules.StartLives),
                string.Format(HudText.InfoMonsterCountFormat, theirs.Board.Count(x => x != null)),
            }.Where(x => x.Length > 0));
            return (string.Format(HudText.OpponentTitleFormat, OpponentName(m)), body);
        }
    }
}
