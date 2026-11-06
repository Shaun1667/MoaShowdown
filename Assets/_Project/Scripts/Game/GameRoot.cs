using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Game.Battle;
using Moa.Showdown.Game.Data;
using UnityEngine;

namespace Moa.Showdown.Game
{
    /// <summary>
    /// 씬 연결 한 곳 (컴포지션 루트). 지금(1-3)은 디버그 팀 두 개로 전투를 계산하고 재생한다.
    /// 다시 보기 = 같은 입력 · 같은 시드로 다시 계산 → 기록이 똑같은지 확인 후 처음부터 재생.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private static readonly int[] Speeds = { 1, 2, 4 };

        [Header("데이터")]
        [SerializeField] private BattleBalance balance;
        [SerializeField] private DebugTeamSet debugTeams;

        [Header("시험 전투")]
        [SerializeField] private int teamAIndex;
        [SerializeField] private int teamBIndex = 1;
        [SerializeField] private int round = 3;
        [SerializeField] private long seed = 12345;

        [Header("씬")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private BattleStage stage;
        [SerializeField] private BattleHud hud;

        private BattleRules _rules;
        private BattlePlayback _playback;
        private string _lastSignature;
        private string _lastInputKey;
        private int _speed = 1;

        public BattlePlayback Playback => _playback;
        public int Speed => _speed;

        private void Start()
        {
            hud.Build(mainCamera, Speeds);
            hud.SpeedClicked += SetSpeed;
            hud.ReplayClicked += Replay;
            SetSpeed(_speed);
            StartBattle();
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
        public void Replay() => StartBattle();

        private void StartBattle()
        {
            _rules = balance.ToRules();
            DebugTeam a = debugTeams.teams[teamAIndex];
            DebugTeam b = debugTeams.teams[teamBIndex];
            BattleTeam teamA = a.ToBattleTeam();
            BattleTeam teamB = b.ToBattleTeam();

            BattleResult result = BattleSimulator.Run(_rules, teamA, teamB, round, (ulong)seed);
            // 결정론 확인: 입력(팀 내용 · 라운드 · 시드 · 체력)이 지난번과 같을 때만 기록을 비교한다
            string inputKey = $"{InputKey(teamA)}/{InputKey(teamB)}/{round}/{seed}/{result.StartHp}";
            string signature = string.Join("|", result.Events.Select(e => e.ToString()));
            if (_lastSignature != null && inputKey == _lastInputKey)
                hud.ShowStatus(signature == _lastSignature ? HudText.SameResult : HudText.DifferentResult);
            else
                hud.ShowStatus("");
            _lastSignature = signature;
            _lastInputKey = inputKey;

            _playback = new BattlePlayback(result, _rules.TicksPerSecond);

            stage.Build(teamA, teamB, (ulong)seed ^ 0x5EEDF00DUL);
            hud.ShowBattle(round, a.name, b.name, result.StartHp);
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
