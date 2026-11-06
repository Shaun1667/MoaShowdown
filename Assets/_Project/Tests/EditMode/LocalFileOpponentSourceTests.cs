using System.Collections.Generic;
using System.IO;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>임시 폴더에서만 읽고 쓴다 — 실제 저장 폴더(persistentDataPath)는 건드리지 않는다.</summary>
    public class LocalFileOpponentSourceTests
    {
        private const string TempPrefix = "MoaShowdownTests_";
        private string _root;
        private string _players;
        private string _bots;
        private string _write;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), TempPrefix + System.Guid.NewGuid().ToString("N"));
            _players = Path.Combine(_root, "players");
            _bots = Path.Combine(_root, "bots");
            _write = Path.Combine(_root, "write");
            Directory.CreateDirectory(_players);
            Directory.CreateDirectory(_bots);
        }

        [TearDown]
        public void TearDown()
        {
            // 이 테스트가 만든 임시 폴더만 지운다
            if (_root != null && Path.GetFileName(_root).StartsWith(TempPrefix) && Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private static TeamSnapshot S(string runId, int round, int wins = 0)
        {
            var s = new TeamSnapshot { RunId = runId, Round = round, Wins = wins, RulesVersion = 1, TrainerId = "t" };
            s.Board[0] = new MonsterSnapshot { SpeciesId = "a", Level = 1 };
            return s;
        }

        private LocalFileOpponentSource Source() =>
            new LocalFileOpponentSource(new[] { _players }, _bots, _write, s => true);

        private static OpponentQuery Q(int round) => new OpponentQuery { Round = round, RulesVersion = 1, OwnRunId = "me", Seed = 3 };

        [Test]
        public void Finds_PlayerTeam_FromFile()
        {
            File.WriteAllText(Path.Combine(_players, "a.json"), TeamSnapshotJson.SerializeMany(new[] { S("p1", 2), S("p2", 3) }));
            OpponentResult r = Source().FindOpponentAsync(Q(3)).Result;
            Assert.That(r.Snapshot.RunId, Is.EqualTo("p2"));
            Assert.That(r.IsBot, Is.False);
            Assert.That(r.Source, Is.EqualTo("a.json"));
        }

        [Test]
        public void FallsBack_ToBot_WhenNoPlayer()
        {
            File.WriteAllText(Path.Combine(_bots, "bots.json"), TeamSnapshotJson.SerializeMany(new[] { S("bot1", 1) }));
            OpponentResult r = Source().FindOpponentAsync(Q(6)).Result;
            Assert.That(r.Snapshot.RunId, Is.EqualTo("bot1"));
            Assert.That(r.IsBot, Is.True);
        }

        [Test]
        public void NothingAnywhere_ReturnsNullSnapshot()
        {
            OpponentResult r = Source().FindOpponentAsync(Q(1)).Result;
            Assert.That(r.Snapshot, Is.Null);
        }

        [Test]
        public void Submit_ThenAnotherRunFindsIt_OwnRunDoesNot()
        {
            var src = Source();
            src.SubmitAsync(S("me", 4)).Wait();
            src.SubmitAsync(S("other", 4)).Wait();
            Assert.That(File.Exists(Path.Combine(_write, LocalFileOpponentSource.SubmittedFileName)), Is.True);

            var mine = new OpponentQuery { Round = 4, RulesVersion = 1, OwnRunId = "me", Seed = 1 };
            Assert.That(src.FindOpponentAsync(mine).Result.Snapshot.RunId, Is.EqualTo("other"));

            var someoneElse = new OpponentQuery { Round = 4, RulesVersion = 1, OwnRunId = "x", Seed = 1 };
            someoneElse.ExcludedRunIds.Add("other");
            Assert.That(src.FindOpponentAsync(someoneElse).Result.Snapshot.RunId, Is.EqualTo("me"));
        }

        [Test]
        public void BadEntries_AreSkipped_WithErrors()
        {
            string good = TeamSnapshotJson.SerializeMany(new[] { S("good", 2) });
            string mixed = good.TrimEnd().TrimEnd(']') + ", {\"formatVersion\":99}, 5 ]";
            File.WriteAllText(Path.Combine(_players, "mixed.json"), mixed);
            File.WriteAllText(Path.Combine(_players, "broken.json"), "{ not json");
            var src = Source();
            OpponentResult r = src.FindOpponentAsync(Q(2)).Result;
            Assert.That(r.Snapshot.RunId, Is.EqualTo("good"));
            Assert.That(src.LoadErrors.Count, Is.EqualTo(3), string.Join("\n", src.LoadErrors));
        }

        [Test]
        public void FileOrder_IsByName_SoPickIsStable()
        {
            File.WriteAllText(Path.Combine(_players, "b.json"), TeamSnapshotJson.SerializeMany(new[] { S("b1", 5), S("b2", 5) }));
            File.WriteAllText(Path.Combine(_players, "a.json"), TeamSnapshotJson.SerializeMany(new[] { S("a1", 5), S("a2", 5) }));
            var picks = new List<string>();
            for (int i = 0; i < 3; i++) picks.Add(Source().FindOpponentAsync(Q(5)).Result.Snapshot.RunId);
            Assert.That(picks, Has.All.EqualTo(picks[0]));
        }
    }
}
