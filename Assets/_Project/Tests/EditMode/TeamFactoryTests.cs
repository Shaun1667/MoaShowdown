using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class TeamFactoryTests
    {
        private static SpeciesCatalog Catalog() => new SpeciesCatalog(new[]
        {
            new SpeciesDef { Id = "a", DisplayName = "A", Attack = 10, AttackIntervalTicks = 8 },
            new SpeciesDef { Id = "b", DisplayName = "B", Attack = 3, AttackIntervalTicks = 2 },
        });

        private static readonly UnitStatRules X2 = new UnitStatRules { LevelAttackMultiplier = 2 };

        private static TeamSnapshot Snap(int slot, MonsterSnapshot m)
        {
            var s = new TeamSnapshot { Round = 1 };
            s.Board[slot] = m;
            return s;
        }

        [TestCase(1, 10)]
        [TestCase(2, 20)]
        [TestCase(3, 40)]
        public void Level_MultipliesAttack(int level, int expected)
        {
            var t = TeamFactory.Build(Snap(0, new MonsterSnapshot { SpeciesId = "a", Level = level }), Catalog(), X2);
            Assert.That(t[0].Attack, Is.EqualTo(expected));
            Assert.That(t[0].AttackIntervalTicks, Is.EqualTo(8));
        }

        [Test]
        public void Buffs_AddAfterLevel_IntervalFloorsAtOne()
        {
            var m = new MonsterSnapshot { SpeciesId = "b", Level = 2 };
            m.Buffs.Add(new StatBuff(StatIds.Attack, 5));
            m.Buffs.Add(new StatBuff(StatIds.Interval, -10));
            m.Buffs.Add(new StatBuff("future_stat", 99));
            var t = TeamFactory.Build(Snap(4, m), Catalog(), X2);
            Assert.That(t[4].Attack, Is.EqualTo(3 * 2 + 5));
            Assert.That(t[4].AttackIntervalTicks, Is.EqualTo(1));
            Assert.That(t[0].IsEmpty, Is.True);
        }

        [Test]
        public void UnknownSpecies_Throws_AndIsNotPlayable()
        {
            var s = Snap(0, new MonsterSnapshot { SpeciesId = "nope", Level = 1 });
            Assert.Throws<SnapshotContentException>(() => TeamFactory.Build(s, Catalog(), X2));
            Assert.That(TeamFactory.IsPlayable(s, Catalog(), X2), Is.False);
        }

        [Test]
        public void HugeLevel_IsRejected_NotOverflow()
        {
            var s = Snap(0, new MonsterSnapshot { SpeciesId = "a", Level = 1000 });
            Assert.Throws<SnapshotContentException>(() => TeamFactory.Build(s, Catalog(), X2));
        }

        [Test]
        public void HugeLevel_WithMultiplierOne_IsQuick()
        {
            var s = Snap(0, new MonsterSnapshot { SpeciesId = "a", Level = int.MaxValue });
            var t = TeamFactory.Build(s, Catalog(), new UnitStatRules { LevelAttackMultiplier = 1 });
            Assert.That(t[0].Attack, Is.EqualTo(10));
        }

        [Test]
        public void Catalog_RejectsDuplicateId()
        {
            Assert.Throws<System.ArgumentException>(() => new SpeciesCatalog(new[]
            {
                new SpeciesDef { Id = "a", Attack = 1, AttackIntervalTicks = 1 },
                new SpeciesDef { Id = "a", Attack = 1, AttackIntervalTicks = 1 },
            }));
        }
    }
}
