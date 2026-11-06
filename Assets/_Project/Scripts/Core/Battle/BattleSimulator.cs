using System;
using System.Collections.Generic;

namespace Moa.Showdown.Core.Battle
{
    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }
        public int EndTick { get; }
        public int HpA { get; }
        public int HpB { get; }
        public int StartHp { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        public BattleResult(BattleOutcome outcome, int endTick, int hpA, int hpB, int startHp, IReadOnlyList<BattleEvent> events)
        {
            Outcome = outcome;
            EndTick = endTick;
            HpA = hpA;
            HpB = hpB;
            StartHp = startHp;
            Events = events;
        }
    }

    /// <summary>
    /// 전투 계산 한 곳. 같은 (규칙, 팀 A, 팀 B, 라운드, 시드) → 항상 같은 결과 (CLAUDE 핵심 규칙 2).
    /// 한 틱의 순서: 공격 타이머 진행(A 0~5칸 → B 0~5칸) → 서든데스 → 피해를 양쪽 동시에 적용 → 종료 판정.
    /// 피해를 동시에 적용하므로 같은 틱에 둘 다 0이 되면 무승부.
    /// </summary>
    public static class BattleSimulator
    {
        public static BattleResult Run(BattleRules rules, BattleTeam teamA, BattleTeam teamB, int round, ulong seed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (teamA == null) throw new ArgumentNullException(nameof(teamA));
            if (teamB == null) throw new ArgumentNullException(nameof(teamB));
            rules.Validate();

            // 시드: 지금(1-1)은 난수를 쓰는 규칙이 없다. 치명타 · 능력 등이 생기면 여기서 전투 전용 DeterministicRandom을 만든다.
            _ = seed;

            int startHp = rules.GetTeamHp(round);
            int hpA = startHp;
            int hpB = startHp;
            var timers = new int[2, BattleTeam.SlotCount];
            var events = new List<BattleEvent>();

            for (int tick = 1; tick <= rules.MaxTicks; tick++)
            {
                int damageToA = 0;
                int damageToB = 0;

                damageToB += TickAttacks(BattleSide.A, teamA, timers, tick, events);
                damageToA += TickAttacks(BattleSide.B, teamB, timers, tick, events);

                if (tick >= rules.SuddenDeathStartTick && (tick - rules.SuddenDeathStartTick) % rules.SuddenDeathIntervalTicks == 0)
                {
                    int pulse = (tick - rules.SuddenDeathStartTick) / rules.SuddenDeathIntervalTicks;
                    int sd = rules.SuddenDeathBaseDamage + rules.SuddenDeathDamageStep * pulse;
                    damageToA += sd;
                    damageToB += sd;
                    events.Add(new BattleEvent(tick, BattleEventType.SuddenDeath, BattleSide.A, -1, sd, pulse));
                }

                if (damageToA > 0)
                {
                    hpA = Math.Max(0, hpA - damageToA);
                    events.Add(new BattleEvent(tick, BattleEventType.TeamHpChanged, BattleSide.A, -1, damageToA, hpA));
                }
                if (damageToB > 0)
                {
                    hpB = Math.Max(0, hpB - damageToB);
                    events.Add(new BattleEvent(tick, BattleEventType.TeamHpChanged, BattleSide.B, -1, damageToB, hpB));
                }

                if (hpA == 0 || hpB == 0)
                {
                    BattleOutcome outcome = hpA == 0 && hpB == 0 ? BattleOutcome.Draw
                        : hpB == 0 ? BattleOutcome.AWins
                        : BattleOutcome.BWins;
                    return Finish(outcome, tick, hpA, hpB, startHp, events);
                }
            }

            return Finish(BattleOutcome.Draw, rules.MaxTicks, hpA, hpB, startHp, events);
        }

        private static int TickAttacks(BattleSide side, BattleTeam team, int[,] timers, int tick, List<BattleEvent> events)
        {
            int s = (int)side;
            int total = 0;
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                BattleUnit unit = team[slot];
                if (unit.IsEmpty) continue;
                timers[s, slot]++;
                if (timers[s, slot] < unit.AttackIntervalTicks) continue;
                timers[s, slot] = 0;
                total += unit.Attack;
                events.Add(new BattleEvent(tick, BattleEventType.Attack, side, slot, unit.Attack, 0));
            }
            return total;
        }

        private static BattleResult Finish(BattleOutcome outcome, int tick, int hpA, int hpB, int startHp, List<BattleEvent> events)
        {
            events.Add(new BattleEvent(tick, BattleEventType.End, BattleSide.A, -1, 0, (int)outcome));
            return new BattleResult(outcome, tick, hpA, hpB, startHp, events);
        }
    }
}
