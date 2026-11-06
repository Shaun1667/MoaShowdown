using System.Collections.Generic;
using System.Globalization;
using Moa.Showdown.Core.Battle;
using Moa.Showdown.Core.Content;
using Moa.Showdown.Core.Snapshot;

namespace Moa.Showdown.Game.Ui
{
    /// <summary>
    /// 정보 창 글 만들기 한 곳 (문구는 <see cref="HudText"/>). 숫자는 규칙 코드가 계산한 값을 받아 그대로 쓴다 — 여기서 다시 계산하지 않는다.
    /// </summary>
    public static class InfoText
    {
        /// <summary>몬스터 한 마리 정보에 필요한 값.</summary>
        public sealed class MonsterInfo
        {
            public SpeciesDef Species;
            public int Level;
            /// <summary>규칙 코드(TeamFactory.BuildUnit)가 계산한 실제 능력치.</summary>
            public BattleUnit Unit;
            public int AttackBuff;
            public int LevelMultiplier;
            public int TicksPerSecond;
            public int? Price;
            public int? SellValue;
            /// <summary>합체 정보: 같은 몬스터 · 같은 레벨 보유 수 (이 몬스터 포함). null이면 표시 안 함.</summary>
            public int? OwnedSameLevel;
            public int MergeCount;
            public int MaxLevel;
            public bool Enemy;
        }

        public static string MonsterTitle(MonsterInfo i) =>
            string.Format(HudText.InfoMonsterTitleFormat, i.Species.DisplayName, i.Level, i.Enemy ? HudText.InfoEnemyTag : "");

        public static string MonsterBody(MonsterInfo i)
        {
            var lines = new List<string>
            {
                string.Format(HudText.InfoRarityFormat, RarityName(i.Species.Rarity)),
                string.Format(HudText.InfoAttackFormat, i.Unit.Attack, i.Species.Attack, i.LevelMultiplier, i.AttackBuff),
                string.Format(HudText.InfoIntervalFormat, Seconds(i.Unit.AttackIntervalTicks, i.TicksPerSecond)),
                string.Format(HudText.InfoDpsFormat, Dps(i.Unit, i.TicksPerSecond)),
            };
            if (i.Price.HasValue) lines.Add(string.Format(HudText.InfoPriceFormat, i.Price.Value));
            if (i.SellValue.HasValue) lines.Add(string.Format(HudText.InfoSellFormat, i.SellValue.Value));
            if (i.OwnedSameLevel.HasValue)
            {
                lines.Add(i.Level >= i.MaxLevel
                    ? HudText.InfoMaxLevel
                    : string.Format(HudText.InfoMergeFormat, i.Level, i.OwnedSameLevel.Value, i.MergeCount));
            }
            return string.Join("\n", lines);
        }

        public static string OpponentBody(TeamSnapshot s, bool isBot, string source, int monsterCount)
        {
            var lines = new List<string>
            {
                isBot ? HudText.InfoBot : HudText.InfoPlayer,
                string.Format(HudText.InfoRunIdFormat, s.RunId),
                string.Format(HudText.InfoTrainerFormat, string.IsNullOrEmpty(s.TrainerId) ? "—" : s.TrainerId),
                string.Format(HudText.InfoWinsLivesLostFormat, s.Wins, s.LivesLost),
                string.Format(HudText.InfoMonsterCountFormat, monsterCount),
            };
            if (!string.IsNullOrEmpty(source)) lines.Add(string.Format(HudText.InfoSourceFormat, source));
            return string.Join("\n", lines);
        }

        public static string MyBody(string trainerId, int wins, int winsGoal, int lives, int startLives, int gold, int round)
        {
            return string.Join("\n", new[]
            {
                string.Format(HudText.InfoTrainerFormat, string.IsNullOrEmpty(trainerId) ? "—" : trainerId),
                string.Format(HudText.InfoRoundFormat, round),
                string.Format(HudText.RunInfoFormat, wins, winsGoal, lives, startLives),
                string.Format(HudText.GoldFormat, gold),
            });
        }

        public static string RarityName(Rarity r)
        {
            int i = (int)r;
            return i >= 0 && i < HudText.RarityNames.Length ? HudText.RarityNames[i] : r.ToString();
        }

        /// <summary>초당 피해 = 공격력 × 1초당 틱 / 공격 간격 (소수 한 자리).</summary>
        public static string Dps(BattleUnit u, int ticksPerSecond) =>
            ((double)u.Attack * ticksPerSecond / u.AttackIntervalTicks).ToString("0.0", CultureInfo.InvariantCulture);

        public static string Seconds(int ticks, int ticksPerSecond) =>
            ((double)ticks / ticksPerSecond).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
