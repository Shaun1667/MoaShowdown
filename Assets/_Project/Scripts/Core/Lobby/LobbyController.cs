using System;
using System.Collections.Generic;
using System.Linq;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>한 라운드의 대전 하나.</summary>
    public sealed class LobbyMatch
    {
        public int PlayerA;
        /// <summary>상대 플레이어 번호. 유령전이면 -1 (GhostOf = 복사한 플레이어).</summary>
        public int PlayerB;
        public int GhostOf = -1;
        public TeamSnapshot SnapshotA;
        public TeamSnapshot SnapshotB;
        public BattleTeam TeamA;
        public BattleTeam TeamB;
        public BattleResult Battle;
        /// <summary>이 전투의 시드 (다시 보기 · 온라인에서 모두가 같은 전투를 계산하는 데 씀).</summary>
        public ulong Seed;
        public int LivesLostA;
        public int LivesLostB;

        public bool IsGhost => PlayerB < 0;

        public bool Involves(int player) => PlayerA == player || PlayerB == player;
    }

    public sealed class LobbyRoundReport
    {
        public int Round;
        public List<LobbyMatch> Matches = new List<LobbyMatch>();
        /// <summary>이번 라운드에 탈락한 플레이어 (낮은 순위부터).</summary>
        public List<int> Eliminated = new List<int>();
        public bool FinishedAfter;

        public LobbyMatch MatchOf(int player) => Matches.FirstOrDefault(m => m.Involves(player));
    }

    /// <summary>
    /// 8인 로비 진행 한 곳 (CLAUDE 핵심 규칙 7: 방장 · 오프라인 · 나중의 서버가 모두 이 코드를 실행).
    /// 라운드 = BeginRound(살아 있는 모두 수입 · 상점 · 라운드 시작 능력, 봇은 바로 행동) → [사람은 상점 · 배치] → ResolveRound(대진 · 전투 · 목숨 · 탈락 · 순위).
    /// 대진: 시드 난수로 섞고 최근 상대를 피해 짝 → 홀수면 한 명은 다른 생존자 팀의 복사본(유령)과 싸움(유령 주인은 영향 없음).
    /// 탈락: 목숨 0 → 순위 = 그 시점 생존자 수 (같은 라운드에 여럿이면 라운드 전 목숨이 많았던 쪽이 높은 순위, 같으면 번호 큰 쪽이 높은 순위). 마지막 1명 = 1등.
    /// </summary>
    public sealed class LobbyController
    {
        private readonly LobbyRules _lobbyRules;
        private readonly RunRules _runRules;
        private readonly BattleRules _battleRules;
        private readonly UnitStatRules _statRules;
        private readonly SpeciesCatalog _catalog;
        private readonly ShopController _shop;
        private readonly BotBrain _bot;
        private readonly int _rulesVersion;

        public LobbyController(LobbyRules lobbyRules, RunRules runRules, BattleRules battleRules, UnitStatRules statRules,
            SpeciesCatalog catalog, ShopController shop, int rulesVersion)
        {
            _lobbyRules = lobbyRules ?? throw new ArgumentNullException(nameof(lobbyRules));
            _runRules = runRules ?? throw new ArgumentNullException(nameof(runRules));
            _battleRules = battleRules ?? throw new ArgumentNullException(nameof(battleRules));
            _statRules = statRules ?? throw new ArgumentNullException(nameof(statRules));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _rulesVersion = rulesVersion;
            _lobbyRules.Validate();
            _runRules.Validate();
            _battleRules.Validate();
            _bot = new BotBrain(shop, catalog, statRules);
        }

        public ShopController Shop => _shop;
        public RunRules RunRules => _runRules;

        /// <summary>새 로비: 사람 humanNames.Count명 + 나머지는 봇. 플레이어마다 자기 시드(상점 난수용)를 로비 시드에서 뽑는다.</summary>
        public LobbyState NewLobby(string lobbyId, ulong seed, IReadOnlyList<string> humanNames, Func<int, string> botName)
        {
            if (humanNames == null || humanNames.Count > _lobbyRules.PlayerCount) throw new ArgumentException("사람 수가 로비 인원보다 많음");
            var lobby = new LobbyState { LobbyId = lobbyId, Seed = unchecked((long)seed), Round = 1 };
            int botNumber = 1;
            for (int i = 0; i < _lobbyRules.PlayerCount; i++)
            {
                bool isBot = i >= humanNames.Count;
                ulong playerSeed = SeedMix.Derive(seed, SeedMix.PurposeLobbyPlayer, i);
                var run = new RunState
                {
                    RunId = $"{lobbyId}_p{i}",
                    Seed = unchecked((long)playerSeed),
                    Round = 1,
                    Lives = _runRules.StartLives,
                    TrainerId = isBot ? "bot" : "player",
                };
                lobby.Players.Add(new LobbyPlayer
                {
                    Index = i,
                    Name = isBot ? botName(botNumber++) : humanNames[i],
                    IsBot = isBot,
                    Run = run,
                });
            }
            return lobby;
        }

        /// <summary>라운드 시작: 살아 있는 모두 수입 · 상점 · 라운드 시작 능력 (라운드당 한 번), 봇은 바로 사고 배치.</summary>
        public void BeginRound(LobbyState lobby)
        {
            foreach (LobbyPlayer p in lobby.Players)
            {
                if (!p.IsAlive) continue;
                p.Run.Round = lobby.Round;
                bool fresh = p.Run.ShopRound != lobby.Round;
                _shop.BeginRound(p.Run);
                if (fresh && p.IsBot) _bot.Act(p.Run);
            }
        }

        /// <summary>대진 → 전투 → 목숨 · 탈락 · 순위 → 다음 라운드 번호. 이미 끝난 로비면 예외.</summary>
        public LobbyRoundReport ResolveRound(LobbyState lobby)
        {
            if (lobby.IsFinished) throw new InvalidOperationException("이미 끝난 로비");
            ulong lobbySeed = unchecked((ulong)lobby.Seed);
            int round = lobby.Round;
            var report = new LobbyRoundReport { Round = round };

            List<int> alive = lobby.Players.Where(p => p.IsAlive).Select(p => p.Index).ToList();
            var livesBefore = lobby.Players.ToDictionary(p => p.Index, p => p.Run.Lives);
            var snapshots = alive.ToDictionary(i => i, i => MakeSnapshot(lobby.Players[i].Run));

            var rng = new DeterministicRandom(SeedMix.Derive(lobbySeed, SeedMix.PurposePairing, round));
            List<(int a, int b)> pairs = Pair(lobby, alive, rng, out int ghostFighter);

            ulong battleBase = SeedMix.Derive(lobbySeed, SeedMix.PurposeBattle, round);
            int matchIndex = 0;
            foreach (var (a, b) in pairs)
            {
                var m = new LobbyMatch { PlayerA = a, PlayerB = b, SnapshotA = snapshots[a], SnapshotB = snapshots[b] };
                Fight(lobby, m, round, battleBase ^ ((ulong)(uint)matchIndex++ * 0x9E3779B97F4A7C15UL));
                report.Matches.Add(m);
            }
            if (ghostFighter >= 0)
            {
                List<int> others = alive.Where(i => i != ghostFighter).ToList();
                int owner = others[rng.NextInt(others.Count)];
                var m = new LobbyMatch { PlayerA = ghostFighter, PlayerB = -1, GhostOf = owner, SnapshotA = snapshots[ghostFighter], SnapshotB = snapshots[owner] };
                Fight(lobby, m, round, battleBase ^ ((ulong)(uint)matchIndex * 0x9E3779B97F4A7C15UL));
                report.Matches.Add(m);
            }

            // 최근 상대 기록 (유령전 제외)
            foreach (var (a, b) in pairs)
            {
                Remember(lobby.Players[a], b);
                Remember(lobby.Players[b], a);
            }

            // 탈락 · 순위
            List<LobbyPlayer> dead = lobby.Players
                .Where(p => p.Placement == 0 && p.Run.Lives <= 0)
                .OrderBy(p => livesBefore[p.Index])
                .ThenBy(p => p.Index)
                .ToList();
            int place = alive.Count;
            foreach (LobbyPlayer p in dead)
            {
                p.Placement = place--;
                lobby.EliminationOrder.Add(p.Index);
                report.Eliminated.Add(p.Index);
            }
            List<LobbyPlayer> survivors = lobby.Players.Where(p => p.Placement == 0).ToList();
            if (survivors.Count == 1) survivors[0].Placement = 1;
            else if (survivors.Count == 0 && dead.Count > 0)
            {
                // 남은 모두가 같은 라운드에 탈락: 위 순서대로 이미 순위가 매겨졌고, 가장 높은 순위를 1등으로
                LobbyPlayer best = dead[dead.Count - 1];
                best.Placement = 1;
                lobby.EliminationOrder.Remove(best.Index);
            }

            report.FinishedAfter = lobby.IsFinished;
            if (!report.FinishedAfter) lobby.Round++;
            return report;
        }

        /// <summary>플레이어의 지금 팀 스냅샷 (보드만).</summary>
        public TeamSnapshot MakeSnapshot(RunState run)
        {
            var snap = new TeamSnapshot
            {
                RulesVersion = _rulesVersion,
                RunId = run.RunId,
                Round = run.Round,
                Wins = run.Wins,
                LivesLost = run.LivesLost,
                TrainerId = run.TrainerId,
            };
            for (int i = 0; i < BattleTeam.SlotCount; i++)
            {
                MonsterSnapshot m = run.Team[i];
                if (m == null) continue;
                var c = new MonsterSnapshot { SpeciesId = m.SpeciesId, Level = m.Level };
                foreach (StatBuff b in m.Buffs) c.Buffs.Add(new StatBuff(b.Stat, b.Value));
                snap.Board[i] = c;
            }
            return snap;
        }

        private void Fight(LobbyState lobby, LobbyMatch m, int round, ulong seed)
        {
            m.Seed = seed;
            m.TeamA = TeamFactory.Build(m.SnapshotA, _catalog, _statRules);
            m.TeamB = TeamFactory.Build(m.SnapshotB, _catalog, _statRules);
            m.Battle = BattleSimulator.Run(_battleRules, m.TeamA, m.TeamB, round, seed);
            RunState a = lobby.Players[m.PlayerA].Run;
            RunState b = m.IsGhost ? null : lobby.Players[m.PlayerB].Run;
            switch (m.Battle.Outcome)
            {
                case BattleOutcome.AWins:
                    a.Wins++;
                    if (b != null) m.LivesLostB = LoseLives(b, round);
                    break;
                case BattleOutcome.BWins:
                    m.LivesLostA = LoseLives(a, round);
                    if (b != null) b.Wins++;
                    break;
            }
        }

        private int LoseLives(RunState r, int round)
        {
            int loss = Math.Min(_runRules.GetLifeLoss(round), r.Lives);
            r.Lives -= loss;
            r.LivesLost += loss;
            return loss;
        }

        private void Remember(LobbyPlayer p, int opponent)
        {
            p.RecentOpponents.Add(opponent);
            while (p.RecentOpponents.Count > Math.Max(1, _lobbyRules.RecentOpponentMemory)) p.RecentOpponents.RemoveAt(0);
        }

        /// <summary>
        /// 짝짓기: 생존자를 시드 난수로 섞고, 최근 상대를 피하는 짝을 앞에서부터 찾는다. 못 찾으면 피할 기억을 줄여 다시 (0이면 아무나).
        /// 홀수면 맨 끝 한 명이 유령전 (ghostFighter).
        /// </summary>
        private List<(int a, int b)> Pair(LobbyState lobby, List<int> alive, DeterministicRandom rng, out int ghostFighter)
        {
            var order = new List<int>(alive);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            ghostFighter = -1;
            if (order.Count % 2 == 1)
            {
                ghostFighter = order[order.Count - 1];
                order.RemoveAt(order.Count - 1);
            }

            int maxMemory = Math.Min(_lobbyRules.RecentOpponentMemory, Math.Max(0, order.Count - 2));
            for (int memory = maxMemory; memory >= 0; memory--)
            {
                var result = new List<(int, int)>();
                if (TryPair(lobby, order, new bool[order.Count], memory, result)) return result;
            }
            throw new InvalidOperationException("짝짓기 실패"); // memory 0이면 항상 성공하므로 오지 않음
        }

        private static bool TryPair(LobbyState lobby, List<int> order, bool[] used, int memory, List<(int, int)> result)
        {
            int first = Array.IndexOf(used, false);
            if (first < 0) return true;
            used[first] = true;
            int a = order[first];
            for (int k = first + 1; k < order.Count; k++)
            {
                if (used[k]) continue;
                int b = order[k];
                if (memory > 0 && RecentlyMet(lobby.Players[a], b, memory)) continue;
                used[k] = true;
                result.Add((a, b));
                if (TryPair(lobby, order, used, memory, result)) return true;
                result.RemoveAt(result.Count - 1);
                used[k] = false;
            }
            used[first] = false;
            return false;
        }

        private static bool RecentlyMet(LobbyPlayer p, int other, int memory)
        {
            List<int> r = p.RecentOpponents;
            for (int i = Math.Max(0, r.Count - memory); i < r.Count; i++)
                if (r[i] == other) return true;
            return false;
        }
    }
}
