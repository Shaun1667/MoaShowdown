using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class RunControllerTests
    {
        private sealed class FakeOpponents : IOpponentSource
        {
            public Func<OpponentQuery, OpponentResult> Answer = q => new OpponentResult();
            public readonly List<TeamSnapshot> Submitted = new List<TeamSnapshot>();
            public readonly List<OpponentQuery> Queries = new List<OpponentQuery>();

            public Task<OpponentResult> FindOpponentAsync(OpponentQuery query)
            {
                Queries.Add(query);
                return Task.FromResult(Answer(query));
            }

            public Task SubmitAsync(TeamSnapshot mine)
            {
                Submitted.Add(mine);
                return Task.CompletedTask;
            }
        }

        // 강함 = 1틱마다 10, 약함 = 100틱마다 1 · 체력 100 → 강한 쪽이 10틱에 이김
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "strong", Attack = 10, AttackIntervalTicks = 1 },
            new SpeciesDef { Id = "weak", Attack = 1, AttackIntervalTicks = 100 },
        });

        private static BattleRules Battle() => new BattleRules
        {
            TicksPerSecond = 10,
            TeamHpByRound = new[] { 100 },
            SuddenDeathStartTick = 1000,
            SuddenDeathIntervalTicks = 10,
            SuddenDeathBaseDamage = 5,
            SuddenDeathDamageStep = 5,
            MaxTicks = 2000,
            BurnIntervalTicks = 10,
            PoisonIntervalTicks = 20,
        };

        private static RunRules Rules(int winsToChampion = 10, int startLives = 10) => new RunRules
        {
            WinsToChampion = winsToChampion,
            StartLives = startLives,
            LifeLossByRound = new[] { 1, 1, 2, 3 },
        };

        private static MonsterSnapshot[] Team(string species)
        {
            var t = new MonsterSnapshot[BattleTeam.SlotCount];
            t[0] = new MonsterSnapshot { SpeciesId = species, Level = 1 };
            return t;
        }

        private static OpponentResult Opp(string runId, string species, bool bot = false)
        {
            var s = new TeamSnapshot { RunId = runId, Round = 1, RulesVersion = 1 };
            s.Board[0] = new MonsterSnapshot { SpeciesId = species, Level = 1 };
            return new OpponentResult { Snapshot = s, IsBot = bot, Source = "fake" };
        }

        private static RunController Controller(FakeOpponents opp, RunRules rules = null) =>
            new RunController(rules ?? Rules(), Battle(), new UnitStatRules { LevelAttackMultiplier = 2 }, Catalog(), opp, 1);

        [Test]
        public void NewRun_StartsAtRound1_FullLives_TeamCopied()
        {
            var start = Team("strong");
            RunState s = Controller(new FakeOpponents()).NewRun("r", 5, "t", start);
            start[0].Level = 9;
            Assert.That(s.Round, Is.EqualTo(1));
            Assert.That(s.Lives, Is.EqualTo(10));
            Assert.That(s.Team[0].Level, Is.EqualTo(1), "시작 팀은 복사 — 원본을 바꿔도 판은 그대로");
        }

        [Test]
        public void Win_AddsWin_NextRound_RemembersOpponent()
        {
            var opp = new FakeOpponents { Answer = q => Opp("enemy", "weak") };
            var c = Controller(opp);
            RunState s = c.NewRun("r", 5, "t", Team("strong"));
            RoundReport rep = c.PlayRoundAsync(s).Result;
            Assert.That(rep.Record.Result, Is.EqualTo("win"));
            Assert.That(s.Wins, Is.EqualTo(1));
            Assert.That(s.Round, Is.EqualTo(2));
            Assert.That(s.FacedRunIds, Is.EqualTo(new[] { "enemy" }));
            Assert.That(rep.OutcomeAfter, Is.EqualTo(RunOutcome.InProgress));
        }

        [TestCase(1, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 3)]
        [TestCase(9, 3)]
        public void Lose_LosesLivesByRound(int round, int expectedLoss)
        {
            var opp = new FakeOpponents { Answer = q => Opp("enemy" + q.Round, "strong") };
            var c = Controller(opp);
            RunState s = c.NewRun("r", 5, "t", Team("weak"));
            s.Round = round;
            RoundReport rep = c.PlayRoundAsync(s).Result;
            Assert.That(rep.Record.Result, Is.EqualTo("lose"));
            Assert.That(rep.Record.LivesLost, Is.EqualTo(expectedLoss));
            Assert.That(s.Lives, Is.EqualTo(10 - expectedLoss));
            Assert.That(s.LivesLost, Is.EqualTo(expectedLoss));
        }

        [Test]
        public void NoOpponent_IsBye_CountsAsWin()
        {
            var c = Controller(new FakeOpponents());
            RunState s = c.NewRun("r", 5, "t", Team("weak"));
            RoundReport rep = c.PlayRoundAsync(s).Result;
            Assert.That(rep.Record.Result, Is.EqualTo("bye"));
            Assert.That(rep.Battle, Is.Null);
            Assert.That(s.Wins, Is.EqualTo(1));
        }

        [Test]
        public void ReachingWins_IsChampion_ThenNoMoreRounds()
        {
            var opp = new FakeOpponents { Answer = q => Opp("e" + q.Round, "weak") };
            var c = Controller(opp, Rules(winsToChampion: 2));
            RunState s = c.NewRun("r", 5, "t", Team("strong"));
            c.PlayRoundAsync(s).Wait();
            RoundReport last = c.PlayRoundAsync(s).Result;
            Assert.That(last.OutcomeAfter, Is.EqualTo(RunOutcome.Champion));
            Assert.That(s.Round, Is.EqualTo(2), "끝난 라운드에서 멈춤");
            var ex = Assert.Throws<AggregateException>(() => c.PlayRoundAsync(s).Wait());
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void LivesHitZero_IsEliminated_ClampedAtZero()
        {
            var opp = new FakeOpponents { Answer = q => Opp("e" + q.Round, "strong") };
            var c = Controller(opp, Rules(startLives: 2));
            RunState s = c.NewRun("r", 5, "t", Team("weak"));
            s.Round = 4; // 패배 시 3개
            RoundReport rep = c.PlayRoundAsync(s).Result;
            Assert.That(rep.OutcomeAfter, Is.EqualTo(RunOutcome.Eliminated));
            Assert.That(s.Lives, Is.EqualTo(0));
            Assert.That(rep.Record.LivesLost, Is.EqualTo(2));
        }

        [Test]
        public void Submit_BeforeBattle_WithCurrentRoundAndWins_QueryExcludesFaced()
        {
            var opp = new FakeOpponents { Answer = q => Opp("e" + q.Round, "weak") };
            var c = Controller(opp);
            RunState s = c.NewRun("myrun", 5, "t", Team("strong"));
            c.PlayRoundAsync(s).Wait();
            c.PlayRoundAsync(s).Wait();
            Assert.That(opp.Submitted.Select(x => (x.Round, x.Wins)), Is.EqualTo(new[] { (1, 0), (2, 1) }));
            Assert.That(opp.Submitted[0].RunId, Is.EqualTo("myrun"));
            Assert.That(opp.Queries[1].OwnRunId, Is.EqualTo("myrun"));
            Assert.That(opp.Queries[1].ExcludedRunIds, Does.Contain("e1"));
        }

        [Test]
        public void Bot_IsNotRemembered()
        {
            var opp = new FakeOpponents { Answer = q => Opp("bot", "weak", bot: true) };
            var c = Controller(opp);
            RunState s = c.NewRun("r", 5, "t", Team("strong"));
            c.PlayRoundAsync(s).Wait();
            Assert.That(s.FacedRunIds, Is.Empty);
            Assert.That(s.History[0].OpponentIsBot, Is.True);
        }

        [Test]
        public void SameSeedSameOpponents_SameRun()
        {
            string Play()
            {
                var opp = new FakeOpponents { Answer = q => Opp("e" + q.Round, q.Round % 2 == 0 ? "strong" : "weak") };
                var c = Controller(opp);
                RunState s = c.NewRun("r", 77, "t", Team("strong"));
                var log = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    RoundReport rep = c.PlayRoundAsync(s).Result;
                    log.Add($"{rep.Round}:{rep.Record.Result}:{rep.Battle?.EndTick}:{opp.Queries.Last().Seed}");
                }
                return string.Join(",", log) + "|" + RunStateJson.Serialize(s);
            }
            Assert.That(Play(), Is.EqualTo(Play()));
        }

        [Test]
        public void DebugLevelUpAll_RaisesEveryMonster()
        {
            RunState s = Controller(new FakeOpponents()).NewRun("r", 5, "t", Team("strong"));
            s.Team[4] = new MonsterSnapshot { SpeciesId = "weak", Level = 3 };
            RunController.DebugLevelUpAll(s);
            Assert.That(s.Team[0].Level, Is.EqualTo(2));
            Assert.That(s.Team[4].Level, Is.EqualTo(4));
        }

        [Test]
        public void RunState_JsonRoundTrip()
        {
            var opp = new FakeOpponents { Answer = q => Opp("e" + q.Round, "weak") };
            var c = Controller(opp);
            RunState s = c.NewRun("r", ulong.MaxValue - 3, "t", Team("strong"));
            s.Team[0].Buffs.Add(new StatBuff("attack", 4));
            c.PlayRoundAsync(s).Wait();
            string once = RunStateJson.Serialize(s);
            RunState back = RunStateJson.Deserialize(once);
            Assert.That(RunStateJson.Serialize(back), Is.EqualTo(once));
            Assert.That(unchecked((ulong)back.Seed), Is.EqualTo(ulong.MaxValue - 3));
            Assert.That(back.History.Count, Is.EqualTo(1));
            Assert.That(back.Team[0].Buffs[0].Value, Is.EqualTo(4));
        }

        [TestCase("{\"formatVersion\":3,\"runId\":\"r\",\"round\":1,\"team\":[null,null,null,null,null,null],\"facedRunIds\":[],\"history\":[]}")]
        [TestCase("{\"runId\":\"r\",\"round\":1}")]
        [TestCase("{\"formatVersion\":1,\"runId\":\"\",\"round\":1,\"team\":[null,null,null,null,null,null],\"facedRunIds\":[],\"history\":[]}")]
        [TestCase("{\"formatVersion\":1,\"runId\":\"r\",\"round\":1,\"team\":[null,null,null],\"facedRunIds\":[],\"history\":[]}")]
        [TestCase("{\"formatVersion\":1,\"runId\":\"r\",\"round\":1,\"lives\":-1,\"team\":[null,null,null,null,null,null],\"facedRunIds\":[],\"history\":[]}")]
        public void RunState_BadJson_IsRejected(string json)
        {
            Assert.Throws<SnapshotFormatException>(() => RunStateJson.Deserialize(json));
        }

        [Test]
        public void SeedMix_SeparatesPurposeAndRound()
        {
            ulong a = SeedMix.Derive(42, SeedMix.PurposeBattle, 1);
            Assert.That(SeedMix.Derive(42, SeedMix.PurposeBattle, 1), Is.EqualTo(a));
            Assert.That(SeedMix.Derive(42, SeedMix.PurposeOpponent, 1), Is.Not.EqualTo(a));
            Assert.That(SeedMix.Derive(42, SeedMix.PurposeBattle, 2), Is.Not.EqualTo(a));
            Assert.That(SeedMix.Derive(43, SeedMix.PurposeBattle, 1), Is.Not.EqualTo(a));
        }
    }
}
