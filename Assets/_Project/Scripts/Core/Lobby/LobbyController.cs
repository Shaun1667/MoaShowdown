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
    public enum CarouselPickResult
    {
        Picked = 0,
        /// <summary>대기석에 자리가 없어 몬스터 대신 가격만큼 골드.</summary>
        PickedAsGold = 1,
        NoCarousel = 2,
        NotYourTurn = 3,
        Taken = 4,
        Invalid = 5,
    }

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
        /// <summary>PvE 라운드 (L-4): 상대는 PvE 팀 (PlayerB = -1).</summary>
        public bool IsPve;
        /// <summary>PvE 보상으로 A가 받은 골드.</summary>
        public int GoldGainedA;

        public bool IsGhost => PlayerB < 0 && !IsPve;

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
    /// 라운드 = [캐러셀 라운드면 StartCarousel → 차례대로 PickCarousel] → BeginRound(살아 있는 모두 수입 · 상점 · 라운드 시작 능력, 봇은 바로 행동) → [사람은 상점 · 배치] → ResolveRound(대진 · 전투 · 목숨 · 탈락 · 순위).
    /// PvE 라운드는 대진 대신 모두가 같은 PvE 팀과 싸운다 (BALANCE 16).
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
            foreach (PveTeamDef t in _lobbyRules.PveTeams)
                foreach (MonsterSnapshot m in t.Board)
                    if (m != null && !_catalog.TryGet(m.SpeciesId, out _)) throw new ArgumentException($"PvE 팀에 모르는 몬스터 '{m.SpeciesId}'");
        }

        public ShopController Shop => _shop;

        /// <summary>이 로비의 공유 풀 (사람의 새로고침 · 팔기에 넘긴다).</summary>
        public static UnitPool PoolOf(LobbyState lobby) => new UnitPool(lobby.Pool);
        public RunRules RunRules => _runRules;

        /// <summary>준비 시간 (초, BALANCE 15).</summary>
        public int PrepSeconds => _lobbyRules.PrepSeconds;

        public LobbyRules Rules => _lobbyRules;

        /// <summary>이 로비의 지금 라운드가 PvE인지 (BALANCE 16).</summary>
        public bool IsPveRound(LobbyState lobby) => _lobbyRules.IsPveRound(lobby.Round);

        /// <summary>지금 라운드에 캐러셀이 있고 아직 안 끝났는지 (시작 전 포함).</summary>
        public bool CarouselPending(LobbyState lobby) =>
            _lobbyRules.IsCarouselRound(lobby.Round) && (lobby.Carousel == null || lobby.Carousel.Round != lobby.Round || !lobby.Carousel.IsDone);

        /// <summary>새 로비: 사람 humanNames.Count명 + 나머지는 봇. 플레이어마다 자기 시드(상점 난수용)를 로비 시드에서 뽑는다.</summary>
        public LobbyState NewLobby(string lobbyId, ulong seed, IReadOnlyList<string> humanNames, Func<int, string> botName)
        {
            if (humanNames == null || humanNames.Count > _lobbyRules.PlayerCount) throw new ArgumentException("사람 수가 로비 인원보다 많음");
            var lobby = new LobbyState { LobbyId = lobbyId, Seed = unchecked((long)seed), Round = 1 };
            // 공유 풀: 몬스터 표 순서대로, 등급별 수량 (BALANCE 14)
            foreach (SpeciesDef d in _catalog.All)
            {
                int r = Math.Min((int)d.Rarity, _lobbyRules.PoolSizeByRarity.Length - 1);
                lobby.Pool.Add(new PoolEntry { SpeciesId = d.Id, Count = _lobbyRules.PoolSizeByRarity[r] });
            }
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

        /// <summary>라운드 시작: 살아 있는 모두 수입 · 상점(공유 풀에서, 번호 순) · 라운드 시작 능력 (라운드당 한 번), 봇은 바로 사고 배치.</summary>
        /// <remarks>사람의 상점 조작(새로고침 · 팔기)도 같은 풀을 써야 한다 → <see cref="PoolOf"/>.</remarks>
        public void BeginRound(LobbyState lobby)
        {
            // 캐러셀 라운드면 상점보다 먼저 — 아직 안 고른 사람은 봇 규칙으로 대신 고름 (사람이 고르게 하려면 먼저 StartCarousel)
            if (_lobbyRules.IsCarouselRound(lobby.Round))
            {
                StartCarousel(lobby);
                while (!lobby.Carousel.IsDone) AutoPickCarousel(lobby);
            }
            var pool = new UnitPool(lobby.Pool);
            foreach (LobbyPlayer p in lobby.Players)
            {
                if (!p.IsAlive) continue;
                p.Run.Round = lobby.Round;
                bool fresh = p.Run.ShopRound != lobby.Round;
                _shop.BeginRound(p.Run, pool);
                if (fresh && p.IsBot) _bot.Act(p.Run, pool);
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

            ulong battleBase = SeedMix.Derive(lobbySeed, SeedMix.PurposeBattle, round);
            PveTeamDef pve = _lobbyRules.PveTeamFor(round);
            if (pve != null)
            {
                // PvE: 살아 있는 모두가 같은 PvE 팀과 싸움 (대진 · 최근 상대 기록 없음)
                TeamSnapshot wild = PveSnapshot(pve, round);
                for (int k = 0; k < alive.Count; k++)
                {
                    int a = alive[k];
                    var m = new LobbyMatch { PlayerA = a, PlayerB = -1, IsPve = true, SnapshotA = snapshots[a], SnapshotB = wild };
                    FightPve(lobby, m, pve, round, battleBase ^ ((ulong)(uint)k * 0x9E3779B97F4A7C15UL));
                    report.Matches.Add(m);
                }
            }
            else
            {
                var rng = new DeterministicRandom(SeedMix.Derive(lobbySeed, SeedMix.PurposePairing, round));
                List<(int a, int b)> pairs = Pair(lobby, alive, rng, out int ghostFighter);

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
            }

            // 탈락 · 순위
            List<LobbyPlayer> dead = lobby.Players
                .Where(p => p.Placement == 0 && p.Run.Lives <= 0)
                .OrderBy(p => livesBefore[p.Index])
                .ThenBy(p => p.Index)
                .ToList();
            int place = alive.Count;
            var deadPool = new UnitPool(lobby.Pool);
            foreach (LobbyPlayer p in dead)
            {
                p.Placement = place--;
                _shop.ReturnEverything(p.Run, deadPool); // 탈락자 몬스터 · 상점 카드는 풀로
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

        private int LoseLives(RunState r, int round) => LoseLivesExactly(r, _runRules.GetLifeLoss(round));

        private static int LoseLivesExactly(RunState r, int amount)
        {
            int loss = Math.Min(amount, r.Lives);
            r.Lives -= loss;
            r.LivesLost += loss;
            return loss;
        }

        // ───────── PvE (L-4) ─────────

        /// <summary>PvE 전투: 이기면 승 +1 · 이김 골드, 지면 목숨 PveLifeLoss · 짐 골드, 무승부는 짐 골드만.</summary>
        private void FightPve(LobbyState lobby, LobbyMatch m, PveTeamDef pve, int round, ulong seed)
        {
            m.Seed = seed;
            m.TeamA = TeamFactory.Build(m.SnapshotA, _catalog, _statRules);
            m.TeamB = TeamFactory.Build(m.SnapshotB, _catalog, _statRules);
            m.Battle = BattleSimulator.Run(_battleRules, m.TeamA, m.TeamB, round, seed);
            RunState a = lobby.Players[m.PlayerA].Run;
            switch (m.Battle.Outcome)
            {
                case BattleOutcome.AWins:
                    a.Wins++;
                    m.GoldGainedA = pve.WinGold;
                    break;
                case BattleOutcome.BWins:
                    m.LivesLostA = LoseLivesExactly(a, _lobbyRules.PveLifeLoss);
                    m.GoldGainedA = pve.LoseGold;
                    break;
                default:
                    m.GoldGainedA = pve.LoseGold;
                    break;
            }
            a.Gold += m.GoldGainedA;
        }

        /// <summary>PvE 팀의 전투용 스냅샷 (모두 같은 팀과 싸움).</summary>
        public TeamSnapshot PveSnapshot(PveTeamDef pve, int round)
        {
            var snap = new TeamSnapshot
            {
                RulesVersion = _rulesVersion,
                RunId = "pve",
                Round = round,
                TrainerId = "pve",
            };
            for (int i = 0; i < BattleTeam.SlotCount; i++)
            {
                MonsterSnapshot m = pve.Board[i];
                if (m != null) snap.Board[i] = new MonsterSnapshot { SpeciesId = m.SpeciesId, Level = m.Level };
            }
            return snap;
        }

        // ───────── 캐러셀 (L-4) ─────────

        /// <summary>
        /// 이 라운드 캐러셀을 연다 (이미 열려 있으면 그대로): 공유 풀에서 (생존자 + 1)마리를 시드 난수로 꺼내 놓고,
        /// 순서 = 목숨 적은 사람부터, 같으면 번호 순. 캐러셀 라운드가 아니면 예외.
        /// </summary>
        public CarouselState StartCarousel(LobbyState lobby)
        {
            if (!_lobbyRules.IsCarouselRound(lobby.Round)) throw new InvalidOperationException($"{lobby.Round}라운드는 캐러셀 라운드가 아님");
            if (lobby.Carousel != null && lobby.Carousel.Round == lobby.Round) return lobby.Carousel;

            List<LobbyPlayer> alive = lobby.Players.Where(p => p.IsAlive).OrderBy(p => p.Run.Lives).ThenBy(p => p.Index).ToList();
            var c = new CarouselState { Round = lobby.Round };
            c.Order.AddRange(alive.Select(p => p.Index));
            var pool = new UnitPool(lobby.Pool);
            var rng = new DeterministicRandom(SeedMix.Derive(unchecked((ulong)lobby.Seed), SeedMix.PurposeCarousel, lobby.Round));
            for (int i = 0; i < alive.Count + 1; i++)
            {
                string id = _shop.DrawCard(pool, lobby.Round, rng); // 상점과 같은 라운드 등급 확률표 (3-2)
                if (id == null) break; // 풀이 비면 놓을 수 있는 만큼만
                c.Units.Add(id);
                c.TakenBy.Add(-1);
            }
            lobby.Carousel = c;
            SkipIfNothingLeft(lobby);
            return c;
        }

        /// <summary>
        /// 지금 차례인 player가 units[unitIndex]를 가져간다. 대기석에 자리가 없고 합체도 안 되면 그 몬스터는 풀로 돌아가고 가격만큼 골드.
        /// 마지막 차례가 끝나면 남은 몬스터는 풀로.
        /// </summary>
        public CarouselPickResult PickCarousel(LobbyState lobby, int player, int unitIndex)
        {
            CarouselState c = lobby.Carousel;
            if (c == null || c.Round != lobby.Round || c.IsDone) return CarouselPickResult.NoCarousel;
            if (c.CurrentPicker != player) return CarouselPickResult.NotYourTurn;
            if (unitIndex < 0 || unitIndex >= c.Units.Count) return CarouselPickResult.Invalid;
            if (c.TakenBy[unitIndex] >= 0) return CarouselPickResult.Taken;

            string id = c.Units[unitIndex];
            c.TakenBy[unitIndex] = player;
            RunState run = lobby.Players[player].Run;
            CarouselPickResult result = CarouselPickResult.Picked;
            if (!_shop.Gain(run, id))
            {
                new UnitPool(lobby.Pool).Return(id, 1);
                run.Gold += _shop.PriceOf(id);
                result = CarouselPickResult.PickedAsGold;
            }
            c.Next++;
            SkipIfNothingLeft(lobby);
            return result;
        }

        /// <summary>지금 차례를 봇 규칙으로 대신 고른다 (봇 차례 · 사람 시간 끝).</summary>
        public CarouselPickResult AutoPickCarousel(LobbyState lobby)
        {
            CarouselState c = lobby.Carousel;
            if (c == null || c.Round != lobby.Round || c.IsDone) return CarouselPickResult.NoCarousel;
            int player = c.CurrentPicker;
            return PickCarousel(lobby, player, _bot.ChooseCarousel(lobby.Players[player].Run, c));
        }

        /// <summary>지금 차례가 봇이면 봇이 고른다 — 사람 차례가 오거나 끝날 때까지.</summary>
        public void AdvanceCarouselBots(LobbyState lobby)
        {
            CarouselState c = lobby.Carousel;
            while (c != null && c.Round == lobby.Round && !c.IsDone && lobby.Players[c.CurrentPicker].IsBot)
                AutoPickCarousel(lobby);
        }

        /// <summary>남은 몬스터가 없으면 남은 차례를 건너뛰고, 끝났으면 안 가져간 몬스터를 풀로.</summary>
        private static void SkipIfNothingLeft(LobbyState lobby)
        {
            CarouselState c = lobby.Carousel;
            if (!c.TakenBy.Contains(-1)) c.Next = c.Order.Count;
            if (!c.IsDone) return;
            var pool = new UnitPool(lobby.Pool);
            for (int i = 0; i < c.Units.Count; i++)
                if (c.TakenBy[i] < 0) pool.Return(c.Units[i], 1);
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
