using System;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Run
{
    /// <summary>라운드 하나를 치른 결과 (화면이 재생 · 표시에 쓰는 것 전부).</summary>
    public sealed class RoundReport
    {
        public int Round;
        public TeamSnapshot MySnapshot;
        public OpponentResult Opponent;
        public BattleTeam MyTeam;
        public BattleTeam OpponentTeam;
        /// <summary>상대가 없어 부전승이면 null.</summary>
        public BattleResult Battle;
        public RoundRecord Record;
        public RunOutcome OutcomeAfter;
    }

    /// <summary>
    /// 판 진행 한 곳. 라운드 = 내 팀 스냅샷 만들기 → 올리기 → 상대 받기 → 전투 → 승수 · 목숨 반영 → 다음 라운드.
    /// 승: 승수 +1 · 패: 라운드별 목숨 감소 · 무: 변화 없음 · 상대 없음: 부전승(승수 +1).
    /// 같은 (상태, 상대 후보, 데이터) → 같은 결과. 상태는 <see cref="RunState"/>만 바꾼다.
    /// </summary>
    public sealed class RunController
    {
        private readonly RunRules _runRules;
        private readonly BattleRules _battleRules;
        private readonly UnitStatRules _statRules;
        private readonly SpeciesCatalog _catalog;
        private readonly IOpponentSource _opponents;
        private readonly int _rulesVersion;

        public RunController(RunRules runRules, BattleRules battleRules, UnitStatRules statRules, SpeciesCatalog catalog, IOpponentSource opponents, int rulesVersion)
        {
            _runRules = runRules ?? throw new ArgumentNullException(nameof(runRules));
            _battleRules = battleRules ?? throw new ArgumentNullException(nameof(battleRules));
            _statRules = statRules ?? throw new ArgumentNullException(nameof(statRules));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _opponents = opponents ?? throw new ArgumentNullException(nameof(opponents));
            _rulesVersion = rulesVersion;
            _runRules.Validate();
            _battleRules.Validate();
            _statRules.Validate();
        }

        public RunRules Rules => _runRules;

        public RunState NewRun(string runId, ulong seed, string trainerId, MonsterSnapshot[] startTeam)
        {
            if (startTeam == null || startTeam.Length != BattleTeam.SlotCount) throw new ArgumentException("시작 팀 칸 수 오류", nameof(startTeam));
            var s = new RunState
            {
                RunId = runId,
                Seed = unchecked((long)seed),
                Round = 1,
                Wins = 0,
                Lives = _runRules.StartLives,
                LivesLost = 0,
                TrainerId = trainerId,
            };
            for (int i = 0; i < startTeam.Length; i++) s.Team[i] = Clone(startTeam[i]);
            RunStateJson.Validate(s);
            return s;
        }

        /// <summary>지금 라운드의 내 팀 스냅샷 (올리기 · 전투용).</summary>
        public TeamSnapshot MakeSnapshot(RunState s)
        {
            var snap = new TeamSnapshot
            {
                RulesVersion = _rulesVersion,
                RunId = s.RunId,
                Round = s.Round,
                Wins = s.Wins,
                LivesLost = s.LivesLost,
                TrainerId = s.TrainerId,
            };
            for (int i = 0; i < BattleTeam.SlotCount; i++) snap.Board[i] = Clone(s.Team[i]);
            return snap;
        }

        public async Task<RoundReport> PlayRoundAsync(RunState s)
        {
            if (s.GetOutcome(_runRules) != RunOutcome.InProgress) throw new InvalidOperationException("이미 끝난 판");
            ulong runSeed = unchecked((ulong)s.Seed);
            int round = s.Round;

            TeamSnapshot mine = MakeSnapshot(s);
            BattleTeam myTeam = TeamFactory.Build(mine, _catalog, _statRules);
            await _opponents.SubmitAsync(mine);

            var query = new OpponentQuery
            {
                Round = round,
                Wins = s.Wins,
                RulesVersion = _rulesVersion,
                OwnRunId = s.RunId,
                Seed = SeedMix.Derive(runSeed, SeedMix.PurposeOpponent, round),
            };
            foreach (string id in s.FacedRunIds) query.ExcludedRunIds.Add(id);
            OpponentResult found = await _opponents.FindOpponentAsync(query);

            var report = new RoundReport { Round = round, MySnapshot = mine, Opponent = found, MyTeam = myTeam };
            var record = new RoundRecord { Round = round };

            if (found?.Snapshot == null)
            {
                record.Result = "bye";
                s.Wins++;
            }
            else
            {
                BattleTeam theirs = TeamFactory.Build(found.Snapshot, _catalog, _statRules);
                BattleResult battle = BattleSimulator.Run(_battleRules, myTeam, theirs, round, SeedMix.Derive(runSeed, SeedMix.PurposeBattle, round));
                report.OpponentTeam = theirs;
                report.Battle = battle;
                record.OpponentRunId = found.Snapshot.RunId;
                record.OpponentIsBot = found.IsBot;
                if (found.Snapshot.RunId != null && !found.IsBot) s.FacedRunIds.Add(found.Snapshot.RunId);

                switch (battle.Outcome)
                {
                    case BattleOutcome.AWins:
                        record.Result = "win";
                        s.Wins++;
                        break;
                    case BattleOutcome.BWins:
                        record.Result = "lose";
                        int loss = Math.Min(_runRules.GetLifeLoss(round), s.Lives);
                        record.LivesLost = loss;
                        s.Lives -= loss;
                        s.LivesLost += loss;
                        break;
                    default:
                        record.Result = "draw";
                        break;
                }
            }

            s.History.Add(record);
            report.Record = record;
            report.OutcomeAfter = s.GetOutcome(_runRules);
            if (report.OutcomeAfter == RunOutcome.InProgress) s.Round++;
            return report;
        }

        /// <summary>디버그: 팀 전체 레벨 +1 (상점이 생기기 전 후반 확인용).</summary>
        public static void DebugLevelUpAll(RunState s)
        {
            foreach (MonsterSnapshot m in s.Team.Where(m => m != null)) m.Level++;
        }

        private static MonsterSnapshot Clone(MonsterSnapshot m)
        {
            if (m == null) return null;
            var c = new MonsterSnapshot { SpeciesId = m.SpeciesId, Level = m.Level };
            foreach (StatBuff b in m.Buffs) c.Buffs.Add(new StatBuff(b.Stat, b.Value));
            return c;
        }
    }
}
