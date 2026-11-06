using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Snapshot;
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
        public const int MeIndex = 0;

        private LobbyController _lobby;

        /// <summary>지금 로비 상태 (Lobby 모드).</summary>
        public LobbyState Lobby { get; private set; }
        public LobbyRoundReport LastLobbyReport { get; private set; }
        public LobbyController LobbyCtl => _lobby;

        private LobbyPlayer Me => Lobby.Players[MeIndex];

        /// <summary>내 상점 조작에 쓰는 공유 풀 (Lobby 모드만, 아니면 null = 무한 상점).</summary>
        private Moa.Showdown.Core.Shop.UnitPool CurrentPool => mode == Mode.Lobby && Lobby != null ? LobbyController.PoolOf(Lobby) : null;

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

        /// <summary>결과 패널 계속: 내가 살아 있고 로비가 안 끝났으면 다음 라운드 상점, 아니면 새 로비.</summary>
        private void LobbyContinue()
        {
            if (Lobby.IsFinished || !Me.IsAlive) StartNewLobby();
            EnterShop();
            RefreshStandings();
        }

        /// <summary>전투 시작: 로비 전체 라운드를 계산하고 내 대진을 재생.</summary>
        private void PlayLobbyRound()
        {
            shopView.Hide();
            tooltip.HideAll();
            hud.HideRoundResult();
            int livesBefore = State.Lives;
            LobbyRoundReport report = _lobby.ResolveRound(Lobby);
            LastLobbyReport = report;
            ShowRunInfo(State.Wins, livesBefore);

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
        }

        private void PlayMyMatch(LobbyMatch m, BattleResult battle)
        {
            bool flip = m.PlayerB == MeIndex;
            BattleTeam mine = flip ? m.TeamB : m.TeamA;
            BattleTeam theirs = flip ? m.TeamA : m.TeamB;
            PlayBattle(mine, theirs, battle, LastLobbyReport.Round, HudText.MyTeam, OpponentName(m), m.Seed, flip);
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
                lines.Add(won ? string.Format(HudText.LobbyWinFormat, OpponentName(m))
                    : lost ? string.Format(HudText.RoundLoseFormat, myLoss, State.Lives)
                    : HudText.RoundDraw);
            }
            foreach (int idx in r.Eliminated)
            {
                LobbyPlayer p = Lobby.Players[idx];
                if (idx != MeIndex) lines.Add(string.Format(HudText.LobbyEliminatedFormat, p.Name, p.Placement));
            }

            if (Me.Placement == 1)
            {
                hud.ShowBanner(HudText.LobbyFirst);
                hud.ShowRoundResult(string.Format(HudText.LobbyFirstFormat, r.Round), HudText.NewRun);
            }
            else if (Me.Placement > 1)
            {
                hud.ShowBanner(string.Format(HudText.PlacementFormat, Me.Placement));
                hud.ShowRoundResult(string.Format(HudText.LobbyOutFormat, r.Round, Me.Placement), HudText.NewRun);
            }
            else
            {
                hud.ShowRoundResult(string.Join("\n", lines.Take(3)), HudText.NextRound);
            }
        }

        private string OpponentName(LobbyMatch m)
        {
            bool flip = m.PlayerB == MeIndex;
            if (m.IsGhost) return string.Format(HudText.GhostFormat, Lobby.Players[m.GhostOf].Name);
            return Lobby.Players[flip ? m.PlayerA : m.PlayerB].Name;
        }

        /// <summary>오른쪽 순위 표: 살아 있는 사람(목숨 많은 순) → 탈락한 사람(순위 순).</summary>
        private void RefreshStandings()
        {
            if (Lobby == null) return;
            IEnumerable<LobbyPlayer> alive = Lobby.Players.Where(p => p.Placement == 0).OrderByDescending(p => p.Run.Lives).ThenBy(p => p.Index);
            IEnumerable<LobbyPlayer> done = Lobby.Players.Where(p => p.Placement > 0).OrderBy(p => p.Placement);
            var lines = new List<string> { HudText.StandingsTitle };
            foreach (LobbyPlayer p in alive)
                lines.Add(string.Format(HudText.StandingsAliveFormat, p.Name, p.Run.Lives, p.Index == MeIndex ? HudText.MeMark : ""));
            foreach (LobbyPlayer p in done)
                lines.Add(string.Format(HudText.StandingsOutFormat, p.Placement, p.Name, p.Index == MeIndex ? HudText.MeMark : ""));
            hud.ShowStandings(string.Join("\n", lines));
        }

        private (string, string)? LobbyBattleUnitInfo(BattleSide side, int slot)
        {
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
            return (HudText.MyTitle, InfoText.MyBody(HudText.MyName, State.Wins, 0, State.Lives, _run.Rules.StartLives, State.Gold, Lobby.Round)
                + "\n" + string.Format(HudText.LobbyAliveFormat, Lobby.AliveCount, Lobby.Players.Count));
        }

        private (string, string)? LobbyOpponentInfo()
        {
            LobbyMatch m = LastLobbyReport?.MatchOf(MeIndex);
            if (m == null || shopView.IsVisible) return null;
            bool flip = m.PlayerB == MeIndex;
            TeamSnapshot theirs = flip ? m.SnapshotA : m.SnapshotB;
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
