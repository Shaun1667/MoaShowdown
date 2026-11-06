using System;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Core.Content
{
    /// <summary>스냅샷은 형식은 맞지만 이 게임 데이터로 싸울 수 없음 (모르는 몬스터 · 터무니없는 수치 등).</summary>
    public sealed class SnapshotContentException : Exception
    {
        public SnapshotContentException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 팀 스냅샷 → 전투 팀 한 곳 (능력치 계산).
    /// 공격력 = 기본 × 배율^(레벨−1) + 공격 버프, 공격 간격 = 기본 + 간격 버프 (최소 1틱).
    /// 스냅샷은 남이 만든 데이터 — 계산은 long으로 하고 상한을 넘으면 거부한다.
    /// </summary>
    public static class TeamFactory
    {
        /// <summary>능력치 안전 상한 (넘침 방지용, 밸런스 값 아님).</summary>
        public const int StatCap = 1_000_000_000;

        public static BattleTeam Build(TeamSnapshot snapshot, SpeciesCatalog catalog, UnitStatRules rules)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.Validate();

            var team = new BattleTeam();
            for (int slot = 0; slot < BattleTeam.SlotCount; slot++)
            {
                MonsterSnapshot m = snapshot.Board[slot];
                if (m == null) continue;
                if (!catalog.TryGet(m.SpeciesId, out SpeciesDef def))
                    throw new SnapshotContentException($"board[{slot}]: 모르는 몬스터 '{m.SpeciesId}'");

                long attack = def.Attack;
                for (int lv = 1; lv < m.Level && rules.LevelAttackMultiplier > 1; lv++)
                {
                    attack *= rules.LevelAttackMultiplier;
                    if (attack > StatCap) throw new SnapshotContentException($"board[{slot}]: 레벨 {m.Level} 공격력 상한 초과");
                }

                long interval = def.AttackIntervalTicks;
                foreach (StatBuff b in m.Buffs)
                {
                    if (b.Stat == StatIds.Attack) attack += b.Value;
                    else if (b.Stat == StatIds.Interval) interval += b.Value;
                    // 모르는 능력치 ID는 무시 (새 버전 클라이언트가 만든 버프일 수 있음)
                }

                if (attack < 0) attack = 0;
                if (attack > StatCap) throw new SnapshotContentException($"board[{slot}]: 공격력 상한 초과");
                if (interval < 1) interval = 1;
                if (interval > StatCap) throw new SnapshotContentException($"board[{slot}]: 공격 간격 상한 초과");

                team[slot] = new BattleUnit((int)attack, (int)interval);
            }
            return team;
        }

        /// <summary>이 데이터로 싸울 수 있는 스냅샷인가 (상대 고를 때 거르기용).</summary>
        public static bool IsPlayable(TeamSnapshot snapshot, SpeciesCatalog catalog, UnitStatRules rules)
        {
            try
            {
                Build(snapshot, catalog, rules);
                return true;
            }
            catch (SnapshotContentException)
            {
                return false;
            }
        }
    }
}
