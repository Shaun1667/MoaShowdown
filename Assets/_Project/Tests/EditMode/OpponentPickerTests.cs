using System.Collections.Generic;
using Moa.Showdown.Core.Online;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class OpponentPickerTests
    {
        private static TeamSnapshot S(string runId, int round, int wins, int rulesVersion = 1) =>
            new TeamSnapshot { RunId = runId, Round = round, Wins = wins, RulesVersion = rulesVersion };

        private static OpponentQuery Q(int round, int wins) =>
            new OpponentQuery { Round = round, Wins = wins, RulesVersion = 1, OwnRunId = "me", Seed = 7 };

        private static bool All(TeamSnapshot s) => true;

        [Test]
        public void Player_FiltersRoundVersionOwnAndExcluded()
        {
            var list = new List<TeamSnapshot>
            {
                S("wrongRound", 2, 0),
                S("wrongVersion", 3, 0, rulesVersion: 2),
                S("me", 3, 0),
                S("seen", 3, 0),
                S("ok", 3, 5),
            };
            var q = Q(3, 0);
            q.ExcludedRunIds.Add("seen");
            Assert.That(OpponentPicker.PickPlayer(list, q, new DeterministicRandom(1), All).RunId, Is.EqualTo("ok"));
        }

        [Test]
        public void Player_PrefersClosestWins()
        {
            var list = new List<TeamSnapshot> { S("far", 5, 0), S("near1", 5, 3), S("near2", 5, 3), S("far2", 5, 4 + 3) };
            for (ulong seed = 0; seed < 20; seed++)
            {
                string id = OpponentPicker.PickPlayer(list, Q(5, 3), new DeterministicRandom(seed), All).RunId;
                Assert.That(id, Is.EqualTo("near1").Or.EqualTo("near2"));
            }
        }

        [Test]
        public void Player_SameSeed_SamePick()
        {
            var list = new List<TeamSnapshot> { S("a", 4, 1), S("b", 4, 1), S("c", 4, 1), S("d", 4, 1) };
            string first = OpponentPicker.PickPlayer(list, Q(4, 1), new DeterministicRandom(99), All).RunId;
            for (int i = 0; i < 5; i++)
                Assert.That(OpponentPicker.PickPlayer(list, Q(4, 1), new DeterministicRandom(99), All).RunId, Is.EqualTo(first));
        }

        [Test]
        public void Player_NotPlayable_IsSkipped_NullWhenNone()
        {
            var list = new List<TeamSnapshot> { S("bad", 2, 0) };
            Assert.That(OpponentPicker.PickPlayer(list, Q(2, 0), new DeterministicRandom(1), s => false), Is.Null);
        }

        [TestCase(1, "bot1")]
        [TestCase(5, "bot4")]
        [TestCase(20, "bot7")]
        public void Bot_SameOrHighestLowerRound(int round, string expected)
        {
            var bots = new List<TeamSnapshot> { S("bot1", 1, 0), S("bot4", 4, 0), S("bot7", 7, 0) };
            Assert.That(OpponentPicker.PickBot(bots, Q(round, 0), new DeterministicRandom(1), All).RunId, Is.EqualTo(expected));
        }

        [Test]
        public void Bot_BelowAllRounds_UsesLowest()
        {
            var bots = new List<TeamSnapshot> { S("bot4", 4, 0), S("bot7", 7, 0) };
            Assert.That(OpponentPicker.PickBot(bots, Q(2, 0), new DeterministicRandom(1), All).RunId, Is.EqualTo("bot4"));
            Assert.That(OpponentPicker.PickBot(new List<TeamSnapshot>(), Q(2, 0), new DeterministicRandom(1), All), Is.Null);
        }
    }
}
