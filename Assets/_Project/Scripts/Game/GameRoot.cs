using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// 씬 연결 한 곳 (컴포지션 루트).
    /// Run 모드(기본): 새 판 → 라운드마다 <see cref="RunController"/>가 계산 → 전투 재생 → 결과 패널 → 다음 라운드 / 새 판.
    /// BattleTest 모드: 1-3 · 1-4의 1:1 시험 전투 (디버그 팀 vs 로컬 상대 또는 디버그 팀).
    /// 다시 보기 = 같은 입력 · 같은 시드로 다시 계산 → 기록이 똑같은지 확인 후 처음부터 재생.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public enum Mode
        {
            Run = 0,
            BattleTest = 1,
        }

        private static readonly int[] Speeds = { 1, 2, 4 };

        [Header("모드")]
        [SerializeField] private Mode mode = Mode.Run;

        [Header("데이터")]
        [SerializeField] private BattleBalance balance;
        [SerializeField] private RunBalance runBalance;
        [SerializeField] private SpeciesTable speciesTable;
        [SerializeField] private DebugTeamSet debugTeams;

        [Header("판 (Run 모드)")]
        [Tooltip("0이면 판마다 새 시드, 아니면 이 시드로 고정 (같은 판 다시 보기용)")]
        [SerializeField] private long runSeed;
        [Tooltip("내 팀을 올리는 폴더 이름 (persistentDataPath 아래). 확인 · 시험 때는 다른 이름으로 바꿔 실제 기록과 섞지 않는다")]
        [SerializeField] private string localWriteFolder = "LocalOpponents";
        [SerializeField] private bool showDebugButtons = true;

        [Header("시험 전투 (BattleTest 모드)")]
        [SerializeField] private int teamAIndex;
        [Tooltip("로컬 상대를 끄거나 상대를 못 찾았을 때 쓰는 디버그 팀")]
        [SerializeField] private int teamBIndex = 1;
        [SerializeField] private int round = 3;
        [SerializeField] private long seed = 12345;
        [Tooltip("켜면 상대를 로컬 파일(StreamingAssets/Opponents · OpponentBots)에서 고른다")]
        [SerializeField] private bool useLocalOpponent = true;
        [Tooltip("상대 고르기에 쓰는 내 승수")]
        [SerializeField] private int wins;

        [Header("씬")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BattleStage stage;
        [SerializeField] private BattleHud hud;

        private BattleRules _rules;
        private BattlePlayback _playback;
        private string _lastSignature;
        private string _lastInputKey;
        private int _speed = 1;
        private IOpponentSource _opponents;
        private SpeciesCatalog _catalog;
        private UnitStatRules _statRules;
        private RunController _run;
        private bool _roundResultPending;

        public BattlePlayback Playback => _playback;
        public int Speed => _speed;
        public IOpponentSource Opponents => _opponents;
        /// <summary>마지막 전투의 상대 정보 (BattleTest 모드, 디버그 · 확인용).</summary>
        public OpponentResult LastOpponent { get; private set; }
        /// <summary>지금 판 상태 (Run 모드).</summary>
        public RunState State { get; private set; }
        /// <summary>마지막 라운드 결과 (Run 모드).</summary>
        public RoundReport LastReport { get; private set; }
        public RunRules RunRules => _run?.Rules;

        private void Start()
        {
            _rules = balance.ToRules();
            _catalog = speciesTable.ToCatalog();
            _statRules = balance.ToStatRules();
            _opponents = new LocalFileOpponentSource(
                new[] { Path.Combine(Application.streamingAssetsPath, "Opponents") },
                Path.Combine(Application.streamingAssetsPath, "OpponentBots"),
                Path.Combine(Application.persistentDataPath, localWriteFolder),
                s => TeamFactory.IsPlayable(s, _catalog, _statRules));

            hud.Build(mainCamera, Speeds);
            hud.SpeedClicked += SetSpeed;
            hud.ReplayClicked += Replay;
            hud.ContinueClicked += Continue;
            hud.DebugLevelUpClicked += DebugLevelUp;
            SetSpeed(_speed);

            if (mode == Mode.Run)
            {
                _run = new RunController(runBalance.ToRules(), _rules, _statRules, _catalog, _opponents, balance.rulesVersion);
                hud.SetDebugVisible(showDebugButtons);
                StartNewRun();
                Continue();
            }
            else
            {
                Replay();
            }
        }

        private void Update()
        {
            if (_playback == null) return;
            float scaled = Time.deltaTime * _speed;
            _playback.Advance(Time.deltaTime, _speed, OnBattleEvent);
            stage.Tick(_playback.CurrentTick, scaled, _playback.IsFinished);
            hud.Tick(scaled);
            if (_roundResultPending && _playback.IsFinished)
            {
                _roundResultPending = false;
                ShowRoundResult();
            }
        }

        public void SetSpeed(int speed)
        {
            _speed = speed;
            hud.SetActiveSpeed(speed);
        }

        // ───────── Run 모드 ─────────

        public void StartNewRun()
        {
            ulong s = runSeed != 0 ? (ulong)runSeed : (ulong)DateTime.UtcNow.Ticks;
            string runId = $"run_{DateTime.UtcNow:yyyyMMddHHmmss}_{s & 0xFFFF:x4}";
            State = _run.NewRun(runId, s, runBalance.startTrainerId, runBalance.ToStartTeam());
            LastReport = null;
            _lastSignature = null;
            ShowRunInfo(State.Wins, State.Lives);
        }

        /// <summary>결과 패널의 계속 버튼: 판이 진행 중이면 다음 라운드, 끝났으면 새 판을 시작하고 첫 라운드.</summary>
        public async void Continue()
        {
            if (mode != Mode.Run) return;
            try
            {
                if (State.GetOutcome(_run.Rules) != RunOutcome.InProgress) StartNewRun();
                await PlayNextRoundAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Task PlayNextRoundAsync()
        {
            hud.HideRoundResult();
            int winsBefore = State.Wins;
            int livesBefore = State.Lives;
            RoundReport report = await _run.PlayRoundAsync(State);
            LastReport = report;
            ShowRunInfo(winsBefore, livesBefore);

            if (report.Battle == null)
            {
                _playback = null;
                hud.ShowStatus("");
                ShowRoundResult();
                return;
            }

            string opponentName = string.Format(report.Opponent.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat,
                report.Opponent.Snapshot.RunId, report.Opponent.Snapshot.Wins);
            _lastSignature = Signature(report.Battle);
            hud.ShowStatus("");
            PlayBattle(report.MyTeam, report.OpponentTeam, report.Battle, report.Round, HudText.MyTeam, opponentName, unchecked((ulong)State.Seed) ^ (ulong)report.Round);
            _roundResultPending = true;
        }

        private void ShowRoundResult()
        {
            RoundReport r = LastReport;
            RunRules rules = _run.Rules;
            ShowRunInfo(State.Wins, State.Lives);
            string detail = r.Record.Result switch
            {
                "win" => string.Format(HudText.RoundWinFormat, State.Wins, rules.WinsToChampion),
                "lose" => string.Format(HudText.RoundLoseFormat, r.Record.LivesLost, State.Lives),
                "draw" => HudText.RoundDraw,
                _ => HudText.RoundBye,
            };
            switch (r.OutcomeAfter)
            {
                case RunOutcome.Champion:
                    hud.ShowBanner(HudText.Champion);
                    detail = string.Format(HudText.ChampionFormat, r.Round, State.Wins);
                    hud.ShowRoundResult(detail, HudText.NewRun);
                    break;
                case RunOutcome.Eliminated:
                    hud.ShowBanner(HudText.Eliminated);
                    detail = string.Format(HudText.EliminatedFormat, r.Round, State.Wins);
                    hud.ShowRoundResult(detail, HudText.NewRun);
                    break;
                default:
                    hud.ShowRoundResult(detail, HudText.NextRound);
                    break;
            }
        }

        private void ShowRunInfo(int winsNow, int livesNow)
        {
            RunRules rules = _run.Rules;
            hud.ShowRunInfo(string.Format(HudText.RunInfoFormat, winsNow, rules.WinsToChampion, livesNow, rules.StartLives));
        }

        /// <summary>디버그: 팀 전체 레벨 +1 (라운드 사이에만, 다음 라운드부터 적용).</summary>
        public void DebugLevelUp()
        {
            if (mode != Mode.Run || State == null) return;
            RunController.DebugLevelUpAll(State);
            int level = State.Team.Where(m => m != null).Select(m => m.Level).DefaultIfEmpty(0).Max();
            hud.ShowStatus(string.Format(HudText.DebugLevelUpDoneFormat, level));
        }

        // ───────── 다시 보기 · 시험 전투 ─────────

        /// <summary>
        /// Run 모드: 마지막 라운드 전투를 같은 팀 · 같은 시드로 다시 계산해 기록이 같은지 확인하고 다시 재생 (판 상태는 그대로).
        /// BattleTest 모드: 시험 전투를 다시 계산 · 재생.
        /// </summary>
        public async void Replay()
        {
            try
            {
                if (mode == Mode.Run) ReplayLastRound();
                else await StartTestBattleAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ReplayLastRound()
        {
            RoundReport r = LastReport;
            if (r?.Battle == null) return;
            ulong rs = unchecked((ulong)State.Seed);
            BattleResult again = BattleSimulator.Run(_rules, r.MyTeam, r.OpponentTeam, r.Round,
                Core.Random.SeedMix.Derive(rs, Core.Random.SeedMix.PurposeBattle, r.Round));
            hud.ShowStatus(Signature(again) == Signature(r.Battle) ? HudText.SameResult : HudText.DifferentResult);
            hud.HideRoundResult();
            string opponentName = string.Format(r.Opponent.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat, r.Opponent.Snapshot.RunId, r.Opponent.Snapshot.Wins);
            PlayBattle(r.MyTeam, r.OpponentTeam, again, r.Round, HudText.MyTeam, opponentName, rs ^ (ulong)r.Round);
            _roundResultPending = true;
        }

        private async Task StartTestBattleAsync()
        {
            DebugTeam a = debugTeams.teams[teamAIndex];
            BattleTeam teamA = a.ToBattleTeam();

            BattleTeam teamB;
            string nameB;
            string note = "";
            LastOpponent = null;
            if (useLocalOpponent)
            {
                var query = new OpponentQuery
                {
                    Round = round,
                    Wins = wins,
                    RulesVersion = balance.rulesVersion,
                    OwnRunId = "debug",
                    Seed = (ulong)seed ^ (ulong)round,
                };
                OpponentResult found = await _opponents.FindOpponentAsync(query);
                LastOpponent = found;
                if (found.Snapshot != null)
                {
                    teamB = TeamFactory.Build(found.Snapshot, _catalog, _statRules);
                    nameB = string.Format(found.IsBot ? HudText.OpponentBotFormat : HudText.OpponentFormat, found.Snapshot.RunId, found.Snapshot.Wins);
                }
                else
                {
                    DebugTeam b = debugTeams.teams[teamBIndex];
                    teamB = b.ToBattleTeam();
                    nameB = b.name;
                    note = HudText.OpponentNotFound;
                }
            }
            else
            {
                DebugTeam b = debugTeams.teams[teamBIndex];
                teamB = b.ToBattleTeam();
                nameB = b.name;
            }

            BattleResult result = BattleSimulator.Run(_rules, teamA, teamB, round, (ulong)seed);
            // 결정론 확인: 입력(팀 내용 · 라운드 · 시드 · 체력)이 지난번과 같을 때만 기록을 비교한다
            string inputKey = $"{InputKey(teamA)}/{InputKey(teamB)}/{round}/{seed}/{result.StartHp}";
            string signature = Signature(result);
            string check = _lastSignature != null && inputKey == _lastInputKey
                ? (signature == _lastSignature ? HudText.SameResult : HudText.DifferentResult)
                : "";
            hud.ShowStatus(string.Join("   ", new[] { note, check }.Where(x => x.Length > 0)));
            _lastSignature = signature;
            _lastInputKey = inputKey;

            PlayBattle(teamA, teamB, result, round, a.name, nameB, (ulong)seed ^ 0x5EEDF00DUL);
        }

        // ───────── 공통 ─────────

        private void PlayBattle(BattleTeam a, BattleTeam b, BattleResult result, int battleRound, string nameA, string nameB, ulong fxSeed)
        {
            _playback = new BattlePlayback(result, _rules.TicksPerSecond);
            stage.Build(a, b, fxSeed);
            hud.ShowBattle(battleRound, nameA, nameB, result.StartHp);
            AddLabels(BattleSide.A, a);
            AddLabels(BattleSide.B, b);
        }

        private static string Signature(BattleResult result) => string.Join("|", result.Events.Select(e => e.ToString()));

        private static string InputKey(BattleTeam team)
        {
            var parts = new string[BattleTeam.SlotCount];
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
                parts[slot] = team[slot].IsEmpty ? "-" : $"{team[slot].Attack}:{team[slot].AttackIntervalTicks}";
            return string.Join(",", parts);
        }

        private void AddLabels(BattleSide side, BattleTeam team)
        {
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                BattleUnit u = team[slot];
                if (u.IsEmpty) continue;
                hud.AddUnitLabel(stage.LabelAnchor(side, slot), u.Attack, u.AttackIntervalTicks, _rules.TicksPerSecond);
            }
        }

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Attack:
                    Vector3 hit = stage.PlayAttack(e.Side, e.Slot);
                    hud.ShowDamage(hit, e.Amount);
                    break;
                case BattleEventType.SuddenDeath:
                    hud.ShowBanner(HudText.SuddenDeath);
                    break;
                case BattleEventType.TeamHpChanged:
                    hud.SetHp(e.Side, e.Value, _playback.Result.StartHp);
                    break;
                case BattleEventType.End:
                    var outcome = (BattleOutcome)e.Value;
                    hud.ShowBanner(outcome == BattleOutcome.AWins ? HudText.Win
                        : outcome == BattleOutcome.BWins ? HudText.Lose
                        : HudText.Draw);
                    break;
            }
        }
    }
}
