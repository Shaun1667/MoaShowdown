using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Lobby;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    /// <summary>L-5 로비 목록: 순서(목숨 많은 순 → 탈락자는 순위 순) · 이번 상대(A ↔ B · 유령 · PvE).</summary>
    public class LobbyStandingsTests
    {
        private static LobbyController Ctl(int players, bool pve = false)
        {
            var catalog = new SpeciesCatalog(new[]
            {
                new SpeciesDef { Id = "s", DisplayName = "S", Attack = 10, AttackIntervalTicks = 10 },
            });
            var stat = new UnitStatRules { LevelAttackMultiplier = 2 };
            var shop = new ShopController(new ShopRules
            {
                StartGold = 30, FirstIncome = 35, IncomeStep = 5, ShopSlots = 5, RerollCost = 3,
                PriceByRarity = new[] { 10, 20, 30, 40, 50 }, SellPercent = 75, MergeCountByLevel = new[] { 3, 2 }, MaxLevel = 3,
            }, catalog, stat);
            var wild = new PveTeamDef { WinGold = 5, LoseGold = 2 };
            wild.Board[0] = new MonsterSnapshot { SpeciesId = "s", Level = 1 };
            return new LobbyController(
                new LobbyRules
                {
                    PlayerCount = players, RecentOpponentMemory = 3, PoolSizeByRarity = new[] { 29, 22, 18, 12, 10 }, PrepSeconds = 30,
                    PveOpeningRounds = pve ? 1 : 0, PveLifeLoss = 1, PveTeams = pve ? new List<PveTeamDef> { wild } : new List<PveTeamDef>(),
                },
                new RunRules { WinsToChampion = 10, StartLives = 10, LifeLossByRound = new[] { 1 } },
                new BattleRules
                {
                    TicksPerSecond = 10, TeamHpByRound = new[] { 100 }, SuddenDeathStartTick = 300, SuddenDeathIntervalTicks = 10,
                    SuddenDeathBaseDamage = 20, SuddenDeathDamageStep = 20, MaxTicks = 2000, BurnIntervalTicks = 10, PoisonIntervalTicks = 20,
                },
                stat, catalog, shop, 1);
        }

        private static LobbyState New(LobbyController c) => c.NewLobby("L", 7, new[] { "나" }, n => "봇 " + n);

        [Test]
        public void Order_AliveByLivesThenIndex_ThenOutByPlacement()
        {
            LobbyState l = New(Ctl(6));
            l.Players[0].Run.Lives = 4;
            l.Players[2].Run.Lives = 9;
            l.Players[4].Run.Lives = 9;
            l.Players[1].Placement = 6;
            l.Players[1].Run.Lives = 0;
            l.Players[5].Placement = 5;
            l.Players[5].Run.Lives = 0;

            List<StandingRow> rows = LobbyStandings.Build(l);
            // 살아 있음: 3(10) → 2(9) → 4(9) → 0(4) / 탈락: 5(5등) → 1(6등)
            Assert.That(rows.Select(r => r.Index), Is.EqualTo(new[] { 3, 2, 4, 0, 5, 1 }));
            Assert.That(rows[0].Name, Is.EqualTo("봇 3"));
            Assert.That(rows.Count(r => !r.IsAlive), Is.EqualTo(2));
            Assert.That(rows.All(r => r.Opponent == OpponentKind.None), Is.True, "결과 없이 부르면 상대 없음");
        }

        [Test]
        public void Opponents_FromReport_BothSidesAndGhost()
        {
            var c = Ctl(5); // 홀수 → 한 명은 유령전
            LobbyState l = New(c);
            LobbyRoundReport rep = c.ResolveRound(l);
            List<StandingRow> rows = LobbyStandings.Build(l, rep);

            foreach (LobbyMatch m in rep.Matches)
            {
                StandingRow a = rows.Single(r => r.Index == m.PlayerA);
                if (m.IsGhost)
                {
                    Assert.That((a.Opponent, a.OpponentIndex), Is.EqualTo((OpponentKind.Ghost, m.GhostOf)));
                    continue;
                }
                StandingRow b = rows.Single(r => r.Index == m.PlayerB);
                Assert.That((a.Opponent, a.OpponentIndex), Is.EqualTo((OpponentKind.Player, m.PlayerB)));
                Assert.That((b.Opponent, b.OpponentIndex), Is.EqualTo((OpponentKind.Player, m.PlayerA)), "B 쪽도 A를 상대로");
            }
            Assert.That(rows.Count(r => r.Opponent == OpponentKind.Ghost), Is.EqualTo(1));
            Assert.That(rows.Count(r => r.Opponent == OpponentKind.Player), Is.EqualTo(4));
        }

        [Test]
        public void BeforeResults_HidesThisRoundsLivesAndElimination()
        {
            var c = Ctl(2);
            LobbyState l = New(c);
            l.Players[1].Run.Team[0] = new MonsterSnapshot { SpeciesId = "s", Level = 3 }; // 봇 1이 이김
            l.Players[0].Run.Lives = 1;
            LobbyRoundReport rep = c.ResolveRound(l);
            Assert.That(l.Players[0].Placement, Is.EqualTo(2), "나는 탈락, 봇 1 우승");

            List<StandingRow> during = LobbyStandings.Build(l, rep, beforeResults: true);
            StandingRow me = during.Single(r => r.Index == 0);
            Assert.That((me.Lives, me.Placement), Is.EqualTo((1, 0)), "재생 중에는 라운드 전 목숨 1 · 아직 살아 있음");
            Assert.That(during.Single(r => r.Index == 1).Placement, Is.EqualTo(0), "우승도 아직");
            Assert.That(me.Opponent, Is.EqualTo(OpponentKind.Player), "상대는 보임");

            List<StandingRow> after = LobbyStandings.Build(l, rep);
            Assert.That(after.Select(r => (r.Index, r.Lives, r.Placement)), Is.EqualTo(new[] { (1, 10, 1), (0, 0, 2) }), "결과 뒤: 우승 → 2등");
        }

        [Test]
        public void Opponents_PveRound_AllWild()
        {
            var c = Ctl(4, pve: true);
            LobbyState l = New(c);
            LobbyRoundReport rep = c.ResolveRound(l);
            List<StandingRow> rows = LobbyStandings.Build(l, rep);
            Assert.That(rows.All(r => r.Opponent == OpponentKind.Pve && r.OpponentIndex == -1), Is.True);
        }
    }
}
