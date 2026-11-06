using Moa.Showdown.Game.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Showdown.Editor.Setup
{
    /// <summary>판 진행 수치 에셋. 이미 있으면 값을 덮어쓰지 않는다. 아래 기본값 = BALANCE 5 · 9.</summary>
    public static class RunSetup
    {
        public const string RunBalancePath = "Assets/_Project/Data/RunBalance.asset";

        [MenuItem("MoaShowdown/Setup/Run Balance")]
        public static void CreateRunBalance()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RunBalance>(RunBalancePath);
            if (existing != null)
            {
                Debug.Log($"[Setup] {RunBalancePath} 이미 있음 — 값은 그대로 둠");
                return;
            }

            var r = ScriptableObject.CreateInstance<RunBalance>();
            r.winsToChampion = 10;
            r.startLives = 10;
            // 1~4라운드 1 · 5~9라운드 2 · 10라운드~ 3 (임시)
            r.lifeLossByRound = new[] { 1, 1, 1, 1, 2, 2, 2, 2, 2, 3 };
            r.startTrainerId = "debug_trainer";
            // 시작 팀 없음 — 1라운드 골드로 상점에서 산다 (2-2)
            // 상점 · 골드 (BALANCE 10): 수입 30 → 35 · 40 · 45 · 50 … (+5씩), 가격은 원작 값 (사용자 2026-11-06), 판매 75%는 추정
            r.startGold = 30;
            r.firstIncome = 35;
            r.incomeStep = 5;
            r.shopSlots = 5;
            r.rerollCost = 3;
            r.priceByRarity = new[] { 10, 20, 30, 40, 50 };
            r.sellPercent = 75;
            r.mergeCountByLevel = new[] { 3, 2 }; // Lv1 × 3 → Lv2, Lv2 × 2 → Lv3 (사용자)
            r.maxLevel = 3; // 합체로는 Lv3까지 (사용자)
            // 로비 (BALANCE 13)
            r.playerCount = 8;
            r.recentOpponentMemory = 3;
            AssetDatabase.CreateAsset(r, RunBalancePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {RunBalancePath} 생성");
        }
    }
}
