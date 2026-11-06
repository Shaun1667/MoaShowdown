using System.Collections.Generic;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Run;
using Moa.Showdown.Core.Shop;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Lobby
{
    /// <summary>
    /// 봇 상점 행동 (간단한 규칙, 난수 없음 — 같은 상태 = 같은 행동, 결정 2026-11-06 "간단한 규칙 AI").
    /// 1) 살 수 있는 동안: 이미 가진 몬스터(합체 노림) → 초당 피해 높은 몬스터 순으로 산다. 대기석이 가득하면 대기석에서 가장 약한 것을 팔고 한 번 더 시도.
    /// 2) 배치: 대기석 몬스터를 보드 빈칸에 강한 것부터 채우고, 보드가 가득하면 보드의 가장 약한 것보다 강한 대기석 몬스터와 맞바꾼다.
    /// 새로고침은 하지 않는다.
    /// </summary>
    public sealed class BotBrain
    {
        private readonly ShopController _shop;
        private readonly SpeciesCatalog _catalog;
        private readonly UnitStatRules _statRules;

        public BotBrain(ShopController shop, SpeciesCatalog catalog, UnitStatRules statRules)
        {
            _shop = shop;
            _catalog = catalog;
            _statRules = statRules;
        }

        public void Act(RunState s, UnitPool pool = null)
        {
            Buy(s, pool);
            Place(s);
        }

        private void Buy(RunState s, UnitPool pool)
        {
            bool soldOnce = false;
            while (true)
            {
                int best = -1;
                long bestScore = long.MinValue;
                for (int i = 0; i < s.Shop.Count; i++)
                {
                    string id = s.Shop[i];
                    if (id == null || _shop.PriceOf(id) > s.Gold) continue;
                    long score = Owned(s, id) * 1_000_000L + Power(new MonsterSnapshot { SpeciesId = id, Level = 1 });
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best < 0) return;

                ShopError e = _shop.Buy(s, best);
                if (e == ShopError.None) continue;
                if (e == ShopError.BenchFull && !soldOnce)
                {
                    soldOnce = true;
                    int weakest = WeakestBench(s);
                    if (weakest >= 0 && _shop.Sell(s, Location.Bench(weakest), pool) == ShopError.None) continue;
                }
                return;
            }
        }

        private void Place(RunState s)
        {
            // 빈칸 채우기: 대기석에서 강한 것부터 보드 앞 칸부터
            while (true)
            {
                int bench = StrongestBench(s);
                int empty = System.Array.IndexOf(s.Team, null);
                if (bench < 0 || empty < 0) break;
                _shop.Move(s, Location.Bench(bench), Location.Board(empty));
            }
            // 가득하면 더 강한 대기석 몬스터와 맞바꾸기
            while (true)
            {
                int bench = StrongestBench(s);
                int weakBoard = WeakestBoard(s);
                if (bench < 0 || weakBoard < 0) break;
                if (Power(s.Bench[bench]) <= Power(s.Team[weakBoard])) break;
                _shop.Move(s, Location.Bench(bench), Location.Board(weakBoard));
            }
        }

        /// <summary>힘 = 초당 피해 비슷한 값 (공격력 × 1000 / 간격) — 정수.</summary>
        private long Power(MonsterSnapshot m)
        {
            BattleUnit u = TeamFactory.BuildUnit(m, _catalog, _statRules);
            return (long)u.Attack * 1000 / u.AttackIntervalTicks;
        }

        private static int Owned(RunState s, string id)
        {
            int n = 0;
            foreach (MonsterSnapshot m in s.Team) if (m != null && m.SpeciesId == id && m.Level == 1) n++;
            foreach (MonsterSnapshot m in s.Bench) if (m != null && m.SpeciesId == id && m.Level == 1) n++;
            return n;
        }

        private int StrongestBench(RunState s) => Pick(s.Bench, strongest: true);
        private int WeakestBench(RunState s) => Pick(s.Bench, strongest: false);
        private int WeakestBoard(RunState s) => Pick(s.Team, strongest: false);

        private int Pick(IReadOnlyList<MonsterSnapshot> slots, bool strongest)
        {
            int best = -1;
            long bestPower = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                long p = Power(slots[i]);
                if (best < 0 || (strongest ? p > bestPower : p < bestPower))
                {
                    best = i;
                    bestPower = p;
                }
            }
            return best;
        }
    }
}
