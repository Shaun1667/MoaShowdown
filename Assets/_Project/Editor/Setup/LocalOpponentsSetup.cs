using System.Collections.Generic;
using System.IO;
using Moa.Showdown.Core.Random;
using Moa.Showdown.Core.Snapshot;
using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>
    /// 로컬 상대 샘플 파일 (밸런스 아님 — 상대 고르기 · 화면 확인용).
    /// StreamingAssets/Opponents/sample_opponents.json = 1~14라운드 × 3팀, StreamingAssets/OpponentBots/bots.json = 봇 5팀.
    /// 고정 시드로 만들어 몇 번 실행해도 같은 파일. 이 두 파일만 덮어쓴다 — 손으로 만든 상대는 다른 파일 이름으로 같은 폴더에 둔다.
    /// </summary>
    public static class LocalOpponentsSetup
    {
        public const string PlayerDir = "Assets/StreamingAssets/Opponents";
        public const string BotDir = "Assets/StreamingAssets/OpponentBots";
        private const ulong GeneratorSeed = 20261106;
        private static readonly int[] FillOrder = { 0, 3, 1, 4, 2, 5 }; // 가운데 칸부터 위 · 아래 번갈아

        [MenuItem("MoaShowdown/Setup/Local Opponents")]
        public static void CreateLocalOpponents()
        {
            SpeciesSetup.CreateSpeciesTable();
            BalanceSetup.CreateBattleBalance();
            var species = AssetDatabase.LoadAssetAtPath<SpeciesTable>(SpeciesSetup.SpeciesTablePath);
            var balance = AssetDatabase.LoadAssetAtPath<BattleBalance>(BalanceSetup.BattleBalancePath);
            var rng = new DeterministicRandom(GeneratorSeed);

            var players = new List<TeamSnapshot>();
            for (int round = 1; round <= 14; round++)
            for (int k = 0; k < 3; k++)
                players.Add(Make(species, balance.rulesVersion, rng, round, $"sample_r{round:00}_{k}", "sample", Mathf.Max(0, Mathf.Min(round - 1, (round - 1) / 2 + k - 1))));

            var bots = new List<TeamSnapshot>();
            foreach (int round in new[] { 1, 4, 7, 10, 13 })
                bots.Add(Make(species, balance.rulesVersion, rng, round, $"bot_r{round:00}", "bot", 0));

            Write(PlayerDir, "sample_opponents.json", players);
            Write(BotDir, "bots.json", bots);
            AssetDatabase.Refresh();
            Debug.Log($"[Setup] 로컬 상대 {players.Count}팀 · 봇 {bots.Count}팀 생성");
        }

        private static TeamSnapshot Make(SpeciesTable species, int rulesVersion, DeterministicRandom rng, int round, string runId, string trainer, int wins)
        {
            var s = new TeamSnapshot
            {
                RulesVersion = rulesVersion,
                RunId = runId,
                Round = round,
                Wins = wins,
                LivesLost = 0,
                TrainerId = trainer,
            };
            int count = Mathf.Min(6, 2 + (round - 1) / 2);
            int level = 1 + (round - 1) / 3;
            for (int i = 0; i < count; i++)
            {
                SpeciesRow row = species.rows[rng.NextInt(species.rows.Count)];
                s.Board[FillOrder[i]] = new MonsterSnapshot { SpeciesId = row.id, Level = level };
            }
            return s;
        }

        private static void Write(string dir, string file, List<TeamSnapshot> list)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, file), TeamSnapshotJson.SerializeMany(list));
        }
    }
}
