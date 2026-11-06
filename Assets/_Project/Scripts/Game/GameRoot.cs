using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// 씬 연결 한 곳 (컴포지션 루트). 내 팀 = 디버그 팀, 상대 = 로컬 상대 공급자(켜져 있을 때) 또는 디버그 팀.
    /// 다시 보기 = 같은 입력 · 같은 시드로 다시 계산 → 기록이 똑같은지 확인 후 처음부터 재생.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private static readonly int[] Speeds = { 1, 2, 4 };

        /// <summary>내 팀을 올리는 로컬 폴더 이름 (Application.persistentDataPath 아래).</summary>
        public const string LocalWriteFolder = "LocalOpponents";

        [Header("데이터")]
        [SerializeField] private BattleBalance balance;
        [SerializeField] private DebugTeamSet debugTeams;
        [SerializeField] private SpeciesTable speciesTable;

        [Header("시험 전투")]
        [SerializeField] private int teamAIndex;
        [Tooltip("로컬 상대를 끄거나 상대를 못 찾았을 때 쓰는 디버그 팀")]
        [SerializeField] private int teamBIndex = 1;
        [SerializeField] private int round = 3;
        [SerializeField] private long seed = 12345;

        [Header("상대")]
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

        public BattlePlayback Playback => _playback;
        public int Speed => _speed;
        public IOpponentSource Opponents => _opponents;
        /// <summary>마지막 전투의 상대 정보 (디버그 · 확인용).</summary>
        public OpponentResult LastOpponent { get; private set; }

        private void Start()
        {
            _catalog = speciesTable.ToCatalog();
            _statRules = balance.ToStatRules();
            _opponents = new LocalFileOpponentSource(
                new[] { Path.Combine(Application.streamingAssetsPath, "Opponents") },
                Path.Combine(Application.streamingAssetsPath, "OpponentBots"),
                Path.Combine(Application.persistentDataPath, LocalWriteFolder),
                s => TeamFactory.IsPlayable(s, _catalog, _statRules));

            hud.Build(mainCamera, Speeds);
            hud.SpeedClicked += SetSpeed;
            hud.ReplayClicked += Replay;
            SetSpeed(_speed);
            Replay();
        }

        private void Update()
        {
            if (_playback == null) return;
            float scaled = Time.deltaTime * _speed;
            _playback.Advance(Time.deltaTime, _speed, OnBattleEvent);
            stage.Tick(_playback.CurrentTick, scaled, _playback.IsFinished);
            hud.Tick(scaled);
        }

        public void SetSpeed(int speed)
        {
            _speed = speed;
            hud.SetActiveSpeed(speed);
        }

        /// <summary>같은 입력으로 다시 계산해 처음부터 재생. 기록이 지난번과 같은지 화면 아래에 표시.</summary>
        public async void Replay()
        {
            try
            {
                await StartBattleAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Task StartBattleAsync()
        {
            _rules = balance.ToRules();
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
            string signature = string.Join("|", result.Events.Select(e => e.ToString()));
            string check = _lastSignature != null && inputKey == _lastInputKey
                ? (signature == _lastSignature ? HudText.SameResult : HudText.DifferentResult)
                : "";
            hud.ShowStatus(string.Join("   ", new[] { note, check }.Where(x => x.Length > 0)));
            _lastSignature = signature;
            _lastInputKey = inputKey;

            _playback = new BattlePlayback(result, _rules.TicksPerSecond);

            stage.Build(teamA, teamB, (ulong)seed ^ 0x5EEDF00DUL);
            hud.ShowBattle(round, a.name, nameB, result.StartHp);
            AddLabels(BattleSide.A, teamA);
            AddLabels(BattleSide.B, teamB);
        }

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
