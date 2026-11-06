using System;
using System.Collections.Generic;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Online
{
    /// <summary>
    /// 상대 고르기 규칙 한 곳 (로컬 · 서버 공용).
    /// 플레이어 팀: 같은 라운드 · 같은 규칙 버전 · 내 판 아님 · 만난 적 없음 · 싸울 수 있음 → 승수가 가장 가까운 무리 → 그중 난수로 하나.
    /// 봇 팀: 같은 라운드가 있으면 그것, 없으면 그보다 낮은 라운드 중 가장 높은 것, 그것도 없으면 가장 낮은 라운드 → 그중 난수로 하나.
    /// 후보 목록의 순서가 결과에 영향을 주므로 호출하는 쪽이 순서를 고정해야 한다.
    /// </summary>
    public static class OpponentPicker
    {
        public static TeamSnapshot PickPlayer(IReadOnlyList<TeamSnapshot> candidates, OpponentQuery q, DeterministicRandom rng, Func<TeamSnapshot, bool> isPlayable)
        {
            var eligible = new List<TeamSnapshot>();
            int bestDiff = int.MaxValue;
            foreach (TeamSnapshot s in candidates)
            {
                if (s.Round != q.Round || s.RulesVersion != q.RulesVersion) continue;
                if (s.RunId != null && (s.RunId == q.OwnRunId || q.ExcludedRunIds.Contains(s.RunId))) continue;
                if (!isPlayable(s)) continue;
                int diff = Math.Abs(s.Wins - q.Wins);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    eligible.Clear();
                }
                if (diff == bestDiff) eligible.Add(s);
            }
            return eligible.Count == 0 ? null : eligible[rng.NextInt(eligible.Count)];
        }

        public static TeamSnapshot PickBot(IReadOnlyList<TeamSnapshot> bots, OpponentQuery q, DeterministicRandom rng, Func<TeamSnapshot, bool> isPlayable)
        {
            int bestBelow = int.MinValue;
            int lowest = int.MaxValue;
            foreach (TeamSnapshot s in bots)
            {
                if (!isPlayable(s)) continue;
                if (s.Round <= q.Round && s.Round > bestBelow) bestBelow = s.Round;
                if (s.Round < lowest) lowest = s.Round;
            }
            if (lowest == int.MaxValue) return null;
            int targetRound = bestBelow != int.MinValue ? bestBelow : lowest;

            var pool = new List<TeamSnapshot>();
            foreach (TeamSnapshot s in bots)
                if (s.Round == targetRound && isPlayable(s)) pool.Add(s);
            return pool[rng.NextInt(pool.Count)];
        }
    }
}
