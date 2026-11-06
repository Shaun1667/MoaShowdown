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
    /// 전투 시작(틱 0): BattleStart 능력.
    /// 한 틱의 순서: 공격(A 0~5칸 → B 0~5칸, 공격 능력 발동) → 화상(BurnInterval마다 쌓인 만큼, 그 뒤 1 감소)
    /// → 독(PoisonInterval마다 쌓인 만큼, 줄지 않음) → 서든데스 → 피해를 양쪽 동시에 적용(방어막이 먼저 받음)
    /// → 회복(살아 있을 때, 시작 체력까지) → 종료 판정.
    /// 피해를 동시에 적용하므로 같은 틱에 둘 다 0이 되면 무승부. 계산은 정수만 (long으로 모아 int 범위로 자름).
    /// </summary>
    public static class BattleSimulator
    {
        private sealed class SideState
        {
            public int Hp;
            public int Shield;
            /// <summary>이 쪽에 쌓인 화상 (상대가 걸어 둔 것).</summary>
            public int Burn;
            /// <summary>이 쪽에 쌓인 독.</summary>
            public int Poison;
            public long IncomingDamage;
            public long IncomingHeal;
            public bool ShieldGained;
            public int BurnAtTickStart;
            public int PoisonAtTickStart;

            public void BeginTick()
            {
                IncomingDamage = 0;
                IncomingHeal = 0;
                ShieldGained = false;
                BurnAtTickStart = Burn;
                PoisonAtTickStart = Poison;
            }
        }

        public static BattleResult Run(BattleRules rules, BattleTeam teamA, BattleTeam teamB, int round, ulong seed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (teamA == null) throw new ArgumentNullException(nameof(teamA));
            if (teamB == null) throw new ArgumentNullException(nameof(teamB));
            rules.Validate();

            // 시드: 지금은 난수를 쓰는 규칙이 없다. 치명타 등이 생기면 여기서 전투 전용 DeterministicRandom을 만든다.
            _ = seed;

            int startHp = rules.GetTeamHp(round);
            var teams = new[] { teamA, teamB };
            var sides = new[] { new SideState { Hp = startHp }, new SideState { Hp = startHp } };
            var timers = new int[2, BattleTeam.SlotCount];
            var attackCounts = new int[2, BattleTeam.SlotCount];
            var events = new List<BattleEvent>();

            // 전투 시작 능력 (틱 0)
            sides[0].BeginTick();
            sides[1].BeginTick();
            for (int s = 0; s < 2; s++)
            {
                for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
                {
                    BattleUnit u = teams[s][slot];
                    if (u.IsEmpty || u.Ability.IsNone || u.Ability.Trigger != AbilityTrigger.BattleStart) continue;
                    long none = 0;
                    ApplyAbility(0, (BattleSide)s, slot, u.Ability, sides, events, ref none);
                }
            }
            EndOfTickRecords(0, sides, events);

            for (int tick = 1; tick <= rules.MaxTicks; tick++)
            {
                sides[0].BeginTick();
                sides[1].BeginTick();

                for (int s = 0; s < 2; s++)
                    TickAttacks((BattleSide)s, teams[s], timers, attackCounts, tick, sides, events);

                if (tick % rules.BurnIntervalTicks == 0)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        SideState st = sides[s];
                        if (st.Burn <= 0) continue;
                        st.IncomingDamage += st.Burn;
                        events.Add(new BattleEvent(tick, BattleEventType.DamageOverTime, (BattleSide)s, -1, st.Burn, 0));
                        st.Burn--;
                    }
                }

                if (tick % rules.PoisonIntervalTicks == 0)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        SideState st = sides[s];
                        if (st.Poison <= 0) continue;
                        st.IncomingDamage += st.Poison;
                        events.Add(new BattleEvent(tick, BattleEventType.DamageOverTime, (BattleSide)s, -1, st.Poison, 1));
                    }
                }

                if (tick >= rules.SuddenDeathStartTick && (tick - rules.SuddenDeathStartTick) % rules.SuddenDeathIntervalTicks == 0)
                {
                    int pulse = (tick - rules.SuddenDeathStartTick) / rules.SuddenDeathIntervalTicks;
                    int sd = rules.SuddenDeathBaseDamage + rules.SuddenDeathDamageStep * pulse;
                    sides[0].IncomingDamage += sd;
                    sides[1].IncomingDamage += sd;
                    events.Add(new BattleEvent(tick, BattleEventType.SuddenDeath, BattleSide.A, -1, sd, pulse));
                }

                // 피해: 방어막이 먼저 받는다 (양쪽 동시)
                for (int s = 0; s < 2; s++)
                {
                    SideState st = sides[s];
                    if (st.IncomingDamage <= 0) continue;
                    long absorbed = Math.Min(st.Shield, st.IncomingDamage);
                    long rest = st.IncomingDamage - absorbed;
                    if (absorbed > 0)
                    {
                        st.Shield -= (int)absorbed;
                        events.Add(new BattleEvent(tick, BattleEventType.ShieldChanged, (BattleSide)s, -1, (int)absorbed, st.Shield));
                    }
                    if (rest > 0)
                    {
                        st.Hp = (int)Math.Max(0, st.Hp - rest);
                        events.Add(new BattleEvent(tick, BattleEventType.TeamHpChanged, (BattleSide)s, -1, (int)Math.Min(rest, int.MaxValue), st.Hp));
                    }
                }

                // 회복: 살아 있을 때만, 시작 체력까지
                for (int s = 0; s < 2; s++)
                {
                    SideState st = sides[s];
                    if (st.IncomingHeal <= 0 || st.Hp <= 0) continue;
                    int healed = (int)Math.Min(startHp - st.Hp, st.IncomingHeal);
                    if (healed <= 0) continue;
                    st.Hp += healed;
                    events.Add(new BattleEvent(tick, BattleEventType.Heal, (BattleSide)s, -1, healed, st.Hp));
                }

                EndOfTickRecords(tick, sides, events);

                if (sides[0].Hp == 0 || sides[1].Hp == 0)
                {
                    BattleOutcome outcome = sides[0].Hp == 0 && sides[1].Hp == 0 ? BattleOutcome.Draw
                        : sides[1].Hp == 0 ? BattleOutcome.AWins
                        : BattleOutcome.BWins;
                    return Finish(outcome, tick, sides[0].Hp, sides[1].Hp, startHp, events);
                }
            }

            return Finish(BattleOutcome.Draw, rules.MaxTicks, sides[0].Hp, sides[1].Hp, startHp, events);
        }

        private static void TickAttacks(BattleSide side, BattleTeam team, int[,] timers, int[,] attackCounts, int tick, SideState[] sides, List<BattleEvent> events)
        {
            int s = (int)side;
            SideState enemy = sides[1 - s];
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                BattleUnit unit = team[slot];
                if (unit.IsEmpty) continue;
                timers[s, slot]++;
                if (timers[s, slot] < unit.AttackIntervalTicks) continue;
                timers[s, slot] = 0;
                attackCounts[s, slot]++;

                long damage = unit.Attack;
                BattleAbility ab = unit.Ability;
                bool fires = !ab.IsNone && (ab.Trigger == AbilityTrigger.OnAttack
                    || (ab.Trigger == AbilityTrigger.EveryNthAttack && ab.EveryN > 0 && attackCounts[s, slot] % ab.EveryN == 0));
                if (fires) ApplyAbility(tick, side, slot, ab, sides, events, ref damage);

                damage = Math.Min(damage, int.MaxValue);
                enemy.IncomingDamage += damage;
                events.Add(new BattleEvent(tick, BattleEventType.Attack, side, slot, (int)damage, 0));
            }
        }

        /// <summary>능력 하나 적용. damage는 공격 중일 때 이번 공격 피해 (전투 시작이면 쓰지 않음).</summary>
        private static void ApplyAbility(int tick, BattleSide side, int slot, BattleAbility ab, SideState[] sides, List<BattleEvent> events, ref long damage)
        {
            SideState mine = sides[(int)side];
            SideState enemy = sides[1 - (int)side];
            switch (ab.Effect)
            {
                case AbilityEffect.BonusDamage:
                    damage += ab.Amount;
                    break;
                case AbilityEffect.DamageMultiplier:
                    damage *= ab.Amount;
                    break;
                case AbilityEffect.Burn:
                    enemy.Burn = ClampAdd(enemy.Burn, ab.Amount);
                    break;
                case AbilityEffect.Poison:
                    enemy.Poison = ClampAdd(enemy.Poison, ab.Amount);
                    break;
                case AbilityEffect.Shield:
                    mine.Shield = ClampAdd(mine.Shield, ab.Amount);
                    mine.ShieldGained = true;
                    break;
                case AbilityEffect.Heal:
                    mine.IncomingHeal += ab.Amount;
                    break;
                default:
                    return; // 상점 단계 능력은 전투에서 무시
            }
            events.Add(new BattleEvent(tick, BattleEventType.Ability, side, slot, ab.Amount, (int)ab.Effect));
        }

        /// <summary>틱 끝: 방어막을 얻었으면 남은 방어막, 상태이상 스택이 바뀌었으면 현재 스택을 기록 (화면 표시용).</summary>
        private static void EndOfTickRecords(int tick, SideState[] sides, List<BattleEvent> events)
        {
            for (int s = 0; s < 2; s++)
            {
                SideState st = sides[s];
                if (st.ShieldGained)
                    events.Add(new BattleEvent(tick, BattleEventType.ShieldChanged, (BattleSide)s, -1, 0, st.Shield));
                if (st.Burn != st.BurnAtTickStart || st.Poison != st.PoisonAtTickStart)
                    events.Add(new BattleEvent(tick, BattleEventType.StatusChanged, (BattleSide)s, -1, st.Burn, st.Poison));
            }
        }

        private static int ClampAdd(int a, int b) => (int)Math.Min((long)a + b, int.MaxValue);

        private static BattleResult Finish(BattleOutcome outcome, int tick, int hpA, int hpB, int startHp, List<BattleEvent> events)
        {
            events.Add(new BattleEvent(tick, BattleEventType.End, BattleSide.A, -1, 0, (int)outcome));
            return new BattleResult(outcome, tick, hpA, hpB, startHp, events);
        }
    }
}
